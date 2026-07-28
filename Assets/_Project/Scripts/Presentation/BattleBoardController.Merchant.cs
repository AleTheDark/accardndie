using System;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	// Vetrina del mercato: due offerte scoperte piu' una carta ignota piu' economica.
	private const int MerchantFaceUpCardOffers = 2;

	private const int MerchantItemOffers = 3;

	private const int MerchantDeckLimit = 12;

	private sealed class MerchantCardOffer
	{
		public CardDefinition Definition;

		public int Cost;

		public bool Mystery;

		public bool Sold;
	}

	private sealed class MerchantItemOffer
	{
		public CampaignConsumableType ItemType;

		public int Cost;

		public bool Sold;
	}

	private void OpenMerchantPanel()
	{
		if (currentRoomType == RoomType.Merchant && !((Object)(object)merchantPanel == (Object)null))
		{
			selectedMerchantSaleCard = null;
			EnsureMerchantStock();
			RefreshMerchantPanel();
			SetImplementationArchiveVisible(visible: false);
			SetMerchantInventoryLauncherVisible(visible: false);
			merchantPanel.SetActive(true);
			ShowMerchantHint();
		}
	}

	private void CloseMerchantPanel()
	{
		if ((Object)(object)merchantPanel != (Object)null)
		{
			merchantPanel.SetActive(false);
			SetMerchantInventoryLauncherVisible(visible: true);
		}
	}

	private void SetMerchantInventoryLauncherVisible(bool visible)
	{
		if ((Object)(object)implementationArchiveButton != (Object)null)
		{
			((Component)implementationArchiveButton).gameObject.SetActive(visible);
		}
		if ((Object)(object)implementationArchiveButtonLabel != (Object)null)
		{
			((Component)implementationArchiveButtonLabel).gameObject.SetActive(visible);
		}
	}

	// --- Stock della stanza ---

	// Azzera la vetrina e il vincolo di banco: chiamato all'ingresso di ogni stanza mercato.
	private void ResetMerchantStock()
	{
		merchantCardOffers.Clear();
		merchantItemOffers.Clear();
		merchantLockedBranch = MerchantBranch.None;
		merchantVisibleBranch = MerchantBranch.Cards;
		merchantShowingGraveyard = false;
		merchantStockRoomKey = -1;
	}

	private void EnsureMerchantStock()
	{
		int roomKey = runProgress?.RoomsCleared ?? 0;
		if (merchantStockRoomKey == roomKey && (merchantCardOffers.Count > 0 || merchantItemOffers.Count > 0))
		{
			return;
		}
		merchantStockRoomKey = roomKey;
		merchantLockedBranch = MerchantBranch.None;
		merchantVisibleBranch = MerchantBranch.Cards;
		merchantShowingGraveyard = false;
		BuildMerchantCardOffers();
		BuildMerchantItemOffers();
	}

	private void BuildMerchantCardOffers()
	{
		merchantCardOffers.Clear();
		List<CardDefinition> pool = GetMerchantCardPool();
		for (int i = 0; i < MerchantFaceUpCardOffers && pool.Count > 0; i++)
		{
			CardDefinition definition = pool[random.NextInclusive(0, pool.Count - 1)];
			pool.Remove(definition);
			merchantCardOffers.Add(new MerchantCardOffer
			{
				Definition = definition,
				Cost = MerchantCardCostFor(definition)
			});
		}
		merchantCardOffers.Add(new MerchantCardOffer
		{
			Mystery = true,
			Cost = configuration.Progression.MerchantMysteryCardCost
		});
	}

	private void BuildMerchantItemOffers()
	{
		merchantItemOffers.Clear();
		List<CampaignConsumableType> pool = Enum.GetValues(typeof(CampaignConsumableType))
			.Cast<CampaignConsumableType>()
			.ToList();
		for (int i = 0; i < MerchantItemOffers && pool.Count > 0; i++)
		{
			CampaignConsumableType itemType = pool[random.NextInclusive(0, pool.Count - 1)];
			pool.Remove(itemType);
			merchantItemOffers.Add(new MerchantItemOffer
			{
				ItemType = itemType,
				Cost = MerchantItemCostFor(itemType)
			});
		}
	}

	// Pool carte del mercato: tutte le forze, solo le classi sbloccate. Le carte gia' presenti
	// nel mazzo sono escluse a monte da GetCampaignRewardPool.
	private List<CardDefinition> GetMerchantCardPool()
	{
		return GetCampaignRewardPool()
			.Where((CardDefinition card) => !card.HasHeroClass || IsHeroClassUnlockedForCampaign(card.HeroClass))
			.ToList();
	}

	// --- Refresh pannello ---

	private void RefreshMerchantPanel()
	{
		if ((Object)(object)merchantPanel == (Object)null)
		{
			return;
		}
		EnsureMerchantStock();
		if ((Object)(object)merchantStatusText != (Object)null)
		{
			merchantStatusText.text =
				$"EXP DISPONIBILE\n<size=30>{runProgress.AvailableExperience}</size>";
		}
		RefreshMerchantBranchTabs();
		RefreshMerchantShelf();
		RefreshMerchantSellText();
		RefreshMerchantActionButtons();
		RefreshMerchantOwnedCards();
		RefreshInitiativeDisplay();
	}

	private void RefreshMerchantBranchTabs()
	{
		ApplyMerchantTabState(
			merchantCardsTabButton,
			merchantCardsTabText,
			merchantCardsTabLockImage,
			merchantCardsTabVfx,
			MerchantBranch.Cards,
			"CARTE");
		ApplyMerchantTabState(
			merchantItemsTabButton,
			merchantItemsTabText,
			merchantItemsTabLockImage,
			merchantItemsTabVfx,
			MerchantBranch.Items,
			"OGGETTI");
	}

	private void ApplyMerchantTabState(
		Button button,
		Text label,
		Image lockImage,
		AccardND.PvpUi.PvpUiVfx vfx,
		MerchantBranch branch,
		string title)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}
		bool locked = IsMerchantBranchLocked(branch);
		bool selected = merchantVisibleBranch == branch;
		button.interactable = !locked;
		ApplyMerchantCampaignCta(
			button,
			selected && !locked
				? "UI/CampaignRestyle/campaign_cta_olive"
				: "UI/CampaignRestyle/campaign_cta_dark_gray");
		if ((Object)(object)label != (Object)null)
		{
			label.text = title;
			label.color = locked
				? new Color(0.66f, 0.7f, 0.72f)
				: (selected ? new Color(0.98f, 0.86f, 0.45f) : new Color(0.88f, 0.93f, 0.95f));
		}
		if ((Object)(object)lockImage != (Object)null)
		{
			((Component)lockImage).gameObject.SetActive(locked);
		}
		if ((Object)(object)vfx != (Object)null)
		{
			((Component)vfx).gameObject.SetActive(selected && !locked);
		}
	}

	private string MerchantShelfHint()
	{
		if (merchantLockedBranch == MerchantBranch.Cards)
		{
			return "Hai scelto il banco delle carte: gli oggetti restano chiusi in questa stanza.";
		}
		if (merchantLockedBranch == MerchantBranch.Items)
		{
			return "Hai scelto il banco degli oggetti: le carte restano chiuse in questa stanza.";
		}
		return "Carte o oggetti: il primo acquisto chiude l'altro banco. Vendere e recuperare resta sempre possibile.";
	}

	private bool IsMerchantBranchLocked(MerchantBranch branch)
	{
		return merchantLockedBranch != MerchantBranch.None && merchantLockedBranch != branch;
	}

	private void SelectMerchantBranch(MerchantBranch branch)
	{
		if (IsMerchantBranchLocked(branch))
		{
			SetMessage(branch == MerchantBranch.Items
				? "MERCATO: hai gia' comprato al banco delle carte. Gli oggetti sono chiusi fino alla prossima stanza."
				: "MERCATO: hai gia' comprato al banco degli oggetti. Le carte sono chiuse fino alla prossima stanza.");
			return;
		}
		if (merchantVisibleBranch == branch)
		{
			return;
		}
		merchantVisibleBranch = branch;
		PlayArrowChangeSfx();
		RefreshMerchantPanel();
	}

	private void RefreshMerchantShelf()
	{
		ClearMerchantShelf();
		if ((Object)(object)merchantShelfRoot == (Object)null)
		{
			return;
		}
		if (merchantVisibleBranch == MerchantBranch.Items)
		{
			foreach (MerchantItemOffer offer in merchantItemOffers)
			{
				BuildMerchantItemSlot(offer);
			}
			return;
		}
		foreach (MerchantCardOffer offer2 in merchantCardOffers)
		{
			BuildMerchantCardSlot(offer2);
		}
	}

	private void ClearMerchantShelf()
	{
		for (int i = merchantShelfViews.Count - 1; i >= 0; i--)
		{
			GameObject slot = merchantShelfViews[i];
			if ((Object)(object)slot != (Object)null)
			{
				// Sgancia subito dal layout: Destroy e' differito e lo slot vecchio resterebbe
				// a occupare spazio nella riga per un frame.
				slot.transform.SetParent(null, false);
				Object.Destroy((Object)(object)slot);
			}
		}
		merchantShelfViews.Clear();
	}

	// --- Acquisti ---

	private void BuyMerchantCardOffer(MerchantCardOffer offer)
	{
		if (offer == null || (Object)(object)merchantPanel == (Object)null || !merchantPanel.activeSelf)
		{
			return;
		}
		if (offer.Sold)
		{
			SetMessage("MERCATO: questa offerta e' gia' stata presa.");
			return;
		}
		if (IsMerchantBranchLocked(MerchantBranch.Cards))
		{
			SetMessage("MERCATO: hai gia' comprato al banco degli oggetti. Le carte sono chiuse fino alla prossima stanza.");
			return;
		}
		if (IsMerchantDeckFull())
		{
			SetMessage($"MERCATO: limite mazzo raggiunto ({MerchantDeckLimit} carte). Vendi una carta prima di comprarne altre.");
			RefreshMerchantPanel();
			return;
		}
		CardDefinition definition = offer.Definition;
		if (offer.Mystery)
		{
			List<CardDefinition> pool = GetMerchantCardPool();
			if (pool.Count == 0)
			{
				SetMessage("MERCATO: il mercante non ha altre carte da offrirti.");
				RefreshMerchantPanel();
				return;
			}
			definition = pool[random.NextInclusive(0, pool.Count - 1)];
		}
		if ((Object)(object)definition == (Object)null)
		{
			SetMessage("MERCATO: questa offerta non e' piu' disponibile.");
			RefreshMerchantPanel();
			return;
		}
		if (!runProgress.TrySpendExperience(offer.Cost))
		{
			SetMessage($"MERCATO: servono {offer.Cost} EXP, disponibili {runProgress.AvailableExperience}.");
			RefreshMerchantPanel();
			return;
		}
		if (!TryAddCardToPlayerCollection(definition))
		{
			runProgress.AddExperience(offer.Cost);
			SetMessage("MERCATO: questa carta e' gia' nel mazzo.");
			RefreshMerchantPanel();
			return;
		}
		// La carta ignota resta acquistabile: e' un pozzo senza fondo limitato solo da EXP e
		// dal tetto del mazzo. Le offerte scoperte invece sono pezzi unici.
		offer.Sold = !offer.Mystery;
		merchantLockedBranch = MerchantBranch.Cards;
		string displayName = CardDisplayNames.MarketName(definition);
		AppendLog($"ACQUISTO - {displayName}, -{offer.Cost} EXP.");
		PlayBuyCardSfx();
		string mystery = offer.Mystery ? "CARTA IGNOTA: " : "ACQUISTO: ";
		SetMessage($"{mystery}{displayName} entra nel mazzo per {offer.Cost} EXP. EXP disponibile: {runProgress.AvailableExperience}.");
		RefreshMerchantPanel();
	}

	private void BuyMerchantItemOffer(MerchantItemOffer offer)
	{
		if (offer == null || (Object)(object)merchantPanel == (Object)null || !merchantPanel.activeSelf)
		{
			return;
		}
		if (offer.Sold)
		{
			SetMessage("MERCATO: questo oggetto e' gia' stato preso.");
			return;
		}
		if (IsMerchantBranchLocked(MerchantBranch.Items))
		{
			SetMessage("MERCATO: hai gia' comprato al banco delle carte. Gli oggetti sono chiusi fino alla prossima stanza.");
			return;
		}
		if (campaignConsumables == null)
		{
			return;
		}
		if (!runProgress.TrySpendExperience(offer.Cost))
		{
			SetMessage($"MERCATO: servono {offer.Cost} EXP, disponibili {runProgress.AvailableExperience}.");
			RefreshMerchantPanel();
			return;
		}
		campaignConsumables.Add(offer.ItemType);
		offer.Sold = true;
		merchantLockedBranch = MerchantBranch.Items;
		string itemName = CampaignConsumableName(offer.ItemType);
		AppendLog($"ACQUISTO OGGETTO - {itemName}, -{offer.Cost} EXP.");
		PlayBuyCardSfx();
		SetMessage($"ACQUISTO: {itemName} entra nella borsa per {offer.Cost} EXP. EXP disponibile: {runProgress.AvailableExperience}.");
		RefreshMerchantPanel();
	}

	// --- Vendita e recupero (sempre disponibili, in entrambi i banchi) ---

	private void RefreshMerchantSellText()
	{
		if ((Object)(object)merchantSellText == (Object)null)
		{
			return;
		}
		if (selectedMerchantSaleCard == null)
		{
			merchantSellText.text = merchantShowingGraveyard
				? "Scegli una pedina da recuperare."
				: "Scegli una pedina da vendere.";
			return;
		}
		CardDefinition definition = selectedMerchantSaleCard.Definition;
		string displayName = CardDisplayNames.MarketName(definition);
		if (selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard)
		{
			merchantSellText.text = $"{displayName}\nRecupero dal Cimitero";
		}
		else
		{
			merchantSellText.text = $"{displayName}\nVendita al Mercante";
		}
	}

	private void RefreshMerchantActionButtons()
	{
		bool hasSelection = selectedMerchantSaleCard != null;
		bool recoveryMode = hasSelection
			? selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard
			: merchantShowingGraveyard;
		if ((Object)(object)merchantSellButton != (Object)null)
		{
			((Component)merchantSellButton).gameObject.SetActive(!recoveryMode);
			merchantSellButton.interactable = hasSelection && !recoveryMode;
			Text sellLabel = ((Component)merchantSellButton).GetComponentInChildren<Text>();
			if ((Object)(object)sellLabel != (Object)null)
			{
				sellLabel.text = hasSelection
					? $"VENDI  +{SellValueFor(selectedMerchantSaleCard.Definition)} EXP"
					: "SELEZIONA CARTA";
			}
		}
		if ((Object)(object)merchantRecoverButton != (Object)null)
		{
			((Component)merchantRecoverButton).gameObject.SetActive(recoveryMode);
			merchantRecoverButton.interactable = hasSelection && recoveryMode;
			Text recoverLabel = ((Component)merchantRecoverButton).GetComponentInChildren<Text>();
			if ((Object)(object)recoverLabel != (Object)null)
			{
				recoverLabel.text = hasSelection
					? $"RECUPERA  -{RecoveryCostFor(selectedMerchantSaleCard.Definition)} EXP"
					: "SELEZIONA CARTA";
			}
		}
	}

	private void RefreshMerchantOwnedCards()
	{
		DestroyPrototypeViews(merchantOwnedCardViews);
		List<CampaignCardInstance> deckCards = GetMerchantDeckCards();
		List<CampaignCardInstance> graveyardCards = GetMerchantGraveyardCards();
		if ((Object)(object)merchantDeckTabText != (Object)null)
		{
			merchantDeckTabText.text = $"MAZZO {deckCards.Count}";
		}
		if ((Object)(object)merchantGraveyardTabText != (Object)null)
		{
			merchantGraveyardTabText.text = $"CIMITERO {graveyardCards.Count}";
		}
		SetMerchantOwnedCardsTabActive(
			merchantDeckTabButton,
			merchantDeckTabVfx,
			!merchantShowingGraveyard);
		SetMerchantOwnedCardsTabActive(
			merchantGraveyardTabButton,
			merchantGraveyardTabVfx,
			merchantShowingGraveyard);
		PopulateMerchantCardSection(
			merchantDeckCardsRoot,
			merchantDeckEmptyText,
			merchantShowingGraveyard ? graveyardCards : deckCards);
	}

	private void SelectMerchantOwnedCardsTab(bool showGraveyard)
	{
		if (merchantShowingGraveyard == showGraveyard)
		{
			return;
		}
		merchantShowingGraveyard = showGraveyard;
		selectedMerchantSaleCard = null;
		PlayArrowChangeSfx();
		RefreshMerchantSellText();
		RefreshMerchantActionButtons();
		RefreshMerchantOwnedCards();
	}

	private void PopulateMerchantCardSection(RectTransform root, Text emptyText, List<CampaignCardInstance> cards)
	{
		if ((Object)(object)root == (Object)null)
		{
			return;
		}
		if ((Object)(object)emptyText != (Object)null)
		{
			((Component)emptyText).gameObject.SetActive(cards.Count == 0);
		}
		GridLayoutGroup grid = ((Component)root).GetComponent<GridLayoutGroup>();
		if ((Object)(object)grid != (Object)null)
		{
			grid.enabled = false;
		}
		ContentSizeFitter fitter = ((Component)root).GetComponent<ContentSizeFitter>();
		if ((Object)(object)fitter != (Object)null)
		{
			fitter.enabled = false;
		}

		int rowCount = Mathf.Max(1, Mathf.CeilToInt(cards.Count / 4f));
		int columns = Mathf.Max(1, Mathf.CeilToInt(cards.Count / (float)rowCount));
		RectTransform viewport = ((Component)root).transform.parent as RectTransform;
		float availableWidth = Mathf.Max(320f, ((Object)(object)viewport != (Object)null ? viewport.rect.width : root.rect.width) - 36f);
		float availableHeight = Mathf.Max(180f, ((Object)(object)viewport != (Object)null ? viewport.rect.height : 260f) - 12f);
		float horizontalGap = rowCount == 1 ? 26f : 18f;
		float maximumCardSize = rowCount == 1 ? 176f : 158f;
		float verticalGap = rowCount == 1 ? 0f : (rowCount >= 3 ? 8f : 22f);
		float topPadding = rowCount == 1 ? 24f : (rowCount >= 3 ? 8f : 18f);
		float bottomPadding = rowCount >= 3 ? 8f : 18f;
		float widthLimitedSize = (availableWidth - horizontalGap * (columns - 1)) / columns;
		float heightLimitedSize =
			(availableHeight - topPadding - bottomPadding - verticalGap * (rowCount - 1)) / rowCount;
		float cardSize = Mathf.Max(76f, Mathf.Min(maximumCardSize, widthLimitedSize, heightLimitedSize));
		float contentHeight = topPadding + rowCount * cardSize + (rowCount - 1) * verticalGap + bottomPadding;
		root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

		List<PrototypeCardView> visibleViews = new List<PrototypeCardView>(cards.Count);
		foreach (CampaignCardInstance card in cards)
		{
			CardDefinition definition = card.Definition;
			PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)root, definition, configuration);
			prototypeCardView.SetInteractable(interactable: true);
			prototypeCardView.SetSelected(card == selectedMerchantSaleCard);
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				SelectMerchantSaleCard(card);
			});
			LayoutElement component = ((Component)prototypeCardView).GetComponent<LayoutElement>();
			if ((Object)(object)component != (Object)null)
			{
				component.minWidth = cardSize;
				component.preferredWidth = cardSize;
				component.minHeight = cardSize;
				component.preferredHeight = cardSize;
				component.flexibleWidth = 0f;
				component.flexibleHeight = 0f;
			}
			visibleViews.Add(prototypeCardView);
			merchantOwnedCardViews.Add(prototypeCardView);
		}

		for (int row = 0, cardIndex = 0; row < rowCount && cardIndex < visibleViews.Count; row++)
		{
			int cardsInRow = Mathf.Min(columns, visibleViews.Count - cardIndex);
			float rowWidth = cardsInRow * cardSize + (cardsInRow - 1) * horizontalGap;
			float firstCenterX = -rowWidth * 0.5f + cardSize * 0.5f;
			for (int column = 0; column < cardsInRow; column++, cardIndex++)
			{
				RectTransform cardRect = (RectTransform)((Component)visibleViews[cardIndex]).transform;
				cardRect.anchorMin = new Vector2(0.5f, 1f);
				cardRect.anchorMax = new Vector2(0.5f, 1f);
				cardRect.pivot = new Vector2(0.5f, 1f);
				cardRect.sizeDelta = new Vector2(cardSize, cardSize);
				cardRect.anchoredPosition = new Vector2(
					firstCenterX + column * (cardSize + horizontalGap),
					-topPadding - row * (cardSize + verticalGap));
			}
		}
	}

	private List<CampaignCardInstance> GetMerchantDeckCards()
	{
		if (campaignDeck == null)
		{
			return new List<CampaignCardInstance>();
		}
		return (from card in campaignDeck.Cards
			where card.Zone != CampaignCardZone.Hand && card.Zone != CampaignCardZone.Battlefield && card.Zone != CampaignCardZone.Graveyard
			orderby MerchantZoneSort(card.Zone), card.Definition.Strength, card.Definition.DisplayName
			select card).ToList();
	}

	private List<CampaignCardInstance> GetMerchantGraveyardCards()
	{
		if (campaignDeck == null)
		{
			return new List<CampaignCardInstance>();
		}
		return (from card in campaignDeck.Cards
			where card.Zone == CampaignCardZone.Graveyard
			orderby card.Definition.Strength, card.Definition.DisplayName
			select card).ToList();
	}

	private static int MerchantZoneSort(CampaignCardZone zone)
	{
		return zone switch
		{
			CampaignCardZone.Deck => 0,
			CampaignCardZone.Cooldown => 1,
			CampaignCardZone.Graveyard => 2,
			_ => 3,
		};
	}

	private void SelectMerchantSaleCard(CampaignCardInstance card)
	{
		selectedMerchantSaleCard = card;
		RefreshMerchantSellText();
		RefreshMerchantActionButtons();
		RefreshMerchantOwnedCards();
	}

	private void SellSelectedMerchantCard()
	{
		if (selectedMerchantSaleCard == null)
		{
			SetMessage("MERCATO: scegli prima una carta da vendere.");
			return;
		}
		if (campaignDeck == null || campaignDeck.Cards.Count <= configuration.DeckBuilding.FormationSize)
		{
			SetMessage("MERCATO: tieni almeno una formazione completa nel mazzo.");
			return;
		}
		if (selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard)
		{
			SetMessage("MERCATO: una carta nel cimitero non puo' essere venduta. Recuperala prima nel mazzo.");
			RefreshMerchantPanel();
			return;
		}
		CardDefinition definition = selectedMerchantSaleCard.Definition;
		int num = SellValueFor(definition);
		if (!campaignDeck.RemoveCard(selectedMerchantSaleCard))
		{
			SetMessage("MERCATO: questa carta non puo' essere venduta adesso.");
			return;
		}
		RemoveCardDefinitionFromList(playerReserve, definition);
		RemoveCardDefinitionFromList(initialPlayerReserve, definition);
		int num2 = runProgress.AddExperience(num);
		selectedMerchantSaleCard = null;
		PlayBuyCardSfx();
		string displayName = CardDisplayNames.MarketName(definition);
		AppendLog($"VENDITA - {displayName}, +{num} EXP.");
		string text = ((num2 > 0) ?$" LEVEL UP: livello {runProgress.PlayerLevel}, D{runProgress.PlayerVigorDieSides}!" : string.Empty);
		SetMessage($"VENDUTA: {displayName}. Ottieni {num} EXP." + text);
		if (num2 > 0)
		{
			ShowLevelUpVigorHint();
		}
		RefreshMerchantPanel();
	}

	private void RecoverSelectedMerchantCard()
	{
		if (selectedMerchantSaleCard == null)
		{
			SetMessage("MERCATO: scegli prima una carta dal cimitero.");
			return;
		}
		if (selectedMerchantSaleCard.Zone != CampaignCardZone.Graveyard)
		{
			SetMessage("MERCATO: questa carta e' gia' fuori dal cimitero.");
			RefreshMerchantPanel();
			return;
		}
		CardDefinition definition = selectedMerchantSaleCard.Definition;
		int num = RecoveryCostFor(definition);
		string displayName = CardDisplayNames.MarketName(definition);
		if (!runProgress.TrySpendExperience(num))
		{
			SetMessage($"MERCATO: servono {num} EXP per recuperare {displayName}, disponibili {runProgress.AvailableExperience}.");
			RefreshMerchantPanel();
		}
		else if (!campaignDeck.RecoverFromGraveyard(selectedMerchantSaleCard))
		{
			SetMessage("MERCATO: questa carta non puo' essere recuperata adesso.");
			RefreshMerchantPanel();
		}
		else
		{
			AppendLog($"RECUPERO MERCATO - {displayName} torna nel mazzo, -{num} EXP.");
			SetMessage($"RECUPERATA: {displayName} torna nel mazzo per {num} EXP. Ora puoi venderla o tenerla.");
			RefreshMerchantPanel();
		}
	}

	// --- Prezzi ---

	private int CurrentMerchantDeckCount()
	{
		return campaignDeck?.Cards.Count ?? 0;
	}

	private bool IsMerchantDeckFull()
	{
		return CurrentMerchantDeckCount() >= MerchantDeckLimit;
	}

	// Il pool contiene tutte le forze, quindi il prezzo scala con la forza: senza questo
	// una carta da 10 costerebbe quanto una da 1.
	private int MerchantCardCostFor(CardDefinition definition)
	{
		ProgressionConfiguration progression = configuration.Progression;
		int strength = ((Object)(object)definition != (Object)null) ? definition.Strength : 0;
		return Math.Max(1, progression.MerchantCardBaseCost + strength * progression.MerchantCardCostPerStrength);
	}

	private static int MerchantItemCostFor(CampaignConsumableType itemType)
	{
		return itemType switch
		{
			CampaignConsumableType.Detector => 12,
			CampaignConsumableType.Defrost => 15,
			CampaignConsumableType.DoubleExp => 18,
			CampaignConsumableType.SigilloRubino => 24,
			CampaignConsumableType.Empower => 22,
			CampaignConsumableType.SecondChance => 26,
			_ => 18,
		};
	}

	private static void RemoveCardDefinitionFromList(List<CardDefinition> cards, CardDefinition definition)
	{
		if (cards != null && !((Object)(object)definition == (Object)null))
		{
			int num = cards.FindIndex((CardDefinition card) => (Object)(object)card != (Object)null && card.Id == definition.Id);
			if (num >= 0)
			{
				cards.RemoveAt(num);
			}
		}
	}

	private static int SellValueFor(CardDefinition definition)
	{
		if (!((Object)(object)definition != (Object)null))
		{
			return 0;
		}
		return Math.Max(3, definition.Strength * 2);
	}

	private static int RecoveryCostFor(CardDefinition definition)
	{
		if (!((Object)(object)definition != (Object)null))
		{
			return 0;
		}
		return Math.Max(3, definition.Strength);
	}
}
}
