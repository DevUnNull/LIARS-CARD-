using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu Build/Tạo prefab Cài đặt: dựng prefab Assets/Resources/SettingsPanel.prefab
/// chuẩn phong cách phương Tây / quán bar Liar's Bar (Dark Wood, Vintage Brass & Amber Gold).
/// Hỗ trợ:
/// 1. Thanh trượt âm lượng tổng (có hiển thị %)
/// 2. Thanh trượt độ nhạy chuột (có hiển thị x)
/// 3. Nút chuyển đổi đồ họa PC / Mobile (với badge màu sắc)
/// 4. Nút khóa FPS (VSync, 30, 60, 75, 120, 144, không giới hạn)
/// 5. Nút ĐÓNG nổi bật (màu đỏ nhung rượu Liar's Bar) + phím tắt [Esc]
/// 6. Nền tối mờ toàn màn hình (Backdrop Blocker)
/// </summary>
public static class BuildSettingsPrefab
{
    const string PrefabPath = "Assets/Resources/SettingsPanel.prefab";
    const string DialogueDir = "Assets/_UI/Dialogue/";

    // Bảng màu chuẩn phong cách Liar's Bar
    static readonly Color DarkWood = new Color(0.16f, 0.10f, 0.07f, 0.98f);     // Gỗ mun nâu sẫm
    static readonly Color TrackDark = new Color(0.08f, 0.05f, 0.04f, 0.95f);    // Rãnh khắc chìm đen gỗ
    static readonly Color AmberGold = new Color(0.92f, 0.68f, 0.28f, 1f);       // Vàng hổ phách / đồng thau cổ
    static readonly Color CreamText = new Color(0.96f, 0.90f, 0.82f, 1f);       // Giấy da kem cổ
    static readonly Color SoftMuted = new Color(0.75f, 0.68f, 0.58f, 1f);       // Chữ phụ mờ
    static readonly Color WoodButton = new Color(0.32f, 0.20f, 0.12f, 1f);      // Nút gỗ mahogany
    static readonly Color WineRedButton = new Color(0.55f, 0.15f, 0.14f, 1f);   // Đỏ nhung rượu Liar's Bar
    static readonly Color BackdropDark = new Color(0.04f, 0.02f, 0.01f, 0.78f); // Nền tối che mờ bar

    [MenuItem("Build/Dựng lại prefab Cài đặt (Phong cách Liar's Bar)")]
    public static void ForceBuild()
    {
        BuildInternal(true);
    }

    [MenuItem("Build/Tạo prefab Cài đặt")]
    public static void Build()
    {
        BuildInternal(false);
    }

    private static void BuildInternal(bool forceOverwrite)
    {
        if (!forceOverwrite && File.Exists(PrefabPath))
        {
            Debug.LogWarning($"{PrefabPath} đã có. Dùng menu 'Build/Dựng lại prefab Cài đặt (Phong cách Liar's Bar)' để tạo lại giao diện mới.");
            return;
        }

        Directory.CreateDirectory("Assets/Resources");

        // Load các sprite đã 9-slice sẵn từ Assets/_UI/Dialogue
        var sprFrame = LoadSprite("input_plain");
        var sprBtn = LoadSprite("btn_login");
        var sprTag = LoadSprite("tag");
        var sprDivider = LoadSprite("divider");
        var sprKey = LoadSprite("key");

        // Canvas gốc
        var root = new GameObject("SettingsPanel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SettingsPanelUI));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var ui = root.GetComponent<SettingsPanelUI>();

        // 1. Nền tối mờ toàn màn hình (Backdrop)
        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        backdropGo.transform.SetParent(root.transform, false);
        var bdRt = (RectTransform)backdropGo.transform;
        Stretch(bdRt, 0, 0, 1, 1);
        var bdImg = backdropGo.GetComponent<Image>();
        bdImg.color = BackdropDark;
        ui.backdrop = bdRt;

        // 2. Khung Bảng Chính (Panel)
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelGo.transform.SetParent(root.transform, false);
        var pImg = panelGo.GetComponent<Image>();
        if (sprFrame != null)
        {
            pImg.sprite = sprFrame;
            pImg.type = Image.Type.Sliced;
            pImg.color = DarkWood;
        }
        else
        {
            pImg.color = DarkWood;
        }

        var pRt = (RectTransform)panelGo.transform;
        pRt.anchorMin = pRt.anchorMax = pRt.pivot = new Vector2(0.5f, 0.5f);
        pRt.sizeDelta = new Vector2(760, 690);
        ui.panel = pRt;

        var vGroup = panelGo.GetComponent<VerticalLayoutGroup>();
        vGroup.padding = new RectOffset(45, 45, 32, 32);
        vGroup.spacing = 15;
        vGroup.childControlWidth = true;
        vGroup.childControlHeight = true;
        vGroup.childForceExpandHeight = false;

        // 3. Tiêu đề (Tag Badge)
        var tagContainer = new GameObject("HeaderContainer", typeof(RectTransform), typeof(LayoutElement));
        tagContainer.transform.SetParent(pRt, false);
        tagContainer.GetComponent<LayoutElement>().preferredHeight = 60;

        var tagImgGo = new GameObject("TagBadge", typeof(RectTransform), typeof(Image));
        tagImgGo.transform.SetParent(tagContainer.transform, false);
        var tagImg = tagImgGo.GetComponent<Image>();
        if (sprTag != null)
        {
            tagImg.sprite = sprTag;
            tagImg.type = Image.Type.Sliced;
            tagImg.color = AmberGold;
        }
        else
        {
            tagImg.color = AmberGold;
        }
        var tagRt = (RectTransform)tagImgGo.transform;
        tagRt.anchorMin = tagRt.anchorMax = tagRt.pivot = new Vector2(0.5f, 0.5f);
        tagRt.sizeDelta = new Vector2(360, 56);

        var titleTxt = NewText(tagImgGo.transform, "Title", "CÀI ĐẶT HỆ THỐNG", 26, new Color(0.24f, 0.12f, 0.04f, 1f));
        titleTxt.fontStyle = FontStyles.Bold;
        Stretch(titleTxt.rectTransform, 0, 0, 1, 1);

        // Đường kẻ phân cách
        var divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divGo.transform.SetParent(pRt, false);
        divGo.GetComponent<LayoutElement>().preferredHeight = 4;
        var divImg = divGo.GetComponent<Image>();
        divImg.color = new Color(AmberGold.r, AmberGold.g, AmberGold.b, 0.45f);

        // 4. Mục Âm Lượng (Volume)
        var volSection = new GameObject("VolumeSection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        volSection.transform.SetParent(pRt, false);
        volSection.GetComponent<LayoutElement>().preferredHeight = 74;
        var volV = volSection.GetComponent<VerticalLayoutGroup>();
        volV.spacing = 6; volV.childControlWidth = volV.childControlHeight = true; volV.childForceExpandHeight = false;

        var volHeader = new GameObject("VolumeHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        volHeader.transform.SetParent(volSection.transform, false);
        volHeader.GetComponent<LayoutElement>().preferredHeight = 28;
        var volH = volHeader.GetComponent<HorizontalLayoutGroup>();
        volH.childControlWidth = true; volH.childControlHeight = true; volH.childForceExpandWidth = false; volH.childForceExpandHeight = false;

        var volLbl = NewText(volHeader.transform, "VolumeLabel", "Âm Lượng Tổng", 23, CreamText);
        volLbl.fontStyle = FontStyles.Bold;
        volLbl.alignment = TextAlignmentOptions.MidlineLeft;
        volLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        ui.volumeValueText = NewText(volHeader.transform, "VolumeValueText", "100%", 23, AmberGold);
        ui.volumeValueText.fontStyle = FontStyles.Bold;
        ui.volumeValueText.alignment = TextAlignmentOptions.MidlineRight;
        ui.volumeValueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;

        ui.volumeSlider = CreateSlider(volSection.transform, "VolumeSlider", 0f, 1f, 1f, sprFrame, sprKey);

        // 5. Mục Độ Nhạy Chuột (Sensitivity)
        var sensSection = new GameObject("SensitivitySection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        sensSection.transform.SetParent(pRt, false);
        sensSection.GetComponent<LayoutElement>().preferredHeight = 74;
        var sensV = sensSection.GetComponent<VerticalLayoutGroup>();
        sensV.spacing = 6; sensV.childControlWidth = sensV.childControlHeight = true; sensV.childForceExpandHeight = false;

        var sensHeader = new GameObject("SensitivityHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        sensHeader.transform.SetParent(sensSection.transform, false);
        sensHeader.GetComponent<LayoutElement>().preferredHeight = 28;
        var sensH = sensHeader.GetComponent<HorizontalLayoutGroup>();
        sensH.childControlWidth = true; sensH.childControlHeight = true; sensH.childForceExpandWidth = false; sensH.childForceExpandHeight = false;

        var sensLbl = NewText(sensHeader.transform, "SensitivityLabel", "Độ Nhạy Chuột", 23, CreamText);
        sensLbl.fontStyle = FontStyles.Bold;
        sensLbl.alignment = TextAlignmentOptions.MidlineLeft;
        sensLbl.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        ui.sensitivityValueText = NewText(sensHeader.transform, "SensitivityValueText", "2.0x", 23, AmberGold);
        ui.sensitivityValueText.fontStyle = FontStyles.Bold;
        ui.sensitivityValueText.alignment = TextAlignmentOptions.MidlineRight;
        ui.sensitivityValueText.gameObject.AddComponent<LayoutElement>().preferredWidth = 120;

        ui.sensitivitySlider = CreateSlider(sensSection.transform, "SensitivitySlider", 0.5f, 6f, 2f, sprFrame, sprKey);

        // Khoảng đệm
        var padGo = new GameObject("Pad", typeof(RectTransform), typeof(LayoutElement));
        padGo.transform.SetParent(pRt, false);
        padGo.GetComponent<LayoutElement>().preferredHeight = 4;

        // 6. Nút Đồ Họa PC / Mobile
        ui.graphicsButton = CreateButton(pRt, "GraphicsButton", "ĐỒ HỌA:  <color=#F1C40F><b>PC</b></color> <size=75%>(Chi tiết cao)</size>", WoodButton, sprFrame, out ui.graphicsText);

        // 7. Nút Khóa FPS
        ui.fpsButton = CreateButton(pRt, "FpsButton", "KHÓA FPS:  <color=#F1C40F><b>VSync (75 Hz)</b></color>", WoodButton, sprFrame, out ui.fpsText);

        // Khoảng đệm trước nút đóng
        var padGo2 = new GameObject("Pad2", typeof(RectTransform), typeof(LayoutElement));
        padGo2.transform.SetParent(pRt, false);
        padGo2.GetComponent<LayoutElement>().preferredHeight = 4;

        // 8. Nút ĐÓNG (Close Button) - Phong cách Đỏ Nhung Rượu Liar's Bar
        ui.closeButton = CreateButton(pRt, "CloseButton", "ĐÓNG CÀI ĐẶT  <size=75%>[Esc]</size>", WineRedButton, sprFrame, out ui.closeText);
        if (ui.closeText != null)
        {
            ui.closeText.color = new Color(1f, 0.94f, 0.90f, 1f);
            ui.closeText.fontStyle = FontStyles.Bold;
        }

        // Tự gán các trường và kiểm tra
        ui.AutoAssign();

        // Lưu Prefab
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();

        // Đồng bộ lại instance trong scene nếu đang mở
        var activeMenu = Object.FindFirstObjectByType<SettingsMenu>();
        if (activeMenu != null)
        {
            var inst = activeMenu.transform.Find("SettingsPanel");
            if (inst != null)
            {
                PrefabUtility.RevertPrefabInstance(inst.gameObject, InteractionMode.AutomatedAction);
                var activeUi = inst.GetComponent<SettingsPanelUI>();
                if (activeUi != null) activeUi.AutoAssign();
            }
        }

        Debug.Log($"<color=green>[BuildSettingsPrefab] Đã tạo thành công {PrefabPath} phong cách Liar's Bar!</color>");
    }

    private static Sprite LoadSprite(string name)
    {
        string path = DialogueDir + name + ".png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Slider CreateSlider(Transform parent, string name, float min, float max, float value, Sprite sprTrack, Sprite sprHandle)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 36;

        // Track Background
        var bg = NewImage(go.transform, "Background", TrackDark);
        if (sprTrack != null)
        {
            bg.sprite = sprTrack;
            bg.type = Image.Type.Sliced;
        }
        Stretch(bg.rectTransform, 0, 0.25f, 1, 0.75f);

        // Fill Area
        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(go.transform, false);
        Stretch(fillArea, 0, 0.25f, 1, 0.75f);
        fillArea.offsetMin = new Vector2(8, 0);
        fillArea.offsetMax = new Vector2(-8, 0);

        var fill = NewImage(fillArea, "Fill", AmberGold);
        Stretch(fill.rectTransform, 0, 0, 1, 1);

        // Handle Area
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(go.transform, false);
        Stretch(handleArea, 0, 0, 1, 1);
        handleArea.offsetMin = new Vector2(16, 0);
        handleArea.offsetMax = new Vector2(-16, 0);

        var handle = NewImage(handleArea, "Handle", new Color(1f, 0.94f, 0.82f, 1f));
        if (sprHandle != null)
        {
            handle.sprite = sprHandle;
            handle.type = Image.Type.Sliced;
        }
        handle.rectTransform.sizeDelta = new Vector2(32, 32);

        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.minValue = min;
        s.maxValue = max;
        s.value = value;
        return s;
    }

    private static Button CreateButton(Transform parent, string name, string label, Color color, Sprite sprBtn, out TMP_Text text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 72;

        var img = go.GetComponent<Image>();
        img.color = color;
        if (sprBtn != null)
        {
            img.sprite = sprBtn;
            img.type = Image.Type.Sliced;
        }

        text = NewText(go.transform, "Text", label, 24, CreamText);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform, 0, 0, 1, 1);

        var b = go.GetComponent<Button>();
        b.targetGraphic = img;

        // Hiệu ứng hover / click
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        b.colors = colors;

        return b;
    }

    private static void Stretch(RectTransform r, float x0, float y0, float x1, float y1)
    {
        r.anchorMin = new Vector2(x0, y0);
        r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static Image NewImage(Transform parent, string name, Color c)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = c;
        return img;
    }

    private static TMP_Text NewText(Transform parent, string name, string text, float size, Color c)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.text = text;
        t.fontSize = size;
        t.color = c;
        t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }
}
