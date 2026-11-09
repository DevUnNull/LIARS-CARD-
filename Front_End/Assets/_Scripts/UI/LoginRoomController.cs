using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public enum LoginViewState { RoomOverview, Transitioning, LoginActive, EnteringBar, InGame }

/// <summary>
/// Đăng nhập ngay trong quán bar (MainScene) bằng hội thoại với bartender: bấm vào bartender, camera tiến lại gần,
/// bartender hỏi "cậu là ai?" rồi form đăng nhập hiện ra ngay trong cảnh. Đăng nhập xong bartender chào,
/// camera lùi dần ra chỗ spawn rồi trao quyền điều khiển cho người chơi.
/// </summary>
[DefaultExecutionOrder(-100)]
public class LoginRoomController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera sẽ được di chuyển. Để trống thì dùng Camera.main.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Góc nhìn toàn cảnh quầy bar lúc mới vào.")]
    [SerializeField] private Transform overviewAnchor;
    [Tooltip("Góc nhìn khi nói chuyện với bartender.")]
    [FormerlySerializedAs("loginAnchor")]
    [SerializeField] private Transform talkAnchor;
    [Tooltip("Thời gian camera tiến lại gần / lùi ra (giây).")]
    [SerializeField] private float transitionDuration = 1.6f;
    [Tooltip("Độ rung nhẹ của camera ở toàn cảnh (mét).")]
    [SerializeField] private float swayAmount = 0.03f;
    [SerializeField] private float introFov = 55f;

    [Header("Bartender")]
    [Tooltip("Collider của bartender để bấm vào.")]
    [SerializeField] private Collider bartenderCollider;
    [SerializeField] private string bartenderName = "Bartender";
    [Tooltip("Các câu bartender nói trước khi hiện form. Bấm chuột / Space để qua câu.")]
    [TextArea(1, 3)]
    [SerializeField] private string[] greetLines =
    {
        "Này, người lạ! Quán này không tiếp kẻ vô danh đâu.",
        "Nói đi... cậu là ai? Cho tôi tên đăng nhập và mật khẩu.",
    };
    [TextArea(1, 3)]
    [SerializeField] private string welcomeLine = "Hà, ra là khách quen! Vào đi, bàn trống đang chờ cậu đấy.";
    [TextArea(1, 3)]
    [SerializeField] private string cancelLine = "Hừ, không muốn nói thì thôi. Cần thì quay lại.";
    [SerializeField] private float charsPerSecond = 45f;

    [Header("Vào quán sau khi đăng nhập")]
    [Tooltip("Người chơi (bị khoá điều khiển trong lúc đăng nhập). Để trống thì tự tìm PlayerController.")]
    [SerializeField] private Transform player;
    [Tooltip("Thời gian camera lùi ra chỗ spawn (giây). Bấm phím bất kỳ để bỏ qua.")]
    [SerializeField] private float enterDuration = 4f;
    [Tooltip("Độ nhô cao của đường bay giữa chừng (mét).")]
    [SerializeField] private float enterArcHeight = 1.0f;
    [Tooltip("Các object ẩn đi sau khi vào quán (canvas hội thoại...).")]
    [SerializeField] private GameObject[] hideWhenInGame;

    [Header("UI")]
    [Tooltip("CanvasGroup của form đăng nhập.")]
    [SerializeField] private CanvasGroup formGroup;
    [Tooltip("Dòng gợi ý ở dưới màn hình.")]
    [SerializeField] private TMP_Text hintText;
    [Tooltip("CanvasGroup của khung hội thoại.")]
    [SerializeField] private CanvasGroup dialogueGroup;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private float fadeDuration = 0.35f;

    public LoginViewState State { get; private set; } = LoginViewState.RoomOverview;

    private Camera cam;
    private bool hovering;
    private Coroutine talkRoutine;

    private Vector3 homeLocalPos;
    private Quaternion homeLocalRot;
    private float homeFov;
    private Behaviour[] lockedScripts;
    private Renderer playerRenderer;
    private GameObject firstPersonCanvas;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform != null)
            cam = cameraTransform.GetComponent<Camera>();
    }

    private void Start()
    {
        LockPlayer();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Các canvas có thể đang tắt trong scene: bật lên, ẩn bằng alpha
        if (formGroup != null) formGroup.gameObject.SetActive(true);
        if (hintText != null)
        {
            var hc = hintText.GetComponentInParent<Canvas>(true);
            hc.gameObject.SetActive(true);
            // Canvas gợi ý ban đầu không có raycaster nên ô nhập/nút trong khung hội thoại không nhận chuột
            if (hc.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) hc.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            hc.sortingOrder = 20;
        }
        SetGroup(formGroup, 0f);
        SetGroup(dialogueGroup, 0f);
        SnapTo(overviewAnchor);
        ApplyHint();
    }

    private void Update()
    {
        if (cameraTransform == null) return;

        if (State == LoginViewState.RoomOverview)
        {
            Sway();
            UpdateHoverAndClick();
        }
        else if (State == LoginViewState.LoginActive)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                CancelTalk();
        }
    }

    // Khoá điều khiển, ẩn thân người chơi và tâm ngắm trong lúc đăng nhập
    private void LockPlayer()
    {
        if (player == null && PlayerController.Instance != null) player = PlayerController.Instance.transform;
        if (cameraTransform != null)
        {
            homeLocalPos = cameraTransform.localPosition;
            homeLocalRot = cameraTransform.localRotation;
            if (cam != null) homeFov = cam.fieldOfView;
        }
        if (player != null)
        {
            lockedScripts = new Behaviour[]
            {
                player.GetComponent<FirstPersonMovement>(),
                player.GetComponent<FirstPersonLook>(),
                player.GetComponent<FirstPersonInteract>(),
                player.GetComponent<RaycastSource>(),
            };
            foreach (var b in lockedScripts) if (b != null) b.enabled = false;
            playerRenderer = player.GetComponent<Renderer>();
            if (playerRenderer != null) playerRenderer.enabled = false;
        }
        var fpc = FindFirstObjectByType<FirstPersonCanvas>(FindObjectsInactive.Include);
        if (fpc != null) { firstPersonCanvas = fpc.gameObject; firstPersonCanvas.SetActive(false); }
    }

    private void UpdateHoverAndClick()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null || bartenderCollider == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        bool hit = bartenderCollider.Raycast(ray, out _, 50f);
        if (hit != hovering) { hovering = hit; ApplyHint(); }

        if (hit && mouse.leftButton.wasPressedThisFrame)
            StartTalk();
    }

    /// <summary>Bắt đầu nói chuyện với bartender (bấm vào bartender).</summary>
    public void StartTalk()
    {
        if (State != LoginViewState.RoomOverview) return;
        State = LoginViewState.Transitioning;
        hovering = false;
        ApplyHint();
        talkRoutine = StartCoroutine(TalkRoutine());
    }

    private IEnumerator TalkRoutine()
    {
        if (nameText != null) nameText.text = bartenderName;
        StartCoroutine(Fade(dialogueGroup, 0f, 1f));
        yield return MoveCamera(talkAnchor);

        for (int i = 0; i < greetLines.Length; i++)
        {
            yield return Say(greetLines[i]);
            // Câu cuối không cần bấm: form hiện ngay để nhập
            if (i < greetLines.Length - 1) yield return WaitClick();
        }

        yield return Fade(formGroup, 0f, 1f);
        State = LoginViewState.LoginActive;
        ApplyHint();
    }

    private void CancelTalk()
    {
        if (talkRoutine != null) StopCoroutine(talkRoutine);
        talkRoutine = StartCoroutine(CancelRoutine());
    }

    private IEnumerator CancelRoutine()
    {
        State = LoginViewState.Transitioning;
        ApplyHint();
        StartCoroutine(Fade(formGroup, formGroup != null ? formGroup.alpha : 0f, 0f));
        yield return Say(cancelLine);
        yield return new WaitForSeconds(0.8f);
        StartCoroutine(Fade(dialogueGroup, 1f, 0f));
        yield return MoveCamera(overviewAnchor);
        State = LoginViewState.RoomOverview;
        ApplyHint();
    }

    /// <summary>Đăng nhập thành công: bartender chào rồi camera lùi ra chỗ spawn.</summary>
    public void Welcome()
    {
        if (State == LoginViewState.EnteringBar || State == LoginViewState.InGame) return;
        if (talkRoutine != null) StopCoroutine(talkRoutine);
        talkRoutine = StartCoroutine(WelcomeRoutine());
    }

    private IEnumerator WelcomeRoutine()
    {
        State = LoginViewState.EnteringBar;
        ApplyHint();
        yield return Fade(formGroup, formGroup != null ? formGroup.alpha : 0f, 0f);
        yield return Say(welcomeLine);
        yield return new WaitForSeconds(1.2f);
        yield return Fade(dialogueGroup, 1f, 0f);
        yield return EnterBarRoutine();
    }

    private IEnumerator EnterBarRoutine()
    {
        Transform parent = cameraTransform.parent;
        Vector3 p0 = cameraTransform.position;
        Quaternion r0 = cameraTransform.rotation;
        float f0 = cam != null ? cam.fieldOfView : homeFov;
        Vector3 p1 = parent != null ? parent.TransformPoint(homeLocalPos) : homeLocalPos;
        Quaternion r1 = parent != null ? parent.rotation * homeLocalRot : homeLocalRot;
        // Điểm điều khiển nhô lên: đường bay cong qua đầu ghế/bàn thay vì cắt xuyên qua
        Vector3 c = (p0 + p1) * 0.5f + Vector3.up * enterArcHeight;

        float dur = Mathf.Max(0.5f, enterDuration);
        for (float t = 0f; t < 1f; t += Time.deltaTime / dur)
        {
            if (t > 0.1f && SkipPressed()) break;
            float k = Mathf.SmoothStep(0f, 1f, t);
            Vector3 a = Vector3.Lerp(p0, c, k), b = Vector3.Lerp(c, p1, k);
            cameraTransform.SetPositionAndRotation(Vector3.Lerp(a, b, k), Quaternion.Slerp(r0, r1, k));
            if (cam != null) cam.fieldOfView = Mathf.Lerp(f0, homeFov, k);
            yield return null;
        }

        // Trả camera về đúng chỗ của người chơi rồi mở khoá điều khiển
        cameraTransform.localPosition = homeLocalPos;
        cameraTransform.localRotation = homeLocalRot;
        if (cam != null) cam.fieldOfView = homeFov;
        if (playerRenderer != null) playerRenderer.enabled = true;
        if (firstPersonCanvas != null) firstPersonCanvas.SetActive(true);
        if (lockedScripts != null) foreach (var b in lockedScripts) if (b != null) b.enabled = true;
        foreach (var go in hideWhenInGame) if (go != null) go.SetActive(false);
        if (formGroup != null) formGroup.gameObject.SetActive(false);
        if (CursorManager.Instance != null) CursorManager.Instance.HideCursor();
        State = LoginViewState.InGame;
    }

    private static bool SkipPressed()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        return (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }

    private IEnumerator WaitClick()
    {
        yield return null; // bỏ qua frame bấm chuột trước đó
        while (!SkipPressed()) yield return null;
    }

    // Gõ chữ dần; bấm chuột/phím để hiện hết ngay
    private IEnumerator Say(string line)
    {
        if (dialogueText == null) yield break;
        dialogueText.text = line;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        int total = dialogueText.textInfo.characterCount;
        float shown = 0f;
        int blipped = 0;
        yield return null;
        while (shown < total)
        {
            if (SkipPressed()) break;
            shown += Time.deltaTime * charsPerSecond;
            int visible = Mathf.Min((int)shown, total);
            dialogueText.maxVisibleCharacters = visible;

            // Tiếng "tạch" khi có chữ mới hiện (bỏ qua dấu cách); minInterval giữ nhịp ~20 tiếng/giây dù chữ chạy nhanh
            if (visible > blipped)
            {
                blipped = visible;
                if (!char.IsWhiteSpace(dialogueText.textInfo.characterInfo[visible - 1].character))
                    GameAudio.Play2D("DialogueBlip", 0.5f, 0.05f);
            }
            yield return null;
        }
        dialogueText.maxVisibleCharacters = int.MaxValue;
    }

    private IEnumerator MoveCamera(Transform target)
    {
        if (target == null || cameraTransform == null) yield break;
        Vector3 p0 = cameraTransform.position;
        Quaternion r0 = cameraTransform.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, transitionDuration))
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            cameraTransform.SetPositionAndRotation(
                Vector3.Lerp(p0, target.position, k),
                Quaternion.Slerp(r0, target.rotation, k));
            yield return null;
        }
        cameraTransform.SetPositionAndRotation(target.position, target.rotation);
    }

    private IEnumerator Fade(CanvasGroup g, float from, float to)
    {
        if (g == null) yield break;

        // Chỉ nhận chuột khi đã hiện rõ
        g.interactable = false;
        g.blocksRaycasts = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, fadeDuration))
        {
            g.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        SetGroup(g, to);
    }

    private static void SetGroup(CanvasGroup g, float alpha)
    {
        if (g == null) return;
        g.alpha = alpha;
        bool on = alpha >= 0.99f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    private void SnapTo(Transform anchor)
    {
        if (anchor != null && cameraTransform != null)
            cameraTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
        if (cam != null) cam.fieldOfView = introFov;
    }

    private void Sway()
    {
        if (overviewAnchor == null) return;
        float t = Time.time * 0.4f;
        Vector3 offset = new Vector3(Mathf.Sin(t), Mathf.Sin(t * 1.3f) * 0.5f, Mathf.Cos(t * 0.7f)) * swayAmount;
        cameraTransform.position = overviewAnchor.position + offset;
    }

    // Phím bấm dạng "keycap": nền tối, chữ vàng, theo sau là mô tả
    private static string Key(string k) =>
        $"<mark=#1a120ccc padding=\"22,22,8,8\"><b><color=#E8C766>{k}</color></b></mark>   <color=#F5E8CC>";

    private void ApplyHint()
    {
        if (hintText == null) return;
        switch (State)
        {
            case LoginViewState.RoomOverview:
                hintText.text = hovering ? Key("Click") + "Nói chuyện với bartender" : "Nhấp vào bartender để bắt chuyện";
                break;
            case LoginViewState.LoginActive:
                hintText.text = ""; // gợi ý [Esc] Quay lại đã nằm trong khung hội thoại
                break;
            default:
                hintText.text = "";
                break;
        }
    }
}
