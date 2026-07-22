using System;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private static string CampaignConsumableResourceName(CampaignConsumableType itemType)
	{
		return itemType switch
		{
			CampaignConsumableType.Detector => "detector_item",
			CampaignConsumableType.SecondChance => "second_chance_item",
			CampaignConsumableType.Defrost => "defrost_item",
			CampaignConsumableType.Empower => "empower_item",
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
		if (campaignConsumables == null || !campaignConsumables.TryConsume(itemType))
		{
			return false;
		}
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

	private static bool IsConsumableBlockedInBattle(CampaignConsumableType itemType)
	{
		return itemType == CampaignConsumableType.SecondChance
			|| itemType == CampaignConsumableType.Defrost;
	}

	private bool IsCampaignBattleActive()
	{
		return campaignDeck != null
			&& (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss)
			&& (draftActive || deploymentDraftActive || roundNumber > 0 || playerCards.Count > 0 || cpuCards.Count > 0);
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
			CampaignConsumableType.DoubleExp
		};
		CampaignConsumableType itemType = pool[random.NextInclusive(0, pool.Length - 1)];
		campaignConsumables.Add(itemType);
		string itemName = CampaignConsumableName(itemType);
		AppendLog(source + " - ottieni consumabile " + itemName + ".");
		return (description: " " + source + ": ottieni " + itemName + ".", bonusExperience: 0);
	}

	private void GrantStartingCampaignConsumablesForTesting()
	{
		campaignConsumables.Clear();
		campaignConsumables.Add(CampaignConsumableType.Detector, 2);
		campaignConsumables.Add(CampaignConsumableType.SecondChance, 2);
		campaignConsumables.Add(CampaignConsumableType.Defrost, 2);
		campaignConsumables.Add(CampaignConsumableType.Empower, 2);
		campaignConsumables.Add(CampaignConsumableType.DoubleExp, 2);
		AppendLog("CONSUMABILI TEST - 2 copie per ogni item aggiunte alla borsa.");
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
