using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Tema grafico unico stile MMO per tutta la UI generata da codice.
    /// Centralizza font, palette colori e gli asset 9-slice procedurali
    /// (pannelli con cornice dorata, bottoni con bisello metallico) usati
    /// sia dalla campagna sia dal PvP, così ogni schermata resta coerente.
    /// </summary>
    public static class MmoUiTheme
    {
        private const string BuiltinFontName = "LegacyRuntime.ttf";

        public enum ButtonVariant
        {
            Gold,
            Arcane,
            Crimson,
            Emerald,
            Violet
        }

        // ---------- Palette ----------
        public static readonly Color Ink = new(0.018f, 0.026f, 0.04f, 0.98f);
        public static readonly Color Panel = new(0.028f, 0.055f, 0.088f, 0.97f);
        public static readonly Color PanelBright = new(0.065f, 0.125f, 0.17f, 0.96f);
        public static readonly Color Gold = new(1f, 0.76f, 0.36f, 1f);
        public static readonly Color Copper = new(0.74f, 0.38f, 0.18f, 1f);
        public static readonly Color Arcane = new(0.15f, 0.82f, 0.95f, 1f);
        public static readonly Color Violet = new(0.56f, 0.34f, 0.92f, 1f);
        public static readonly Color Good = new(0.42f, 0.9f, 0.45f, 1f);
        public static readonly Color Bad = new(0.95f, 0.28f, 0.24f, 1f);
        public static readonly Color TextMuted = new(0.7f, 0.83f, 0.9f, 1f);

        // ---------- Font ----------
        private static Font titleFont;
        private static Font bodyFont;
        private static Font bodyBoldFont;

        /// <summary>Font da titolo/bottone (Cinzel, taglio lapidario alla Trajan).</summary>
        public static Font TitleFont
        {
            get
            {
                if (titleFont != null)
                    return titleFont;
                titleFont = LoadFont("Fonts/Cinzel", new[] { "Cinzel", "Trajan Pro", "Palatino Linotype", "Georgia" });
                return titleFont;
            }
        }

        /// <summary>Font di lettura per corpo testo, etichette e log.</summary>
        public static Font BodyFont
        {
            get
            {
                if (bodyFont != null)
                    return bodyFont;
                bodyFont = LoadFont("Fonts/AlegreyaSans", new[] { "Alegreya Sans", "Segoe UI", "Verdana" });
                return bodyFont;
            }
        }

        /// <summary>Variante bold reale del font di lettura (per numeri e valori).</summary>
        public static Font BodyBoldFont
        {
            get
            {
                if (bodyBoldFont != null)
                    return bodyBoldFont;
                bodyBoldFont = LoadFont("Fonts/AlegreyaSansBold", new[] { "Alegreya Sans", "Segoe UI", "Verdana" });
                return bodyBoldFont;
            }
        }

        private static Font LoadFont(string resourcePath, string[] osFallbacks)
        {
            Font font = Resources.Load<Font>(resourcePath);
            if (font != null)
                return font;
            font = Font.CreateDynamicFontFromOSFont(osFallbacks, 24);
            if (font != null)
                return font;
            return Resources.GetBuiltinResource<Font>(BuiltinFontName);
        }

        /// <summary>Applica il taglio da titolo MMO a un testo esistente: Cinzel in grassetto.</summary>
        public static void StyleAsTitle(Text text)
        {
            text.font = TitleFont;
            if (text.fontStyle == FontStyle.Italic || text.fontStyle == FontStyle.BoldAndItalic)
                text.fontStyle = FontStyle.BoldAndItalic;
            else
                text.fontStyle = FontStyle.Bold;
        }

        // ---------- Micro-interazioni ----------
        /// <summary>Aggiunge il feedback hover/press da MMO (leggera scala) a un bottone.</summary>
        public static void AddMotion(Selectable selectable)
        {
            if (selectable != null && selectable.GetComponent<UiButtonMotion>() == null)
                selectable.gameObject.AddComponent<UiButtonMotion>();
        }

        // ---------- Sprite procedurali ----------
        private static Sprite panelSprite;
        private static Sprite softPanelSprite;
        private static Sprite gemSprite;
        private static readonly Dictionary<ButtonVariant, Sprite> buttonSprites = new();

        public static Color AccentOf(ButtonVariant variant) => variant switch
        {
            ButtonVariant.Crimson => new Color(0.86f, 0.3f, 0.26f, 1f),
            ButtonVariant.Emerald => new Color(0.34f, 0.82f, 0.46f, 1f),
            ButtonVariant.Violet => new Color(0.58f, 0.4f, 0.92f, 1f),
            ButtonVariant.Gold => Gold,
            _ => Arcane
        };

        /// <summary>Mappa un colore "semantico" legacy sulla variante di bottone più vicina.</summary>
        public static ButtonVariant ResolveVariant(Color color)
        {
            if (color.r > 0.35f && color.g < 0.23f && color.b < 0.23f)
                return ButtonVariant.Crimson;
            if (color.g > color.r * 1.35f && color.g > color.b * 1.1f)
                return ButtonVariant.Emerald;
            if (color.r > 0.16f && color.b > color.g * 1.08f)
                return ButtonVariant.Violet;
            if (color.r > 0.38f && color.g > 0.24f && color.b < 0.15f)
                return ButtonVariant.Gold;
            return ButtonVariant.Arcane;
        }

        /// <summary>
        /// Pannello con doppia cornice dorata bisellata su corpo scuro sfumato.
        /// 48x48, 9-slice con bordo 14: regge bene da tooltip a pannelli full screen.
        /// </summary>
        public static Sprite GetPanelSprite()
        {
            if (panelSprite != null)
                return panelSprite;

            const int size = 48;
            const float radius = 11f;
            Color baseBottom = new(0.018f, 0.035f, 0.065f);
            Color baseTop = new(0.055f, 0.115f, 0.165f);
            Color goldPeak = new(1f, 0.78f, 0.34f);
            Color goldShadow = new(0.34f, 0.21f, 0.07f);
            Color bevelLight = new(0.16f, 0.32f, 0.42f);
            Color shadowGroove = new(0.006f, 0.014f, 0.026f);
            Color arcaneLine = new(0.08f, 0.58f, 0.72f, 1f);

            panelSprite = BakeSprite("Mmo UI Panel", size, size, new Vector4(14f, 14f, 14f, 14f), (x, y, d, xn, yn) =>
            {
                if (d < 0f)
                    return Color.clear;
                Color color;
                if (d < 1.4f)
                {
                    float t = d / 1.4f;
                    Color col = Color.Lerp(goldShadow, goldPeak, t);
                    color = new Color(col.r, col.g, col.b, t);
                }
                else if (d < 3f)
                {
                    // Cresta dorata: illuminata dall'alto per un effetto metallo battuto.
                    float lit = Mathf.Clamp01(yn * 0.8f + (1f - xn) * 0.2f);
                    Color crest = Color.Lerp(goldShadow, goldPeak, 0.35f + lit * 0.65f);
                    color = Color.Lerp(crest, goldShadow, (d - 1.4f) / 1.6f * 0.7f);
                }
                else if (d < 4.4f)
                {
                    color = Color.Lerp(shadowGroove, bevelLight, (d - 3f) / 1.4f);
                }
                else if (d < 6f)
                {
                    Color body = Color.Lerp(baseBottom, baseTop, yn);
                    Color coldRim = Color.Lerp(bevelLight, arcaneLine, 0.2f + Mathf.Sin((xn + yn) * 18f) * 0.04f);
                    color = Color.Lerp(coldRim, body, (d - 4.4f) / 1.6f);
                }
                else
                {
                    color = Color.Lerp(baseBottom, baseTop, yn);
                    if (yn > 0.72f)
                        color = Color.Lerp(color, PanelBright, (yn - 0.72f) * 0.9f);
                    float rune = Mathf.Sin((xn * 19f + yn * 11f) * Mathf.PI);
                    if (rune > 0.985f && d > 12f)
                        color = Color.Lerp(color, arcaneLine, 0.18f);
                }
                float alpha = Mathf.Clamp01(d + 0.5f);
                return new Color(color.r, color.g, color.b, color.a * alpha);
            }, radius);
            return panelSprite;
        }

        /// <summary>Pannello sobrio con filo perimetrale freddo, per sezioni interne e badge.</summary>
        public static Sprite GetSoftPanelSprite()
        {
            if (softPanelSprite != null)
                return softPanelSprite;

            const int size = 48;
            const float radius = 8f;
            Color rim = new(0.78f, 0.58f, 0.25f, 1f);
            Color coldRim = new(0.12f, 0.5f, 0.68f, 1f);
            Color bodyBottom = new(0.012f, 0.026f, 0.052f, 1f);
            Color bodyTop = new(0.052f, 0.1f, 0.145f, 1f);

            softPanelSprite = BakeSprite("Mmo UI Soft Panel", size, size, new Vector4(10f, 10f, 10f, 10f), (x, y, d, xn, yn) =>
            {
                if (d < 0f)
                    return Color.clear;
                Color body = Color.Lerp(bodyBottom, bodyTop, yn);
                Color color;
                if (d < 1.4f)
                    color = new Color(rim.r, rim.g, rim.b, Mathf.Clamp01(d / 1.4f));
                else if (d < 3.6f)
                    color = Color.Lerp(Color.Lerp(rim, coldRim, 0.25f), body, (d - 1.4f) / 2.2f);
                else
                    color = body;
                float alpha = Mathf.Clamp01(d + 0.5f);
                return new Color(color.r, color.g, color.b, color.a * alpha);
            }, radius);
            return softPanelSprite;
        }

        /// <summary>
        /// Bottone MMO: corpo scuro vetroso, cornice metallica bisellata color accento
        /// con luce dall'alto, filo interno luminoso, riflesso in alto e borchie angolari.
        /// 112x88, 9-slice con bordo 22. Una texture per variante, in cache.
        /// </summary>
        public static Sprite GetButtonSprite(ButtonVariant variant)
        {
            if (buttonSprites.TryGetValue(variant, out Sprite cached) && cached != null)
                return cached;

            const int w = 112, h = 88;
            const float radius = 16f;
            Color accent = AccentOf(variant);
            Color outline = new(0.004f, 0.008f, 0.016f, 1f);
            Color frameHi = Color.Lerp(accent, Color.white, 0.66f);
            Color frameLo = Scale(accent, 0.24f);
            Color innerLine = Color.Lerp(accent, Color.white, 0.32f);
            Color groove = new(0.01f, 0.018f, 0.034f, 1f);
            Color bodyTop = Color.Lerp(new Color(0.035f, 0.075f, 0.115f), accent, 0.22f);
            Color bodyBottom = new(0.012f, 0.026f, 0.048f);

            // Centri delle borchie angolari (restano intatti nei corner del 9-slice).
            Vector2[] studs =
            {
                new(11f, 11f), new(w - 12f, 11f), new(11f, h - 12f), new(w - 12f, h - 12f)
            };

            Sprite sprite = BakeSprite($"Mmo UI Button {variant}", w, h, new Vector4(22f, 22f, 22f, 22f), (x, y, d, xn, yn) =>
            {
                if (d < -0.5f)
                    return Color.clear;

                Color color;
                if (d < 1.3f)
                {
                    color = outline;
                }
                else if (d < 5.6f)
                {
                    // Bisello metallico: chiave di luce alto-sinistra, brunito in basso.
                    float lit = Mathf.Clamp01(yn * 0.82f + (1f - xn) * 0.18f);
                    float ridge = 1f - Mathf.Abs((d - 3.4f) / 2.2f);
                    Color metal = Color.Lerp(frameLo, frameHi, lit);
                    color = Color.Lerp(metal, frameHi, Mathf.Clamp01(ridge) * lit * 0.35f);
                }
                else if (d < 6.8f)
                {
                    color = groove;
                }
                else if (d < 8f)
                {
                    color = Color.Lerp(innerLine, Color.white, Mathf.Clamp01(yn - 0.65f) * 0.35f);
                }
                else
                {
                    color = Color.Lerp(bodyBottom, bodyTop, yn);
                    if (d < 13f)
                        color = Color.Lerp(color, bodyBottom, (13f - d) / 13f * 0.4f);
                    if (yn > 0.6f)
                        color = Color.Lerp(color, Color.white, (yn - 0.6f) * 0.5f * 0.34f);
                    if (yn < 0.16f)
                        color = Color.Lerp(color, outline, (0.16f - yn) * 0.9f);
                }

                foreach (Vector2 stud in studs)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), stud);
                    if (dist < 3.4f)
                    {
                        float lit = Mathf.Clamp01((stud.y - y + 3.4f) / 6.8f * 0.4f + (stud.x - x + 3.4f) / 6.8f * 0.2f + 0.25f);
                        Color studColor = Color.Lerp(frameHi, frameLo, lit);
                        color = Color.Lerp(studColor, color, Mathf.Clamp01(dist - 2.4f));
                    }
                }

                float alpha = Mathf.Clamp01(d + 0.5f);
                return new Color(color.r, color.g, color.b, alpha);
            }, radius);
            buttonSprites[variant] = sprite;
            return sprite;
        }

        /// <summary>Applica a un Button il ColorBlock standard del tema (hover chiaro, press scuro).</summary>
        public static void ApplyButtonColors(Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.18f, 1.08f, 1f);
            colors.pressedColor = new Color(0.66f, 0.76f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.34f, 0.38f, 0.44f, 0.58f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.09f;
            button.colors = colors;
        }

        public static Sprite GetGemSprite()
        {
            if (gemSprite != null)
                return gemSprite;

            const int size = 64;
            gemSprite = BakeSprite("Mmo UI Crystal Gem", size, size, Vector4.zero, (x, y, d, xn, yn) =>
            {
                float dx = Mathf.Abs(xn - 0.5f) * 2f;
                float dy = Mathf.Abs(yn - 0.5f) * 2f;
                float diamond = 1f - (dx + dy);
                if (diamond < 0f)
                    return Color.clear;

                Color edge = new(0.02f, 0.03f, 0.05f, 1f);
                Color cyan = new(0.1f, 0.85f, 1f, 1f);
                Color core = Color.Lerp(cyan, Color.white, Mathf.Clamp01(1f - Mathf.Max(dx, dy)));
                Color color = diamond < 0.12f ? edge : Color.Lerp(cyan, core, diamond);
                if (xn < 0.5f && yn > 0.5f)
                    color = Color.Lerp(color, Color.white, 0.32f);
                if (xn > 0.52f && yn < 0.5f)
                    color = Color.Lerp(color, new Color(0.02f, 0.3f, 0.55f), 0.42f);
                return new Color(color.r, color.g, color.b, Mathf.Clamp01(diamond * 3.5f));
            }, 0f);
            return gemSprite;
        }

        public static void AddPanelGem(RectTransform parent, string name, Vector2 anchor, Vector2 size, Color tint)
        {
            if (parent == null)
                return;

            var gem = new GameObject(name, typeof(RectTransform), typeof(Image));
            gem.transform.SetParent(parent, false);
            var rect = (RectTransform)gem.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var image = gem.GetComponent<Image>();
            image.sprite = GetGemSprite();
            image.preserveAspect = true;
            image.color = tint;
            image.raycastTarget = false;
        }

        private delegate Color PixelShader(int x, int y, float insideDistance, float xn, float yn);

        /// <summary>Rasterizza una texture rounded-rect via SDF e la impacchetta come sprite 9-slice.</summary>
        private static Sprite BakeSprite(string name, int width, int height, Vector4 border, PixelShader shade, float radius)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            float halfW = (width - 1) * 0.5f;
            float halfH = (height - 1) * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float yn = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float xn = x / (float)(width - 1);
                    float qx = Mathf.Abs(x - halfW) - (halfW - radius);
                    float qy = Mathf.Abs(y - halfH) - (halfH - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float d = radius - (outside + inside);
                    pixels[y * width + x] = shade(x, y, d, xn, yn);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u,
                SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Color Scale(Color color, float factor) =>
            new(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}
