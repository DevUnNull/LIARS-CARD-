using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class CreateLobbyRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Range(2, 4)]
        public int MaxPlayers { get; set; } = 4;
    }
}
