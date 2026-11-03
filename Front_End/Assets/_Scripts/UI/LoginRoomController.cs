using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public enum LoginViewState { RoomOverview, Transitioning, LoginActive }

/// <summary>
/// Màn đăng nhập trong quán bar 3D: camera bay từ cảnh rộng vào màn hình máy tính khi bấm vào PC,
/// sau đó form đăng nhập (Canvas) mờ dần hiện ra. Esc để lùi lại.
/// </summary>
public class LoginRoomController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Camera sẽ được di chuyển. Để trống thì dùng Camera.main.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("Vị trí/góc nhìn toàn cảnh quán.")]
    [SerializeField] private Transform overviewAnchor;
    [Tooltip("Vị trí/góc nhìn cận màn hình máy tính.")]
    [SerializeField] private Transform loginAnchor;
    [Tooltip("Thời gian bay giữa hai góc (giây).")]
    [SerializeField] private float transitionDuration = 1.4f;
    [Tooltip("Độ rung nhẹ của camera ở toàn cảnh (mét).")]
    [SerializeField] private float swayAmount = 0.03f;

    [Header("Tương tác")]
    [Tooltip("Collider của máy tính để bấm vào.")]
    [SerializeField] private Collider pcCollider;
    [Tooltip("Renderer của màn hình PC (sáng hơn khi rê chuột vào).")]
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Color screenIdle = new Color(0.05f, 0.35f, 0.12f);
    [SerializeField] private Color screenHover = new Color(0.15f, 1.1f, 0.35f);

    [Header("UI")]
    [Tooltip("CanvasGroup của form đăng nhập (mờ dần khi camera đến nơi).")]
    [SerializeField] private CanvasGroup formGroup;
    [Tooltip("Dòng gợi ý ở dưới màn hình.")]
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private float fadeDuration = 0.35f;

    public LoginViewState State { get; private set; } = LoginViewState.RoomOverview;

    private Camera cam;
    private Material screenMat;
    private bool hovering;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform != null)
            cam = cameraTransform.GetComponent<Camera>();

        if (screenRenderer != null)
        {
            screenMat = screenRenderer.material;
            screenMat.EnableKeyword("_EMISSION");
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetForm(0f);
        SnapTo(overviewAnchor);
        ApplyHint();
        SetHover(false);
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
                GoToOverview();
        }
    }

    /// <summary>Bay vào màn hình PC (dùng được từ nút UI hoặc script khác).</summary>
    public void GoToLogin()
    {
        if (State == LoginViewState.RoomOverview)
            StartCoroutine(Transition(loginAnchor, LoginViewState.LoginActive));
    }

    /// <summary>Lùi camera ra toàn cảnh quán.</summary>
    public void GoToOverview()
    {
        if (State == LoginViewState.LoginActive)
            StartCoroutine(Transition(overviewAnchor, LoginViewState.RoomOverview));
    }

    private void UpdateHoverAndClick()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null || pcCollider == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        bool hit = pcCollider.Raycast(ray, out _, 50f);
        SetHover(hit);

        if (hit && mouse.leftButton.wasPressedThisFrame)
            GoToLogin();
    }

    private IEnumerator Transition(Transform target, LoginViewState finalState)
    {
        if (target == null || cameraTransform == null) yield break;

        State = LoginViewState.Transitioning;
        SetHover(false);
        ApplyHint();

        // Form mờ đi trước khi camera rời PC
        if (finalState == LoginViewState.RoomOverview)
            yield return Fade(formGroup != null ? formGroup.alpha : 0f, 0f);

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

        if (finalState == LoginViewState.LoginActive)
            yield return Fade(0f, 1f);

        State = finalState;
        ApplyHint();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (formGroup == null) yield break;

        // Chỉ nhận chuột khi đã hiện rõ
        formGroup.interactable = false;
        formGroup.blocksRaycasts = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, fadeDuration))
        {
            formGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        SetForm(to);
    }

    private void SetForm(float alpha)
    {
        if (formGroup == null) return;
        formGroup.alpha = alpha;
        bool on = alpha >= 0.99f;
        formGroup.interactable = on;
        formGroup.blocksRaycasts = on;
    }

    private void SnapTo(Transform anchor)
    {
        if (anchor != null && cameraTransform != null)
            cameraTransform.SetPositionAndRotation(anchor.position, anchor.rotation);
    }

    private void Sway()
    {
        if (overviewAnchor == null) return;
        float t = Time.time * 0.4f;
        Vector3 offset = new Vector3(Mathf.Sin(t), Mathf.Sin(t * 1.3f) * 0.5f, Mathf.Cos(t * 0.7f)) * swayAmount;
        cameraTransform.position = overviewAnchor.position + offset;
    }

    private void SetHover(bool on)
    {
        hovering = on;
        if (screenMat != null)
            screenMat.SetColor("_EmissionColor", on ? screenHover : screenIdle);
        ApplyHint();
    }

    private void ApplyHint()
    {
        if (hintText == null) return;
        switch (State)
        {
            case LoginViewState.RoomOverview:
                hintText.text = hovering ? "Nhấp để ngồi vào máy tính" : "Nhấp vào máy tính để đăng nhập";
                break;
            case LoginViewState.LoginActive:
                hintText.text = "[Esc] Quay lại quán";
                break;
            default:
                hintText.text = "";
                break;
        }
    }
}
