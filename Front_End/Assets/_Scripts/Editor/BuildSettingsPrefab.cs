using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu Build/Tạo prefab Cài đặt: dựng prefab Assets/Resources/SettingsPanel.prefab một lần để bạn tự chỉnh giao diện.
/// Nếu prefab đã tồn tại thì KHÔNG ghi đè (tránh mất chỉnh sửa), xoá prefab đi nếu muốn dựng lại.
/// </summary>
public static class BuildSettingsPrefab
{
    const string Path = "Assets/Resources/SettingsPanel.prefab";
    static readonly Color Dark = new Color(0.12f, 0.08f, 0.06f, 0.92f), Accent = new Color(0.85f, 0.6f, 0.25f);

    [MenuItem("Build/Tạo prefab Cài đặt")]
    public static void Build()
    {
        if (File.Exists(Path)) { Debug.LogWarning($"{Path} đã có, không ghi đè. Xoá nó đi nếu muốn dựng lại."); return; }
        Directory.CreateDirectory("Assets/Resources");

        var root = new GameObject("SettingsPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SettingsPanelUI));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var ui = root.GetComponent<SettingsPanelUI>();

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(root.transform, false);
        panel.GetComponent<Image>().color = Dark;
        var rt = (RectTransform)panel.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(800, 640);
        var v = panel.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(50, 50, 40, 40); v.spacing = 18;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandHeight = false;
        ui.panel = rt;

        Label(panel.transform, "Title", "CÀI ĐẶT", 56, Accent);
        Label(panel.transform, "VolumeLabel", "Âm lượng", 38, Color.white);
        ui.volumeSlider = Slider(panel.transform, "VolumeSlider", 0f, 1f, 1f);
        Label(panel.transform, "SensitivityLabel", "Độ nhạy xoay", 38, Color.white);
        ui.sensitivitySlider = Slider(panel.transform, "SensitivitySlider", 0.5f, 6f, 2f);
        ui.graphicsButton = Button(panel.transform, "GraphicsButton", "Đồ họa", out ui.graphicsText);
        ui.closeButton = Button(panel.transform, "CloseButton", "ĐÓNG", out _);

        PrefabUtility.SaveAsPrefabAsset(root, Path);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        Debug.Log($"Đã tạo {Path}. Mở prefab này để chỉnh giao diện.");
    }

    static void Label(Transform parent, string name, string text, float size, Color color)
    {
        var t = NewText(parent, name, text, size, color);
        t.alignment = TextAlignmentOptions.Left;
        t.gameObject.AddComponent<LayoutElement>().preferredHeight = size + 14;
    }

    static Slider Slider(Transform parent, string name, float min, float max, float value)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 50;
        var bg = NewImage(go.transform, "Background", new Color(1, 1, 1, 0.2f));
        Stretch(bg.rectTransform, 0, 0.35f, 1, 0.65f);
        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(go.transform, false);
        Stretch(fillArea, 0, 0.35f, 1, 0.65f);
        var fill = NewImage(fillArea, "Fill", Accent);
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(go.transform, false);
        Stretch(handleArea, 0, 0, 1, 1);
        handleArea.offsetMin = new Vector2(20, 0); handleArea.offsetMax = new Vector2(-20, 0);
        var handle = NewImage(handleArea, "Handle", Color.white);
        handle.rectTransform.sizeDelta = new Vector2(40, 50);

        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.minValue = min; s.maxValue = max; s.value = value;
        return s;
    }

    static Button Button(Transform parent, string name, string label, out TMP_Text text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.35f, 0.22f, 0.12f, 0.95f);
        go.GetComponent<LayoutElement>().preferredHeight = 90;
        text = NewText(go.transform, "Text", label, 40, Color.white);
        Stretch(text.rectTransform, 0, 0, 1, 1);
        var b = go.GetComponent<Button>();
        b.targetGraphic = img;
        return b;
    }

    static void Stretch(RectTransform r, float x0, float y0, float x1, float y1)
    {
        r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Image NewImage(Transform parent, string name, Color c)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        return img;
    }

    static TMP_Text NewText(Transform parent, string name, string text, float size, Color c)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.text = text; t.fontSize = size; t.color = c; t.raycastTarget = false; t.alignment = TextAlignmentOptions.Center;
        return t;
    }
}
