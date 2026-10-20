using System.ComponentModel.DataAnnotations;

namespace Back_End.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên người chơi không được để trống")]
        [MaxLength(50)]
        public string DisplayName { get; set; } = string.Empty;
    }
}
