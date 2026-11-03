using UnityEngine;

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Constructs authentic 3D playing card GameObjects with front & back quads,
    /// silhouette highlight border, and visual controllers.
    /// Front face faces the player (negative Z in card space), back face faces outward (positive Z).
    /// </summary>
    public static class CardMeshBuilder
    {
        public const float CARD_WIDTH = 0.072f;    // 7.2 cm
        public const float CARD_HEIGHT = 0.115f;  // 11.5 cm
        public const float CARD_THICKNESS = 0.001f; // 1 mm

        public static GameObject CreateCardObject(string cardInstanceId, string rank)
        {
            var cardRoot = new GameObject($"Card3D_{rank}_{cardInstanceId}");

            // 1. Front Quad (Mặt số / quân bài - hướng thẳng về phía camera người chơi)
            var frontQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frontQuad.name = "FrontFace";
            frontQuad.transform.SetParent(cardRoot.transform, false);
            frontQuad.transform.localPosition = new Vector3(0, 0, -CARD_THICKNESS * 0.5f);
            frontQuad.transform.localRotation = Quaternion.identity; // Normal hướng về -Z (nhìn thấy mặt số từ phía player)
            frontQuad.transform.localScale = new Vector3(CARD_WIDTH, CARD_HEIGHT, 1f);
            Object.Destroy(frontQuad.GetComponent<Collider>());

            var frontRenderer = frontQuad.GetComponent<MeshRenderer>();
            frontRenderer.material = CardTextureGenerator.GetCardFrontMaterial(rank);
            frontRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 2. Back Quad (Mặt sau lá bài / Beer mug - quay lưng về phía trước / đối thủ)
            var backQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backQuad.name = "BackFace";
            backQuad.transform.SetParent(cardRoot.transform, false);
            backQuad.transform.localPosition = new Vector3(0, 0, CARD_THICKNESS * 0.5f);
            backQuad.transform.localRotation = Quaternion.Euler(0, 180f, 0); // Normal hướng về +Z (quay lưng ra ngoài)
            backQuad.transform.localScale = new Vector3(CARD_WIDTH, CARD_HEIGHT, 1f);
            Object.Destroy(backQuad.GetComponent<Collider>());

            var backRenderer = backQuad.GetComponent<MeshRenderer>();
            backRenderer.material = CardTextureGenerator.GetCardBackMaterial();
            backRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 3. Highlight Border Quad (Viền sáng khi chọn / focus - nằm ngay giữa 2 mặt tạo khung viền)
            var highlightQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlightQuad.name = "HighlightBorder";
            highlightQuad.transform.SetParent(cardRoot.transform, false);
            highlightQuad.transform.localPosition = Vector3.zero;
            highlightQuad.transform.localRotation = Quaternion.identity;
            highlightQuad.transform.localScale = new Vector3(CARD_WIDTH * 1.08f, CARD_HEIGHT * 1.06f, 1f);
            Object.Destroy(highlightQuad.GetComponent<Collider>());

            var hlRenderer = highlightQuad.GetComponent<MeshRenderer>();
            hlRenderer.material = CardTextureGenerator.GetSelectionHighlightMaterial();
            hlRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            highlightQuad.SetActive(false);

            // 4. Visual Component
            var visual = cardRoot.AddComponent<CardVisual3D>();
            visual.Initialize(cardInstanceId, rank, frontRenderer, backRenderer, highlightQuad);

            return cardRoot;
        }
    }
}
