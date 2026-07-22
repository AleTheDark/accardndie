using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    internal static class BattleUiFactory
    {
        public static Font DefaultFont => AccardND.Battlefield.MmoUiTheme.BodyFont;

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.sprite = MmoUiTheme.GetPanelSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, color.a);
            MmoUiTheme.AddPanelGem((RectTransform)panel.transform, "Panel Crystal", new Vector2(0.5f, 1f), new Vector2(28f, 28f), new Color(0.82f, 0.96f, 1f, 0.78f));
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
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text, fallbackDefaultText: content);
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
            var img = holder.GetComponent<Image>();
            img.sprite = MmoUiTheme.GetButtonSprite(MmoUiTheme.ResolveVariant(color));
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, color.a);
            var button = holder.GetComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null)
                button.onClick.AddListener(onClick);
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);
            Text text = CreateText(holder.transform, "Label", label, fontSize);
            MmoUiTheme.StyleAsTitle(text);
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
                UnityEngine.Object.Destroy(parent.GetChild(index).gameObject);
        }
    }

    [CreateAssetMenu(menuName = "Accard N' Die/UI/Editable Text Override Database", fileName = "EditableTextOverrides")]
    public sealed class EditableTextOverrideDatabase : ScriptableObject
    {
        [SerializeField] private List<EditableTextOverride> entries = new();

        public bool TryGet(string key, out EditableTextOverride entry)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].Key, key, StringComparison.Ordinal))
                {
                    entry = entries[index];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public EditableTextOverride GetOrCreate(string key)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].Key, key, StringComparison.Ordinal))
                    return entries[index];
            }

            var entry = new EditableTextOverride { Key = key };
            entries.Add(entry);
            return entry;
        }
    }

    [Serializable]
    public sealed class EditableTextOverride
    {
        public string Key;
        public bool OverrideText;
        [TextArea(1, 6)] public string Text;
        public bool OverrideLayout = true;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 OffsetMin;
        public Vector2 OffsetMax;
        public Vector2 Pivot;
        public Vector3 LocalScale = Vector3.one;
        public Vector3 LocalEulerAngles;
    }

    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class EditableRuntimeText : MonoBehaviour
    {
        private const string ResourceName = "EditableTextOverrides";
        private static EditableTextOverrideDatabase cachedDatabase;

        [SerializeField] private string key;
        [SerializeField] private string defaultText;
        [SerializeField] private bool captureLateText = true;

        private Text text;
        private bool applied;

        public string Key => key;
        public string DefaultText => defaultText;

        public static EditableRuntimeText Bind(Text target, string explicitKey = null, string fallbackDefaultText = null)
        {
            if (target == null)
                return null;

            var binding = target.GetComponent<EditableRuntimeText>();
            if (binding == null)
                binding = target.gameObject.AddComponent<EditableRuntimeText>();

            binding.text = target;
            binding.key = string.IsNullOrWhiteSpace(explicitKey)
                ? BuildKey(target.transform)
                : explicitKey.Trim();

            if (!string.IsNullOrEmpty(fallbackDefaultText))
                binding.defaultText = fallbackDefaultText;
            else if (!string.IsNullOrEmpty(target.text))
                binding.defaultText = target.text;

            binding.ApplyOverrides();
            return binding;
        }

        private void OnEnable()
        {
            text = GetComponent<Text>();
            if (string.IsNullOrWhiteSpace(key))
                key = BuildKey(transform);

            ApplyOverrides();
        }

        private void LateUpdate()
        {
            if (!captureLateText || text == null || !string.IsNullOrEmpty(defaultText) || string.IsNullOrEmpty(text.text))
                return;

            defaultText = text.text;
        }

        public void ApplyOverrides()
        {
            if (applied || string.IsNullOrWhiteSpace(key))
                return;

            EditableTextOverrideDatabase database = LoadDatabase();
            if (database == null || !database.TryGet(key, out EditableTextOverride entry))
                return;

            text ??= GetComponent<Text>();
            if (entry.OverrideText && text != null)
                text.text = entry.Text ?? string.Empty;

            if (entry.OverrideLayout && transform is RectTransform rect)
            {
                rect.anchorMin = entry.AnchorMin;
                rect.anchorMax = entry.AnchorMax;
                rect.offsetMin = entry.OffsetMin;
                rect.offsetMax = entry.OffsetMax;
                rect.pivot = entry.Pivot;
                rect.localScale = entry.LocalScale == Vector3.zero ? Vector3.one : entry.LocalScale;
                rect.localEulerAngles = entry.LocalEulerAngles;
            }

            applied = true;
        }

        private static EditableTextOverrideDatabase LoadDatabase()
        {
            if (cachedDatabase == null)
                cachedDatabase = Resources.Load<EditableTextOverrideDatabase>(ResourceName);

            return cachedDatabase;
        }

        private static string BuildKey(Transform target)
        {
            var builder = new StringBuilder(target.name);
            Transform parent = target.parent;
            while (parent != null)
            {
                builder.Insert(0, parent.name + "/");
                parent = parent.parent;
            }

            return builder.ToString();
        }
    }
}
