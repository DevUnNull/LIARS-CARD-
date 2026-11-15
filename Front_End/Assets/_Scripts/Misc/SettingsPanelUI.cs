using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Các đối tượng tham chiếu của bảng Cài đặt (Settings UI) phong cách Liar's Bar.
/// Cho phép tinh chỉnh trực tiếp trong Inspector hoặc Hierarchy/Prefab mà không cần sửa code.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    [Header("Bảng Giao Diện")]
    [Tooltip("Khung chính của bảng (object con 'Panel'), bật/tắt khi mở/đóng menu.")]
    public RectTransform panel;

    [Tooltip("Lớp nền mờ tối toàn màn hình (che mờ nền khi mở menu cài đặt).")]
    public RectTransform backdrop;

    [Tooltip("Vị trí (lệch so với mép phải, giữa chiều cao) khi hiện cạnh bảng chọn trang phục trong gương (đơn vị: pixel).")]
    public Vector2 sideOffset = new Vector2(-40, 0);

    [Header("Âm Lượng")]
    [Tooltip("Thanh trượt âm lượng tổng (Master Volume). Khoảng giá trị 0 - 1.")]
    public Slider volumeSlider;
    [Tooltip("Chữ hiển thị phần trăm âm lượng (ví dụ: 100%).")]
    public TMP_Text volumeValueText;

    [Header("Độ Nhạy Chuột")]
    [Tooltip("Thanh trượt độ nhạy xoay camera (Mouse Sensitivity). Khuyến nghị: 0.5 - 6.0.")]
    public Slider sensitivitySlider;
    [Tooltip("Chữ hiển thị hệ số độ nhạy chuột (ví dụ: 2.0x).")]
    public TMP_Text sensitivityValueText;

    [Header("Đồ Họa & Hiệu Năng")]
    [Tooltip("Nút đổi mức đồ họa (chuyển đổi giữa PC và Mobile).")]
    public Button graphicsButton;
    [Tooltip("Chữ hiển thị trên nút đồ họa.")]
    public TMP_Text graphicsText;

    [Tooltip("Nút đổi mức khóa FPS (VSync, 30, 60, 75, 120, 144, không giới hạn).")]
    public Button fpsButton;
    [Tooltip("Chữ hiển thị trên nút khóa FPS.")]
    public TMP_Text fpsText;

    [Header("Nút Đóng")]
    [Tooltip("Nút ĐÓNG cài đặt. Tự ẩn khi bảng hiện cạnh gương (đóng cùng bảng trang phục).")]
    public Button closeButton;
    [Tooltip("Chữ trên nút đóng.")]
    public TMP_Text closeText;

    void Reset() => AutoAssign();

    /// <summary>Tìm các object con theo tên mặc định.</summary>
    [ContextMenu("Tự gán tham chiếu theo tên object con")]
    public void AutoAssign()
    {
        T Find<T>(string n) where T : Component
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t.GetComponent<T>();
            return null;
        }

        panel = Find<RectTransform>("Panel");
        backdrop = Find<RectTransform>("Backdrop");

        volumeSlider = Find<Slider>("VolumeSlider");
        volumeValueText = Find<TMP_Text>("VolumeValueText");

        sensitivitySlider = Find<Slider>("SensitivitySlider");
        sensitivityValueText = Find<TMP_Text>("SensitivityValueText");

        graphicsButton = Find<Button>("GraphicsButton");
        graphicsText = graphicsButton != null ? graphicsButton.GetComponentInChildren<TMP_Text>(true) : null;

        fpsButton = Find<Button>("FpsButton");
        fpsText = fpsButton != null ? fpsButton.GetComponentInChildren<TMP_Text>(true) : null;

        closeButton = Find<Button>("CloseButton");
        closeText = closeButton != null ? closeButton.GetComponentInChildren<TMP_Text>(true) : null;
    }

    public bool IsComplete => panel && volumeSlider && sensitivitySlider && graphicsButton && graphicsText && fpsButton && fpsText && closeButton;
}
