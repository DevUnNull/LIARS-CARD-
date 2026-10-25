namespace Back_End.Models
{
    public class MatchPlayer
    {
        public string PlayerId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int SeatIndex { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool IsReady { get; set; } = false;
        public List<Card> Hand { get; set; } = new();
        public PlayerRevolver Revolver { get; set; } = new();

        public int RemainingCardCount => Hand.Count;
    }
}
