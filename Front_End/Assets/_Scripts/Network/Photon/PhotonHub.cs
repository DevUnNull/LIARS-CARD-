using System;
using System.Collections.Generic;
using Fusion;
using LiarsCard.Server;
using LiteNetLib.Utils;
using UnityEngine;

/// <summary>
/// Object mạng chung của phòng (Master Client Object): Master Client luôn giữ quyền, chạy GameServer.
/// Mọi request / gói tin đi qua 2 RPC Up (client → master) và Down (master → 1 người), chia nhỏ
/// vì RPC Fusion tối đa 512 byte. Đổi Master Client thì server mới khởi động từ đầu (Epoch tăng).
/// </summary>
public class PhotonHub : NetworkBehaviour, IStateAuthorityChanged, IPlayerLeft
{
    public static PhotonHub Instance { get; private set; }

    /// <summary>Tăng mỗi lần một Master Client mới khởi động server (client thấy đổi thì vào lại).</summary>
    [Networked] public int Epoch { get; set; }

    private const byte KindRequest = 0, KindPacket = 1, KindResponse = 0;
    // Base64 4/3 + header RPC phải nằm gọn trong 512 byte kể cả khi chuỗi mã hoá 2 byte/ký tự
    private const int ChunkBytes = 150;

    private GameServer server;
    private readonly Dictionary<PlayerRef, string> idOf = new Dictionary<PlayerRef, string>();
    private readonly Dictionary<string, PlayerRef> refOf = new Dictionary<string, PlayerRef>();
    private readonly Dictionary<(PlayerRef, int), string[]> partial = new Dictionary<(PlayerRef, int), string[]>();
    private int nextMessageId;

    public override void Spawned()
    {
        Instance = this;
        if (HasStateAuthority) StartServer();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    public void StateAuthorityChanged()
    {
        if (HasStateAuthority) StartServer();
    }

    private void StartServer()
    {
        Debug.Log("[PhotonHub] Máy này là chủ phòng: khởi động GameServer.");
        server = new GameServer(SendToPlayer, PositionOf);
        idOf.Clear();
        refOf.Clear();
        Epoch++;
    }

    private static Vector3? PositionOf(string playerId) => NetPlayer.TryGet(playerId, out var p) ? p.Pos : (Vector3?)null;

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority || server == null || !idOf.TryGetValue(player, out var id)) return;
        idOf.Remove(player);
        refOf.Remove(id);
        server.PlayerLeft(id);
    }

    // ───────────── Đăng ký người chơi ─────────────

    /// <summary>Báo tên cho server. playerId lấy từ UserId Photon của người gửi nên không giả mạo được.</summary>
    public void SendHello(string displayName)
    {
        if (HasStateAuthority) OnHello(Runner.LocalPlayer, displayName);
        else RPC_Hello(displayName);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Hello(string displayName, RpcInfo info = default) => OnHello(info.Source, displayName);

    private void OnHello(PlayerRef player, string displayName)
    {
        if (server == null) return;
        string id = Runner.GetPlayerUserId(player);
        if (string.IsNullOrEmpty(id)) return;
        if (displayName != null && displayName.Length > AuthApiClient.MaxNameLength) displayName = displayName.Substring(0, AuthApiClient.MaxNameLength);
        idOf[player] = id;
        refOf[id] = player;
        server.PlayerJoined(id, displayName);
    }

    // ───────────── Client → Master ─────────────

    public void SendRequest(int requestId, string route, string json)
    {
        var w = new NetDataWriter();
        w.Put(KindRequest); w.Put(requestId); w.Put(route); w.Put(json ?? "");
        Up(w.CopyData());
    }

    public void SendPacket(byte[] data)
    {
        var w = new NetDataWriter();
        w.Put(KindPacket); w.Put(data);
        Up(w.CopyData());
    }

    private void Up(byte[] data)
    {
        if (HasStateAuthority) { OnUp(Runner.LocalPlayer, data); return; }
        int msg = ++nextMessageId;
        var parts = Split(data);
        for (int i = 0; i < parts.Length; i++) RPC_Up(msg, (short)i, (short)parts.Length, parts[i]);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Up(int msg, short part, short parts, string chunk, RpcInfo info = default)
    {
        var data = Join(info.Source, msg, part, parts, chunk);
        if (data != null) OnUp(info.Source, data);
    }

    private void OnUp(PlayerRef from, byte[] data)
    {
        if (server == null || !idOf.TryGetValue(from, out var playerId)) return;   // chưa Hello hoặc server vừa đổi
        var r = new NetDataReader(data);
        if (r.GetByte() == KindRequest)
        {
            int requestId = r.GetInt();
            string route = r.GetString();
            string json = r.GetString();
            var (ok, body) = server.Handle(playerId, route, json);
            var w = new NetDataWriter();
            w.Put(KindResponse); w.Put(requestId); w.Put(ok); w.Put(body ?? "");
            Down(from, w.CopyData());
        }
        else server.HandlePacket(playerId, r.GetRemainingBytes());
    }

    // ───────────── Master → 1 người ─────────────

    private void SendToPlayer(string playerId, byte[] packet)
    {
        if (!refOf.TryGetValue(playerId, out var player)) return;
        var w = new NetDataWriter();
        w.Put(KindPacket); w.Put(packet);
        Down(player, w.CopyData());
    }

    private void Down(PlayerRef to, byte[] data)
    {
        if (to == Runner.LocalPlayer) { OnDown(data); return; }
        int msg = ++nextMessageId;
        var parts = Split(data);
        for (int i = 0; i < parts.Length; i++) RPC_Down(to, msg, (short)i, (short)parts.Length, parts[i]);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Down([RpcTarget] PlayerRef target, int msg, short part, short parts, string chunk)
    {
        var data = Join(PlayerRef.None, msg, part, parts, chunk);
        if (data != null) OnDown(data);
    }

    private static void OnDown(byte[] data)
    {
        var r = new NetDataReader(data);
        if (r.GetByte() == KindResponse)
        {
            int requestId = r.GetInt();
            bool ok = r.GetBool();
            GameLink.OnResponse(requestId, ok, r.GetString());
        }
        else GameLink.OnPacket(r.GetRemainingBytes());
    }

    // ───────────── Chia nhỏ / ghép lại ─────────────

    private static string[] Split(byte[] data)
    {
        int n = Math.Max(1, (data.Length + ChunkBytes - 1) / ChunkBytes);
        var parts = new string[n];
        for (int i = 0; i < n; i++)
        {
            int off = i * ChunkBytes;
            parts[i] = Convert.ToBase64String(data, off, Math.Min(ChunkBytes, data.Length - off));
        }
        return parts;
    }

    private byte[] Join(PlayerRef from, int msg, short part, short parts, string chunk)
    {
        if (parts <= 1) return Convert.FromBase64String(chunk);
        if (part < 0 || part >= parts || parts > 256) return null;   // chặn gói rác làm tràn bộ nhớ
        var key = (from, msg);
        if (!partial.TryGetValue(key, out var buf) || buf.Length != parts) partial[key] = buf = new string[parts];
        buf[part] = chunk;
        foreach (var s in buf) if (s == null) return null;
        partial.Remove(key);

        var all = new List<byte>();
        foreach (var s in buf) all.AddRange(Convert.FromBase64String(s));
        return all.ToArray();
    }
}
