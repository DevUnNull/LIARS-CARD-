using System;
using UnityEngine;
using LiarsCard.Network;

public class TableLobbyUI : MonoBehaviour
{
    public static TableLobbyUI Instance { get; private set; }

    [Header("Settings")]
    public bool enableOnGUI = true;

    private bool myReadyState = false;
    private string statusMessage = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (TableSessionManager.Instance != null)
        {
            TableSessionManager.Instance.OnTableStateChanged += OnTableStateChanged;
            TableSessionManager.Instance.OnMatchStarted += OnMatchStarted;
            TableSessionManager.Instance.OnLeftTable += OnLeftTable;
        }
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

    private void OnTableStateChanged(TableResponseDto table)
    {
        if (table == null) return;

        // Cap nhat trang thai ready cua ban than
        string myId = TableSessionManager.Instance.MyPlayerId;
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
    }

    private void OnMatchStarted(string matchId)
    {
        statusMessage = $"Ván đấu đang bắt đầu! Match ID: {matchId}";
    }

    private void OnLeftTable()
    {
        myReadyState = false;
        statusMessage = "";
    }

    private void OnGUI()
    {
        if (!enableOnGUI) return;

        var session = TableSessionManager.Instance;
        if (session == null || !session.isSeated) return;

        // Neu tran dau da bat dau, an UI phong cho de nhuong cho CardBattleUI
        if (!string.IsNullOrEmpty(session.currentMatchId)) return;

        var table = session.currentTableState;

        // Custom GUI Skin
        GUI.skin.box.fontSize = 14;
        GUI.skin.button.fontSize = 13;
        GUI.skin.label.fontSize = 13;

        // Ve bang trung tam Table Lobby
        float panelWidth = 500;
        float panelHeight = 440;
        float posX = (Screen.width - panelWidth) / 2f;
        float posY = (Screen.height - panelHeight) / 2f - 30f;

        GUILayout.BeginArea(new Rect(posX, posY, panelWidth, panelHeight), GUI.skin.box);

        // Header
        GUILayout.Space(5);
        GUILayout.Label($"<size=18><b>♠ LIARS CARD - PHÒNG CHỜ BÀN SỐ {session.currentTableId} ♠</b></size>", GetCenterHeaderStyle());
        GUILayout.Label($"Ghế của bạn: <b>Ghế {session.currentSeatIndex}</b> | Trạng thái: <color=yellow>ĐANG CHỜ BẮT ĐẦU</color>", GetCenterSubHeaderStyle());
        GUILayout.Space(8);

        // Danh sach 4 ghe
        GUILayout.Label("<b>DANH SÁCH GHẾ NGỒI (4 CHỖ):</b>");
        GUILayout.Space(4);

        if (table != null && table.seats != null)
        {
            string myId = session.MyPlayerId;

            for (int i = 0; i < table.seats.Count; i++)
            {
                var seat = table.seats[i];
                bool isMe = !string.IsNullOrEmpty(seat.playerId) && seat.playerId == myId;
                bool isHost = !string.IsNullOrEmpty(seat.playerId) && seat.playerId == table.hostPlayerId;

                GUILayout.BeginHorizontal(GUI.skin.box);

                // Cot 1: Ghe & Host
                string seatTitle = $"<b>Ghế {seat.seatIndex}:</b> ";
                if (isHost) seatTitle += "<color=gold>👑 [CHỦ BÀN]</color> ";
                if (isMe) seatTitle += "<color=cyan>(Bạn)</color> ";
                GUILayout.Label(seatTitle, GUILayout.Width(220));

                // Cot 2: Player ID
                if (string.IsNullOrEmpty(seat.playerId))
                {
                    GUI.color = Color.gray;
                    GUILayout.Label("<i>(Còn trống)</i>");
                    GUI.color = Color.white;
                }
                else
                {
                    string idShort = seat.playerId.Length > 12 ? seat.playerId.Substring(0, 12) + "..." : seat.playerId;
                    GUILayout.Label($"ID: {idShort}", GUILayout.Width(130));

                    // Cot 3: Ready badge
                    if (seat.isReady)
                    {
                        GUI.color = Color.green;
                        GUILayout.Label("<b>✔ ĐÃ SẴN SÀNG</b>");
                        GUI.color = Color.white;
                    }
                    else
                    {
                        GUI.color = new Color(0.9f, 0.5f, 0.2f);
                        GUILayout.Label("<b>⏳ Chưa Ready</b>");
                        GUI.color = Color.white;
                    }
                }

                GUILayout.EndHorizontal();
            }
        }
        else
        {
            GUILayout.Label("<i>Đang tải dữ liệu bàn từ server...</i>");
        }

        GUILayout.Space(10);

        // Nut Bam Tuong Tac
        GUILayout.BeginHorizontal();

        // Nut 1: Ready / Huy Ready
        if (myReadyState)
        {
            GUI.color = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("<b>HỦY SẴN SÀNG</b>", GUILayout.Height(40)))
            {
                session.RequestToggleReady(false, (ok, msg) => statusMessage = msg);
            }
        }
        else
        {
            GUI.color = Color.green;
            if (GUILayout.Button("<b>SẴN SÀNG (READY)</b>", GUILayout.Height(40)))
            {
                session.RequestToggleReady(true, (ok, msg) => statusMessage = msg);
            }
        }

        // Nut 2: Start Match (Danh cho Host)
        if (session.IsHost)
        {
            if (session.CanStartMatch)
            {
                GUI.color = new Color(1f, 0.8f, 0.2f);
                if (GUILayout.Button("<b>👑 BẮT ĐẦU TRẬN ĐẤU</b>", GUILayout.Height(40)))
                {
                    statusMessage = "Đang khởi tạo trận đấu...";
                    session.RequestStartMatch((ok, msg) =>
                    {
                        if (!ok) statusMessage = $"Lỗi bắt đầu trận: {msg}";
                    });
                }
            }
            else
            {
                GUI.color = Color.gray;
                string hint = (table != null && table.playerCount < 2) ? "Cần >= 2 người" : "Đợi mọi người Ready";
                GUILayout.Button($"👑 Bắt đầu ({hint})", GUILayout.Height(40));
            }
        }

        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Nut Roi Ghe
        GUI.color = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("RỜI BÀN (ESC)", GUILayout.Height(30)))
        {
            var interact = FindAnyObjectByType<FirstPersonInteract>();
            if (interact != null) interact.LeaveSeat();
            else session.OnLeaveSeatSuccess();
        }
        GUI.color = Color.white;

        // Status log text
        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Space(6);
            GUI.color = Color.yellow;
            GUILayout.Label(statusMessage, GetCenterSubHeaderStyle());
            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }

    private GUIStyle GetCenterHeaderStyle()
    {
        var style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        return style;
    }

    private GUIStyle GetCenterSubHeaderStyle()
    {
        var style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        return style;
    }
}