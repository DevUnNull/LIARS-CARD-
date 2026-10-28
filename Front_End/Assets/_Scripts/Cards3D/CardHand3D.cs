using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LiarsCard.Network;

namespace LiarsCard.Cards3D
{
    public enum HandVisualState
    {
        Resting,    // Úp bài xuống cạnh bàn / ngực (Face-down)
        Peeking     // Nhấc bài lên trước mặt camera xem số (Face-up)
    }

    /// <summary>
    /// Base interface for Card Hand Input Context / State Pattern
    /// </summary>
    public interface IHandInputState
    {
        void Enter(CardHand3D hand);
        void Update(CardHand3D hand);
        void Exit(CardHand3D hand);
    }

    /// <summary>
    /// Trạng thái NGOÀI LƯỢT (Not My Turn / IdleState):
    /// - Mặc định: Bài úp xuống cạnh bàn / hạ thấp khỏi tầm nhìn (Idle/Resting).
    /// - Giữ Space: Nhấc bài lên xem (Peek). Nhả Space: Tự động hạ úp bài xuống.
    /// - Không cho phép chọn bài hay đánh bài.
    /// </summary>
    public class IdleHandState : IHandInputState
    {
        public void Enter(CardHand3D hand)
        {
            hand.SetVisualState(HandVisualState.Resting);
        }

        public void Update(CardHand3D hand)
        {
            // Kiểm tra nếu tới lượt của mình -> Tự động chuyển sang TurnActionState
            if (CardBattleUI.Instance != null && CardBattleUI.Instance.IsMyTurn)
            {
                hand.TransitionToState(hand.turnActionState);
                return;
            }

            // Giữ phím Space để xem bài (Peek), khi thả Space ra thì tự động hạ/úp bài xuống vị trí cũ
            bool isHoldingSpace = hand.IsSpaceHeld();
            if (isHoldingSpace)
            {
                if (hand.visualState != HandVisualState.Peeking)
                {
                    hand.SetVisualState(HandVisualState.Peeking);
                }
            }
            else
            {
                if (hand.visualState != HandVisualState.Resting)
                {
                    hand.SetVisualState(HandVisualState.Resting);
                }
            }
        }

        public void Exit(CardHand3D hand)
        {
        }
    }

    /// <summary>
    /// Trạng thái ĐẾN LƯỢT (My Turn / TurnActionState):
    /// - Bắt đầu lượt: Tự động chạy animation nhấc bài lên trước mặt (không cần giữ Space).
    /// - Phím A / D (hoặc mũi tên Trái / Phải): Chuyển đổi giữa các quân bài (lá đang trỏ nhô nhẹ + đổi viền).
    /// - Nhấn Space (Tap - GetKeyDown): Chọn (Select) hoặc Bỏ chọn (Deselect) lá đang trỏ tới (tối đa 3 lá).
    /// - Nhấn E: Đánh (hạ) các lá đã chọn ra giữa bàn. Sau đó kết thúc lượt và hạ bài về Resting.
    /// </summary>
    public class TurnActionHandState : IHandInputState
    {
        public void Enter(CardHand3D hand)
        {
            // Tự động nhấc bài lên trước mặt khi đến lượt mà không cần giữ Space
            hand.SetVisualState(HandVisualState.Peeking);

            // Đảm bảo focus index hợp lệ
            if (hand.cardsInHand.Count > 0)
            {
                hand.focusedIndex = Mathf.Clamp(hand.focusedIndex, 0, hand.cardsInHand.Count - 1);
            }
        }

        public void Update(CardHand3D hand)
        {
            // Nếu không còn là lượt của mình (ví dụ lượt kết thúc hoặc bị timeout) -> chuyển về IdleHandState
            if (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsMyTurn)
            {
                hand.TransitionToState(hand.idleState);
                return;
            }

            // Luôn duy trì tư thế nhấc bài khi trong lượt
            if (hand.visualState != HandVisualState.Peeking)
            {
                hand.SetVisualState(HandVisualState.Peeking);
            }

            // 1. Điều hướng bài: A / Left Arrow -> Sang trái
            if (hand.IsNavLeftPressed())
            {
                hand.FocusPreviousCard();
            }
            // D / Right Arrow -> Sang phải
            else if (hand.IsNavRightPressed())
            {
                hand.FocusNextCard();
            }

            // 2. Chọn bài: Nhấn Space (GetKeyDown) để Chọn / Hủy chọn lá bài đang trỏ tới (Tối đa 3 lá)
            if (hand.IsSpaceDown())
            {
                hand.ToggleSelectCard(hand.focusedIndex);
            }

            // 3. Hạ bài: Nhấn phím E để đánh các lá đã chọn ra giữa bàn
            if (hand.IsEPressed())
            {
                if (CardBattleUI.Instance != null)
                {
                    CardBattleUI.Instance.ActionPlayCards();
                }
            }

            // 4. Bắt láo: Nhấn phím X
            if (hand.IsXPressed())
            {
                if (CardBattleUI.Instance != null)
                {
                    CardBattleUI.Instance.ActionChallenge();
                }
            }
        }

        public void Exit(CardHand3D hand)
        {
            // Sau khi hết lượt: tự động hạ tay xuống lại trạng thái mặc định
            hand.SetVisualState(HandVisualState.Resting);
        }
    }

    /// <summary>
    /// Trạng thái Đang ném bài: Khóa input trong khi các lá bài đang bay ra giữa bàn
    /// </summary>
    public class ThrowingHandState : IHandInputState
    {
        public void Enter(CardHand3D hand) { }
        public void Update(CardHand3D hand) { }
        public void Exit(CardHand3D hand) { }
    }

    /// <summary>
    /// Immersive First-Person 3D Card Hand Controller (Liar's Bar style).
    /// Pure keyboard control: Mouse is strictly reserved for Camera rotation.
    /// Manages Input Context / State Pattern (IdleState & TurnActionState),
    /// Fan layout math, and throw animations.
    /// </summary>
    public class CardHand3D : MonoBehaviour
    {
        public static CardHand3D Instance { get; private set; }

        [Header("State Pattern")]
        public IHandInputState currentState { get; private set; }
        public readonly IdleHandState idleState = new IdleHandState();
        public readonly TurnActionHandState turnActionState = new TurnActionHandState();
        public readonly ThrowingHandState throwingState = new ThrowingHandState();

        [Header("Visual State")]
        public HandVisualState visualState = HandVisualState.Resting;
        public bool isPeeking => visualState == HandVisualState.Peeking;

        [Header("Cards In Hand")]
        public List<CardVisual3D> cardsInHand = new List<CardVisual3D>();
        public int focusedIndex = 0;

        [Header("Anchor Positions (Relative to Player Camera)")]
        // Vị trí cụm bài khi Hé bài trước tầm mắt
        public Vector3 peekAnchorPos = new Vector3(0f, -0.16f, 0.38f);
        // Vị trí cụm bài khi Úp nghỉ ngang ngực / cạnh bàn
        public Vector3 restAnchorPos = new Vector3(0.04f, -0.32f, 0.44f);

        [Header("Fan Layout Configuration")]
        public float angleStep = 6.0f;
        public float xOffsetStep = 0.045f;

        private Transform handAnchor;
        private Camera targetCamera;
        private Coroutine throwCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (FindAnyObjectByType<CardAudioSystem>() == null)
            {
                var audioObj = new GameObject("[CardAudioSystem]");
                audioObj.AddComponent<CardAudioSystem>();
            }

            if (FindAnyObjectByType<TableCenterPile3D>() == null)
            {
                var pileObj = new GameObject("[TableCenterPile3D]");
                pileObj.AddComponent<TableCenterPile3D>();
            }

            SetupHandAnchor();

            // Khởi tạo trạng thái ban đầu là IdleHandState
            TransitionToState(idleState);
        }

        public void TransitionToState(IHandInputState newState)
        {
            if (currentState == newState) return;
            currentState?.Exit(this);
            currentState = newState;
            currentState?.Enter(this);
        }

        public void SetupHandAnchor()
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                var playerCamObj = GameObject.Find("PlayerCamera");
                if (playerCamObj != null) targetCamera = playerCamObj.GetComponent<Camera>();
            }

            if (targetCamera != null)
            {
                var existingAnchor = targetCamera.transform.Find("FPS_HandAnchor");
                if (existingAnchor != null)
                {
                    handAnchor = existingAnchor;
                }
                else
                {
                    var anchorObj = new GameObject("FPS_HandAnchor");
                    anchorObj.transform.SetParent(targetCamera.transform, false);
                    handAnchor = anchorObj.transform;
                }

                handAnchor.localPosition = restAnchorPos;
                handAnchor.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (handAnchor == null || targetCamera == null)
            {
                SetupHandAnchor();
                if (handAnchor == null) return;
            }

            // Chỉ xử lý tương tác khi đang trong trận đấu
            if (CardBattleUI.Instance == null || !CardBattleUI.Instance.IsInBattle)
            {
                if (cardsInHand.Count > 0) ClearHand();
                return;
            }

            // 1. Luôn ẩn và khóa chuột trong trận đấu: Chuột chỉ dùng thuần túy để xoay camera/nhân vật (Mouse Look)
            UpdateCursorLock();

            // 2. Thực thi State Pattern (IdleState hoặc TurnActionState)
            currentState?.Update(this);

            // 3. Cập nhật vị trí & xoay của Hand Anchor theo visualState
            UpdateHandAnchorTransform();

            // 4. Cập nhật nan quạt (Fan layout) cho các lá bài
            UpdateCardFanLayout();
        }

        private void UpdateCursorLock()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void SetVisualState(HandVisualState newVisualState)
        {
            if (visualState == newVisualState) return;
            visualState = newVisualState;

            if (visualState == HandVisualState.Peeking)
            {
                if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlayShuffle();
            }
            else if (visualState == HandVisualState.Resting)
            {
                if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlaySnap();
            }
        }

        private void UpdateHandAnchorTransform()
        {
            if (handAnchor == null) return;

            Vector3 targetPos = (visualState == HandVisualState.Peeking) ? peekAnchorPos : restAnchorPos;
            handAnchor.localPosition = Vector3.Lerp(handAnchor.localPosition, targetPos, Time.deltaTime * 10f);
        }

        private void UpdateCardFanLayout()
        {
            int count = cardsInHand.Count;
            if (count == 0) return;

            float startAngle = -((count - 1) * angleStep) / 2f;
            float startX = -((count - 1) * xOffsetStep) / 2f;
            float centerIdx = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var card = cardsInHand[i];
                if (card == null) continue;

                float angle = startAngle + i * angleStep;
                float posX = startX + i * xOffsetStep;

                // Vòng cung tự nhiên
                float arcY = -Mathf.Abs(i - centerIdx) * 0.006f;

                // Độ nhô cao:
                // - Lá đang trỏ vào (focused): Nhô nhẹ +1.8 cm kèm viền xanh ngọc
                // - Lá đã chọn (selected): Nhô cao +3.8 cm kèm viền vàng kim
                float selectY = 0f;
                bool isCardFocused = (i == focusedIndex && (currentState is TurnActionHandState || visualState == HandVisualState.Peeking));
                if (card.isSelected) selectY += 0.038f;
                if (isCardFocused) selectY += 0.018f;

                float posZ = i * 0.0035f;

                Vector3 targetPos = new Vector3(posX, (visualState == HandVisualState.Peeking ? 0f : -0.08f) + arcY + selectY, posZ);
                Quaternion targetRot = Quaternion.Euler(visualState == HandVisualState.Peeking ? 12f : 52f, 0f, -angle);

                card.targetLocalPosition = targetPos;
                card.targetLocalRotation = targetRot;

                card.SetFocused(isCardFocused);
            }
        }

        #region Card Focus & Selection

        public void FocusPreviousCard()
        {
            if (cardsInHand.Count == 0) return;
            focusedIndex = (focusedIndex - 1 + cardsInHand.Count) % cardsInHand.Count;
            if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlaySnap();
        }

        public void FocusNextCard()
        {
            if (cardsInHand.Count == 0) return;
            focusedIndex = (focusedIndex + 1) % cardsInHand.Count;
            if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlaySnap();
        }

        public void ToggleSelectCard(int index)
        {
            if (index < 0 || index >= cardsInHand.Count) return;

            var card = cardsInHand[index];
            bool newSelect = !card.isSelected;

            // Giới hạn chọn tối đa 3 lá
            if (newSelect && GetSelectedCount() >= 3)
            {
                return;
            }

            card.SetSelected(newSelect);
            if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlaySnap();

            SyncSelectionToUI();
        }

        public int GetSelectedCount()
        {
            int c = 0;
            foreach (var card in cardsInHand)
            {
                if (card != null && card.isSelected) c++;
            }
            return c;
        }

        private void SyncSelectionToUI()
        {
            if (CardBattleUI.Instance == null) return;

            for (int i = 0; i < cardsInHand.Count && i < CardBattleUI.Instance.selectedCards.Count; i++)
            {
                CardBattleUI.Instance.selectedCards[i] = cardsInHand[i].isSelected;
            }
        }

        #endregion

        #region Hand Synchronization & Throw Physics

        public void SyncHand(List<CardDto> newCards)
        {
            if (newCards == null) return;

            ClearHand();

            for (int i = 0; i < newCards.Count; i++)
            {
                var cardDto = newCards[i];
                var cardObj = CardMeshBuilder.CreateCardObject(cardDto.cardInstanceId, cardDto.rank);
                cardObj.transform.SetParent(handAnchor, false);

                var visual = cardObj.GetComponent<CardVisual3D>();
                cardsInHand.Add(visual);
            }

            focusedIndex = Mathf.Clamp(focusedIndex, 0, Mathf.Max(0, cardsInHand.Count - 1));
        }

        public void ClearHand()
        {
            foreach (var card in cardsInHand)
            {
                if (card != null) Destroy(card.gameObject);
            }
            cardsInHand.Clear();
            focusedIndex = 0;
        }

        public void AnimateThrowSelectedCards(Action onComplete)
        {
            if (throwCoroutine != null) StopCoroutine(throwCoroutine);
            TransitionToState(throwingState);
            throwCoroutine = StartCoroutine(ThrowRoutine(onComplete));
        }

        private IEnumerator ThrowRoutine(Action onComplete)
        {
            var thrownCards = new List<CardVisual3D>();
            for (int i = 0; i < cardsInHand.Count; i++)
            {
                if (cardsInHand[i].isSelected)
                {
                    thrownCards.Add(cardsInHand[i]);
                }
            }

            var pile = TableCenterPile3D.Instance;
            if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlayThrow();

            int completedCount = 0;
            for (int i = 0; i < thrownCards.Count; i++)
            {
                var card = thrownCards[i];
                Vector3 targetPilePos = pile != null ? pile.GetNextStackPosition() : new Vector3(-1.75f, 1.51f, -5f);
                Quaternion targetPileRot = pile != null ? pile.GetNextStackRotation() : Quaternion.Euler(90f, 0, 0);

                float delay = i * 0.08f;
                StartCoroutine(DelayedThrow(card, targetPilePos, targetPileRot, delay, () =>
                {
                    if (pile != null) pile.AddCardToPile(card.gameObject);
                    completedCount++;
                }));
            }

            yield return new WaitUntil(() => completedCount >= thrownCards.Count);

            // Dọn các lá đã đánh khỏi hand
            cardsInHand.RemoveAll(c => thrownCards.Contains(c));
            focusedIndex = Mathf.Clamp(focusedIndex, 0, Mathf.Max(0, cardsInHand.Count - 1));

            // Quay về IdleHandState sau khi đánh xong
            TransitionToState(idleState);

            onComplete?.Invoke();
        }

        private IEnumerator DelayedThrow(CardVisual3D card, Vector3 targetPos, Quaternion targetRot, float delay, Action onArrived)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            card.AnimateFlyToWorld(targetPos, targetRot, 0.42f, onArrived);
        }

        #endregion

        #region Input Helper Checks (Keyboard & Legacy Fallback)

        public bool IsSpaceHeld()
        {
            var kb = Keyboard.current;
            if (kb != null) return kb.spaceKey.isPressed;
            try { return Input.GetKey(KeyCode.Space); } catch { return false; }
        }

        public bool IsSpaceDown()
        {
            var kb = Keyboard.current;
            if (kb != null) return kb.spaceKey.wasPressedThisFrame;
            try { return Input.GetKeyDown(KeyCode.Space); } catch { return false; }
        }

        public bool IsNavLeftPressed()
        {
            var kb = Keyboard.current;
            if (kb != null) return kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
            try { return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow); } catch { return false; }
        }

        public bool IsNavRightPressed()
        {
            var kb = Keyboard.current;
            if (kb != null) return kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
            try { return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow); } catch { return false; }
        }

        public bool IsEPressed()
        {
            var kb = Keyboard.current;
            if (kb != null) return kb.eKey.wasPressedThisFrame;
            try { return Input.GetKeyDown(KeyCode.E); } catch { return false; }
        }

        public bool IsXPressed()
        {
            var kb = Keyboard.current;
            if (kb != null) return kb.xKey.wasPressedThisFrame;
            try { return Input.GetKeyDown(KeyCode.X); } catch { return false; }
        }

        #endregion
    }
}
