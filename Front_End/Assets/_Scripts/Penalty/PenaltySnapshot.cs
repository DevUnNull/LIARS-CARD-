// Dữ liệu trạng thái server gửi về
// DTO
using System;

namespace LiarsCard.Penalty
{
    [Serializable]
    public sealed class PenaltySnapshot
    {
        public string PenaltyId; // ID duy nhất của một hình phạt
        public PenaltyState State; // Trạng thái nghiệp vụ do server xác nhận. phải nằm trong bốn giá trị enum của PenaltyState
        public long Revision; // Phiên bản tăng dần của các cập nhật cho cùng một hình phạt. Xử lý thông báo đến trễ
                              // bắt đầu từ 1, tăng đơn điệu trong phạm vi một PenaltyId. Không reset khi Unity reconnect.
        public long UpdatedAtUnixMs; // thời điểm server cập nhật, tính bằng Unix timestamp mili giây UTC; chỉ dùng cho log và debug.

        // Server sẽ trả về:
        //{
        //  "PenaltyId": "penalty-8f31",
        //  "State": 2, // State 2 tương ứng với Resolving
        //  "Revision": 3,
        //  "UpdatedAtUnixMs": 1792405200000
        //}

        // lưu ý: code trên (test) chỉ để dùng làm Response (Server trả về cho Client hoặc Swagger) chưa đủ để làm request body
        // sau khi có server mới tích hợp id, Revision và Update lên.
    }
}
