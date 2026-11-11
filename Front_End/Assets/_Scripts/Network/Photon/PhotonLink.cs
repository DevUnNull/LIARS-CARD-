using System;
using System.Threading.Tasks;
using Fusion;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Lớp mạng Photon Fusion 2 (Shared Mode) cho GameLink: vào phòng chung của quán bar, spawn PhotonHub
/// (nếu là người đầu tiên) và NetPlayer của mình, rồi chuyển request / gói tin qua PhotonHub.
/// </summary>
public class PhotonLink : MonoBehaviour, GameLink.ITransport
{
    [Header("Prefab Mạng")]
    [Tooltip("Prefab PhotonHub (NetworkObject, bật Is Master Client Object).")]
    [SerializeField] private NetworkObject hubPrefab;
    [Tooltip("Prefab NetPlayer (NetworkObject) mỗi người tự spawn cho mình.")]
    [SerializeField] private NetworkObject playerPrefab;

    [Header("Phòng")]
    [Tooltip("Tên phòng Photon: mọi người cùng tên phòng sẽ vào chung một quán.")]
    [SerializeField] private string sessionName = "LiarsCard-Bar";
    [Tooltip("Số người tối đa trong quán. Gói Photon miễn phí cho tối đa 20 người online cùng lúc.")]
    [SerializeField, Range(2, 20)] private int maxPlayers = 20;
    [Tooltip("Thời gian chờ kết nối (giây).")]
    [SerializeField, Min(5f)] private float connectTimeout = 20f;

    private NetworkRunner runner;
    private int seenEpoch;
    private string myName;

    public bool IsConnected => runner != null && runner.IsRunning && PhotonHub.Instance != null && seenEpoch > 0;

    private void Awake() => GameLink.Transport = this;

    private void OnDestroy()
    {
        if (ReferenceEquals(GameLink.Transport, this)) GameLink.Transport = null;
    }

    public async Task ConnectAsync(string playerId, string displayName)
    {
        if (runner != null) throw new InvalidOperationException("Đã vào phòng.");
        myName = displayName;

        var go = new GameObject("[NetworkRunner]");
        DontDestroyOnLoad(go);
        runner = go.AddComponent<NetworkRunner>();

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = sessionName,
            PlayerCount = maxPlayers,
            AuthValues = new AuthenticationValues(playerId),   // playerId = UserId Photon, server dùng để nhận ra người gửi
        });
        if (!result.Ok)
        {
            Cleanup();
            throw new Exception(result.ShutdownReason == ShutdownReason.GameIsFull
                ? "Quán đã đầy người, thử lại sau."
                : $"Không vào được phòng Photon: {result.ShutdownReason}");
        }

        try
        {
            if (runner.IsSharedModeMasterClient && PhotonHub.Instance == null) runner.Spawn(hubPrefab);
            runner.Spawn(playerPrefab, inputAuthority: runner.LocalPlayer);

            float until = Time.realtimeSinceStartup + connectTimeout;
            while (PhotonHub.Instance == null || PhotonHub.Instance.Epoch == 0)
            {
                if (Time.realtimeSinceStartup > until || runner == null || !runner.IsRunning)
                    throw new TimeoutException("Không kết nối được với chủ phòng.");
                await Task.Yield();
            }
            Hello();
        }
        catch
        {
            Disconnect();   // không để runner treo trong phòng khi vào dở
            throw;
        }
    }

    // Chủ phòng mới (người cũ thoát) khởi động lại server từ đầu: đăng ký lại và đứng dậy khỏi bàn
    private void Update()
    {
        var hub = PhotonHub.Instance;
        if (hub == null || seenEpoch == 0 || hub.Epoch == seenEpoch) return;
        Debug.LogWarning("[PhotonLink] Chủ phòng đã đổi: ván đang chơi bị huỷ.");
        GameLink.FailAllPending("[HOST_CHANGED] Chủ phòng vừa thoát, ván bị huỷ.");
        Hello();
        if (TableSessionManager.Instance != null && TableSessionManager.Instance.isSeated)
        {
            var interact = FindFirstObjectByType<FirstPersonInteract>();
            if (interact != null) interact.LeaveSeat();
        }
    }

    private void Hello()
    {
        seenEpoch = PhotonHub.Instance.Epoch;
        PhotonHub.Instance.SendHello(myName);
    }

    public void Disconnect()
    {
        if (runner != null && runner.IsRunning) runner.Shutdown();
        Cleanup();
    }

    private void Cleanup()
    {
        if (runner != null) Destroy(runner.gameObject);
        runner = null;
        seenEpoch = 0;
        GameLink.FailAllPending("[DISCONNECTED] Đã rời phòng.");
    }

    public void SendRequest(int requestId, string route, string json) => PhotonHub.Instance.SendRequest(requestId, route, json);

    public void SendPacket(byte[] data) => PhotonHub.Instance.SendPacket(data);
}
