using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>Central celebration for the Barbarian's bagpipe supreme.</summary>
    public static class BarbarianSupremeVfx
    {
        private static Sprite auraSprite;

        public static IEnumerator Play(RectTransform caster, float duration = 2.8f)
        {
            if (caster == null)
                yield break;

            // La cornamusa e' un evento di schermo, non della pedina: usa il Canvas
            // radice cosi' resta al centro anche se carte, tavolo o moneta si muovono.
            Canvas canvas = caster.GetComponentInParent<Canvas>();
            RectTransform parent = canvas != null
                ? canvas.rootCanvas.transform as RectTransform
                : caster.parent as RectTransform;
            if (parent == null)
                yield break;

            GameObject rootObject = new("Barbarian Bagpipes Supreme VFX", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();
            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Image veil = CreateImage(root, "Rage Veil", Aura(), new Color(0.25f, 0.008f, 0.002f, 0f));
            veil.rectTransform.anchorMin = Vector2.zero;
            veil.rectTransform.anchorMax = Vector2.one;
            veil.rectTransform.offsetMin = veil.rectTransform.offsetMax = Vector2.zero;

            Image outerAura = CreateImage(root, "Party Rage Aura", Aura(), new Color(1f, 0.04f, 0.008f, 0f));
            outerAura.rectTransform.sizeDelta = new Vector2(900f, 900f);
            Image innerAura = CreateImage(root, "Golden War Cry", Aura(), new Color(1f, 0.42f, 0.04f, 0f));
            innerAura.rectTransform.sizeDelta = new Vector2(620f, 620f);

            Sprite bagpipes = Resources.Load<Sprite>("UI/barbarian_bagpipes_supreme_cutout");
            Image instrument = CreateImage(root, "Great Barbarian Bagpipes", bagpipes, Color.white);
            instrument.rectTransform.sizeDelta = new Vector2(550f, 550f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.13f));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.76f) / 0.24f));
                float intensity = appear * fade;
                float sway = Mathf.Sin(elapsed * 5.6f) * 9f + Mathf.Sin(elapsed * 11.2f) * 2.5f;

                group.alpha = intensity;
                veil.color = new Color(0.25f, 0.008f, 0.002f, 0.20f * intensity);
                outerAura.color = new Color(1f, 0.04f, 0.008f, 0.28f * intensity);
                outerAura.rectTransform.localScale = Vector3.one * (0.55f + t * 1.35f);
                innerAura.color = new Color(1f, 0.42f, 0.04f, (0.32f + Mathf.Sin(elapsed * 12f) * 0.09f) * intensity);
                innerAura.rectTransform.localScale = Vector3.one * (0.72f + Mathf.Sin(elapsed * 6f) * 0.06f);
                instrument.rectTransform.anchoredPosition = new Vector2(sway, Mathf.Sin(elapsed * 3f) * 8f);
                instrument.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sway * 0.42f);
                instrument.rectTransform.localScale = Vector3.one * (0.78f + appear * 0.22f + Mathf.Sin(elapsed * 6f) * 0.025f);
                yield return null;
            }

            Object.Destroy(rootObject);
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite Aura()
        {
            if (auraSprite != null)
                return auraSprite;

            const int size = 128;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            auraSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f);
            auraSprite.hideFlags = HideFlags.HideAndDontSave;
            return auraSprite;
        }
    }
}
