using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Các object của bảng Cài đặt. Chỉnh giao diện ngay trong Hierarchy/prefab, không cần sửa code.</summary>
public class SettingsPanelUI : MonoBehaviour
{
    [Header("Bảng")]
    [Tooltip("Object gốc của bảng (con 'Panel'), bật/tắt khi mở/đóng menu.")]
    public RectTransform panel;
    [Tooltip("Vị trí (lệch so với mép phải, giữa chiều cao) khi hiện cạnh bảng chọn trang phục trong gương (đơn vị: pixel).")]
    public Vector2 sideOffset = new Vector2(-40, 0);

    [Header("Điều chỉnh")]
    [Tooltip("Thanh âm lượng tổng. Khoảng giá trị (Min/Max) chỉnh ngay trên Slider. Khuyến nghị: 0 - 1.")]
    public Slider volumeSlider;
    [Tooltip("Thanh độ nhạy xoay camera. Khoảng giá trị chỉnh ngay trên Slider. Khuyến nghị: 0.5 - 6.")]
    public Slider sensitivitySlider;
    [Tooltip("Nút đổi mức đồ họa (xoay vòng qua các Quality Level).")]
    public Button graphicsButton;
    [Tooltip("Chữ trên nút đồ họa.")]
    public TMP_Text graphicsText;

    [Tooltip("Nút đổi mức khóa FPS (VSync, 30, 60, 75, 120, 144, không giới hạn).")]
    public Button fpsButton;
    [Tooltip("Chữ trên nút khóa FPS.")]
    public TMP_Text fpsText;

    [Header("Đóng")]
    [Tooltip("Nút ĐÓNG. Tự ẩn khi bảng hiện cạnh gương (đóng cùng bảng trang phục).")]
    public Button closeButton;

    void Reset() => AutoAssign();

    /// <summary>Tìm các object con theo tên mặc định (Panel, VolumeSlider, SensitivitySlider, GraphicsButton, CloseButton).</summary>
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
        volumeSlider = Find<Slider>("VolumeSlider");
        sensitivitySlider = Find<Slider>("SensitivitySlider");
        graphicsButton = Find<Button>("GraphicsButton");
        graphicsText = graphicsButton != null ? graphicsButton.GetComponentInChildren<TMP_Text>(true) : null;
        fpsButton = Find<Button>("FpsButton");
        fpsText = fpsButton != null ? fpsButton.GetComponentInChildren<TMP_Text>(true) : null;
        closeButton = Find<Button>("CloseButton");
    }

    public bool IsComplete => panel && volumeSlider && sensitivitySlider && graphicsButton && graphicsText && fpsButton && fpsText && closeButton;
}
