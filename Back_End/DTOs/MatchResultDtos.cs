namespace Back_End.DTOs
{
    public class MatchResultResponse
    {
        public string MatchId { get; set; } = string.Empty;
        public string? WinnerId { get; set; }
        public List<string> ParticipantIds { get; set; } = new();
        public int TotalRounds { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
    }
}

