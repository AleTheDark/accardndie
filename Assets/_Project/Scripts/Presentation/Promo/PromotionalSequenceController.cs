using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    /// <summary>Deterministic, self-running promotional trailer. No gameplay bootstrap or input required.</summary>
    public sealed class PromotionalSequenceController : MonoBehaviour
    {
        [SerializeField] private Sprite[] backgrounds;
        [SerializeField] private Sprite[] bosses;
        [SerializeField] private Sprite[] heroes;
        [SerializeField] private float playbackSpeed = 1f;

        private CanvasGroup fade;
        private Image background;
        private Image boss;
        private Image heroLeft;
        private Image heroRight;
        private Text headline;
        private Text subtitle;
        private Text die;
        private RectTransform board;
        private RectTransform heroLeftPawn;
        private RectTransform heroRightPawn;
        private RectTransform bossPawn;
        private RectTransform doorLeft;
        private RectTransform doorRight;

        private IEnumerator Start()
        {
            // Il trailer e' un'animazione continua: tiene il frame rate alto
            // per tutta la sua durata invece di lasciarlo decidere al governor.
            AccardND.Battlefield.FrameRateGovernor.Acquire(this);
            BuildView();
            yield return PlayTrailer();
        }

        private void OnDisable() => AccardND.Battlefield.FrameRateGovernor.Release(this);

        private void BuildView()
        {
            var canvasObject = new GameObject("Promo Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            background = CreateImage("Background", canvas.transform, Color.white, Vector2.zero, Vector2.one);
            CreateImage("Vignette", canvas.transform, new Color(0.01f, 0.015f, 0.035f, 0.42f), Vector2.zero, Vector2.one);

            board = new GameObject("Battlefield", typeof(RectTransform)).GetComponent<RectTransform>();
            board.SetParent(canvas.transform, false);
            SetRect(board, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f));

            heroLeft = CreatePawn("Hero Vanguard", board, new Vector2(0.24f, 0.35f), new Vector2(220f, 220f), new Color(0.1f, 0.55f, 1f), out heroLeftPawn);
            heroRight = CreatePawn("Hero Support", board, new Vector2(0.39f, 0.61f), new Vector2(190f, 190f), new Color(0.2f, 0.85f, 0.65f), out heroRightPawn);
            boss = CreatePawn("Enemy Boss", board, new Vector2(0.76f, 0.52f), new Vector2(290f, 290f), new Color(0.92f, 0.15f, 0.12f), out bossPawn);

            Image leftDoor = CreateImage("Loot Door Left", canvas.transform, new Color(0.045f, 0.025f, 0.015f), Vector2.zero, new Vector2(0.5f, 1f));
            Image rightDoor = CreateImage("Loot Door Right", canvas.transform, new Color(0.045f, 0.025f, 0.015f), new Vector2(0.5f, 0f), Vector2.one);
            doorLeft = leftDoor.rectTransform;
            doorRight = rightDoor.rectTransform;

            headline = CreateLabel("Headline", canvas.transform, 76, FontStyle.Bold, new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.96f));
            subtitle = CreateLabel("Subtitle", canvas.transform, 30, FontStyle.Normal, new Vector2(0.12f, 0.05f), new Vector2(0.88f, 0.16f));
            die = CreateLabel("D20", canvas.transform, 112, FontStyle.Bold, new Vector2(0.42f, 0.35f), new Vector2(0.58f, 0.65f));
            die.gameObject.SetActive(false);

            Image fadeImage = CreateImage("Fade", canvas.transform, Color.black, Vector2.zero, Vector2.one);
            fade = fadeImage.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
        }

        private IEnumerator PlayTrailer()
        {
            SetShot(0, 0, "STANZA LOOT", "IL TESORO E' DAVANTI A TE");
            yield return FadeTo(0f, 0.35f);
            yield return RevealShotText();
            yield return OpenLootDoors();
            yield return Wait(0.65f);
            yield return Zoom(1f, 1.08f, 0.75f);

            yield return Transition(() => SetShot(1, 1, "SCHIERA LE PEDINE", "PREPARATI AL COMBATTIMENTO"));
            yield return RevealShotText();
            yield return CardsEnter();
            yield return Wait(0.9f);

            headline.text = "LANCIA IL DADO";
            yield return RevealText(headline, 0.34f, 1.12f);
            yield return RollD20();

            yield return Transition(() => SetShot(2, 2, "IL GUARDIANO ATTACCA", "DIFENDI IL BOTTINO"));
            yield return RevealShotText();
            yield return BossAttack();
            yield return Wait(0.65f);

            yield return Transition(() => SetShot(0, 3, "COMBATTI. RISCHIA. VINCI.", "ACCARD N' DIE"));
            yield return RevealShotText();
            yield return Zoom(1f, 1.12f, 1.15f);
            yield return Wait(1.5f);
            yield return FadeTo(1f, 0.65f);
        }

        private void SetShot(int backgroundIndex, int bossIndex, string title, string caption)
        {
            if (backgrounds != null && backgrounds.Length > 0)
                background.sprite = backgrounds[Mathf.Abs(backgroundIndex) % backgrounds.Length];
            if (bosses != null && bosses.Length > 0)
                boss.sprite = bosses[Mathf.Abs(bossIndex) % bosses.Length];
            if (heroes != null && heroes.Length > 0)
            {
                heroLeft.sprite = heroes[0];
                heroRight.sprite = heroes[Mathf.Min(1, heroes.Length - 1)];
            }
            board.localScale = Vector3.one;
            board.anchoredPosition = Vector2.zero;
            bossPawn.anchoredPosition = Vector2.zero;
            headline.text = title;
            subtitle.text = caption;
            PrepareTextForReveal(headline);
            PrepareTextForReveal(subtitle);
        }

        private IEnumerator RevealShotText()
        {
            yield return RevealText(headline, 0.42f, 1.16f);
            yield return RevealText(subtitle, 0.3f, 1.08f);

        }

        private static void PrepareTextForReveal(Text text)
        {
            if (text == null)
                return;

            CanvasGroup group = text.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            text.rectTransform.localScale = Vector3.one * 0.72f;
        }

        private IEnumerator RevealText(Text text, float duration, float overshoot)
        {
            CanvasGroup group = text.GetComponent<CanvasGroup>();
            RectTransform rect = text.rectTransform;
            Vector2 target = rect.anchoredPosition;
            Vector2 start = target + Vector2.down * 38f;
            Outline outline = text.GetComponent<Outline>();
            Color baseOutline = outline.effectColor;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutBack(t);
                group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.7f));
                rect.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
                float scale = Mathf.LerpUnclamped(0.72f, overshoot, eased);
                rect.localScale = Vector3.one * scale;
                outline.effectColor = Color.Lerp(new Color(1f, 0.76f, 0.18f, 1f), baseOutline, t);
                yield return null;
            }

            rect.anchoredPosition = target;
            rect.localScale = Vector3.one;
            group.alpha = 1f;
            outline.effectColor = baseOutline;
            yield return TextImpactPulse(rect, duration * 0.45f);
        }

        private IEnumerator TextImpactPulse(RectTransform rect, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI) * (1f - t);
                rect.localScale = Vector3.one * (1f + pulse * 0.09f);
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        private IEnumerator Transition(Action swap)
        {
            yield return FadeTo(1f, 0.22f);
            swap();
            yield return FadeTo(0f, 0.28f);
        }

        private IEnumerator CardsEnter()
        {
            Vector2 leftTarget = Vector2.zero;
            Vector2 rightTarget = Vector2.zero;
            float elapsed = 0f;
            while (elapsed < 0.65f)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                float p = EaseOutBack(Mathf.Clamp01(elapsed / 0.65f));
                heroLeftPawn.anchoredPosition = Vector2.LerpUnclamped(Vector2.left * 700f, leftTarget, p);
                heroRightPawn.anchoredPosition = Vector2.LerpUnclamped(Vector2.down * 700f, rightTarget, p);
                yield return null;
            }
        }

        private IEnumerator RollD20()
        {
            die.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < 1.05f)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                die.text = Mathf.Clamp(1 + Mathf.FloorToInt(elapsed * 47f) % 20, 1, 20).ToString();
                die.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * 720f);
                yield return null;
            }
            die.text = "18";
            die.color = new Color(1f, 0.78f, 0.2f);
            die.rectTransform.localRotation = Quaternion.identity;
            die.rectTransform.localScale = Vector3.one * 1.35f;
            yield return Wait(0.55f);
            die.gameObject.SetActive(false);
        }

        private IEnumerator BossAttack()
        {
            RectTransform target = bossPawn;
            Vector2 origin = Vector2.zero;
            yield return Move(target, origin, Vector2.left * 260f, 0.28f);
            for (int frame = 0; frame < 14; frame++)
            {
                float strength = 22f * (1f - frame / 14f);
                board.anchoredPosition = new Vector2(Mathf.Sin(frame * 4.7f), Mathf.Cos(frame * 3.9f)) * strength;
                fade.alpha = frame < 3 ? 0.55f : 0f;
                yield return null;
            }
            board.anchoredPosition = Vector2.zero;
            fade.alpha = 0f;
            yield return Move(target, Vector2.left * 260f, origin, 0.35f);
        }

        private IEnumerator Move(RectTransform target, Vector2 from, Vector2 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                target.anchoredPosition = Vector2.LerpUnclamped(from, to, p);
                yield return null;
            }
            target.anchoredPosition = to;
        }

        private IEnumerator OpenLootDoors()
        {
            Vector2 leftStart = Vector2.zero;
            Vector2 rightStart = Vector2.zero;
            float elapsed = 0f;
            while (elapsed < 0.8f)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.8f));
                doorLeft.anchoredPosition = Vector2.Lerp(leftStart, Vector2.left * 980f, p);
                doorRight.anchoredPosition = Vector2.Lerp(rightStart, Vector2.right * 980f, p);
                yield return null;
            }
            doorLeft.gameObject.SetActive(false);
            doorRight.gameObject.SetActive(false);
        }

        private IEnumerator Zoom(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                board.localScale = Vector3.one * Mathf.Lerp(from, to, p);
                yield return null;
            }
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = fade.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime * playbackSpeed;
                fade.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            fade.alpha = target;
        }

        private IEnumerator Wait(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds / Mathf.Max(0.1f, playbackSpeed));
        }

        private static Image CreateImage(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetRect(image.rectTransform, min, max);
            return image;
        }

        private static Image CreatePawn(string name, Transform parent, Vector2 anchor, Vector2 size, Color rimColor, out RectTransform pawnRoot)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(Outline));
            root.transform.SetParent(parent, false);
            pawnRoot = root.GetComponent<RectTransform>();
            pawnRoot.anchorMin = pawnRoot.anchorMax = anchor;
            pawnRoot.pivot = new Vector2(0.5f, 0.5f);
            pawnRoot.sizeDelta = size;
            Image frame = root.GetComponent<Image>();
            frame.color = new Color(0.035f, 0.045f, 0.07f, 1f);
            root.GetComponent<Mask>().showMaskGraphic = true;
            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = rimColor;
            outline.effectDistance = new Vector2(8f, -8f);

            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitObject.transform.SetParent(root.transform, false);
            Image portrait = portraitObject.GetComponent<Image>();
            portrait.color = Color.white;
            portrait.preserveAspect = false;
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.offsetMin = new Vector2(12f, 12f);
            portraitRect.offsetMax = new Vector2(-12f, -12f);
            return portrait;
        }

        private static Text CreateLabel(string name, Transform parent, int size, FontStyle style, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline), typeof(Shadow), typeof(CanvasGroup));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.BoldAndItalic;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.Lerp(new Color(0.92f, 0.82f, 0.42f), Color.white, 0.2f);
            text.raycastTarget = false;
            Outline outline = gameObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.01f, 0f, 0.96f);
            outline.effectDistance = new Vector2(2.8f, -2.8f);
            outline.useGraphicAlpha = true;
            Shadow shadow = gameObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
