namespace Back_End.DTOs
{
    public class PlayCardsResponse
    {
        public bool Accepted { get; set; } = true;
        public string PlayId { get; set; } = string.Empty;
        public int PlayedCardCount { get; set; }
        public int RemainingCardCount { get; set; }
        public int StateVersion { get; set; }
    }
}
