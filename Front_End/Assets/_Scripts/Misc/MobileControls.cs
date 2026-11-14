using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem.EnhancedTouch;
using LiarsCard.Cards3D;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>Đầu vào cảm ứng, FirstPersonMovement / FirstPersonLook cộng thêm vào input bàn phím + chuột.</summary>
public static class MobileInput
{
    public static Vector2 Move;
    public static Vector2 Look;
    public static bool Peek; // giữ nút xem bài = giữ Space
}

/// <summary>
/// Điều khiển cảm ứng, chỉ tự bật trên Android/iOS (web và PC không bị ảnh hưởng):
/// joystick trái = đi, vuốt phải = xoay đầu, chạm lá bài = chọn,
/// nút ĐÁNH (E) / TỐ CÁO (X) / TIN (C) hiện đúng lúc, dùng lại sprite keycap của bảng Tutorial.
/// Không dùng EventSystem: tự xử lý chạm bằng EnhancedTouch nên không dính input của UI khác.
/// </summary>
[DefaultExecutionOrder(-100)]
public class MobileControls : MonoBehaviour
{
    const float StickRadius = 130f;   // px màn hình
    const float LookScale = 0.15f;    // px vuốt -> đơn vị mà FirstPersonLook nhân thêm sensitivity
    const float TapMaxMove = 25f;
    const float TapMaxTime = 0.4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Application.isMobilePlatform) return;
        Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
        DontDestroyOnLoad(new GameObject("MobileControls", typeof(MobileControls)));
    }

    RectTransform stickBase, stickKnob, playBtn, accuseBtn, trustBtn, peekBtn, interactBtn, leaveBtn;
    bool spritesReady;
    int stickFinger = -1, lookFinger = -1, peekFinger = -1;
    Vector2 stickOrigin, lookStart;
    float lookTime, lookMoved;

    void Awake()
    {
        EnhancedTouchSupport.Enable();

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var circle = MakeCircle();
        stickBase = MakeImage("StickBase", circle, new Color(1, 1, 1, 0.25f), StickRadius * 2);
        stickKnob = MakeImage("StickKnob", circle, new Color(1, 1, 1, 0.6f), StickRadius);
        stickBase.gameObject.SetActive(false);
        stickKnob.gameObject.SetActive(false);
    }

    RectTransform MakeImage(string name, Sprite sprite, Color color, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false; img.preserveAspect = true;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero; // toạ độ canvas = px màn hình khi scale 1, chỉnh lại ở ToCanvas
        return rt;
    }

    RectTransform MakeButton(string name, Sprite sprite, Vector2 fromBottomRight)
    {
        var rt = MakeImage(name, sprite, Color.white, 240f);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-fromBottomRight.x, fromBottomRight.y);
        rt.gameObject.SetActive(false);
        return rt;
    }

    // Rời ghế (thay phím Esc): nút chữ góc trên phải
    RectTransform MakeLeaveButton()
    {
        var rt = MakeImage("Leave", null, new Color(0.15f, 0.1f, 0.08f, 0.85f), 100f);
        rt.sizeDelta = new Vector2(260, 100);
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
        rt.anchoredPosition = new Vector2(-40, -40);
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(rt, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = "RỜI GHẾ"; t.fontSize = 40; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        var trt = (RectTransform)go.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        rt.gameObject.SetActive(false);
        return rt;
    }

    static Sprite MakeCircle()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    // Lấy sprite phím E / X / C có sẵn trong Tutorial của BattleHUD, không cần thêm asset
    void TryLoadSprites(CardBattleUI ui)
    {
        if (ui.battleHUD == null) return;
        Sprite S(string path)
        {
            var t = ui.battleHUD.transform.Find(path);
            return t != null && t.TryGetComponent<Image>(out var i) ? i.sprite : null;
        }
        playBtn = MakeButton("Play", S("Tutorial/Throw Card/Image"), new Vector2(60, 260));
        accuseBtn = MakeButton("Accuse", S("Tutorial/Liar/Image"), new Vector2(330, 260));
        peekBtn = MakeButton("Peek", S("Tutorial/Select Card/Image"), new Vector2(60, 260));
        interactBtn = MakeButton("Interact", S("Tutorial/Throw Card/Image"), new Vector2(330, 530));
        leaveBtn = MakeLeaveButton();
        trustBtn = MakeButton("Trust", S("Tutorial/Truth/Image"), new Vector2(60, 530));
        spritesReady = true;
    }

    void Update()
    {
        MobileInput.Look = Vector2.zero;
        var ui = CardBattleUI.Instance;
        if (ui == null) return;
        if (!spritesReady) { TryLoadSprites(ui); if (!spritesReady) return; }

        // Bảng gợi ý phím vô nghĩa trên điện thoại
        var tut = ui.battleHUD.transform.Find("Tutorial");
        if (tut != null && tut.gameObject.activeSelf) tut.gameObject.SetActive(false);

        bool turn = ui.IsInBattle && ui.IsMyTurn;
        var seat = TableSessionManager.Instance;
        leaveBtn.gameObject.SetActive(seat != null && seat.isSeated);
        // Nút E: đang ngắm vật tương tác được (ghế, gương, jukebox...) và đang đi lại được
        interactBtn.gameObject.SetActive(CanWalk() && RaycastSource.Instance != null && RaycastSource.Instance.currentObject != null);
        peekBtn.gameObject.SetActive(ui.IsInBattle && !turn);
        if (!peekBtn.gameObject.activeSelf) { MobileInput.Peek = false; peekFinger = -1; }
        playBtn.gameObject.SetActive(turn && ui.SelectedCardCount > 0);
        accuseBtn.gameObject.SetActive(turn && ui.lastPlay != null && !string.IsNullOrEmpty(ui.lastPlay.playId));
        trustBtn.gameObject.SetActive(turn && ui.truthObject != null && ui.truthObject.activeSelf);

        foreach (var t in Touch.activeTouches)
        {
            int id = t.finger.index;
            Vector2 p = t.screenPosition;
            switch (t.phase)
            {
                case TouchPhase.Began:
                    if (TapButton(id, p, ui) || OverUI(p)) break; // chạm vào UI (menu, lobby...) thì nhường cho UI
                    if (p.x < Screen.width * 0.4f && stickFinger < 0 && CanWalk()) BeginStick(id, p);
                    else if (lookFinger < 0) { lookFinger = id; lookStart = p; lookTime = Time.time; lookMoved = 0; }
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (id == stickFinger) MoveStick(p);
                    else if (id == lookFinger)
                    {
                        MobileInput.Look += t.delta * LookScale;
                        lookMoved += t.delta.magnitude;
                    }
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (id == peekFinger) { peekFinger = -1; MobileInput.Peek = false; }
                    else if (id == stickFinger) { stickFinger = -1; MobileInput.Move = Vector2.zero; ShowStick(false); }
                    else if (id == lookFinger)
                    {
                        lookFinger = -1;
                        if (t.phase == TouchPhase.Ended && lookMoved < TapMaxMove && Time.time - lookTime < TapMaxTime) TapCard(p, ui);
                    }
                    break;
            }
        }
    }

    // Đăng nhập (bartender) hoặc đang ngồi bàn: di chuyển bị khoá nên không hiện joystick
    static bool CanWalk() => PlayerController.Instance != null && PlayerController.Instance.firstPersonMovement != null
                             && PlayerController.Instance.firstPersonMovement.enabled;

    static readonly List<RaycastResult> hits = new List<RaycastResult>();

    static bool OverUI(Vector2 p)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        es.RaycastAll(new PointerEventData(es) { position = p }, hits);
        return hits.Count > 0;
    }

    bool TapButton(int id, Vector2 p, CardBattleUI ui)
    {
        if (Hit(peekBtn, p)) { peekFinger = id; MobileInput.Peek = true; return true; }
        if (Hit(leaveBtn, p)) { var fi = FindFirstObjectByType<FirstPersonInteract>(); if (fi != null) fi.LeaveSeat(); return true; }
        if (Hit(interactBtn, p))
        {
            var obj = RaycastSource.Instance.currentObject;
            if (obj != null && obj.TryGetComponent<Item>(out var item)) item.OnInteract();
            return true;
        }
        if (Hit(playBtn, p)) { ui.ActionPlayCards(); return true; }
        if (Hit(accuseBtn, p)) { ui.ActionChallenge(); return true; }
        if (Hit(trustBtn, p)) { ui.ActionTrust(); return true; }
        return false;
    }

    static bool Hit(RectTransform rt, Vector2 p) =>
        rt.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(rt, p, null);

    // Chọn lá bài có tâm gần điểm chạm nhất (bài chồng quạt nên không dùng raycast, và bài không có collider)
    void TapCard(Vector2 p, CardBattleUI ui)
    {
        var hand = CardHand3D.Instance;
        var cam = Camera.main;
        if (hand == null || cam == null || !ui.IsMyTurn) return;

        int best = -1;
        float bestDist = Screen.height * 0.15f;
        for (int i = 0; i < hand.cardsInHand.Count; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(hand.cardsInHand[i].transform.position);
            if (sp.z <= 0) continue;
            float d = Vector2.Distance(sp, p);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        if (best < 0) return;
        hand.focusedIndex = best;
        hand.ToggleSelectCard(best);
    }

    void BeginStick(int id, Vector2 p)
    {
        stickFinger = id;
        stickOrigin = p;
        ShowStick(true);
        MoveStick(p);
    }

    void MoveStick(Vector2 p)
    {
        Vector2 d = p - stickOrigin;
        MobileInput.Move = Vector2.ClampMagnitude(d / StickRadius, 1f);
        SetStickPos(stickBase, stickOrigin);
        SetStickPos(stickKnob, stickOrigin + Vector2.ClampMagnitude(d, StickRadius));
    }

    void ShowStick(bool on)
    {
        stickBase.gameObject.SetActive(on);
        stickKnob.gameObject.SetActive(on);
    }

    // Màn hình px -> toạ độ local của canvas (bottom-left pivot) qua scale của CanvasScaler
    void SetStickPos(RectTransform rt, Vector2 screenPos)
    {
        float k = 1f / transform.localScale.x;
        rt.anchoredPosition = screenPos * k - rt.sizeDelta * 0.5f;
    }
}
