using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Windows;

public class FirstPersonMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float gravity = 3f;

    private CharacterController characterController;
    private InputPlayer playerInput;
    private Vector2 temp;


    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        CursorManager.Instance.HideCursor();
    }

    private void OnEnable()
    {
        playerInput = new InputPlayer();
        playerInput.Enable();
    }

    private void OnDisable()
    {
        playerInput.Disable();
    }

    // CharacterController không có interpolation: Move trong FixedUpdate (50Hz) làm camera khựng khi FPS > 50
    private void Update()
    {
        temp = HandleInput();
        MovePlayer(temp);
    }

    private Vector2 HandleInput()
    {
        return playerInput.PlayerInput.Move.ReadValue<Vector2>();
    }

    private void MovePlayer(Vector2 moveVec)
    {
        Vector3 move = transform.right * moveVec.x + transform.forward * moveVec.y + Vector3.down * gravity;
        characterController.Move(moveSpeed * Time.deltaTime * move);
    }
}
