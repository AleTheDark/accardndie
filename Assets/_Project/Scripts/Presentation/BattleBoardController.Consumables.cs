using System;
using System.Collections.Generic;
using System.Linq;
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
	// Oggetti della bisaccia portati in questa run e non ancora usati.
	private readonly List<string> runBagItemIds = new List<string>();

	// Oggetti della bisaccia consumati: a fine run il server li scala dalla scorta.
	private readonly List<string> consumedBagItemIds = new List<string>();

	private static string CampaignConsumableResourceName(CampaignConsumableType itemType)
	{
		return itemType switch
		{
			CampaignConsumableType.Detector => "detector_item",
			CampaignConsumableType.SecondChance => "second_chance_item",
			CampaignConsumableType.Defrost => "defrost_item",
			CampaignConsumableType.Empower => "empower_item",
			CampaignConsumableType.SigilloRubino => "ruby_seal_item",
			CampaignConsumableType.DoubleExp => "double_exp_item",
			_ => "info_button",
		};
	}

	private static string CampaignConsumableName(CampaignConsumableType itemType)
	{
		return GameText.Get(GameTextKeys.Consumables.Name(CampaignConsumableLocalizationId(itemType)));
	}

	private static string CampaignConsumableDescription(CampaignConsumableType itemType)
	{
		return GameText.Get(GameTextKeys.Consumables.Description(CampaignConsumableLocalizationId(itemType)));
	}

	private static string CampaignConsumableLocalizationId(CampaignConsumableType itemType)
	{
		return itemType switch
		{
			CampaignConsumableType.Detector => "detector",
			CampaignConsumableType.SecondChance => "second_chance",
			CampaignConsumableType.Defrost => "defrost",
			CampaignConsumableType.Empower => "empower",
			CampaignConsumableType.SigilloRubino => "ruby_seal",
			CampaignConsumableType.DoubleExp => "double_experience",
			_ => "generic",
		};
	}

	private void HandleCampaignConsumableClicked(CampaignConsumableType itemType)
	{
		ShowCampaignConsumableInspection(itemType);
	}

	private void ConfirmInspectedCampaignConsumable()
	{
		if (!inspectedCampaignConsumableActive)
		{
			return;
		}
		CampaignConsumableType itemType = inspectedCampaignConsumableType;
		CloseCardInspection(playSfx: false);
		if (campaignDeck == null || pvpPresentationActive)
		{
			return;
		}
		if (TryUseCampaignConsumable(itemType) && (Object)(object)implementationArchivePanel != (Object)null && implementationArchivePanel.activeSelf)
		{
			RefreshImplementationArchive();
		}
	}

	private bool TryUseCampaignConsumable(CampaignConsumableType itemType)
	{
		if (IsConsumableBlockedInBattle(itemType) && IsCampaignBattleActive())
		{
			string itemName = CampaignConsumableName(itemType);
			SetMessage(GameText.Format(GameTextKeys.Consumables.CannotUseInBattle, itemName));
			AppendLog(GameText.Format(GameTextKeys.Consumables.BlockedByBattleLog, itemName));
			return false;
		}
		if (itemType == CampaignConsumableType.Empower && IsEmpowerBlockedInCurrentRoom())
		{
			SetMessage(GameText.Get(GameTextKeys.Consumables.EmpowerBossBlocked));
			AppendLog(GameText.Get(GameTextKeys.Consumables.EmpowerBossBlockedLog));
			return false;
		}
		if (itemType == CampaignConsumableType.SigilloRubino)
		{
			return BeginRubySealTargetSelection();
		}
		if (campaignConsumables == null || !campaignConsumables.TryConsume(itemType))
		{
			return false;
		}
		RecordConsumedBagItem(itemType);
		switch (itemType)
		{
		case CampaignConsumableType.Detector:
			PlayDetectorItemUseSfx();
			if ((Object)(object)roomChoicePanel != (Object)null && roomChoicePanel.activeSelf)
			{
				RevealCurrentCampaignDoorsWithDetector();
				SetMessage(GameText.Get(GameTextKeys.Consumables.DetectorRevealed));
			}
			else
			{
				nextDoorChoiceRevealed = true;
				SetMessage(GameText.Get(GameTextKeys.Consumables.DetectorNextChoice));
			}
			AppendLog(GameText.Get(GameTextKeys.Consumables.DetectorActivatedLog));
			return true;
		case CampaignConsumableType.SecondChance:
			int revived = RecoverAllGraveyardCards();
			SetMessage(GameText.Format(GameTextKeys.Consumables.SecondChanceUsed, revived));
			AppendLog(GameText.Format(GameTextKeys.Consumables.SecondChanceUsedLog, revived));
			return true;
		case CampaignConsumableType.Defrost:
			int defrosted = campaignDeck?.ReleaseCooldown() ?? 0;
			SetMessage(GameText.Format(GameTextKeys.Consumables.DefrostUsed, defrosted));
			AppendLog(GameText.Format(GameTextKeys.Consumables.DefrostUsedLog, defrosted));
			return true;
		case CampaignConsumableType.Empower:
			PlayEmpowerItemUseSfx();
			nextRoomEmpowered = true;
			RefreshPlayerHud();
			SetMessage(GameText.Get(GameTextKeys.Consumables.EmpowerUsed));
			AppendLog(GameText.Get(GameTextKeys.Consumables.EmpowerUsedLog));
			return true;
		case CampaignConsumableType.DoubleExp:
			nextRoomDoubleExperience = true;
			SetMessage(GameText.Get(GameTextKeys.Consumables.DoubleExperienceReady));
			AppendLog(GameText.Get(GameTextKeys.Consumables.DoubleExperienceReadyLog));
			return true;
		default:
			return false;
		}
	}

	private bool IsEmpowerBlockedInCurrentRoom()
	{
		return campaignDeck != null && currentRoomType == RoomType.Boss;
	}

	private bool BeginRubySealTargetSelection()
	{
		if (!HasRubySealTarget())
		{
			SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealNoTarget));
			AppendLog(GameText.Get(GameTextKeys.Consumables.RubySealNoTargetLog));
			return false;
		}
		rubySealTargetSelectionActive = true;
		ShowRubySealTargetPanel();
		SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealSelectTarget));
		AppendLog(GameText.Get(GameTextKeys.Consumables.RubySealSelectTargetLog));
		return true;
	}

	private void HandleImplementationCardClicked(CampaignCardInstance card)
	{
		if (!rubySealTargetSelectionActive)
		{
			ShowCardInspection(card?.Definition);
			return;
		}
		TryApplyRubySealTo(card);
	}

	private bool TryApplyRubySealTo(CampaignCardInstance target)
	{
		if (!IsRubySealTarget(target))
		{
			SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealInvalidTarget));
			return false;
		}
		if (campaignConsumables == null || !campaignConsumables.TryConsume(CampaignConsumableType.SigilloRubino))
		{
			rubySealTargetSelectionActive = false;
			return false;
		}
		if (!campaignDeck.TryApplyRubySeal(target, 2))
		{
			campaignConsumables.Add(CampaignConsumableType.SigilloRubino);
			SetMessage(GameText.Get(GameTextKeys.Consumables.RubySealAlreadyApplied));
			return false;
		}
		RecordConsumedBagItem(CampaignConsumableType.SigilloRubino);
		rubySealTargetSelectionActive = false;
		PlayEmpowerItemUseSfx();
		string cardName = CardDisplayNames.MarketName(target.Definition);
		SetMessage(GameText.Format(GameTextKeys.Consumables.RubySealApplied, cardName));
		AppendLog(GameText.Format(GameTextKeys.Consumables.RubySealAppliedLog, cardName));
		if ((Object)(object)implementationArchivePanel != (Object)null && implementationArchivePanel.activeSelf)
		{
			RefreshImplementationArchive();
		}
		return true;
	}

	private bool HasRubySealTarget()
	{
		return playerCards.Any(IsRubySealBattleTarget);
	}

	private static bool IsRubySealBattleTarget(BattleCardState card)
	{
		return card != null && !card.Eliminated && IsRubySealTarget(card.CampaignCard);
	}

	private void ShowRubySealTargetPanel()
	{
		CloseRubySealTargetPanel(cancelSelection: false);
		Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
		Image backdrop = CreateImage("Ruby Seal Target Backdrop", (Transform)(object)safeAreaRoot, new Color(0.015f, 0.005f, 0.02f, 0.88f));
		backdrop.raycastTarget = true;
		Stretch(backdrop.rectTransform, 0f);
		rubySealTargetPanel = ((Component)backdrop).gameObject;

		Image window = CreateImage("Ruby Seal Target Window", ((Component)backdrop).transform, new Color(0.18f, 0.035f, 0.055f, 0.98f));
		RectTransform windowRect = window.rectTransform;
		windowRect.anchorMin = new Vector2(0.15f, 0.2f);
		windowRect.anchorMax = new Vector2(0.85f, 0.8f);
		windowRect.offsetMin = Vector2.zero;
		windowRect.offsetMax = Vector2.zero;

		Text title = CreateText("Ruby Seal Target Title", ((Component)window).transform, font, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
		title.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Consumables.RubySealTargetTitle,
			"SIGILLO RUBINO - SCEGLI UNA PEDINA SCHIERATA");
		RectTransform titleRect = title.rectTransform;
		titleRect.anchorMin = new Vector2(0.05f, 0.82f);
		titleRect.anchorMax = new Vector2(0.95f, 0.97f);
		titleRect.offsetMin = Vector2.zero;
		titleRect.offsetMax = Vector2.zero;

		rubySealTargetCardsRoot = CreateCardRow("Ruby Seal Deployed Pawns", ((Component)window).transform, new Vector2(0.5f, 0.52f));
		rubySealTargetCardsRoot.sizeDelta = new Vector2(1050f, 300f);
		foreach (BattleCardState battleCard in playerCards.Where(IsRubySealBattleTarget))
		{
			PrototypeCardView view = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)rubySealTargetCardsRoot, battleCard.Definition, configuration);
			view.SetInteractable(true);
			view.SetStrengthValue(DisplayStrength(battleCard));
			((UnityEvent)view.Button.onClick).AddListener((UnityAction)delegate
			{
				ApplyRubySealToBattleCard(battleCard);
			});
			rubySealTargetCardViews.Add(view);
		}

		Button cancel = CreateButton(
			"Cancel Ruby Seal Selection",
			((Component)window).transform,
			font,
			GameText.GetOrFallbackSilent(GameTextKeys.Common.Cancel, "ANNULLA"));
		RectTransform cancelRect = ((Component)cancel).GetComponent<RectTransform>();
		cancelRect.anchorMin = new Vector2(0.4f, 0.04f);
		cancelRect.anchorMax = new Vector2(0.6f, 0.16f);
		cancelRect.offsetMin = Vector2.zero;
		cancelRect.offsetMax = Vector2.zero;
		((UnityEvent)cancel.onClick).AddListener((UnityAction)delegate { CloseRubySealTargetPanel(cancelSelection: true); });
		rubySealTargetPanel.transform.SetAsLastSibling();
	}

	private void ApplyRubySealToBattleCard(BattleCardState battleCard)
	{
		if (!IsRubySealBattleTarget(battleCard) || !TryApplyRubySealTo(battleCard.CampaignCard))
			return;
		battleCard.PermanentCombatBonus += 2;
		if ((Object)(object)battleCard.View != (Object)null)
			battleCard.View.SetStrengthValue(DisplayStrength(battleCard));
		CloseRubySealTargetPanel(cancelSelection: false);
	}

	private void CloseRubySealTargetPanel(bool cancelSelection)
	{
		if (cancelSelection)
			rubySealTargetSelectionActive = false;
		foreach (PrototypeCardView view in rubySealTargetCardViews)
			if ((Object)(object)view != (Object)null) Object.Destroy(((Component)view).gameObject);
		rubySealTargetCardViews.Clear();
		if ((Object)(object)rubySealTargetPanel != (Object)null)
			Object.Destroy(rubySealTargetPanel);
		rubySealTargetPanel = null;
		rubySealTargetCardsRoot = null;
	}

	private static bool IsRubySealTarget(CampaignCardInstance card)
	{
		return card != null
			&& !card.HasRubySeal
			&& card.Definition != null
			&& card.Definition.CanEnterCombat
			&& card.Zone != AccardND.GameData.CampaignCardZone.Graveyard;
	}

	private static bool IsConsumableBlockedInBattle(CampaignConsumableType itemType)
	{
		return itemType == CampaignConsumableType.SecondChance
			|| itemType == CampaignConsumableType.Defrost;
	}

	private bool IsCampaignBattleActive()
	{
		if (IsRoomChoiceActive())
		{
			return false;
		}
		return campaignDeck != null
			&& (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss)
			&& (draftActive || deploymentDraftActive || roundNumber > 0 || playerCards.Count > 0 || cpuCards.Count > 0);
	}

	private bool IsRoomChoiceActive()
	{
		return (Object)(object)roomChoicePanel != (Object)null && roomChoicePanel.activeSelf;
	}

	private int RecoverAllGraveyardCards()
	{
		if (campaignDeck == null)
		{
			return 0;
		}
		int recovered = 0;
		foreach (var card in campaignDeck.Cards)
		{
			if (card.Zone == AccardND.GameData.CampaignCardZone.Graveyard && campaignDeck.RecoverFromGraveyard(card))
			{
				recovered++;
			}
		}
		return recovered;
	}

	private (string description, int bonusExperience) GrantRandomConsumable(string source)
	{
		CampaignConsumableType[] pool =
		{
			CampaignConsumableType.Detector,
			CampaignConsumableType.SecondChance,
			CampaignConsumableType.Defrost,
			CampaignConsumableType.Empower,
			CampaignConsumableType.SigilloRubino,
			CampaignConsumableType.DoubleExp
		};
		CampaignConsumableType itemType = pool[random.NextInclusive(0, pool.Length - 1)];
		campaignConsumables.Add(itemType);
		string itemName = CampaignConsumableName(itemType);
		AppendLog(GameText.Format(GameTextKeys.Consumables.GrantedLog, source, itemName));
		return (description: GameText.Format(GameTextKeys.Consumables.GrantedDescription, source, itemName), bonusExperience: 0);
	}

	/// <summary>
	/// Riempie la borsa di run con la bisaccia scelta al Santuario: un pezzo per oggetto
	/// selezionato. La scorta permanente non viene toccata qui, cala solo a fine run e solo
	/// per quello che e' stato davvero usato.
	/// </summary>
	private void LoadCampaignConsumablesFromBag()
	{
		campaignConsumables.Clear();
		runBagItemIds.Clear();
		consumedBagItemIds.Clear();
		List<string> bag = singlePlayerProgressService.Progress?.bagItems;
		if (bag == null || bag.Count == 0)
		{
			AppendLog(GameText.Get(GameTextKeys.Consumables.BagEmptyLog));
			WarmCampaignRunAds();
			return;
		}

		foreach (string itemId in bag)
		{
			if (!TryParseSanctuaryItemId(itemId, out CampaignConsumableType itemType))
			{
				continue;
			}
			campaignConsumables.Add(itemType);
			runBagItemIds.Add(itemId);
		}
		AppendLog(GameText.Format(GameTextKeys.Consumables.BagLoadedLog, runBagItemIds.Count));
		// La borsa e' composta: da qui in poi si sa se l'interstitial degli oggetti puo'
		// servire, ed e' il primo momento in cui questa run puo' chiedere annunci.
		WarmCampaignRunAds();
	}

	/// <summary>Id del catalogo Santuario -> tipo consumabile di run.</summary>
	private static bool TryParseSanctuaryItemId(string itemId, out CampaignConsumableType itemType)
	{
		switch (itemId)
		{
		case "detector":
			itemType = CampaignConsumableType.Detector;
			return true;
		case "second-chance":
			itemType = CampaignConsumableType.SecondChance;
			return true;
		case "defrost":
			itemType = CampaignConsumableType.Defrost;
			return true;
		case "empower":
			itemType = CampaignConsumableType.Empower;
			return true;
		case "sigillo-rubino":
		case "ruby-seal":
			itemType = CampaignConsumableType.SigilloRubino;
			return true;
		case "double-exp":
			itemType = CampaignConsumableType.DoubleExp;
			return true;
		default:
			itemType = CampaignConsumableType.Detector;
			return false;
		}
	}

	private static string SanctuaryItemIdOf(CampaignConsumableType itemType) => itemType switch
	{
		CampaignConsumableType.Detector => "detector",
		CampaignConsumableType.SecondChance => "second-chance",
		CampaignConsumableType.Defrost => "defrost",
		CampaignConsumableType.Empower => "empower",
		CampaignConsumableType.SigilloRubino => "sigillo-rubino",
		CampaignConsumableType.DoubleExp => "double-exp",
		_ => null
	};

	/// <summary>
	/// Segna come consumato un oggetto arrivato dalla bisaccia. Solo questi vengono scalati
	/// dalla scorta a fine run: quelli trovati in run (loot, mercante) non ne fanno parte.
	///
	/// E' anche il punto in cui parte l'interstitial dell'uso oggetti, ed e' l'unico che va
	/// bene: qui l'oggetto e' gia' stato tolto dalla borsa, quindi la pubblicita' non puo'
	/// partire per un uso poi rifiutato (sigillo senza bersaglio, potenziamento in stanza
	/// boss). Le regole di frequenza fanno il resto: tre oggetti di fila restano una
	/// pubblicita' sola.
	/// </summary>
	private void RecordConsumedBagItem(CampaignConsumableType itemType)
	{
		string itemId = SanctuaryItemIdOf(itemType);
		if (string.IsNullOrEmpty(itemId) || !runBagItemIds.Remove(itemId))
		{
			return;
		}
		consumedBagItemIds.Add(itemId);
		AccardND.Ads.AdService.ShowInterstitial(AccardND.Ads.AdPlacement.BagItemUsed);
	}

	private int ConsumeNextRoomExperienceMultiplier()
	{
		if (!nextRoomDoubleExperience)
		{
			return 1;
		}
		nextRoomDoubleExperience = false;
		AppendLog(GameText.Get(GameTextKeys.Consumables.DoubleExperienceConsumedLog));
		return 2;
	}

	private void ShowCampaignConsumableInspection(CampaignConsumableType itemType)
	{
		if ((Object)(object)cardInspectionPanel == (Object)null || (Object)(object)cardInspectionSlot == (Object)null)
		{
			return;
		}
		if ((Object)(object)inspectedCardView != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)inspectedCardView).gameObject);
			inspectedCardView = null;
		}
		ClearInspectionStatusRows();
		inspectedCampaignConsumableActive = true;
		inspectedCampaignConsumableType = itemType;
		Image icon = CreateImage("Consumable Inspection Icon", (Transform)(object)cardInspectionSlot, Color.white);
		icon.sprite = LoadSpriteResource("UI/" + CampaignConsumableResourceName(itemType));
		icon.preserveAspect = true;
		icon.raycastTarget = false;
		Stretch(icon.rectTransform);
		cardInspectionStatusRows.Add(((Component)icon).gameObject);
		cardInspectionSummaryText.text = GameText.Format(
			GameTextKeys.Consumables.InspectionSummary,
			CampaignConsumableName(itemType),
			CampaignConsumableDescription(itemType),
			campaignConsumables?.GetQuantity(itemType) ?? 0);
		if ((Object)(object)cardInspectionDraftConfirmButton != (Object)null)
		{
			bool canUse = campaignDeck != null
				&& !pvpPresentationActive
				&& (campaignConsumables?.GetQuantity(itemType) ?? 0) > 0
				&& !(IsConsumableBlockedInBattle(itemType) && IsCampaignBattleActive());
			((Component)cardInspectionDraftConfirmButton).gameObject.SetActive(canUse);
			cardInspectionDraftConfirmButton.interactable = canUse;
			if ((Object)(object)cardInspectionDraftConfirmButtonText != (Object)null)
			{
				cardInspectionDraftConfirmButtonText.text = GameText.Get(GameTextKeys.Common.Use);
			}
			if ((Object)(object)cardInspectionDraftConfirmButtonRect != (Object)null)
			{
				SetCardInspectionConfirmButtonRect(Screen.width > Screen.height);
			}
			if (canUse)
			{
				((Component)cardInspectionDraftConfirmButton).transform.SetAsLastSibling();
			}
		}
		cardInspectionPanel.SetActive(true);
		PlayCardInspectionOpenSfx();
		if ((Object)(object)cardInspectionCloseButton != (Object)null)
		{
			((Component)cardInspectionCloseButton).transform.SetAsLastSibling();
		}
	}
}
}
