using System.Collections.Concurrent;

namespace Back_End.Services
{
    /// <summary>Tên hiển thị (lúc đăng ký) theo playerId, ghi lại khi người chơi ngồi vào bàn.</summary>
    public static class PlayerNames
    {
        // shortcut: chỉ trong RAM, server restart thì người chơi phải ngồi lại mới có tên, đủ vì bàn/trận cũng chỉ trong RAM
        private static readonly ConcurrentDictionary<string, string> names = new();

        public static void Set(string playerId, string? displayName)
        {
            if (!string.IsNullOrWhiteSpace(displayName)) names[playerId] = displayName.Trim();
        }

        public static string? Get(string? playerId) =>
            playerId != null && names.TryGetValue(playerId, out var n) ? n : playerId;
    }
}
