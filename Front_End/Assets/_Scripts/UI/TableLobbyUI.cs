using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LiarsCard.Network;

public class TableLobbyUI : MonoBehaviour
{
    public static TableLobbyUI Instance { get; private set; }

    [Header("Settings")]
    public bool enableOnGUI = false; // Tắt hoàn toàn OnGUI cũ để dùng Canvas GameObject trên Hierarchy

    [Header("uGUI Hierarchy References")]
    public GameObject lobbyPanel;
    public TMP_Text titleText;
    public TMP_Text subTitleText;
    public TMP_Text statusText;

    [System.Serializable]
    public class SeatRowUI
    {
        public GameObject root;
        public TMP_Text seatTitleText;
        public TMP_Text playerIdText;
        public TMP_Text readyBadgeText;
    }
    public SeatRowUI[] seatRows = new SeatRowUI[4];

    [Header("Buttons")]
    public Button readyButton;
    public TMP_Text readyButtonText;
    public Image readyButtonBg;

    public Button startMatchButton;
    public TMP_Text startButtonText;
    public Image startButtonBg;

    public Button quickTestButton;
    public Button leaveButton;

    private bool myReadyState = false;
    private string statusMessage = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Gắn sự kiện click cho các nút bấm uGUI
        if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
        if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);
        if (quickTestButton != null) quickTestButton.onClick.AddListener(OnQuickTestClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);

        if (TableSessionManager.Instance != null)
        {
            TableSessionManager.Instance.OnTableStateChanged += OnTableStateChanged;
            TableSessionManager.Instance.OnMatchStarted += OnMatchStarted;
            TableSessionManager.Instance.OnLeftTable += OnLeftTable;
        }

        UpdateLobbyUI();
    }

    private void OnDestroy()
    {
        if (TableSessionManager.Instance != null)
        {
            TableSessionManager.Instance.OnTableStateChanged -= OnTableStateChanged;
            TableSessionManager.Instance.OnMatchStarted -= OnMatchStarted;
            TableSessionManager.Instance.OnLeftTable -= OnLeftTable;
        }
    }

    private void Update()
    {
        // Cập nhật giao diện thường xuyên để phản ánh đúng trạng thái bàn và nút bấm
        UpdateLobbyUI();
    }

    private void OnTableStateChanged(TableResponseDto table)
    {
        if (table == null) return;

        // Cập nhật trạng thái ready của bản thân
        string myId = TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : null;
        if (!string.IsNullOrEmpty(myId) && table.seats != null)
        {
            foreach (var seat in table.seats)
            {
                if (seat.playerId == myId)
                {
                    myReadyState = seat.isReady;
                    break;
                }
            }
        }

        UpdateLobbyUI();
    }

    private void OnMatchStarted(string matchId)
    {
        statusMessage = $"Ván đấu đang bắt đầu! Match ID: {matchId}";
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
    }

    private void OnLeftTable()
    {
        myReadyState = false;
        statusMessage = "";
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
    }

    public void UpdateLobbyUI()
    {
        var session = TableSessionManager.Instance;
        bool shouldShow = session != null && session.isSeated &&
                          !session.isInCardBattle &&
                          string.IsNullOrEmpty(session.currentMatchId) &&
                          (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsInBattle);

        // Đảm bảo Canvas cha luôn bật nếu cần hiển thị
        if (lobbyPanel != null)
        {
            var parentCanvas = lobbyPanel.GetComponentInParent<Canvas>(true);
            if (parentCanvas != null && !parentCanvas.gameObject.activeSelf)
            {
                parentCanvas.gameObject.SetActive(true);
            }

            if (lobbyPanel.activeSelf != shouldShow)
            {
                lobbyPanel.SetActive(shouldShow);
                Debug.Log($"[TableLobbyUI] Cập nhật hiển thị TableLobbyPanel: {shouldShow}");
            }
        }

        if (!shouldShow || session == null) return;

        // Mở khóa chuột khi đang ở phòng chờ để tương tác các nút bấm
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        var table = session.currentTableState;

        // 1. Header
        if (titleText != null)
        {
            titleText.text = $"♠ LIARS CARD - PHÒNG CHỜ BÀN SỐ {session.currentTableId} ♠";
        }
        if (subTitleText != null)
        {
            subTitleText.text = $"Ghế của bạn: <b>Ghế {session.currentSeatIndex}</b> | Trạng thái: <color=#FFD700>ĐANG CHỜ BẮT ĐẦU</color>";
        }

        // 2. Danh sách ghế
        if (seatRows != null && table != null && table.seats != null)
        {
            string myId = session.MyPlayerId;

            for (int i = 0; i < seatRows.Length; i++)
            {
                var row = seatRows[i];
                if (row == null) continue;

                if (i < table.seats.Count)
                {
                    var seat = table.seats[i];
                    bool isMe = !string.IsNullOrEmpty(seat.playerId) && seat.playerId == myId;
                    bool isHost = !string.IsNullOrEmpty(seat.playerId) && seat.playerId == table.hostPlayerId;

                    if (row.seatTitleText != null)
                    {
                        string title = $"<b>Ghế {seat.seatIndex}:</b> ";
                        if (isHost) title += "<color=#FFD700>[CHỦ BÀN]</color> ";
                        if (isMe) title += "<color=#00FFFF>(Bạn)</color> ";
                        row.seatTitleText.text = title;
                    }

                    if (string.IsNullOrEmpty(seat.playerId))
                    {
                        if (row.playerIdText != null) row.playerIdText.text = "<color=#888888><i>(Còn trống)</i></color>";
                        if (row.readyBadgeText != null) row.readyBadgeText.text = "";
                    }
                    else
                    {
                        string idShort = seat.playerId.Length > 12 ? seat.playerId.Substring(0, 12) + "..." : seat.playerId;
                        if (row.playerIdText != null) row.playerIdText.text = $"ID: {idShort}";

                        if (row.readyBadgeText != null)
                        {
                            row.readyBadgeText.text = seat.isReady ? "<color=#2ECC71><b>✔ ĐÃ SẴN SÀNG</b></color>" : "<color=#E67E22><b>⏳ Chưa Ready</b></color>";
                        }
                    }
                }
            }
        }

        // 3. Nút Sẵn sàng / Hủy sẵn sàng
        if (readyButtonText != null)
        {
            readyButtonText.text = myReadyState ? "<b>HỦY SẴN SÀNG</b>" : "<b>SẴN SÀNG (READY)</b>";
        }
        if (readyButtonBg != null)
        {
            readyButtonBg.color = myReadyState ? new Color(0.85f, 0.3f, 0.3f) : new Color(0.18f, 0.7f, 0.35f);
        }

        // 4. Nút Bắt đầu trận đấu (Chủ bàn)
        if (startMatchButton != null)
        {
            startMatchButton.gameObject.SetActive(session.IsHost);

            if (session.IsHost)
            {
                bool canStart = session.CanStartMatch;
                startMatchButton.interactable = canStart;

                if (startButtonText != null)
                {
                    string hint = (table != null && table.playerCount < 2) ? "Cần >= 2 người" : "Đợi mọi người Ready";
                    startButtonText.text = canStart ? "<b>BẮT ĐẦU TRẬN ĐẤU</b>" : $"Bắt đầu ({hint})";
                }
                if (startButtonBg != null)
                {
                    startButtonBg.color = canStart ? new Color(0.95f, 0.75f, 0.15f) : new Color(0.4f, 0.4f, 0.45f);
                }
            }
        }

        // 5. Status Text
        if (statusText != null)
        {
            statusText.text = statusMessage;
        }
    }

    #region Button Click Handlers

    public void OnReadyClicked()
    {
        var session = TableSessionManager.Instance;
        if (session == null) return;

        session.RequestToggleReady(!myReadyState, (ok, msg) =>
        {
            statusMessage = msg;
            UpdateLobbyUI();
        });
    }

    public void OnStartMatchClicked()
    {
        var session = TableSessionManager.Instance;
        if (session == null) return;

        statusMessage = "Đang khởi tạo trận đấu...";
        session.RequestStartMatch((ok, msg) =>
        {
            if (!ok) statusMessage = $"Lỗi bắt đầu trận: {msg}";
            UpdateLobbyUI();
        });
    }

    public void OnQuickTestClicked()
    {
        statusMessage = "Chức năng Test Nhanh!";
        UpdateLobbyUI();
    }

    public void OnLeaveClicked()
    {
        var interact = FindAnyObjectByType<FirstPersonInteract>();
        if (interact != null) interact.LeaveSeat();
        else if (TableSessionManager.Instance != null) TableSessionManager.Instance.OnLeaveSeatSuccess();
    }

    #endregion
}
