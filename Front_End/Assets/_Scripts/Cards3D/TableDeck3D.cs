using UnityEngine;

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Builds and displays a realistic 3D deck of playing cards ("Sấp bài") directly on the table.
    /// Sits on the table felt next to the central played card pile, featuring the cute
    /// low-poly card back texture (_4) and realistic paper-layered edges.
    /// </summary>
    public class TableDeck3D : MonoBehaviour
    {
        public static TableDeck3D Instance { get; private set; }

        [Header("Deck Dimensions")]
        // Cùng tỉ lệ với lá bài trên bàn (TableCardScale = 2x bài cầm tay)
        public float deckWidth = 0.072f * TableCenterPile3D.TableCardScale;
        public float deckLength = 0.115f * TableCenterPile3D.TableCardScale;
        public float deckHeight = 0.018f * TableCenterPile3D.TableCardScale;

        [Header("Offset relative to Table Center")]
        public Vector3 offsetFromCenter = new Vector3(0.6f, 0f, 0.4f);
        public float rotationAngle = 18f;

        private GameObject deckModel;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Quán nhiều bàn: bộ bài chỉ dựng khi người chơi đã ngồi vào một bàn (ChairItem gọi lại BuildDeckOnTable)
            if (TableCenterPile3D.HasTable) BuildDeckOnTable();
        }

        public void BuildDeckOnTable()
        {
            if (deckModel != null) Destroy(deckModel);

            Vector3 tablePos = TableCenterPile3D.GetTableTopCenter();
            float tableTopY = tablePos.y + 0.003f;

            Vector3 deckPosition = new Vector3(
                tablePos.x + offsetFromCenter.x,
                tableTopY + (deckHeight * 0.5f),
                tablePos.z + offsetFromCenter.z
            );

            transform.position = deckPosition;
            transform.rotation = Quaternion.Euler(0, rotationAngle, 0);

            deckModel = new GameObject("Deck_MeshRoot");
            deckModel.transform.SetParent(transform, false);

            // 1. Core Block (The thick card paper edge stack)
            var edgeCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edgeCube.name = "Deck_PaperEdgeStack";
            edgeCube.transform.SetParent(deckModel.transform, false);
            edgeCube.transform.localPosition = Vector3.zero;
            edgeCube.transform.localRotation = Quaternion.identity;
            edgeCube.transform.localScale = new Vector3(deckWidth, deckHeight, deckLength);

            var edgeRenderer = edgeCube.GetComponent<MeshRenderer>();
            edgeRenderer.material = CardTextureGenerator.GetDeckEdgeMaterial();
            edgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Destroy(edgeCube.GetComponent<Collider>());

            // 2. Top Face Quad (Mặt sau lá bài _4)
            var topQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            topQuad.name = "Deck_TopCardFace";
            topQuad.transform.SetParent(deckModel.transform, false);
            // Sits precisely on top of the block
            topQuad.transform.localPosition = new Vector3(0, (deckHeight * 0.5f) + 0.0005f, 0);
            topQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            topQuad.transform.localScale = new Vector3(deckWidth, deckLength, 1f);

            var topRenderer = topQuad.GetComponent<MeshRenderer>();
            topRenderer.material = CardTextureGenerator.GetCardBackMaterial();
            topRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(topQuad.GetComponent<Collider>());

            // 3. Realistic askew card on top (Lá bài trên cùng hơi lệch góc 2 độ tạo cảm giác tự nhiên)
            var topCardAskew = GameObject.CreatePrimitive(PrimitiveType.Quad);
            topCardAskew.name = "Deck_TopCardAskew";
            topCardAskew.transform.SetParent(deckModel.transform, false);
            topCardAskew.transform.localPosition = new Vector3(0.0008f, (deckHeight * 0.5f) + 0.0009f, -0.0006f);
            topCardAskew.transform.localRotation = Quaternion.Euler(90f, -2.5f, 0f);
            topCardAskew.transform.localScale = new Vector3(deckWidth, deckLength, 1f);

            var askewRenderer = topCardAskew.GetComponent<MeshRenderer>();
            askewRenderer.material = CardTextureGenerator.GetCardBackMaterial();
            askewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(topCardAskew.GetComponent<Collider>());
        }
    }
}
