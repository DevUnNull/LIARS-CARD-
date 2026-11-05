using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dàn lại khung hội thoại đăng nhập (RoomHintCanvas/DialoguePanel) giống ảnh mẫu Assets/_Prefabs/Composite/dialogDemo.png,
/// dùng các mảnh sprite đã cắt từ "Dialogue .png" ở Assets/_UI/Dialogue. Chạy lại được nhiều lần.
/// </summary>
public static class StyleLoginUI
{
    private const string Dir = "Assets/_UI/Dialogue/";

    private static readonly Color Brown = new Color(0.27f, 0.15f, 0.11f);
    private static readonly Color BrownSoft = new Color(0.27f, 0.15f, 0.11f, 0.5f);
    private static readonly Color Cream = new Color(0.96f, 0.88f, 0.80f);

    [MenuItem("Tools/Liar's Card/Style Login UI")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        GameObject root = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "RoomHintCanvas") root = r;
        if (root == null) { Debug.LogError("[StyleLoginUI] Không thấy RoomHintCanvas trong scene đang mở (mở MainScene)."); return; }
        var panel = root.transform.Find("DialoguePanel") as RectTransform;
        var area = panel != null ? panel.Find("FormArea") as RectTransform : null;
        if (panel == null || area == null) { Debug.LogError("[StyleLoginUI] Thiếu DialoguePanel/FormArea (chạy Setup Dialogue Form trước)."); return; }

        // Sprite: borders = (trái, dưới, phải, trên) tính theo pixel gốc
        var frame = Sprite_("frame", 420, 100, 180, 290);
        var tag = Sprite_("tag", 80, 30, 80, 30);
        var inEmail = Sprite_("input_email", 125, 55, 75, 55);
        var inPass = Sprite_("input_pass", 120, 55, 75, 55);
        var inPlain = Sprite_("input_plain", 70, 55, 75, 55);
        var btn = Sprite_("btn_login", 200, 60, 80, 60);
        var key = Sprite_("key", 40, 40, 40, 40);
        var divider = Sprite_("divider", 0, 0, 0, 0);
        if (frame == null || tag == null || inEmail == null) { Debug.LogError("[StyleLoginUI] Thiếu sprite trong " + Dir + " (đợi Unity import xong rồi chạy lại)."); return; }

        // ---- Khung chính (đã gồm avatar bartender + bong bóng thoại + nền nâu) ----
        panel.sizeDelta = new Vector2(1500f, 420f);
        var pimg = panel.GetComponent<Image>();
        if (pimg == null) pimg = panel.gameObject.AddComponent<Image>();
        Sliced(pimg, frame, 1.29f);
        pimg.raycastTarget = false;

        Stretch(area);

        // ---- Tên (nhãn cam góc trên trái) ----
        Destroy(panel, "NameTag");
        var tagGo = NewImage("NameTag", panel, tag, 1.32f);
        Place(tagGo.rectTransform, -364, 166, 253, 80);
        tagGo.transform.SetSiblingIndex(0);

        var nameT = panel.Find("NameText")?.GetComponent<TMP_Text>();
        if (nameT != null)
        {
            Place(nameT.rectTransform, -364, 168, 240, 60);
            nameT.color = new Color(0.32f, 0.16f, 0.05f);
            nameT.fontStyle = FontStyles.Bold; nameT.fontSize = 32f; nameT.characterSpacing = 0f;
            nameT.alignment = TextAlignmentOptions.Center;
        }

        // ---- Lời thoại (trong bong bóng kem) ----
        var dlg = panel.Find("DialogueText")?.GetComponent<TMP_Text>();
        if (dlg != null)
        {
            Place(dlg.rectTransform, 130, 62, 1020, 100);
            dlg.color = Brown; dlg.fontSize = 34f; dlg.fontStyle = FontStyles.Bold;
            dlg.alignment = TextAlignmentOptions.MidlineLeft;
            dlg.enableWordWrapping = true;
        }

        // ---- Hàng đăng nhập ----
        var login = area.Find("LoginRow");
        if (login != null)
        {
            Input(login, "Email_Input", inEmail, -405, -72, 470, "Nhập email...", 80);
            Input(login, "Password_Input", inPass, 85, -72, 470, "Nhập mật khẩu...", 80);
            Primary(login, "LoginButton", btn, 495, -72, 300, "ĐĂNG NHẬP");
            Link(login, "BackToRegisterButton", 500, -160, 300, "Tạo tài khoản");
            Status(login, -20, -160, 640);
        }

        // ---- Hàng đăng ký ----
        var reg = area.Find("RegisterRow");
        if (reg != null)
        {
            Input(reg, "Email_Input", inEmail, -470, -72, 350, "Nhập email...", 80);
            Input(reg, "DisplayName_Input", inPlain, -125, -72, 310, "Tên hiển thị...", 35);
            Input(reg, "Password_Input", inPass, 200, -72, 310, "Nhập mật khẩu...", 80);
            Primary(reg, "RegisterButton", btn, 515, -72, 280, "ĐĂNG KÝ");
            Link(reg, "BackToLoginButton", 500, -160, 300, "Đã có tài khoản");
            Status(reg, -20, -160, 640);
        }

        // ---- Hàng dưới: [Esc] Quay lại | ... ----
        Destroy(area, "EscHint"); Destroy(area, "Divider");
        var font = dlg != null ? dlg.font : null;
        var keyImg = NewImage("EscHint", area, key, 1.95);
        Place(keyImg.rectTransform, -600, -160, 90, 42);
        var keyTxt = NewText("Label", keyImg.transform, "Esc", 22f, Cream, FontStyles.Bold, font);
        Stretch(keyTxt.rectTransform); keyTxt.alignment = TextAlignmentOptions.Center;
        var back = NewText("EscText", keyImg.transform, "Quay lại", 24f, Cream, FontStyles.Bold, font);
        Place(back.rectTransform, 125, 0, 160, 40); back.alignment = TextAlignmentOptions.MidlineLeft;
        var div = NewImage("Divider", area, divider, 1f);
        Place(div.rectTransform, 340, -160, 6, 56);
        div.type = Image.Type.Simple; div.preserveAspect = false;

        // Gợi ý "Esc quay lại" đã nằm trong khung -> dòng gợi ý dưới màn hình chỉ còn lúc nhấp bartender (xem LoginRoomController)
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[StyleLoginUI] Xong. Nhớ Ctrl+S.");
    }

    // ---------- helpers ----------
    private static Sprite Sprite_(string name, int l, int b, int r, int t)
    {
        string path = Dir + name + ".png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = new Vector4(l, b, r, t);
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void Sliced(Image img, Sprite s, float ppm)
    {
        img.sprite = s; img.color = Color.white;
        img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppm;
        EditorUtility.SetDirty(img);
    }

    private static void Destroy(Transform parent, string n)
    {
        var t = parent.Find(n);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    private static Image NewImage(string n, Transform parent, Sprite s, double ppm)
    {
        var g = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        g.transform.SetParent(parent, false);
        var img = g.GetComponent<Image>();
        Sliced(img, s, (float)ppm);
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI NewText(string n, Transform parent, string text, float size, Color c, FontStyles style, TMP_FontAsset font)
    {
        var g = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        g.transform.SetParent(parent, false);
        var t = g.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = c; t.fontStyle = style; t.raycastTarget = false;
        return t;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Input(Transform row, string n, Sprite s, float x, float y, float w, string hint, float leftMargin)
    {
        var f = row.Find(n)?.GetComponent<TMP_InputField>();
        if (f == null) return;
        Place((RectTransform)f.transform, x, y, w, 80f);
        var bg = f.GetComponent<Image>();
        Sliced(bg, s, 1.65f);
        f.targetGraphic = bg;

        var cb = f.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, 0.97f, 0.92f);
        cb.selectedColor = Color.white; cb.pressedColor = Color.white; cb.fadeDuration = 0.1f;
        f.colors = cb;

        f.caretColor = new Color(0.85f, 0.25f, 0.15f); f.customCaretColor = true; f.caretWidth = 3;
        f.selectionColor = new Color(0.95f, 0.45f, 0.25f, 0.35f);

        if (f.textComponent != null) { f.textComponent.color = Brown; f.textComponent.fontSize = 28f; f.textComponent.fontStyle = FontStyles.Bold; f.textComponent.margin = new Vector4(leftMargin, 0, 20, 0); }
        if (f.placeholder is TMP_Text ph) { ph.text = hint; ph.color = BrownSoft; ph.fontSize = 28f; ph.fontStyle = FontStyles.Bold; ph.margin = new Vector4(leftMargin, 0, 20, 0); }
        EditorUtility.SetDirty(f);
    }

    private static void Primary(Transform row, string n, Sprite s, float x, float y, float w, string text)
    {
        var b = row.Find(n)?.GetComponent<Button>();
        if (b == null) return;
        Place((RectTransform)b.transform, x, y, w, 100f);
        var bg = b.GetComponent<Image>();
        Sliced(bg, s, 1.56f);
        b.targetGraphic = bg;
        var cb = b.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.1f, 1.06f, 1f);
        cb.pressedColor = new Color(0.78f, 0.7f, 0.65f); cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f); cb.fadeDuration = 0.08f;
        b.colors = cb;

        var l = b.GetComponentInChildren<TMP_Text>(true);
        if (l != null)
        {
            Stretch(l.rectTransform);
            l.rectTransform.offsetMin = new Vector2(125f, 0f); l.rectTransform.offsetMax = new Vector2(-25f, 0f);
            l.text = text; l.color = Color.white; l.fontStyle = FontStyles.Bold; l.fontSize = 32f; l.characterSpacing = 2f;
            l.alignment = TextAlignmentOptions.Center; l.enableAutoSizing = false;
        }
        EditorUtility.SetDirty(b);
    }

    // Nút chữ trần ("Tạo tài khoản"): không nền, rê chuột thì sáng lên
    private static void Link(Transform row, string n, float x, float y, float w, string text)
    {
        var b = row.Find(n)?.GetComponent<Button>();
        if (b == null) return;
        Place((RectTransform)b.transform, x, y, w, 50f);
        var bg = b.GetComponent<Image>();
        bg.sprite = null; bg.color = new Color(1f, 1f, 1f, 0f); bg.type = Image.Type.Simple;

        var l = b.GetComponentInChildren<TMP_Text>(true);
        if (l != null)
        {
            Stretch(l.rectTransform);
            l.rectTransform.offsetMin = Vector2.zero; l.rectTransform.offsetMax = Vector2.zero;
            l.text = text; l.color = Color.white; l.fontStyle = FontStyles.Bold; l.fontSize = 26f; l.characterSpacing = 0f;
            l.alignment = TextAlignmentOptions.Center; l.enableAutoSizing = false;
            b.targetGraphic = l;
        }
        var cb = b.colors;
        cb.normalColor = Cream * 0.9f; cb.normalColor = new Color(cb.normalColor.r, cb.normalColor.g, cb.normalColor.b, 1f);
        cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.8f, 0.7f, 0.6f); cb.fadeDuration = 0.1f;
        b.colors = cb;
        EditorUtility.SetDirty(b);
    }

    private static void Status(Transform row, float x, float y, float w)
    {
        var t = row.Find("StatusText")?.GetComponent<TMP_Text>();
        if (t == null) return;
        Place(t.rectTransform, x, y, w, 50f);
        t.fontSize = 24f; t.color = new Color(1f, 0.82f, 0.55f); t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
    }
}
