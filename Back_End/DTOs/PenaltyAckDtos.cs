using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class PenaltyAckRequest
    {
        [Required]
        public Guid ActionId { get; set; }

        [Required]
        public string PenaltyEventId { get; set; } = string.Empty;
    }

    public class PenaltyAckResponse
    {
        public string MatchId { get; set; } = string.Empty;
        public string PenaltyEventId { get; set; } = string.Empty;
        public bool Accepted { get; set; } = true;
        public string NextPhase { get; set; } = string.Empty;
        public string? WinnerPlayerId { get; set; }
        public int StateVersion { get; set; }
    }
}

