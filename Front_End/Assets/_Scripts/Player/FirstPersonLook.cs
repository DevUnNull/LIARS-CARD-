using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public class FirstPersonLook : MonoBehaviour
{
    public float sensitivity = 2;
    public float smoothing = 1.5f;

    public Camera playerCamera;
    private Vector2 velocity;
    private Vector2 frameVelocity;

    private InputPlayer input;

    private void Awake()
    {
        input = new InputPlayer();
        playerCamera = GetComponentInChildren<Camera>();
    }

    private void OnEnable()
    {
        input.Enable();
    }

    private void OnDisable()
    {
        input.Disable();
    }

    private void Start()
    {
        CursorManager.Instance.HideCursor();
    }

    void Update()
    {
        // Khi chuột đang mở khóa (đang tương tác UI Canvas bàn chơi / lobby) -> không xoay camera theo chuột
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 mouseDelta = HandleInput();
        RotateCamera(mouseDelta);
    }

    private Vector2 HandleInput()
    {
        return input.PlayerInput.Rotate.ReadValue<Vector2>();
    }

    private float defaultFov;

    /// <summary>Quay mặt về tâm bàn, cúi nhẹ và mở rộng FOV để thấy hết mọi người quanh bàn.</summary>
    public void FocusOnTable(Vector3 tableCenter, float pitchDown = 8f, float fov = 75f)
    {
        Vector3 d = tableCenter - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;

        velocity = new Vector2(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -pitchDown);
        frameVelocity = Vector2.zero;
        transform.localRotation = Quaternion.AngleAxis(velocity.x, Vector3.up);
        playerCamera.transform.localRotation = Quaternion.AngleAxis(-velocity.y, Vector3.right);

        if (defaultFov <= 0f) defaultFov = playerCamera.fieldOfView;
        playerCamera.fieldOfView = fov;
    }

    public void RestoreFov()
    {
        if (defaultFov <= 0f) return;
        playerCamera.fieldOfView = defaultFov;
        defaultFov = 0f;
    }

    private void RotateCamera(Vector2 mouseDelta)
    {
        Vector2 rawFrameVelocity = mouseDelta * sensitivity;

        frameVelocity = Vector2.Lerp(
            frameVelocity,
            rawFrameVelocity,
            1f / Mathf.Max(1f, smoothing));

        velocity += frameVelocity;
        velocity.y = Mathf.Clamp(velocity.y, -90f, 90f);

        playerCamera.transform.localRotation =
            Quaternion.AngleAxis(-velocity.y, Vector3.right);

        transform.localRotation =
            Quaternion.AngleAxis(velocity.x, Vector3.up);
    }
}
