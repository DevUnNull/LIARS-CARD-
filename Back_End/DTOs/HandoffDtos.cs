using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class JoinMatchRequest
    {
        [Required]
        public string JoinTicket { get; set; } = string.Empty;
    }

    public class TableHandoffResponse
    {
        public string MatchId { get; set; } = string.Empty;
        public int TableId { get; set; }
        public string JoinTicket { get; set; } = string.Empty;
        public int ExpiresIn { get; set; } = 30;
    }
}

