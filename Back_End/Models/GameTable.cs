namespace Back_End.Models
{
    public class GameTable
    {
        public int Id { get; init; }
        public TableStatus Status { get; set; } = TableStatus.Waiting;
        public List<TableSeat> Seats { get; init; } = new();
    }

    public enum TableStatus
    {
        Waiting,
        Playing,
        Summary
    }
}
