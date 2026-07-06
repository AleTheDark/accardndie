using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>Helper per UI generata da codice, con look fantasy competitivo coerente.</summary>
    internal static class PvpUiFactory
    {
        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static Sprite cachedPanelSprite;
        private static Sprite cachedSoftPanelSprite;
        private static Sprite cachedDiamondSprite;

        public static readonly Color Ink = new(0.018f, 0.026f, 0.04f, 0.98f);
        public static readonly Color Panel = new(0.045f, 0.068f, 0.1f, 0.96f);
        public static readonly Color PanelBright = new(0.075f, 0.105f, 0.145f, 0.96f);
        public static readonly Color Gold = new(1f, 0.78f, 0.28f, 1f);
        public static readonly Color Copper = new(0.82f, 0.42f, 0.18f, 1f);
        public static readonly Color Arcane = new(0.2f, 0.72f, 0.82f, 1f);
        public static readonly Color Violet = new(0.42f, 0.22f, 0.72f, 1f);
        public static readonly Color Good = new(0.36f, 0.95f, 0.55f, 1f);
        public static readonly Color Bad = new(1f, 0.36f, 0.32f, 1f);
        public static readonly Color TextMuted = new(0.68f, 0.78f, 0.88f, 1f);

        private const string ButtonResourceRoot = "UI/MMORPGButtons/";

        public static Sprite GetPanelSprite()
        {
            if (cachedPanelSprite != null)
                return cachedPanelSprite;

            Texture2D val = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "Pvp Runtime Rounded UI Panel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] array = new Color32[1024];
            Color baseBottom = new Color(0.04f, 0.06f, 0.10f); // #0a101a
            Color baseTop = new Color(0.08f, 0.12f, 0.18f);    // #141f2e
            Color goldPeak = new Color(0.85f, 0.65f, 0.18f);   // #d9a62e
            Color goldShadow = new Color(0.45f, 0.32f, 0.08f); // #735214
            Color bevelLight = new Color(0.18f, 0.25f, 0.35f); // #2e4059
            Color shadowGroove = new Color(0.01f, 0.02f, 0.04f); // #03050a

            for (int i = 0; i < 32; i++)
            {
                float grad = (float)i / 31.0f;
                for (int j = 0; j < 32; j++)
                {
                    float cx = j - 15.5f;
                    float cy = i - 15.5f;
                    float ax = Mathf.Abs(cx);
                    float ay = Mathf.Abs(cy);

                    float d_edge;
                    if (ax > 8.5f && ay > 8.5f)
                    {
                        float dist = Mathf.Sqrt((ax - 8.5f) * (ax - 8.5f) + (ay - 8.5f) * (ay - 8.5f));
                        d_edge = 7.0f - dist;
                    }
                    else
                    {
                        d_edge = 15.5f - Mathf.Max(ax, ay);
                    }

                    Color color;
                    if (d_edge < 0.0f)
                    {
                        color = Color.clear;
                    }
                    else if (d_edge < 1.0f)
                    {
                        float t = d_edge;
                        Color col = Color.Lerp(goldShadow, goldPeak, t);
                        color = new Color(col.r, col.g, col.b, t);
                    }
                    else if (d_edge < 2.0f)
                    {
                        float t = d_edge - 1.0f;
                        color = Color.Lerp(goldPeak, goldShadow, t);
                    }
                    else if (d_edge < 3.0f)
                    {
                        float t = d_edge - 2.0f;
                        color = Color.Lerp(shadowGroove, bevelLight, t);
                    }
                    else if (d_edge < 4.0f)
                    {
                        float t = d_edge - 3.0f;
                        Color bodyBase = Color.Lerp(baseBottom, baseTop, grad);
                        color = Color.Lerp(bevelLight, bodyBase, t);
                    }
                    else
                    {
                        color = Color.Lerp(baseBottom, baseTop, grad);
                    }

                    array[i * 32 + j] = color;
                }
            }
            val.SetPixels32(array);
            val.Apply(false, true);
            cachedPanelSprite = Sprite.Create(val, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(9f, 9f, 9f, 9f));
            cachedPanelSprite.name = "Pvp Runtime Rounded UI Panel";
            cachedPanelSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedPanelSprite;
        }

        public static Sprite GetSoftPanelSprite()
        {
            if (cachedSoftPanelSprite != null)
                return cachedSoftPanelSprite;

            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "Pvp Runtime Soft UI Panel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[1024];
            Color rim = new Color(0.32f, 0.45f, 0.55f, 1f);
            Color bodyBottom = new Color(0.018f, 0.028f, 0.045f, 1f);
            Color bodyTop = new Color(0.08f, 0.115f, 0.16f, 1f);
            for (int y = 0; y < 32; y++)
            {
                float grad = y / 31f;
                for (int x = 0; x < 32; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, 31 - x), Mathf.Min(y, 31 - y));
                    Color body = Color.Lerp(bodyBottom, bodyTop, grad);
                    Color color = edge < 1f
                        ? new Color(rim.r, rim.g, rim.b, Mathf.Clamp01(edge))
                        : edge < 3f ? Color.Lerp(rim, body, (edge - 1f) * 0.5f) : body;
                    pixels[y * 32 + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cachedSoftPanelSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(7f, 7f, 7f, 7f));
            cachedSoftPanelSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedSoftPanelSprite;
        }

        public static Sprite GetDiamondSprite()
        {
            if (cachedDiamondSprite != null)
                return cachedDiamondSprite;

            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = "Pvp Runtime Diamond Glyph",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[1024];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Abs(x - 15.5f) / 15.5f;
                    float dy = Mathf.Abs(y - 15.5f) / 15.5f;
                    float dist = dx + dy;
                    float alpha = Mathf.Clamp01((1.02f - dist) * 6f);
                    Color color = Color.Lerp(Copper, Gold, 1f - dist * 0.7f);
                    pixels[y * 32 + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cachedDiamondSprite = Sprite.Create(texture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 100f);
            cachedDiamondSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedDiamondSprite;
        }

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

            var outline = holder.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var shadow = holder.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(2f, -2f);

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
            
            var img = holder.GetComponent<Image>();
            string variant = ResolveButtonVariant(color);
            ButtonAssetKind assetKind = ResolveButtonAssetKind(label);
            Sprite normalSprite = assetKind == ButtonAssetKind.Plain ? null : LoadButtonSprite(variant, "normal", assetKind);
            Sprite hoverSprite = assetKind == ButtonAssetKind.Plain ? null : LoadButtonSprite(variant, "hover", assetKind);
            Sprite pressedSprite = assetKind == ButtonAssetKind.Plain ? null : LoadButtonSprite(variant, "pressed", assetKind);
            Sprite disabledSprite = assetKind == ButtonAssetKind.Plain ? null : LoadButtonSprite(variant, "disabled", assetKind);
            img.sprite = normalSprite != null ? normalSprite : GetSoftPanelSprite();
            img.type = Image.Type.Sliced;
            img.color = normalSprite != null
                ? new Color(1f, 1f, 1f, color.a)
                : new Color(Mathf.Min(1f, color.r * 1.1f), Mathf.Min(1f, color.g * 1.1f), Mathf.Min(1f, color.b * 1.1f), color.a);

            var button = holder.GetComponent<Button>();
            button.targetGraphic = img;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            if (normalSprite != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = hoverSprite,
                    pressedSprite = pressedSprite,
                    selectedSprite = hoverSprite,
                    disabledSprite = disabledSprite
                };
            }
            else
            {
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.22f, 1.18f, 1.08f, 1f);
                colors.pressedColor = new Color(0.72f, 0.78f, 0.86f, 1f);
                colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }

            Text text = CreateText(holder.transform, "Label", label, fontSize);
            Stretch((RectTransform)text.transform, 6f, 2f);
            return button;
        }

        private static Sprite LoadButtonSprite(string variant, string state, ButtonAssetKind kind = ButtonAssetKind.Long)
        {
            string prefix = kind switch
            {
                ButtonAssetKind.Small => "mmorpg_small_button",
                _ => "mmorpg_button"
            };
            return Resources.Load<Sprite>($"{ButtonResourceRoot}{prefix}_{variant}_{state}");
        }

        private static ButtonAssetKind ResolveButtonAssetKind(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return ButtonAssetKind.Long;
            string trimmed = label.Trim();
            if (trimmed == "X")
                return ButtonAssetKind.Plain;
            if (trimmed == "<<" || trimmed == ">>")
                return ButtonAssetKind.Small;
            return trimmed.Length <= 8 ? ButtonAssetKind.Small : ButtonAssetKind.Long;
        }

        private enum ButtonAssetKind
        {
            Long,
            Small,
            Plain
        }

        private static string ResolveButtonVariant(Color color)
        {
            if (color.r > 0.35f && color.g < 0.23f && color.b < 0.23f)
                return "crimson";
            if (color.g > color.r * 1.35f && color.g > color.b * 1.1f)
                return "emerald";
            if (color.r > 0.16f && color.b > color.g * 1.08f)
                return "violet";
            if (color.r > 0.38f && color.g > 0.24f && color.b < 0.15f)
                return "gold";
            return "arcane";
        }

        public static RectTransform CreateTitleBand(Transform parent, string title, string subtitle = null)
        {
            RectTransform band = CreateSoftPanel(parent, "Title Band", new Color(0.02f, 0.035f, 0.055f, 0.86f));
            Image leftGlyph = CreateGlyph(band, "Left Glyph", Gold);
            SetAnchors(leftGlyph.rectTransform, new Vector2(0.02f, 0.18f), new Vector2(0.065f, 0.82f));
            Image rightGlyph = CreateGlyph(band, "Right Glyph", Gold);
            SetAnchors(rightGlyph.rectTransform, new Vector2(0.935f, 0.18f), new Vector2(0.98f, 0.82f));

            Text titleText = CreateText(band, "Title", title, 36, TextAnchor.MiddleCenter);
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
            Text label = CreateText(holder, "Title", title, 18, TextAnchor.MiddleLeft);
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

        public static RectTransform CreateProgressBar(Transform parent, string name, float value, Color fillColor)
        {
            RectTransform track = CreateSoftPanel(parent, name, new Color(0.018f, 0.026f, 0.04f, 0.96f));
            RectTransform fill = CreateSoftPanel(track, "Fill", fillColor);
            SetAnchors(fill, Vector2.zero, new Vector2(Mathf.Clamp01(value), 1f));
            return track;
        }

        public static Text CreateBadge(Transform parent, string name, string label, Color color, int fontSize = 15)
        {
            RectTransform badge = CreateSoftPanel(parent, name, color);
            Text text = CreateText(badge, "Label", label, fontSize);
            text.color = Color.white;
            Stretch((RectTransform)text.transform, 6f, 2f);
            return text;
        }

        public static Image CreateGlyph(Transform parent, string name, Color color)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            var image = holder.GetComponent<Image>();
            image.sprite = GetDiamondSprite();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
