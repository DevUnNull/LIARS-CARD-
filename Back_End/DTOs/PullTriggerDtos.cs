using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class PullTriggerRequest
    {
        [Required]
        public Guid ActionId { get; set; }

        [Required]
        public int ExpectedVersion { get; set; }
    }

    public class PullTriggerResponse
    {
        public string MatchId { get; set; } = string.Empty;
        public string PenalizedPlayerId { get; set; } = string.Empty;
        public bool IsBulletHit { get; set; }
        public bool Survived { get; set; }
        public bool Eliminated { get; set; }
        public string NextPhase { get; set; } = string.Empty;
        public string? WinnerPlayerId { get; set; }
        public int StateVersion { get; set; }
        public string? PenaltyEventId { get; set; }
    }
}
