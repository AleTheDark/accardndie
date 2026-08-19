using AccardND.Battlefield;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AccardND.Ads
{
    /// <summary>Scena isolata per collaudare entrambi i formati del provider fake.</summary>
    public sealed class FakeAdDebugScene : MonoBehaviour
    {
        private readonly FakeAdProvider provider = new FakeAdProvider();
        private Text result;

        private void Awake()
        {
            EnsureEventSystem();
            BuildUi();
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("Fake Ad Test Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image backdrop = Panel(canvas.transform, "Backdrop", MmoUiTheme.Ink, null);
            Stretch(backdrop.rectTransform, Vector2.zero, Vector2.one);

            Image panel = Panel(canvas.transform, "MMO UI Test Panel", MmoUiTheme.Panel, MmoUiTheme.GetPanelSprite());
            Stretch(panel.rectTransform, new Vector2(0.22f, 0.2f), new Vector2(0.78f, 0.8f));
            MmoUiTheme.AddPanelGem(panel.rectTransform, "Panel Crystal", new Vector2(0.5f, 1f),
                new Vector2(38f, 38f), new Color(0.82f, 0.96f, 1f, 0.86f));

            Text title = Label(panel.transform, "Title", 52, MmoUiTheme.Gold);
            title.text = "Prova annunci fake";
            Stretch(title.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.9f));

            Text description = Label(panel.transform, "Description", 28, MmoUiTheme.TextMuted);
            description.text = "Apri i due formati per verificare frame, font, chiusura e ricompensa.";
            Stretch(description.rectTransform, new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.72f));

            Button interstitial = Button(panel.transform, "Interstitial", "Prova interstitial");
            Stretch((RectTransform)interstitial.transform, new Vector2(0.12f, 0.38f), new Vector2(0.48f, 0.52f));
            interstitial.onClick.AddListener(() => Show(AdPlacement.BagItemUsed));

            Button rewarded = Button(panel.transform, "Rewarded", "Prova rewarded");
            Stretch((RectTransform)rewarded.transform, new Vector2(0.52f, 0.38f), new Vector2(0.88f, 0.52f));
            rewarded.onClick.AddListener(() => Show(AdPlacement.TavernBonusClaim));

            result = Label(panel.transform, "Result", 27, Color.white);
            result.text = "Nessun annuncio mostrato";
            Stretch(result.rectTransform, new Vector2(0.1f, 0.13f), new Vector2(0.9f, 0.3f));
        }

        private async void Show(AdPlacement placement)
        {
            result.text = "Annuncio in corso...";
            AdResult adResult = await provider.ShowAsync(placement, default);
            result.text = $"Esito: {adResult.Outcome}";
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Image Panel(Transform parent, string name, Color color, Sprite sprite)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Image));
            host.transform.SetParent(parent, false);
            Image image = host.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private static Text Label(Transform parent, string name, int size, Color color)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Text));
            host.transform.SetParent(parent, false);
            Text text = host.GetComponent<Text>();
            text.font = MmoUiTheme.LoreFont;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button Button(Transform parent, string name, string caption)
        {
            Image image = Panel(parent, name, Color.white,
                MmoUiTheme.GetButtonSprite(MmoUiTheme.ButtonVariant.Gold));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);
            Text label = Label(image.transform, "Label", 27, Color.white);
            label.text = caption;
            label.raycastTarget = false;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            return button;
        }

        private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
