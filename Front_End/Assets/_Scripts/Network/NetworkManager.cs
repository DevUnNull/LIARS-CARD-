using System;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using UnityEngine;
using LiarsCard.Cards3D;

/// <summary>
/// Đầu mối mạng phía client. Vị trí / skin / tên của mình được đồng bộ qua Photon (NetPlayer đọc từ đây);
/// các sự kiện trận đấu là gói tin GameServer gửi về qua GameLink (cùng định dạng gói LiteNetLib cũ).
/// </summary>
public class NetworkManager : Singleton<NetworkManager>
{
    public bool IsConnected => GameLink.IsConnected;

    /// <summary>Tư thế mới nhất của mình (PlayerController cập nhật, NetPlayer gửi đi mỗi tick).</summary>
    public static Vector3 LocalPosition;
    public static float LocalRotationY, LocalHeadYaw, LocalHeadPitch;
    public static byte LocalSkin;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        DontDestroyOnLoad(gameObject);
        GameLink.PacketReceived += OnPacket;
    }

    private void OnDestroy()
    {
        if (Instance == this) GameLink.PacketReceived -= OnPacket;
    }

    public Task ConnectAsync(string playerId, string displayName)
    {
        if (GameLink.Transport == null)
            return Task.FromException(new InvalidOperationException("Chưa có lớp mạng Photon trong scene."));
        LocalSkin = (byte)PlayerSkin.SavedSkin;
        return GameLink.Transport.ConnectAsync(playerId, displayName);
    }

    public void Disconnect() => GameLink.Transport?.Disconnect();

    private void OnApplicationQuit() => Disconnect();

    public void SendPlayerTransform(Vector3 position, float rotationY, float headYaw, float headPitch)
    {
        LocalPosition = position;
        LocalRotationY = rotationY;
        LocalHeadYaw = Mathf.Clamp(headYaw, -120f, 120f);
        LocalHeadPitch = Mathf.Clamp(headPitch, -90f, 90f);
    }

    /// <summary>Skin đang mặc (ID trong SkinCatalog), đồng bộ qua NetPlayer.</summary>
    public void SendSkin(byte skinId) => LocalSkin = skinId;

    /// <summary>Người bị phạt gửi tư thế súng (stage 1 = nhấc, 2 = dí vào đầu; pitch = độ cúi đầu) để server chuyển cho cả bàn.</summary>
    public void SendGunPose(byte stage, float pitch) => Send(4, w => { w.Put(stage); w.Put(pitch); });

    /// <summary>Báo cho cả bàn thấy mình hạ bài (0), nhấc bài lên xem (1) hoặc đánh `count` lá (2).</summary>
    public void SendHandAction(byte action, byte count = 0) => Send(5, w => { w.Put(action); w.Put(count); });

    /// <summary>Jukebox: gửi lệnh (JukeboxCommand). Lệnh Ended (2) kèm số thứ tự bài vừa hết.</summary>
    public void SendJukebox(byte cmd, int endedIndex = 0) =>
        Send(7, w => { w.Put(cmd); if (cmd == (byte)JukeboxCommand.Ended) w.Put(endedIndex); });

    private static void Send(byte type, Action<NetDataWriter> body)
    {
        if (!GameLink.IsConnected) return;
        var w = new NetDataWriter();
        w.Put(type);
        body(w);
        GameLink.SendPacket(w.CopyData());
    }

    private static void OnPacket(byte[] data)
    {
        try
        {
            var reader = new NetDataReader(data);
            if (reader.AvailableBytes < 1) return;

            switch (reader.GetByte())
            {
                case 10: // Âm thanh 3D
                    {
                        string soundName = reader.GetString();
                        var pos = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
                        if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlaySpatialSfx(soundName, pos);
                        break;
                    }

                case 11: // Chờ người thua bóp cò
                    {
                        string penalizedPlayerId = reader.GetString();
                        string challengerPlayerId = reader.GetString();
                        bool challengeSucceeded = reader.GetBool();
                        if (CardBattleUI.Instance != null)
                            CardBattleUI.Instance.OnPenaltyWaitingForTrigger(penalizedPlayerId, challengerPlayerId, challengeSucceeded);
                        break;
                    }

                case 12: // Kết quả bóp cò
                    {
                        string penalizedPlayerId = reader.GetString();
                        bool isBulletHit = reader.GetBool();
                        bool survived = reader.GetBool();
                        bool eliminated = reader.GetBool();
                        string nextPhase = reader.GetString();
                        string winnerPlayerId = reader.GetString();
                        int shotNumber = reader.AvailableBytes >= 1 ? reader.GetByte() : 0;
                        if (CardBattleUI.Instance != null)
                            CardBattleUI.Instance.OnTriggerResultReceived(penalizedPlayerId, isBulletHit, survived, eliminated, nextPhase, winnerPlayerId, shotNumber);
                        break;
                    }

                case 13: // Tư thế súng của người bị phạt: holderId, stage, pitch
                    {
                        string holderId = reader.GetString();
                        byte stage = reader.GetByte();
                        float pitch = reader.GetFloat();
                        if (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsInBattle) break;
                        if (RevolverGun.Instance != null) RevolverGun.Instance.OnRemoteGunPose(holderId, stage, pitch);
                        break;
                    }

                case 14: // Bài trên tay người khác: playerId, action (0 hạ, 1 nhấc xem, 2 đánh), count
                    {
                        string handPlayerId = reader.GetString();
                        byte action = reader.GetByte();
                        byte count = reader.GetByte();
                        if (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsInBattle) break;
                        if (action == 2) RemoteHands.Instance.PlayCards(handPlayerId, count);
                        else RemoteHands.Instance.SetPeek(handPlayerId, action == 1);
                        break;
                    }

                case 16: // Jukebox: playing, index bài, elapsedMs, volume (0..10)
                    {
                        bool playing = reader.GetBool();
                        int trackIndex = reader.GetInt();
                        long elapsedMs = reader.GetLong();
                        byte volume = reader.GetByte();
                        JukeboxItem.OnServerState(playing, trackIndex, elapsedMs, volume);
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Lỗi đọc gói dữ liệu: {ex.Message}");
        }
    }
}
