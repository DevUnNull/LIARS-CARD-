using System;
using System.Collections;
using UnityEngine;
using LiarsCard.Network;

public class TableSessionManager : Singleton<TableSessionManager>
{
    [Header("Session State")]
    public int currentTableId { get; private set; }
    public int currentSeatIndex { get; private set; }
    public bool isSeated { get; private set; }
    public string currentMatchId { get; private set; }
    public TableResponseDto currentTableState { get; private set; }

    public event Action<TableResponseDto> OnTableStateChanged;
    public event Action<string> OnMatchStarted;
    public event Action OnLeftTable;

    private Coroutine pollCoroutine;

    public string MyPlayerId
    {
        get
        {
            var auth = AuthApiClient.Instance;
            if (auth == null) return null;
            if (!string.IsNullOrEmpty(auth.CurrentUserId)) return auth.CurrentUserId;
            return AuthApiClient.ExtractUserIdFromToken(auth.AccessToken);
        }
    }

    public bool IsHost
    {
        get
        {
            if (currentTableState == null || string.IsNullOrEmpty(currentTableState.hostPlayerId))
                return false;
            string myId = MyPlayerId;
            return !string.IsNullOrEmpty(myId) && currentTableState.hostPlayerId == myId;
        }
    }

    public bool CanStartMatch
    {
        get
        {
            if (!IsHost || currentTableState == null) return false;
            if (currentTableState.playerCount < 2) return false;

            // Tat ca thanh vien khac (ngoai host) deu phai ready
            string myId = MyPlayerId;
            foreach (var seat in currentTableState.seats)
            {
                if (!string.IsNullOrEmpty(seat.playerId) && seat.playerId != myId)
                {
                    if (!seat.isReady) return false;
                }
            }
            return true;
        }
    }

    public void OnSitDownSuccess(int tableId, int seatIndex)
    {
        isSeated = true;
        currentTableId = tableId;
        currentSeatIndex = seatIndex;
        currentMatchId = null;

        // Mo khoa con tro chuot de tuong tac UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pollCoroutine != null) StopCoroutine(pollCoroutine);
        pollCoroutine = StartCoroutine(PollTableRoutine());

        // Refresh ngay lap tuc
        RefreshTableNow();
    }

    public void OnLeaveSeatSuccess()
    {
        isSeated = false;
        currentTableId = 0;
        currentSeatIndex = -1;
        currentMatchId = null;
        currentTableState = null;

        if (pollCoroutine != null)
        {
            StopCoroutine(pollCoroutine);
            pollCoroutine = null;
        }

        // Khoa lai con tro chuot khi dung day
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnLeftTable?.Invoke();
    }

    public void RefreshTableNow()
    {
        if (!isSeated || currentTableId <= 0) return;
        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return;

        StartCoroutine(MatchApiClient.Instance.GetTable(currentTableId, auth.AccessToken,
            onSuccess: res =>
            {
                currentTableState = res;
                OnTableStateChanged?.Invoke(res);

                // Kiem tra xem ban da bat dau vao van bai chua (status == 1: Playing)
                if (res.status == 1 && !string.IsNullOrEmpty(res.currentMatchId))
                {
                    if (currentMatchId != res.currentMatchId)
                    {
                        currentMatchId = res.currentMatchId;
                        Debug.Log($"<color=green>[TableSessionManager] Tran dau da bat dau! Match ID: {currentMatchId}</color>");
                        OnMatchStarted?.Invoke(currentMatchId);
                    }
                }
            },
            onError: err => Debug.LogWarning($"[TableSessionManager] Loi lay thong tin ban: {err}")
        ));
    }

    private IEnumerator PollTableRoutine()
    {
        while (isSeated && currentTableId > 0)
        {
            yield return new WaitForSeconds(1.5f);
            RefreshTableNow();
        }
    }

    public void RequestToggleReady(bool ready, Action<bool, string> callback = null)
    {
        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken))
        {
            callback?.Invoke(false, "Chua dang nhap");
            return;
        }

        StartCoroutine(MatchApiClient.Instance.SetTableReady(ready, auth.AccessToken,
            onSuccess: res =>
            {
                RefreshTableNow();
                callback?.Invoke(true, "Cap nhat trang thai thanh cong");
            },
            onError: err => callback?.Invoke(false, err)
        ));
    }

    public void RequestStartMatch(Action<bool, string> callback = null)
    {
        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken))
        {
            callback?.Invoke(false, "Chua dang nhap");
            return;
        }

        if (!IsHost)
        {
            callback?.Invoke(false, "Chi chu ban moi co quyen bat dau tran dau");
            return;
        }

        StartCoroutine(MatchApiClient.Instance.StartTableMatch(currentTableId, auth.AccessToken,
            onSuccess: res =>
            {
                currentMatchId = res.matchId;
                Debug.Log($"<color=green>[TableSessionManager] Chu ban da tao Match: {res.matchId}</color>");
                OnMatchStarted?.Invoke(currentMatchId);
                callback?.Invoke(true, res.matchId);
            },
            onError: err => callback?.Invoke(false, err)
        ));
    }
    public void SimulatePlayer2JoinAndReady(Action<bool, string> callback)
    {
        StartCoroutine(SimulatePlayer2Routine(callback));
    }

    private IEnumerator SimulatePlayer2Routine(Action<bool, string> callback)
    {
        var auth = AuthApiClient.Instance;
        string baseUrl = auth != null ? auth.baseUrl : "http://localhost:5280";
        string botEmail = "player2_test@gmail.com";
        string botPass = "Password123@";
        string botToken = null;

        // 1. Thu dang nhap bot
        string loginJson = $"{{\"email\":\"{botEmail}\",\"password\":\"{botPass}\"}}";
        using (var req = new UnityEngine.Networking.UnityWebRequest($"{baseUrl}/api/auth/login", "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(loginJson));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.certificateHandler = new BypassCertificateHandler();
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                var res = JsonUtility.FromJson<AuthApiClient.AuthResponse>(req.downloadHandler.text);
                botToken = res?.accessToken;
            }
        }

        // 2. Neu chua co tai khoan thi dang ky
        if (string.IsNullOrEmpty(botToken))
        {
            string regJson = $"{{\"email\":\"{botEmail}\",\"displayName\":\"Player 2 (Test)\",\"password\":\"{botPass}\"}}";
            using (var req = new UnityEngine.Networking.UnityWebRequest($"{baseUrl}/api/auth/register", "POST"))
            {
                req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(regJson));
                req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.certificateHandler = new BypassCertificateHandler();
                yield return req.SendWebRequest();

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<AuthApiClient.AuthResponse>(req.downloadHandler.text);
                    botToken = res?.accessToken;
                }
            }
        }

        if (string.IsNullOrEmpty(botToken))
        {
            callback?.Invoke(false, "Không thể đăng nhập tài khoản Player 2.");
            yield break;
        }

        // 3. Bot ngoi vao ghe 1
        yield return MatchApiClient.Instance.SitDown(currentTableId, 1, botToken, null, null);

        // 4. Bot ready
        yield return MatchApiClient.Instance.SetTableReady(true, botToken, null, null);

        RefreshTableNow();
        callback?.Invoke(true, "Player 2 ảo đã ngồi vào Ghế 1 và Sẵn Sàng!");
    }
}