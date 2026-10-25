using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class PlayCardsRequest
    {
        [Required]
        public Guid ActionId { get; set; }

        [Required]
        public int ExpectedVersion { get; set; }

        [Required]
        public List<string> CardInstanceIds { get; set; } = new();
    }
}
