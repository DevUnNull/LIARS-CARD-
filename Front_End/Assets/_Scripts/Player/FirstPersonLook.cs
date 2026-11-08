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

    [Header("Ngồi Ở Bàn")]
    [Tooltip("Góc quay đầu tối đa sang trái/phải khi đang ngồi (độ). Thân người bị khóa.")]
    [SerializeField, Range(10f, 120f)] private float maxHeadYaw = 75f;
    [Tooltip("Góc ngẩng/cúi đầu tối đa khi đang ngồi (độ).")]
    [SerializeField, Range(10f, 89f)] private float maxHeadPitch = 60f;

    private bool bodyLocked;
    private float bodyYaw;
    private CharacterAnimator characterAnimator;

    /// <summary>Góc quay đầu so với thân (độ, + = phải). Chỉ khác 0 khi đang ngồi.</summary>
    public float HeadYaw => bodyLocked ? velocity.x - bodyYaw : 0f;
    /// <summary>Góc cúi đầu (độ, + = cúi xuống), giống trục X của camera.</summary>
    public float HeadPitch => -velocity.y;

    private void Awake()
    {
        input = new InputPlayer();
        playerCamera = GetComponentInChildren<Camera>();
        characterAnimator = GetComponent<CharacterAnimator>();
    }

    /// <summary>Ngồi vào bàn: khóa thân theo hướng hiện tại, chuột chỉ xoay đầu. Đứng dậy: mở khóa.</summary>
    public void LockBody(bool locked)
    {
        bodyLocked = locked;
        bodyYaw = velocity.x;
        frameVelocity = Vector2.zero;
        ApplyRotation();
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
        if (Cursor.lockState == CursorLockMode.Locked)
            RotateCamera(HandleInput());

        // Model nhân vật (bóng / gương / người khác qua mạng) quay đầu theo
        if (characterAnimator != null) characterAnimator.SetHeadLook(HeadYaw, HeadPitch);
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
        if (bodyLocked)
        {
            velocity.x = Mathf.Clamp(velocity.x, bodyYaw - maxHeadYaw, bodyYaw + maxHeadYaw);
            velocity.y = Mathf.Clamp(velocity.y, -maxHeadPitch, maxHeadPitch);
        }
        else velocity.y = Mathf.Clamp(velocity.y, -90f, 90f);

        ApplyRotation();
    }

    // Đứng: thân quay theo chuột, camera chỉ ngẩng/cúi. Ngồi: thân đứng yên, camera quay cả trái/phải.
    private void ApplyRotation()
    {
        float bodyAngle = bodyLocked ? bodyYaw : velocity.x;
        transform.localRotation = Quaternion.AngleAxis(bodyAngle, Vector3.up);
        playerCamera.transform.localRotation =
            Quaternion.AngleAxis(velocity.x - bodyAngle, Vector3.up) * Quaternion.AngleAxis(-velocity.y, Vector3.right);
    }
}
