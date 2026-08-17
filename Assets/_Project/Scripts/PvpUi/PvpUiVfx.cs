using AccardND.Battlefield;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// VFX UI leggero e deterministico: usa sprite procedurali gia in memoria,
    /// quindi non introduce video o texture di bagliore statiche.
    /// </summary>
    public sealed class PvpUiVfx : MonoBehaviour
    {
        public static RectTransform CreateDefeatPetals(Transform parent) =>
            PvpMatchResultOverlay.CreateDefeatPetals(parent);

        private enum VfxStyle
        {
            RankedButton,
            PulseButton,
            RankAura
        }

        private sealed class Spark
        {
            public RectTransform Rect;
            public Image Image;
            public float Seed;
            public float Speed;
            public float Radius;
        }

        private VfxStyle style;
        private RectTransform effectRoot;
        private Canvas canvas;
        private Image pulse;
        private RectTransform shimmer;
        private Image shimmerImage;
        private Spark[] sparks;
        private Color tint = Color.white;
        private float intensity = 1f;
        private float startedAt;

        public static PvpUiVfx CreateRankedButton(RectTransform button, Color color)
        {
            PvpUiVfx effect = Create(
                button, "Ranked Button VFX", VfxStyle.RankedButton,
                Vector2.zero, Vector2.one, color, 8);
            effect.effectRoot.SetAsFirstSibling();
            return effect;
        }

        public static PvpUiVfx CreatePulseButton(RectTransform button, Color color)
        {
            PvpUiVfx effect = Create(
                button, "Button Pulse VFX", VfxStyle.PulseButton,
                Vector2.zero, Vector2.one, color, 0);
            effect.effectRoot.SetAsFirstSibling();
            return effect;
        }

        public static PvpUiVfx CreateRankAura(
            Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            return Create(
                parent, "Rank Aura VFX", VfxStyle.RankAura,
                anchorMin, anchorMax, color, 12);
        }

        public void SetTint(Color color, float strength = 1f)
        {
            tint = color;
            intensity = Mathf.Clamp01(strength);

            // Build crea l'Image bianca. Applicare subito la tinta evita il flash bianco
            // prima che Update animi il Pulse aggiunto o clonato a runtime.
            if (pulse != null)
            {
                Color glowColor = tint;
                glowColor.a = (style == VfxStyle.PulseButton ? 0.12f : 0.08f) * intensity;
                pulse.color = glowColor;
                if (style == VfxStyle.PulseButton)
                    pulse.rectTransform.localScale = Vector3.one * 0.96f;
            }
        }

        public void SetSweepScale(Vector3 scale)
        {
            if (shimmer != null)
                shimmer.localScale = scale;
        }

        private static PvpUiVfx Create(
            Transform parent,
            string name,
            VfxStyle style,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            int sparkCount)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(PvpUiVfx));
            holder.transform.SetParent(parent, false);
            RectTransform root = (RectTransform)holder.transform;
            PvpUiFactory.SetAnchors(root, anchorMin, anchorMax);
            holder.GetComponent<CanvasGroup>().blocksRaycasts = false;

            PvpUiVfx effect = holder.GetComponent<PvpUiVfx>();
            effect.style = style;
            effect.effectRoot = root;
            effect.tint = color;
            effect.startedAt = Time.unscaledTime;
            effect.Build(sparkCount);
            return effect;
        }

        private void Build(int sparkCount)
        {
            canvas = effectRoot.GetComponentInParent<Canvas>();

            pulse = CreateImage(
                effectRoot, "Pulse", MmoUiTheme.GetRadialGlowSprite(),
                Vector2.zero, Vector2.one, true);

            if (style == VfxStyle.RankAura)
            {
                // L'emblema di lega viene disegnato dal proprietario dell'aura. Un
                // secondo stemma procedurale qui rimaneva leggibile attraverso le
                // trasparenze dell'artwork e, al refresh dopo un'amichevole, sembrava
                // un badge estraneo sovrapposto al rank.
            }
            else if (style == VfxStyle.RankedButton)
            {
                shimmerImage = CreateImage(
                    effectRoot, "Arcane Sweep", MmoUiTheme.GetRadialGlowSprite(),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), false);
                shimmer = shimmerImage.rectTransform;
            }

            sparks = new Spark[sparkCount];
            for (int index = 0; index < sparkCount; index++)
            {
                var sparkObject = new GameObject(
                    $"Spark {index + 1}", typeof(RectTransform), typeof(Image));
                sparkObject.transform.SetParent(effectRoot, false);
                RectTransform rect = (RectTransform)sparkObject.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                float size = style == VfxStyle.RankAura
                    ? Mathf.Lerp(9f, 18f, Hash01(index * 17 + 3))
                    : Mathf.Lerp(5f, 11f, Hash01(index * 13 + 7));
                rect.sizeDelta = new Vector2(size, size);

                Image image = sparkObject.GetComponent<Image>();
                image.sprite = MmoUiTheme.GetStarSprite();
                image.preserveAspect = true;
                image.raycastTarget = false;

                sparks[index] = new Spark
                {
                    Rect = rect,
                    Image = image,
                    Seed = Hash01(index * 29 + 11),
                    Speed = Mathf.Lerp(0.28f, 0.72f, Hash01(index * 31 + 5)),
                    Radius = Mathf.Lerp(0.32f, 0.48f, Hash01(index * 23 + 19))
                };
            }
        }

        private void Update()
        {
            if (effectRoot == null || pulse == null)
            {
                enabled = false;
                return;
            }

            // Questi bagliori stanno anche su bottoni dentro liste scorrevoli:
            // fuori dallo schermo non c'e' motivo di continuare a pulsare.
            if (!UiVfxBudget.ShouldAnimate(effectRoot, canvas))
                return;

            float time = Time.unscaledTime;
            float breathing = 0.5f + 0.5f * Mathf.Sin(time * 2.15f);
            Color glowColor = tint;
            glowColor.a = (style == VfxStyle.RankAura
                ? Mathf.Lerp(0.045f, 0.13f, breathing)
                : style == VfxStyle.PulseButton
                    ? Mathf.Lerp(0.12f, 0.34f, breathing)
                    : Mathf.Lerp(0.08f, 0.24f, breathing)) * intensity;
            pulse.color = glowColor;
            pulse.rectTransform.localScale = Vector3.one * (style == VfxStyle.PulseButton
                ? Mathf.Lerp(0.96f, 1.09f, breathing)
                : Mathf.Lerp(0.94f, 1.06f, breathing));

            if (style == VfxStyle.RankAura)
                UpdateRankAura(time);
            else if (style == VfxStyle.RankedButton)
                UpdateRankedButton(time);
        }

        private void UpdateRankAura(float time)
        {
            float width = Mathf.Max(1f, effectRoot.rect.width);
            float height = Mathf.Max(1f, effectRoot.rect.height);
            float radiusBase = Mathf.Min(width, height);
            for (int index = 0; index < sparks.Length; index++)
            {
                Spark spark = sparks[index];
                if (spark == null || spark.Rect == null || spark.Image == null)
                    continue;
                float angle = (spark.Seed * 360f + time * spark.Speed * 70f) * Mathf.Deg2Rad;
                float radius = radiusBase * spark.Radius *
                    (0.92f + 0.08f * Mathf.Sin(time * 2.4f + spark.Seed * 11f));
                spark.Rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                spark.Rect.localEulerAngles = new Vector3(0f, 0f, -angle * Mathf.Rad2Deg);

                Color sparkColor = Color.Lerp(Color.white, tint, 0.68f);
                sparkColor.a = (0.16f + 0.5f *
                    Mathf.Pow(0.5f + 0.5f * Mathf.Sin(time * 3.4f + spark.Seed * 17f), 3f)) * intensity;
                spark.Image.color = sparkColor;
            }
        }

        private void UpdateRankedButton(float time)
        {
            if (shimmer == null || shimmerImage == null)
            {
                enabled = false;
                return;
            }

            float width = Mathf.Max(1f, effectRoot.rect.width);
            float height = Mathf.Max(1f, effectRoot.rect.height);
            float lifetime = time - startedAt;
            float sweep = Mathf.Repeat(lifetime * 0.24f + 0.68f, 1f);
            float smoothTravel = Mathf.SmoothStep(0f, 1f, sweep);
            shimmer.anchoredPosition = new Vector2(Mathf.Lerp(-0.32f, 0.30f, smoothTravel) * width, 0f);
            shimmer.sizeDelta = new Vector2(width, height);
            Color sweepColor = Color.Lerp(Color.white, tint, 0.38f);
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.20f, sweep));
            float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.76f, 0.98f, sweep));
            float initialFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lifetime / 0.45f));
            sweepColor.a = (0.08f + 0.12f * Mathf.Sin(sweep * Mathf.PI))
                * fadeIn * fadeOut * initialFade * intensity;
            shimmerImage.color = sweepColor;

            for (int index = 0; index < sparks.Length; index++)
            {
                Spark spark = sparks[index];
                if (spark == null || spark.Rect == null || spark.Image == null)
                    continue;
                float travel = Mathf.Repeat(time * spark.Speed + spark.Seed, 1f);
                float x = Mathf.Lerp(-0.46f, 0.46f, travel) * width;
                float y = Mathf.Sin(time * (1.5f + spark.Seed) + spark.Seed * 19f) * height * 0.28f;
                spark.Rect.anchoredPosition = new Vector2(x, y);
                spark.Rect.localEulerAngles = new Vector3(0f, 0f, time * 45f + spark.Seed * 360f);

                Color sparkColor = Color.Lerp(Color.white, tint, 0.52f);
                sparkColor.a = Mathf.Sin(travel * Mathf.PI) * 0.48f * intensity;
                spark.Image.color = sparkColor;
            }
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool preserveAspect)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)holder.transform;
            PvpUiFactory.SetAnchors(rect, anchorMin, anchorMax);
            Image image = holder.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static float Hash01(int value)
        {
            float hashed = Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f;
            return hashed - Mathf.Floor(hashed);
        }
    }
}
