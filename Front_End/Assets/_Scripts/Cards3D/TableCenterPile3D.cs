using System.Collections.Generic;
using UnityEngine;

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Represents the central face-down bluff card pile on the table.
    /// Manages stacking of cards played by all players with realistic angle/position jitter.
    /// </summary>
    public class TableCenterPile3D : MonoBehaviour
    {
        public static TableCenterPile3D Instance { get; private set; }

        /// <summary>Bài nằm trên bàn to gấp đôi bài cầm tay, cho cân với bàn/ghế đã phóng to.</summary>
        public const float TableCardScale = 2f;

        private List<GameObject> pileCards = new List<GameObject>();
        private float currentStackHeight = 0.002f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Position self in center of table if not already set
            AlignWithTable();
        }

        /// <summary>Tâm mặt bàn trong world space. Dùng chung cho chồng bài, bộ bài và animation chia bài.</summary>
        /// <summary>Bàn đang ngồi (quán nhiều bàn). Null thì dùng object tên "Table" như scene cũ.</summary>
        public static Transform ActiveTable { get; set; }

        public static bool HasTable => ActiveTable != null || GameObject.Find("Table") != null;

        public static Vector3 GetTableTopCenter()
        {
            var t = ActiveTable != null ? ActiveTable.gameObject : GameObject.Find("Table");
            if (t == null) return new Vector3(-1.75f, 1.5f, -5f); // fallback: vị trí bàn mặc định

            // Mặt bàn = đỉnh collider (đúng cho cube lẫn model bàn); không có collider thì dùng công thức cube cũ.
            var col = t.GetComponentInChildren<Collider>();
            float topY = col != null ? col.bounds.max.y : t.transform.position.y + t.transform.localScale.y * 0.5f;
            return new Vector3(t.transform.position.x, topY, t.transform.position.z);
        }

        // Bài đánh ra được xếp chồng lên đỉnh bộ bài có sẵn trên bàn; chưa có bộ bài thì đặt giữa mặt bàn.
        public void AlignWithTable()
        {
            var deck = TableDeck3D.Instance;
            if (deck != null)
            {
                Vector3 deckPos = deck.transform.position;
                float deckTopY = GetTableTopCenter().y + deck.deckHeight;
                transform.position = new Vector3(deckPos.x, deckTopY + 0.005f, deckPos.z);
            }
            else
            {
                transform.position = GetTableTopCenter() + Vector3.up * 0.005f;
            }
        }

        public Vector3 GetNextStackPosition()
        {
            AlignWithTable();

            // Lệch nhẹ tự nhiên, nhỏ để lá bài nằm gọn trên bộ bài
            float jitterX = Random.Range(-0.012f, 0.012f);
            float jitterZ = Random.Range(-0.012f, 0.012f);
            return transform.position + new Vector3(jitterX, currentStackHeight, jitterZ);
        }

        public Quaternion GetNextStackRotation()
        {
            // Úp mặt: mặt số của lá bài (-Z) hướng xuống bàn, mặt lưng hướng lên. Euler(+90) sẽ ngửa mặt số lên.
            // Cùng hướng với bộ bài (rotationAngle) để lá bài nằm khớp lên chồng, chỉ lệch vài độ.
            float baseYaw = TableDeck3D.Instance != null ? TableDeck3D.Instance.rotationAngle : 0f;
            return Quaternion.Euler(-90f, baseYaw + Random.Range(-5f, 5f), 0f);
        }

        public void AddCardToPile(GameObject cardObj)
        {
            if (cardObj == null) return;

            cardObj.transform.SetParent(transform, true);
            var col = cardObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            // Update() của CardVisual3D kéo lá về tư thế trên tay (nghiêng 52°) => lật ngửa mặt số; nằm trên chồng thì đứng yên, úp
            if (cardObj.TryGetComponent<CardVisual3D>(out var visual)) visual.enabled = false;

            pileCards.Add(cardObj);
            currentStackHeight += 0.002f; // Increment stack height per card

            // Sound effect
            if (CardAudioSystem.Instance != null)
            {
                CardAudioSystem.Instance.PlayTableImpact();
            }
        }

        /// <summary>
        /// Clears the played card pile (e.g. after round resolution / challenge).
        /// </summary>
        public void ClearPile()
        {
            foreach (var card in pileCards)
            {
                if (card != null) Destroy(card);
            }
            pileCards.Clear();
            currentStackHeight = 0.002f;
        }
    }
}
