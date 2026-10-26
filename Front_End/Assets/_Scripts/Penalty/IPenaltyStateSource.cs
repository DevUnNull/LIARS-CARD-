// Ranh giới giữa module và nguồn trạng thái server/mock
using System;

namespace LiarsCard.Penalty
{
    //Interface chịu trách nhiệm cung cấp snapshot hình phạt từ server hoặc nguồn giả lập cho Unity.
    //Interface không chứa code kết nối mạng. Phần triển khai thực tế sẽ đảm nhận việc nhận dữ liệu và chuyển thành PenaltySnapshot.
    public interface IPenaltyStateSource
    {
        event Action<PenaltySnapshot> SnapshotReceived;

        void Subscribe();
        void Unsubscribe();
    }
}