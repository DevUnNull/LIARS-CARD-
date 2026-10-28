using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

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
        var auth = AuthApiClient.Instance;

        if (auth == null || string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            Debug.LogWarning("Not login yet.");
            // Force release locally even if server returns error
        }

        isLeavingSeat = true;

        try
        {
            string url = $"{AuthApiClient.Instance.baseUrl.TrimEnd('/')}/api/tables/leaveSeat";

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 15;

                request.SetRequestHeader(
                    "Authorization",
                    $"Bearer {auth.AccessToken}");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"Cant not leave: HTTP {request.responseCode}\n" +
                        $"{request.error}\n{request.downloadHandler.text}");

                    // Force release locally even if server returns error
                }

                Debug.Log("Leave sit.");
                PlayerController.Instance.firstPersonMovement.enabled = true;

                if (TableSessionManager.Instance != null)
                {
                    TableSessionManager.Instance.OnLeaveSeatSuccess();
                }
            }
        }
        finally
        {
            isLeavingSeat = false;
        }
    }
}
