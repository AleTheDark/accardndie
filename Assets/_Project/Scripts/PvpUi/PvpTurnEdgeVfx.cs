using AccardND.Battlefield;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Indicatore discreto del turno PvP: il giocatore locale illumina il bordo
    /// inferiore in blu, l'avversario quello superiore in rosso.
    /// </summary>
    public sealed class PvpTurnEdgeVfx : MonoBehaviour
    {
        private sealed class Spark
        {
            public RectTransform Rect;
            public Image Image;
            public float Seed;
            public float Speed;
        }

        private const int SparkCount = 10;
        private static readonly Color LocalBlue = new(0.12f, 0.56f, 1f, 1f);
        private static readonly Color OpponentRed = new(1f, 0.18f, 0.14f, 1f);

        private RectTransform root;
        private Canvas canvas;
        private Image softGlow;
        private Image travellingGlow;
        private Spark[] sparks;
        private bool localTurn;

        public static PvpTurnEdgeVfx Create(RectTransform parent)
        {
            var holder = new GameObject(
                "PvP Turn Edge VFX",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(PvpTurnEdgeVfx));
            holder.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)holder.transform;
            PvpUiFactory.SetAnchors(rect, Vector2.zero, Vector2.one);
            CanvasGroup group = holder.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            PvpTurnEdgeVfx effect = holder.GetComponent<PvpTurnEdgeVfx>();
            effect.root = rect;
            effect.Build();
            return effect;
        }

        public void SetTurn(bool visible, bool isLocalTurn)
        {
            localTurn = isLocalTurn;
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
            if (visible)
                ApplyTint();
        }

        private void Build()
        {
            canvas = root.GetComponentInParent<Canvas>();
            softGlow = CreateImage("Edge Glow", MmoUiTheme.GetRadialGlowSprite());
            travellingGlow = CreateImage("Travelling Glow", MmoUiTheme.GetRadialGlowSprite());

            sparks = new Spark[SparkCount];
            for (int index = 0; index < SparkCount; index++)
            {
                Image image = CreateImage($"Turn Spark {index + 1}", MmoUiTheme.GetStarSprite());
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                float size = Mathf.Lerp(4f, 9f, Hash01(index * 23 + 7));
                rect.sizeDelta = new Vector2(size, size);
                image.preserveAspect = true;
                sparks[index] = new Spark
                {
                    Rect = rect,
                    Image = image,
                    Seed = Hash01(index * 31 + 11),
                    Speed = Mathf.Lerp(0.055f, 0.105f, Hash01(index * 17 + 3))
                };
            }

            ApplyTint();
        }

        private void Update()
        {
            if (root == null || softGlow == null || !UiVfxBudget.ShouldAnimate(root, canvas))
                return;

            float time = Time.unscaledTime;
            float width = Mathf.Max(1f, root.rect.width);
            float height = Mathf.Max(1f, root.rect.height);
            float direction = localTurn ? 1f : -1f;
            float edgeY = localTurn ? -height * 0.46f : height * 0.46f;
            Color tint = localTurn ? LocalBlue : OpponentRed;

            float breath = 0.5f + 0.5f * Mathf.Sin(time * 1.55f);
            RectTransform softRect = softGlow.rectTransform;
            softRect.anchoredPosition = new Vector2(0f, edgeY);
            softRect.sizeDelta = new Vector2(width * 1.08f, height * 0.22f);
            Color softColor = tint;
            softColor.a = Mathf.Lerp(0.055f, 0.105f, breath);
            softGlow.color = softColor;

            float travel = Mathf.SmoothStep(0f, 1f, Mathf.Repeat(time * 0.12f, 1f));
            RectTransform travelRect = travellingGlow.rectTransform;
            travelRect.anchoredPosition = new Vector2(
                Mathf.Sin(time * 0.43f) * width * 0.08f,
                edgeY + direction * travel * height * 0.19f);
            travelRect.sizeDelta = new Vector2(width * 0.72f, height * 0.13f);
            Color travelColor = Color.Lerp(Color.white, tint, 0.72f);
            travelColor.a = Mathf.Sin(travel * Mathf.PI) * 0.075f;
            travellingGlow.color = travelColor;

            for (int index = 0; index < sparks.Length; index++)
            {
                Spark spark = sparks[index];
                float progress = Mathf.Repeat(time * spark.Speed + spark.Seed, 1f);
                float x = Mathf.Lerp(-0.46f, 0.46f, Hash01(index * 43 + 5)) * width;
                float y = edgeY + direction * progress * height * 0.22f;
                spark.Rect.anchoredPosition = new Vector2(x, y);
                spark.Rect.localEulerAngles = new Vector3(0f, 0f, time * 18f + spark.Seed * 360f);
                Color sparkColor = Color.Lerp(Color.white, tint, 0.58f);
                sparkColor.a = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 2f) * 0.42f;
                spark.Image.color = sparkColor;
            }
        }

        private void ApplyTint()
        {
            Color tint = localTurn ? LocalBlue : OpponentRed;
            tint.a = 0f;
            softGlow.color = tint;
            travellingGlow.color = tint;
            foreach (Spark spark in sparks)
                spark.Image.color = tint;
        }

        private Image CreateImage(string name, Sprite sprite)
        {
            var holder = new GameObject(name, typeof(RectTransform), typeof(Image));
            holder.transform.SetParent(root, false);
            Image image = holder.GetComponent<Image>();
            image.sprite = sprite;
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
