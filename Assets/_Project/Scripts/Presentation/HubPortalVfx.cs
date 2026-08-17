using AccardND.Battlefield;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    /// <summary>
    /// Scintillio UI leggero per i portali dell'hub. Tutti gli elementi sono
    /// procedurali e non intercettano i raycast del pulsante sottostante.
    /// </summary>
    internal sealed class HubPortalVfx : MonoBehaviour
    {
        private sealed class Spark
        {
            public RectTransform Rect;
            public Image Image;
            public float Seed;
            public float Speed;
            public float Scale;
        }

        private RectTransform root;
        private Canvas canvas;
        private Image glow;
        private Spark[] sparks;
        private Color tint;
        private float glowStrength = 1f;
        private float startedAt;

        public static HubPortalVfx Attach(Button button, Color color, int sparkCount = 12, float glowStrength = 1f)
        {
            if (button == null)
                return null;

            var holder = new GameObject(
                "Portal Sparkle VFX",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(HubPortalVfx));
            holder.transform.SetParent(button.transform, false);

            RectTransform rect = (RectTransform)holder.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            CanvasGroup group = holder.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            HubPortalVfx effect = holder.GetComponent<HubPortalVfx>();
            effect.root = rect;
            effect.tint = color;
            effect.glowStrength = Mathf.Clamp01(glowStrength);
            effect.startedAt = Time.unscaledTime;
            effect.Build(Mathf.Max(10, sparkCount));
            return effect;
        }

        private void Build(int sparkCount)
        {
            canvas = root.GetComponentInParent<Canvas>();

            var glowObject = new GameObject("Portal Glow", typeof(RectTransform), typeof(Image));
            glowObject.transform.SetParent(root, false);
            RectTransform glowRect = (RectTransform)glowObject.transform;
            glowRect.anchorMin = new Vector2(-0.12f, -0.18f);
            glowRect.anchorMax = new Vector2(1.12f, 1.18f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            glow = glowObject.GetComponent<Image>();
            glow.sprite = MmoUiTheme.GetRadialGlowSprite();
            glow.raycastTarget = false;

            sparks = new Spark[sparkCount];
            for (int index = 0; index < sparks.Length; index++)
            {
                var sparkObject = new GameObject(
                    $"Portal Spark {index + 1}",
                    typeof(RectTransform),
                    typeof(Image));
                sparkObject.transform.SetParent(root, false);

                RectTransform sparkRect = (RectTransform)sparkObject.transform;
                sparkRect.anchorMin = sparkRect.anchorMax = new Vector2(0.5f, 0.5f);
                sparkRect.pivot = new Vector2(0.5f, 0.5f);

                float seed = Hash01(index * 37 + 11);
                float size = Mathf.Lerp(5f, 11f, Hash01(index * 19 + 5));
                sparkRect.sizeDelta = new Vector2(size, size);

                Image sparkImage = sparkObject.GetComponent<Image>();
                sparkImage.sprite = MmoUiTheme.GetRadialGlowSprite();
                sparkImage.preserveAspect = true;
                sparkImage.raycastTarget = false;

                sparks[index] = new Spark
                {
                    Rect = sparkRect,
                    Image = sparkImage,
                    Seed = seed,
                    Speed = Mathf.Lerp(0.6f, 1.15f, Hash01(index * 23 + 3)),
                    Scale = Mathf.Lerp(0.9f, 1.65f, Hash01(index * 29 + 17))
                };
            }
        }

        private void Update()
        {
            if (root == null || glow == null || sparks == null)
            {
                enabled = false;
                return;
            }

            // Venti scintille per portale, otto portali: animarle mentre l'hub
            // sta sotto un modale significa ricostruire il canvas a vuoto.
            if (!UiVfxBudget.ShouldAnimate(root, canvas))
                return;

            float time = Time.unscaledTime;
            float lifetime = time - startedAt;
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lifetime / 0.55f));
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2.15f);

            Color glowColor = tint;
            glowColor.a = Mathf.Lerp(0.12f, 0.3f, breathe) * appear * glowStrength;
            glow.color = glowColor;
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.05f, breathe);

            float width = Mathf.Max(1f, root.rect.width);
            float height = Mathf.Max(1f, root.rect.height);
            for (int index = 0; index < sparks.Length; index++)
            {
                Spark spark = sparks[index];
                if (spark == null || spark.Rect == null || spark.Image == null)
                    continue;

                float phase = Mathf.Repeat(time * spark.Speed * 0.34f + spark.Seed, 1f);
                float side = Hash01(index * 41 + 7) < 0.5f ? -1f : 1f;
                float x = side * width * Mathf.Lerp(0.2f, 0.48f, Hash01(index * 31 + 9));
                x += Mathf.Sin(time * (0.8f + spark.Seed) + spark.Seed * 13f) * width * 0.075f;
                float y = Mathf.Lerp(-0.46f, 0.46f, phase) * height;
                y += Mathf.Sin(time * 1.9f + spark.Seed * 21f) * height * 0.1f;
                spark.Rect.anchoredPosition = new Vector2(x, y);
                spark.Rect.localEulerAngles = new Vector3(0f, 0f, time * 42f + spark.Seed * 360f);

                float pulse = Mathf.Sin(phase * Mathf.PI);
                float twinkle = Mathf.Pow(
                    0.5f + 0.5f * Mathf.Sin(time * 4.2f + spark.Seed * 27f),
                    3f);
                spark.Rect.localScale = Vector3.one * spark.Scale * Mathf.Lerp(0.78f, 1.38f, twinkle);

                Color sparkColor = Color.Lerp(Color.white, tint, 0.78f);
                sparkColor.a = pulse * Mathf.Lerp(0.42f, 1f, twinkle) * appear;
                spark.Image.color = sparkColor;
            }
        }

        private static float Hash01(int value)
        {
            float hashed = Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f;
            return hashed - Mathf.Floor(hashed);
        }
    }
}
