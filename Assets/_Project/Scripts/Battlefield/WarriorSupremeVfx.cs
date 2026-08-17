using System.Collections;
using System.Collections.Generic;
using AccardND.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Scariche persistenti della Suprema del Guerriero. Copia esclusivamente la resa
    /// grafica del Sigillo Oscuro: non applica né rappresenta alcuno stato di Sigillo.
    /// </summary>
    internal sealed class WarriorSupremeLightningVfx : DarkSigilLightningVfx
    {
        protected override void Awake()
        {
            base.Awake();
            ConfigureWarriorSupreme();
        }
    }

    /// <summary>Persistent blue ascension aura and attack lightning for the Warrior supreme.</summary>
    public sealed class WarriorSupremeVfx : MonoBehaviour
    {
        private static Sprite glowSprite;
        private static Sprite ringSprite;
        private static Sprite boltSprite;

        private RectTransform target;
        private RectTransform auraRoot;
        private Image outerGlow;
        private Image innerGlow;
        private Image ring;
        private WarriorSupremeLightningVfx lightning;
        private Vector3 baseScale;
        private int bonus;

        public bool HasEmpoweredSword => bonus == 2;

        /// <summary>Spegne definitivamente il loop quando la pedina viene eliminata.</summary>
        internal void StopOnDefeat()
        {
            if (auraRoot != null)
            {
                auraRoot.gameObject.SetActive(false);
                Destroy(auraRoot.gameObject);
                auraRoot = null;
            }

            if (target != null)
                target.localScale = baseScale;

            Destroy(this);
        }

        public static WarriorSupremeVfx Activate(PrototypeCardView view, int supremeBonus)
        {
            if (view == null)
                return null;

            WarriorSupremeVfx effect = view.GetComponent<WarriorSupremeVfx>();
            if (effect == null)
                effect = view.gameObject.AddComponent<WarriorSupremeVfx>();
            effect.Initialize(Mathf.Max(2, supremeBonus));
            return effect;
        }

        private void Initialize(int supremeBonus)
        {
            bonus = supremeBonus;
            target = transform as RectTransform;
            baseScale = target != null ? target.localScale : Vector3.one;

            if (auraRoot != null)
            {
                auraRoot.gameObject.SetActive(true);
                return;
            }

            GameObject root = new("Warrior Supreme Blue Aura", typeof(RectTransform), typeof(CanvasGroup));
            auraRoot = root.GetComponent<RectTransform>();
            auraRoot.SetParent(target, false);
            // Keep the aura tight to the pawn silhouette. Wide screen-space glows quickly
            // turn into an indistinct cloud when several UI effects overlap.
            auraRoot.anchorMin = new Vector2(-0.075f, -0.055f);
            auraRoot.anchorMax = new Vector2(1.075f, 1.085f);
            auraRoot.offsetMin = auraRoot.offsetMax = Vector2.zero;
            auraRoot.SetAsFirstSibling();
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject lightningObject = new("Dense Black Blue Lightning", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(WarriorSupremeLightningVfx));
            RectTransform lightningRect = lightningObject.GetComponent<RectTransform>();
            lightningRect.SetParent(auraRoot, false);
            lightningRect.anchorMin = Vector2.zero;
            lightningRect.anchorMax = Vector2.one;
            lightningRect.offsetMin = lightningRect.offsetMax = Vector2.zero;
            lightning = lightningObject.GetComponent<WarriorSupremeLightningVfx>();

            outerGlow = Image("Outer Blue Flame", Glow(), new Color(0.015f, 0.22f, 1f, 0.13f));
            innerGlow = Image("Cyan Energy Core", Glow(), new Color(0.05f, 0.86f, 1f, 0.09f));
            ring = Image("Supreme Energy Ring", Ring(), new Color(0.22f, 0.88f, 1f, 0.88f));
            ring.rectTransform.localScale = Vector3.one * 0.91f;

        }

        private void Update()
        {
            if (auraRoot == null || target == null)
                return;
            float time = Time.unscaledTime;
            float strength = bonus >= 4 ? 1.06f : 1.02f;
            outerGlow.rectTransform.localScale = Vector3.one * (0.98f + Mathf.Sin(time * 2.9f) * 0.025f) * strength;
            innerGlow.rectTransform.localScale = Vector3.one * (0.84f + Mathf.Sin(time * 5.3f) * 0.018f) * strength;
            ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, time * 11f);
            ring.color = new Color(0.18f, 0.78f, 1f, 0.72f + Mathf.Sin(time * 4.2f) * 0.1f);
            float pawnScale = bonus >= 4 ? 1.06f : 1.025f;
            target.localScale = Vector3.Lerp(target.localScale, baseScale * pawnScale, Time.unscaledDeltaTime * 7f);

        }

        public IEnumerator PlaySwordInfusion(RectTransform overlayParent, RectTransform defender)
        {
            if (!HasEmpoweredSword || overlayParent == null || defender == null || target == null)
                yield break;

            const float duration = 0.52f;
            List<Image> bolts = new();
            for (int i = 0; i < 9; i++)
            {
                GameObject go = new($"Warrior Supreme Sword Lightning {i:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(overlayParent, false);
                rect.SetAsLastSibling();
                Image image = go.GetComponent<Image>();
                image.sprite = Bolt();
                image.color = i % 3 == 0 ? Color.white : new Color(0.05f, 0.72f, 1f, 1f);
                image.raycastTarget = false;
                bolts.Add(image);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 start = Vector3.Lerp(target.position, defender.position, Mathf.Clamp01(t * 1.25f));
                Vector3 direction = (defender.position - target.position).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                for (int i = 0; i < bolts.Count; i++)
                {
                    RectTransform rect = bolts[i].rectTransform;
                    float lane = (i - 4f) * 13f;
                    rect.position = start + Vector3.Cross(direction, Vector3.forward) * lane + direction * Random.Range(-32f, 38f);
                    rect.sizeDelta = new Vector2(Random.Range(4f, 9f), Random.Range(42f, 105f));
                    rect.localRotation = Quaternion.Euler(0f, 0f, angle + Random.Range(-24f, 24f));
                    Color color = bolts[i].color;
                    color.a = (1f - t) * Random.Range(0.55f, 1f);
                    bolts[i].color = color;
                }
                yield return null;
            }
            foreach (Image image in bolts)
                if (image != null) Destroy(image.gameObject);
        }

        private Image Image(string name, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(auraRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite Glow() => glowSprite != null ? glowSprite : glowSprite = BuildSprite(false, false);
        private static Sprite Ring() => ringSprite != null ? ringSprite : ringSprite = BuildSprite(true, false);
        private static Sprite Bolt() => boltSprite != null ? boltSprite : boltSprite = BuildSprite(false, true);

        private static Sprite BuildSprite(bool ring, bool bolt)
        {
            int size = bolt ? 32 : 128;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = (x + .5f) / size * 2f - 1f;
                float ny = (y + .5f) / size * 2f - 1f;
                float alpha;
                if (bolt)
                {
                    float zigzag = Mathf.Sin((ny + 1f) * 13f) * .22f;
                    alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(nx - zigzag) * 8f), 1.4f) * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ny)), .25f);
                }
                else
                {
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    alpha = ring ? Mathf.Clamp01(1f - Mathf.Abs(d - .72f) * 16f) : Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true); texture.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * .5f, 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
