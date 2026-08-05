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
        private const string BackButtonResource = "UI/CampaignRestyle/campaign_cta_back_red";
        private const string ConfirmButtonResource = "UI/CampaignRestyle/campaign_cta_confirm_green";
        private const string LightButtonResource = "UI/CampaignRestyle/campaign_cta_light";

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
        private static Font titleBoldFont;
        private static Font bodyFont;
        private static Font bodyBoldFont;
        private static Font displayFont;
        private static Font loreFont;

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

        /// <summary>
        /// Variante bold reale del font da titolo (Cinzel Bold statico). Va preferita
        /// a TitleFont + FontStyle.Bold: quel grassetto è sintetico e a corpo grande
        /// ingrossa le grazie in modo uniforme, sporcando il taglio lapidario.
        /// </summary>
        public static Font TitleBoldFont
        {
            get
            {
                if (titleBoldFont != null)
                    return titleBoldFont;
                titleBoldFont = LoadFont("Fonts/CinzelBold", new[] { "Cinzel", "Trajan Pro", "Palatino Linotype", "Georgia" });
                return titleBoldFont;
            }
        }

        /// <summary>Font di lettura per corpo testo, etichette e log.</summary>
        public static Font BodyFont
        {
            get
            {
                if (bodyFont != null)
                    return bodyFont;
                bodyFont = LoadFont("Fonts/Alegreya", new[] { "Alegreya", "Georgia", "Palatino Linotype" });
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
                bodyBoldFont = LoadFont("Fonts/AlegreyaBold", new[] { "Alegreya", "Georgia", "Palatino Linotype" });
                return bodyBoldFont;
            }
        }

        /// <summary>
        /// Font da display per i momenti celebrativi (ricompense, nomi epici):
        /// Rye, wood type a grazie spinate. Pesante e decorato, quindi va usato
        /// solo su righe corte e non per valori numerici o corpo testo.
        /// </summary>
        public static Font DisplayFont
        {
            get
            {
                if (displayFont != null)
                    return displayFont;
                displayFont = LoadFont("Fonts/Rye", new[] { "Rye", "Rockwell", "Georgia" });
                return displayFont;
            }
        }

        /// <summary>
        /// Font da prosa narrativa (IM Fell English SC, torchio inglese del '600).
        /// E' un maiuscoletto: rende solo su testo scritto in maiuscolo/minuscolo,
        /// su una stringa già tutta in caps non aggiunge nulla. Le grazie sono fini,
        /// quindi non scendere sotto i 16px: a 13 il testo impasta.
        /// Va usato su blocchi brevi, non su testi lunghi da leggere tutti d'un fiato.
        /// </summary>
        public static Font LoreFont
        {
            get
            {
                if (loreFont != null)
                    return loreFont;
                loreFont = LoadFont("Fonts/IMFellEnglishSC", new[] { "IM FELL English SC", "Garamond", "Georgia" });
                return loreFont;
            }
        }

        /// <summary>Dimensione minima sotto la quale <see cref="LoreFont"/> perde leggibilità.</summary>
        public const int LoreFontMinSize = 16;

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

        /// <summary>Applica il taglio da titolo MMO a un testo esistente: Cinzel Bold.</summary>
        public static void StyleAsTitle(Text text)
        {
            text.font = TitleBoldFont;
            // Il peso arriva dal font, non dall'emboldening: qui resta solo il corsivo.
            if (text.fontStyle == FontStyle.Italic || text.fontStyle == FontStyle.BoldAndItalic)
                text.fontStyle = FontStyle.Italic;
            else
                text.fontStyle = FontStyle.Normal;
        }

        /// <summary>
        /// Applica il maiuscoletto medievale alle intestazioni principali delle schermate.
        /// Bottoni e titoli secondari continuano a usare Cinzel.
        /// </summary>
        public static void StyleAsScreenTitle(Text text)
        {
            text.font = LoreFont;
            text.fontStyle = FontStyle.Normal;
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
        private static Sprite rankCrestSprite;
        private static Sprite starSprite;
        private static Sprite radialGlowSprite;
        private static Sprite screenTitlePlaqueSprite;
        private static Sprite screenOuterFrameSprite;
        private static Sprite backButtonSprite;
        private static Sprite confirmButtonSprite;
        private static Sprite lightButtonSprite;
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
        /// Targa condivisa dalle schermate di primo livello.
        /// </summary>
        public static Sprite GetScreenTitlePlaqueSprite()
        {
            if (screenTitlePlaqueSprite == null)
                screenTitlePlaqueSprite = Resources.Load<Sprite>("UI/Sanctuary/sanctuary_title_plaque_v2");
            return screenTitlePlaqueSprite;
        }

        /// <summary>Cornice perimetrale condivisa da tutte le schermate principali.</summary>
        public static Sprite GetScreenOuterFrameSprite()
        {
            if (screenOuterFrameSprite == null)
                screenOuterFrameSprite = Resources.Load<Sprite>("UI/Common/screen_outer_frame_gold");
            return screenOuterFrameSprite;
        }

        public static void ApplyScreenOuterFrame(Image image)
        {
            if (image == null)
                return;

            image.sprite = GetScreenOuterFrameSprite();
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        public static bool IsBackButtonLabel(string label) =>
            (label ?? string.Empty).Trim().ToUpperInvariant() == "INDIETRO";

        public static bool IsLightButtonLabel(string label) =>
            (label ?? string.Empty).Trim().ToUpperInvariant() == "AGGIORNA";

        public static bool IsBackButton(string semanticName, string label)
        {
            string name = (semanticName ?? string.Empty).ToUpperInvariant();
            return name.Contains("BACK") || name.Contains("RETURN") || name.Contains("CANCEL") ||
                   name.Contains("CLOSE") || name.Contains("LEAVE") || IsBackButtonLabel(label);
        }

        public static bool IsLightButton(string semanticName, string label)
        {
            string name = (semanticName ?? string.Empty).ToUpperInvariant();
            return name.Contains("UPDATE") || IsLightButtonLabel(label);
        }

        /// <summary>
        /// Applica il frame rosso condiviso a ogni azione di ritorno, mantenendo
        /// un solo asset e gli stessi stati hover/pressione in tutte le schermate.
        /// </summary>
        public static void ApplyBackButtonStyle(Button button, Text label = null)
        {
            if (button == null)
                return;

            Image image = button.GetComponent<Image>();
            Sprite frame = GetBackButtonSprite();
            if (image != null && frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                button.targetGraphic = image;

                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.86f, 0.86f, 1f);
                colors.pressedColor = new Color(0.72f, 0.42f, 0.42f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;
            }

            label ??= button.GetComponentInChildren<Text>(true);
            if (label == null)
                return;

            StyleAsScreenTitle(label);
            label.fontSize = 30;
            label.resizeTextMaxSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = new Vector2(0.12f, 0.03f);
            label.rectTransform.anchorMax = new Vector2(0.88f, 0.97f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        public static Sprite GetBackButtonSprite()
        {
            if (backButtonSprite != null)
                return backButtonSprite;

            Texture2D texture = Resources.Load<Texture2D>(BackButtonResource);
            if (texture == null)
                return null;

            Rect crop = new(
                texture.width * (145f / 1983f),
                texture.height * (196f / 793f),
                texture.width * (1692f / 1983f),
                texture.height * (400f / 793f));
            backButtonSprite = Sprite.Create(
                texture,
                crop,
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            backButtonSprite.name = "Shared Back Red CTA";
            backButtonSprite.hideFlags = HideFlags.HideAndDontSave;
            return backButtonSprite;
        }

        /// <summary>Applica il CTA verde condiviso alle azioni di conferma.</summary>
        public static void ApplyConfirmButtonStyle(Button button, Text label = null)
        {
            if (button == null)
                return;

            Image image = button.GetComponent<Image>();
            Sprite frame = GetConfirmButtonSprite();
            if (image != null && frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                button.targetGraphic = image;

                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.86f, 1f, 0.9f, 1f);
                colors.pressedColor = new Color(0.42f, 0.72f, 0.48f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;
            }

            label ??= button.GetComponentInChildren<Text>(true);
            if (label == null)
                return;

            StyleAsScreenTitle(label);
            label.fontSize = 30;
            label.resizeTextMaxSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = new Vector2(0.12f, 0.03f);
            label.rectTransform.anchorMax = new Vector2(0.88f, 0.97f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        public static Sprite GetConfirmButtonSprite()
        {
            if (confirmButtonSprite != null)
                return confirmButtonSprite;

            Texture2D texture = Resources.Load<Texture2D>(ConfirmButtonResource);
            if (texture == null)
                return null;

            Rect crop = new(
                texture.width * (145f / 1983f),
                texture.height * (196f / 793f),
                texture.width * (1692f / 1983f),
                texture.height * (400f / 793f));
            confirmButtonSprite = Sprite.Create(
                texture,
                crop,
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            confirmButtonSprite.name = "Shared Confirm Green CTA";
            confirmButtonSprite.hideFlags = HideFlags.HideAndDontSave;
            return confirmButtonSprite;
        }

        /// <summary>Applica il CTA giallo luminoso alle azioni di aggiornamento.</summary>
        public static void ApplyLightButtonStyle(Button button, Text label = null)
        {
            if (button == null)
                return;

            Image image = button.GetComponent<Image>();
            Sprite frame = GetLightButtonSprite();
            if (image != null && frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                button.targetGraphic = image;

                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 0.86f, 1f);
                colors.pressedColor = new Color(0.82f, 0.68f, 0.32f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;
            }

            label ??= button.GetComponentInChildren<Text>(true);
            if (label == null)
                return;

            StyleAsScreenTitle(label);
            label.color = new Color(0.17f, 0.08f, 0.015f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = new Vector2(0.12f, 0.03f);
            label.rectTransform.anchorMax = new Vector2(0.88f, 0.97f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        public static Sprite GetLightButtonSprite()
        {
            if (lightButtonSprite != null)
                return lightButtonSprite;

            Texture2D texture = Resources.Load<Texture2D>(LightButtonResource);
            if (texture == null)
                return null;

            Rect crop = new(
                texture.width * (145f / 1983f),
                texture.height * (196f / 793f),
                texture.width * (1692f / 1983f),
                texture.height * (400f / 793f));
            lightButtonSprite = Sprite.Create(
                texture,
                crop,
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            lightButtonSprite.name = "Shared Light CTA";
            lightButtonSprite.hideFlags = HideFlags.HideAndDontSave;
            return lightButtonSprite;
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
        /// Bottone fantasy runico: corpo scuro profondo, angoli tagliati, doppia
        /// cornice metallica e luce interna color accento. È procedurale e 9-slice,
        /// quindi resta nitido dal piccolo pulsante "X" alle grandi azioni del PvP.
        /// </summary>
        public static Sprite GetButtonSprite(ButtonVariant variant)
        {
            if (buttonSprites.TryGetValue(variant, out Sprite cached) && cached != null)
                return cached;

            const int w = 136, h = 84;
            const float cut = 19f;
            const float inverseSqrtTwo = 0.70710678f;
            Color accent = AccentOf(variant);
            Color outerInk = new(0.002f, 0.004f, 0.009f, 1f);
            Color neutralShadow = new(0.075f, 0.085f, 0.11f, 1f);
            Color neutralMid = variant == ButtonVariant.Gold
                ? new Color(0.48f, 0.39f, 0.23f, 1f)
                : new Color(0.34f, 0.38f, 0.44f, 1f);
            Color neutralLight = variant == ButtonVariant.Gold
                ? new Color(0.88f, 0.76f, 0.48f, 1f)
                : new Color(0.76f, 0.82f, 0.88f, 1f);
            Color frameShadow = Color.Lerp(neutralShadow, Scale(accent, 0.32f), 0.28f);
            Color frameMid = Color.Lerp(neutralMid, accent, 0.2f);
            Color frameLight = Color.Lerp(neutralLight, accent, 0.24f);
            Color innerLight = Color.Lerp(accent, Color.white, 0.34f);
            Color groove = new(0.006f, 0.011f, 0.021f, 1f);
            Color bodyBottom = Color.Lerp(new Color(0.006f, 0.012f, 0.025f), accent, 0.035f);
            Color bodyTop = Color.Lerp(new Color(0.027f, 0.041f, 0.065f), accent, 0.12f);

            Sprite sprite = BakeSprite($"Mmo UI Button {variant}", w, h, new Vector4(28f, 28f, 28f, 28f), (x, y, ignoredDistance, xn, yn) =>
            {
                float halfW = (w - 1) * 0.5f;
                float halfH = (h - 1) * 0.5f;
                float px = Mathf.Abs(x - halfW);
                float py = Mathf.Abs(y - halfH);
                float edgeX = halfW - px;
                float edgeY = halfH - py;
                float edgeCut = (halfW + halfH - cut - px - py) * inverseSqrtTwo;
                float d = Mathf.Min(edgeX, Mathf.Min(edgeY, edgeCut));

                if (d < -0.7f)
                    return Color.clear;

                Color color;
                if (d < 1.15f)
                {
                    color = outerInk;
                }
                else if (d < 4.65f)
                {
                    // Metallo inciso: chiave di luce alta-sinistra e gola brunita in basso.
                    float key = Mathf.Clamp01(yn * 0.76f + (1f - xn) * 0.24f);
                    float ridge = 1f - Mathf.Abs((d - 2.85f) / 1.8f);
                    Color metal = Color.Lerp(frameShadow, frameLight, key);
                    color = Color.Lerp(metal, frameMid, (1f - Mathf.Clamp01(ridge)) * 0.42f);
                }
                else if (d < 5.85f)
                {
                    color = groove;
                }
                else if (d < 7.1f)
                {
                    color = Color.Lerp(innerLight, frameLight, Mathf.Clamp01((yn - 0.42f) * 0.55f));
                }
                else
                {
                    color = Color.Lerp(bodyBottom, bodyTop, yn);

                    // Bagliore centrale discreto: dà profondità senza effetto neon.
                    float horizontalGlow = Mathf.Clamp01(1f - Mathf.Abs(xn - 0.5f) * 2f);
                    float verticalGlow = Mathf.Clamp01(1f - Mathf.Abs(yn - 0.56f) * 2.7f);
                    color = Color.Lerp(color, accent, horizontalGlow * verticalGlow * 0.075f);

                    // Vignettatura e riflesso alto, entrambi dentro la seconda cornice.
                    if (d < 12f)
                        color = Color.Lerp(color, bodyBottom, (12f - d) / 12f * 0.34f);
                    if (yn > 0.71f)
                        color = Color.Lerp(color, Color.white, (yn - 0.71f) * 0.13f);

                    // Trama arcana quasi invisibile, utile soprattutto sui pulsanti larghi.
                    float weave = Mathf.Sin((xn * 17f + yn * 9f) * Mathf.PI);
                    if (weave > 0.94f && d > 12f)
                        color = Color.Lerp(color, accent, (weave - 0.94f) * 0.32f);
                }

                // Piccoli cristalli incastonati negli angoli tagliati. Essendo nei
                // corner del 9-slice non si deformano quando cambia la larghezza.
                Vector2 pixel = new(x, y);
                Vector2[] crystals =
                {
                    new(11.5f, 11.5f), new(w - 12.5f, 11.5f),
                    new(11.5f, h - 12.5f), new(w - 12.5f, h - 12.5f)
                };
                foreach (Vector2 crystal in crystals)
                {
                    float dx = Mathf.Abs(pixel.x - crystal.x) / 3.8f;
                    float dy = Mathf.Abs(pixel.y - crystal.y) / 3.8f;
                    float diamond = dx + dy;
                    if (diamond < 1f)
                    {
                        float facet = Mathf.Clamp01(0.62f + (crystal.y - y) * 0.09f + (crystal.x - x) * 0.045f);
                        Color gem = Color.Lerp(Scale(accent, 0.34f), Color.Lerp(accent, Color.white, 0.72f), facet);
                        color = Color.Lerp(gem, color, Mathf.Clamp01((diamond - 0.72f) / 0.28f));
                    }
                }

                float alpha = Mathf.Clamp01(d + 0.7f);
                return new Color(color.r, color.g, color.b, alpha);
            }, 0f);
            buttonSprites[variant] = sprite;
            return sprite;
        }

        /// <summary>Applica a un Button il ColorBlock standard del tema (hover chiaro, press scuro).</summary>
        public static void ApplyButtonColors(Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.13f, 1.11f, 1.07f, 1f);
            colors.pressedColor = new Color(0.62f, 0.69f, 0.78f, 1f);
            colors.selectedColor = new Color(1.1f, 1.08f, 1.04f, 1f);
            colors.disabledColor = new Color(0.31f, 0.34f, 0.39f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.11f;
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

        /// <summary>
        /// Emblema di lega: stella a dodici punte con anelli concentrici e cuore a
        /// rombo sfaccettato. È inciso in scala di grigi proprio per essere tinto
        /// col colore del tier (Image.color) senza sporcare le luci.
        /// </summary>
        public static Sprite GetRankCrestSprite()
        {
            if (rankCrestSprite != null)
                return rankCrestSprite;

            const int size = 192;
            rankCrestSprite = BakeSprite("Mmo UI Rank Crest", size, size, Vector4.zero, (x, y, d, xn, yn) =>
            {
                float dx = xn - 0.5f;
                float dy = yn - 0.5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) * 2f;   // 0 al centro, 1 sul bordo
                if (radius > 1f)
                    return Color.clear;

                float angle = Mathf.Atan2(dy, dx);
                // Luce da alto-sinistra: dà volume a raggi, anello e rombo.
                float key = Mathf.Clamp01(0.5f + (-dx + dy) * 1.1f);

                const float ringRadius = 0.62f;
                const float ringHalfWidth = 0.055f;

                float level = 0f;
                float alpha = 0f;

                // Raggi oltre l'anello: quattro lame lunghe sulle cardinali e quattro
                // corte sulle diagonali, luminose alla base e affilate in punta.
                float longLobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 4f);
                float shortLobe = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f + Mathf.PI * 0.5f)), 6f);
                float spikeReach = ringRadius + 0.34f * longLobe + 0.17f * shortLobe;
                if (radius < spikeReach && radius > ringRadius - 0.02f)
                {
                    float t = Mathf.Clamp01((spikeReach - radius) / Mathf.Max(0.001f, spikeReach - ringRadius));
                    level = Mathf.Lerp(0.34f, 0.95f, t * 0.7f + key * 0.3f);
                    // L'opacità segue la distanza dal profilo, non la lunghezza della lama:
                    // così il raggio ha un contorno netto invece di sfumare in un alone.
                    alpha = Mathf.Clamp01((spikeReach - radius) * 26f);
                }

                // Disco interno scuro su cui poggia il rombo.
                if (radius < ringRadius)
                {
                    level = Mathf.Lerp(0.05f, 0.17f, yn);
                    alpha = 1f;
                }

                // Anello metallico bisellato: cresta chiara e gola scura all'interno.
                float ring = Mathf.Abs(radius - ringRadius);
                if (ring < ringHalfWidth)
                {
                    float crest = 1f - ring / ringHalfWidth;
                    level = Mathf.Lerp(0.28f, 1f, key) * (0.45f + crest * 0.55f);
                    alpha = 1f;
                }
                else if (radius < ringRadius && radius > ringRadius - ringHalfWidth * 2f)
                {
                    level = 0.03f;
                    alpha = 1f;
                }

                // Cuore a rombo: smusso perimetrale sfaccettato più tavola centrale
                // luminosa, così legge come una gemma incassata e non come uno scacco.
                float diamond = (Mathf.Abs(dx) + Mathf.Abs(dy)) * 2f;
                const float diamondSize = 0.38f;
                const float tableSize = diamondSize * 0.56f;
                if (diamond < diamondSize)
                {
                    float facet;
                    if (dy > Mathf.Abs(dx))
                        facet = 1f;                     // smusso superiore, piena luce
                    else if (dx < -Mathf.Abs(dy))
                        facet = 0.7f;                   // sinistra
                    else if (dx > Mathf.Abs(dy))
                        facet = 0.34f;                  // destra, in ombra
                    else
                        facet = 0.16f;                  // basso, la faccia più scura

                    if (diamond < tableSize)
                    {
                        // Tavola centrale: quasi piatta, con la luce che scivola da alto-sinistra.
                        float core = 1f - diamond / tableSize;
                        level = Mathf.Lerp(0.5f, 1f, key) * (0.78f + core * 0.22f);
                    }
                    else
                    {
                        level = facet * 0.9f;
                        // Filo luminoso sul contorno della tavola.
                        if (diamond < tableSize + 0.025f)
                            level = Mathf.Lerp(level, 1f, 0.7f);
                    }

                    // Filo luminoso lungo il perimetro del rombo.
                    if (diamond > diamondSize - 0.03f)
                        level = Mathf.Lerp(level, 1f, 0.85f);
                    alpha = 1f;
                }
                else if (diamond < diamondSize + 0.03f)
                {
                    level = 0.02f;   // solco scuro che stacca il rombo dal disco
                    alpha = 1f;
                }

                // Borchie sulle diagonali dell'anello.
                for (int corner = 0; corner < 4; corner++)
                {
                    float studAngle = Mathf.PI * 0.25f + corner * Mathf.PI * 0.5f;
                    float sx = 0.5f + Mathf.Cos(studAngle) * ringRadius * 0.5f;
                    float sy = 0.5f + Mathf.Sin(studAngle) * ringRadius * 0.5f;
                    float dist = Mathf.Sqrt((xn - sx) * (xn - sx) + (yn - sy) * (yn - sy));
                    if (dist < 0.03f)
                    {
                        float t = Mathf.Clamp01((0.03f - dist) / 0.012f);
                        level = Mathf.Lerp(level, Mathf.Lerp(0.4f, 1f, key), t);
                        alpha = 1f;
                    }
                }

                if (alpha <= 0f)
                    return Color.clear;
                // Sfumatura sul bordo esterno per non tagliare l'emblema di netto.
                if (radius > 0.94f)
                    alpha *= 1f - (radius - 0.94f) / 0.06f;
                return new Color(level, level, level, Mathf.Clamp01(alpha));
            }, 0f);
            return rankCrestSprite;
        }

        /// <summary>Stella a cinque punte in bianco: le divisioni si accendono cambiandone il colore.</summary>
        public static Sprite GetStarSprite()
        {
            if (starSprite != null)
                return starSprite;

            const int size = 64;
            starSprite = BakeSprite("Mmo UI Star", size, size, Vector4.zero, (x, y, d, xn, yn) =>
            {
                float dx = xn - 0.5f;
                float dy = yn - 0.54f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                if (radius > 1f)
                    return Color.clear;

                // Profilo a stella: raggio del bordo interpolato per teorema dei seni fra
                // la punta (tip, alla distanza outer) e il vertice interno (inner).
                const float outer = 0.94f;
                const float inner = 0.42f;
                float half = Mathf.PI * 0.2f;                      // mezzo settore = 36°
                float angle = Mathf.Atan2(dx, dy);                 // 0 sulla punta in alto
                float wedge = Mathf.Abs(Mathf.Repeat(angle + half, half * 2f) - half);
                float border = outer * inner * Mathf.Sin(half)
                    / Mathf.Max(0.0001f, outer * Mathf.Sin(wedge) + inner * Mathf.Sin(half - wedge));

                float edge = border - radius;
                if (edge < 0f)
                    return Color.clear;

                // Centro più chiaro delle punte: dà un accenno di volume.
                float level = Mathf.Lerp(0.68f, 1f, Mathf.Clamp01(1f - radius / Mathf.Max(0.01f, border)));
                return new Color(level, level, level, Mathf.Clamp01(edge * 22f));
            }, 0f);
            return starSprite;
        }

        /// <summary>Alone radiale morbido: si mette dietro emblemi e avatar per dare profondità.</summary>
        public static Sprite GetRadialGlowSprite()
        {
            if (radialGlowSprite != null)
                return radialGlowSprite;

            const int size = 128;
            radialGlowSprite = BakeSprite("Mmo UI Radial Glow", size, size, Vector4.zero, (x, y, d, xn, yn) =>
            {
                float dx = xn - 0.5f;
                float dy = yn - 0.5f;
                float radius = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                float alpha = Mathf.Pow(1f - radius, 2.4f);
                return new Color(1f, 1f, 1f, alpha);
            }, 0f);
            return radialGlowSprite;
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
