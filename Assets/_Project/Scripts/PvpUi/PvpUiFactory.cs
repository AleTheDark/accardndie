using AccardND.Battlefield;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>Helper per UI generata da codice, con look fantasy competitivo coerente (tema MmoUiTheme).</summary>
    internal static class PvpUiFactory
    {
        public static Font DefaultFont => MmoUiTheme.BodyFont;

        public static readonly Color Ink = new(0.004f, 0.005f, 0.007f, 0.99f);
        public static readonly Color Panel = new(0.012f, 0.014f, 0.017f, 0.98f);
        public static readonly Color PanelBright = new(0.055f, 0.045f, 0.04f, 0.97f);
        public static readonly Color Gold = new(0.92f, 0.72f, 0.38f, 1f);
        public static readonly Color Copper = new(0.66f, 0.39f, 0.2f, 1f);
        public static readonly Color Arcane = new(0.36f, 0.7f, 0.84f, 1f);
        public static readonly Color Violet = new(0.62f, 0.34f, 0.9f, 1f);
        public static readonly Color Good = MmoUiTheme.Good;
        public static readonly Color Bad = MmoUiTheme.Bad;
        public static readonly Color TextMuted = new(0.77f, 0.72f, 0.63f, 1f);

        public static Sprite GetPanelSprite() => MmoUiTheme.GetPanelSprite();

        public static Sprite GetSoftPanelSprite() => MmoUiTheme.GetSoftPanelSprite();

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var img = panel.GetComponent<Image>();
            img.sprite = GetPanelSprite();
            img.type = Image.Type.Sliced;
            // Preserving original alpha but using a brightened tint so the custom gold borders render beautifully!
            img.color = new Color(Mathf.Min(1f, color.r * 2f), Mathf.Min(1f, color.g * 2f), Mathf.Min(1f, color.b * 2f), color.a);
            return (RectTransform)panel.transform;
        }

        public static RectTransform CreateSoftPanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var img = panel.GetComponent<Image>();
            img.sprite = GetSoftPanelSprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            return (RectTransform)panel.transform;
        }

        /// <summary>
        /// Contenitore di solo layout: non aggiunge immagini o veli scuri.
        /// Le schermate con Screen Outer Frame usano il fondale centralizzato
        /// creato da <see cref="CreateScreenOuterFrame"/>.
        /// </summary>
        public static RectTransform CreateContainer(Transform parent, string name)
        {
            var container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            return (RectTransform)container.transform;
        }

        public static Text CreateText(
            Transform parent, string name, string content, int size,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Text));
            holder.transform.SetParent(parent, false);
            var text = holder.GetComponent<Text>();
            int resolvedSize = ResolveFontSize(parent, size);
            text.font = DefaultFont;
            text.fontSize = resolvedSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(11, resolvedSize - 10);
            text.resizeTextMaxSize = resolvedSize;
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text, fallbackDefaultText: content);

            var outline = holder.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var shadow = holder.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        private static int ResolveFontSize(Transform parent, int requestedSize)
        {
            bool insideLargeScreen = false;
            for (Transform current = parent; current != null; current = current.parent)
            {
                if (current.name == "Lobby" || current.name == "Classifica")
                {
                    insideLargeScreen = true;
                    break;
                }
            }

            if (!insideLargeScreen)
                return requestedSize;

            float multiplier = requestedSize <= 18
                ? 1.45f
                : requestedSize <= 28
                    ? 1.3f
                    : 1.18f;
            return Mathf.RoundToInt(requestedSize * multiplier);
        }

        /// <summary>Testo con il font da titolo (Cinzel): per intestazioni, nomi schermata e bottoni.</summary>
        public static Text CreateTitleText(
            Transform parent, string name, string content, int size,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Text text = CreateText(parent, name, content, size, anchor);
            MmoUiTheme.StyleAsTitle(text);
            return text;
        }

        public static Text CreateLabel(
            Transform parent, string name, string content, int size,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text text = CreateText(parent, name, content, size, anchor, FontStyle.Normal);
            text.color = TextMuted;
            return text;
        }

        public static Button CreateButton(
            Transform parent, string name, string label, Color color, UnityAction onClick, int fontSize = 22)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(parent, false);

            MmoUiTheme.ButtonVariant variant = ResolveButtonVariant(name, label, color);
            var img = holder.GetComponent<Image>();
            img.sprite = MmoUiTheme.GetButtonSprite(variant);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = new Color(1f, 1f, 1f, color.a);

            var button = holder.GetComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);

            Text text = CreateTitleText(holder.transform, "Label", label, fontSize);
            text.color = Color.Lerp(new Color(0.94f, 0.9f, 0.82f, 1f), MmoUiTheme.AccentOf(variant), 0.1f);
            Stretch((RectTransform)text.transform, 10f, 2f);
            if (MmoUiTheme.IsBackButton(name, label))
                MmoUiTheme.ApplyBackButtonStyle(button, text);
            else if (MmoUiTheme.IsLightButton(name, label))
                MmoUiTheme.ApplyLightButtonStyle(button, text);
            return button;
        }

        private static MmoUiTheme.ButtonVariant ResolveButtonVariant(string name, string label, Color color)
        {
            string semanticName = (name ?? string.Empty).ToUpperInvariant();
            if (semanticName.Contains("CANCEL") || semanticName.Contains("CLOSE") || semanticName.Contains("BACK") ||
                semanticName.Contains("RETURN") || semanticName.Contains("REMOVE") || semanticName.Contains("DECLINE"))
                return MmoUiTheme.ButtonVariant.Crimson;
            if (semanticName.Contains("CONFIRM") || semanticName.Contains("SAVE") || semanticName.Contains("CONTINUE") ||
                semanticName.Contains("ACCEPT") || semanticName.Contains("JOIN"))
                return MmoUiTheme.ButtonVariant.Emerald;
            if (semanticName.Contains("PROFILE") || semanticName.Contains("CHALLENGE") || semanticName.Contains("SEARCH") ||
                semanticName.Contains("QUEUE"))
                return MmoUiTheme.ButtonVariant.Violet;
            if (semanticName.Contains("LOADOUT") || semanticName.Contains("CREATE") || semanticName.Contains("ADD"))
                return MmoUiTheme.ButtonVariant.Gold;

            string value = ((name ?? string.Empty) + " " + (label ?? string.Empty)).ToUpperInvariant();
            if (value.Contains("ANNULLA") || value.Contains("RIFIUTA") || value.Contains("RIMUOVI") || value.Contains("CHIUDI") || value.Contains("INDIETRO") || value.Contains("CANCEL") || value.Contains("CLOSE"))
                return MmoUiTheme.ButtonVariant.Crimson;
            if (value.Contains("CODICE"))
                return MmoUiTheme.ButtonVariant.Arcane;
            if (value.Contains("ACCETTA") || value.Contains("SALVA") || value.Contains("CONFERMA") || value.Contains("CONTINUA") || value.Contains("ENTRA"))
                return MmoUiTheme.ButtonVariant.Emerald;
            if (value.Contains("PROFILO") || value.Contains("SFIDA") || value.Contains("CERCA") || value.Contains("QUEUE"))
                return MmoUiTheme.ButtonVariant.Violet;
            if (value.Contains("LOADOUT") || value.Contains("CREA") || value.Contains("AGGIUNGI"))
                return MmoUiTheme.ButtonVariant.Gold;
            return MmoUiTheme.ResolveVariant(color);
        }

        public static RectTransform CreateTitleBand(Transform parent, string title, string subtitle = null)
        {
            RectTransform band = CreateSoftPanel(parent, "Title Band", new Color(0.02f, 0.035f, 0.055f, 0.86f));

            Text titleText = CreateTitleText(band, "Title", title, 36, TextAnchor.MiddleCenter);
            titleText.color = Gold;
            SetAnchors((RectTransform)titleText.transform, new Vector2(0.08f, subtitle == null ? 0.08f : 0.34f), new Vector2(0.92f, 0.92f));
            if (!string.IsNullOrEmpty(subtitle))
            {
                Text subtitleText = CreateLabel(band, "Subtitle", subtitle, 16, TextAnchor.MiddleCenter);
                SetAnchors((RectTransform)subtitleText.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.36f));
            }
            return band;
        }

        /// <summary>
        /// Intestazione condivisa dalle schermate di primo livello.
        /// </summary>
        public static RectTransform CreateScreenTitlePanel(
            Transform parent,
            string name,
            string title,
            string subtitle = null,
            int titleSize = 40)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);

            Image plaque = holder.GetComponent<Image>();
            plaque.sprite = MmoUiTheme.GetScreenTitlePlaqueSprite();
            plaque.type = Image.Type.Simple;
            plaque.preserveAspect = false;
            plaque.color = Color.white;
            plaque.raycastTarget = false;

            Text titleText = CreateTitleText(
                holder.transform, "Title", title, titleSize, TextAnchor.MiddleCenter);
            MmoUiTheme.StyleAsScreenTitle(titleText);
            titleText.color = Gold;
            SetAnchors(
                (RectTransform)titleText.transform,
                new Vector2(0.08f, string.IsNullOrEmpty(subtitle) ? 0.18f : 0.29f),
                new Vector2(0.92f, string.IsNullOrEmpty(subtitle) ? 0.72f : 0.64f));
            titleText.rectTransform.offsetMin = new Vector2(0f, -23f);
            titleText.rectTransform.offsetMax = new Vector2(0f, -23f);

            if (!string.IsNullOrEmpty(subtitle))
            {
                Text subtitleText = CreateText(
                    holder.transform, "Subtitle", subtitle, 17, TextAnchor.MiddleCenter, FontStyle.Normal);
                subtitleText.color = new Color(0.9f, 0.82f, 0.66f, 1f);
                SetAnchors(
                    (RectTransform)subtitleText.transform,
                    new Vector2(0.08f, 0.08f),
                    new Vector2(0.92f, 0.31f));
            }

            return (RectTransform)holder.transform;
        }

        public static RectTransform CreateScreenOuterFrame(
            Transform parent, float topAnchor, Transform firstForeground = null)
        {
            var backgroundObject = new GameObject(
                "Screen Inner Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(parent, false);

            Image background = backgroundObject.GetComponent<Image>();
            background.sprite = GetSoftPanelSprite();
            background.type = Image.Type.Sliced;
            background.color = new Color(0.004f, 0.005f, 0.008f, 0.72f);
            background.raycastTarget = true;
            SetAnchors(
                (RectTransform)backgroundObject.transform,
                new Vector2(0.018f, 0.018f),
                new Vector2(0.982f, topAnchor - 0.01f));

            // Nella Lobby il contenuto esiste già quando viene creata la cornice:
            // il fondale comune deve stare sotto di esso, mentre la cornice resta sopra.
            if (firstForeground != null && firstForeground.parent == parent)
                backgroundObject.transform.SetSiblingIndex(firstForeground.GetSiblingIndex());

            var holder = new GameObject("Screen Outer Frame", typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);

            Image frame = holder.GetComponent<Image>();
            MmoUiTheme.ApplyScreenOuterFrame(frame);
            SetAnchors(
                (RectTransform)holder.transform,
                new Vector2(0.008f, 0.008f),
                new Vector2(0.992f, topAnchor));
            return (RectTransform)holder.transform;
        }

        public static Text CreateSectionHeader(Transform parent, string title, string value = null)
        {
            RectTransform holder = CreateSoftPanel(parent, "Section Header", new Color(0.03f, 0.05f, 0.075f, 0.88f));
            var element = holder.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 48f;
            Text label = CreateTitleText(holder, "Title", title, 18, TextAnchor.MiddleLeft);
            label.color = Gold;
            SetAnchors((RectTransform)label.transform, new Vector2(0.025f, 0f), new Vector2(value == null ? 0.98f : 0.68f, 1f));
            if (!string.IsNullOrEmpty(value))
            {
                Text right = CreateLabel(holder, "Value", value, 16, TextAnchor.MiddleRight);
                right.color = Arcane;
                SetAnchors((RectTransform)right.transform, new Vector2(0.68f, 0f), new Vector2(0.975f, 1f));
            }
            return label;
        }

        public static Text CreateBadge(Transform parent, string name, string label, Color color, int fontSize = 15)
        {
            RectTransform badge = CreateSoftPanel(parent, name, color);
            Text text = CreateText(badge, "Label", label, fontSize);
            text.color = Color.white;
            Stretch((RectTransform)text.transform, 6f, 2f);
            return text;
        }

        /// <summary>Testo per numeri e valori: usa il bold reale del font di lettura.</summary>
        public static Text CreateValueText(
            Transform parent, string name, string content, int size,
            TextAnchor anchor = TextAnchor.MiddleRight)
        {
            Text text = CreateText(parent, name, content, size, anchor, FontStyle.Normal);
            text.font = MmoUiTheme.BodyBoldFont;
            return text;
        }

        /// <summary>
        /// Colore identitario della lega, dal grigio acciaio dei Nabbo all'oro degli
        /// Onnipotenti. Tier sconosciuto o assente = acciaio spento.
        /// </summary>
        public static Color TierAccent(string tier)
        {
            string key = (tier ?? string.Empty).Trim().ToUpperInvariant();
            return key switch
            {
                "NABBO" => new Color(0.62f, 0.7f, 0.78f, 1f),
                "APPRENDISTA" => Copper,
                "ESPERTO" => Arcane,
                "DIVINO" => Violet,
                "DIAMANTE" => Violet,
                "ONNIPOTENTE" => Gold,
                _ => new Color(0.48f, 0.56f, 0.64f, 1f)
            };
        }

        public static Sprite RankEmblem(string tier)
        {
            string key = (tier ?? string.Empty).Trim().ToLowerInvariant();
            string file = key switch
            {
                "nabbo" => "rank_nabbo_v1",
                "apprendista" or "bronze" or "bronzo" => "rank_apprendista_v1",
                "platino" or "platinum" => "rank_platino_v1",
                "esperto" or "diamond" or "diamante" => "rank_esperto_v1",
                "gold" or "oro" => "rank_gold_v1",
                "divino" => "rank_divino_v1",
                "onnipotente" or "master" => "rank_onnipotente_v1",
                _ => "rank_nabbo_v1"
            };
            return Resources.Load<Sprite>($"UI/MultiplayerRestyle/Ranks/{file}");
        }

        /// <summary>Alone radiale dietro emblemi e ritratti: puro decoro, non intercetta il tocco.</summary>
        public static Image CreateGlow(Transform parent, string name, Color tint)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            var image = holder.GetComponent<Image>();
            image.sprite = MmoUiTheme.GetRadialGlowSprite();
            image.color = tint;
            image.raycastTarget = false;
            Stretch((RectTransform)holder.transform);
            return image;
        }

        /// <summary>Barra di avanzamento con binario inciso, riempimento tinto ed etichetta sovrapposta.</summary>
        public sealed class ProgressBar
        {
            public RectTransform Root;
            public RectTransform Fill;
            public Text Label;

            /// <summary>Imposta il riempimento (0-1) e il testo mostrato al centro.</summary>
            public void SetValue(float normalized, string label)
            {
                if (Fill != null)
                {
                    float clamped = Mathf.Clamp01(normalized);
                    Fill.anchorMin = new Vector2(0f, 0f);
                    // Un filo di riempimento resta sempre visibile: comunica "barra viva, valore zero".
                    Fill.anchorMax = new Vector2(Mathf.Max(clamped, 0.015f), 1f);
                }
                if (Label != null)
                    Label.text = label ?? string.Empty;
            }
        }

        public static ProgressBar CreateProgressBar(
            Transform parent, string name, Color fillColor, int fontSize = 16)
        {
            RectTransform track = CreateSoftPanel(parent, name, new Color(0.012f, 0.02f, 0.032f, 0.88f));

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(track, false);
            var fillImage = fillObject.GetComponent<Image>();
            // Riempimento pieno senza sprite: con la cornice del pannello leggerebbe
            // come un secondo riquadro invece che come una barra.
            fillImage.color = fillColor;
            fillImage.raycastTarget = false;
            var fill = (RectTransform)fillObject.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.015f, 1f);
            // Il riempimento resta dentro la cornice del binario.
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);

            Text label = CreateValueText(track, "Label", string.Empty, fontSize, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            Stretch((RectTransform)label.transform, 8f, 1f);

            return new ProgressBar { Root = track, Fill = fill, Label = label };
        }

        /// <summary>Riga statistica: il rettangolo per posizionarla e il testo del valore da aggiornare.</summary>
        public sealed class StatRow
        {
            public RectTransform Root;
            public Text Caption;
            public Text Value;
        }

        /// <summary>
        /// Riga "etichetta a sinistra, valore a destra". Ha già un LayoutElement per
        /// stare in un layout verticale, ma si può anche ancorare a mano.
        /// </summary>
        public static StatRow CreateStatRow(
            Transform parent, string name, string label, string value, float height = 46f)
        {
            RectTransform row = CreateSoftPanel(parent, name, new Color(0.022f, 0.036f, 0.055f, 0.85f));
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1f;

            Text caption = CreateLabel(row, "Caption", label, 18, TextAnchor.MiddleLeft);
            SetAnchors((RectTransform)caption.transform, new Vector2(0.04f, 0.05f), new Vector2(0.62f, 0.95f));

            Text valueText = CreateValueText(row, "Value", value, 22);
            valueText.color = Color.white;
            SetAnchors((RectTransform)valueText.transform, new Vector2(0.62f, 0.05f), new Vector2(0.96f, 0.95f));

            var separatorObject = new GameObject("Gold Separator", typeof(RectTransform), typeof(Image));
            separatorObject.transform.SetParent(row, false);
            Image separator = separatorObject.GetComponent<Image>();
            separator.color = new Color(0.64f, 0.48f, 0.25f, 0.28f);
            separator.raycastTarget = false;
            RectTransform separatorRect = (RectTransform)separatorObject.transform;
            separatorRect.anchorMin = new Vector2(0.03f, 0f);
            separatorRect.anchorMax = new Vector2(0.97f, 0f);
            separatorRect.offsetMin = Vector2.zero;
            separatorRect.offsetMax = new Vector2(0f, 1f);
            return new StatRow { Root = row, Caption = caption, Value = valueText };
        }

        /// <summary>Linguetta di navigazione. Lo stato attivo si applica con <see cref="SetTabActive"/>.</summary>
        public static Button CreateTabButton(
            Transform parent, string name, string label, UnityAction onClick, int fontSize = 26)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(parent, false);

            var img = holder.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;

            var button = holder.GetComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);

            Text text = CreateTitleText(holder.transform, "Label", label, fontSize);
            Stretch((RectTransform)text.transform, 10f, 2f);

            SetTabActive(button, false);
            return button;
        }

        /// <summary>Linguetta attiva: cornice viola piena e testo dorato; spenta: bordo freddo smorzato.</summary>
        public static void SetTabActive(Button tab, bool active)
        {
            if (tab == null)
                return;

            var img = tab.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = MmoUiTheme.GetButtonSprite(
                    active ? MmoUiTheme.ButtonVariant.Violet : MmoUiTheme.ButtonVariant.Gold);
                img.color = active ? Color.white : new Color(0.44f, 0.42f, 0.38f, 0.96f);
            }

            Text label = tab.GetComponentInChildren<Text>();
            if (label != null)
                label.color = active ? new Color(0.98f, 0.87f, 0.64f, 1f) : new Color(0.78f, 0.74f, 0.68f, 1f);
        }

        /// <summary>
        /// Ritratto del giocatore: alone, cornice a pannello e artwork al centro.
        /// Ritorna l'Image del ritratto, da riempire quando arriva l'icona scelta.
        /// </summary>
        public static Image CreateAvatar(Transform parent, string name, Color accent)
        {
            RectTransform frame = CreateSoftPanel(parent, name, new Color(0.05f, 0.08f, 0.12f, 0.88f));
            CreateGlow(frame, "Glow", new Color(accent.r, accent.g, accent.b, 0.18f));

            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(frame, false);
            var portrait = portraitObject.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.enabled = false;
            SetAnchors((RectTransform)portraitObject.transform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
            return portrait;
        }

        /// <summary>Campo di testo a tema, con placeholder e limite di caratteri.</summary>
        public static InputField CreateInputField(
            Transform parent, string name, string placeholder, int characterLimit, int fontSize = 22)
        {
            RectTransform panel = CreateSoftPanel(parent, name, new Color(0.06f, 0.09f, 0.13f, 0.98f));
            var field = panel.gameObject.AddComponent<InputField>();
            field.characterLimit = characterLimit;

            Text text = CreateText(panel, "Input Text", string.Empty, fontSize, TextAnchor.MiddleLeft, FontStyle.Normal);
            text.color = Color.white;
            text.resizeTextForBestFit = false;
            SetAnchors((RectTransform)text.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
            field.textComponent = text;

            Text hint = CreateLabel(panel, "Placeholder", placeholder, fontSize, TextAnchor.MiddleLeft);
            hint.resizeTextForBestFit = false;
            SetAnchors((RectTransform)hint.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
            field.placeholder = hint;
            return field;
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rect, float horizontalPadding = 0f, float verticalPadding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
            rect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
        }

        public static void Clear(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                Object.Destroy(parent.GetChild(index).gameObject);
        }
    }
}
