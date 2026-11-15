using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menu Cài đặt (âm lượng, độ nhạy, đồ họa). Giao diện là prefab Resources/SettingsPanel (tạo bằng menu Build/Tạo prefab Cài đặt).
/// Mở bằng phím Esc, hoặc hiện cạnh bảng chọn trang phục trong gương (ShowSide). Giá trị lưu PlayerPrefs.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    const string VolKey = "set_volume", SensKey = "set_sens", QualKey = "set_quality", FpsKey = "set_fps";
    static readonly int[] FpsOptions = { -1, 30, 60, 75, 120, 144, 0 }; // -1 = VSync theo màn hình, 0 = không giới hạn
    int fpsIndex;

    public static bool IsOpen { get; private set; } // CardHand3D không khoá chuột lại khi menu đang mở

    static SettingsMenu instance;
    [Header("Giao diện")]
    [Tooltip("Bảng Cài đặt (object con SettingsPanel trong Hierarchy). Tự gán khi thêm component; chỉnh giao diện ngay trên object đó.")]
    [SerializeField] SettingsPanelUI ui;
    CursorLockMode prevLock;
    bool prevVisible, side;
    float sens;

    void Reset() => ui = GetComponentInChildren<SettingsPanelUI>(true);

    void Awake()
    {
        instance = this;
        if (ui != null && !ui.IsComplete) ui.AutoAssign(); // lỡ mất tham chiếu thì tự tìm lại theo tên
        if (ui == null || !ui.IsComplete)
        {
            Debug.LogWarning("SettingsMenu: thiếu SettingsPanelUI hoặc thiếu object con (Panel, VolumeSlider, SensitivitySlider, GraphicsButton, CloseButton).");
            enabled = false;
            return;
        }

        sens = PlayerPrefs.GetFloat(SensKey, 2f);
        if (PlayerPrefs.HasKey(VolKey)) AudioListener.volume = PlayerPrefs.GetFloat(VolKey);
        if (PlayerPrefs.HasKey(QualKey)) QualitySettings.SetQualityLevel(PlayerPrefs.GetInt(QualKey));

        ui.volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        UpdateVolumeLabel(AudioListener.volume);
        ui.volumeSlider.onValueChanged.AddListener(x =>
        {
            AudioListener.volume = x;
            PlayerPrefs.SetFloat(VolKey, x);
            UpdateVolumeLabel(x);
        });

        ui.sensitivitySlider.SetValueWithoutNotify(sens);
        UpdateSensitivityLabel(sens);
        ui.sensitivitySlider.onValueChanged.AddListener(x =>
        {
            sens = x;
            PlayerPrefs.SetFloat(SensKey, x);
            UpdateSensitivityLabel(x);
        });

        ui.graphicsButton.onClick.AddListener(() =>
        {
            int next = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
            QualitySettings.SetQualityLevel(next, true);
            PlayerPrefs.SetInt(QualKey, next);
            ApplyFps(); // đổi mức đồ họa sẽ ghi đè VSync theo mức đó, đặt lại theo lựa chọn của người chơi
            RefreshGraphicsText();
        });

        fpsIndex = Mathf.Clamp(PlayerPrefs.GetInt(FpsKey, 0), 0, FpsOptions.Length - 1);
        ui.fpsButton.onClick.AddListener(() =>
        {
            fpsIndex = (fpsIndex + 1) % FpsOptions.Length;
            PlayerPrefs.SetInt(FpsKey, fpsIndex);
            ApplyFps();
        });

        ApplyFps();
        ui.closeButton.onClick.AddListener(Toggle);

        if (ui.backdrop != null)
        {
            var backBtn = ui.backdrop.GetComponent<Button>();
            if (backBtn != null) backBtn.onClick.AddListener(Toggle);
        }

        RefreshGraphicsText();
        ui.panel.gameObject.SetActive(false);
        if (ui.backdrop != null) ui.backdrop.gameObject.SetActive(false);
    }

    void UpdateVolumeLabel(float val)
    {
        if (ui.volumeValueText != null)
            ui.volumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
    }

    void UpdateSensitivityLabel(float val)
    {
        if (ui.sensitivityValueText != null)
            ui.sensitivityValueText.text = $"{val:0.0}x";
    }

    // Màn 75Hz mà game chạy 200 FPS không VSync thì xé hình, khung không đều nên camera khựng: mặc định VSync
    void ApplyFps()
    {
        int cap = FpsOptions[fpsIndex];
        int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
        if (hz <= 0) hz = 60;
        QualitySettings.vSyncCount = cap == -1 ? 1 : 0;
        // VSync còn đặt thêm trần = Hz màn hình: trong Editor (Game view tắt VSync) hoặc nơi VSync không có tác dụng thì vẫn bị khóa đúng nhịp màn hình
        Application.targetFrameRate = cap > 0 ? cap : cap == -1 ? Mathf.Max(hz, 30) : (Application.isMobilePlatform ? 120 : -1);

        if (ui.fpsText != null)
        {
            string capStr = cap == -1 ? $"VSync ({hz} Hz)" : cap == 0 ? "Không giới hạn" : $"{cap} FPS";
            ui.fpsText.text = $"KHÓA FPS: <color=#F1C40F><b>{capStr}</b></color>";
        }
    }

    void RefreshGraphicsText()
    {
        if (ui.graphicsText == null) return;
        int level = QualitySettings.GetQualityLevel();
        string qName = (level >= 0 && level < QualitySettings.names.Length) ? QualitySettings.names[level] : "PC";
        bool isPC = qName.IndexOf("PC", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (isPC)
        {
            ui.graphicsText.text = "ĐỒ HỌA: <color=#F1C40F><b>PC</b></color> <size=75%>(Chi tiết cao)</size>";
        }
        else
        {
            ui.graphicsText.text = "ĐỒ HỌA: <color=#2ECC71><b>MOBILE</b></color> <size=75%>(Tối ưu mượt)</size>";
        }
    }

    void Update()
    {
        // Độ nhạy áp lên nhân vật mỗi frame (nhân vật được tạo sau menu)
        var pc = PlayerController.Instance;
        if (pc != null && pc.firstPersonLook != null) pc.firstPersonLook.sensitivity = sens;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            // Đang đối thoại đăng nhập thì Esc là "quay lại", không mở menu
            var login = FindFirstObjectByType<LoginRoomController>();
            if (ui.panel.gameObject.activeSelf || login == null || login.State == LoginViewState.InGame) Toggle();
        }
    }

    /// <summary>Hiện bảng cài đặt ở cạnh phải (dùng trong gương thay đồ), con trỏ do màn đó tự quản.</summary>
    public static void ShowSide(bool on)
    {
        var m = instance;
        if (m == null || !m.enabled || m.side == on || (on && m.ui.panel.gameObject.activeSelf)) return;
        m.side = on;
        var rt = m.ui.panel;
        rt.anchorMin = rt.anchorMax = rt.pivot = on ? new Vector2(1, 0.5f) : new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = on ? m.ui.sideOffset : Vector2.zero;
        if (m.ui.backdrop != null) m.ui.backdrop.gameObject.SetActive(false); // Khi soi gương không che đen màn hình
        m.ui.closeButton.gameObject.SetActive(!on);
        rt.gameObject.SetActive(on);
        IsOpen = on;
    }

    void Toggle()
    {
        if (side) return;
        bool open = !ui.panel.gameObject.activeSelf;
        ui.panel.gameObject.SetActive(open);
        if (ui.backdrop != null) ui.backdrop.gameObject.SetActive(open);
        IsOpen = open;
        if (open)
        {
            prevLock = Cursor.lockState; prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else
        {
            PlayerPrefs.Save();
            // Đang đi lại / trong ván: khoá chuột lại (trên web, Esc đã nhả khoá nên không thể chỉ khôi phục trạng thái cũ)
            var pc = PlayerController.Instance;
            bool playing = (pc != null && pc.firstPersonMovement != null && pc.firstPersonMovement.enabled)
                           || (CardBattleUI.Instance != null && CardBattleUI.Instance.IsInBattle);
            if (playing && !Application.isMobilePlatform) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            else { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }
        }
    }
}
