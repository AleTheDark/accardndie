using System;
using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.GameData;
using AccardND.Localization;
using AccardND.NetProtocol;
using AccardND.Presentation;
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
        private const string LegacyPrefsKey = "pvp-loadout";
        private const string PrefsKeyPrefix = "pvp-loadout-slot-";
        private const string ActiveSlotPrefsKey = "pvp-loadout-active-slot";
        private const string ClassIconAtlasResource = "UI/DeckBuilder/class_icons_atlas";
        private const string HardcoreLockedEmblemResource = "UI/CampaignRestyle/hardcore_portal_emblem_locked";
        private const string MultiplayerBackdropResource = "UI/MultiplayerRestyle/multiplayer_gothic_hall";
        private static readonly HeroClass[] ClassGridOrder =
        {
            HeroClass.Barbarian, HeroClass.Paladin, HeroClass.Warrior,
            HeroClass.Mage, HeroClass.Necromancer, HeroClass.Priest,
            HeroClass.Assassin, HeroClass.Hunter, HeroClass.Rogue
        };

        private readonly RectTransform root;
        private readonly RectTransform fullscreenBackdrop;
        private RectTransform fullscreenFrameLayer;
        private readonly CardDatabase database;
        private readonly GameConfiguration configuration;
        private readonly PvpLoadoutRules rules = PvpLoadoutRules.CreateDefault();
        private readonly List<CardDefinition> catalog = new();
        private readonly List<CardDefinition> selection = new();
        private readonly List<Button> loadoutTabs = new();
        private readonly UnityAction<PvpLoadoutDto> onConfirmed;
        private readonly UnityAction onCancelled;
        private readonly Action<CardDefinition, UnityAction, bool, string> showCampaignInspection;
        private readonly int unlockedSlotCount;

        private Text summaryText;
        private Text catalogTitle;
        private RectTransform contentRoot;
        private RectTransform scrollPanel;
        private ScrollRect catalogScrollRect;
        private RectTransform catalogScrollbar;
        private RectTransform gridContent;
        private RectTransform selectionBar;
        private RectTransform inspectionOverlay;
        private RectTransform inspectionArtSlot;
        private Text inspectionTitle;
        private Text inspectionBody;
        private Button inspectionBuyButton;
        private Text inspectionBuyText;
        private Button confirmButton;
        private Button backButton;
        private CardDefinition inspectedCard;
        private HeroClass? selectedClass;
        private int activeSlot;

        public PvpLoadoutBuilderScreen(
            Transform parent,
            CardDatabase database,
            GameConfiguration configuration,
            int unlockedSlotCount,
            Action<CardDefinition, UnityAction, bool, string> showCampaignInspection,
            UnityAction<PvpLoadoutDto> onConfirmed,
            UnityAction onCancelled)
        {
            this.database = database;
            this.configuration = configuration;
            this.unlockedSlotCount = Mathf.Clamp(unlockedSlotCount, 1, 4);
            this.showCampaignInspection = showCampaignInspection;
            this.onConfirmed = onConfirmed;
            this.onCancelled = onCancelled;
            activeSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotPrefsKey, 1), 1, this.unlockedSlotCount);
            BuildCatalog();

            Transform backdropParent = parent;
            if (parent != null && parent.GetComponent<SafeAreaRect>() != null && parent.parent != null)
                backdropParent = parent.parent;
            fullscreenBackdrop = CreateMultiplayerBackdrop(backdropParent);

            root = PvpUiFactory.CreatePanel(parent, "LoadoutBuilder", PvpUiFactory.Ink);
            PvpUiFactory.Stretch(root);
            DisablePanelBackground(root);
            BuildStaticUi();
            LoadSavedSelection();
            RefreshDynamicUi();
        }

        public void Destroy()
        {
            if (root != null)
                UnityEngine.Object.Destroy(root.gameObject);
            if (fullscreenBackdrop != null)
                UnityEngine.Object.Destroy(fullscreenBackdrop.gameObject);
            if (fullscreenFrameLayer != null)
                UnityEngine.Object.Destroy(fullscreenFrameLayer.gameObject);
        }

        /// <summary>Loadout salvato in precedenza, se ancora valido.</summary>
        public static PvpLoadoutDto LoadSaved()
        {
            int activeSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotPrefsKey, 1), 1, 4);
            return LoadSaved(activeSlot);
        }

        private static PvpLoadoutDto LoadSaved(int slot)
        {
            string json = PlayerPrefs.GetString(
                PrefsKeyPrefix + Mathf.Clamp(slot, 1, 4),
                slot == 1 ? PlayerPrefs.GetString(LegacyPrefsKey, string.Empty) : string.Empty);
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
            // Usa il canvas full-bleed, come la schermata Arena/Lobby, così la
            // cornice non viene ristretta dalla safe area del contenuto.
            Transform frameParent = root.parent != null && root.parent.GetComponent<SafeAreaRect>() != null
                && root.parent.parent != null
                ? root.parent.parent
                : root.parent;
            fullscreenFrameLayer = PvpUiFactory.CreateContainer(frameParent, "Loadout Frame Layer");
            PvpUiFactory.Stretch(fullscreenFrameLayer);
            if (root.parent != null && root.parent.parent == frameParent)
                fullscreenFrameLayer.SetSiblingIndex(root.parent.GetSiblingIndex());
            PvpUiFactory.CreateScreenOuterFrame(fullscreenFrameLayer, 0.795f);

            RectTransform titleBand = PvpUiFactory.CreateScreenTitlePanel(
                fullscreenFrameLayer,
                "Loadout Title Frame",
                GameText.GetOrFallbackSilent(GameTextKeys.PvpLoadout.Title, "LOADOUT"),
                null,
                50);
            PvpUiFactory.SetAnchors(titleBand, new Vector2(0.08f, 0.785f), new Vector2(0.92f, 0.9f));
            Text titleText = titleBand.Find("Title")?.GetComponent<Text>();
            if (titleText != null)
            {
                titleText.font = MmoUiTheme.LoreFont;
                titleText.fontStyle = FontStyle.Normal;
                titleText.fontSize = 50;
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = 34;
                titleText.resizeTextMaxSize = 50;
            }

            contentRoot = PvpUiFactory.CreateContainer(root, "Loadout Content");
            PvpUiFactory.Stretch(contentRoot);
            contentRoot.anchoredPosition = new Vector2(0f, -150f);

            RectTransform summaryPanel = PvpUiFactory.CreateSoftPanel(contentRoot, "Loadout Summary", new Color(0.035f, 0.06f, 0.09f, 0.96f));
            DisablePanelBackground(summaryPanel);
            PvpUiFactory.SetAnchors(summaryPanel, new Vector2(0.04f, 0.79f), new Vector2(0.76f, 0.885f));
            summaryText = PvpUiFactory.CreateText(summaryPanel, "Summary", string.Empty, 25, TextAnchor.MiddleLeft, FontStyle.Bold);
            summaryText.alignment = TextAnchor.MiddleCenter;
            summaryText.color = PvpUiFactory.TextMuted;
            PvpUiFactory.Stretch((RectTransform)summaryText.transform, 18f, 8f);
            summaryText.rectTransform.anchorMin = Vector2.zero;
            summaryText.rectTransform.anchorMax = Vector2.one;
            summaryText.rectTransform.offsetMin = new Vector2(126f, -12f);
            summaryText.rectTransform.offsetMax = new Vector2(90f, -28f);

            backButton = PvpUiFactory.CreateButton(
                contentRoot, "Back", GameText.GetOrFallbackSilent(GameTextKeys.Common.Back, "INDIETRO"),
                new Color(0.5f, 0.12f, 0.12f, 0.98f), BackFromBuilder, 32);
            MmoUiTheme.ApplyBackButtonStyle(backButton, backButton.GetComponentInChildren<Text>());
            RectTransform backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = new Vector2(0.76f, 0.665f);
            backRect.anchorMax = new Vector2(0.97f, 0.77f);
            backRect.offsetMin = new Vector2(-47f, -48f);
            backRect.offsetMax = new Vector2(-47f, -48f);

            BuildLoadoutTabs();

            catalogTitle = PvpUiFactory.CreateTitleText(
                contentRoot, "Catalog Title", GameText.GetOrFallbackSilent(GameTextKeys.PvpLoadout.ChooseClass, "SCEGLI UNA CLASSE"), 38, TextAnchor.MiddleCenter);
            catalogTitle.font = MmoUiTheme.LoreFont;
            catalogTitle.fontStyle = FontStyle.Normal;
            catalogTitle.resizeTextForBestFit = true;
            catalogTitle.resizeTextMinSize = 28;
            catalogTitle.resizeTextMaxSize = 38;
            catalogTitle.color = PvpUiFactory.Gold;
            catalogTitle.raycastTarget = false;
            PvpUiFactory.SetAnchors((RectTransform)catalogTitle.transform, new Vector2(0.1f, 0.69f), new Vector2(0.9f, 0.745f));
            catalogTitle.rectTransform.offsetMin = new Vector2(0f, -48f);
            catalogTitle.rectTransform.offsetMax = new Vector2(0f, -48f);

            // Griglia scorrevole del catalogo.
            scrollPanel = PvpUiFactory.CreateSoftPanel(contentRoot, "Scroll", new Color(0.018f, 0.028f, 0.045f, 0.92f));
            DisablePanelBackground(scrollPanel);
            PvpUiFactory.SetAnchors(scrollPanel, new Vector2(0.025f, 0.3f), new Vector2(0.975f, 0.685f));
            scrollPanel.offsetMin = new Vector2(0f, -52f);
            scrollPanel.offsetMax = new Vector2(0f, -52f);
            catalogScrollRect = scrollPanel.gameObject.AddComponent<ScrollRect>();
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
            grid.cellSize = new Vector2(220f, 275f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(18, 18, 18, 18);
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = contentHolder.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            catalogScrollRect.content = gridContent;
            catalogScrollRect.viewport = scrollPanel;
            catalogScrollRect.horizontal = false;
            catalogScrollRect.vertical = true;
            catalogScrollRect.scrollSensitivity = 30f;

            var scrollbarObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarObject.transform.SetParent(scrollPanel, false);
            catalogScrollbar = (RectTransform)scrollbarObject.transform;
            PvpUiFactory.SetAnchors(catalogScrollbar, new Vector2(0.952f, 0.025f), new Vector2(0.972f, 0.975f));
            catalogScrollbar.offsetMin = new Vector2(-133.5f, 0f);
            catalogScrollbar.offsetMax = new Vector2(-133.5f, 0f);
            Image scrollbarTrack = scrollbarObject.GetComponent<Image>();
            scrollbarTrack.color = new Color(0.16f, 0.12f, 0.06f, 0.95f);

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(scrollbarObject.transform, false);
            var handleRect = (RectTransform)handleObject.transform;
            PvpUiFactory.SetAnchors(handleRect, Vector2.zero, Vector2.one);
            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.color = PvpUiFactory.Gold;

            Scrollbar verticalScrollbar = scrollbarObject.GetComponent<Scrollbar>();
            verticalScrollbar.handleRect = handleRect;
            verticalScrollbar.targetGraphic = handleImage;
            verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;
            catalogScrollRect.verticalScrollbar = verticalScrollbar;
            catalogScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            catalogScrollRect.verticalScrollbarSpacing = 6f;

            selectionBar = PvpUiFactory.CreateSoftPanel(contentRoot, "Selection", new Color(0.025f, 0.04f, 0.065f, 0.95f));
            DisablePanelBackground(selectionBar);
            PvpUiFactory.SetAnchors(selectionBar, new Vector2(0.04f, 0.025f), new Vector2(0.96f, 0.29f));
            selectionBar.offsetMin = new Vector2(0f, 92f);
            selectionBar.offsetMax = new Vector2(0f, 92f);

            BuildInspectionOverlay();
            backButton.transform.SetAsLastSibling();
        }

        private static void DisablePanelBackground(RectTransform panel)
        {
            Image background = panel != null ? panel.GetComponent<Image>() : null;
            if (background != null)
            {
                background.color = Color.clear;
                background.enabled = false;
            }
        }

        private static RectTransform CreateMultiplayerBackdrop(Transform parent)
        {
            Sprite sprite = Resources.Load<Sprite>(MultiplayerBackdropResource);
            if (sprite == null)
                return null;

            var holder = new GameObject(
                "Gothic Hall Backdrop", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            holder.transform.SetParent(parent, false);
            Image image = holder.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(0.94f, 0.94f, 0.97f, 1f);
            RectTransform rect = (RectTransform)holder.transform;
            PvpUiFactory.Stretch(rect);
            AspectRatioFitter fitter = holder.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            rect.SetAsFirstSibling();
            return rect;
        }

        private void BuildLoadoutTabs()
        {
            for (int index = 1; index <= 4; index++)
            {
                int captured = index;
                bool unlocked = index <= unlockedSlotCount;
                Button tab = PvpUiFactory.CreateButton(
                    contentRoot,
                    $"Loadout Tab {index}",
                    GameText.GetOrFallbackSilent(GameTextKeys.PvpLoadout.SlotTitle, "LOADOUT {0}", index),
                    index == activeSlot ? new Color(0.1f, 0.48f, 0.25f, 0.98f) : new Color(0.08f, 0.14f, 0.18f, 0.98f),
                    () => SelectLoadoutSlot(captured),
                    25);
                float xMin = 0.06f + (index - 1) * 0.22f;
                RectTransform tabRect = (RectTransform)tab.transform;
                PvpUiFactory.SetAnchors(
                    tabRect,
                    new Vector2(xMin, 0.735f), new Vector2(xMin + 0.21f, 0.78f));
                float compactOffset = -(index - 1);
                tabRect.offsetMin = new Vector2(compactOffset, 0f);
                tabRect.offsetMax = new Vector2(compactOffset, 0f);
                tab.interactable = unlocked;
                if (!unlocked)
                {
                    var lockObject = new GameObject("Hardcore Lock", typeof(RectTransform), typeof(Image));
                    lockObject.transform.SetParent(tab.transform, false);
                    Image lockImage = lockObject.GetComponent<Image>();
                    lockImage.sprite = Resources.Load<Sprite>(HardcoreLockedEmblemResource);
                    lockImage.preserveAspect = true;
                    lockImage.raycastTarget = false;
                    PvpUiFactory.SetAnchors(
                        (RectTransform)lockObject.transform,
                        new Vector2(0.04f, 0.08f), new Vector2(0.3f, 0.92f));

                    Text tabLabel = tab.GetComponentInChildren<Text>();
                    if (tabLabel != null)
                        PvpUiFactory.SetAnchors(
                            (RectTransform)tabLabel.transform,
                            new Vector2(0.28f, 0.03f), new Vector2(0.96f, 0.97f));
                }
                loadoutTabs.Add(tab);
            }
        }

        private void RefreshDynamicUi()
        {
            RefreshSelectionBar();
            RefreshSummary();
            RefreshGrid();
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
                ? GameText.Format(GameTextKeys.PvpLoadout.ChooseRemaining, rules.RequiredCardCount - selection.Count)
                : result.IsValid ? GameText.Get(GameTextKeys.PvpLoadout.Valid) : result.Errors[0].Message;
            summaryText.text = GameText.Format(
                GameTextKeys.PvpLoadout.Summary,
                selection.Count,
                rules.RequiredCardCount,
                result.TotalCost,
                rules.Budget,
                state.ToUpperInvariant());
            summaryText.color = result.IsValid && selection.Count == rules.RequiredCardCount
                ? PvpUiFactory.Good
                : PvpUiFactory.Gold;
            confirmButton.interactable = result.IsValid && selection.Count == rules.RequiredCardCount;
        }

        private void RefreshGrid(bool resetScrollPosition = false)
        {
            float previousScrollPosition = catalogScrollRect.verticalNormalizedPosition;
            bool preserveScrollPosition = selectedClass.HasValue && !resetScrollPosition;

            PvpUiFactory.Clear(gridContent);
            if (catalog.Count == 0)
            {
                Text warning = PvpUiFactory.CreateText(
                    gridContent, "NoDb",
                    GameText.Get(GameTextKeys.PvpLoadout.MissingDatabase), 22);
                ((RectTransform)warning.transform).sizeDelta = new Vector2(900f, 80f);
                return;
            }

            if (!selectedClass.HasValue)
            {
                catalogScrollRect.vertical = false;
                catalogScrollRect.verticalNormalizedPosition = 1f;
                catalogScrollbar.gameObject.SetActive(false);
                PvpUiFactory.SetAnchors(scrollPanel, new Vector2(0.025f, 0.3f), new Vector2(0.975f, 0.685f));
                scrollPanel.offsetMin = new Vector2(0f, -52f);
                scrollPanel.offsetMax = new Vector2(0f, -52f);
                catalogTitle.text = GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpLoadout.ChooseClass,
                    "SCEGLI UNA CLASSE");
                GridLayoutGroup classGrid = gridContent.GetComponent<GridLayoutGroup>();
                classGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                classGrid.constraintCount = 3;
                classGrid.cellSize = new Vector2(200f, 200f);
                classGrid.spacing = new Vector2(16f, 16f);
                foreach (HeroClass heroClass in ClassGridOrder)
                    CreateClassCell(heroClass);
                return;
            }

            GridLayoutGroup cardGrid = gridContent.GetComponent<GridLayoutGroup>();
            catalogScrollRect.vertical = true;
            catalogScrollbar.gameObject.SetActive(true);
            PvpUiFactory.SetAnchors(scrollPanel, new Vector2(0.025f, 0.375f), new Vector2(0.975f, 0.685f));
            scrollPanel.offsetMin = new Vector2(0f, -52f);
            scrollPanel.offsetMax = new Vector2(0f, -52f);
            cardGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cardGrid.constraintCount = 1;
            cardGrid.cellSize = new Vector2(650f, 190f);
            cardGrid.spacing = new Vector2(0f, 14f);
            catalogTitle.text = GameText.GetLocalizedFallback(
                GameTextKeys.PvpLoadout.ClassCardCount,
                "{0} · {1} CARTE", "{0} · {1} CARDS", "{0} · {1} KARTEN", "{0} · {1} CARTAS", "{0} · {1} CARTES",
                CardRulesGlossary.HeroClassName(selectedClass.Value).ToUpperInvariant(), 9);
            int shownCards = 0;
            foreach (CardDefinition card in catalog)
            {
                if (card.HeroClass != selectedClass.Value || shownCards >= 9)
                    continue;
                shownCards++;
                CardDefinition captured = card;
                bool selected = IsSelected(card);
                var cell = PvpUiFactory.CreatePanel(
                    gridContent, $"Card {card.Id}",
                    selected ? new Color(0.65f, 0.45f, 0.12f, 0.98f) : new Color(0.075f, 0.12f, 0.17f, 0.96f));

                if (card.Artwork != null)
                {
                    var artHolder = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artHolder.transform.SetParent(cell, false);
                    var art = artHolder.GetComponent<Image>();
                    art.sprite = card.Artwork;
                    art.preserveAspect = true;
                    art.raycastTarget = false;
                    PvpUiFactory.SetAnchors((RectTransform)artHolder.transform, new Vector2(0.02f, 0.06f), new Vector2(0.31f, 0.94f));
                }

                rules.TryGetCardCost(card.Strength, out int loadoutCost);
                Text label = PvpUiFactory.CreateText(
                    cell, "Label",
                    GameText.GetLocalizedFallback(GameTextKeys.PvpLoadout.CardStats,
                        "POTENZA {0} · COSTO {1}", "POWER {0} · COST {1}", "STÄRKE {0} · KOSTEN {1}", "PODER {0} · COSTE {1}", "PUISSANCE {0} · COÛT {1}",
                        card.Strength, loadoutCost),
                    30, TextAnchor.MiddleCenter, FontStyle.Bold);
                label.raycastTarget = false;
                label.color = selected ? Color.white : new Color(0.88f, 0.94f, 0.98f);
                PvpUiFactory.SetAnchors((RectTransform)label.transform, new Vector2(0.33f, 0.56f), new Vector2(0.97f, 0.94f));

                Button info = PvpUiFactory.CreateButton(
                    cell, "Info", GameText.GetLocalizedFallback(GameTextKeys.PvpLoadout.Info, "INFO", "INFO", "INFO", "INFO", "INFO"), PvpUiFactory.Copper,
                    () => ShowInspection(captured), 20);
                PvpUiFactory.SetAnchors(
                    (RectTransform)info.transform, new Vector2(0.35f, 0.1f), new Vector2(0.62f, 0.48f));

                Button choose = selected
                    ? PvpUiFactory.CreateButton(
                        cell, "Remove", GameText.GetLocalizedFallback(GameTextKeys.PvpLoadout.Remove, "RIMUOVI", "REMOVE", "ENTFERNEN", "QUITAR", "RETIRER"), new Color(0.7f, 0.08f, 0.06f, 1f),
                        () => RemoveCard(captured), 20)
                    : PvpUiFactory.CreateButton(
                        cell, "Choose", GameText.GetLocalizedFallback(GameTextKeys.PvpLoadout.Choose, "SCEGLI", "CHOOSE", "WÄHLEN", "ELEGIR", "CHOISIR"), PvpUiFactory.Good,
                        () => AddCard(captured), 20);
                PvpUiFactory.SetAnchors(
                    (RectTransform)choose.transform, new Vector2(0.67f, 0.1f), new Vector2(0.94f, 0.48f));
                choose.interactable = selected || selection.Count < rules.RequiredCardCount;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
            catalogScrollRect.StopMovement();
            catalogScrollRect.verticalNormalizedPosition = preserveScrollPosition
                ? previousScrollPosition
                : 1f;
        }

        private void CreateClassCell(HeroClass heroClass)
        {
            HeroClass captured = heroClass;
            RectTransform cell = PvpUiFactory.CreatePanel(
                gridContent, $"Class {heroClass}", new Color(1f, 1f, 1f, 0.001f));
            Sprite classIcon = LoadClassIcon(heroClass);
            if (classIcon != null)
            {
                var artObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(cell, false);
                Image art = artObject.GetComponent<Image>();
                art.sprite = classIcon;
                art.preserveAspect = true;
                art.raycastTarget = false;
                PvpUiFactory.SetAnchors((RectTransform)artObject.transform, new Vector2(0.08f, 0.31f), new Vector2(0.92f, 0.96f));
            }

            Text label = PvpUiFactory.CreateText(
                cell, "Class Name",
                GameText.GetOrFallbackSilent(
                    GameTextKeys.Rules.HeroClassName(heroClass.ToString().ToLowerInvariant()),
                    heroClass.ToString()).ToUpperInvariant(),
                22,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            label.color = PvpUiFactory.Gold;
            label.raycastTarget = false;
            PvpUiFactory.SetAnchors((RectTransform)label.transform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.25f));
            cell.gameObject.AddComponent<Button>().onClick.AddListener(() => SelectClass(captured));
        }

        private static Sprite LoadClassIcon(HeroClass heroClass)
        {
            string expectedName = "class_" + heroClass.ToString().ToLowerInvariant();
            foreach (Sprite sprite in Resources.LoadAll<Sprite>(ClassIconAtlasResource))
            {
                if (sprite != null && string.Equals(sprite.name, expectedName, StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }
            return null;
        }

        private void SelectClass(HeroClass heroClass)
        {
            selectedClass = heroClass;
            RefreshGrid(resetScrollPosition: true);
        }

        private void ShowClasses()
        {
            selectedClass = null;
            RefreshGrid();
        }

        private void BackFromBuilder()
        {
            if (selectedClass.HasValue)
            {
                ShowClasses();
                return;
            }
            onCancelled?.Invoke();
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

            inspectionBuyText = PvpUiFactory.CreateText(
                holder.transform, "Label", GameText.Get(GameTextKeys.PvpLoadout.Add), 20);
            inspectionBuyText.color = Color.white;
            PvpUiFactory.SetAnchors((RectTransform)inspectionBuyText.transform, new Vector2(0.16f, 0.02f), new Vector2(0.84f, 0.22f));
            return button;
        }

        private void ShowInspection(CardDefinition card)
        {
            inspectedCard = card;
            if (showCampaignInspection != null)
            {
                bool selected = IsSelected(card);
                bool full = selection.Count >= rules.RequiredCardCount;
                string buttonText = selected
                    ? GameText.Get(GameTextKeys.PvpLoadout.AlreadyAdded)
                    : full
                        ? GameText.Get(GameTextKeys.PvpLoadout.Full)
                        : GameText.Get(GameTextKeys.PvpLoadout.Add);
                showCampaignInspection(card, BuyInspectedCard, card != null && !selected && !full, buttonText);
                return;
            }
            PvpUiFactory.Clear(inspectionArtSlot);
            if (card?.Artwork != null)
            {
                PrototypeCardView cardView = PrototypeCardView.Create(inspectionArtSlot, card, configuration);
                RectTransform cardRect = (RectTransform)cardView.transform;
                PvpUiFactory.Stretch(cardRect, 8f, 8f);
                cardRect.SetAsFirstSibling();
            }

            string className = card != null ? card.HeroClass.ToString() : string.Empty;
            inspectionTitle.text = card != null ? card.DisplayName : string.Empty;
            inspectionBody.text = card == null
                ? string.Empty
                : GameText.Format(
                    GameTextKeys.PvpLoadout.InspectionBody,
                    className,
                    card.Strength,
                    card.Strength,
                    CardRulesText(card));
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
            inspectionBuyText.text = selected
                ? GameText.Get(GameTextKeys.PvpLoadout.AlreadyAdded)
                : full
                    ? GameText.Get(GameTextKeys.PvpLoadout.Full)
                    : GameText.Get(GameTextKeys.PvpLoadout.Add);
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
                selectionBar, "Caption", GameText.Get(GameTextKeys.PvpLoadout.YourLoadout), 40, TextAnchor.MiddleCenter);
            caption.font = MmoUiTheme.LoreFont;
            caption.fontStyle = FontStyle.Normal;
            caption.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)caption.transform, new Vector2(0.18f, 0.9f), new Vector2(0.7f, 0.99f));

            confirmButton = PvpUiFactory.CreateButton(
                selectionBar,
                "Confirm",
                GameText.Get(GameTextKeys.PvpLoadout.Save),
                new Color(0.1f, 0.55f, 0.25f, 0.98f),
                Confirm,
                24);
            PvpUiFactory.SetAnchors(
                (RectTransform)confirmButton.transform,
                new Vector2(0.73f, 0.82f),
                new Vector2(0.94f, 0.99f));
            MmoUiTheme.ApplyConfirmButtonStyle(confirmButton, confirmButton.GetComponentInChildren<Text>());

            Text hint = PvpUiFactory.CreateLabel(
                selectionBar, "Hint", GameText.Get(GameTextKeys.PvpLoadout.RemoveHint), 25, TextAnchor.MiddleCenter);
            PvpUiFactory.SetAnchors((RectTransform)hint.transform, new Vector2(0.16f, 0.8f), new Vector2(0.84f, 0.9f));

            for (int index = 0; index < rules.RequiredCardCount; index++)
            {
                const int columns = 3;
                int column = index % columns;
                int row = index / columns;
                float xMin = 0.055f + column * 0.315f;
                float xMax = xMin + 0.26f;
                float yMax = 0.79f - row * 0.245f;
                float yMin = yMax - 0.215f;
                var slot = PvpUiFactory.CreatePanel(
                    selectionBar, $"Slot{index}",
                    index < selection.Count ? new Color(0.07f, 0.26f, 0.32f, 0.95f) : new Color(0.075f, 0.09f, 0.12f, 0.9f));
                PvpUiFactory.SetAnchors(
                    slot, new Vector2(xMin, yMin), new Vector2(xMax, yMax));

                if (index >= selection.Count)
                {
                    Text empty = PvpUiFactory.CreateText(slot, "Empty", "+", 30);
                    empty.color = new Color(0.42f, 0.5f, 0.58f);
                    continue;
                }

                CardDefinition card = selection[index];
                Text label = PvpUiFactory.CreateText(
                    slot,
                    "Label",
                    GameText.GetLocalizedFallback(GameTextKeys.PvpLoadout.CardStrength,
                        "POTENZA {0}", "POWER {0}", "STÄRKE {0}", "PODER {0}", "PUISSANCE {0}", card.Strength),
                    25,
                    TextAnchor.MiddleCenter,
                    FontStyle.Bold);
                label.raycastTarget = false;
                PvpUiFactory.SetAnchors(
                    (RectTransform)label.transform,
                    new Vector2(0.36f, 0.08f),
                    new Vector2(0.94f, 0.92f));

                Sprite classIcon = LoadClassIcon(card.HeroClass);
                if (classIcon != null)
                {
                    var classIconObject = new GameObject("Class Icon", typeof(RectTransform), typeof(Image));
                    classIconObject.transform.SetParent(slot, false);
                    Image classImage = classIconObject.GetComponent<Image>();
                    classImage.sprite = classIcon;
                    classImage.preserveAspect = true;
                    classImage.raycastTarget = false;
                    PvpUiFactory.SetAnchors(
                        (RectTransform)classIconObject.transform,
                        new Vector2(0.05f, 0.08f), new Vector2(0.35f, 0.92f));
                }

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

        private void RemoveCard(CardDefinition card)
        {
            if (card == null)
                return;
            for (int index = 0; index < selection.Count; index++)
            {
                if (selection[index] != null && selection[index].Id == card.Id)
                {
                    RemoveAt(index);
                    return;
                }
            }
        }

        private void SelectLoadoutSlot(int slot)
        {
            if (slot < 1 || slot > unlockedSlotCount || slot == activeSlot)
                return;
            activeSlot = slot;
            PlayerPrefs.SetInt(ActiveSlotPrefsKey, activeSlot);
            PlayerPrefs.Save();
            selection.Clear();
            selectedClass = null;
            LoadSavedSelection();
            RefreshLoadoutTabs();
            RefreshDynamicUi();
        }

        private void RefreshLoadoutTabs()
        {
            for (int index = 0; index < loadoutTabs.Count; index++)
            {
                Image image = loadoutTabs[index] != null ? loadoutTabs[index].GetComponent<Image>() : null;
                if (image != null)
                    image.color = index + 1 == activeSlot
                        ? new Color(0.1f, 0.48f, 0.25f, 0.98f)
                        : new Color(0.08f, 0.14f, 0.18f, 0.98f);
            }
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
            PlayerPrefs.SetString(PrefsKeyPrefix + activeSlot, JsonUtility.ToJson(dto));
            PlayerPrefs.SetInt(ActiveSlotPrefsKey, activeSlot);
            PlayerPrefs.Save();
            onConfirmed(dto);
        }

        private void LoadSavedSelection()
        {
            PvpLoadoutDto saved = LoadSaved(activeSlot);
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
