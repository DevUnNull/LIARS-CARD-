namespace Back_End.DTOs
{
    public sealed record PlayerEliminatedEvent(
        string MatchId,
        string PlayerId,
        int StateVersion);

    public sealed record MatchEndedEvent(
        string MatchId,
        int StateVersion,
        string? WinnerPlayerId);

    public sealed record RoundStartedEvent(
        string MatchId,
        Guid PenaltyEventId,
        int RoundNumber,
        int StateVersion,
        string Phase,
        string TableRank,
        string? CurrentTurnPlayerId);
}
