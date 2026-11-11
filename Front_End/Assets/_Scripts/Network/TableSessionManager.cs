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
    public bool isInCardBattle { get; set; }
    public string currentMatchId { get; private set; }
    public TableResponseDto currentTableState { get; private set; }

    public event Action<TableResponseDto> OnTableStateChanged;
    public event Action<string> OnMatchStarted;
    public event Action OnLeftTable;

    private Coroutine pollCoroutine;

    public string MyPlayerId => AuthApiClient.Instance != null ? AuthApiClient.Instance.CurrentUserId : null;

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
        isInCardBattle = false;
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

    public void OnMatchEnded()
    {
        isInCardBattle = false;
        currentMatchId = null;
        RefreshTableNow();
    }

    public void OnLeaveSeatSuccess()
    {
        isSeated = false;
        isInCardBattle = false;
        if (PlayerController.Instance != null) PlayerController.Instance.firstPersonLook.LockBody(false);
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
                    isInCardBattle = true;
                    if (currentMatchId != res.currentMatchId)
                    {
                        currentMatchId = res.currentMatchId;
                        Debug.Log($"<color=green>[TableSessionManager] Tran dau da bat dau! Match ID: {currentMatchId}</color>");
                        OnMatchStarted?.Invoke(currentMatchId);
                    }
                }
                else if (res.status == 0)
                {
                    // Chi xoa currentMatchId khi CardBattleUI khong con trong tran
                    if (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsInBattle)
                    {
                        if (!string.IsNullOrEmpty(currentMatchId))
                        {
                            currentMatchId = null;
                            isInCardBattle = false;
                            Debug.Log("<color=yellow>[TableSessionManager] Tran dau da ket thuc. Quay ve phong cho ban!</color>");
                        }
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
}
