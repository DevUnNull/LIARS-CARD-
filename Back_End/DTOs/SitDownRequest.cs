using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class SitDownRequest
    {
        [Required]
        [Range(0, 3)]
        public int? SeatIndex { get; set; }
    }
}
