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
        private Button confirmButton;

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

            root = PvpUiFactory.CreatePanel(parent, "LoadoutBuilder", new Color(0.06f, 0.09f, 0.13f, 0.99f));
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
            Text title = PvpUiFactory.CreateText(root, "Title", "COMPONI IL TUO LOADOUT", 30);
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.03f, 0.93f), new Vector2(0.75f, 0.99f));

            Button cancel = PvpUiFactory.CreateButton(
                root, "Cancel", "INDIETRO", new Color(0.5f, 0.12f, 0.12f, 0.98f), onCancelled, 20);
            PvpUiFactory.SetAnchors((RectTransform)cancel.transform, new Vector2(0.78f, 0.93f), new Vector2(0.97f, 0.99f));

            summaryText = PvpUiFactory.CreateText(root, "Summary", string.Empty, 20, TextAnchor.MiddleLeft, FontStyle.Normal);
            PvpUiFactory.SetAnchors((RectTransform)summaryText.transform, new Vector2(0.03f, 0.87f), new Vector2(0.72f, 0.925f));

            confirmButton = PvpUiFactory.CreateButton(
                root, "Confirm", "CONFERMA", new Color(0.1f, 0.55f, 0.25f, 0.98f), Confirm, 20);
            PvpUiFactory.SetAnchors((RectTransform)confirmButton.transform, new Vector2(0.78f, 0.865f), new Vector2(0.97f, 0.925f));

            // Griglia scorrevole del catalogo.
            RectTransform scrollPanel = PvpUiFactory.CreatePanel(root, "Scroll", new Color(0f, 0f, 0f, 0.4f));
            PvpUiFactory.SetAnchors(scrollPanel, new Vector2(0.02f, 0.24f), new Vector2(0.98f, 0.86f));
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
            grid.cellSize = new Vector2(170f, 200f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = contentHolder.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = gridContent;
            scroll.viewport = scrollPanel;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;

            selectionBar = PvpUiFactory.CreatePanel(root, "Selection", new Color(0.03f, 0.05f, 0.08f, 0.9f));
            PvpUiFactory.SetAnchors(selectionBar, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.23f));
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
                $"CARTE {selection.Count}/{rules.RequiredCardCount}   BUDGET {result.TotalCost}/{rules.Budget}   {state}";
            summaryText.color = result.IsValid && selection.Count == rules.RequiredCardCount
                ? new Color(0.4f, 1f, 0.55f)
                : new Color(1f, 0.85f, 0.4f);
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
                int copies = CountCopies(card);
                var cell = PvpUiFactory.CreatePanel(
                    gridContent, $"Card {card.Id}",
                    copies > 0 ? new Color(0.75f, 0.55f, 0.1f, 0.95f) : new Color(0.13f, 0.2f, 0.28f, 0.95f));

                if (card.Artwork != null)
                {
                    var artHolder = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artHolder.transform.SetParent(cell, false);
                    var art = artHolder.GetComponent<Image>();
                    art.sprite = card.Artwork;
                    art.preserveAspect = true;
                    art.raycastTarget = false;
                    PvpUiFactory.SetAnchors((RectTransform)artHolder.transform, new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.98f));
                }

                Text label = PvpUiFactory.CreateText(
                    cell, "Label",
                    $"{card.DisplayName}  [{card.Strength}]\n{card.HeroClass}" + (copies > 0 ? $"  x{copies}" : string.Empty),
                    15, TextAnchor.LowerCenter, FontStyle.Bold);
                label.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)label.transform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.27f));

                cell.gameObject.AddComponent<Button>().onClick.AddListener(() => AddCard(captured));
            }
        }

        private void RefreshSelectionBar()
        {
            PvpUiFactory.Clear(selectionBar);
            Text caption = PvpUiFactory.CreateText(
                selectionBar, "Caption", "LE TUE 9 CARTE (tocca per rimuovere)", 16, TextAnchor.UpperLeft);
            PvpUiFactory.SetAnchors((RectTransform)caption.transform, new Vector2(0.01f, 0.8f), new Vector2(0.7f, 0.98f));

            for (int index = 0; index < rules.RequiredCardCount; index++)
            {
                float xMin = 0.01f + index * (0.98f / rules.RequiredCardCount);
                var slot = PvpUiFactory.CreatePanel(
                    selectionBar, $"Slot{index}",
                    index < selection.Count ? new Color(0.07f, 0.28f, 0.34f, 0.95f) : new Color(0.1f, 0.12f, 0.16f, 0.9f));
                PvpUiFactory.SetAnchors(
                    slot, new Vector2(xMin, 0.03f), new Vector2(xMin + 0.98f / rules.RequiredCardCount - 0.005f, 0.78f));

                if (index >= selection.Count)
                {
                    PvpUiFactory.CreateText(slot, "Empty", "+", 26);
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
                    PvpUiFactory.SetAnchors((RectTransform)artHolder.transform, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.98f));
                }
                Text label = PvpUiFactory.CreateText(
                    slot, "Label", $"{card.DisplayName} [{card.Strength}]", 13, TextAnchor.LowerCenter, FontStyle.Normal);
                label.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)label.transform, new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.3f));

                int captured = index;
                slot.gameObject.AddComponent<Button>().onClick.AddListener(() => RemoveAt(captured));
            }
        }

        private int CountCopies(CardDefinition card)
        {
            int count = 0;
            foreach (CardDefinition selected in selection)
            {
                if (selected == card)
                    count++;
            }
            return count;
        }

        private void AddCard(CardDefinition card)
        {
            if (selection.Count >= rules.RequiredCardCount)
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
