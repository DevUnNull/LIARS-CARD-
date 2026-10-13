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
        Vector2 mouseDelta = HandleInput();
        RotateCamera(mouseDelta);
    }

    private Vector2 HandleInput()
    {
        return input.PlayerInput.Rotate.ReadValue<Vector2>();
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
