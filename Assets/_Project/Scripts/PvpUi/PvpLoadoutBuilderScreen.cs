using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.GameData;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Composizione del loadout PvP: 9 carte entro il budget, con limiti per
    /// valore validati in tempo reale (stesse regole del server, che comunque
    /// rivalida). La selezione viene salvata in PlayerPrefs.
    /// </summary>
    internal sealed class PvpLoadoutBuilderScreen
    {
        private const string PrefsKey = "pvp-loadout";

        private readonly RectTransform root;
        private readonly CardDatabase database;
        private readonly PvpLoadoutRules rules = PvpLoadoutRules.CreateDefault();
        private readonly List<CardDefinition> catalog = new();
        private readonly List<CardDefinition> selection = new();
        private readonly UnityAction<PvpLoadoutDto> onConfirmed;
        private readonly UnityAction onCancelled;

        private Text summaryText;
        private RectTransform gridContent;
        private RectTransform selectionBar;
        private RectTransform inspectionOverlay;
        private RectTransform inspectionArtSlot;
        private Text inspectionTitle;
        private Text inspectionBody;
        private Button inspectionBuyButton;
        private Text inspectionBuyText;
        private Button confirmButton;
        private CardDefinition inspectedCard;

        public PvpLoadoutBuilderScreen(
            Transform parent,
            CardDatabase database,
            UnityAction<PvpLoadoutDto> onConfirmed,
            UnityAction onCancelled)
        {
            this.database = database;
            this.onConfirmed = onConfirmed;
            this.onCancelled = onCancelled;
            BuildCatalog();

            root = PvpUiFactory.CreatePanel(parent, "LoadoutBuilder", PvpUiFactory.Ink);
            PvpUiFactory.Stretch(root);
            BuildStaticUi();
            LoadSavedSelection();
            RefreshDynamicUi();
        }

        public void Destroy() => Object.Destroy(root.gameObject);

        /// <summary>Loadout salvato in precedenza, se ancora valido.</summary>
        public static PvpLoadoutDto LoadSaved()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return null;
            PvpLoadoutDto dto = JsonUtility.FromJson<PvpLoadoutDto>(json);
            if (dto?.cards == null || dto.cards.Length == 0)
                return null;
            PvpLoadoutValidationResult result =
                PvpLoadoutValidator.Validate(dto.ToLoadout(), PvpLoadoutRules.CreateDefault());
            return result.IsValid ? dto : null;
        }

        private void BuildCatalog()
        {
            if (database == null)
                return;
            foreach (CardDefinition card in database.Cards)
            {
                if (card != null
                    && card.Category == CardCategory.Monster
                    && card.CanEnterCombat)
                    catalog.Add(card);
            }
            catalog.Sort((left, right) => left.Strength != right.Strength
                ? left.Strength.CompareTo(right.Strength)
                : string.CompareOrdinal(left.Id, right.Id));
        }

        private void BuildStaticUi()
        {
            RectTransform titleBand = PvpUiFactory.CreateTitleBand(
                root, "FORGIA LOADOUT", "9 evocazioni, budget limitato, identita da arena");
            PvpUiFactory.SetAnchors(titleBand, new Vector2(0.03f, 0.89f), new Vector2(0.74f, 0.985f));

            Button cancel = PvpUiFactory.CreateButton(
                root, "Cancel", "INDIETRO", new Color(0.5f, 0.12f, 0.12f, 0.98f), onCancelled, 20);
            PvpUiFactory.SetAnchors((RectTransform)cancel.transform, new Vector2(0.78f, 0.925f), new Vector2(0.97f, 0.985f));

            summaryText = PvpUiFactory.CreateText(root, "Summary", string.Empty, 20, TextAnchor.MiddleLeft, FontStyle.Normal);
            summaryText.color = PvpUiFactory.TextMuted;
            PvpUiFactory.SetAnchors((RectTransform)summaryText.transform, new Vector2(0.04f, 0.815f), new Vector2(0.75f, 0.875f));

            confirmButton = PvpUiFactory.CreateButton(
                root, "Confirm", "CONFERMA", new Color(0.1f, 0.55f, 0.25f, 0.98f), Confirm, 20);
            PvpUiFactory.SetAnchors((RectTransform)confirmButton.transform, new Vector2(0.78f, 0.815f), new Vector2(0.97f, 0.895f));

            // Griglia scorrevole del catalogo.
            RectTransform scrollPanel = PvpUiFactory.CreateSoftPanel(root, "Scroll", new Color(0.018f, 0.028f, 0.045f, 0.92f));
            PvpUiFactory.SetAnchors(scrollPanel, new Vector2(0.02f, 0.2f), new Vector2(0.98f, 0.8f));
            var scroll = scrollPanel.gameObject.AddComponent<ScrollRect>();
            scrollPanel.gameObject.AddComponent<RectMask2D>();

            var contentHolder = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentHolder.transform.SetParent(scrollPanel, false);
            gridContent = (RectTransform)contentHolder.transform;
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0.5f, 1f);
            gridContent.offsetMin = Vector2.zero;
            gridContent.offsetMax = Vector2.zero;
            var grid = contentHolder.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(190f, 226f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(14, 14, 14, 14);
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = contentHolder.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = gridContent;
            scroll.viewport = scrollPanel;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;

            selectionBar = PvpUiFactory.CreateSoftPanel(root, "Selection", new Color(0.025f, 0.04f, 0.065f, 0.95f));
            PvpUiFactory.SetAnchors(selectionBar, new Vector2(0.02f, 0.015f), new Vector2(0.98f, 0.185f));

            BuildInspectionOverlay();
        }

        private void RefreshDynamicUi()
        {
            RefreshSummary();
            RefreshGrid();
            RefreshSelectionBar();
        }

        private PvpLoadoutValidationResult Validate()
        {
            var cards = new List<PvpLoadoutCard>();
            foreach (CardDefinition card in selection)
                cards.Add(new PvpLoadoutCard(card.Id, card.Strength, card.HeroClass));
            return PvpLoadoutValidator.Validate(new PvpLoadout(cards, baseDieSides: 3), rules);
        }

        private void RefreshSummary()
        {
            PvpLoadoutValidationResult result = Validate();
            string state = selection.Count < rules.RequiredCardCount
                ? $"scegli ancora {rules.RequiredCardCount - selection.Count} carte"
                : result.IsValid ? "loadout valido!" : result.Errors[0].Message;
            summaryText.text =
                $"CARTE {selection.Count}/{rules.RequiredCardCount}    BUDGET {result.TotalCost}/{rules.Budget}    {state.ToUpperInvariant()}";
            summaryText.color = result.IsValid && selection.Count == rules.RequiredCardCount
                ? PvpUiFactory.Good
                : PvpUiFactory.Gold;
            confirmButton.interactable = result.IsValid && selection.Count == rules.RequiredCardCount;
        }

        private void RefreshGrid()
        {
            PvpUiFactory.Clear(gridContent);
            if (catalog.Count == 0)
            {
                Text warning = PvpUiFactory.CreateText(
                    gridContent, "NoDb",
                    "CardDatabase non assegnato: verrà usato il loadout predefinito.", 22);
                ((RectTransform)warning.transform).sizeDelta = new Vector2(900f, 80f);
                return;
            }

            foreach (CardDefinition card in catalog)
            {
                CardDefinition captured = card;
                bool selected = IsSelected(card);
                var cell = PvpUiFactory.CreatePanel(
                    gridContent, $"Card {card.Id}",
                    selected ? new Color(0.65f, 0.45f, 0.12f, 0.98f) : new Color(0.075f, 0.12f, 0.17f, 0.96f));

                Text cost = PvpUiFactory.CreateBadge(
                    cell, "Cost", card.Strength.ToString(), selected ? PvpUiFactory.Gold : PvpUiFactory.Copper, 18);
                PvpUiFactory.SetAnchors((RectTransform)cost.transform.parent, new Vector2(0.72f, 0.82f), new Vector2(0.96f, 0.97f));

                if (card.Artwork != null)
                {
                    var artHolder = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artHolder.transform.SetParent(cell, false);
                    var art = artHolder.GetComponent<Image>();
                    art.sprite = card.Artwork;
                    art.preserveAspect = true;
                    art.raycastTarget = false;
                    PvpUiFactory.SetAnchors((RectTransform)artHolder.transform, new Vector2(0.05f, 0.33f), new Vector2(0.95f, 0.98f));
                }

                Text label = PvpUiFactory.CreateText(
                    cell, "Label",
                    $"{card.DisplayName}\n{card.HeroClass}" + (selected ? "  - SCELTA" : string.Empty),
                    15, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.raycastTarget = false;
                label.color = selected ? Color.white : new Color(0.88f, 0.94f, 0.98f);
                PvpUiFactory.SetAnchors((RectTransform)label.transform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.31f));

                Button button = cell.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => ShowInspection(captured));
            }
        }

        private void BuildInspectionOverlay()
        {
            inspectionOverlay = PvpUiFactory.CreatePanel(root, "Loadout Inspection Overlay", new Color(0f, 0f, 0f, 0.88f));
            PvpUiFactory.Stretch(inspectionOverlay);
            inspectionOverlay.gameObject.SetActive(false);

            Button backdropButton = inspectionOverlay.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(CloseInspection);

            RectTransform panel = PvpUiFactory.CreateSoftPanel(inspectionOverlay, "Inspection Panel", new Color(0.03f, 0.045f, 0.065f, 0.98f));
            PvpUiFactory.SetAnchors(panel, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));

            Button close = PvpUiFactory.CreateButton(
                panel, "Close", "X", new Color(0.45f, 0.12f, 0.12f, 0.98f), CloseInspection, 22);
            PvpUiFactory.SetAnchors((RectTransform)close.transform, new Vector2(0.925f, 0.9f), new Vector2(0.985f, 0.975f));

            inspectionArtSlot = PvpUiFactory.CreateSoftPanel(panel, "Art Slot", new Color(0.015f, 0.022f, 0.035f, 0.95f));
            PvpUiFactory.SetAnchors(inspectionArtSlot, new Vector2(0.045f, 0.16f), new Vector2(0.43f, 0.86f));

            inspectionTitle = PvpUiFactory.CreateText(panel, "Title", string.Empty, 28, TextAnchor.MiddleLeft);
            inspectionTitle.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)inspectionTitle.transform, new Vector2(0.47f, 0.76f), new Vector2(0.9f, 0.9f));

            inspectionBody = PvpUiFactory.CreateLabel(panel, "Body", string.Empty, 18, TextAnchor.UpperLeft);
            inspectionBody.color = new Color(0.86f, 0.92f, 0.96f);
            PvpUiFactory.SetAnchors((RectTransform)inspectionBody.transform, new Vector2(0.47f, 0.34f), new Vector2(0.91f, 0.75f));

            inspectionBuyButton = CreateBuyButton(panel);
            PvpUiFactory.SetAnchors((RectTransform)inspectionBuyButton.transform, new Vector2(0.54f, 0.08f), new Vector2(0.84f, 0.3f));
            inspectionBuyButton.onClick.AddListener(BuyInspectedCard);
        }

        private Button CreateBuyButton(Transform parent)
        {
            var holder = new GameObject("Buy Button", typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(parent, false);
            Image image = holder.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/loadout_buy_button");
            image.preserveAspect = true;
            image.color = Color.white;
            Button button = holder.GetComponent<Button>();
            button.targetGraphic = image;

            inspectionBuyText = PvpUiFactory.CreateText(holder.transform, "Label", "AGGIUNGI", 20);
            inspectionBuyText.color = Color.white;
            PvpUiFactory.SetAnchors((RectTransform)inspectionBuyText.transform, new Vector2(0.16f, 0.02f), new Vector2(0.84f, 0.22f));
            return button;
        }

        private void ShowInspection(CardDefinition card)
        {
            inspectedCard = card;
            PvpUiFactory.Clear(inspectionArtSlot);
            if (card?.Artwork != null)
            {
                var artHolder = new GameObject("Artwork", typeof(RectTransform), typeof(Image));
                artHolder.transform.SetParent(inspectionArtSlot, false);
                Image art = artHolder.GetComponent<Image>();
                art.sprite = card.Artwork;
                art.preserveAspect = true;
                art.raycastTarget = false;
                PvpUiFactory.Stretch((RectTransform)artHolder.transform, 8f, 8f);
            }

            string className = card != null ? card.HeroClass.ToString() : string.Empty;
            inspectionTitle.text = card != null ? card.DisplayName : string.Empty;
            inspectionBody.text = card == null
                ? string.Empty
                : $"Classe: {className}\nForza: {card.Strength}\nCosto loadout: {card.Strength}\n\n{CardRulesText(card)}";
            RefreshInspectionBuyState();
            inspectionOverlay.gameObject.SetActive(true);
            inspectionOverlay.SetAsLastSibling();
        }

        private string CardRulesText(CardDefinition card)
        {
            if (!string.IsNullOrWhiteSpace(card.RulesText))
                return card.RulesText;
            return CardRulesGlossary.AbilityTitle(card.HeroClass) + "\n" +
                   CardRulesGlossary.AbilityDescription(card.HeroClass, null);
        }

        private void RefreshInspectionBuyState()
        {
            if (inspectionBuyButton == null || inspectionBuyText == null)
                return;

            bool selected = IsSelected(inspectedCard);
            bool full = selection.Count >= rules.RequiredCardCount;
            bool canBuy = inspectedCard != null && !selected && !full;
            inspectionBuyButton.interactable = canBuy;
            inspectionBuyText.text = selected ? "GIA NEL LOADOUT" : full ? "LOADOUT PIENO" : "AGGIUNGI";
            inspectionBuyText.color = canBuy ? Color.white : new Color(0.7f, 0.72f, 0.74f);
        }

        private void BuyInspectedCard()
        {
            if (inspectedCard == null || IsSelected(inspectedCard) || selection.Count >= rules.RequiredCardCount)
                return;
            AddCard(inspectedCard);
            CloseInspection();
        }

        private void CloseInspection()
        {
            inspectedCard = null;
            if (inspectionOverlay != null)
                inspectionOverlay.gameObject.SetActive(false);
        }

        private void RefreshSelectionBar()
        {
            PvpUiFactory.Clear(selectionBar);
            Text caption = PvpUiFactory.CreateText(
                selectionBar, "Caption", "LOADOUT - TOCCA UNA CARTA PER RIMUOVERLA", 15, TextAnchor.MiddleLeft);
            caption.color = PvpUiFactory.Arcane;
            PvpUiFactory.SetAnchors((RectTransform)caption.transform, new Vector2(0.015f, 0.7f), new Vector2(0.75f, 0.96f));

            for (int index = 0; index < rules.RequiredCardCount; index++)
            {
                float xMin = 0.01f + index * (0.98f / rules.RequiredCardCount);
                var slot = PvpUiFactory.CreatePanel(
                    selectionBar, $"Slot{index}",
                    index < selection.Count ? new Color(0.07f, 0.26f, 0.32f, 0.95f) : new Color(0.075f, 0.09f, 0.12f, 0.9f));
                PvpUiFactory.SetAnchors(
                    slot, new Vector2(xMin, 0.08f), new Vector2(xMin + 0.98f / rules.RequiredCardCount - 0.005f, 0.66f));

                if (index >= selection.Count)
                {
                    Text empty = PvpUiFactory.CreateText(slot, "Empty", "+", 20);
                    empty.color = new Color(0.42f, 0.5f, 0.58f);
                    continue;
                }

                CardDefinition card = selection[index];
                if (card.Artwork != null)
                {
                    var artHolder = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artHolder.transform.SetParent(slot, false);
                    var art = artHolder.GetComponent<Image>();
                    art.sprite = card.Artwork;
                    art.preserveAspect = true;
                    art.raycastTarget = false;
                    PvpUiFactory.SetAnchors((RectTransform)artHolder.transform, new Vector2(0.04f, 0.12f), new Vector2(0.36f, 0.88f));
                }
                Text label = PvpUiFactory.CreateText(
                    slot, "Label", $"{card.DisplayName}\n[{card.Strength}]", 11, TextAnchor.MiddleLeft, FontStyle.Normal);
                label.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)label.transform, new Vector2(card.Artwork != null ? 0.39f : 0.08f, 0.08f), new Vector2(0.96f, 0.92f));

                int captured = index;
                slot.gameObject.AddComponent<Button>().onClick.AddListener(() => RemoveAt(captured));
            }
        }

        private bool IsSelected(CardDefinition card)
        {
            if (card == null)
                return false;
            foreach (CardDefinition selected in selection)
            {
                if (selected != null && selected.Id == card.Id)
                    return true;
            }
            return false;
        }

        private void AddCard(CardDefinition card)
        {
            if (selection.Count >= rules.RequiredCardCount || IsSelected(card))
                return;
            selection.Add(card);
            RefreshDynamicUi();
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= selection.Count)
                return;
            selection.RemoveAt(index);
            RefreshDynamicUi();
        }

        private void Confirm()
        {
            var cards = new LoadoutCardDto[selection.Count];
            for (int index = 0; index < selection.Count; index++)
                cards[index] = new LoadoutCardDto
                {
                    definitionId = selection[index].Id,
                    value = selection[index].Strength,
                    heroClass = (int)selection[index].HeroClass
                };
            var dto = new PvpLoadoutDto
            {
                cards = cards,
                baseDieSides = 3,
                bagDiceSides = new int[0]
            };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(dto));
            PlayerPrefs.Save();
            onConfirmed(dto);
        }

        private void LoadSavedSelection()
        {
            PvpLoadoutDto saved = LoadSaved();
            if (saved?.cards == null || database == null)
                return;
            foreach (LoadoutCardDto card in saved.cards)
            {
                CardDefinition definition = database.FindById(card.definitionId);
                if (definition != null && selection.Count < rules.RequiredCardCount)
                    selection.Add(definition);
            }
        }
    }
}
