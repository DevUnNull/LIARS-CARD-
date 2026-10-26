// Định nghĩa enum trạng thái

namespace LiarsCard.Penalty
{
    public enum PenaltyState
    {
        Pending = 0, // Server đã xác nhận hình phạt hợp lệ, đang chờ thực thi.
        Animating = 1, // Server xác nhận giai đoạn trình diễn; Unity hiển thị animation.
        Resolving = 2, // Server đang xử lý hiệu lực gameplay của hình phạt.
        Completed = 3 // Server xác nhận hình phạt đã hoàn tất.
    }
}

