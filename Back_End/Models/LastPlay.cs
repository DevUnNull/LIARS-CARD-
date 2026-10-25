namespace Back_End.Models
{
    public class LastPlay
    {
        public string PlayId { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public int CardCount { get; set; }
        public List<Card> Cards { get; set; } = new();
        public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
        public bool Resolved { get; set; } = false;
        public bool? WasBluff { get; set; }
    }
}
