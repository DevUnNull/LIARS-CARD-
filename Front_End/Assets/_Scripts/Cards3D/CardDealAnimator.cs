using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Animation chia bài đầu ván: từ chồng bài trên bàn, lần lượt bay từng lá về phía ghế của mỗi người đang ngồi
    /// (hướng lấy từ vị trí ghế thật), rồi trượt về phía người chơi và biến mất. Bài trên tay chỉ hiện sau khi chia xong.
    /// </summary>
    public class CardDealAnimator : MonoBehaviour
    {
        public static CardDealAnimator Instance { get; private set; }

        public const int CardsPerPlayer = 5;   // khớp DeckRoundService.cardsPerPlayer ở backend

        [Header("Timing")]
        public float flyDuration = 0.35f;
        public float interval = 0.09f;

        [Header("Layout")]
        public float landRadius = 1.1f;        // khoảng cách từ tâm bàn tới chỗ đặt bài (mép bàn cách tâm 1.5m)
        public float cardSpread = 0.09f;       // độ xòe giữa các lá của 1 người (bài trên bàn to gấp đôi)

        public bool IsDealing { get; private set; }

        private readonly List<Action> waiting = new List<Action>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);
        }

        /// <summary>Chạy action ngay nếu không đang chia bài, ngược lại chờ chia xong.</summary>
        public static void WhenIdle(Action action)
        {
            if (Instance == null || !Instance.IsDealing) action();
            else Instance.waiting.Add(action);
        }

        /// <summary>Chia bài cho mọi người đang ngồi tại bàn của mình.</summary>
        public void PlayDeal()
        {
            if (IsDealing) return;
            StartCoroutine(DealRoutine());
        }

        private IEnumerator DealRoutine()
        {
            IsDealing = true;

            var seats = GetSeatDirections();
            if (seats.Count > 0)
            {
                Vector3 center = TableCenterPile3D.GetTableTopCenter();
                Vector3 start = TableDeck3D.Instance != null ? TableDeck3D.Instance.transform.position : center;
                if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlayShuffle();

                var landed = new List<Transform>();
                int flying = 0;

                for (int round = 0; round < CardsPerPlayer; round++)
                {
                    foreach (var seat in seats)
                    {
                        var card = CreateCardBack(start);
                        landed.Add(card.transform);

                        Vector3 dir = seat.dir;
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        float offset = (round - (CardsPerPlayer - 1) * 0.5f) * cardSpread;
                        Vector3 target = center + dir * landRadius + side * offset + Vector3.up * (0.004f + round * 0.002f);
                        Quaternion rot = Quaternion.Euler(90f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);

                        flying++;
                        StartCoroutine(Fly(card.transform, target, rot, flyDuration, 0.15f, () =>
                        {
                            flying--;
                            if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlaySnap();
                        }));
                        yield return new WaitForSeconds(interval);
                    }
                }

                yield return new WaitUntil(() => flying <= 0);
                yield return new WaitForSeconds(0.3f);

                // Mỗi người "nhặt" bài: trượt về phía ghế rồi biến mất
                int sliding = 0;
                for (int i = 0; i < landed.Count; i++)
                {
                    var t = landed[i];
                    if (t == null) continue;
                    var seat = seats[i % seats.Count];
                    Vector3 away = t.position + seat.dir * 0.9f + Vector3.down * 0.15f;
                    sliding++;
                    StartCoroutine(Fly(t, away, t.rotation, 0.3f, 0f, () => sliding--));
                }
                yield return new WaitUntil(() => sliding <= 0);

                foreach (var t in landed)
                    if (t != null) Destroy(t.gameObject);
            }

            IsDealing = false;

            var pending = new List<Action>(waiting);
            waiting.Clear();
            foreach (var a in pending) a();
        }

        private struct SeatDir
        {
            public int seatIndex;
            public Vector3 dir;   // hướng ngang từ tâm bàn tới ghế
        }

        // Lấy các ghế đang có người của bàn mình; hướng chia = hướng từ tâm bàn tới ghế đó trong scene.
        private List<SeatDir> GetSeatDirections()
        {
            var result = new List<SeatDir>();
            var session = TableSessionManager.Instance;
            if (session == null || session.currentTableState == null) return result;

            Vector3 center = TableCenterPile3D.GetTableTopCenter();
            var chairs = FindObjectsByType<ChairItem>(FindObjectsSortMode.None);

            foreach (var seat in session.currentTableState.seats)
            {
                if (string.IsNullOrEmpty(seat.playerId)) continue;

                foreach (var chair in chairs)
                {
                    if (chair.TableId != session.currentTableId || chair.SitIndex != seat.seatIndex) continue;

                    Vector3 d = chair.transform.position - center;
                    d.y = 0f;
                    if (d.sqrMagnitude < 0.0001f) break;
                    result.Add(new SeatDir { seatIndex = seat.seatIndex, dir = d.normalized });
                    break;
                }
            }

            result.Sort((a, b) => a.seatIndex.CompareTo(b.seatIndex));
            return result;
        }

        private static GameObject CreateCardBack(Vector3 position)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "DealCard";
            Destroy(q.GetComponent<Collider>());
            q.transform.position = position;
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            float s = TableCenterPile3D.TableCardScale;
            q.transform.localScale = new Vector3(CardMeshBuilder.CARD_WIDTH * s, CardMeshBuilder.CARD_HEIGHT * s, 1f);

            var r = q.GetComponent<MeshRenderer>();
            r.material = CardTextureGenerator.GetCardBackMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q;
        }

        private static IEnumerator Fly(Transform t, Vector3 to, Quaternion toRot, float duration, float arc, Action onDone)
        {
            Vector3 from = t.position;
            Quaternion fromRot = t.rotation;
            float elapsed = 0f;

            while (elapsed < duration && t != null)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * arc;
                t.SetPositionAndRotation(p, Quaternion.Slerp(fromRot, toRot, k));
                yield return null;
            }

            if (t != null) t.SetPositionAndRotation(to, toRot);
            onDone?.Invoke();
        }
    }
}
