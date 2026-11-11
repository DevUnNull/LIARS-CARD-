using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using LiarsCard.Cards3D;

public class NetworkManager : Singleton<NetworkManager>
{
    [SerializeField]
    private string apiBaseUrl = "http://localhost:5280";

    [Header("LiteNetLib")]
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private int serverPort = 9050;

    [Header("Timeout")]
    [SerializeField] private int httpTimeoutSeconds = 15;
    [SerializeField] private float connectTimeoutSeconds = 10f;

    private const string CONNECTION_KEY = "LiarsCard";

    private NetManager client;
    private NetPeer serverPeer;

    private CancellationTokenSource connectCancellation;
    private string connectionError;

    private bool isConnecting;
    private bool isOwner;

    public bool IsConnected => serverPeer != null && serverPeer.ConnectionState == ConnectionState.Connected;

    public void ApplyServerConfig()
    {
        if (!ServerConfig.IsSet) return;
        apiBaseUrl = ServerConfig.ApiBaseUrl;
        serverAddress = ServerConfig.UdpHost;
        serverPort = ServerConfig.UdpPort;
    }

    protected override void Awake()
    {
        base.Awake();
        ApplyServerConfig();

        DontDestroyOnLoad(gameObject);
        isOwner = true;
        var listener = new EventBasedNetListener();
        client = new NetManager(listener);

        listener.PeerConnectedEvent += peer =>
        {
            serverPeer = peer;
            connectionError = null;
            SendSkin((byte)PlayerSkin.SavedSkin);

            Debug.Log("Connected to server.");
        };

        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            serverPeer = null;

            connectionError = $"Disconnected to server: {info.Reason}";

            Debug.LogWarning(connectionError);
        };

        listener.NetworkReceiveEvent += (peer, reader, channel, deliveryMethod) =>
        {
            try
            {
                if (reader.AvailableBytes < 1)
                    return;

                byte packetType = reader.GetByte();

                switch (packetType)
                {
                    case 2:
                        {
                            string playerId = reader.GetString(128);

                            // 16 byte = server cũ chưa có góc đầu, 24 byte = kèm headYaw + headPitch
                            if (reader.AvailableBytes != 16 && reader.AvailableBytes != 24)
                                return;

                            float x = reader.GetFloat();
                            float y = reader.GetFloat();
                            float z = reader.GetFloat();
                            float rotationY = reader.GetFloat();
                            float headYaw = reader.AvailableBytes >= 8 ? reader.GetFloat() : 0f;
                            float headPitch = reader.AvailableBytes >= 4 ? reader.GetFloat() : 0f;

                            var manager = RemotePlayerManager.Instance;

                            if (manager == null)
                                return;

                            manager.UpdateRemotePlayer(
                                playerId,
                                new Vector3(x, y, z),
                                rotationY,
                                headYaw,
                                headPitch);

                            break;
                        }

                    case 3:
                        {
                            string playerId = reader.GetString(128);

                            if (reader.AvailableBytes != 0)
                                return;

                            var manager = RemotePlayerManager.Instance;

                            if (manager == null)
                                return;

                            manager.RemoveRemotePlayer(playerId);

                            break;
                        }

                    case 10: // Audio SFX Broadcast (Spatial 3D Sound)
                        {
                            string soundName = reader.GetString();
                            float x = reader.GetFloat();
                            float y = reader.GetFloat();
                            float z = reader.GetFloat();

                            var audioSys = CardAudioSystem.Instance;
                            if (audioSys != null)
                            {
                                audioSys.PlaySpatialSfx(soundName, new Vector3(x, y, z));
                            }
                            break;
                        }

                    case 11: // Penalty Waiting For Trigger
                        {
                            string penalizedPlayerId = reader.GetString();
                            string challengerPlayerId = reader.GetString();
                            bool challengeSucceeded = reader.GetBool();

                            var battleUI = CardBattleUI.Instance;
                            if (battleUI != null)
                            {
                                battleUI.OnPenaltyWaitingForTrigger(penalizedPlayerId, challengerPlayerId, challengeSucceeded);
                            }
                            break;
                        }

                    case 13: // Tư thế súng của người bị phạt (server chuyển tiếp): holderId, stage, pitch
                        {
                            string holderId = reader.GetString();
                            byte stage = reader.GetByte();
                            float pitch = reader.GetFloat();

                            // Gói của trận mình đã rời (vẫn nằm trong danh sách trận trên server) => bỏ qua
                            if (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsInBattle) break;
                            var gun = RevolverGun.Instance;
                            if (gun != null) gun.OnRemoteGunPose(holderId, stage, pitch);
                            break;
                        }

                    case 14: // Hành động bài trên tay của người khác: playerId, action (0 hạ, 1 nhấc xem, 2 đánh), count
                        {
                            string handPlayerId = reader.GetString();
                            byte action = reader.GetByte();
                            byte count = reader.GetByte();

                            if (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsInBattle) break;
                            if (action == 2) RemoteHands.Instance.PlayCards(handPlayerId, count);
                            else RemoteHands.Instance.SetPeek(handPlayerId, action == 1);
                            break;
                        }

                    case 15: // Skin của người chơi khác: playerId, skinId
                        {
                            string skinPlayerId = reader.GetString(128);
                            byte skinId = reader.GetByte();

                            if (RemotePlayerManager.Instance != null)
                                RemotePlayerManager.Instance.SetRemoteSkin(skinPlayerId, skinId);
                            break;
                        }

                    case 17: // Tên hiển thị của người chơi khác: playerId, displayName
                        {
                            string namePlayerId = reader.GetString(128);
                            PlayerNames.Remember(namePlayerId, reader.GetString(64));
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

                    case 12: // Trigger Result Broadcast
                        {
                            string penalizedPlayerId = reader.GetString();
                            bool isBulletHit = reader.GetBool();
                            bool survived = reader.GetBool();
                            bool eliminated = reader.GetBool();
                            string nextPhase = reader.GetString();
                            string winnerPlayerId = reader.GetString();
                            int shotNumber = reader.AvailableBytes >= 1 ? reader.GetByte() : 0;

                            var battleUI = CardBattleUI.Instance;
                            if (battleUI != null)
                            {
                                battleUI.OnTriggerResultReceived(penalizedPlayerId, isBulletHit, survived, eliminated, nextPhase, winnerPlayerId, shotNumber);
                            }
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Lỗi đọc gói dữ liệu: {ex.Message}");
            }
            finally
            {
                reader.Recycle();
            }
        };
    }

    private void Update()
    {
        if (!isOwner)
            return;

        client?.PollEvents();
    }

    private async Task<string> RequestTicketAsync(string accessToken, CancellationToken token)
    {
        string url =
            $"{apiBaseUrl.TrimEnd('/')}/api/connection/ticket";

        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = httpTimeoutSeconds;

            request.SetRequestHeader(
                "Authorization",
                $"Bearer {accessToken}");

            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    request.Abort();
                    token.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

            token.ThrowIfCancellationRequested();

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"Cant get ticket. HTTP {request.responseCode}: " +
                    request.error);
            }

            var response = JsonUtility.FromJson<TicketResponse>(
                request.downloadHandler.text);

            if (response == null ||
                string.IsNullOrWhiteSpace(response.ticket))
            {
                throw new Exception("Ticket is not correct.");
            }

            return response.ticket;
        }
    }

    public async Task ConnectAsync(string accessToken)
    {
        if (!isOwner || client == null)
            throw new InvalidOperationException(
                "NetworkManager chưa được khởi tạo.");

        if (isConnecting)
            throw new InvalidOperationException("Đang kết nối.");

        if (IsConnected)
            throw new InvalidOperationException("Đã kết nối tới server.");

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token đang trống.");

        isConnecting = true;

        var cancellation = new CancellationTokenSource();
        connectCancellation = cancellation;
        CancellationToken token = cancellation.Token;

        try
        {
            client.Stop();
            serverPeer = null;
            connectionError = null;

            string ticket = await RequestTicketAsync(accessToken, token);

            token.ThrowIfCancellationRequested();

            if (!client.Start())
                throw new Exception("Không thể khởi chạy client UDP.");

            var writer = new NetDataWriter();
            writer.Put(CONNECTION_KEY);
            writer.Put(ticket);

            client.Connect(serverAddress, serverPort, writer);

            float startedAt = Time.realtimeSinceStartup;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(connectionError))
                    throw new Exception(connectionError);

                if (IsConnected)
                    break;

                if (Time.realtimeSinceStartup - startedAt
                    >= connectTimeoutSeconds)
                {
                    throw new TimeoutException(
                        "Hết thời gian chờ kết nối server.");
                }

                await Task.Yield();
            }
        }
        catch
        {
            client.Stop();
            serverPeer = null;
            throw;
        }
        finally
        {
            connectCancellation = null;
            cancellation.Dispose();
            isConnecting = false;
        }
    }

    public void SendPlayerTransform(Vector3 position, float rotationY, float headYaw, float headPitch)
    {
        if (!IsConnected)
            return;

        var writer = new NetDataWriter();

        writer.Put((byte)1);

        writer.Put(position.x);
        writer.Put(position.y);
        writer.Put(position.z);
        writer.Put(rotationY);
        writer.Put(headYaw);
        writer.Put(headPitch);

        serverPeer.Send(writer, DeliveryMethod.Sequenced);
    }

    /// <summary>Người bị phạt gửi tư thế súng (stage 1 = nhấc, 2 = dí vào đầu; pitch = độ cúi đầu) để server chuyển cho cả bàn.</summary>
    public void SendGunPose(byte stage, float pitch)
    {
        if (!IsConnected)
            return;

        var writer = new NetDataWriter();

        writer.Put((byte)4);
        writer.Put(stage);
        writer.Put(pitch);

        serverPeer.Send(writer, DeliveryMethod.Sequenced);
    }

    /// <summary>Báo cho cả bàn thấy mình hạ bài (0), nhấc bài lên xem (1) hoặc đánh `count` lá (2).</summary>
    public void SendHandAction(byte action, byte count = 0)
    {
        if (!IsConnected)
            return;

        var writer = new NetDataWriter();

        writer.Put((byte)5);
        writer.Put(action);
        writer.Put(count);

        serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>Báo skin đang mặc (ID trong SkinCatalog) để server chuyển cho mọi người.</summary>
    public void SendSkin(byte skinId)
    {
        if (!IsConnected)
            return;

        var writer = new NetDataWriter();

        writer.Put((byte)6);
        writer.Put(skinId);

        serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    /// <summary>Jukebox: gửi lệnh (JukeboxCommand). Lệnh Ended (2) kèm số thứ tự bài vừa hết.</summary>
    public void SendJukebox(byte cmd, int endedIndex = 0)
    {
        if (!IsConnected)
            return;

        var writer = new NetDataWriter();
        writer.Put((byte)7);
        writer.Put(cmd);
        if (cmd == (byte)JukeboxCommand.Ended) writer.Put(endedIndex);
        serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    public void Disconnect()
    {
        if (!isOwner)
            return;

        connectCancellation?.Cancel();

        client?.Stop();

        serverPeer = null;
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    private void OnDestroy()
    {
        if (!isOwner)
            return;

        Disconnect();
    }
}

[Serializable]
public class TicketResponse
{
    public string ticket;
    public int expiresIn;
}
