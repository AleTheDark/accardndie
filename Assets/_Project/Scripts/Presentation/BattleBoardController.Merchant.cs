using System;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
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
	private const int MerchantMaximumUpgrades = 2;
	private const string MerchantUpgradeRelicOneId = "merchant-upgrade-relic-1";
	private const string MerchantUpgradeRelicTwoId = "merchant-upgrade-relic-2";

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
		}
	}

	private void CloseMerchantPanel()
	{
		HideMerchantBranchConfirmPopup();
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
		HideMerchantBranchConfirmPopup();
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

	// La carta ignota non deve coincidere con una carta scoperta ancora in vetrina.
	// In caso contrario, comprando prima l'ignota il mercante continuerebbe a mostrare
	// come acquistabile una carta che il giocatore ha appena aggiunto al mazzo.
	private List<CardDefinition> GetMerchantMysteryCardPool()
	{
		List<CardDefinition> pool = GetMerchantCardPool();
		pool.RemoveAll(candidate => merchantCardOffers.Any(offer =>
			!offer.Sold
			&& !offer.Mystery
			&& (Object)(object)offer.Definition != (Object)null
			&& CardPurchaseUniqueness.AreEquivalent(offer.Definition, candidate)));
		return pool;
	}

	// --- Refresh pannello ---

	private void RefreshMerchantPanel()
	{
		RefreshBagGoldCounter();
		if ((Object)(object)merchantPanel == (Object)null)
		{
			return;
		}
		EnsureMerchantStock();
		if ((Object)(object)merchantStatusText != (Object)null)
		{
			merchantStatusText.text = GameText.Format(GameTextKeys.Merchant.GoldAvailable, runProgress.Gold);
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
			GameText.Get(GameTextKeys.Merchant.BranchCards));
		ApplyMerchantTabState(
			merchantItemsTabButton,
			merchantItemsTabText,
			merchantItemsTabLockImage,
			merchantItemsTabVfx,
			MerchantBranch.Items,
			GameText.Get(GameTextKeys.Merchant.BranchItems));
		ApplyMerchantTabState(
			merchantUpgradesTabButton,
			merchantUpgradesTabText,
			merchantUpgradesTabLockImage,
			merchantUpgradesTabVfx,
			MerchantBranch.Upgrades,
			GameText.Get(GameTextKeys.Merchant.BranchUpgrades));
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
			label.fontSize = locked ? 28 : 30;
			label.resizeTextMaxSize = locked ? 28 : 30;
			label.color = locked
				? new Color(0.66f, 0.7f, 0.72f)
				: (selected ? new Color(0.98f, 0.86f, 0.45f) : new Color(0.88f, 0.93f, 0.95f));
			SetRect(
				label.rectTransform,
				new Vector2(0.04f, 0.06f),
				locked ? new Vector2(0.76f, 0.94f) : new Vector2(0.96f, 0.94f));
		}
		if ((Object)(object)lockImage != (Object)null)
		{
			((Component)lockImage).gameObject.SetActive(locked);
			if (locked)
			{
				lockImage.rectTransform.SetAsLastSibling();
			}
		}
		if ((Object)(object)vfx != (Object)null)
		{
			((Component)vfx).gameObject.SetActive(selected && !locked);
		}
	}

	private bool IsMerchantBranchLocked(MerchantBranch branch)
	{
		return branch != MerchantBranch.Upgrades
			&& merchantLockedBranch != MerchantBranch.None
			&& merchantLockedBranch != branch;
	}

	private void SelectMerchantBranch(MerchantBranch branch)
	{
		if (IsMerchantBranchLocked(branch))
		{
			SetMessage("MERCATO: il primo acquisto ha chiuso questo banco fino alla prossima stanza.");
			return;
		}
		if (merchantVisibleBranch == branch)
		{
			return;
		}
		merchantVisibleBranch = branch;
		selectedMerchantSaleCard = null;
		merchantShowingGraveyard = false;
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
		if (merchantVisibleBranch == MerchantBranch.Upgrades)
		{
			BuildMerchantUpgradeInfoSlot();
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
		BuyMerchantCardOffer(offer, branchLockConfirmed: false);
	}

	private void BuyMerchantCardOffer(MerchantCardOffer offer, bool branchLockConfirmed)
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
			List<CardDefinition> pool = GetMerchantMysteryCardPool();
			if (pool.Count == 0)
			{
				SetMessage(GameText.Get(GameTextKeys.Merchant.NoMoreCards));
				RefreshMerchantPanel();
				return;
			}
		}
		else if ((Object)(object)definition == (Object)null)
		{
			SetMessage("MERCATO: questa offerta non e' piu' disponibile.");
			RefreshMerchantPanel();
			return;
		}
		int cost = EffectiveMerchantCost(offer.Cost);
		if (runProgress.Gold < cost)
		{
			SetMessage(GameText.Format(GameTextKeys.Merchant.InsufficientGold, cost, runProgress.Gold));
			RefreshMerchantPanel();
			return;
		}
		if (merchantLockedBranch == MerchantBranch.None && !branchLockConfirmed)
		{
			string purchaseName = offer.Mystery
				? GameText.Get(GameTextKeys.Merchant.UnknownCard)
				: CardDisplayNames.MarketName(definition);
			ShowMerchantBranchConfirmPopup(
				MerchantBranch.Cards,
				purchaseName,
				cost,
				() => BuyMerchantCardOffer(offer, branchLockConfirmed: true));
			return;
		}
		if (offer.Mystery)
		{
			// La carta ignota viene estratta solo dopo la conferma, cosi' Annulla non consuma
			// neppure una scelta casuale del mercato.
			List<CardDefinition> pool = GetMerchantMysteryCardPool();
			if (pool.Count == 0)
			{
				SetMessage(GameText.Get(GameTextKeys.Merchant.NoMoreCards));
				RefreshMerchantPanel();
				return;
			}
			definition = pool[random.NextInclusive(0, pool.Count - 1)];
		}
		if (!runProgress.TrySpendGold(cost))
		{
			SetMessage(GameText.Format(GameTextKeys.Merchant.InsufficientGold, cost, runProgress.Gold));
			RefreshMerchantPanel();
			return;
		}
		if (!TryAddCardToPlayerCollection(definition))
		{
			runProgress.AddGold(cost);
			SetMessage("MERCATO: questa carta e' gia' nel mazzo.");
			RefreshMerchantPanel();
			return;
		}
		// La carta ignota resta acquistabile: e' un pozzo senza fondo limitato solo da oro e
		// dal tetto del mazzo. Le offerte scoperte invece sono pezzi unici.
		offer.Sold = !offer.Mystery;
		merchantLockedBranch = MerchantBranch.Cards;
		if (ShouldTrackQuestProgress)
			runProgress.RecordMerchantPurchase();
		string displayName = CardDisplayNames.MarketName(definition);
		AppendLog(GameText.Format(GameTextKeys.Merchant.CardPurchaseLog, displayName, cost));
		PlayBuyCardSfx();
		string mystery = offer.Mystery
			? GameText.Get(GameTextKeys.Merchant.MysteryPurchasePrefix)
			: GameText.Get(GameTextKeys.Merchant.PurchasePrefix);
		SetMessage(GameText.Format(GameTextKeys.Merchant.CardPurchased, mystery, displayName, cost, runProgress.Gold));
		RefreshMerchantPanel();
	}

	private void BuyMerchantItemOffer(MerchantItemOffer offer)
	{
		BuyMerchantItemOffer(offer, branchLockConfirmed: false);
	}

	private void BuyMerchantItemOffer(MerchantItemOffer offer, bool branchLockConfirmed)
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
		int cost = EffectiveMerchantCost(offer.Cost);
		if (runProgress.Gold < cost)
		{
			SetMessage(GameText.Format(GameTextKeys.Merchant.InsufficientGold, cost, runProgress.Gold));
			RefreshMerchantPanel();
			return;
		}
		if (merchantLockedBranch == MerchantBranch.None && !branchLockConfirmed)
		{
			ShowMerchantBranchConfirmPopup(
				MerchantBranch.Items,
				CampaignConsumableName(offer.ItemType),
				cost,
				() => BuyMerchantItemOffer(offer, branchLockConfirmed: true));
			return;
		}
		if (!runProgress.TrySpendGold(cost))
		{
			SetMessage(GameText.Format(GameTextKeys.Merchant.InsufficientGold, cost, runProgress.Gold));
			RefreshMerchantPanel();
			return;
		}
		campaignConsumables.Add(offer.ItemType);
		offer.Sold = true;
		merchantLockedBranch = MerchantBranch.Items;
		if (ShouldTrackQuestProgress)
			runProgress.RecordMerchantPurchase();
		string itemName = CampaignConsumableName(offer.ItemType);
		AppendLog(GameText.Format(GameTextKeys.Merchant.ItemPurchaseLog, itemName, cost));
		PlayBuyCardSfx();
		SetMessage(GameText.Format(GameTextKeys.Merchant.ItemPurchased, itemName, cost, runProgress.Gold));
		RefreshMerchantPanel();
	}

	private void ShowMerchantBranchConfirmPopup(
		MerchantBranch chosenBranch,
		string purchaseName,
		int cost,
		Action confirmAction)
	{
		if ((Object)(object)merchantBranchConfirmPopup == (Object)null)
		{
			confirmAction?.Invoke();
			return;
		}
		string cardsBranch = GameText.Get(GameTextKeys.Merchant.BranchCards);
		string itemsBranch = GameText.Get(GameTextKeys.Merchant.BranchItems);
		string chosenName = chosenBranch switch
		{
			MerchantBranch.Cards => cardsBranch,
			MerchantBranch.Items => itemsBranch,
			_ => "POTENZIA"
		};
		string closedName = chosenBranch switch
		{
			MerchantBranch.Cards => "OGGETTI",
			MerchantBranch.Items => "CARTE",
			_ => string.Empty
		};
		merchantBranchConfirmTitleText.text = GameText.Format(GameTextKeys.Merchant.BranchConfirmTitle, chosenName);
		merchantBranchConfirmBodyText.text = GameText.Format(GameTextKeys.Merchant.BranchConfirmBody, purchaseName, cost, closedName);
		merchantBranchConfirmAction = confirmAction;
		merchantBranchConfirmPopup.SetActive(true);
		merchantBranchConfirmPopup.transform.SetAsLastSibling();
	}

	private void HideMerchantBranchConfirmPopup()
	{
		if ((Object)(object)merchantBranchConfirmPopup != (Object)null)
		{
			merchantBranchConfirmPopup.SetActive(false);
		}
		merchantBranchConfirmAction = null;
	}

	// --- Vendita e recupero (sempre disponibili, in entrambi i banchi) ---

	private void RefreshMerchantSellText()
	{
		if ((Object)(object)merchantSellText == (Object)null)
		{
			return;
		}
		bool upgradePage = merchantVisibleBranch == MerchantBranch.Upgrades;
		if (selectedMerchantSaleCard == null)
		{
			merchantSellText.text = upgradePage
				? GameText.Get(GameTextKeys.Merchant.UpgradeSelectionHint)
				: merchantShowingGraveyard
				? "Scegli una pedina da recuperare."
				: "Scegli una pedina da vendere.";
			return;
		}
		CardDefinition definition = selectedMerchantSaleCard.Definition;
		string displayName = CardDisplayNames.MarketName(definition);
		if (selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard)
		{
			merchantSellText.text = GameText.Format(GameTextKeys.Merchant.RecoverDescription, displayName);
		}
		else if (upgradePage)
		{
			int strength = definition.Strength + selectedMerchantSaleCard.PermanentItemBonus;
			merchantSellText.text = selectedMerchantSaleCard.MerchantUpgradeCount >= MerchantMaximumUpgrades
				? GameText.Format(GameTextKeys.Merchant.UpgradeCardMaximumDescription, displayName, strength)
				: GameText.Format(GameTextKeys.Merchant.UpgradeCardDescription, displayName, strength, UpgradeCostFor(selectedMerchantSaleCard));
		}
		else
		{
			merchantSellText.text = GameText.Format(GameTextKeys.Merchant.SellDescription, displayName);
		}
	}

	private void RefreshMerchantActionButtons()
	{
		bool hasSelection = selectedMerchantSaleCard != null;
		bool upgradePage = merchantVisibleBranch == MerchantBranch.Upgrades;
		bool recoveryMode = !upgradePage && (hasSelection
			? selectedMerchantSaleCard.Zone == CampaignCardZone.Graveyard
			: merchantShowingGraveyard);
		if ((Object)(object)merchantSellButton != (Object)null)
		{
			((Component)merchantSellButton).gameObject.SetActive(!recoveryMode && !upgradePage);
			merchantSellButton.interactable = hasSelection && !recoveryMode && !upgradePage;
			Text sellLabel = ((Component)merchantSellButton).GetComponentInChildren<Text>();
			if ((Object)(object)sellLabel != (Object)null)
			{
				sellLabel.text = hasSelection
					? GameText.Format(GameTextKeys.Merchant.SellForGold, SellValueFor(selectedMerchantSaleCard.Definition))
					: GameText.Get(GameTextKeys.Merchant.SelectCard);
			}
		}
		if ((Object)(object)merchantRecoverButton != (Object)null)
		{
			((Component)merchantRecoverButton).gameObject.SetActive(recoveryMode);
			bool canAffordRecovery = hasSelection
				&& (runProgress?.Gold ?? 0) >= RecoveryCostFor(selectedMerchantSaleCard.Definition);
			merchantRecoverButton.interactable = hasSelection && recoveryMode && canAffordRecovery;
			Text recoverLabel = ((Component)merchantRecoverButton).GetComponentInChildren<Text>();
			if ((Object)(object)recoverLabel != (Object)null)
			{
				recoverLabel.text = hasSelection
					? GameText.Format(GameTextKeys.Merchant.RecoverForGold, RecoveryCostFor(selectedMerchantSaleCard.Definition))
					: GameText.Get(GameTextKeys.Merchant.SelectCard);
			}
		}
		if ((Object)(object)merchantUpgradeButton != (Object)null)
		{
			((Component)merchantUpgradeButton).gameObject.SetActive(upgradePage);
			bool upgradeEligible = hasSelection && !recoveryMode
				&& selectedMerchantSaleCard.MerchantUpgradeCount < MerchantMaximumUpgrades
				&& HasMerchantUpgradeRelic(selectedMerchantSaleCard.MerchantUpgradeCount + 1);
			bool canUpgrade = upgradeEligible
				&& (runProgress?.Gold ?? 0) >= UpgradeCostFor(selectedMerchantSaleCard);
			merchantUpgradeButton.interactable = canUpgrade;
			Text upgradeLabel = ((Component)merchantUpgradeButton).GetComponentInChildren<Text>();
			if ((Object)(object)upgradeLabel != (Object)null)
			{
				upgradeLabel.text = !hasSelection
					? GameText.Get(GameTextKeys.Merchant.SelectUpgradePawn)
					: upgradeEligible
						? GameText.Format(GameTextKeys.Merchant.UpgradeAction, UpgradeCostFor(selectedMerchantSaleCard))
						: selectedMerchantSaleCard.MerchantUpgradeCount >= MerchantMaximumUpgrades
							? GameText.Get(GameTextKeys.Merchant.UpgradeMaximum)
							: GameText.Format(GameTextKeys.Merchant.UpgradeRelicRequired, selectedMerchantSaleCard.MerchantUpgradeCount + 1);
			}
		}
	}

	private void RefreshMerchantOwnedCards()
	{
		DestroyPrototypeViews(merchantOwnedCardViews);
		List<CampaignCardInstance> deckCards = GetMerchantDeckCards();
		List<CampaignCardInstance> graveyardCards = GetMerchantGraveyardCards();
		bool upgradePage = merchantVisibleBranch == MerchantBranch.Upgrades;
		if ((Object)(object)merchantDeckTabButton != (Object)null)
			((Component)merchantDeckTabButton).gameObject.SetActive(!upgradePage);
		if ((Object)(object)merchantGraveyardTabButton != (Object)null)
			((Component)merchantGraveyardTabButton).gameObject.SetActive(!upgradePage);
		if ((Object)(object)merchantDeckTabText != (Object)null)
		{
			merchantDeckTabText.text = GameText.Format(GameTextKeys.Merchant.DeckCount, deckCards.Count);
		}
		if ((Object)(object)merchantGraveyardTabText != (Object)null)
		{
			merchantGraveyardTabText.text = GameText.Format(GameTextKeys.Merchant.GraveyardCount, graveyardCards.Count);
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
			upgradePage ? deckCards : merchantShowingGraveyard ? graveyardCards : deckCards);
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
		runProgress.AddGold(num);
		selectedMerchantSaleCard = null;
		PlayBuyCardSfx();
		string displayName = CardDisplayNames.MarketName(definition);
		AppendLog(GameText.Format(GameTextKeys.Merchant.SoldLog, displayName, num));
		SetMessage(GameText.Format(GameTextKeys.Merchant.Sold, displayName, num));
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
		// Il recupero si paga sempre: lo sconto del talento "Recupero" e' gia' dentro questo
		// prezzo. Il "Secondo fiato" non passa piu' di qui - non sconta un recupero, evita
		// che la pedina arrivi al cimitero.
		int num = RecoveryCostFor(definition);
		string displayName = CardDisplayNames.MarketName(definition);
		if (!runProgress.TrySpendGold(num))
		{
			SetMessage(GameText.Format(GameTextKeys.Merchant.RecoverInsufficientGold, num, displayName, runProgress.Gold));
			RefreshMerchantPanel();
		}
		else if (!campaignDeck.RecoverFromGraveyard(selectedMerchantSaleCard))
		{
			runProgress.AddGold(num);
			SetMessage("MERCATO: questa carta non puo' essere recuperata adesso.");
			RefreshMerchantPanel();
		}
		else
		{
			if (ShouldTrackQuestProgress)
				runProgress.RecordMerchantPurchase();
			AppendLog(GameText.Format(GameTextKeys.Merchant.RecoveredLog, displayName, num));
			SetMessage(GameText.Format(GameTextKeys.Merchant.Recovered, displayName, num));
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
		int strength = ((Object)(object)definition != (Object)null) ? definition.Strength : 0;
		return AccardND.GameData.TalentRunModifiers.MerchantCost(
			MerchantEconomy.CardCost(strength, runProgress?.RoomsCleared ?? 0),
			ActiveTalents);
	}

	private int MerchantItemCostFor(CampaignConsumableType itemType)
	{
		int baseCost = itemType switch
		{
			CampaignConsumableType.Detector => 12,
			CampaignConsumableType.DoubleExp => 18,
			CampaignConsumableType.SigilloRubino => 24,
			CampaignConsumableType.Empower => 22,
			CampaignConsumableType.SecondChance => 26,
			CampaignConsumableType.ManaGain5 => 12,
			CampaignConsumableType.ManaGain10 => 20,
			CampaignConsumableType.Jolly => 28,
			_ => 18,
		};
		return MerchantEconomy.ScaleByRoom(baseCost, runProgress?.RoomsCleared ?? 0);
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

	// Non piu' statica: lo sconto del ramo Borsa vive nel pacchetto talenti della run, e
	// senza istanza non c'e' modo di leggerlo.
	private int UpgradeCostFor(CampaignCardInstance card)
	{
		if (card == null) return 0;
		return AccardND.GameData.TalentRunModifiers.MerchantCost(
			MerchantEconomy.UpgradeCost(
				card.Definition.Strength + card.PermanentItemBonus,
				card.MerchantUpgradeCount),
			ActiveTalents);
	}

	private bool HasMerchantUpgradeRelic(int upgradeLevel)
	{
		string relicId = upgradeLevel <= 1
			? MerchantUpgradeRelicOneId
			: MerchantUpgradeRelicTwoId;
		return singlePlayerProgressService != null &&
			singlePlayerProgressService.IsUnlocked(
				AccardND.GameData.SinglePlayerUnlockType.Slot,
				relicId);
	}

	private int RecoveryCostFor(CardDefinition definition)
	{
		if (!((Object)(object)definition != (Object)null))
		{
			return 0;
		}
		return AccardND.GameData.TalentRunModifiers.RecoveryCost(
			MerchantEconomy.RecoveryCost(definition.Strength, runProgress?.RoomsCleared ?? 0),
			ActiveTalents);
	}

	private static int EffectiveMerchantCost(int baseCost) => baseCost;

	private void UpgradeSelectedMerchantCard()
	{
		CampaignCardInstance card = selectedMerchantSaleCard;
		if (card == null || card.Zone == CampaignCardZone.Graveyard)
		{
			SetMessage(GameText.Get(GameTextKeys.Merchant.UpgradeSelectDeckPawn));
			return;
		}
		if (card.MerchantUpgradeCount >= MerchantMaximumUpgrades)
		{
			SetMessage(GameText.Get(GameTextKeys.Merchant.UpgradeAlreadyMaximum));
			return;
		}
		if (merchantVisibleBranch != MerchantBranch.Upgrades || IsMerchantBranchLocked(MerchantBranch.Upgrades))
		{
			SetMessage(GameText.Get(GameTextKeys.Merchant.UpgradeBranchLocked));
			return;
		}
		int requiredRelic = card.MerchantUpgradeCount + 1;
		if (!HasMerchantUpgradeRelic(requiredRelic))
		{
			SetMessage(GameText.Format(GameTextKeys.Merchant.UpgradeUnlockRelic, requiredRelic));
			RefreshMerchantPanel();
			return;
		}

		// "Primo affare" si consuma qui e non al calcolo del prezzo: il pannello mostra il
		// costo pieno finche' il giocatore non conferma, altrimenti il talento brucerebbe
		// ogni volta che apre e chiude la scheda di una pedina.
		int fullCost = UpgradeCostFor(card);
		int cost = ConsumeMerchantUpgradeCost(fullCost);
		bool usedFreeUpgrade = cost < fullCost;
		if (!runProgress.TrySpendGold(cost))
		{
			RestoreMerchantUpgradeCost(usedFreeUpgrade);
			SetMessage(GameText.Format(GameTextKeys.Merchant.UpgradeInsufficientGold, cost, runProgress.Gold));
			RefreshMerchantPanel();
			return;
		}
		if (!campaignDeck.TryApplyMerchantUpgrade(card, MerchantMaximumUpgrades))
		{
			runProgress.AddGold(cost);
			RestoreMerchantUpgradeCost(usedFreeUpgrade);
			SetMessage(GameText.Get(GameTextKeys.Merchant.UpgradeUnavailable));
			RefreshMerchantPanel();
			return;
		}

		if (ShouldTrackQuestProgress)
			runProgress.RecordMerchantPurchase();
		string displayName = CardDisplayNames.MarketName(card.Definition);
		if (cost <= 0)
		{
			AppendLog(GameText.Format(GameTextKeys.Merchant.UpgradeFreeLog, displayName));
			SetMessage(GameText.Format(GameTextKeys.Merchant.UpgradeFreeSuccess, displayName));
		}
		else
		{
			AppendLog(GameText.Format(GameTextKeys.Merchant.UpgradePaidLog, displayName, cost));
			SetMessage(GameText.Format(GameTextKeys.Merchant.UpgradePaidSuccess, displayName, cost));
		}
		PlayForgeHitSfx();
		RefreshMerchantPanel();
	}

	private void RefreshBagGoldCounter()
	{
		if ((Object)(object)implementationArchiveGoldText != (Object)null)
			implementationArchiveGoldText.text = GameText.Format(GameTextKeys.Merchant.GoldCounter, Math.Max(0, runProgress?.Gold ?? 0));
	}
}
}
