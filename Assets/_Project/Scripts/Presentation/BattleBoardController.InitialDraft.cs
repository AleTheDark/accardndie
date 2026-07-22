using System;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private const int InitialDraftOfferCount = 9;

	private void StartCampaignDraftMode()
	{
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)cardDatabase == (Object)null)
		{
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		}
		if ((Object)(object)cardDatabase == (Object)null)
		{
			SetMessage("Database carte non trovato. Usa Accard N' Die > Rebuild Card Database.");
			return;
		}
		formationDraftService = new FormationDraftService(random);
		BeginInitialDraft();
	}

	private void BeginInitialDraft()
	{
		initialDraftDeck.Clear();
		initialDraftOffers.Clear();
		initialDraftSelectedIndices.Clear();
		initialDraftCaptainClass = null;
		initialDraftChoosingCaptain = true;
		if ((Object)(object)initialDraftPanel != (Object)null)
		{
			initialDraftPanel.SetActive(true);
		}
		ShowInitialDraftHint();
		AppendLog("DRAFT CAMPAGNA - scegli un capitano, poi conferma 3 carte per pacchetto.");
		GenerateInitialDraftOffers();
		RefreshInitialDraftView();
	}

	private void GenerateInitialDraftOffers()
	{
		initialDraftOffers.Clear();
		initialDraftSelectedIndices.Clear();
		List<CardDefinition> pool = BuildInitialDraftPool();
		while (initialDraftOffers.Count < InitialDraftOfferCount && pool.Count > 0)
		{
			CardDefinition picked = DrawInitialDraftOffer(pool);
			initialDraftOffers.Add(picked);
			pool.Remove(picked);
		}
	}

	private List<CardDefinition> BuildInitialDraftPool()
	{
		var pool = new List<CardDefinition>();
		foreach (CardDefinition card in cardDatabase.Cards)
		{
			if ((Object)(object)card == (Object)null || card.Category != CardCategory.Monster || !card.CanEnterCombat)
			{
				continue;
			}
			if (CardPurchaseUniqueness.ContainsEquivalent(card, initialDraftDeck)
				|| CardPurchaseUniqueness.ContainsEquivalent(card, initialDraftOffers))
			{
				continue;
			}
			pool.Add(card);
		}
		return pool;
	}

	private CardDefinition DrawInitialDraftOffer(List<CardDefinition> pool)
	{
		int totalWeight = 0;
		foreach (CardDefinition card in pool)
		{
			totalWeight += InitialDraftOfferWeight(card);
		}
		int roll = random.NextInclusive(1, Mathf.Max(1, totalWeight));
		foreach (CardDefinition card in pool)
		{
			roll -= InitialDraftOfferWeight(card);
			if (roll <= 0)
			{
				return card;
			}
		}
		return pool[pool.Count - 1];
	}

	private int InitialDraftOfferWeight(CardDefinition card)
	{
		int weight = Mathf.Max(1, 12 - Mathf.Abs(card.Strength - (initialDraftChoosingCaptain ?7 : 5)) * 2);
		if (initialDraftChoosingCaptain && card.Strength >= 6)
		{
			weight += 8;
		}
		if (!initialDraftChoosingCaptain && initialDraftCaptainClass.HasValue && card.HeroClass == initialDraftCaptainClass.Value)
		{
			weight += 10;
		}
		return Mathf.Max(1, weight);
	}

	private void ToggleInitialDraftOffer(int index)
	{
		if (index < 0 || index >= initialDraftOffers.Count)
		{
			return;
		}
		int selectionLimit = InitialDraftSelectionLimit();
		if (initialDraftSelectedIndices.Contains(index))
		{
			initialDraftSelectedIndices.Remove(index);
		}
		else
		{
			if (initialDraftSelectedIndices.Count >= selectionLimit)
			{
				ShowInitialDraftNotice(initialDraftChoosingCaptain ? "Scegli un solo capitano." : $"Puoi confermare al massimo {selectionLimit} carte.");
				return;
			}
			initialDraftSelectedIndices.Add(index);
		}
		RefreshInitialDraftSelectionVisuals();
		RefreshInitialDraftConfirmButton();
	}

	private void ShowInitialDraftOfferInspection(int index)
	{
		if (index < 0 || index >= initialDraftOffers.Count)
		{
			return;
		}
		inspectedInitialDraftOfferIndex = index;
		ShowCardInspection(initialDraftOffers[index]);
		RefreshCardInspectionDraftConfirmButton();
	}

	private void RefreshCardInspectionDraftConfirmButton()
	{
		if ((Object)(object)cardInspectionDraftConfirmButton == (Object)null)
		{
			return;
		}
		bool valid = inspectedInitialDraftOfferIndex >= 0 && inspectedInitialDraftOfferIndex < initialDraftOffers.Count;
		((Component)cardInspectionDraftConfirmButton).gameObject.SetActive(valid);
		if (!valid)
		{
			return;
		}
		bool selected = initialDraftSelectedIndices.Contains(inspectedInitialDraftOfferIndex);
		cardInspectionDraftConfirmButton.interactable = selected || initialDraftSelectedIndices.Count < InitialDraftSelectionLimit();
		if ((Object)(object)cardInspectionDraftConfirmButtonText != (Object)null)
		{
			cardInspectionDraftConfirmButtonText.text = selected ? "RIMUOVI" : "SELEZIONA";
		}
		((Component)cardInspectionDraftConfirmButton).transform.SetAsLastSibling();
	}

	private void ConfirmInspectedInitialDraftOffer()
	{
		if (inspectedCampaignConsumableActive)
		{
			ConfirmInspectedCampaignConsumable();
			return;
		}
		if (inspectedInitialDraftOfferIndex < 0 || inspectedInitialDraftOfferIndex >= initialDraftOffers.Count)
		{
			CloseCardInspection();
			return;
		}
		ToggleInitialDraftOffer(inspectedInitialDraftOfferIndex);
		CloseCardInspection();
	}

	private int InitialDraftSelectionLimit()
	{
		if (initialDraftChoosingCaptain)
		{
			return 1;
		}
		return Mathf.Min(3, Mathf.Max(0, configuration.DeckBuilding.DeckSize - initialDraftDeck.Count));
	}

	private void ConfirmInitialDraftSelection()
	{
		int selectionLimit = InitialDraftSelectionLimit();
		if (initialDraftSelectedIndices.Count != selectionLimit)
		{
			ShowInitialDraftNotice(initialDraftChoosingCaptain ? "Prima scegli il tuo capitano." : $"Scegli {selectionLimit} carte prima di confermare.");
			return;
		}
		List<CardDefinition> selected = initialDraftSelectedIndices
			.OrderBy(index => index)
			.Select(index => initialDraftOffers[index])
			.Where(card => (Object)(object)card != (Object)null)
			.ToList();
		foreach (CardDefinition card in selected)
		{
			initialDraftDeck.Add(card);
		}
		if (initialDraftChoosingCaptain)
		{
			initialDraftCaptainClass = selected[0].HeroClass;
			initialDraftChoosingCaptain = false;
			AppendLog($"CAPITANO DRAFT - {selected[0].DisplayName} ({HeroClassDisplayName(selected[0].HeroClass)}).");
		}
		else
		{
			AppendLog($"PACCHETTO DRAFT - aggiunte {selected.Count} carte.");
		}
		PlayBuyCardSfx();
		if (initialDraftDeck.Count >= configuration.DeckBuilding.DeckSize)
		{
			StartDraftBuiltCampaign();
			return;
		}
		GenerateInitialDraftOffers();
		RefreshInitialDraftView();
	}

	private void StartDraftBuiltCampaign()
	{
		campaignDeck = new CampaignDeckState(initialDraftDeck);
		GrantStartingCampaignConsumablesForTesting();
		ResetScenarioRuleState();
		initialDraftPanel.SetActive(false);
		DestroyPrototypeViews(initialDraftOfferViews);
		DestroyPrototypeViews(initialDraftDeckViews);
		((Component)campaignZoneRect).gameObject.SetActive(false);
		AppendLog($"CAMPAGNA DRAFT AVVIATA - {campaignDeck.Cards.Count} carte nel mazzo.");
		PlayTransitionSfx();
		BeginRoomChoice();
	}

	private void RefreshInitialDraftView()
	{
		RefreshInitialDraftLayout();
		if ((Object)(object)initialDraftHeadingText != (Object)null)
		{
			initialDraftHeadingText.text = initialDraftChoosingCaptain ? "SCEGLI IL CAPITANO" : "DRAFT DEL MAZZO";
		}
		if ((Object)(object)initialDraftStatusText != (Object)null)
		{
			string captain = initialDraftCaptainClass.HasValue ?$"CAPITANO {HeroClassDisplayName(initialDraftCaptainClass.Value).ToUpperInvariant()}  -  " : string.Empty;
			initialDraftStatusText.text = captain + $"MAZZO {initialDraftDeck.Count}/{configuration.DeckBuilding.DeckSize}";
		}
		if ((Object)(object)initialDraftPromptText != (Object)null)
		{
			initialDraftPromptText.color = new Color(0.88f, 0.92f, 0.96f);
			initialDraftPromptText.text = initialDraftChoosingCaptain
				? "Scegli una carta capitano: guidera' le offerte successive."
				: $"Tocca una carta per leggerla, poi seleziona {InitialDraftSelectionLimit()} carte.";
		}
		RefreshInitialDraftOffers();
		RefreshInitialDraftDeckPreview();
		RefreshInitialDraftConfirmButton();
	}

	private void RefreshInitialDraftOffers()
	{
		DestroyPrototypeViews(initialDraftOfferViews);
		if ((Object)(object)initialDraftOffersRoot == (Object)null)
		{
			return;
		}
		ResizeInitialDraftOfferGrid();
		for (int i = 0; i < initialDraftOffers.Count; i++)
		{
			int index = i;
			CardDefinition card = initialDraftOffers[i];
			PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)initialDraftOffersRoot, card, configuration);
			view.RaiseStrengthText();
			((UnityEvent)view.Button.onClick).AddListener((UnityAction)delegate
			{
				ShowInitialDraftOfferInspection(index);
			});
			initialDraftOfferViews.Add(view);
		}
		RefreshInitialDraftSelectionVisuals();
	}

	private void RefreshInitialDraftDeckPreview()
	{
		DestroyPrototypeViews(initialDraftDeckViews);
		if ((Object)(object)initialDraftDeckRoot == (Object)null)
		{
			return;
		}
		ResizeInitialDraftDeckGrid();
		if ((Object)(object)initialDraftDeckText != (Object)null)
		{
			((Component)initialDraftDeckText).gameObject.SetActive(initialDraftDeck.Count == 0);
		}
		foreach (CardDefinition card in initialDraftDeck)
		{
			CardDefinition capturedCard = card;
			PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)initialDraftDeckRoot, card, configuration);
			view.RaiseStrengthText();
			view.SetInteractable((Object)(object)capturedCard != (Object)null);
			((UnityEvent)view.Button.onClick).AddListener((UnityAction)delegate
			{
				ShowCardInspection(capturedCard);
			});
			initialDraftDeckViews.Add(view);
		}
	}

	private void RefreshInitialDraftSelectionVisuals()
	{
		for (int i = 0; i < initialDraftOfferViews.Count; i++)
		{
			initialDraftOfferViews[i].SetDraftSelected(initialDraftSelectedIndices.Contains(i));
		}
	}

	private void RefreshInitialDraftConfirmButton()
	{
		if ((Object)(object)initialDraftConfirmButton == (Object)null)
		{
			return;
		}
		int limit = InitialDraftSelectionLimit();
		initialDraftConfirmButton.interactable = initialDraftSelectedIndices.Count == limit;
		if ((Object)(object)initialDraftConfirmButtonText != (Object)null)
		{
			initialDraftConfirmButtonText.text = initialDraftChoosingCaptain ? "SCEGLI" : $"CONFERMA {limit}";
		}
	}

	private void ShowInitialDraftNotice(string message)
	{
		if ((Object)(object)initialDraftPromptText != (Object)null)
		{
			initialDraftPromptText.text = message;
			initialDraftPromptText.color = new Color(0.95f, 0.79f, 0.34f);
		}
	}

	private void RefreshInitialDraftLayout()
	{
		if ((Object)(object)initialDraftPanel == (Object)null || (Object)(object)safeAreaRoot == (Object)null)
		{
			return;
		}
		Rect safeRect = safeAreaRoot.rect;
		float width = Mathf.Max(1f, safeRect.width);
		float height = Mathf.Max(1f, safeRect.height);
		bool compact = IsCompactLayout(width / height, configuration.ResponsiveLayout);
		bool wide = !compact && width / height >= 1.65f;
		RefreshResponsiveDeckBuilderFrame(initialDraftFrameImage, initialDraftFrameAspectFitter, compact);
		SetRect(initialDraftHeadingText.rectTransform,
			compact ?new Vector2(0.08f, 0.862f) : new Vector2(0.12f, 0.852f),
			compact ?new Vector2(0.92f, 0.94f) : new Vector2(0.88f, 0.938f));
		initialDraftHeadingText.fontSize = compact ?42 : 42;
		initialDraftHeadingText.resizeTextMaxSize = initialDraftHeadingText.fontSize;
		initialDraftHeadingText.resizeTextMinSize = compact ?30 : 28;
		SetRect(initialDraftStatusText.rectTransform,
			compact ?new Vector2(0.08f, 0.805f) : new Vector2(0.18f, 0.79f),
			compact ?new Vector2(0.92f, 0.858f) : new Vector2(0.82f, 0.842f));
		initialDraftStatusText.fontSize = compact ?26 : 24;
		SetRect(initialDraftPromptText.rectTransform,
			compact ?new Vector2(0.1f, 0.735f) : new Vector2(0.2f, 0.715f),
			compact ?new Vector2(0.9f, 0.805f) : new Vector2(0.8f, 0.775f));
		initialDraftPromptText.fontSize = compact ?26 : 25;
		initialDraftPromptText.resizeTextMaxSize = initialDraftPromptText.fontSize;
		initialDraftPromptText.resizeTextMinSize = compact ?20 : 19;
		SetRect(initialDraftOffersRoot,
			compact ?new Vector2(0.12f, 0.315f) : new Vector2(wide ?0.19f : 0.16f, 0.28f),
			compact ?new Vector2(0.88f, 0.725f) : new Vector2(wide ?0.81f : 0.84f, 0.7f));
		SetRect(initialDraftDeckRoot,
			compact ?new Vector2(0.08f, 0.185f) : new Vector2(0.15f, 0.12f),
			compact ?new Vector2(0.92f, 0.28f) : new Vector2(0.85f, 0.235f));
		SetRect(initialDraftDeckText.rectTransform,
			compact ?new Vector2(0.12f, 0.195f) : new Vector2(0.2f, 0.145f),
			compact ?new Vector2(0.88f, 0.265f) : new Vector2(0.8f, 0.215f));
		SetRect(initialDraftConfirmButtonRect,
			compact ?new Vector2(0.32f, 0.065f) : new Vector2(0.38f, 0.045f),
			compact ?new Vector2(0.68f, 0.145f) : new Vector2(0.62f, 0.105f));
		ResizeInitialDraftOfferGrid();
		ResizeInitialDraftDeckGrid();
	}

	private void ResizeInitialDraftOfferGrid()
	{
		if ((Object)(object)initialDraftOffersRoot == (Object)null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		GridLayoutGroup grid = ((Component)initialDraftOffersRoot).GetComponent<GridLayoutGroup>();
		Rect rect = initialDraftOffersRoot.rect;
		float cardSize = Mathf.Min(
			Mathf.Max(1f, rect.width - grid.spacing.x * 2f) / 3f,
			Mathf.Max(1f, rect.height - grid.spacing.y * 2f) / 3f);
		grid.cellSize = new Vector2(cardSize, cardSize);
	}

	private void ResizeInitialDraftDeckGrid()
	{
		if ((Object)(object)initialDraftDeckRoot == (Object)null)
		{
			return;
		}
		Canvas.ForceUpdateCanvases();
		GridLayoutGroup grid = ((Component)initialDraftDeckRoot).GetComponent<GridLayoutGroup>();
		Rect rect = initialDraftDeckRoot.rect;
		float cardSize = Mathf.Min(
			Mathf.Max(1f, rect.width - grid.spacing.x * 8f) / 9f,
			Mathf.Max(1f, rect.height));
		grid.cellSize = new Vector2(cardSize, cardSize);
	}
}
}
