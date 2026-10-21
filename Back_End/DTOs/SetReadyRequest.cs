using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class SetReadyRequest
    {
        [Required]
        public bool? Ready { get; set; }
    }
}
