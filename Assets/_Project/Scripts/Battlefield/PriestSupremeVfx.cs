using System.Collections;
using System.Collections.Generic;
using AccardND.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>Purificazione: croce centrale, esplosione bianca e raggi verso i soli stati rimossi.</summary>
    public static class PriestSupremeVfx
    {
        private static Sprite auraSprite;

        public static IEnumerator Play(RectTransform caster, IReadOnlyList<PrototypeCardView> affected,
            System.Action<PrototypeCardView> onImpact = null, float duration = 2.8f)
        {
            if (caster == null)
                yield break;

            Canvas canvas = caster.GetComponentInParent<Canvas>();
            RectTransform parent = canvas != null ? canvas.rootCanvas.transform as RectTransform : caster.parent as RectTransform;
            if (parent == null)
                yield break;

            GameObject rootObject = new("Priest Purification Supreme VFX", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();
            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Image veil = CreateImage(root, "Purification Veil", Aura(), new Color(1f, 1f, 1f, 0f));
            veil.rectTransform.anchorMin = Vector2.zero;
            veil.rectTransform.anchorMax = Vector2.one;
            veil.rectTransform.offsetMin = veil.rectTransform.offsetMax = Vector2.zero;

            Image burst = CreateImage(root, "White Radial Explosion", Aura(), Color.clear);
            burst.rectTransform.sizeDelta = new Vector2(760f, 760f);

            Sprite crossSprite = Resources.Load<Sprite>("UI/priest_purification_cross");
            Image cross = CreateImage(root, "Great Purification Cross", crossSprite, Color.white);
            cross.rectTransform.sizeDelta = new Vector2(460f, 460f);

            const float burstAt = 1.28f;
            const float beamDuration = 0.72f;
            List<Image> beams = new();
            List<Image> sparks = new();
            List<PrototypeCardView> targets = new();
            List<bool> impacts = new();
            if (affected != null)
            {
                foreach (PrototypeCardView view in affected)
                {
                    if (view == null || view.RectTransform == null)
                        continue;
                    Vector3 worldCenter = view.RectTransform.TransformPoint(view.RectTransform.rect.center);
                    Vector2 end = root.InverseTransformPoint(worldCenter);
                    float length = end.magnitude;
                    if (length < 1f)
                        continue;
                    Image beam = CreateImage(root, "Purification Ray", Aura(), new Color(1f, 1f, 1f, 0f));
                    beam.preserveAspect = false;
					beam.type = Image.Type.Filled;
					beam.fillMethod = Image.FillMethod.Horizontal;
					beam.fillOrigin = (int)Image.OriginHorizontal.Left;
					beam.fillAmount = 0f;
                    beam.rectTransform.sizeDelta = new Vector2(length, 18f);
                    beam.rectTransform.pivot = new Vector2(0f, 0.5f);
                    beam.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(end.y, end.x) * Mathf.Rad2Deg);
                    beams.Add(beam);
                    Image spark = CreateImage(root, "Purification Impact", Aura(), Color.clear);
                    spark.rectTransform.sizeDelta = new Vector2(120f, 120f);
                    spark.rectTransform.anchoredPosition = end;
                    sparks.Add(spark);
                    targets.Add(view);
                    impacts.Add(false);
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.35f));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 2.35f) / 0.45f));
                float sway = Mathf.Sin(elapsed * 5.6f) * 9f + Mathf.Sin(elapsed * 11.2f) * 2.5f;
                cross.rectTransform.anchoredPosition = new Vector2(sway, Mathf.Sin(elapsed * 3f) * 8f);
                cross.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sway * 0.42f);
                cross.rectTransform.localScale = Vector3.one * (0.78f + appear * 0.22f + Mathf.Sin(elapsed * 6f) * 0.025f);
                cross.color = new Color(1f, 1f, 1f, appear * fade);

                float explosionT = Mathf.Clamp01((elapsed - burstAt) / 0.34f);
                float explosionAlpha = (1f - explosionT) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - burstAt) / 0.08f));
                burst.color = new Color(1f, 1f, 1f, explosionAlpha * 0.95f);
                burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.18f, 2.15f, explosionT);
                veil.color = new Color(1f, 1f, 1f, explosionAlpha * 0.28f);

                float beamT = Mathf.Clamp01((elapsed - burstAt) / beamDuration);
                float beamAlpha = Mathf.Sin(beamT * Mathf.PI) * fade;
                for (int i = 0; i < beams.Count; i++)
                {
                    beams[i].fillAmount = beamT;
                    beams[i].color = new Color(1f, 1f, 1f, beamAlpha * 0.9f);
                    sparks[i].color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((beamT - 0.72f) / 0.28f)) * fade);
                    sparks[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.35f, beamT);
                    if (!impacts[i] && beamT >= 1f)
                    {
                        impacts[i] = true;
                        onImpact?.Invoke(targets[i]);
                    }
                }
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
