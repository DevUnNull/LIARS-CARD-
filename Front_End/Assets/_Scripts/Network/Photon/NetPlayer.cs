using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Đại diện mạng của mỗi người chơi (mỗi người tự spawn và giữ quyền). Chủ object ghi tư thế / skin / tên
/// từ NetworkManager mỗi tick; máy khác đọc ra và đẩy vào RemotePlayerManager như gói vị trí cũ.
/// Photon chỉ gửi phần thay đổi nên ngồi yên ở bàn gần như không tốn băng thông.
/// </summary>
public class NetPlayer : NetworkBehaviour
{
    private static readonly Dictionary<string, NetPlayer> byId = new Dictionary<string, NetPlayer>();

    [Networked] public NetworkString<_32> PlayerId { get; set; }
    [Networked] public NetworkString<_32> DisplayName { get; set; }
    [Networked] public Vector3 Pos { get; set; }
    [Networked] public float RotY { get; set; }
    [Networked] public float HeadYaw { get; set; }
    [Networked] public float HeadPitch { get; set; }
    [Networked] public byte Skin { get; set; }

    private string id;
    private string shownName;
    private int shownSkin = -1;

    public static bool TryGet(string playerId, out NetPlayer p) => byId.TryGetValue(playerId ?? "", out p) && p != null;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            PlayerId = AuthApiClient.Instance.CurrentUserId;
            DisplayName = AuthApiClient.Instance.DisplayName;
        }
        name = "NetPlayer_" + PlayerId;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (id == null) return;
        byId.Remove(id);
        if (!HasStateAuthority && RemotePlayerManager.Instance != null) RemotePlayerManager.Instance.RemoveRemotePlayer(id);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Pos = NetworkManager.LocalPosition;
        RotY = NetworkManager.LocalRotationY;
        HeadYaw = NetworkManager.LocalHeadYaw;
        HeadPitch = NetworkManager.LocalHeadPitch;
        Skin = NetworkManager.LocalSkin;
    }

    public override void Render()
    {
        if (id == null)
        {
            string pid = PlayerId.Value;
            if (string.IsNullOrEmpty(pid)) return;   // chưa nhận được dữ liệu đầu tiên
            id = pid;
            byId[id] = this;
        }
        if (HasStateAuthority) return;

        string n = DisplayName.Value;
        if (n != shownName) { shownName = n; PlayerNames.Remember(id, n); }

        var remotes = RemotePlayerManager.Instance;
        if (remotes == null || Pos == Vector3.zero) return;   // chủ object chưa gửi tư thế thật
        remotes.UpdateRemotePlayer(id, Pos, RotY, HeadYaw, HeadPitch);
        if (Skin != shownSkin) { shownSkin = Skin; remotes.SetRemoteSkin(id, Skin); }
    }
}
