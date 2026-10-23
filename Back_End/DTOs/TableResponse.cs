using Back_End.Models;

namespace Back_End.DTOs
{
    public class TableResponse
    {
        public int TableId { get; set; }
        public TableStatus Status { get; set; }
        public int Capacity { get; set; }
        public int PlayerCount { get; set; }

        public List<SeatResponse> Seats { get; set; } = new();
    }
}
