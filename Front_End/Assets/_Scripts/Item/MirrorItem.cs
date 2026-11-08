using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Gương thay đồ: nhấn tương tác để mở bảng chọn skin, đổi skin xem trực tiếp trong gương,
/// "Chọn" thì lưu + báo cho mọi người, "Hủy" thì trả lại skin cũ.
/// </summary>
public class MirrorItem : Item
{
    [Header("Giao Diện")]
    [Tooltip("Font TMP cho bảng chọn skin. Để trống thì dùng font mặc định của TMP Settings.")]
    [SerializeField] private TMP_FontAsset font;

    private GameObject panel;
    private TextMeshProUGUI nameText;
    private PlayerSkin skin;
    private int originalSkin;

    public override void OnInteract()
    {
        base.OnInteract();
        if (panel != null && panel.activeSelf) return;

        var player = PlayerController.Instance;
        if (player == null || !player.TryGetComponent(out skin) || skin.Catalog == null || skin.Catalog.Count == 0) return;

        if (panel == null) BuildPanel();
        originalSkin = skin.Current;
        panel.SetActive(true);
        player.firstPersonMovement.enabled = false;
        CursorManager.Instance.EnableCursor();
        Refresh();
    }

    private void Step(int dir)
    {
        int count = skin.Catalog.Count;
        skin.SetSkin(((skin.Current + dir) % count + count) % count);
        Refresh();
    }

    private void Close(bool confirm)
    {
        if (confirm) skin.SaveAndBroadcast();
        else skin.SetSkin(originalSkin);

        panel.SetActive(false);
        PlayerController.Instance.firstPersonMovement.enabled = true;
        CursorManager.Instance.HideCursor();
    }

    private void Refresh() =>
        nameText.text = $"{skin.Catalog.DisplayName(skin.Current)}  ({skin.Current + 1}/{skin.Catalog.Count})";

    private void BuildPanel()
    {
        panel = new GameObject("SkinMirrorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = panel.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var box = NewRect("Box", panel.transform, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(720, 190));
        box.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.07f, 0.05f, 0.88f);

        Label(box, "Title", "TRANG PHỤC", 26, new Vector2(0, 62), new Vector2(680, 40)).color = new Color(0.85f, 0.65f, 0.3f);
        nameText = Label(box, "Name", "", 34, new Vector2(0, 10), new Vector2(440, 50));

        Button(box, "Prev", "<", new Vector2(-290, 10), new Vector2(80, 60), () => Step(-1));
        Button(box, "Next", ">", new Vector2(290, 10), new Vector2(80, 60), () => Step(1));
        Button(box, "Ok", "Chọn", new Vector2(-110, -58), new Vector2(200, 54), () => Close(true));
        Button(box, "Cancel", "Hủy", new Vector2(110, -58), new Vector2(200, 54), () => Close(false));
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private TextMeshProUGUI Label(Transform parent, string name, string text, float size, Vector2 pos, Vector2 box)
    {
        var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), pos, box);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.95f, 0.9f, 0.8f);
        t.raycastTarget = false;
        return t;
    }

    private void Button(Transform parent, string name, string text, Vector2 pos, Vector2 size, UnityAction onClick)
    {
        var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), pos, size);
        rt.pivot = new Vector2(0.5f, 0.5f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.36f, 0.24f, 0.15f);
        var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        var label = Label(rt, "Text", text, 28, Vector2.zero, size);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    }
}
