namespace Back_End.Models
{
    public enum PenaltyAnimationStatus
    {
        Pending,
        Completed,
        TimedOut
    }

    public enum PenaltyAnimationAckOutcome
    {
        Accepted,
        Duplicate,
        TimedOut,
        AlreadyTerminal
    }

    public sealed class PenaltyAnimationSession
    {
        private readonly HashSet<string> _requiredPlayerIds;
        private readonly HashSet<string> _acknowledgedPlayerIds = new(StringComparer.Ordinal);

        public string MatchId { get; }
        public Guid PenaltyEventId { get; }
        public IReadOnlyCollection<string> RequiredPlayerIds => _requiredPlayerIds.ToArray();
        public int AcknowledgedCount => _acknowledgedPlayerIds.Count;
        public int RequiredAckCount => _requiredPlayerIds.Count;
        public DateTimeOffset CreatedAtUtc { get; }
        public DateTimeOffset DeadlineUtc { get; }
        public DateTimeOffset? CompletedAtUtc { get; private set; }
        public PenaltyAnimationStatus Status { get; private set; }
        public bool ChallengeSucceeded { get; }
        public string PenalizedPlayerId { get; }
        public bool Survived { get; }
        public bool Eliminated { get; }
        public int StateVersion { get; }

        public PenaltyAnimationSession(
            string matchId,
            Guid penaltyEventId,
            IEnumerable<string> requiredPlayerIds,
            DateTimeOffset createdAtUtc,
            DateTimeOffset deadlineUtc,
            bool challengeSucceeded,
            string penalizedPlayerId,
            bool survived,
            bool eliminated,
            int stateVersion)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            if (penaltyEventId == Guid.Empty)
            {
                throw new ArgumentException("Penalty event ID is required.", nameof(penaltyEventId));
            }

            if (deadlineUtc <= createdAtUtc)
            {
                throw new ArgumentOutOfRangeException(nameof(deadlineUtc));
            }

            MatchId = matchId;
            PenaltyEventId = penaltyEventId;
            _requiredPlayerIds = requiredPlayerIds
                .Where(playerId => !string.IsNullOrWhiteSpace(playerId))
                .ToHashSet(StringComparer.Ordinal);

            if (_requiredPlayerIds.Count == 0)
            {
                throw new ArgumentException("At least one player must be required to acknowledge.", nameof(requiredPlayerIds));
            }

            CreatedAtUtc = createdAtUtc;
            DeadlineUtc = deadlineUtc;
            ChallengeSucceeded = challengeSucceeded;
            PenalizedPlayerId = penalizedPlayerId;
            Survived = survived;
            Eliminated = eliminated;
            StateVersion = stateVersion;
            Status = PenaltyAnimationStatus.Pending;
        }

        public bool RequiresAcknowledgementFrom(string playerId) =>
            _requiredPlayerIds.Contains(playerId);

        public bool HasAcknowledged(string playerId) =>
            _acknowledgedPlayerIds.Contains(playerId);

        public PenaltyAnimationAckOutcome Acknowledge(string playerId, DateTimeOffset nowUtc)
        {
            if (!_requiredPlayerIds.Contains(playerId))
            {
                throw new InvalidOperationException("Player is not part of this penalty animation session.");
            }

            if (Status != PenaltyAnimationStatus.Pending)
            {
                return PenaltyAnimationAckOutcome.AlreadyTerminal;
            }

            if (nowUtc >= DeadlineUtc)
            {
                TransitionTo(PenaltyAnimationStatus.TimedOut, nowUtc);
                return PenaltyAnimationAckOutcome.TimedOut;
            }

            if (!_acknowledgedPlayerIds.Add(playerId))
            {
                return PenaltyAnimationAckOutcome.Duplicate;
            }

            if (_acknowledgedPlayerIds.Count == _requiredPlayerIds.Count)
            {
                TransitionTo(PenaltyAnimationStatus.Completed, nowUtc);
            }

            return PenaltyAnimationAckOutcome.Accepted;
        }

        public bool TryTimeout(DateTimeOffset nowUtc)
        {
            if (Status != PenaltyAnimationStatus.Pending || nowUtc < DeadlineUtc)
            {
                return false;
            }

            TransitionTo(PenaltyAnimationStatus.TimedOut, nowUtc);
            return true;
        }

        private void TransitionTo(PenaltyAnimationStatus status, DateTimeOffset atUtc)
        {
            if (Status != PenaltyAnimationStatus.Pending ||
                status == PenaltyAnimationStatus.Pending)
            {
                throw new InvalidOperationException("Penalty animation session is already terminal.");
            }

            Status = status;
            CompletedAtUtc = atUtc;
        }
    }
}
