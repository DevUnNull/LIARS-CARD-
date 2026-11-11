using System.Collections.Generic;

/// <summary>Tên hiển thị theo playerId, lấy từ ghế bàn và trạng thái trận server gửi về.</summary>
public static class PlayerNames
{
    private static readonly Dictionary<string, string> names = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

    public static void Remember(string playerId, string displayName)
    {
        if (!string.IsNullOrEmpty(playerId) && !string.IsNullOrWhiteSpace(displayName) && displayName != playerId)
            names[playerId] = displayName;
    }

    /// <summary>Tên của người chơi; chưa biết tên thì "Người chơi" (không lộ ID).</summary>
    public static string Of(string playerId) =>
        !string.IsNullOrEmpty(playerId) && names.TryGetValue(playerId, out var n) ? n : "Người chơi";
}
