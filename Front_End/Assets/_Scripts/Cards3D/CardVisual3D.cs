using System;
using System.Collections;
using UnityEngine;

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Visual and state controller for individual 3D cards in the player's hand or on table.
    /// Handles card pop-up on selection/focus, border aura, and parabolic flying animation.
    /// Mouse clicking is disabled to keep mouse strictly for FPP camera look.
    /// </summary>
    public class CardVisual3D : MonoBehaviour
    {
        public string cardInstanceId { get; private set; }
        public string rank { get; private set; }

        public bool isSelected { get; private set; }
        public bool isFocused { get; private set; }

        private MeshRenderer frontRenderer;
        private MeshRenderer backRenderer;
        private GameObject highlightQuad;
        private Material highlightMat;

        // Target transform in hand space
        public Vector3 targetLocalPosition { get; set; }
        public Quaternion targetLocalRotation { get; set; }

        private bool isFlying = false;

        public void Initialize(string instanceId, string cardRank, MeshRenderer front, MeshRenderer back, GameObject highlight)
        {
            cardInstanceId = instanceId;
            rank = cardRank;
            frontRenderer = front;
            backRenderer = back;
            highlightQuad = highlight;

            if (highlightQuad != null)
            {
                var r = highlightQuad.GetComponent<MeshRenderer>();
                if (r != null)
                {
                    // Create an instance material so we can adjust colors independently
                    highlightMat = new Material(r.sharedMaterial);
                    r.material = highlightMat;
                }
            }
        }

        public void SetRank(string newRank)
        {
            rank = newRank;
            if (frontRenderer != null)
            {
                frontRenderer.material = CardTextureGenerator.GetCardFrontMaterial(rank);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            UpdateHighlightVisual();
        }

        public void SetFocused(bool focused)
        {
            isFocused = focused;
            UpdateHighlightVisual();
        }

        private void UpdateHighlightVisual()
        {
            if (highlightQuad != null)
            {
                bool active = isSelected || isFocused;
                highlightQuad.SetActive(active);

                if (active && highlightMat != null)
                {
                    // Đã chọn: Viền vàng kim rực rỡ
                    // Đang focus trỏ tới (chưa chọn): Viền xanh ngọc dịu
                    if (isSelected)
                    {
                        highlightMat.color = new Color(1f, 0.85f, 0.2f, 0.95f);
                    }
                    else if (isFocused)
                    {
                        highlightMat.color = new Color(0.2f, 0.85f, 1f, 0.8f);
                    }
                }
            }
        }

        private void Update()
        {
            if (isFlying) return;

            // Di chuyển mượt mà về vị trí & góc xoay đích
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * 14f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, Time.deltaTime * 14f);
        }

        /// <summary>
        /// Animates the card flying along a parabolic arc toward the table center pile.
        /// </summary>
        public void AnimateFlyToWorld(Vector3 targetWorldPos, Quaternion targetWorldRot, float duration, Action onArrival)
        {
            StartCoroutine(FlyRoutine(targetWorldPos, targetWorldRot, duration, onArrival));
        }

        private IEnumerator FlyRoutine(Vector3 targetWorldPos, Quaternion targetWorldRot, float duration, Action onArrival)
        {
            isFlying = true;
            transform.SetParent(null, true); // Detach from hand anchor

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth ease-out curve
                float easeT = Mathf.SmoothStep(0f, 1f, t);

                // Parabolic arc (lifts slightly in the air before landing on felt)
                float arcHeight = Mathf.Sin(t * Mathf.PI) * 0.18f;
                Vector3 currentPos = Vector3.Lerp(startPos, targetWorldPos, easeT);
                currentPos.y += arcHeight;

                transform.position = currentPos;
                transform.rotation = Quaternion.Slerp(startRot, targetWorldRot, easeT);

                yield return null;
            }

            transform.position = targetWorldPos;
            transform.rotation = targetWorldRot;
            isFlying = false;

            onArrival?.Invoke();
        }
    }
}
