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
        public string? PenalizedPlayerId { get; set; }
        public string? CurrentPenaltyEventId { get; set; }
        public bool PenaltyResolved { get; set; } = false;
        public DateTime? PenaltyResolvedAt { get; set; }
        public bool RoundTransitionHandled { get; set; } = false;
        public HashSet<string> PenaltyAckedPlayers { get; set; } = new();
        /// <summary>Những người đã chọn "Tin" lượt đánh cuối (khi người đánh đã hết bài). Reset mỗi vòng / khi có tố.</summary>
        public HashSet<string> TrustedPlayers { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public MatchPlayer? FindPlayer(string playerId)
        {
            return Players.Find(p => p.PlayerId == playerId);
        }

        public List<MatchPlayer> AlivePlayers => Players.FindAll(p => p.IsAlive);
    }
}
