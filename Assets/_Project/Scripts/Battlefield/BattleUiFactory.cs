using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    internal static class BattleUiFactory
    {
        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return (RectTransform)panel.transform;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int size,
            TextAnchor anchor = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Bold)
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
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Color color,
            UnityAction onClick,
            int fontSize = 22)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(parent, false);
            holder.GetComponent<Image>().color = color;
            var button = holder.GetComponent<Button>();
            if (onClick != null)
                button.onClick.AddListener(onClick);
            Text text = CreateText(holder.transform, "Label", label, fontSize);
            Stretch((RectTransform)text.transform, 6f, 2f);
            return button;
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
