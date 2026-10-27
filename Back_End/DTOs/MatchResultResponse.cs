namespace Back_End.DTOs
{
    /// <summary>A public participant summary in a persisted match result.</summary>
    public sealed record MatchResultPlayerResponse(
        string PlayerId,
        string DisplayName,
        int SeatIndex,
        bool IsAliveAtEnd,
        bool IsWinner,
        int RemainingCardCountAtEnd);

    /// <summary>The persisted, non-secret result of a completed match.</summary>
    public sealed record MatchResultResponse(
        string MatchId,
        string? WinnerPlayerId,
        IReadOnlyList<MatchResultPlayerResponse> Players,
        DateTimeOffset MatchCreatedAtUtc,
        DateTimeOffset EndedAtUtc,
        int FinalStateVersion);
}
