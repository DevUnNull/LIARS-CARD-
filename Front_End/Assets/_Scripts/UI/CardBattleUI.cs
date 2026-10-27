using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiarsCard.Network;

public class CardBattleUI : MonoBehaviour
{
    public static CardBattleUI Instance { get; private set; }

    [Header("Settings")]
    public bool enableOnGUI = true;

    [Header("Match State")]
    public string matchId = "";
    public int roundNumber = 0;
    public string tableRank = "-";
    public string currentTurnPlayerId = "-";
    public int stateVersion = 1;
    public PublicLastPlayDto lastPlay;
    public string winnerPlayerId = null;

    [Header("Player Hand")]
    public List<CardDto> myCards = new List<CardDto>();
    public List<bool> selectedCards = new List<bool>();

    // Penalty Modal State
    private bool showPenaltyModal = false;
    private string penaltyTitle = "";
    private string penaltyDetail = "";
    private float penaltyModalTimer = 0f;

    // Status Log
    private string statusMessage = "";
    private Coroutine matchPollCoroutine;

    public bool IsMyTurn
    {
        get
        {
            var session = TableSessionManager.Instance;
            if (session == null || string.IsNullOrEmpty(session.MyPlayerId)) return false;
            return !string.IsNullOrEmpty(currentTurnPlayerId) && currentTurnPlayerId == session.MyPlayerId;
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (TableSessionManager.Instance != null)
        {
            TableSessionManager.Instance.OnMatchStarted += StartBattle;
            TableSessionManager.Instance.OnLeftTable += StopBattle;
        }
    }

    private void OnDestroy()
    {
        if (TableSessionManager.Instance != null)
        {
            TableSessionManager.Instance.OnMatchStarted -= StartBattle;
            TableSessionManager.Instance.OnLeftTable -= StopBattle;
        }
    }

    private void Update()
    {
        if (showPenaltyModal)
        {
            penaltyModalTimer -= Time.deltaTime;
            if (penaltyModalTimer <= 0f)
            {
                showPenaltyModal = false;
            }
        }
    }

    public void StartBattle(string newMatchId)
    {
        matchId = newMatchId;
        winnerPlayerId = null;
        showPenaltyModal = false;
        statusMessage = "Đang kết nối vào ván đấu bài...";

        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return;

        // 1. Bao Ready trong Match de Server chia bai & bat dau round
        StartCoroutine(MatchApiClient.Instance.Ready(matchId, auth.AccessToken,
            onSuccess: res =>
            {
                ApplyState(res);
                statusMessage = $"Ván đấu bắt đầu! Round {res.roundNumber} - Rank bàn: {res.tableRank}";
                FetchMyHand();
            },
            onError: err => statusMessage = $"Lỗi ready match: {err}"
        ));

        // 2. Bat dau polling trang thai match
        if (matchPollCoroutine != null) StopCoroutine(matchPollCoroutine);
        matchPollCoroutine = StartCoroutine(PollMatchRoutine());
    }

    public void StopBattle()
    {
        matchId = "";
        myCards.Clear();
        selectedCards.Clear();
        lastPlay = null;
        winnerPlayerId = null;
        showPenaltyModal = false;

        if (matchPollCoroutine != null)
        {
            StopCoroutine(matchPollCoroutine);
            matchPollCoroutine = null;
        }
    }

    private IEnumerator PollMatchRoutine()
    {
        while (!string.IsNullOrEmpty(matchId))
        {
            yield return new WaitForSeconds(1.5f);

            var auth = AuthApiClient.Instance;
            if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) continue;

            StartCoroutine(MatchApiClient.Instance.GetState(matchId, auth.AccessToken,
                onSuccess: res => ApplyState(res),
                onError: null
            ));
        }
    }

    private void ApplyState(PublicMatchStateResponse res)
    {
        if (res == null) return;

        roundNumber = res.roundNumber;
        tableRank = res.tableRank;
        currentTurnPlayerId = res.currentTurnPlayerId;
        stateVersion = res.stateVersion;
        lastPlay = res.lastPlay;
        winnerPlayerId = res.winnerPlayerId;

        // Neu so luong bai tren tay bi lech (hoac round moi), lay lai bai
        if (myCards.Count == 0 && roundNumber > 0)
        {
            FetchMyHand();
        }
    }

    public void FetchMyHand()
    {
        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken) || string.IsNullOrEmpty(matchId)) return;

        StartCoroutine(MatchApiClient.Instance.GetHand(matchId, auth.AccessToken,
            onSuccess: res =>
            {
                myCards = res.cards ?? new List<CardDto>();
                selectedCards.Clear();
                for (int i = 0; i < myCards.Count; i++) selectedCards.Add(false);
                statusMessage = $"Đã nhận {myCards.Count} lá bài từ server!";
            },
            onError: err => statusMessage = $"Lỗi nhận bài: {err}"
        ));
    }

    public void ActionPlayCards()
    {
        if (!IsMyTurn)
        {
            statusMessage = "Chưa tới lượt của bạn!";
            return;
        }

        var chosenCardIds = new List<string>();
        for (int i = 0; i < myCards.Count; i++)
        {
            if (selectedCards[i]) chosenCardIds.Add(myCards[i].cardInstanceId);
        }

        if (chosenCardIds.Count < 1 || chosenCardIds.Count > 3)
        {
            statusMessage = "LUẬT GAME: Mỗi lượt chỉ được đánh từ 1 đến 3 lá bài!";
            return;
        }

        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return;

        var req = new PlayCardsRequest
        {
            actionId = Guid.NewGuid().ToString(),
            expectedVersion = stateVersion,
            cardInstanceIds = chosenCardIds
        };

        statusMessage = $"Đang đánh úp {chosenCardIds.Count} lá bài...";

        StartCoroutine(MatchApiClient.Instance.PlayCards(matchId, auth.AccessToken, req,
            onSuccess: res =>
            {
                stateVersion = res.stateVersion;
                statusMessage = $"ĐÁNH BÀI THÀNH CÔNG! Bạn còn {res.remainingCardCount} lá.";
                FetchMyHand();
            },
            onError: err => statusMessage = $"Lỗi đánh bài: {err}"
        ));
    }

    public void ActionChallenge()
    {
        if (lastPlay == null || string.IsNullOrEmpty(lastPlay.playId))
        {
            statusMessage = "Chưa có lượt đánh nào để tố cáo!";
            return;
        }

        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return;

        var req = new ChallengeRequest
        {
            actionId = Guid.NewGuid().ToString(),
            expectedVersion = stateVersion,
            targetPlayId = lastPlay.playId
        };

        statusMessage = "Đang tố cáo đối thủ nói dối (Liar!)...";

        StartCoroutine(MatchApiClient.Instance.Challenge(matchId, auth.AccessToken, req,
            onSuccess: res =>
            {
                stateVersion = res.stateVersion;

                // Thiet lap Popup Penalty
                showPenaltyModal = true;
                penaltyModalTimer = 4.5f;

                if (res.challengeSucceeded)
                {
                    penaltyTitle = "<color=green><b>TỐ CÁO ĐÚNG! CÓ GIAN LẬN!</b></color>";
                }
                else
                {
                    penaltyTitle = "<color=red><b>TỐ CÁO SAI! BÀI HỢP LỆ!</b></color>";
                }

                string victim = res.penalizedPlayerId == TableSessionManager.Instance.MyPlayerId ? "BẠN" : $"[{res.penalizedPlayerId}]";
                if (res.eliminated)
                {
                    penaltyDetail = $"💥 <b>ĐOÀNG!</b> Ổ đạn nổ trúng sọ!\n{victim} <b>ĐÃ BỊ LOẠI KHỎI TRẬN ĐẤU!</b>";
                }
                else
                {
                    penaltyDetail = $"💨 <b>CẠCH!</b> Cò súng tạch!\n{victim} <b>MAY MẮN SỐNG SÓT! (PHEW)</b>";
                }

                FetchMyHand();
            },
            onError: err => statusMessage = $"Lỗi tố cáo: {err}"
        ));
    }

    private void OnGUI()
    {
        if (!enableOnGUI) return;
        if (string.IsNullOrEmpty(matchId)) return;

        // Custom GUI Skin
        GUI.skin.box.fontSize = 14;
        GUI.skin.button.fontSize = 13;
        GUI.skin.label.fontSize = 13;

        // 1. TOP HEADER: Thong tin van dau
        RenderTopHeader();

        // 2. MIDDLE: Chống bài úp & lượt đánh trước
        RenderMiddleArena();

        // 3. BOTTOM: Bài trên tay & Nút thao tác
        RenderBottomHand();

        // 4. MODAL POPUP: Penalty hoặc Victory
        if (showPenaltyModal)
        {
            RenderPenaltyModal();
        }
        else if (!string.IsNullOrEmpty(winnerPlayerId))
        {
            RenderVictoryModal();
        }
    }

    private void RenderTopHeader()
    {
        float width = 640;
        float height = 75;
        float posX = (Screen.width - width) / 2f;
        float posY = 15;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.BeginHorizontal();

        // Vong dau
        GUILayout.Label($"<b>VÒNG: {roundNumber}</b>\nVersion: {stateVersion}", GUILayout.Width(110));

        // Rank ban
        GUILayout.BeginVertical();
        GUILayout.Label($"<size=17>RANK BÀN: <color=yellow><b>QUÂN {tableRank}</b></color></size>", GetCenterHeaderStyle());
        GUILayout.Label("<i>(Bắt buộc đánh bài theo rank này hoặc Joker)</i>", GetCenterSubHeaderStyle());
        GUILayout.EndVertical();

        // Luot di
        GUILayout.BeginVertical(GUILayout.Width(180));
        if (IsMyTurn)
        {
            GUI.color = Color.green;
            GUILayout.Label("<size=15><b>👉 LƯỢT CỦA BẠN!</b></size>", GetCenterHeaderStyle());
            GUI.color = Color.white;
        }
        else
        {
            string shortTurnId = currentTurnPlayerId.Length > 12 ? currentTurnPlayerId.Substring(0, 12) + "..." : currentTurnPlayerId;
            GUILayout.Label($"Đang đợi: <b>{shortTurnId}</b>", GetCenterHeaderStyle());
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void RenderMiddleArena()
    {
        float width = 450;
        float height = 75;
        float posX = (Screen.width - width) / 2f;
        float posY = 98;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.Label("<b>🃏 MẶT BÀN ĐẤU</b>", GetCenterHeaderStyle());

        if (lastPlay != null && !string.IsNullOrEmpty(lastPlay.playId))
        {
            string pId = lastPlay.playerId.Length > 12 ? lastPlay.playerId.Substring(0, 12) + "..." : lastPlay.playerId;
            GUILayout.Label($"Lượt trước: <color=cyan><b>{pId}</b></color> vừa đánh úp <b>{lastPlay.cardCount}</b> lá bài!", GetCenterSubHeaderStyle());
        }
        else
        {
            GUILayout.Label("<i>(Chưa có ai đánh bài trong vòng này)</i>", GetCenterSubHeaderStyle());
        }

        GUILayout.EndArea();
    }

    private void RenderBottomHand()
    {
        float width = 640;
        float height = 230;
        float posX = (Screen.width - width) / 2f;
        float posY = Screen.height - height - 20;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);

        int selectedCount = 0;
        for (int i = 0; i < selectedCards.Count; i++) if (selectedCards[i]) selectedCount++;

        GUILayout.BeginHorizontal();
        GUILayout.Label($"<b>BÀI TRÊN TAY CỦA BẠN:</b> ({myCards.Count} lá)");
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Đã chọn: <color=orange><b>{selectedCount}/3</b></color> lá");
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Render 5 Card Slots
        GUILayout.BeginHorizontal();
        if (myCards.Count == 0)
        {
            GUILayout.Label("<i>(Đang nhận bài từ Server hoặc bạn đã đánh hết bài an toàn!)</i>");
        }
        else
        {
            for (int i = 0; i < myCards.Count; i++)
            {
                var card = myCards[i];
                bool isSel = selectedCards[i];

                // Card slot box
                if (isSel) GUI.color = new Color(1f, 0.85f, 0.3f);
                else GUI.color = Color.white;

                string rankColor = card.rank == "JOKER" ? "magenta" : (card.rank == tableRank ? "yellow" : "white");
                string cardText = $"\n<size=22><color={rankColor}><b>{card.rank}</b></color></size>\n\n{(isSel ? "<b>[ĐÃ CHỌN]</b>" : "Click chọn")}";

                if (GUILayout.Button(cardText, GUILayout.Width(110), GUILayout.Height(105)))
                {
                    selectedCards[i] = !selectedCards[i];
                }

                GUI.color = Color.white;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Action Buttons Row
        GUILayout.BeginHorizontal();

        // Nut 1: Danh bai
        if (IsMyTurn && selectedCount >= 1 && selectedCount <= 3)
        {
            GUI.color = Color.green;
            if (GUILayout.Button($"<b>ĐÁNH {selectedCount} LÁ BÀI (PLAY CARDS)</b>", GUILayout.Height(42)))
            {
                ActionPlayCards();
            }
        }
        else
        {
            GUI.color = Color.gray;
            string disabledHint = !IsMyTurn ? "Chưa tới lượt" : (selectedCount == 0 ? "Chọn 1-3 lá" : "Tối đa 3 lá");
            GUILayout.Button($"Đánh bài ({disabledHint})", GUILayout.Height(42));
        }

        // Nut 2: To cao
        if (lastPlay != null && !string.IsNullOrEmpty(lastPlay.playId))
        {
            GUI.color = new Color(0.95f, 0.25f, 0.25f);
            if (GUILayout.Button("<b>🔫 TỐ CÁO (CHALLENGE / LIAR!)</b>", GUILayout.Height(42)))
            {
                ActionChallenge();
            }
        }
        else
        {
            GUI.color = Color.gray;
            GUILayout.Button("Tố cáo (Chưa có bài trên bàn)", GUILayout.Height(42));
        }

        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        // Status log
        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Space(4);
            GUI.color = Color.yellow;
            GUILayout.Label(statusMessage, GetCenterSubHeaderStyle());
            GUI.color = Color.white;
        }

        GUILayout.EndArea();
    }

    private void RenderPenaltyModal()
    {
        float width = 480;
        float height = 210;
        float posX = (Screen.width - width) / 2f;
        float posY = (Screen.height - height) / 2f;

        GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        GUI.Box(new Rect(posX - 10, posY - 10, width + 20, height + 20), "");
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.Space(10);
        GUILayout.Label("<size=18><b>⚖ KẾT QUẢ TỐ CÁO & CÒ SÚNG PHẠT ⚖</b></size>", GetCenterHeaderStyle());
        GUILayout.Space(8);
        GUILayout.Label(penaltyTitle, GetCenterHeaderStyle());
        GUILayout.Space(8);
        GUILayout.Label($"<size=15>{penaltyDetail}</size>", GetCenterHeaderStyle());
        GUILayout.Space(12);
        GUILayout.Label($"<i>Tự động chuyển vòng sau: {Mathf.CeilToInt(penaltyModalTimer)}s...</i>", GetCenterSubHeaderStyle());
        GUILayout.EndArea();
    }

    private void RenderVictoryModal()
    {
        float width = 500;
        float height = 240;
        float posX = (Screen.width - width) / 2f;
        float posY = (Screen.height - height) / 2f;

        GUI.color = new Color(0.05f, 0.1f, 0.05f, 0.95f);
        GUI.Box(new Rect(posX - 10, posY - 10, width + 20, height + 20), "");
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.Space(15);

        bool isMeWinner = (winnerPlayerId == TableSessionManager.Instance.MyPlayerId);
        if (isMeWinner)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("<size=22><b>🏆 CHIẾN THẮNG TUYỆT ĐỐI! 🏆</b></size>", GetCenterHeaderStyle());
            GUI.color = Color.white;
            GUILayout.Label("<b>Bạn là người sống sót duy nhất tại bàn chơi Liar's Card!</b>", GetCenterHeaderStyle());
        }
        else
        {
            GUI.color = Color.red;
            GUILayout.Label("<size=22><b>💀 BẠN ĐÃ THUA CUỘC! 💀</b></size>", GetCenterHeaderStyle());
            GUI.color = Color.white;
            GUILayout.Label($"Người chiến thắng ván chơi: <b>{winnerPlayerId}</b>", GetCenterHeaderStyle());
        }

        GUILayout.Space(25);
        GUI.color = new Color(0.2f, 0.8f, 0.9f);
        if (GUILayout.Button("<b>QUAY VỀ PHÒNG CHỜ BÀN</b>", GUILayout.Height(40)))
        {
            winnerPlayerId = null;
            StopBattle();
            TableSessionManager.Instance.RefreshTableNow();
        }
        GUI.color = Color.white;

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