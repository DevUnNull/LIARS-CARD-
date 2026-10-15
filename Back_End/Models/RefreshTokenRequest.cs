using System.ComponentModel.DataAnnotations;

namespace Back_End.Models
{
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
