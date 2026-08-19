using UnityEngine;

namespace AccardND.UiKit
{
    /// <summary>
    /// Sprite generati a runtime, senza passare da asset importati.
    /// </summary>
    public static class ProceduralSprites
    {
        private const int HelpAuraSize = 128;

        private static Sprite helpAura;

        /// <summary>
        /// Alone dorato sfumato, usato per illuminare il bersaglio di un suggerimento.
        /// Generato una volta e tenuto in cache: e' un anello gaussiano attorno al raggio
        /// 0.72 piu' un riempimento tenue verso il centro.
        /// </summary>
        public static Sprite HelpAura()
        {
            if (helpAura != null)
            {
                return helpAura;
            }

            Texture2D texture = new Texture2D(HelpAuraSize, HelpAuraSize, TextureFormat.ARGB32, false)
            {
                name = "help_aura",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[HelpAuraSize * HelpAuraSize];
            Vector2 center = new Vector2(63.5f, 63.5f);
            for (int y = 0; y < HelpAuraSize; y++)
            {
                for (int x = 0; x < HelpAuraSize; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), center) / 64f;
                    float ring = Mathf.Exp(-Mathf.Pow((radius - 0.72f) / 0.15f, 2f));
                    float fill = Mathf.Clamp01(1f - radius) * 0.18f;
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(ring * 0.75f + fill));
                    pixels[y * HelpAuraSize + x] = new Color32(255, 205, 48, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            helpAura = Sprite.Create(
                texture,
                new Rect(0f, 0f, HelpAuraSize, HelpAuraSize),
                new Vector2(0.5f, 0.5f),
                100f);
            helpAura.name = "help_aura";
            helpAura.hideFlags = HideFlags.HideAndDontSave;
            return helpAura;
        }
    }
}
