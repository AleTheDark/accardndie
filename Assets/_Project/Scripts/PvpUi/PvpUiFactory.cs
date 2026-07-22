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

        public static readonly Color Ink = MmoUiTheme.Ink;
        public static readonly Color Panel = MmoUiTheme.Panel;
        public static readonly Color PanelBright = MmoUiTheme.PanelBright;
        public static readonly Color Gold = MmoUiTheme.Gold;
        public static readonly Color Copper = MmoUiTheme.Copper;
        public static readonly Color Arcane = MmoUiTheme.Arcane;
        public static readonly Color Violet = MmoUiTheme.Violet;
        public static readonly Color Good = MmoUiTheme.Good;
        public static readonly Color Bad = MmoUiTheme.Bad;
        public static readonly Color TextMuted = MmoUiTheme.TextMuted;

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
            MmoUiTheme.AddPanelGem((RectTransform)panel.transform, "Top Crystal", new Vector2(0.5f, 1f), new Vector2(34f, 34f), Color.white);
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
            if (color.a > 0.9f)
                MmoUiTheme.AddPanelGem((RectTransform)panel.transform, "Small Crystal", new Vector2(0.5f, 1f), new Vector2(22f, 22f), new Color(0.75f, 0.95f, 1f, 0.74f));
            return (RectTransform)panel.transform;
        }

        public static Text CreateText(
            Transform parent, string name, string content, int size,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Text));
            holder.transform.SetParent(parent, false);
            var text = holder.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(9, size - 8);
            text.resizeTextMaxSize = size;
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text, fallbackDefaultText: content);

            var outline = holder.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var shadow = holder.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(2f, -2f);

            return text;
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
            text.color = Color.Lerp(Color.white, MmoUiTheme.AccentOf(variant), 0.12f);
            Stretch((RectTransform)text.transform, 10f, 2f);
            return button;
        }

        private static MmoUiTheme.ButtonVariant ResolveButtonVariant(string name, string label, Color color)
        {
            string value = ((name ?? string.Empty) + " " + (label ?? string.Empty)).ToUpperInvariant();
            if (value.Contains("ANNULLA") || value.Contains("RIFIUTA") || value.Contains("RIMUOVI") || value.Contains("CHIUDI") || value.Contains("INDIETRO") || value.Contains("CANCEL") || value.Contains("CLOSE"))
                return MmoUiTheme.ButtonVariant.Crimson;
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
