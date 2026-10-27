using Back_End.Models;

namespace Back_End.DTOs
{
    public sealed record PenaltyAnimationStarted(
        string MatchId,
        Guid PenaltyEventId,
        int StateVersion,
        DateTimeOffset DeadlineUtc,
        bool ChallengeSucceeded,
        string PenalizedPlayerId,
        bool Survived,
        bool Eliminated);

    public sealed record PenaltyAnimationEnded(
        string MatchId,
        Guid PenaltyEventId,
        string Status,
        int AcknowledgedCount,
        int RequiredAckCount);

    public sealed record PenaltyAnimationSnapshot(
        PenaltyAnimationStarted Event,
        string Status,
        bool AcknowledgedByCaller,
        int AcknowledgedCount,
        int RequiredAckCount);

    public sealed record PenaltyAnimationAckResponse(
        bool Acknowledged,
        string Status,
        int AcknowledgedCount,
        int RequiredAckCount);

    public static class PenaltyAnimationDtoMapper
    {
        public static PenaltyAnimationStarted ToStartedEvent(this PenaltyAnimationSession session) =>
            new(
                session.MatchId,
                session.PenaltyEventId,
                session.StateVersion,
                session.DeadlineUtc,
                session.ChallengeSucceeded,
                session.PenalizedPlayerId,
                session.Survived,
                session.Eliminated);

        public static PenaltyAnimationEnded ToEndedEvent(this PenaltyAnimationSession session) =>
            new(
                session.MatchId,
                session.PenaltyEventId,
                session.Status.ToString(),
                session.AcknowledgedCount,
                session.RequiredAckCount);

        public static PenaltyAnimationSnapshot ToSnapshot(
            this PenaltyAnimationSession session,
            string playerId) =>
            new(
                session.ToStartedEvent(),
                session.Status.ToString(),
                session.HasAcknowledged(playerId),
                session.AcknowledgedCount,
                session.RequiredAckCount);
    }
}
