using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Procedural, resolution-independent UI VFX used when a Barbarian gains Fury.
    /// It intentionally owns no gameplay state and can safely run on any card RectTransform.
    /// </summary>
    public static class BarbarianFuryVfx
    {
        private sealed class Shard
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Direction;
            public float Spin;
            public float Delay;
        }

        private static Sprite softDisc;
        private static Sprite ring;
        private static Sprite streak;

        public static IEnumerator Play(RectTransform target, float duration = 1.65f)
        {
            if (target == null)
                yield break;

            RectTransform parent = target.parent as RectTransform;
            if (parent == null)
                yield break;

            GameObject rootObject = new("Barbarian Fury VFX", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.position = target.position;
            root.sizeDelta = target.rect.size * 1.55f;
            root.SetAsLastSibling();
            CanvasGroup group = rootObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Image glow = CreateImage(root, "Blood Core", SoftDisc(), new Color(1f, 0.015f, 0.005f, 0f));
            glow.rectTransform.sizeDelta = root.sizeDelta * 1.35f;
            Image outerRing = CreateImage(root, "Shock Ring", Ring(), new Color(1f, 0.08f, 0.015f, 0f));
            outerRing.rectTransform.sizeDelta = root.sizeDelta * 0.8f;
            Image innerRing = CreateImage(root, "Inner Ring", Ring(), new Color(1f, 0.42f, 0.08f, 0f));
            innerRing.rectTransform.sizeDelta = root.sizeDelta * 0.58f;

            List<Shard> shards = new();
            for (int i = 0; i < 28; i++)
            {
                float angle = i * (360f / 28f) + Random.Range(-5f, 5f);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                Image image = CreateImage(root, $"Rage Streak {i:00}", Streak(),
                    i % 4 == 0 ? new Color(1f, 0.68f, 0.12f, 0f) : new Color(1f, 0.025f, 0.008f, 0f));
                image.rectTransform.sizeDelta = new Vector2(Random.Range(5f, 11f), Random.Range(55f, 125f));
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
                shards.Add(new Shard
                {
                    Rect = image.rectTransform,
                    Image = image,
                    Direction = direction,
                    Spin = Random.Range(-100f, 100f),
                    Delay = Random.Range(0f, 0.28f)
                });
            }

            Vector3 originalScale = target.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f));
                float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.67f) / 0.33f));
                float intensity = attack * release;
                float pulse = 0.82f + Mathf.Sin(elapsed * 26f) * 0.12f + Mathf.Sin(elapsed * 43f) * 0.06f;

                glow.color = new Color(1f, 0.015f, 0.005f, 0.38f * intensity);
                glow.rectTransform.localScale = Vector3.one * (0.8f + t * 0.65f + pulse * 0.08f);
                outerRing.color = new Color(1f, 0.035f, 0.005f, 0.9f * intensity);
                outerRing.rectTransform.localScale = Vector3.one * (0.65f + Mathf.Pow(t, 0.45f) * 1.3f);
                outerRing.rectTransform.Rotate(0f, 0f, 90f * Time.unscaledDeltaTime);
                innerRing.color = new Color(1f, 0.48f, 0.08f, 0.72f * intensity);
                innerRing.rectTransform.localScale = Vector3.one * (0.9f + pulse * 0.13f);
                innerRing.rectTransform.Rotate(0f, 0f, -145f * Time.unscaledDeltaTime);

                for (int i = 0; i < shards.Count; i++)
                {
                    Shard shard = shards[i];
                    float local = Mathf.Clamp01((t - shard.Delay) / Mathf.Max(0.01f, 0.72f - shard.Delay));
                    float alpha = Mathf.Sin(local * Mathf.PI) * intensity;
                    float distance = Mathf.Lerp(12f, root.sizeDelta.y * 0.72f, local);
                    shard.Rect.anchoredPosition = shard.Direction * distance;
                    shard.Rect.localScale = new Vector3(1f - local * 0.55f, 0.55f + local * 1.15f, 1f);
                    shard.Rect.Rotate(0f, 0f, shard.Spin * Time.unscaledDeltaTime);
                    Color color = shard.Image.color;
                    color.a = alpha * (i % 4 == 0 ? 0.95f : 0.68f);
                    shard.Image.color = color;
                }

                float rumble = intensity * 2.2f;
                target.localScale = originalScale * (1f + intensity * 0.045f)
                    + new Vector3(Random.Range(-rumble, rumble), Random.Range(-rumble, rumble), 0f) * 0.001f;
                yield return null;
            }

            target.localScale = originalScale;
            Object.Destroy(rootObject);
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private static Sprite SoftDisc() => softDisc != null ? softDisc : softDisc = BuildSprite(128, false, false);
        private static Sprite Ring() => ring != null ? ring : ring = BuildSprite(128, true, false);
        private static Sprite Streak() => streak != null ? streak : streak = BuildSprite(32, false, true);

        private static Sprite BuildSprite(int size, bool hollow, bool vertical)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = hollow ? "Fury Ring" : vertical ? "Fury Streak" : "Fury Glow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float distance = vertical
                    ? Mathf.Sqrt(nx * nx * 5f + ny * ny)
                    : Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = hollow
                    ? Mathf.Clamp01(1f - Mathf.Abs(distance - 0.72f) * 18f)
                    : Mathf.Pow(Mathf.Clamp01(1f - distance), vertical ? 0.45f : 1.8f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
