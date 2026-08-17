using System.Collections.Generic;
using UnityEngine;

namespace AccardND.Presentation
{
    /// <summary>
    /// Bandierine per il selettore della lingua, disegnate a runtime come gli altri
    /// sprite del tema: le emoji bandiera non vengono rese dai font di sistema su
    /// Android e WebGL, e un atlante di PNG sarebbe un asset da mantenere a mano.
    /// Le lingue non previste ricevono un gonfalone neutro, così la UI non si rompe.
    /// </summary>
    internal static class LocaleFlagSprites
    {
        private const int Width = 96;
        private const int Height = 64;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Bande piene, dall'alto o da sinistra, con peso relativo.</summary>
        private readonly struct BandedFlag
        {
            public BandedFlag(bool vertical, Color[] colors, float[] weights = null)
            {
                Vertical = vertical;
                Colors = colors;
                Weights = weights;
            }

            public bool Vertical { get; }

            public Color[] Colors { get; }

            public float[] Weights { get; }
        }

        private static readonly Color Green = new Color(0.00f, 0.55f, 0.27f);
        private static readonly Color Red = new Color(0.81f, 0.11f, 0.19f);
        private static readonly Color White = new Color(0.97f, 0.97f, 0.97f);
        private static readonly Color Blue = new Color(0.00f, 0.21f, 0.60f);
        private static readonly Color Black = new Color(0.07f, 0.07f, 0.07f);
        private static readonly Color Gold = new Color(0.95f, 0.79f, 0.19f);
        private static readonly Color Orange = new Color(0.94f, 0.55f, 0.20f);

        private static readonly Dictionary<string, BandedFlag> Banded = new Dictionary<string, BandedFlag>
        {
            ["it"] = new BandedFlag(true, new[] { Green, White, Red }),
            ["fr"] = new BandedFlag(true, new[] { Blue, White, Red }),
            ["ro"] = new BandedFlag(true, new[] { Blue, Gold, Red }),
            ["ie"] = new BandedFlag(true, new[] { Green, White, Orange }),
            ["be"] = new BandedFlag(true, new[] { Black, Gold, Red }),
            ["de"] = new BandedFlag(false, new[] { Black, Red, Gold }),
            ["nl"] = new BandedFlag(false, new[] { Red, White, Blue }),
            ["ru"] = new BandedFlag(false, new[] { White, Blue, Red }),
            ["pl"] = new BandedFlag(false, new[] { White, Red }),
            ["uk"] = new BandedFlag(false, new[] { Blue, Gold }),
            ["es"] = new BandedFlag(false, new[] { Red, Gold, Red }, new[] { 1f, 2f, 1f }),
        };

        public static Sprite Get(string localeCode)
        {
            string key = Normalize(localeCode);
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
                return cached;

            Sprite sprite = Bake(key);
            Cache[key] = sprite;
            return sprite;
        }

        private static string Normalize(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
                return string.Empty;

            string trimmed = localeCode.Trim().ToLowerInvariant();
            int separator = trimmed.IndexOfAny(new[] { '-', '_' });
            return separator > 0 ? trimmed.Substring(0, separator) : trimmed;
        }

        private static Sprite Bake(string key)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "Locale Flag " + (string.IsNullOrEmpty(key) ? "generic" : key),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Banded.TryGetValue(key, out BandedFlag banded);
            bool hasBands = Banded.ContainsKey(key);

            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                float yn = y / (float)(Height - 1);
                for (int x = 0; x < Width; x++)
                {
                    float xn = x / (float)(Width - 1);
                    Color color;
                    if (hasBands)
                        color = SampleBands(banded, xn, yn);
                    else if (key == "en" || key == "gb")
                        color = SampleUnionJack(xn, yn);
                    else if (key == "ja")
                        color = SampleDisc(xn, yn, White, Red, 0.28f);
                    else
                        color = SampleNeutral(xn, yn);

                    pixels[y * Width + x] = ApplyBorder(color, xn, yn);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, Width, Height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Color SampleBands(BandedFlag flag, float xn, float yn)
        {
            // Le bande orizzontali si contano dall'alto, quindi yn va rovesciato.
            float axis = flag.Vertical ? xn : 1f - yn;
            float[] weights = flag.Weights;
            int count = flag.Colors.Length;

            float total = 0f;
            for (int index = 0; index < count; index++)
                total += weights != null ? weights[index] : 1f;

            float cursor = 0f;
            for (int index = 0; index < count; index++)
            {
                cursor += (weights != null ? weights[index] : 1f) / total;
                if (axis <= cursor)
                    return flag.Colors[index];
            }

            return flag.Colors[count - 1];
        }

        /// <summary>
        /// Union Jack semplificata: le croci sono simmetriche invece che contrappuntate,
        /// una differenza invisibile alla dimensione a cui viene mostrata.
        /// </summary>
        private static Color SampleUnionJack(float xn, float yn)
        {
            float rising = Mathf.Abs(yn - xn);
            float falling = Mathf.Abs(yn - (1f - xn));
            float fromVerticalAxis = Mathf.Abs(xn - 0.5f);
            float fromHorizontalAxis = Mathf.Abs(yn - 0.5f);

            if (fromVerticalAxis < 0.075f || fromHorizontalAxis < 0.11f)
                return Red;
            if (fromVerticalAxis < 0.13f || fromHorizontalAxis < 0.19f)
                return White;
            if (rising < 0.08f || falling < 0.08f)
                return Red;
            if (rising < 0.2f || falling < 0.2f)
                return White;
            return Blue;
        }

        private static Color SampleDisc(float xn, float yn, Color field, Color disc, float radius)
        {
            float dx = (xn - 0.5f) * (Width / (float)Height);
            float dy = yn - 0.5f;
            return Mathf.Sqrt(dx * dx + dy * dy) <= radius ? disc : field;
        }

        /// <summary>Gonfalone senza nazione: sfumatura scura, per le lingue non previste.</summary>
        private static Color SampleNeutral(float xn, float yn)
        {
            float shade = Mathf.Lerp(0.16f, 0.30f, (xn + yn) * 0.5f);
            return new Color(shade * 0.7f, shade * 0.8f, shade);
        }

        private static Color ApplyBorder(Color color, float xn, float yn)
        {
            float edge = Mathf.Min(Mathf.Min(xn, 1f - xn), Mathf.Min(yn, 1f - yn));
            if (edge < 0.028f)
                return new Color(0.03f, 0.04f, 0.05f, 1f);
            return color;
        }
    }
}
