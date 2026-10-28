using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class BuildGameplayCanvasEditor
{
    [MenuItem("LiarsCard/Build Gameplay Hierarchy UI")]
    public static void BuildUI()
    {
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var defaultFont = TMP_Settings.defaultFontAsset;

        // 1. Tạo hoặc tìm [Gameplay_Canvas]
        var canvasObj = GameObject.Find("[Gameplay_Canvas]");
        if (canvasObj != null)
        {
            GameObject.DestroyImmediate(canvasObj);
        }

        canvasObj = new GameObject("[Gameplay_Canvas]");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Sprite mặc định cho UI
        var defaultSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        var btnSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ==========================================
        // 2. TẠO BẢNG PHÒNG CHỜ BÀN (TableLobbyPanel)
        // ==========================================
        var lobbyPanel = CreateUIObject("TableLobbyPanel", canvasObj.transform);
        SetRect(lobbyPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(580, 520));
        var lobbyBg = lobbyPanel.AddComponent<Image>();
        lobbyBg.sprite = defaultSprite;
        lobbyBg.type = Image.Type.Sliced;
        lobbyBg.color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

        // Header Title
        var titleObj = CreateUIObject("TitleText", lobbyPanel.transform);
        SetRect(titleObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(540, 42));
        var titleText = AddTMP(titleObj, "♠ LIARS CARD - PHÒNG CHỜ BÀN SỐ 1 ♠", 22, TextAlignmentOptions.Center, Color.white, defaultFont, true);

        // Subtitle
        var subTitleObj = CreateUIObject("SubTitleText", lobbyPanel.transform);
        SetRect(subTitleObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -68), new Vector2(540, 30));
        var subTitleText = AddTMP(subTitleObj, "Ghế của bạn: Ghế 0 | Trạng thái: ĐANG CHỜ BẮT ĐẦU", 15, TextAlignmentOptions.Center, new Color(0.9f, 0.9f, 0.9f), defaultFont, false);

        // Container 4 ghế
        var seatContainer = CreateUIObject("SeatListContainer", lobbyPanel.transform);
        SetRect(seatContainer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 35), new Vector2(530, 240));
        var seatVGroup = seatContainer.AddComponent<VerticalLayoutGroup>();
        seatVGroup.spacing = 8;
        seatVGroup.childControlWidth = true;
        seatVGroup.childControlHeight = true;
        seatVGroup.childForceExpandWidth = true;
        seatVGroup.childForceExpandHeight = false;

        var seatRows = new TableLobbyUI.SeatRowUI[4];
        for (int i = 0; i < 4; i++)
        {
            var rowObj = CreateUIObject($"SeatRow_{i}", seatContainer.transform);
            var rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(530, 50);

            var rowBg = rowObj.AddComponent<Image>();
            rowBg.sprite = defaultSprite;
            rowBg.type = Image.Type.Sliced;
            rowBg.color = new Color(0.18f, 0.2f, 0.26f, 0.85f);

            var rowHGroup = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowHGroup.padding = new RectOffset(12, 12, 6, 6);
            rowHGroup.spacing = 10;
            rowHGroup.childControlWidth = false;
            rowHGroup.childControlHeight = true;
            rowHGroup.childForceExpandWidth = false;
            rowHGroup.childForceExpandHeight = true;

            // Seat Name Text
            var nameObj = CreateUIObject("SeatTitleText", rowObj.transform);
            SetWidth(nameObj, 200);
            var nameTmp = AddTMP(nameObj, $"Ghế {i}:", 15, TextAlignmentOptions.MidlineLeft, Color.white, defaultFont, true);

            // Player ID Text
            var idObj = CreateUIObject("PlayerIdText", rowObj.transform);
            SetWidth(idObj, 150);
            var idTmp = AddTMP(idObj, "(Còn trống)", 14, TextAlignmentOptions.MidlineLeft, new Color(0.7f, 0.7f, 0.7f), defaultFont, false);

            // Ready Badge Text
            var badgeObj = CreateUIObject("ReadyBadgeText", rowObj.transform);
            SetWidth(badgeObj, 140);
            var badgeTmp = AddTMP(badgeObj, "", 14, TextAlignmentOptions.MidlineRight, Color.green, defaultFont, true);

            seatRows[i] = new TableLobbyUI.SeatRowUI
            {
                root = rowObj,
                seatTitleText = nameTmp,
                playerIdText = idTmp,
                readyBadgeText = badgeTmp
            };
        }

        // Action Buttons Row (Ready + Start)
        var btnRow = CreateUIObject("ActionButtonsRow", lobbyPanel.transform);
        SetRect(btnRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 115), new Vector2(530, 52));
        var btnHGroup = btnRow.AddComponent<HorizontalLayoutGroup>();
        btnHGroup.spacing = 12;
        btnHGroup.childControlWidth = true;
        btnHGroup.childControlHeight = true;
        btnHGroup.childForceExpandWidth = true;
        btnHGroup.childForceExpandHeight = true;

        // Ready Button
        var readyBtnObj = CreateButton("ReadyButton", btnRow.transform, btnSprite, new Color(0.18f, 0.7f, 0.35f), "SẴN SÀNG (READY)", defaultFont, 16);
        var readyBtn = readyBtnObj.GetComponent<Button>();
        var readyBtnBg = readyBtnObj.GetComponent<Image>();
        var readyBtnText = readyBtnObj.GetComponentInChildren<TMP_Text>();

        // Start Match Button
        var startBtnObj = CreateButton("StartMatchButton", btnRow.transform, btnSprite, new Color(0.95f, 0.75f, 0.15f), "BẮT ĐẦU TRẬN ĐẤU", defaultFont, 16);
        var startBtn = startBtnObj.GetComponent<Button>();
        var startBtnBg = startBtnObj.GetComponent<Image>();
        var startBtnText = startBtnObj.GetComponentInChildren<TMP_Text>();

        // Leave Table Button
        var leaveBtnObj = CreateButton("LeaveButton", lobbyPanel.transform, btnSprite, new Color(0.45f, 0.45f, 0.5f), "RỜI BÀN (ESC)", defaultFont, 14);
        SetRect(leaveBtnObj, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(530, 40));
        var leaveBtn = leaveBtnObj.GetComponent<Button>();

        // Status Text
        var statusObj = CreateUIObject("StatusText", lobbyPanel.transform);
        SetRect(statusObj, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 18), new Vector2(530, 26));
        var statusTmp = AddTMP(statusObj, "", 13, TextAlignmentOptions.Center, Color.yellow, defaultFont, false);

        // ==========================================
        // 3. TẠO BATTLE HUD (Giao diện trong trận)
        // ==========================================
        var battleHUD = CreateUIObject("BattleHUD", canvasObj.transform);
        SetRectFill(battleHUD);

        // Top Header
        var topHeader = CreateUIObject("TopHeaderPanel", battleHUD.transform);
        SetRect(topHeader, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(740, 80));
        var topBg = topHeader.AddComponent<Image>();
        topBg.sprite = defaultSprite;
        topBg.type = Image.Type.Sliced;
        topBg.color = new Color(0.12f, 0.13f, 0.17f, 0.9f);

        var topHGroup = topHeader.AddComponent<HorizontalLayoutGroup>();
        topHGroup.padding = new RectOffset(16, 16, 8, 8);
        topHGroup.spacing = 15;
        topHGroup.childControlWidth = false;
        topHGroup.childControlHeight = true;
        topHGroup.childForceExpandWidth = false;
        topHGroup.childForceExpandHeight = true;

        var roundObj = CreateUIObject("RoundText", topHeader.transform);
        SetWidth(roundObj, 120);
        var roundTmp = AddTMP(roundObj, "<b>VÒNG: 1</b>\n<size=12>Ver: 1</size>", 16, TextAlignmentOptions.MidlineLeft, Color.white, defaultFont, true);

        var rankObj = CreateUIObject("TableRankText", topHeader.transform);
        SetWidth(rankObj, 360);
        var rankTmp = AddTMP(rankObj, "RANK BÀN: <color=#F1C40F><b>QUÂN KING (K)</b></color>", 18, TextAlignmentOptions.Center, Color.white, defaultFont, true);

        var turnObj = CreateUIObject("CurrentTurnText", topHeader.transform);
        SetWidth(turnObj, 200);
        var turnTmp = AddTMP(turnObj, "<color=#2ECC71><b>👉 LƯỢT CỦA BẠN! [E]</b></color>", 16, TextAlignmentOptions.MidlineRight, Color.white, defaultFont, true);

        // Arena Last Play Banner
        var arenaObj = CreateUIObject("ArenaInfoPanel", battleHUD.transform);
        SetRect(arenaObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -135), new Vector2(580, 60));
        var arenaBg = arenaObj.AddComponent<Image>();
        arenaBg.sprite = defaultSprite;
        arenaBg.type = Image.Type.Sliced;
        arenaBg.color = new Color(0.12f, 0.13f, 0.17f, 0.85f);

        var lastPlayTextObj = CreateUIObject("LastPlayText", arenaObj.transform);
        SetRect(lastPlayTextObj, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 50));
        var lastPlayTmp = AddTMP(lastPlayTextObj, "<i>(Chưa có ai đánh bài trong vòng này)</i>", 14, TextAlignmentOptions.Center, Color.white, defaultFont, false);

        // Bottom HUD Bar
        var bottomHUD = CreateUIObject("BottomHUDPanel", battleHUD.transform);
        SetRect(bottomHUD, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 65), new Vector2(760, 110));
        var bottomBg = bottomHUD.AddComponent<Image>();
        bottomBg.sprite = defaultSprite;
        bottomBg.type = Image.Type.Sliced;
        bottomBg.color = new Color(0.12f, 0.13f, 0.17f, 0.9f);

        var hintObj = CreateUIObject("HintText", bottomHUD.transform);
        SetRect(hintObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(730, 28));
        var hintTmp = AddTMP(hintObj, "<color=#F1C40F><b>[GIỮ SPACE / CHUỘT PHẢI]:</b></color> HÉ BÀI  |  <color=#F1C40F><b>[A/D/CLICK]:</b></color> CHỌN LÁ  |  <color=#F1C40F><b>[E]:</b></color> HẠ BÀI  |  <color=#F1C40F><b>[X]:</b></color> BẮT LÁO", 13, TextAlignmentOptions.Center, Color.white, defaultFont, false);

        var actionRow = CreateUIObject("ActionButtonsRow", bottomHUD.transform);
        SetRect(actionRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(720, 50));
        var actionHGroup = actionRow.AddComponent<HorizontalLayoutGroup>();
        actionHGroup.spacing = 15;
        actionHGroup.childControlWidth = true;
        actionHGroup.childControlHeight = true;
        actionHGroup.childForceExpandWidth = true;
        actionHGroup.childForceExpandHeight = true;

        var playBtnObj = CreateButton("PlayCardsButton", actionRow.transform, btnSprite, new Color(0.18f, 0.7f, 0.35f), "HẠ BÀI [E]", defaultFont, 16);
        var playBtn = playBtnObj.GetComponent<Button>();
        var playBtnText = playBtnObj.GetComponentInChildren<TMP_Text>();

        var callBtnObj = CreateButton("CallLiarButton", actionRow.transform, btnSprite, new Color(0.9f, 0.45f, 0.15f), "BẮT LÁO (CALL LIAR) [X]", defaultFont, 16);
        var callBtn = callBtnObj.GetComponent<Button>();

        // Mặc định ẩn BattleHUD cho đến khi vào ván
        battleHUD.SetActive(false);

        // ==========================================
        // 4. TẠO MODAL BÓP CÒ SÚNG NGA (Russian Roulette)
        // ==========================================
        var rrModal = CreateUIObject("RussianRouletteModal", canvasObj.transform);
        SetRectFill(rrModal);

        var rrDim = CreateUIObject("DimBackground", rrModal.transform);
        SetRectFill(rrDim);
        var rrDimImg = rrDim.AddComponent<Image>();
        rrDimImg.color = new Color(0.04f, 0.04f, 0.08f, 0.95f);

        var rrBox = CreateUIObject("ModalBox", rrModal.transform);
        SetRect(rrBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 320));
        var rrBoxImg = rrBox.AddComponent<Image>();
        rrBoxImg.sprite = defaultSprite;
        rrBoxImg.type = Image.Type.Sliced;
        rrBoxImg.color = new Color(0.15f, 0.16f, 0.22f, 0.98f);

        var rrTitleObj = CreateUIObject("TitleText", rrBox.transform);
        SetRect(rrTitleObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(540, 40));
        var rrTitleTmp = AddTMP(rrTitleObj, "💀 CÒ QUAY NGA (RUSSIAN ROULETTE) 💀", 20, TextAlignmentOptions.Center, Color.white, defaultFont, true);

        var rrDetailObj = CreateUIObject("DetailText", rrBox.transform);
        SetRect(rrDetailObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(520, 80));
        var rrDetailTmp = AddTMP(rrDetailObj, "Bạn vừa bị bắt quả tang nói dối! Hãy chuẩn bị bóp cò!", 16, TextAlignmentOptions.Center, Color.white, defaultFont, false);

        // Progress Slider
        var sliderObj = CreateSlider("ProgressBar", rrBox.transform, defaultSprite);
        SetRect(sliderObj, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(480, 26));
        var rrSlider = sliderObj.GetComponent<Slider>();

        // Trigger Button
        var rrTriggerBtnObj = CreateButton("TriggerButton", rrBox.transform, btnSprite, new Color(0.85f, 0.25f, 0.25f), "[🔫 CLICK ĐỂ BÓP CÒ NGAY]", defaultFont, 16);
        SetRect(rrTriggerBtnObj, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 48), new Vector2(480, 44));
        var rrTriggerBtn = rrTriggerBtnObj.GetComponent<Button>();
        var rrTriggerBtnText = rrTriggerBtnObj.GetComponentInChildren<TMP_Text>();

        rrModal.SetActive(false);

        // ==========================================
        // 5. TẠO MODAL KẾT THÚC / CHIẾN THẮNG (GameOverModal)
        // ==========================================
        var goModal = CreateUIObject("GameOverModal", canvasObj.transform);
        SetRectFill(goModal);

        var goDim = CreateUIObject("DimBackground", goModal.transform);
        SetRectFill(goDim);
        var goDimImg = goDim.AddComponent<Image>();
        goDimImg.color = new Color(0.04f, 0.04f, 0.08f, 0.95f);

        var goBox = CreateUIObject("ModalBox", goModal.transform);
        SetRect(goBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 320));
        var goBoxImg = goBox.AddComponent<Image>();
        goBoxImg.sprite = defaultSprite;
        goBoxImg.type = Image.Type.Sliced;
        goBoxImg.color = new Color(0.15f, 0.16f, 0.22f, 0.98f);

        var goTitleObj = CreateUIObject("TitleText", goBox.transform);
        SetRect(goTitleObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(540, 44));
        var goTitleTmp = AddTMP(goTitleObj, "🏆 CHIẾN THẮNG TUYỆT ĐỐI! 🏆", 22, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.2f), defaultFont, true);

        var goWinnerObj = CreateUIObject("WinnerText", goBox.transform);
        SetRect(goWinnerObj, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -95), new Vector2(520, 60));
        var goWinnerTmp = AddTMP(goWinnerObj, "Bạn là người sống sót duy nhất tại bàn chơi Liar's Card!", 16, TextAlignmentOptions.Center, Color.white, defaultFont, false);

        var goReturnBtnObj = CreateButton("ReturnToLobbyButton", goBox.transform, btnSprite, new Color(0.2f, 0.7f, 0.9f), "QUAY VỀ PHÒNG CHỜ BÀN", defaultFont, 16);
        SetRect(goReturnBtnObj, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(480, 44));
        var goReturnBtn = goReturnBtnObj.GetComponent<Button>();

        var goLeaveBtnObj = CreateButton("LeaveButton", goBox.transform, btnSprite, new Color(0.85f, 0.3f, 0.3f), "RỜI BÀN (ESC)", defaultFont, 15);
        SetRect(goLeaveBtnObj, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(480, 40));
        var goLeaveBtn = goLeaveBtnObj.GetComponent<Button>();

        goModal.SetActive(false);

        // ==========================================
        // 6. GẮN THAM CHIẾU VÀO [Gameplay_Managers]
        // ==========================================
        var gm = GameObject.Find("[Gameplay_Managers]");
        if (gm != null)
        {
            var lobbyUI = gm.GetComponent<TableLobbyUI>();
            if (lobbyUI != null)
            {
                lobbyUI.enableOnGUI = false;
                lobbyUI.lobbyPanel = lobbyPanel;
                lobbyUI.titleText = titleText;
                lobbyUI.subTitleText = subTitleText;
                lobbyUI.statusText = statusTmp;
                lobbyUI.seatRows = seatRows;
                lobbyUI.readyButton = readyBtn;
                lobbyUI.readyButtonText = readyBtnText;
                lobbyUI.readyButtonBg = readyBtnBg;
                lobbyUI.startMatchButton = startBtn;
                lobbyUI.startButtonText = startBtnText;
                lobbyUI.startButtonBg = startBtnBg;
                lobbyUI.leaveButton = leaveBtn;

                EditorUtility.SetDirty(lobbyUI);
            }

            var battleUI = gm.GetComponent<CardBattleUI>();
            if (battleUI != null)
            {
                battleUI.enableOnGUI = false;
                battleUI.battleHUD = battleHUD;
                battleUI.roundText = roundTmp;
                battleUI.tableRankText = rankTmp;
                battleUI.currentTurnText = turnTmp;
                battleUI.lastPlayText = lastPlayTmp;
                battleUI.hudHintText = hintTmp;
                battleUI.playCardsButton = playBtn;
                battleUI.playCardsButtonText = playBtnText;
                battleUI.callLiarButton = callBtn;

                battleUI.russianRouletteModal = rrModal;
                battleUI.rrTitleText = rrTitleTmp;
                battleUI.rrDetailText = rrDetailTmp;
                battleUI.rrProgressBar = rrSlider;
                battleUI.rrTriggerButton = rrTriggerBtn;
                battleUI.rrTriggerButtonText = rrTriggerBtnText;

                battleUI.gameOverModal = goModal;
                battleUI.goTitleText = goTitleTmp;
                battleUI.goWinnerText = goWinnerTmp;
                battleUI.goReturnButton = goReturnBtn;
                battleUI.goLeaveButton = goLeaveBtn;

                EditorUtility.SetDirty(battleUI);
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[BUILD_UI_SUCCESS] Successfully generated [Gameplay_Canvas] with TableLobbyPanel, BattleHUD, RussianRouletteModal, GameOverModal on Hierarchy!");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void SetRect(GameObject obj, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
    }

    private static void SetRectFill(GameObject obj)
    {
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetWidth(GameObject obj, float width)
    {
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        var le = obj.AddComponent<LayoutElement>();
        le.preferredWidth = width;
    }

    private static TMP_Text AddTMP(GameObject obj, string text, float fontSize, TextAlignmentOptions align, Color color, TMP_FontAsset font, bool bold)
    {
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        if (font != null) tmp.font = font;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    private static GameObject CreateButton(string name, Transform parent, Sprite sprite, Color color, string label, TMP_FontAsset font, float fontSize)
    {
        var btnObj = CreateUIObject(name, parent);
        var img = btnObj.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;

        var btn = btnObj.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        var textObj = CreateUIObject("Text", btnObj.transform);
        SetRectFill(textObj);
        AddTMP(textObj, label, fontSize, TextAlignmentOptions.Center, Color.white, font, true);

        return btnObj;
    }

    private static GameObject CreateSlider(string name, Transform parent, Sprite sprite)
    {
        var sliderObj = CreateUIObject(name, parent);
        var slider = sliderObj.AddComponent<Slider>();

        var bgObj = CreateUIObject("Background", sliderObj.transform);
        SetRectFill(bgObj);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = sprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

        var fillAreaObj = CreateUIObject("Fill Area", sliderObj.transform);
        SetRectFill(fillAreaObj);

        var fillObj = CreateUIObject("Fill", fillAreaObj.transform);
        SetRectFill(fillObj);
        var fillImg = fillObj.AddComponent<Image>();
        fillImg.sprite = sprite;
        fillImg.type = Image.Type.Sliced;
        fillImg.color = new Color(0.9f, 0.2f, 0.2f, 1f);

        slider.fillRect = fillObj.GetComponent<RectTransform>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;

        return sliderObj;
    }
}
