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

        public void AlignWithTable()
        {
            var tableObj = GameObject.Find("Table");
            if (tableObj != null)
            {
                // Table top is position.y + (scale.y / 2)
                float topY = tableObj.transform.position.y + (tableObj.transform.localScale.y * 0.5f) + 0.005f;
                transform.position = new Vector3(tableObj.transform.position.x, topY, tableObj.transform.position.z);
            }
            else
            {
                // Fallback default coordinates
                transform.position = new Vector3(-1.75f, 1.51f, -5f);
            }
        }

        public Vector3 GetNextStackPosition()
        {
            // Slight natural jitter on felt
            float jitterX = Random.Range(-0.04f, 0.04f);
            float jitterZ = Random.Range(-0.04f, 0.04f);
            return transform.position + new Vector3(jitterX, currentStackHeight, jitterZ);
        }

        public Quaternion GetNextStackRotation()
        {
            // Face-down flat on table with random angular spread
            float angleJitter = Random.Range(-22f, 22f);
            return Quaternion.Euler(90f, angleJitter, 0f);
        }

        public void AddCardToPile(GameObject cardObj)
        {
            if (cardObj == null) return;

            cardObj.transform.SetParent(transform, true);
            var col = cardObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;

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
