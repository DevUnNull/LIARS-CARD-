using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Cầu nối giữa client và GameServer (chạy trên Master Client). Client chỉ gọi Request / SendPacket;
/// lớp mạng (PhotonLink) tự đăng ký vào <see cref="Transport"/> và đẩy phản hồi / gói tin về qua
/// <see cref="OnResponse"/> / <see cref="OnPacket"/>.
/// </summary>
public static class GameLink
{
    public interface ITransport
    {
        bool IsConnected { get; }
        Task ConnectAsync(string playerId, string displayName);
        void Disconnect();
        void SendRequest(int requestId, string route, string json);
        void SendPacket(byte[] data);
    }

    public static ITransport Transport;
    public static bool IsConnected => Transport != null && Transport.IsConnected;

    /// <summary>Gói tin server gửi về (định dạng giống gói LiteNetLib cũ, byte đầu là loại gói).</summary>
    public static event Action<byte[]> PacketReceived;

    private static readonly Dictionary<int, Action<bool, string>> pending = new Dictionary<int, Action<bool, string>>();
    private static int nextId;

    /// <summary>Gửi request tới server, đợi phản hồi (ok, body). Lỗi có dạng "[CODE] message".</summary>
    public static IEnumerator Request(string route, object args, Action<bool, string> done, float timeoutSeconds = 10f)
    {
        if (!IsConnected)
        {
            done?.Invoke(false, "[NOT_CONNECTED] Chưa kết nối vào phòng.");
            yield break;
        }

        int id = ++nextId;
        bool finished = false, ok = false;
        string body = null;
        pending[id] = (o, b) => { finished = true; ok = o; body = b; };
        Transport.SendRequest(id, route, args == null ? "" : JsonUtility.ToJson(args));

        float until = Time.realtimeSinceStartup + timeoutSeconds;
        while (!finished && Time.realtimeSinceStartup < until) yield return null;
        pending.Remove(id);

        if (!finished) done?.Invoke(false, "[TIMEOUT] Không nhận được phản hồi từ chủ phòng.");
        else done?.Invoke(ok, body);
    }

    public static void SendPacket(byte[] data)
    {
        if (IsConnected) Transport.SendPacket(data);
    }

    public static void OnResponse(int requestId, bool ok, string body)
    {
        if (pending.TryGetValue(requestId, out var cb)) cb(ok, body);
    }

    public static void OnPacket(byte[] data) => PacketReceived?.Invoke(data);

    /// <summary>Mất chủ phòng / rớt mạng: huỷ mọi request đang chờ.</summary>
    public static void FailAllPending(string reason)
    {
        foreach (var cb in new List<Action<bool, string>>(pending.Values)) cb(false, reason);
        pending.Clear();
    }
}
