using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LiarsCard.Cards3D
{
    /// <summary>
    /// Generates, extracts, and caches authentic materials and textures for 3D cards and UI.
    /// Integrates the user's custom cute low-poly card spritesheet:
    /// _0: A, _1: K, _2: Q, _3: Joker, _4: Card Back.
    /// Provides pixel-isolated Texture2D and URP materials for 3D hands and table decks.
    /// </summary>
    public static class CardTextureGenerator
    {
        private static readonly Dictionary<string, Sprite> registeredSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static Sprite registeredCardBack;

        private static readonly Dictionary<string, Material> frontMaterials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private static Material backMaterial;
        private static Material deckEdgeMaterial;
        private static Material selectionHighlightMaterial;

        private static Shader cardShader;

        public static void RegisterSprites(Sprite a, Sprite k, Sprite q, Sprite joker, Sprite back)
        {
            if (a != null) registeredSprites["A"] = a;
            if (k != null) registeredSprites["K"] = k;
            if (q != null)
            {
                registeredSprites["Q"] = q;
                registeredSprites["QUEEN"] = q;
            }
            if (joker != null)
            {
                registeredSprites["JOKER"] = joker;
                registeredSprites["J"] = joker;
            }
            if (back != null) registeredCardBack = back;

            // Invalidate cached materials to rebuild with fresh sprites
            frontMaterials.Clear();
            backMaterial = null;
        }

        public static Sprite GetCardSprite(string rank)
        {
            EnsureSpritesLoaded();
            if (string.IsNullOrEmpty(rank)) return registeredSprites.GetValueOrDefault("A") ?? registeredCardBack;
            rank = rank.Trim().ToUpper();

            if (registeredSprites.TryGetValue(rank, out var s) && s != null)
            {
                return s;
            }

            // Normalization
            if (rank == "1" || rank == "ACE") return registeredSprites.GetValueOrDefault("A");
            if (rank == "KING") return registeredSprites.GetValueOrDefault("K");
            if (rank == "QUEEN") return registeredSprites.GetValueOrDefault("Q");
            if (rank == "JOKER" || rank == "J") return registeredSprites.GetValueOrDefault("JOKER");

            return registeredSprites.GetValueOrDefault("A") ?? registeredCardBack;
        }

        public static Sprite GetCardBackSprite()
        {
            EnsureSpritesLoaded();
            return registeredCardBack;
        }

        private static void EnsureSpritesLoaded()
        {
            if (registeredSprites.Count >= 4 && registeredCardBack != null) return;

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Prefabs/Cart" });
            if (guids != null)
            {
                foreach (var g in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(g);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    if (assets != null)
                    {
                        foreach (var obj in assets)
                        {
                            if (obj is Sprite sprite)
                            {
                                if (sprite.name.EndsWith("_0")) registeredSprites["A"] = sprite;
                                else if (sprite.name.EndsWith("_1")) registeredSprites["K"] = sprite;
                                else if (sprite.name.EndsWith("_2"))
                                {
                                    registeredSprites["Q"] = sprite;
                                    registeredSprites["QUEEN"] = sprite;
                                }
                                else if (sprite.name.EndsWith("_3"))
                                {
                                    registeredSprites["JOKER"] = sprite;
                                    registeredSprites["J"] = sprite;
                                }
                                else if (sprite.name.EndsWith("_4")) registeredCardBack = sprite;
                            }
                        }
                    }
                }
            }
#endif
        }

        private static Shader GetCardShader()
        {
            if (cardShader != null) return cardShader;
            cardShader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Sprites/Default");
            return cardShader;
        }

        public static Material GetCardFrontMaterial(string rank)
        {
            rank = (rank ?? "A").ToUpper();
            if (frontMaterials.TryGetValue(rank, out var mat) && mat != null)
            {
                return mat;
            }

            var sprite = GetCardSprite(rank);
            mat = CreateMaterialFromSprite(sprite, $"Mat_CardFront_{rank}");
            if (mat != null)
            {
                frontMaterials[rank] = mat;
                return mat;
            }

            // Fallback default material if sprite unavailable
            mat = new Material(GetCardShader());
            mat.name = $"Mat_CardFront_Fallback_{rank}";
            frontMaterials[rank] = mat;
            return mat;
        }

        public static Material GetCardBackMaterial()
        {
            if (backMaterial != null) return backMaterial;

            var sprite = GetCardBackSprite();
            backMaterial = CreateMaterialFromSprite(sprite, "Mat_CardBack_LowPoly");
            if (backMaterial != null) return backMaterial;

            backMaterial = new Material(GetCardShader());
            backMaterial.name = "Mat_CardBack_Fallback";
            return backMaterial;
        }

        public static Material GetDeckEdgeMaterial()
        {
            if (deckEdgeMaterial != null) return deckEdgeMaterial;

            deckEdgeMaterial = new Material(GetCardShader());
            deckEdgeMaterial.name = "Mat_DeckEdge_Paper";

            // Creamy paper card edge color with subtle warm tone
            Color edgeCol = new Color(0.92f, 0.90f, 0.86f);
            if (deckEdgeMaterial.HasProperty("_BaseColor")) deckEdgeMaterial.SetColor("_BaseColor", edgeCol);
            if (deckEdgeMaterial.HasProperty("_Color")) deckEdgeMaterial.SetColor("_Color", edgeCol);
            if (deckEdgeMaterial.HasProperty("_Smoothness")) deckEdgeMaterial.SetFloat("_Smoothness", 0.2f);
            if (deckEdgeMaterial.HasProperty("_Metallic")) deckEdgeMaterial.SetFloat("_Metallic", 0.0f);

            return deckEdgeMaterial;
        }

        public static Material GetSelectionHighlightMaterial()
        {
            if (selectionHighlightMaterial != null) return selectionHighlightMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? GetCardShader();
            selectionHighlightMaterial = new Material(shader);
            selectionHighlightMaterial.name = "Mat_CardSelectionHighlight";

            Color goldGlow = new Color(1f, 0.85f, 0.2f, 0.9f);
            if (selectionHighlightMaterial.HasProperty("_Color")) selectionHighlightMaterial.SetColor("_Color", goldGlow);
            if (selectionHighlightMaterial.HasProperty("_BaseColor")) selectionHighlightMaterial.SetColor("_BaseColor", goldGlow);

            return selectionHighlightMaterial;
        }

        public static Material CreateMaterialFromSprite(Sprite sprite, string matName)
        {
            if (sprite == null) return null;

            Texture2D tex = ExtractSpriteTexture(sprite);
            if (tex == null) return null;

            var mat = new Material(GetCardShader());
            mat.name = matName;
            mat.mainTexture = tex;

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.42f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);

            return mat;
        }

        public static Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;

            int w = Mathf.RoundToInt(sprite.rect.width);
            int h = Mathf.RoundToInt(sprite.rect.height);

            // Fast path: direct GetPixels if texture is readable
            if (sprite.texture.isReadable)
            {
                try
                {
                    var pixels = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, w, h);
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tex.name = sprite.name + "_IsolatedTex";
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    tex.SetPixels(pixels);
                    tex.Apply(false, false);
                    return tex;
                }
                catch { }
            }

            // GPU RenderTexture fallback
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);

            Vector2 scale = new Vector2(sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
            Vector2 offset = new Vector2(sprite.rect.x / sprite.texture.width, sprite.rect.y / sprite.texture.height);

            Graphics.Blit(sprite.texture, rt, scale, offset);

            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.name = sprite.name + "_IsolatedTex";
            result.wrapMode = TextureWrapMode.Clamp;
            result.filterMode = FilterMode.Bilinear;
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply(false, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }
    }
}
