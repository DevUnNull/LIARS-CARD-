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
    [Tooltip("Mô hình bàn (mặt bàn) của khu bàn này. Để trống thì dùng object tên \"Table\".")]
    [SerializeField] private Transform tableModel;
    [Tooltip("Điểm súng nằm chờ trên bàn này.")]
    [SerializeField] private Transform gunSpot;

    // Mặt ghế cao 0.45 trong model gốc (chưa nhân scale của prefab)
    private const float ModelSeatHeight = 0.45f;
    [SerializeField] private float eyeAboveSeat = 0.95f;

    public int SitIndex => sitIndex;
    public int TableId => tableID;

    private bool isRequesting;

    // Đặt người chơi lên ghế: mắt cao hơn mặt ghế để nhìn qua mặt bàn thấy mặt người đối diện, rồi quay về tâm bàn.
    private void SeatPlayer(PlayerController player)
    {
        var controller = player.GetComponent<CharacterController>();
        var look = player.firstPersonLook;

        controller.enabled = false;

        float seatTopY = transform.position.y + ModelSeatHeight * transform.lossyScale.y;
        player.transform.position = new Vector3(transform.position.x, player.transform.position.y, transform.position.z);
        player.transform.position += Vector3.up * (seatTopY + eyeAboveSeat - look.playerCamera.transform.position.y);

        controller.enabled = true;
        player.firstPersonMovement.enabled = false;

        if (tableModel != null) LiarsCard.Cards3D.TableCenterPile3D.ActiveTable = tableModel;
        look.FocusOnTable(LiarsCard.Cards3D.TableCenterPile3D.GetTableTopCenter());

        // Bộ bài và súng chuyển sang bàn vừa ngồi
        if (LiarsCard.Cards3D.TableDeck3D.Instance != null) LiarsCard.Cards3D.TableDeck3D.Instance.BuildDeckOnTable();
        if (LiarsCard.Cards3D.TableCenterPile3D.Instance != null) LiarsCard.Cards3D.TableCenterPile3D.Instance.AlignWithTable();
        if (gunSpot != null && LiarsCard.Cards3D.RevolverGun.Instance != null)
            LiarsCard.Cards3D.RevolverGun.Instance.SetRestPose(gunSpot.position, gunSpot.rotation);
    }

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

                SeatPlayer(PlayerController.Instance);

                if (TableSessionManager.Instance != null)
                {
                    TableSessionManager.Instance.OnSitDownSuccess(tableID, sitIndex);
                }
            }
        }
        finally
        {
            isRequesting = false;
        }
    }
}