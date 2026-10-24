namespace Back_End.Models
{
    public class GameMatch
    {
        public string MatchId { get; set; } = string.Empty;
        public int RoundNumber { get; set; } = 0;
        public string TableRank { get; set; } = string.Empty;
        public string? CurrentTurnPlayerId { get; set; }
        public RoundPhase Phase { get; set; } = RoundPhase.WaitingForPlayers;
        public int StateVersion { get; set; } = 1;
        public List<MatchPlayer> Players { get; set; } = new();
        public LastPlay? LastPlay { get; set; }
        public string? WinnerPlayerId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public MatchPlayer? FindPlayer(string playerId)
        {
            return Players.Find(p => p.PlayerId == playerId);
        }

        public List<MatchPlayer> AlivePlayers => Players.FindAll(p => p.IsAlive);
    }
}
