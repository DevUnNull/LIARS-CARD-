using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class ChallengeRequest
    {
        [Required]
        public Guid ActionId { get; set; }

        [Required]
        public int ExpectedVersion { get; set; }

        [Required]
        public string TargetPlayId { get; set; } = string.Empty;
    }
}
