namespace Back_End.Models
{
    public class TableSeat
    {
        public int SeatIndex { get; init; }
        public string? PlayerId { get; set; }
        public bool IsReady { get; set; }
    }
}
