using System.Collections.Generic;
using UnityEngine;
using LiarsCard.Network;
using TMPro;

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Bài trên tay của những người chơi KHÁC: mọi người nhìn thấy họ cầm bài, nhấc bài lên xem (Space)
    /// và quăng bài vào chồng bài giữa bàn. Chỉ là hiệu ứng hiển thị; số lá lấy từ trạng thái trận,
    /// hành động nhấc/đánh bài do server chuyển tiếp (gói 14).
    /// </summary>
    public class RemoteHands : MonoBehaviour
    {
        private static RemoteHands instance;

        public static RemoteHands Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[RemoteHands]");
                    instance = go.AddComponent<RemoteHands>();
                }
                return instance;
            }
        }

        // Vị trí cụm bài so với gốc nhân vật (gốc + camera cao 0.6m), cùng số với CardHand3D
        private static readonly Vector3 PeekOffset = new Vector3(0f, 0.44f, 0.38f);
        private static readonly Vector3 RestOffset = new Vector3(0.04f, 0.28f, 0.44f);

        private class Hand
        {
            public Transform anchor;
            public readonly List<CardVisual3D> cards = new List<CardVisual3D>();
            public bool peek;
            public int count;
            public Transform avatar;
        }

        private readonly Dictionary<string, Hand> hands = new Dictionary<string, Hand>();

        // Nhãn "X/6" luôn nổi trên đầu mỗi người chơi còn sống
        private class ShotLabel
        {
            public TextMeshPro text;
            public int shot = 1;
            public bool alive = true;
            public bool fired; // chỉ hiện nhãn sau khi người này bắn lần đầu
            public float flashUntil;
            public int flashShot;
            public bool flashHit;
        }
        private readonly Dictionary<string, ShotLabel> labels = new Dictionary<string, ShotLabel>();
        private string myIdCached = "";

        /// <summary>Bắn xong: nhãn hiện đúng phát vừa bóp (đỏ nếu trúng) trong `seconds` giây rồi trở lại số phát kế tiếp.</summary>
        public void FlashShot(string playerId, int shot, bool hit, float seconds)
        {
            if (string.IsNullOrEmpty(playerId) || shot < 1) return;
            var l = GetLabel(playerId);
            l.fired = true;
            l.flashShot = shot;
            l.flashHit = hit;
            l.flashUntil = Time.time + seconds;
        }

        private ShotLabel GetLabel(string playerId)
        {
            if (!labels.TryGetValue(playerId, out var l))
            {
                var go = new GameObject("ShotLabel_" + playerId);
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshPro>();
                t.alignment = TextAlignmentOptions.Center;
                t.fontSize = 2.2f;
                t.fontStyle = FontStyles.Bold;
                t.outlineWidth = 0.2f;
                t.outlineColor = Color.black;
                l = new ShotLabel { text = t };
                labels[playerId] = l;
            }
            return l;
        }

        /// <summary>Số phát "kế tiếp" của mỗi người (kể cả mình) từ trạng thái trận.</summary>
        public void SyncShots(List<PublicPlayerStateDto> players, string myId)
        {
            if (players == null) return;
            myIdCached = myId ?? "";
            foreach (var p in players)
            {
                if (string.IsNullOrEmpty(p.playerId)) continue;
                var l = GetLabel(p.playerId);
                l.shot = Mathf.Clamp(p.shotCount, 1, 6);
                l.alive = p.isAlive;
                if (RemotePlayerManager.Instance != null && RemotePlayerManager.Instance.TryGetRemote(p.playerId, out var avatar))
                    if (avatar.TryGetComponent<CharacterAnimator>(out var anim)) anim.SetAlive(p.isAlive);
                if (p.shotCount > 1) l.fired = true;
            }
        }

        /// <summary>Số phát kế tiếp của chính mình (hiện ở HUD vì không tự thấy chữ trên đầu).</summary>
        public int GetMyShot() => labels.TryGetValue(myIdCached, out var l) && l.fired ? l.shot : 0;

        private void UpdateLabels(bool inBattle)
        {
            var remotes = RemotePlayerManager.Instance;
            var cam = Camera.main;
            foreach (var kv in labels)
            {
                var l = kv.Value;
                Transform avatar = null;
                bool show = inBattle && l.alive && l.fired && remotes != null && cam != null && remotes.TryGetRemote(kv.Key, out avatar);
                if (!show) { l.text.gameObject.SetActive(false); continue; }

                bool flashing = Time.time < l.flashUntil;
                int n = flashing ? l.flashShot : l.shot;
                l.text.text = n + "/6";
                l.text.color = flashing ? (l.flashHit ? new Color(1f, 0.25f, 0.2f) : Color.white) : new Color(1f, 0.9f, 0.5f);

                Vector3 pos = avatar.position + Vector3.up * 1.15f;
                l.text.transform.position = pos;
                l.text.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);
                l.text.gameObject.SetActive(true);
            }
        }

        /// <summary>Đồng bộ số lá của từng người (từ trạng thái trận) – bỏ qua chính mình và người đã chết.</summary>
        public void SyncCounts(List<PublicPlayerStateDto> players, string myId)
        {
            if (players == null) return;

            foreach (var p in players)
            {
                if (string.IsNullOrEmpty(p.playerId) || string.Equals(p.playerId, myId, System.StringComparison.OrdinalIgnoreCase)) continue;
                GetHand(p.playerId).count = p.isAlive ? Mathf.Max(0, p.remainingCardCount) : 0;
            }
        }

        public void SetPeek(string playerId, bool peek)
        {
            var h = GetHand(playerId);
            if (h.peek == peek) return;
            h.peek = peek;
            if (CardAudioSystem.Instance != null)
            {
                if (peek) CardAudioSystem.Instance.PlayShuffle(); else CardAudioSystem.Instance.PlaySnap();
            }
        }

        /// <summary>Người chơi đánh `count` lá: các lá bay từ tay họ vào chồng bài giữa bàn.</summary>
        public void PlayCards(string playerId, int count)
        {
            var h = GetHand(playerId);
            var pile = TableCenterPile3D.Instance;
            if (pile == null || count < 1) return;

            h.peek = false;
            RefreshCards(h, Mathf.Max(h.count, count));

            if (CardAudioSystem.Instance != null) CardAudioSystem.Instance.PlayThrow();

            for (int i = 0; i < count && h.cards.Count > 0; i++)
            {
                var card = h.cards[h.cards.Count - 1];
                h.cards.RemoveAt(h.cards.Count - 1);
                h.count = Mathf.Max(0, h.count - 1);
                if (card == null) continue;

                Vector3 pos = pile.GetNextStackPosition();
                Quaternion rot = pile.GetNextStackRotation();
                StartCoroutine(DelayedFly(card, pos, rot, i * 0.08f, pile));
            }
        }

        public void Clear()
        {
            foreach (var h in hands.Values)
            {
                foreach (var c in h.cards) if (c != null) Destroy(c.gameObject);
                if (h.anchor != null) Destroy(h.anchor.gameObject);
            }
            hands.Clear();
            foreach (var l in labels.Values) if (l.text != null) Destroy(l.text.gameObject);
            labels.Clear();
        }

        private System.Collections.IEnumerator DelayedFly(CardVisual3D card, Vector3 pos, Quaternion rot, float delay, TableCenterPile3D pile)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (card == null) yield break;
            card.AnimateFlyToWorld(pos, rot, 0.42f, () => { if (card != null) pile.AddCardToPile(card.gameObject); }, TableCenterPile3D.TableCardScale);
        }

        private Hand GetHand(string playerId)
        {
            if (!hands.TryGetValue(playerId, out var h))
            {
                var anchor = new GameObject("RemoteHand_" + playerId).transform;
                anchor.SetParent(transform, false);
                h = new Hand { anchor = anchor };
                hands[playerId] = h;
            }
            return h;
        }

        private static void RefreshCards(Hand h, int wanted)
        {
            h.cards.RemoveAll(c => c == null);
            while (h.cards.Count < wanted)
            {
                var obj = CardMeshBuilder.CreateCardObject("remote", "A");
                obj.transform.SetParent(h.anchor, false);
                var v = obj.GetComponent<CardVisual3D>();
                v.targetLocalPosition = Vector3.zero;
                v.targetLocalRotation = Quaternion.identity;
                h.cards.Add(v);
            }
            while (h.cards.Count > wanted)
            {
                var c = h.cards[h.cards.Count - 1];
                h.cards.RemoveAt(h.cards.Count - 1);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Update()
        {
            var battle = CardBattleUI.Instance;
            bool inBattle = battle != null && battle.IsInBattle;
            var remotes = RemotePlayerManager.Instance;
            UpdateLabels(inBattle);

            foreach (var kv in hands)
            {
                var h = kv.Value;
                bool visible = inBattle && remotes != null && remotes.TryGetRemote(kv.Key, out h.avatar) && h.count > 0;
                if (!visible)
                {
                    if (h.cards.Count > 0) RefreshCards(h, 0);
                    continue;
                }

                // Đang có animation bay thì giữ nguyên các lá còn lại, chỉ đồng bộ số lá khi đứng yên
                RefreshCards(h, h.count);

                // Anchor đi theo nhân vật (chỉ xoay theo hướng nhìn ngang)
                Quaternion yaw = Quaternion.Euler(0f, h.avatar.eulerAngles.y, 0f);
                Vector3 off = h.peek ? PeekOffset : RestOffset;
                Vector3 target = h.avatar.position + yaw * off;
                h.anchor.position = Vector3.Lerp(h.anchor.position == Vector3.zero ? target : h.anchor.position, target, Time.deltaTime * 10f);
                h.anchor.rotation = yaw;

                // Xếp quạt giống bài của mình
                int n = h.cards.Count;
                float startAngle = -((n - 1) * 6f) / 2f;
                float startX = -((n - 1) * 0.045f) / 2f;
                for (int i = 0; i < n; i++)
                {
                    var c = h.cards[i];
                    if (c == null) continue;
                    float angle = startAngle + i * 6f;
                    c.targetLocalPosition = new Vector3(startX + i * 0.045f, h.peek ? 0f : -0.08f, i * 0.0035f);
                    c.targetLocalRotation = Quaternion.Euler(h.peek ? 12f : 52f, 0f, -angle);
                }
            }
        }
    }
}
