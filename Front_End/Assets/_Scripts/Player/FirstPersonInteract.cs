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
        input.PlayerInput.Escape.performed += OnESCWasPressed;
    }

    private void OnDisable()
    {
        input.PlayerInput.Interact.performed -= OnInteractWasPressed;
        input.PlayerInput.Escape.performed -= OnESCWasPressed;
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

    private void OnESCWasPressed(InputAction.CallbackContext context)
    {
        if (isLeavingSeat)
            return;

        StartCoroutine(SendLeaveSeatRequest());
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
