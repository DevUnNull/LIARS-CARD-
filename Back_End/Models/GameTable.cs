namespace Back_End.Models
{
    public class GameTable
    {
        public int Id { get; init; }
        public TableStatus Status { get; set; } = TableStatus.Waiting;
        public string? HostPlayerId { get; set; }
        public string? CurrentMatchId { get; set; }
        public List<TableSeat> Seats { get; init; } = new();
    }

    public enum TableStatus
    {
        Waiting,
        Playing,
        Summary
    }
}
