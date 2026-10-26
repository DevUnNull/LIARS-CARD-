using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using static AuthApiClient;

public class ChairItem : Item
{
    [SerializeField] private int sitIndex;
    [SerializeField] private int tableID;

    private bool isRequesting;

    public override void OnInteract()
    {
        base.OnInteract();

        if (isRequesting)
            return;

        StartCoroutine(SendSitDownRequest());
    }

    private IEnumerator SendSitDownRequest()
    {
        var auth = AuthApiClient.Instance;

        if (auth == null || string.IsNullOrWhiteSpace(auth.AccessToken))
        {
            Debug.LogWarning("Not login yet");
            yield break;
        }

        isRequesting = true;

        try
        {
            string url = $"{AuthApiClient.Instance.baseUrl.TrimEnd('/')}/api/tables/{tableID}/sit";

            string json = JsonUtility.ToJson(new SitDownData
            {
                seatIndex = sitIndex
            });

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(json));

                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 15;

                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader(
                    "Authorization", $"Bearer {auth.AccessToken}");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(
                        $"Cant sit: HTTP {request.responseCode}\n" +
                        $"{request.error}\n{request.downloadHandler.text}");

                    yield break;
                }

                Debug.Log($"Table : {tableID}, Chair : {sitIndex}.");

                var player = PlayerController.Instance;
                var controller = player.GetComponent<CharacterController>();

                controller.enabled = false;

                player.transform.position = transform.position + Vector3.up * 1.5f;

                controller.enabled = true;

                player.firstPersonMovement.enabled = false;
            }
        }
        finally
        {
            isRequesting = false;
        }
    }
}