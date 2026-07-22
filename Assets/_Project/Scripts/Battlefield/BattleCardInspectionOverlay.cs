using System.Collections.Generic;
using AccardND.GameCore;
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
            font = AccardND.Battlefield.MmoUiTheme.BodyFont;
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

            string rules = BuildCardRulesSummary(definition);
            string extra = string.IsNullOrWhiteSpace(extraSummary) ? string.Empty : "\n\n" + extraSummary;
            bool isBossOrMiniboss = IsBossOrMiniboss(definition);
            summaryText.resizeTextMinSize = isBossOrMiniboss ? 15 : 20;
            summaryText.resizeTextMaxSize = isBossOrMiniboss ? 30 : 34;
            summaryText.text = $"{rules}{extra}";

            if (statuses == null)
                return;

            foreach (PrototypeCardView.StatusToken status in statuses)
                AddStatusRow(status);
        }

        private string BuildCardRulesSummary(CardDefinition definition)
        {
            bool isBossOrMiniboss = IsBossOrMiniboss(definition);
            if (!definition.HasHeroClass)
            {
                string rules = string.IsNullOrWhiteSpace(definition.RulesText)
                    ? "Abilita:\nNessuna abilita di combattimento."
                    : "Abilita:\n" + definition.RulesText;
                return $"Potenza: {definition.Strength}\nFamiglia: Nessuna\nClasse: Nessuna\n\nVantaggio contro Nessuno\nSvantaggio contro Nessuno\n\n{rules}";
            }

            ClassFamily family = HeroClassFamily.Of(definition.HeroClass);
            ClassFamily weakAgainst = IsBragusBoss(definition) ? ClassFamily.Cunning : WeakAgainst(family);
            var lines = new List<string>
            {
                $"Potenza: {definition.Strength}",
                "Famiglia: " + CardRulesGlossary.ClassFamilyName(family),
                "Classe: " + CardRulesGlossary.HeroClassName(definition.HeroClass),
                string.Empty,
                "Vantaggio contro " + CardRulesGlossary.ClassFamilyName(StrongAgainst(family)),
                "Svantaggio contro " + CardRulesGlossary.ClassFamilyName(weakAgainst),
            };

            if (!isBossOrMiniboss)
            {
                lines.Add(string.Empty);
                lines.Add("Aura di Famiglia: " + CardRulesGlossary.ClassFamilyName(family) + "\n" + FamilyAuraDescription(family));
                lines.Add("Aura di Classe: " + CardRulesGlossary.HeroClassName(definition.HeroClass) + "\n" + ClassAuraDescription(definition.HeroClass));
                lines.Add(CardRulesGlossary.AbilityTitle(definition.HeroClass) + ":\n" + CardRulesGlossary.AbilityDescription(definition.HeroClass, configuration.ClassBalance));
            }

            if (CanBeEquipped(definition))
            {
                lines.Add($"EQUIPAGGIA: questa carta puo essere sacrificata per potenziare un alleato di +{AttachmentBonus(definition)} permanentemente. Se l'alleato equipaggiato muore, muore anche questa carta.");
            }

            if (!string.IsNullOrWhiteSpace(definition.RulesText))
                lines.Add("Regole:\n" + definition.RulesText);

            return string.Join("\n", lines);
        }

        private static bool IsBossOrMiniboss(CardDefinition definition) =>
            definition != null && definition.Category == CardCategory.Boss;

        private static bool IsBragusBoss(CardDefinition definition) =>
            definition != null && string.Equals(definition.Id, "boss-bragus", System.StringComparison.OrdinalIgnoreCase);

        private static bool CanBeEquipped(CardDefinition definition) =>
            definition != null && definition.CanEnterCombat && definition.Strength >= 2 && definition.Strength < 5;

        private static int AttachmentBonus(CardDefinition definition) => definition != null ? 5 - definition.Strength : 0;

        private static ClassFamily StrongAgainst(ClassFamily family)
        {
            return family switch
            {
                ClassFamily.Might => ClassFamily.Cunning,
                ClassFamily.Cunning => ClassFamily.Magic,
                ClassFamily.Magic => ClassFamily.Might,
                _ => ClassFamily.Might
            };
        }

        private static ClassFamily WeakAgainst(ClassFamily family)
        {
            return family switch
            {
                ClassFamily.Might => ClassFamily.Magic,
                ClassFamily.Cunning => ClassFamily.Might,
                ClassFamily.Magic => ClassFamily.Cunning,
                _ => ClassFamily.Might
            };
        }

        private static string FamilyAuraDescription(ClassFamily family)
        {
            return family switch
            {
                ClassFamily.Might => "Quando muore una pedina qualsiasi, ogni carta con aura Forzuta attiva acquisisce +1 permanente.",
                ClassFamily.Cunning => "le carte Astuta attaccano con vantaggio i nemici che hanno bonus o malus.",
                ClassFamily.Magic => "Le tue carte Magica si difendono con un dado piu forte, esempio: Se ho un D6 mi difendo con un D8.",
                _ => "nessun effetto."
            };
        }

        private static string ClassAuraDescription(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => "i Guerrieri con abilita pronta attaccano con +1.",
                HeroClass.Barbarian => "Furia del Barbaro vale +1 extra.",
                HeroClass.Paladin => "quando un Paladino sopravvive ad una difesa, contrattacca con +1.",
                HeroClass.Rogue => "i Ladri ritirano una volta per dado se esce 1 o 2, in attacco e in difesa.",
                HeroClass.Assassin => "gli Assassini controllano meglio i bersagli inibiti.",
                HeroClass.Hunter => "i Bersagli marcati dal Cacciatore hanno un bonus maggiore.",
                HeroClass.Mage => "il Mago riduce di una taglia extra il dado Vigore nemico.",
                HeroClass.Necromancer => "La prima volta che un tuo alleato viene ucciso, resta in campo per un ultimo turno.",
                HeroClass.Priest => "le Benedizioni del Sacerdote danno un bonus maggiore.",
                _ => "nessun effetto."
            };
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

            summaryText = CreateText("Inspection Summary", bookRoot, 30, FontStyle.Bold, TextAnchor.UpperLeft);
            summaryText.color = new Color(0.16f, 0.085f, 0.025f);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            summaryText.verticalOverflow = VerticalWrapMode.Truncate;
            summaryText.resizeTextForBestFit = true;
            summaryText.resizeTextMinSize = 20;
            summaryText.resizeTextMinSize = 22;
            summaryText.resizeTextMaxSize = 34;

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
                landscape ? new Vector2(0.01f, 0.015f) : new Vector2(0.04f, 0.035f),
                landscape ? new Vector2(0.99f, 0.985f) : new Vector2(0.96f, 0.965f));
            SetAnchors(cardSlot,
                landscape ? new Vector2(0.105f, 0.14f) : new Vector2(0.215f, 0.63f),
                landscape ? new Vector2(0.5f, 0.945f) : new Vector2(0.785f, 0.955f));
            SetAnchors(summaryText.rectTransform,
                landscape ? new Vector2(0.49f, 0.27f) : new Vector2(0.12f, 0.24f),
                landscape ? new Vector2(0.91f, 0.875f) : new Vector2(0.84f, 0.615f));
            SetAnchors(statusRoot,
                landscape ? new Vector2(0.495f, 0.085f) : new Vector2(0.12f, 0.06f),
                landscape ? new Vector2(0.915f, 0.255f) : new Vector2(0.84f, 0.225f));
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

            Text label = CreateText("Status Label", row.transform, 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            label.text = status.Label;
            label.color = new Color(0.16f, 0.085f, 0.025f);
            LayoutElement textLayout = label.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
            textLayout.preferredHeight = 42f;
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
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text);
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
