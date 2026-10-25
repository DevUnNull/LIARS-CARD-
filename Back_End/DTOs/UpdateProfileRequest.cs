
using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "Tên người chơi không được để trống")]
        [StringLength(20, MinimumLength = 3,
            ErrorMessage = "Tên người chơi phải dài từ 3 đến 20 ký tự")]
        public string DisplayName { get; set; } = string.Empty;
    }
}
