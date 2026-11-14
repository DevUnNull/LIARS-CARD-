using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonInteract : MonoBehaviour
{
    private InputPlayer input;
    private bool isLeavingSeat;

    private void Awake()
    {
        input = new InputPlayer();
    }

    private void OnEnable()
    {
        input.Enable();
        input.PlayerInput.Interact.performed += OnInteractWasPressed;
    }

    private void OnDisable()
    {
        input.PlayerInput.Interact.performed -= OnInteractWasPressed;
        input.Disable();
    }

    private void OnInteractWasPressed(InputAction.CallbackContext context)
    {
        if (RaycastSource.Instance.currentObject == null)
        {
            return;
        }

        Item itemObj = RaycastSource.Instance.currentObject.GetComponent<Item>();
        itemObj.OnInteract();
    }

    public void LeaveSeat()
    {
        if (isLeavingSeat) return;
        StartCoroutine(SendLeaveSeatRequest());
    }

    // Rời ghế bằng Q (Esc bị trình duyệt chiếm: thoát fullscreen / nhả khoá chuột, và dành cho menu Cài đặt)
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.qKey.wasPressedThisFrame && TableSessionManager.Instance != null && TableSessionManager.Instance.isSeated)
            LeaveSeat();
    }

    private IEnumerator SendLeaveSeatRequest()
    {
        isLeavingSeat = true;
        try
        {
            // Lỗi từ server (vd. chưa ngồi) vẫn đứng dậy tại chỗ cho khỏi kẹt
            yield return LiarsCard.Network.MatchApiClient.Instance.LeaveSeat(null, err => Debug.LogWarning($"Rời ghế lỗi: {err}"));

            Debug.Log("Leave sit.");
            PlayerController.Instance.firstPersonMovement.enabled = true;
            PlayerController.Instance.firstPersonLook.RestoreFov();
            if (TableSessionManager.Instance != null) TableSessionManager.Instance.OnLeaveSeatSuccess();
        }
        finally
        {
            isLeavingSeat = false;
        }
    }
}
