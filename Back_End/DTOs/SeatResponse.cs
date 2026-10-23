namespace Back_End.DTOs
{
    public class SeatResponse
    {
        public int SeatIndex { get; set; }
        public string? PlayerId { get; set; }
        public bool IsReady { get; set; }
    }
}
