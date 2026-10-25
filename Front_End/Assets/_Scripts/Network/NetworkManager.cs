using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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

    protected override void Awake()
    {
        base.Awake();

        DontDestroyOnLoad(gameObject);
        isOwner = true;
        var listener = new EventBasedNetListener();
        client = new NetManager(listener);

        listener.PeerConnectedEvent += peer =>
        {
            serverPeer = peer;
            connectionError = null;

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

                            if (reader.AvailableBytes != 16)
                                return;

                            float x = reader.GetFloat();
                            float y = reader.GetFloat();
                            float z = reader.GetFloat();
                            float rotationY = reader.GetFloat();

                            var manager = RemotePlayerManager.Instance;

                            if (manager == null)
                                return;

                            manager.UpdateRemotePlayer(
                                playerId,
                                new Vector3(x, y, z),
                                rotationY);

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

    public void SendPlayerTransform(Vector3 position, float rotationY)
    {
        if (!IsConnected)
            return;

        var writer = new NetDataWriter();

        writer.Put((byte)1);

        writer.Put(position.x);
        writer.Put(position.y);
        writer.Put(position.z);
        writer.Put(rotationY);

        serverPeer.Send(writer, DeliveryMethod.Sequenced);
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