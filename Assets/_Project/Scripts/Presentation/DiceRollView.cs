using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    public sealed class DiceRollView : MonoBehaviour
    {
        private DiceSpriteCatalog catalog;
        private RectTransform dieRect;
        private Image dieImage;
        private Text resultText;
        private Text captionText;

        public static DiceRollView Create(Transform parent)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Image backdrop = CreateImage("Dice Roll Overlay", parent, new Color(0.01f, 0.018f, 0.03f, 0.82f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            DiceRollView view = backdrop.gameObject.AddComponent<DiceRollView>();
            view.catalog = Resources.Load<DiceSpriteCatalog>("DiceSpriteCatalog");

            view.captionText = CreateText("Caption", backdrop.transform, font, 27, FontStyle.Bold, TextAnchor.MiddleCenter);
            view.captionText.color = new Color(0.9f, 0.93f, 0.97f);
            SetRect(view.captionText.rectTransform, new Vector2(0.18f, 0.68f), new Vector2(0.82f, 0.78f));

            view.dieImage = CreateImage("Die", backdrop.transform, Color.white);
            view.dieImage.preserveAspect = true;
            view.dieRect = view.dieImage.rectTransform;
            view.dieRect.anchorMin = new Vector2(0.5f, 0.5f);
            view.dieRect.anchorMax = new Vector2(0.5f, 0.5f);
            view.dieRect.pivot = new Vector2(0.5f, 0.5f);
            view.dieRect.sizeDelta = new Vector2(310f, 310f);

            view.resultText = CreateText("Result", backdrop.transform, font, 70, FontStyle.Bold, TextAnchor.MiddleCenter);
            view.resultText.color = new Color(1f, 0.83f, 0.3f);
            SetRect(view.resultText.rectTransform, new Vector2(0.35f, 0.18f), new Vector2(0.65f, 0.35f));

            backdrop.gameObject.SetActive(false);
            return view;
        }

        public IEnumerator PlayRoll(int sides, int result, string caption, float rollDuration = 0.62f)
        {
            gameObject.SetActive(true);
            captionText.text = caption;
            resultText.text = string.Empty;
            dieRect.localRotation = Quaternion.identity;
            dieRect.localScale = Vector3.one;

            Sprite[] frames = catalog != null ? catalog.FindFrames(sides) : System.Array.Empty<Sprite>();
            if (frames.Length > 0)
            {
                float frameDuration = rollDuration / frames.Length;
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    dieImage.sprite = frames[frameIndex];
                    dieRect.localScale = Vector3.one * (frameIndex % 2 == 0 ? 0.96f : 1.04f);
                    yield return new WaitForSecondsRealtime(frameDuration);
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(rollDuration);
            }

            dieRect.localRotation = Quaternion.identity;
            dieRect.localScale = Vector3.one;
            Sprite resultSprite = catalog != null ? catalog.FindResult(sides, result) : null;
            if (resultSprite != null)
                dieImage.sprite = resultSprite;
            resultText.text = $"D{sides}  →  {result}";
            yield return new WaitForSecondsRealtime(0.58f);
            gameObject.SetActive(false);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            int size,
            FontStyle style,
            TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one);
        }

        private static void SetRect(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
