using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bảng thông số nhỏ ở góc dưới bên phải: FPS, thời gian mỗi khung hình, ping tới server Photon.
/// Giao diện là object thật trong Hierarchy (PerfHud/Text), chỉnh vị trí, cỡ chữ, màu ngay trong Inspector.
/// Phím F3 bật/tắt.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class PerfHud : MonoBehaviour
{
    [Header("Giao Diện")]
    [Tooltip("Chữ hiển thị thông số (object con 'Text'). Tự gán khi thêm component.")]
    [SerializeField] private TMP_Text label;

    [Header("Thông Số Hiển Thị")]
    [Tooltip("Hiện FPS (số khung hình mỗi giây).")]
    [SerializeField] private bool showFps = true;
    [Tooltip("Hiện thời gian mỗi khung hình (đơn vị: mili giây) và khung hình chậm nhất trong chu kỳ. Càng thấp càng mượt; \"tệ nhất\" cao vọt lên là lúc bị khựng.")]
    [SerializeField] private bool showFrameMs = true;
    [Tooltip("Hiện ping (RTT) tới server Photon (đơn vị: mili giây).")]
    [SerializeField] private bool showPing = true;
    [Tooltip("Bao lâu cập nhật số một lần (đơn vị: giây). Khuyến nghị 0.25 - 1.")]
    [SerializeField, Range(0.1f, 2f)] private float refreshSeconds = 0.5f;

    [Header("Điều Khiển")]
    [Tooltip("Phím bật/tắt bảng thông số.")]
    [SerializeField] private Key toggleKey = Key.F3;

    private float timer, worst;
    private int frames;

    private void Reset() => label = GetComponentInChildren<TMP_Text>(true);

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
    }

    private void Update()
    {
        if (label == null) return;

        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) label.enabled = !label.enabled;
        if (!label.enabled) return;

        timer += Time.unscaledDeltaTime;
        frames++;
        worst = Mathf.Max(worst, Time.unscaledDeltaTime);
        if (timer < refreshSeconds) return;

        float fps = frames / timer;
        float ms = timer / frames * 1000f;
        float maxMs = worst * 1000f;
        timer = 0f; frames = 0; worst = 0f;

        string text = "";
        if (showFps) text += $"FPS {fps:0}";
        if (showFrameMs) text += (text.Length > 0 ? "  " : "") + $"{ms:0.0} ms (tệ nhất {maxMs:0})";
        if (showPing) text += (text.Length > 0 ? "\n" : "") + PingText();
        label.SetText(text);
    }

    private static string PingText()
    {
        var hub = PhotonHub.Instance;
        var runner = hub != null ? hub.Runner : null;
        if (runner == null || !runner.IsRunning) return "Ping --";
        int rtt = (int)System.Math.Round(runner.GetPlayerRtt(PlayerRef.None) * 1000);
        return runner.IsSharedModeMasterClient ? $"Ping {rtt} ms (chủ phòng)" : $"Ping {rtt} ms";
    }
}
