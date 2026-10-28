using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using LiarsCard.Network;
using LiarsCard.Cards3D;

public class CardBattleUI : MonoBehaviour
{
    public static CardBattleUI Instance { get; private set; }

    [Header("Settings")]
    public bool enableOnGUI = false; // Tắt OnGUI cũ, ưu tiên dùng uGUI Canvas GameObject trên Hierarchy

    [Header("Match State")]
    public string matchId = "";
    public int roundNumber = 0;
    public string tableRank = "-";
    public string currentTurnPlayerId = "-";
    public int stateVersion = 1;
    public PublicLastPlayDto lastPlay;
    public string winnerPlayerId = null;
    public string penalizedPlayerId = "";
    private string pendingWinnerId = null;

    [Header("Player Hand")]
    public List<CardDto> myCards = new List<CardDto>();
    public List<bool> selectedCards = new List<bool>();
    public int focusedCardIndex = 0;

    [Header("Russian Roulette Penalty State")]
    public bool awaitingTriggerPull = false;
    public bool triggerPulled = false;
    public bool isPenalizedPlayer = false;
    public float triggerHoldProgress = 0f;
    public float triggerArmCooldown = 0f;
    private const float TRIGGER_HOLD_TIME = 0.6f;

    private bool showPenaltyModal = false;
    private string penaltyTitle = "";
    private string penaltyDetail = "";
    private float penaltyModalTimer = 0f;
    private ChallengeResponse pendingChallengeResult;

    // Status Log
    private string statusMessage = "";
    private Coroutine matchPollCoroutine;

    [Header("uGUI Battle Hierarchy References")]
    public GameObject battleHUD;
    public TMP_Text roundText;
    public TMP_Text tableRankText;
    public Image imageCurrentCart;
    public TMP_Text currentTurnText;
    public TMP_Text lastPlayText;
    public TMP_Text hudHintText;
    public Button playCardsButton;
    public TMP_Text playCardsButtonText;
    public Button callLiarButton;

    [Header("Card Visual Sprites (_0: A, _1: K, _2: Q, _3: Joker, _4: Back)")]
    public Sprite spriteA;
    public Sprite spriteK;
    public Sprite spriteQ;
    public Sprite spriteJoker;
    public Sprite spriteBack;

    [Header("uGUI Russian Roulette Modal")]
    public GameObject russianRouletteModal;
    public TMP_Text rrTitleText;
    public TMP_Text rrDetailText;
    public Slider rrProgressBar;
    public Button rrTriggerButton;
    public TMP_Text rrTriggerButtonText;

    [Header("uGUI Game Over Modal")]
    public GameObject gameOverModal;
    public TMP_Text goTitleText;
    public TMP_Text goWinnerText;
    public Button goReturnButton;
    public Button goLeaveButton;

    public bool IsInBattle => !string.IsNullOrEmpty(matchId);

    public bool IsMyTurn
    {
        get
        {
            var session = TableSessionManager.Instance;
            if (session == null || string.IsNullOrEmpty(session.MyPlayerId)) return false;
            return !string.IsNullOrEmpty(currentTurnPlayerId) &&
                   string.Equals(currentTurnPlayerId, session.MyPlayerId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public int SelectedCardCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < selectedCards.Count; i++)
            {
                if (selectedCards[i]) count++;
            }
            return count;
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Tự động tìm ImageCurentCart nếu chưa gán
        if (imageCurrentCart == null)
        {
            var imgObj = GameObject.Find("ImageCurentCart");
            if (imgObj != null) imageCurrentCart = imgObj.GetComponent<Image>();
        }
        if (imageCurrentCart != null)
        {
            imageCurrentCart.preserveAspect = true;
        }

        // Đăng ký sprites vào CardTextureGenerator
        CardTextureGenerator.RegisterSprites(spriteA, spriteK, spriteQ, spriteJoker, spriteBack);

        // Khởi tạo các Manager 3D nếu chưa có
        if (GetComponent<CardHand3D>() == null) gameObject.AddComponent<CardHand3D>();
        if (GetComponent<CardAudioSystem>() == null) gameObject.AddComponent<CardAudioSystem>();
        if (GetComponent<TableCenterPile3D>() == null) gameObject.AddComponent<TableCenterPile3D>();
        if (FindAnyObjectByType<TableDeck3D>() == null)
        {
            var deckObj = new GameObject("[TableDeck3D]");
            deckObj.AddComponent<TableDeck3D>();
        }

        // Gắn sự kiện nút bấm uGUI
        if (playCardsButton != null) playCardsButton.onClick.AddListener(ActionPlayCards);
        if (callLiarButton != null) callLiarButton.onClick.AddListener(ActionChallenge);
        if (rrTriggerButton != null) rrTriggerButton.onClick.AddListener(ExecuteTriggerPull);
        if (goReturnButton != null) goReturnButton.onClick.AddListener(ActionReturnToLobby);
        if (goLeaveButton != null) goLeaveButton.onClick.AddListener(ActionLeaveAfterMatch);

        if (TableSessionManager.Instance != null)
        {
            TableSessionManager.Instance.OnMatchStarted += StartBattle;
            TableSessionManager.Instance.OnLeftTable += StopBattle;
        }

        UpdateUGUI();
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
        if (string.IsNullOrEmpty(matchId))
        {
            UpdateUGUI();
            return;
        }

        // Giảm cooldown lên nòng súng (tránh click nhầm từ nút bấm trước)
        if (triggerArmCooldown > 0f)
        {
            triggerArmCooldown -= Time.deltaTime;
        }

        // Xử lý đếm ngược tắt Modal Penalty sau khi đã bóp cò
        if (showPenaltyModal && !awaitingTriggerPull)
        {
            penaltyModalTimer -= Time.deltaTime;
            if (penaltyModalTimer <= 0f)
            {
                showPenaltyModal = false;

                // Sau khi tắt kết quả bắn súng xong thì mới chuyển sang màn hình Chiến Thắng / Thua Cuộc (nếu có)
                if (!string.IsNullOrEmpty(pendingWinnerId))
                {
                    winnerPlayerId = pendingWinnerId;
                    pendingWinnerId = null;
                }
                else
                {
                    // Người chơi may mắn sống sót: Lấy bài mới cho round tiếp theo và tiếp tục trận đấu
                    statusMessage = "<color=green>Bạn may mắn sống sót! Tiếp tục vòng đấu tiếp theo...</color>";
                    FetchMyHand();
                }
            }
        }

        // Xử lý Input bàn phím & chuột
        HandleGameplayInput();

        // Cập nhật các GameObject uGUI trên Hierarchy
        UpdateUGUI();
    }

    #region Input Handling (A/D, Space, E, X, Mouse Left Hold/Click)

    private void HandleGameplayInput()
    {
        // 1. Nếu đang ở màn hình Russian Roulette: Xử lý Bóp cò súng Nga
        if (awaitingTriggerPull)
        {
            HandleRussianRouletteInput();
            return;
        }

        // Nếu modal kết quả hoặc victory đang hiển thị thì không nhận phím đánh bài
        if (showPenaltyModal || !string.IsNullOrEmpty(winnerPlayerId))
        {
            return;
        }

        // 2. Phím E: Đặt / Hạ bài xuống bàn (Play Cards)
        // Náº¿u cÃ³ CardHand3D xá»­ lÃ½ FPP 3D State Pattern thÃ¬ Ä‘á»ƒ CardHand3D tá»± Ä‘iá»u hÆ°á»›ng vÃ  thá»±c thi
        if (CardHand3D.Instance != null)
        {
            return;
        }

        // Fallback khi khÃ´ng cÃ³ há»‡ thá»‘ng CardHand3D:
        if (IsKeyEPressed())
        {
            ActionPlayCards();
        }

        // 3. Phím X: Bắt láo (Call Liar!) người đi trước đó
        if (IsKeyXPressed())
        {
            ActionChallenge();
        }
    }

    private void HandleRussianRouletteInput()
    {
        if (triggerPulled) return;

        // Đang trong thời gian rút súng và lên nòng -> chưa nhận input bóp cò
        if (triggerArmCooldown > 0f) return;

        // Nếu KHÔNG phải người bị phạt (Spectator):
        if (!isPenalizedPlayer)
        {
            // Spectator không thể tự bóp cò, chỉ quan sát và chờ server broadcast kết quả
            triggerHoldProgress = 0f;
            return;
        }

        bool isLeftMouseDown = false;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            isLeftMouseDown = mouse.leftButton.isPressed;
        }
        else
        {
            try { isLeftMouseDown = Input.GetMouseButton(0); } catch { }
        }

        // Bóp cò bằng cách GIỮ chuột trái (hoặc click nút GUI)
        if (isLeftMouseDown)
        {
            triggerHoldProgress += Time.deltaTime / TRIGGER_HOLD_TIME;
            if (triggerHoldProgress >= 1f)
            {
                ExecuteTriggerPull();
            }
        }
        else
        {
            triggerHoldProgress = Mathf.Max(0f, triggerHoldProgress - Time.deltaTime * 2.5f);
        }
    }

    public void ExecuteTriggerPull()
    {
        if (triggerPulled || !isPenalizedPlayer) return;

        triggerPulled = true;
        triggerHoldProgress = 1f;

        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return;

        var req = new PullTriggerRequest
        {
            actionId = Guid.NewGuid().ToString(),
            expectedVersion = stateVersion
        };

        statusMessage = "Đang siết cò súng Nga...";

        StartCoroutine(MatchApiClient.Instance.PullTrigger(matchId, auth.AccessToken, req,
            onSuccess: res =>
            {
                stateVersion = res.stateVersion;
                OnTriggerResultReceived(res.penalizedPlayerId, res.isBulletHit, res.survived, res.eliminated, res.nextPhase, res.winnerPlayerId);
            },
            onError: err =>
            {
                statusMessage = $"Lỗi bóp cò: {err}";
                triggerPulled = false; // Cho phép thử lại nếu lỗi mạng
            }
        ));
    }

    public void OnPenaltyWaitingForTrigger(string penalizedId, string challengerId, bool challengeSucceeded)
    {
        penalizedPlayerId = penalizedId;
        string myId = TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : "";
        isPenalizedPlayer = !string.IsNullOrEmpty(myId) && string.Equals(penalizedId, myId, StringComparison.OrdinalIgnoreCase);

        awaitingTriggerPull = true;
        triggerPulled = false;
        triggerHoldProgress = 0f;
        triggerArmCooldown = 0.5f;
        showPenaltyModal = true;
        pendingWinnerId = null;

        if (challengeSucceeded)
        {
            penaltyTitle = "<color=#2ECC71><b>BẮT LÁO ĐÚNG! ĐỐI THỦ ĐÃ NÓI DỐI!</b></color>";
        }
        else
        {
            penaltyTitle = "<color=#E74C3C><b>BẮT LÁO SAI! BÀI ĐỐI THỦ HỢP LỆ!</b></color>";
        }

        if (isPenalizedPlayer)
        {
            statusMessage = "<color=red><b>BẠN BỊ PHẠT! GIỮ CHUỘT TRÁI ĐỂ TỰ BÓP CÒ VÀO ĐẦU!</b></color>";
        }
        else
        {
            statusMessage = $"<color=yellow>Đang chờ [{penalizedId}] tự bóp cò súng Nga...</color>";
        }

        UpdateUGUI();
    }

    public void OnTriggerResultReceived(string victimId, bool isBulletHit, bool survived, bool eliminated, string nextPhase, string winnerId)
    {
        awaitingTriggerPull = false;
        showPenaltyModal = true;
        triggerHoldProgress = 1f;

        string myId = TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : "";
        bool isMe = !string.IsNullOrEmpty(myId) && string.Equals(victimId, myId, StringComparison.OrdinalIgnoreCase);
        string victimName = isMe ? "BẠN" : $"[{victimId}]";

        // Phát âm thanh súng nổ hoặc cạch khô
        if (CardAudioSystem.Instance != null)
        {
            string sfxName = isBulletHit ? "Gunshot" : "DryFire";
            CardAudioSystem.Instance.PlaySpatialSfx(sfxName, transform.position);
        }

        if (eliminated)
        {
            penaltyModalTimer = 4.0f; // Hiện kết quả phạt súng trong 4.0s
            penaltyDetail = $"💥 <b>ĐOÀNG! BÙM!</b> Ổ đạn nổ tung sọ!\n<color=red><b>{victimName} ĐÃ BỊ LOẠI KHỎI TRẬN ĐẤU!</b></color>";

            if (!string.IsNullOrEmpty(winnerId))
            {
                pendingWinnerId = winnerId;
            }
            else if (isMe)
            {
                pendingWinnerId = "Đối thủ";
            }
            else
            {
                pendingWinnerId = !string.IsNullOrEmpty(myId) ? myId : "Bạn";
            }
        }
        else
        {
            penaltyModalTimer = 3.5f; // Hiện kết quả sống sót trong 3.5s
            penaltyDetail = $"💨 <b>*CẠCH!*</b> Cò súng gõ vào ổ đạn rỗng!\n<color=green><b>{victimName} MAY MẮN SỐNG SÓT! (PHEW)</b></color>";
            pendingWinnerId = null;
        }

        UpdateUGUI();
    }

    public void ToggleSelectCard(int index)
    {
        if (index < 0 || index >= myCards.Count || index >= selectedCards.Count) return;

        focusedCardIndex = index;

        if (selectedCards[index])
        {
            selectedCards[index] = false;
            statusMessage = $"Đã bỏ chọn lá {myCards[index].rank}. Còn chọn: {SelectedCardCount}/3 lá.";
        }
        else
        {
            if (SelectedCardCount >= 3)
            {
                statusMessage = "<color=red><b>CHỈ ĐƯỢC CHỌN TỐI ĐA 3 LÁ BÀI!</b> (Hãy bỏ bớt lá khác trước)</color>";
            }
            else
            {
                selectedCards[index] = true;
                statusMessage = $"Đã chọn lá {myCards[index].rank}. Tổng cộng: {SelectedCardCount}/3 lá.";
            }
        }

        // Cập nhật lên CardHand3D nếu có
        if (CardHand3D.Instance != null && index < CardHand3D.Instance.cardsInHand.Count)
        {
            CardHand3D.Instance.cardsInHand[index].SetSelected(selectedCards[index]);
        }
    }

    private bool IsKeyEPressed()
    {
        var kb = Keyboard.current;
        if (kb != null) return kb.eKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.E); } catch { return false; }
    }

    private bool IsKeyXPressed()
    {
        var kb = Keyboard.current;
        if (kb != null) return kb.xKey.wasPressedThisFrame;
        try { return Input.GetKeyDown(KeyCode.X); } catch { return false; }
    }

    #endregion

    #region Battle Lifecycle & Server API Calls

    public void StartBattle(string newMatchId)
    {
        matchId = newMatchId;
        winnerPlayerId = null;
        pendingWinnerId = null;
        showPenaltyModal = false;
        awaitingTriggerPull = false;
        triggerPulled = false;
        triggerHoldProgress = 0f;
        triggerArmCooldown = 0f;
        statusMessage = "Đang kết nối vào ván đấu bài...";

        // Reset Table Pile
        if (TableCenterPile3D.Instance != null)
        {
            TableCenterPile3D.Instance.ClearPile();
        }

        var auth = AuthApiClient.Instance;
        if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return;

        // 1. Báo Ready trong Match để Server chia bài & bắt đầu round
        StartCoroutine(MatchApiClient.Instance.Ready(matchId, auth.AccessToken,
            onSuccess: res =>
            {
                ApplyState(res);
                statusMessage = $"Ván đấu bắt đầu! Round {res.roundNumber} - Rank bàn: {res.tableRank}";
                FetchMyHand();
            },
            onError: err => statusMessage = $"Lỗi ready match: {err}"
        ));

        // 2. Bắt đầu polling trạng thái match
        if (matchPollCoroutine != null) StopCoroutine(matchPollCoroutine);
        matchPollCoroutine = StartCoroutine(PollMatchRoutine());

        UpdateUGUI();
    }

    public void StopBattle()
    {
        matchId = "";
        myCards.Clear();
        selectedCards.Clear();
        focusedCardIndex = 0;
        lastPlay = null;
        winnerPlayerId = null;
        pendingWinnerId = null;
        showPenaltyModal = false;
        awaitingTriggerPull = false;
        triggerPulled = false;

        // Dọn dẹp hand 3D và pile bài trên bàn
        if (CardHand3D.Instance != null)
        {
            CardHand3D.Instance.ClearHand();
        }
        if (TableCenterPile3D.Instance != null)
        {
            TableCenterPile3D.Instance.ClearPile();
        }

        if (matchPollCoroutine != null)
        {
            StopCoroutine(matchPollCoroutine);
            matchPollCoroutine = null;
        }

        UpdateUGUI();
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

        int oldRound = roundNumber;
        roundNumber = res.roundNumber;
        tableRank = res.tableRank;
        currentTurnPlayerId = res.currentTurnPlayerId;
        stateVersion = res.stateVersion;
        lastPlay = res.lastPlay;

        // Nếu qua round mới: dọn dẹp chồng bài cũ trên bàn
        if (roundNumber > oldRound)
        {
            if (TableCenterPile3D.Instance != null)
            {
                TableCenterPile3D.Instance.ClearPile();
            }
        }

        // Nếu đang trong quá trình bóp cò súng Nga hoặc xem kết quả súng, TUYỆT ĐỐI KHÔNG cắt ngang màn hình bằng winnerPlayerId
        if (awaitingTriggerPull || showPenaltyModal)
        {
            // Để ExecuteTriggerPull và penaltyModalTimer tự quyết định winnerPlayerId
        }
        else
        {
            winnerPlayerId = res.winnerPlayerId;
        }

        // Đồng bộ trạng thái phạt từ Polling Server nếu gói UDP bị trễ
        if (res.phase == "PenaltyResolution" && !string.IsNullOrEmpty(res.penalizedPlayerId))
        {
            if (!awaitingTriggerPull && !showPenaltyModal)
            {
                OnPenaltyWaitingForTrigger(res.penalizedPlayerId, "", false);
            }
        }

        // Nếu sang round mới hoặc chưa có bài thì lấy bài mới
        if (roundNumber > oldRound && roundNumber > 0)
        {
            FetchMyHand();
        }
        else if (myCards.Count == 0 && roundNumber > 0)
        {
            FetchMyHand();
        }

        UpdateUGUI();
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
                focusedCardIndex = Mathf.Clamp(focusedCardIndex, 0, Mathf.Max(0, myCards.Count - 1));
                statusMessage = $"Đã nhận {myCards.Count} lá bài 3D! [Giữ Space / Chuột phải] để hé bài lên xem.";

                // Đồng bộ sang hệ thống bài 3D
                if (CardHand3D.Instance != null)
                {
                    CardHand3D.Instance.SyncHand(myCards);
                }

                UpdateUGUI();
            },
            onError: err => statusMessage = $"Lỗi nhận bài: {err}"
        ));
    }

    public void ActionPlayCards()
    {
        if (!IsMyTurn)
        {
            statusMessage = "<color=red><b>CHƯA TỚI LƯỢT CỦA BẠN!</b></color>";
            return;
        }

        var chosenCardIds = new List<string>();
        for (int i = 0; i < myCards.Count; i++)
        {
            if (selectedCards[i]) chosenCardIds.Add(myCards[i].cardInstanceId);
        }

        if (chosenCardIds.Count < 1 || chosenCardIds.Count > 3)
        {
            statusMessage = "<color=yellow><b>LUẬT GAME: Mỗi lượt chỉ được đặt từ 1 đến 3 lá bài!</b></color>";
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

        statusMessage = $"Đang hạ úp {chosenCardIds.Count} lá bài xuống bàn...";

        StartCoroutine(MatchApiClient.Instance.PlayCards(matchId, auth.AccessToken, req,
            onSuccess: res =>
            {
                stateVersion = res.stateVersion;
                statusMessage = $"HẠ BÀI THÀNH CÔNG! Bạn còn lại {res.remainingCardCount} lá.";

                // Kích hoạt animation quăng bài 3D bay úp xuống chồng bài giữa bàn!
                if (CardHand3D.Instance != null)
                {
                    CardHand3D.Instance.AnimateThrowSelectedCards(() =>
                    {
                        FetchMyHand();
                    });
                }
                else
                {
                    FetchMyHand();
                }
            },
            onError: err => statusMessage = $"Lỗi hạ bài: {err}"
        ));
    }

    public void ActionChallenge()
    {
        if (lastPlay == null || string.IsNullOrEmpty(lastPlay.playId))
        {
            statusMessage = "<color=yellow>Chưa có lượt đánh nào trước đó để Bắt Láo (Call Liar)!</color>";
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

        statusMessage = "Đang tố cáo đối thủ BẮT LÁO (Call Liar!)...";

        StartCoroutine(MatchApiClient.Instance.Challenge(matchId, auth.AccessToken, req,
            onSuccess: res =>
            {
                stateVersion = res.stateVersion;
                string myId = TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : "";
                OnPenaltyWaitingForTrigger(res.penalizedPlayerId, myId, res.challengeSucceeded);
            },
            onError: err => statusMessage = $"Lỗi bắt láo: {err}"
        ));
    }

    public void ActionReturnToLobby()
    {
        winnerPlayerId = null;
        pendingWinnerId = null;
        StopBattle();
        if (TableSessionManager.Instance != null)
        {
            TableSessionManager.Instance.OnMatchEnded();
        }
    }

    public void ActionLeaveAfterMatch()
    {
        winnerPlayerId = null;
        pendingWinnerId = null;
        StopBattle();
        var interact = FindAnyObjectByType<FirstPersonInteract>();
        if (interact != null) interact.LeaveSeat();
        else if (TableSessionManager.Instance != null) TableSessionManager.Instance.OnLeaveSeatSuccess();
    }

    #endregion

    #region uGUI Hierarchy Updates

    public void UpdateUGUI()
    {
        bool inBattle = IsInBattle;

        // 1. Russian Roulette Modal
        if (russianRouletteModal != null)
        {
            bool showRR = inBattle && showPenaltyModal;
            if (russianRouletteModal.activeSelf != showRR) russianRouletteModal.SetActive(showRR);

            if (showRR)
            {
                if (rrTitleText != null) rrTitleText.text = penaltyTitle;
                if (rrDetailText != null)
                {
                    if (awaitingTriggerPull)
                    {
                        if (isPenalizedPlayer)
                        {
                            rrDetailText.text = "Bạn vừa vi phạm luật chơi! Hãy giữ chuột trái hoặc nhấn nút để bóp cò!";
                        }
                        else
                        {
                            string victim = !string.IsNullOrEmpty(penalizedPlayerId) ? penalizedPlayerId : "Đối thủ";
                            rrDetailText.text = $"[{victim}] đang run rẩy đặt súng vào đầu và chuẩn bị bóp cò... <i>(Đang chờ đối phương bóp cò)</i>";
                        }
                    }
                    else
                    {
                        rrDetailText.text = penaltyDetail;
                    }
                }

                if (rrProgressBar != null)
                {
                    rrProgressBar.value = triggerHoldProgress;
                }

                if (rrTriggerButton != null)
                {
                    bool showBtn = awaitingTriggerPull && isPenalizedPlayer;
                    rrTriggerButton.gameObject.SetActive(showBtn);
                    rrTriggerButton.interactable = (triggerArmCooldown <= 0f);
                }

                if (battleHUD != null && battleHUD.activeSelf) battleHUD.SetActive(false);
                if (gameOverModal != null && gameOverModal.activeSelf) gameOverModal.SetActive(false);
                return;
            }
        }

        // 2. Game Over Modal
        if (gameOverModal != null)
        {
            bool hasWinner = inBattle && !string.IsNullOrEmpty(winnerPlayerId);
            if (gameOverModal.activeSelf != hasWinner) gameOverModal.SetActive(hasWinner);

            if (hasWinner)
            {
                string myId = TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : null;
                bool isMeWinner = (!string.IsNullOrEmpty(myId) && string.Equals(winnerPlayerId, myId, StringComparison.OrdinalIgnoreCase))
                                  || string.Equals(winnerPlayerId, "Bạn", StringComparison.OrdinalIgnoreCase);

                if (goTitleText != null)
                {
                    goTitleText.text = isMeWinner ? "🏆 <color=#F1C40F>CHIẾN THẮNG TUYỆT ĐỐI!</color> 🏆" : "💀 <color=#E74C3C>BẠN ĐÃ THUA CUỘC!</color> 💀";
                }
                if (goWinnerText != null)
                {
                    goWinnerText.text = isMeWinner ? "Bạn là người sống sót duy nhất tại bàn chơi Liar's Card!" : $"Người chiến thắng ván chơi: <b>{winnerPlayerId}</b>";
                }

                if (battleHUD != null && battleHUD.activeSelf) battleHUD.SetActive(false);
                return;
            }
        }

        // 3. Battle HUD (Top Header, Arena Info, Bottom Hotkeys)
        if (battleHUD != null)
        {
            if (battleHUD.activeSelf != inBattle) battleHUD.SetActive(inBattle);

            if (inBattle)
            {
                if (roundText != null)
                {
                    roundText.text = $"<b>VÒNG: {roundNumber}</b>\n<size=12>Ver: {stateVersion}</size>";
                }

                if (tableRankText != null)
                {
                    tableRankText.text = $"RANK BÀN: <color=#F1C40F><b>QUÂN {tableRank}</b></color>";
                }

                if (imageCurrentCart != null)
                {
                    Sprite activeSprite = CardTextureGenerator.GetCardSprite(tableRank);
                    if (activeSprite != null && !string.IsNullOrEmpty(tableRank) && tableRank != "-")
                    {
                        if (!imageCurrentCart.gameObject.activeSelf) imageCurrentCart.gameObject.SetActive(true);
                        imageCurrentCart.sprite = activeSprite;
                    }
                    else
                    {
                        // Khi chưa vào ván hoặc chưa lật bài: hiển thị mặt sau lá bài (_4)
                        Sprite back = CardTextureGenerator.GetCardBackSprite();
                        if (back != null)
                        {
                            if (!imageCurrentCart.gameObject.activeSelf) imageCurrentCart.gameObject.SetActive(true);
                            imageCurrentCart.sprite = back;
                        }
                    }
                }

                if (currentTurnText != null)
                {
                    if (IsMyTurn)
                    {
                        currentTurnText.text = "<color=#2ECC71><b>👉 LƯỢT CỦA BẠN! [E]</b></color>";
                    }
                    else
                    {
                        string shortTurnId = currentTurnPlayerId.Length > 12 ? currentTurnPlayerId.Substring(0, 12) + "..." : currentTurnPlayerId;
                        currentTurnText.text = $"Đang đợi: <b>{shortTurnId}</b>";
                    }
                }

                if (lastPlayText != null)
                {
                    if (lastPlay != null && !string.IsNullOrEmpty(lastPlay.playId))
                    {
                        string pId = lastPlay.playerId.Length > 12 ? lastPlay.playerId.Substring(0, 12) + "..." : lastPlay.playerId;
                        lastPlayText.text = $"Lượt trước: <color=#00FFFF><b>{pId}</b></color> vừa đánh úp <b>{lastPlay.cardCount}</b> lá bài! (Bấm [X] để Bắt Láo)";
                    }
                    else
                    {
                        lastPlayText.text = "<i>(Chưa có ai đánh bài trong vòng này)</i>";
                    }
                }

                if (playCardsButton != null)
                {
                    int selCount = SelectedCardCount;
                    playCardsButton.interactable = IsMyTurn && selCount >= 1 && selCount <= 3;
                    if (playCardsButtonText != null)
                    {
                        playCardsButtonText.text = $"HẠ {selCount} LÁ BÀI [E]";
                    }
                }

                if (callLiarButton != null)
                {
                    callLiarButton.interactable = IsMyTurn && lastPlay != null && !string.IsNullOrEmpty(lastPlay.playId);
                }

                if (hudHintText != null)
                {
                    if (IsMyTurn)
                    {
                        hudHintText.text = "<b>ĐẾN LƯỢT:</b> [A/D]: CHỌN LÁ | [SPACE]: CHỌN/BỎ LÁ (TỐI ĐA 3) | [E]: ĐÁNH BÀI | [X]: BẮT LÁO";
                    }
                    else
                    {
                        hudHintText.text = "<b>NGOÀI LƯỢT:</b> [GIỮ SPACE]: HÉ BÀI XEM | [CHUỘT]: QUAN SÁT | [X]: BẮT LÁO";
                    }
                }
            }
        }
    }

    #endregion

    #region Fallback IMGUI Rendering (Tắt khi enableOnGUI = false)

    private void OnGUI()
    {
        if (!enableOnGUI) return;
        if (string.IsNullOrEmpty(matchId)) return;

        GUI.skin.box.fontSize = 14;
        GUI.skin.button.fontSize = 13;
        GUI.skin.label.fontSize = 13;

        if (showPenaltyModal)
        {
            GUI.color = new Color(0.04f, 0.04f, 0.08f, 0.96f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.color = Color.white;
            RenderRussianRouletteModal();
            return;
        }

        if (!string.IsNullOrEmpty(winnerPlayerId))
        {
            GUI.color = new Color(0.04f, 0.04f, 0.08f, 0.96f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.color = Color.white;
            RenderVictoryModal();
            return;
        }

        RenderTopHeader();
        RenderMiddleArena();
        RenderBottomHand();
    }

    private void RenderTopHeader()
    {
        float width = 680;
        float height = 75;
        float posX = (Screen.width - width) / 2f;
        float posY = 15;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.BeginHorizontal();

        GUILayout.Label($"<b>VÒNG: {roundNumber}</b>\nVer: {stateVersion}", GUILayout.Width(100));

        GUILayout.BeginVertical();
        GUILayout.Label($"<size=17>RANK BÀN: <color=yellow><b>QUÂN {tableRank}</b></color></size>", GetCenterHeaderStyle());
        GUILayout.Label("<i>(Luật: Bắt buộc đánh bài đúng Rank này hoặc Joker)</i>", GetCenterSubHeaderStyle());
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(190));
        if (IsMyTurn)
        {
            GUI.color = Color.green;
            GUILayout.Label("<size=15><b>👉 LƯỢT CỦA BẠN! [E]</b></size>", GetCenterHeaderStyle());
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
        float width = 520;
        float height = 75;
        float posX = (Screen.width - width) / 2f;
        float posY = 98;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.Label("<b>🃏 MẶT BÀN ĐẤU (3D TABLE ARENA)</b>", GetCenterHeaderStyle());

        if (lastPlay != null && !string.IsNullOrEmpty(lastPlay.playId))
        {
            string pId = lastPlay.playerId.Length > 12 ? lastPlay.playerId.Substring(0, 12) + "..." : lastPlay.playerId;
            GUILayout.Label($"Lượt trước: <color=cyan><b>{pId}</b></color> vừa đánh úp <b>{lastPlay.cardCount}</b> lá bài! (Bấm <b>[X]</b> để Bắt Láo)", GetCenterSubHeaderStyle());
        }
        else
        {
            GUILayout.Label("<i>(Chưa có ai đánh bài trong vòng này)</i>", GetCenterSubHeaderStyle());
        }

        GUILayout.EndArea();
    }

    private void RenderBottomHand()
    {
        float width = 720;
        float height = 115;
        float posX = (Screen.width - width) / 2f;
        float posY = Screen.height - height - 12;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);

        GUILayout.Label("<color=yellow><b>[GIỮ SPACE / CHUỘT PHẢI]:</b></color> HÉ BÀI  |  <color=yellow><b>[A / D / CLICK BÀI 3D]:</b></color> CHỌN LÁ  |  <color=yellow><b>[E]:</b></color> HẠ BÀI  |  <color=yellow><b>[X]:</b></color> BẮT LÁO", GetCenterSubHeaderStyle());
        GUILayout.Space(8);

        GUILayout.BeginHorizontal();

        int selCount = SelectedCardCount;
        bool canPlay = IsMyTurn && selCount >= 1 && selCount <= 3;
        GUI.enabled = canPlay;
        GUI.color = canPlay ? new Color(0.2f, 0.9f, 0.3f) : Color.gray;
        if (GUILayout.Button($"<b>HẠ {selCount} LÁ BÀI [E]</b>", GUILayout.Height(48)))
        {
            ActionPlayCards();
        }
        GUI.color = Color.white;
        GUI.enabled = true;

        bool canChallenge = IsMyTurn && lastPlay != null && !string.IsNullOrEmpty(lastPlay.playId);
        GUI.enabled = canChallenge;
        GUI.color = canChallenge ? new Color(1f, 0.45f, 0.2f) : Color.gray;
        if (GUILayout.Button("<b>BẮT LÁO (CALL LIAR) [X]</b>", GUILayout.Height(48)))
        {
            ActionChallenge();
        }
        GUI.color = Color.white;
        GUI.enabled = true;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void RenderRussianRouletteModal()
    {
        float width = 560;
        float height = 300;
        float posX = (Screen.width - width) / 2f;
        float posY = (Screen.height - height) / 2f;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.Space(12);

        GUILayout.Label($"<size=20><b>💀 CÒ QUAY NGA (RUSSIAN ROULETTE) 💀</b></size>", GetCenterHeaderStyle());
        GUILayout.Space(6);
        GUILayout.Label($"<size=16>{penaltyTitle}</size>", GetCenterHeaderStyle());
        GUILayout.Space(12);

        if (awaitingTriggerPull)
        {
            if (isPenalizedPlayer)
            {
                GUI.color = new Color(1f, 0.35f, 0.35f);
                GUILayout.Label("<size=16><b>⚠️ BẠN LÀ NGƯỜI BỊ PHẠT! PHẢI BÓP CÒ SÚNG NGA! ⚠️</b></size>", GetCenterHeaderStyle());
                GUI.color = Color.white;
                GUILayout.Label("<i>(GIỮ CHUỘT TRÁI để gồng lực bóp cò, hoặc bấm nút dưới)</i>", GetCenterSubHeaderStyle());

                GUILayout.Space(12);
                Rect barRect = GUILayoutUtility.GetRect(width - 60, 26);
                barRect.x += 10;
                GUI.Box(barRect, "");
                Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * triggerHoldProgress, barRect.height);
                GUI.color = Color.red;
                GUI.Box(fillRect, triggerHoldProgress > 0.05f ? $"BÓP CÒ... {Mathf.RoundToInt(triggerHoldProgress * 100)}%" : "");
                GUI.color = Color.white;

                GUILayout.Space(10);
                GUI.enabled = (triggerArmCooldown <= 0f);
                GUI.color = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("<b>[🔫 CLICK VÀO ĐÂY ĐỂ BÓP CÒ NGAY]</b>", GUILayout.Height(38)))
                {
                    ExecuteTriggerPull();
                }
                GUI.color = Color.white;
                GUI.enabled = true;
            }
            else
            {
                string victim = pendingChallengeResult != null ? pendingChallengeResult.penalizedPlayerId : "Đối thủ";
                GUI.color = Color.yellow;
                GUILayout.Label($"<size=16><b>🔫 [{victim}] ĐANG ĐẶT SÚNG VÀO ĐẦU VÀ CHUẨN BỊ BÓP CÒ...</b></size>", GetCenterHeaderStyle());
                GUI.color = Color.white;

                GUILayout.Space(15);
                Rect barRect = GUILayoutUtility.GetRect(width - 60, 22);
                barRect.x += 10;
                GUI.Box(barRect, "");
                Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * triggerHoldProgress, barRect.height);
                GUI.color = Color.yellow;
                GUI.Box(fillRect, "Đang chờ đối thủ bóp cò...");
                GUI.color = Color.white;
            }
        }
        else
        {
            GUILayout.Label($"<size=17>{penaltyDetail}</size>", GetCenterHeaderStyle());
            GUILayout.Space(15);
            GUILayout.Label($"<i>Tự động chuyển tiếp sau: {Mathf.CeilToInt(penaltyModalTimer)}s...</i>", GetCenterSubHeaderStyle());
        }

        GUILayout.EndArea();
    }

    private void RenderVictoryModal()
    {
        float width = 560;
        float height = 300;
        float posX = (Screen.width - width) / 2f;
        float posY = (Screen.height - height) / 2f;

        GUILayout.BeginArea(new Rect(posX, posY, width, height), GUI.skin.box);
        GUILayout.Space(15);

        string myId = TableSessionManager.Instance != null ? TableSessionManager.Instance.MyPlayerId : null;
        bool isMeWinner = (!string.IsNullOrEmpty(myId) && string.Equals(winnerPlayerId, myId, StringComparison.OrdinalIgnoreCase))
                          || string.Equals(winnerPlayerId, "Bạn", StringComparison.OrdinalIgnoreCase);

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
        GUI.color = new Color(0.2f, 0.85f, 0.95f);
        if (GUILayout.Button("<b>QUAY VỀ PHÒNG CHỜ BÀN (SẴN SÀNG VÁN MỚI)</b>", GUILayout.Height(40)))
        {
            ActionReturnToLobby();
        }

        GUILayout.Space(8);
        GUI.color = new Color(0.85f, 0.4f, 0.4f);
        if (GUILayout.Button("<b>RỜI BÀN (ĐỨNG DẬY - ESC)</b>", GUILayout.Height(36)))
        {
            ActionLeaveAfterMatch();
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

    #endregion
}
