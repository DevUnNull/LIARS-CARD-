using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Âm thanh chung của cả game: nhạc nền saloon, tiếng không khí quán, và hàm phát SFX theo tên sự kiện.
/// Tự khởi động khi vào game (không cần đặt vào scene). Mỗi sự kiện là một thư mục
/// Resources/Audio/Sfx/&lt;Tên&gt;/ chứa vài biến thể — phát ngẫu nhiên 1 file cho đỡ lặp.
/// Nhạc nền lấy ngẫu nhiên trong Resources/Audio/Music và tự nhỏ dần khi Jukebox đang phát.
/// </summary>
public class GameAudio : MonoBehaviour
{
    [Header("Âm Lượng")]
    [Tooltip("Âm lượng nhạc nền (0 - 1). Nên để nhỏ (~0.2) cho nhạc nằm phía sau.")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.2f;

    [Tooltip("Âm lượng chung của mọi hiệu ứng (0 - 1).")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Tooltip("Tốc độ nhạc nền nhỏ đi / to lại khi Jukebox bật / tắt (đơn vị âm lượng mỗi giây).")]
    [SerializeField, Min(0.01f)] private float musicFadeSpeed = 0.4f;

    [Header("Không Khí Quán")]
    [Tooltip("Khoảng thời gian ngẫu nhiên giữa 2 tiếng ly chạm trong quán (giây).")]
    [SerializeField] private Vector2 ambientInterval = new Vector2(8f, 20f);

    [Tooltip("Vùng trong quán (tâm, kích thước) để phát tiếng ly chạm ngẫu nhiên.")]
    [SerializeField] private Bounds ambientArea = new Bounds(new Vector3(0f, 1.1f, 0f), new Vector3(24f, 0.5f, 16f));

    private const int PoolSize = 24;

    private static GameAudio instance;

    private readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Button> hookedButtons = new HashSet<Button>();

    private AudioSource[] pool;
    private int poolNext;
    private AudioSource ui2D;
    private AudioSource music;
    private AudioClip[] playlist;
    private int playlistNext;
    private float nextAmbientAt;
    private string lastWinner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("[GameAudio]");
        DontDestroyOnLoad(go);
        go.AddComponent<GameAudio>();
    }

    private void Awake()
    {
        instance = this;

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var child = new GameObject("Sfx3D_" + i);
            child.transform.SetParent(transform, false);
            pool[i] = child.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
            pool[i].spatialBlend = 1f;
            pool[i].rolloffMode = AudioRolloffMode.Linear;
            pool[i].minDistance = 2f;
        }

        ui2D = gameObject.AddComponent<AudioSource>();
        ui2D.playOnAwake = false;
        ui2D.spatialBlend = 0f;

        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.volume = 0f;

        playlist = Resources.LoadAll<AudioClip>("Audio/Music");
        Shuffle(playlist);
        nextAmbientAt = Time.time + ambientInterval.y;

        SceneManager.sceneLoaded += OnSceneLoaded;
        HookButtons(null);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookButtons(null);

    private void Update()
    {
        UpdateMusic();

        if (Time.time >= nextAmbientAt)
        {
            nextAmbientAt = Time.time + UnityEngine.Random.Range(ambientInterval.x, ambientInterval.y);
            Vector3 p = ambientArea.center + Vector3.Scale(ambientArea.extents, new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)));
            PlayAt("AmbientGlass", p, 0.35f, 0f, 20f);
        }

        CheckMatchEnd();
    }

    private void UpdateMusic()
    {
        if (playlist == null || playlist.Length == 0) return;

        if (!music.isPlaying)
        {
            music.clip = playlist[playlistNext++ % playlist.Length];
            music.Play();
        }

        float target = JukeboxItem.IsPlaying ? 0f : musicVolume;
        music.volume = Mathf.MoveTowards(music.volume, target, musicFadeSpeed * Time.unscaledDeltaTime);
    }

    // Có người thắng ván: chuông cho cả bàn, thêm tiếng tiền nếu mình thắng
    private void CheckMatchEnd()
    {
        var ui = CardBattleUI.Instance;
        string winner = ui != null ? ui.winnerPlayerId : null;
        if (winner == lastWinner) return;
        lastWinner = winner;
        if (string.IsNullOrEmpty(winner)) return;

        Play2D("MatchEnd", 0.8f);
        string myId = TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : null;
        if ((!string.IsNullOrEmpty(myId) && string.Equals(winner, myId, StringComparison.OrdinalIgnoreCase))
            || string.Equals(winner, "Bạn", StringComparison.OrdinalIgnoreCase))
            Play2D("WinCoins", 0.9f);
    }

    /// <summary>Có âm thanh cho sự kiện này không (thư mục Resources/Audio/Sfx/&lt;name&gt; có file).</summary>
    public static bool Has(string name) => instance != null && instance.GetClip(name) != null;

    /// <summary>Phát không gian 2D (nghe như nhau ở mọi chỗ) — dùng cho UI và tiếng của chính mình.</summary>
    public static void Play2D(string name, float volume = 1f, float minInterval = 0.05f)
    {
        if (instance == null || !instance.Ready(name, minInterval)) return;
        var clip = instance.GetClip(name);
        if (clip != null) instance.ui2D.PlayOneShot(clip, volume * instance.sfxVolume);
    }

    /// <summary>Phát 3D tại một điểm trong quán (nhỏ dần theo khoảng cách, tắt hẳn ở maxDistance mét).</summary>
    public static void PlayAt(string name, Vector3 position, float volume = 1f, float minInterval = 0.05f, float maxDistance = 25f)
    {
        if (instance == null || !instance.Ready(name, minInterval)) return;
        var clip = instance.GetClip(name);
        if (clip == null) return;

        var src = instance.pool[instance.poolNext];
        instance.poolNext = (instance.poolNext + 1) % PoolSize;
        src.transform.position = position;
        src.maxDistance = maxDistance;
        src.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
        src.clip = clip;
        src.volume = volume * instance.sfxVolume;
        src.Play();
    }

    /// <summary>Gắn tiếng click cho mọi Button (uGUI) dưới root; root = null thì quét cả scene. Gọi lại nhiều lần không bị trùng.</summary>
    public static void HookButtons(GameObject root)
    {
        if (instance == null) return;
        var buttons = root != null
            ? root.GetComponentsInChildren<Button>(true)
            : FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var b in buttons)
        {
            if (!instance.hookedButtons.Add(b)) continue;
            b.onClick.AddListener(() => Play2D("UiClick", 0.6f));
        }
    }

    private bool Ready(string name, float minInterval)
    {
        if (lastPlayed.TryGetValue(name, out float t) && Time.unscaledTime - t < minInterval) return false;
        lastPlayed[name] = Time.unscaledTime;
        return true;
    }

    private AudioClip GetClip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!clips.TryGetValue(name, out var set))
        {
            set = Resources.LoadAll<AudioClip>("Audio/Sfx/" + name);
            clips[name] = set;
        }
        return set.Length == 0 ? null : set[UnityEngine.Random.Range(0, set.Length)];
    }

    private static void Shuffle(AudioClip[] list)
    {
        for (int i = list.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    [ContextMenu("Test: Bài Nhạc Nền Tiếp Theo")]
    private void DebugNextMusic() => music.Stop();

    [ContextMenu("Test: Tiếng Người Vào Quán")]
    private void DebugJoin() => Play2D("PlayerJoin");
}
