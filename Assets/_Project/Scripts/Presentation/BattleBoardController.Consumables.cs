using System;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameData;
using UnityEngine;
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
		return itemType switch
		{
			CampaignConsumableType.Detector => "Detector",
			CampaignConsumableType.SecondChance => "Seconda Chance",
			CampaignConsumableType.Defrost => "Defrost",
			CampaignConsumableType.Empower => "Empower",
			CampaignConsumableType.SigilloRubino => "Sigillo Rubino",
			CampaignConsumableType.DoubleExp => "Doppia EXP",
			_ => "Consumabile",
		};
	}

	private static string CampaignConsumableDescription(CampaignConsumableType itemType)
	{
		return itemType switch
		{
			CampaignConsumableType.Detector => "Rivela il contenuto delle tre porte nella prossima scelta della via. Le etichette compaiono sulle porte prima di entrare.",
			CampaignConsumableType.SecondChance => "Resuscita tutte le carte nel cimitero e le rimette nel mazzo. Non puo essere usata in battaglia.",
			CampaignConsumableType.Defrost => "Scongela tutte le carte in cooldown e le rimette nel mazzo. Non puo essere usata in battaglia.",
			CampaignConsumableType.Empower => "Aumenta di uno step il tuo dado Vigore in attacco per la stanza corrente o per la prossima stanza. Non puo essere usato nelle stanze Boss o Miniboss.",
			CampaignConsumableType.SigilloRubino => "Potenzia permanentemente di +2 una carta del mazzo. Ogni carta puo ricevere un solo Sigillo Rubino.",
			CampaignConsumableType.DoubleExp => "Raddoppia tutta l'esperienza ottenuta nella prossima stanza.",
			_ => "Oggetto consumabile da campagna.",
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
			SetMessage($"{CampaignConsumableName(itemType)} non puo essere usato in battaglia.");
			AppendLog($"CONSUMABILE - {CampaignConsumableName(itemType)} bloccato: battaglia in corso.");
			return false;
		}
		if (itemType == CampaignConsumableType.Empower && IsEmpowerBlockedInCurrentRoom())
		{
			SetMessage("Empower non puo essere usato nelle stanze Boss o Miniboss.");
			AppendLog("CONSUMABILE - Empower bloccato: stanza Boss/Miniboss.");
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
				SetMessage("Detector attivato: i destini delle tre porte sono rivelati.");
			}
			else
			{
				nextDoorChoiceRevealed = true;
				SetMessage("Detector attivato: la prossima scelta porte mostrera il destino di ogni porta.");
			}
			AppendLog("CONSUMABILE - Detector attivato.");
			return true;
		case CampaignConsumableType.SecondChance:
			int revived = RecoverAllGraveyardCards();
			SetMessage($"Seconda Chance: {revived} carte tornano dal cimitero al mazzo.");
			AppendLog($"CONSUMABILE - Seconda Chance recupera {revived} carte.");
			return true;
		case CampaignConsumableType.Defrost:
			int defrosted = campaignDeck?.ReleaseCooldown() ?? 0;
			SetMessage($"Defrost: {defrosted} carte tornano dal cooldown al mazzo.");
			AppendLog($"CONSUMABILE - Defrost libera {defrosted} carte.");
			return true;
		case CampaignConsumableType.Empower:
			PlayEmpowerItemUseSfx();
			nextRoomEmpowered = true;
			RefreshPlayerHud();
			SetMessage("Empower attivato: il tuo dado Vigore in attacco sale di uno step per questa stanza o la prossima.");
			AppendLog("CONSUMABILE - Empower pronto: dado Vigore d'attacco +1 step nella stanza corrente o prossima.");
			return true;
		case CampaignConsumableType.DoubleExp:
			nextRoomDoubleExperience = true;
			SetMessage("Doppia EXP attivata: la prossima stanza dara esperienza doppia.");
			AppendLog("CONSUMABILE - Doppia EXP pronta per la prossima stanza.");
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
			SetMessage("Sigillo Rubino: nessuna carta valida nel mazzo. Le carte gia' sigillate non possono riceverne un altro.");
			AppendLog("CONSUMABILE - Sigillo Rubino bloccato: nessun bersaglio valido.");
			return false;
		}
		rubySealTargetSelectionActive = true;
		if ((Object)(object)implementationArchivePanel != (Object)null)
		{
			SetImplementationArchiveVisible(true);
			RefreshImplementationArchive();
		}
		SetMessage("Sigillo Rubino pronto: scegli una carta del mazzo o del cooldown nella borsa.");
		AppendLog("CONSUMABILE - Sigillo Rubino: scelta bersaglio attiva.");
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
			SetMessage("Sigillo Rubino: scegli una carta combattente non sigillata, fuori dal cimitero.");
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
			SetMessage("Sigillo Rubino: questa carta e' gia' sigillata.");
			return false;
		}
		RecordConsumedBagItem(CampaignConsumableType.SigilloRubino);
		rubySealTargetSelectionActive = false;
		PlayEmpowerItemUseSfx();
		string cardName = CardDisplayNames.MarketName(target.Definition);
		SetMessage($"Sigillo Rubino inciso su {cardName}: forza permanente +2.");
		AppendLog($"CONSUMABILE - Sigillo Rubino: {cardName} ottiene +2 permanente.");
		if ((Object)(object)implementationArchivePanel != (Object)null && implementationArchivePanel.activeSelf)
		{
			RefreshImplementationArchive();
		}
		return true;
	}

	private bool HasRubySealTarget()
	{
		if (campaignDeck == null)
		{
			return false;
		}
		return campaignDeck.Cards.Any(IsRubySealTarget);
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
		AppendLog(source + " - ottieni consumabile " + itemName + ".");
		return (description: " " + source + ": ottieni " + itemName + ".", bonusExperience: 0);
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
			AppendLog("BISACCIA - vuota: nessun consumabile in questa run.");
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
		AppendLog($"BISACCIA - {runBagItemIds.Count} consumabili portati in run.");
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
	/// </summary>
	private void RecordConsumedBagItem(CampaignConsumableType itemType)
	{
		string itemId = SanctuaryItemIdOf(itemType);
		if (string.IsNullOrEmpty(itemId) || !runBagItemIds.Remove(itemId))
		{
			return;
		}
		consumedBagItemIds.Add(itemId);
	}

	private int ConsumeNextRoomExperienceMultiplier()
	{
		if (!nextRoomDoubleExperience)
		{
			return 1;
		}
		nextRoomDoubleExperience = false;
		AppendLog("CONSUMABILE - Doppia EXP consumata.");
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
		cardInspectionSummaryText.text = CampaignConsumableName(itemType) + "\n\n" + CampaignConsumableDescription(itemType)
			+ $"\n\nQuantita: {campaignConsumables?.GetQuantity(itemType) ?? 0}\nUso singolo. Solo campagna.";
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
				cardInspectionDraftConfirmButtonText.text = "USA";
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
