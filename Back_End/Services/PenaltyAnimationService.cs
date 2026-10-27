using Back_End.DTOs;
using Back_End.Infrastructure.MatchState;
using Back_End.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Back_End.Services
{
    public sealed class PenaltyAnimationService : IPenaltyAnimationService
    {
        private readonly IMatchStore _matchStore;
        private readonly IPenaltyAnimationNotifier _notifier;
        private readonly TimeProvider _timeProvider;
        private readonly PenaltyAnimationOptions _options;
        private readonly ILogger<PenaltyAnimationService> _logger;

        public PenaltyAnimationService(
            IMatchStore matchStore,
            IPenaltyAnimationNotifier notifier,
            TimeProvider timeProvider,
            IOptions<PenaltyAnimationOptions> options,
            ILogger<PenaltyAnimationService> logger)
        {
            _matchStore = matchStore;
            _notifier = notifier;
            _timeProvider = timeProvider;
            _options = options.Value;
            _logger = logger;
        }

        public PenaltyAnimationSession CreateSession(
            GameMatch match,
            Guid penaltyEventId,
            PenaltyResult penaltyResult)
        {
            var createdAt = _timeProvider.GetUtcNow();
            var session = new PenaltyAnimationSession(
                match.MatchId,
                penaltyEventId,
                match.Players.Select(player => player.PlayerId),
                createdAt,
                createdAt.AddSeconds(_options.AckTimeoutSeconds),
                penaltyResult.ChallengeSucceeded,
                penaltyResult.PenalizedPlayerId,
                penaltyResult.Survived,
                penaltyResult.Eliminated,
                match.StateVersion);

            if (!_matchStore.TryCreatePenaltyAnimation(session))
            {
                throw new InvalidOperationException("Penalty event ID already exists for this match.");
            }

            return session;
        }

        public async Task PublishStartedAsync(
            PenaltyAnimationSession session,
            CancellationToken cancellationToken)
        {
            try
            {
                await _notifier.PublishStartedAsync(
                    session.ToStartedEvent(),
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to publish penalty animation start for {MatchId}/{PenaltyEventId}.",
                    session.MatchId,
                    session.PenaltyEventId);
            }
        }

        public async Task<PenaltyAnimationAckResponse> AcknowledgeAsync(
            string matchId,
            Guid penaltyEventId,
            string playerId,
            CancellationToken cancellationToken)
        {
            PenaltyAnimationAckResponse response;
            PenaltyAnimationEnded? endedEvent = null;

            using (await _matchStore.LockMatchAsync(matchId))
            {
                var match = _matchStore.GetMatch(matchId);
                if (match == null || match.FindPlayer(playerId) == null)
                {
                    throw new HubException("Player is not a member of this match.");
                }

                var session = _matchStore.GetPenaltyAnimation(matchId, penaltyEventId);
                if (session == null)
                {
                    throw new HubException("Penalty animation session was not found.");
                }

                if (!session.RequiresAcknowledgementFrom(playerId))
                {
                    throw new HubException("Player is not required to acknowledge this event.");
                }

                bool wasPending = session.Status == PenaltyAnimationStatus.Pending;
                var outcome = session.Acknowledge(playerId, _timeProvider.GetUtcNow());
                _matchStore.SavePenaltyAnimation(session);

                if (wasPending && session.Status != PenaltyAnimationStatus.Pending)
                {
                    endedEvent = session.ToEndedEvent();
                }

                response = new PenaltyAnimationAckResponse(
                    outcome == PenaltyAnimationAckOutcome.Accepted,
                    session.Status.ToString(),
                    session.AcknowledgedCount,
                    session.RequiredAckCount);
            }

            if (endedEvent != null)
            {
                await PublishEndedAsync(endedEvent, cancellationToken);
            }

            return response;
        }

        public async Task<IReadOnlyList<PenaltyAnimationSnapshot>> GetReconnectSnapshotAsync(
            string matchId,
            string playerId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (await _matchStore.LockMatchAsync(matchId))
            {
                var match = _matchStore.GetMatch(matchId);
                if (match == null || match.FindPlayer(playerId) == null)
                {
                    throw new HubException("Player is not a member of this match.");
                }

                var sessions = _matchStore.GetPenaltyAnimations(matchId)
                    .Where(session => session.RequiresAcknowledgementFrom(playerId))
                    .ToList();

                var snapshots = sessions
                    .Where(session => session.Status == PenaltyAnimationStatus.Pending)
                    .Select(session => session.ToSnapshot(playerId))
                    .ToList();

                var latestTerminalSession = sessions
                    .Where(session => session.Status != PenaltyAnimationStatus.Pending)
                    .MaxBy(session => session.CreatedAtUtc);

                if (latestTerminalSession != null)
                {
                    snapshots.Add(latestTerminalSession.ToSnapshot(playerId));
                }

                return snapshots
                    .OrderBy(snapshot => snapshot.Event.DeadlineUtc)
                    .ToArray();
            }
        }

        public async Task<bool> TryTimeoutAsync(
            string matchId,
            Guid penaltyEventId,
            CancellationToken cancellationToken)
        {
            PenaltyAnimationEnded? endedEvent = null;

            using (await _matchStore.LockMatchAsync(matchId))
            {
                var session = _matchStore.GetPenaltyAnimation(matchId, penaltyEventId);
                if (session == null)
                {
                    return false;
                }

                if (session.TryTimeout(_timeProvider.GetUtcNow()))
                {
                    _matchStore.SavePenaltyAnimation(session);
                    endedEvent = session.ToEndedEvent();
                }
            }

            if (endedEvent == null)
            {
                return false;
            }

            await PublishEndedAsync(endedEvent, cancellationToken);
            return true;
        }

        private async Task PublishEndedAsync(
            PenaltyAnimationEnded message,
            CancellationToken cancellationToken)
        {
            try
            {
                await _notifier.PublishEndedAsync(message, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to publish penalty animation completion for {MatchId}/{PenaltyEventId}.",
                    message.MatchId,
                    message.PenaltyEventId);
            }
        }
    }
}
