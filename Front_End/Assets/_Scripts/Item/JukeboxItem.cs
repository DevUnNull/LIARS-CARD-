using UnityEngine;

/// <summary>Lệnh điều khiển Jukebox — trùng số với JukeboxService.Command ở server.</summary>
public enum JukeboxCommand : byte { Next = 0, Prev = 1, Ended = 2, PlayStop = 3, VolumeUp = 4, VolumeDown = 5 }

/// <summary>
/// Loa + danh sách nhạc của Jukebox. Server giữ (đang phát?, số thứ tự bài, đã phát bao lâu, âm lượng) — packet 16 —
/// nên cả quán nghe cùng một bài, cùng một đoạn, cùng âm lượng; hết bài thì tự chuyển.
/// Các nút bấm (JukeboxButton) gọi <see cref="Command"/>. Chưa kết nối server thì chạy riêng trên máy mình.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class JukeboxItem : MonoBehaviour
{
    private const byte MaxVolumeStep = 10;

    [Header("Danh Sách Nhạc")]
    [Tooltip("Các bài nhạc phát theo thứ tự (lấy từ Assets/Audio/Jukebox_Music). Mọi máy phải có cùng danh sách, cùng thứ tự.")]
    [SerializeField] private AudioClip[] tracks;

    [Header("Âm Thanh")]
    [Tooltip("Loa phát nhạc. Spatial Blend = 1, Max Distance ~35m để cả quán nghe được.")]
    [SerializeField] private AudioSource speaker;

    [Tooltip("Âm lượng của loa khi nút âm lượng ở mức cao nhất (10/10). Khoảng 0 - 1.")]
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1f;

    [Header("Hoạt Ảnh")]
    [Tooltip("Animator của model Jukebox_3500 (state Play_Music / Stop_Music: hạ / nhấc cần đĩa). Để trống nếu không có.")]
    [SerializeField] private Animator animator;

    // Trạng thái mới nhất từ server; để static vì packet có thể tới trước khi scene có Jukebox
    private static bool statePlaying;
    private static int stateIndex;
    private static long stateElapsedMs;
    private static byte stateVolume = 7;
    private static float stateReceivedAt;

    private static JukeboxItem instance;

    /// <summary>Jukebox đang phát nhạc (GameAudio dùng để nhỏ nhạc nền).</summary>
    public static bool IsPlaying => statePlaying;

    private bool isPlaying;
    private int playingIndex;
    private float endsAt;
    private bool endReported;

    /// <summary>Gọi từ NetworkManager khi nhận packet 16.</summary>
    public static void OnServerState(bool playing, int index, long elapsedMs, byte volume)
    {
        statePlaying = playing;
        stateIndex = index;
        stateElapsedMs = elapsedMs;
        stateVolume = (byte)Mathf.Min(volume, MaxVolumeStep);
        stateReceivedAt = Time.realtimeSinceStartup;
        if (instance != null) instance.Apply();
    }

    /// <summary>Gửi lệnh lên server; chưa kết nối thì tự xử lý trên máy mình.</summary>
    public static void Command(JukeboxCommand cmd, int endedIndex = 0)
    {
        var net = NetworkManager.Instance;
        if (net != null && net.IsConnected)
        {
            net.SendJukebox((byte)cmd, endedIndex);
            return;
        }

        switch (cmd)
        {
            case JukeboxCommand.PlayStop: OnServerState(!statePlaying, stateIndex, 0, stateVolume); break;
            case JukeboxCommand.Next:
            case JukeboxCommand.Ended: OnServerState(true, stateIndex + 1, 0, stateVolume); break;
            case JukeboxCommand.Prev: OnServerState(true, stateIndex - 1, 0, stateVolume); break;
            case JukeboxCommand.VolumeUp: OnServerState(statePlaying, stateIndex, stateElapsedMs, (byte)Mathf.Min(stateVolume + 1, MaxVolumeStep)); break;
            case JukeboxCommand.VolumeDown: OnServerState(statePlaying, stateIndex, stateElapsedMs, (byte)Mathf.Max(stateVolume - 1, 0)); break;
        }
    }

    private void Reset()
    {
        speaker = GetComponent<AudioSource>();
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        instance = this;
        if (speaker == null) speaker = GetComponent<AudioSource>();
        speaker.playOnAwake = false;
        speaker.loop = false;
    }

    private void Start() => Apply();

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (!isPlaying || endReported || Time.realtimeSinceStartup < endsAt) return;

        endReported = true;
        Command(JukeboxCommand.Ended, playingIndex);
    }

    private void Apply()
    {
        speaker.volume = maxVolume * stateVolume / MaxVolumeStep;

        // Chỉ đổi âm lượng thì không phát lại bài
        if (statePlaying == isPlaying && (!statePlaying || stateIndex == playingIndex)) return;

        // Controller không có transition nên gọi thẳng state; đổi bài khi đang phát thì giữ nguyên cần đĩa
        if (animator != null && statePlaying != isPlaying) animator.Play(statePlaying ? "Play_Music" : "Stop_Music");
        isPlaying = statePlaying;
        playingIndex = stateIndex;
        speaker.Stop();
        if (!isPlaying || tracks == null || tracks.Length == 0) return;

        var clip = tracks[((playingIndex % tracks.Length) + tracks.Length) % tracks.Length];
        // Nhảy tới đúng đoạn cả quán đang nghe; lệch quá cuối bài thì chờ chuyển bài
        float offset = stateElapsedMs / 1000f + (Time.realtimeSinceStartup - stateReceivedAt);
        endReported = false;
        endsAt = Time.realtimeSinceStartup + Mathf.Max(0f, clip.length - offset);
        if (offset >= clip.length) return;

        speaker.clip = clip;
        speaker.time = offset;
        speaker.Play();
        Debug.Log($"[Jukebox] Đang phát: {clip.name}");
    }

    [ContextMenu("Test: Nhảy Tới 10 Giây Cuối Bài")]
    private void DebugNearEnd()
    {
        if (!isPlaying || speaker.clip == null) return;
        isPlaying = false;   // ép Apply phát lại cùng bài ở vị trí mới
        OnServerState(true, playingIndex, (long)((speaker.clip.length - 10f) * 1000), stateVolume);
    }
}
