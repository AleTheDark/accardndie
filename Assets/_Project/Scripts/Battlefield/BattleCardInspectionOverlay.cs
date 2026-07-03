using System.Collections.Generic;
using AccardND.GameData;
using AccardND.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.Battlefield
{
    public sealed class BattleCardInspectionOverlay
    {
        private readonly Transform parent;
        private readonly GameConfiguration configuration;
        private readonly Font font;
        private GameObject panel;
        private RectTransform bookRoot;
        private Image bookImage;
        private AspectRatioFitter bookAspectFitter;
        private RectTransform cardSlot;
        private Text summaryText;
        private RectTransform statusRoot;
        private readonly List<GameObject> statusRows = new();

        public BattleCardInspectionOverlay(Transform parent, GameConfiguration configuration)
        {
            this.parent = parent;
            this.configuration = configuration != null
                ? configuration
                : ScriptableObject.CreateInstance<GameConfiguration>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Create();
        }

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Show(
            CardDefinition definition,
            IReadOnlyList<PrototypeCardView.StatusToken> statuses = null,
            string extraSummary = null)
        {
            if (definition == null || panel == null)
                return;

            ClearCardSlot();
            ClearStatuses();
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            RefreshLayout();

            PrototypeCardView view = PrototypeCardView.Create(cardSlot, definition, configuration);
            Stretch(view.RectTransform);
            view.SetInteractable(false);
            view.ClearActionOverlay();

            string classText = definition.HasHeroClass ? definition.HeroClass.ToString() : definition.Category.ToString();
            string rules = string.IsNullOrWhiteSpace(definition.RulesText) ? string.Empty : "\n\n" + definition.RulesText;
            string extra = string.IsNullOrWhiteSpace(extraSummary) ? string.Empty : "\n\n" + extraSummary;
            summaryText.text = $"{definition.DisplayName}\nForza {definition.Strength} - {classText}{rules}{extra}";

            if (statuses == null)
                return;

            foreach (PrototypeCardView.StatusToken status in statuses)
                AddStatusRow(status);
        }

        public void Close()
        {
            if (panel == null)
                return;

            ClearCardSlot();
            ClearStatuses();
            panel.SetActive(false);
        }

        public void Destroy()
        {
            if (panel != null)
                Object.Destroy(panel);
            panel = null;
        }

        private void Create()
        {
            panel = new GameObject("Card Inspection Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            panel.transform.SetParent(parent, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            Stretch(panelRect);

            Image background = panel.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            Button closeHotspot = panel.GetComponent<Button>();
            closeHotspot.transition = Selectable.Transition.None;
            closeHotspot.onClick.AddListener(new UnityAction(Close));

            Canvas canvas = panel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1200;
            panel.AddComponent<GraphicRaycaster>();

            bookRoot = new GameObject("Inspection Book Root", typeof(RectTransform)).GetComponent<RectTransform>();
            bookRoot.SetParent(panel.transform, false);
            bookAspectFitter = bookRoot.gameObject.AddComponent<AspectRatioFitter>();
            bookAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            bookImage = new GameObject("Inspection Book", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            bookImage.transform.SetParent(bookRoot, false);
            bookImage.color = Color.white;
            bookImage.preserveAspect = true;
            Stretch(bookImage.rectTransform);

            cardSlot = new GameObject("Inspection Card Slot", typeof(RectTransform)).GetComponent<RectTransform>();
            cardSlot.SetParent(bookRoot, false);

            summaryText = CreateText("Inspection Summary", bookRoot, 22, FontStyle.Bold, TextAnchor.UpperLeft);
            summaryText.color = new Color(0.16f, 0.085f, 0.025f);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Truncate;
            summaryText.resizeTextForBestFit = true;
            summaryText.resizeTextMinSize = 12;
            summaryText.resizeTextMaxSize = 22;

            statusRoot = new GameObject("Inspection Status Rows", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            statusRoot.SetParent(bookRoot, false);
            VerticalLayoutGroup layout = statusRoot.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 7f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            Button closeButton = CreateImageButton("Close Card Inspection", bookRoot, LoadSprite("UI/cancel_button"));
            closeButton.onClick.AddListener(new UnityAction(Close));
            SetAnchors((RectTransform)closeButton.transform, new Vector2(0.82f, 0.865f), new Vector2(0.91f, 0.92f));
            closeButton.transform.SetAsLastSibling();

            RefreshLayout();
            panel.SetActive(false);
        }

        private void RefreshLayout()
        {
            bool landscape = Screen.width >= Screen.height * 1.08f;
            Sprite bookSprite = LoadSprite(landscape ? "UI/card_inspection_landscape" : "UI/card_inspection");
            bookImage.sprite = bookSprite;
            bookAspectFitter.aspectRatio = bookSprite != null
                ? bookSprite.rect.width / bookSprite.rect.height
                : landscape ? 1.4992679f : 0.562799f;

            SetAnchors(bookRoot,
                landscape ? new Vector2(0.035f, 0.055f) : new Vector2(0.04f, 0.035f),
                landscape ? new Vector2(0.965f, 0.945f) : new Vector2(0.96f, 0.965f));
            SetAnchors(cardSlot,
                landscape ? new Vector2(0.115f, 0.12f) : new Vector2(0.235f, 0.57f),
                landscape ? new Vector2(0.485f, 0.9f) : new Vector2(0.765f, 0.885f));
            SetAnchors(summaryText.rectTransform,
                landscape ? new Vector2(0.51f, 0.55f) : new Vector2(0.18f, 0.34f),
                landscape ? new Vector2(0.89f, 0.82f) : new Vector2(0.82f, 0.54f));
            SetAnchors(statusRoot,
                landscape ? new Vector2(0.515f, 0.19f) : new Vector2(0.16f, 0.12f),
                landscape ? new Vector2(0.905f, 0.505f) : new Vector2(0.84f, 0.31f));
        }

        private void AddStatusRow(PrototypeCardView.StatusToken status)
        {
            GameObject row = new GameObject("Inspection Status Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(statusRoot, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;

            Image icon = new GameObject("Status Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(row.transform, false);
            icon.sprite = PrototypeCardView.GetStatusIconSprite(status.Label);
            icon.color = icon.sprite != null ? Color.white : status.Color;
            icon.preserveAspect = true;
            LayoutElement iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 34f;
            iconLayout.preferredHeight = 34f;

            Text label = CreateText("Status Label", row.transform, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = status.Label;
            label.color = new Color(0.16f, 0.085f, 0.025f);
            LayoutElement textLayout = label.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
            textLayout.preferredHeight = 36f;
            statusRows.Add(row);
        }

        private void ClearCardSlot()
        {
            if (cardSlot == null)
                return;
            for (int index = cardSlot.childCount - 1; index >= 0; index--)
                Object.Destroy(cardSlot.GetChild(index).gameObject);
        }

        private void ClearStatuses()
        {
            for (int index = statusRows.Count - 1; index >= 0; index--)
            {
                if (statusRows[index] != null)
                    Object.Destroy(statusRows[index]);
            }
            statusRows.Clear();
        }

        private Text CreateText(string name, Transform textParent, int size, FontStyle style, TextAnchor anchor)
        {
            Text text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(textParent, false);
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateImageButton(string name, Transform buttonParent, Sprite sprite)
        {
            Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).GetComponent<Image>();
            image.transform.SetParent(buttonParent, false);
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            Button button = image.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Sprite LoadSprite(string path) => Resources.Load<Sprite>(path);

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
