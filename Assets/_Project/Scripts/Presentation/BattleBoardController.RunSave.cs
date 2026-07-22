using System;
using System.Collections.Generic;
using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	// Persistenza del save/resume della run di campagna. Isolato in questo partial per
	// contenere la superficie di modifica sul controller: gli altri file lo agganciano solo
	// in tre punti (BeginRoomChoice -> SaveCurrentRun, ReturnToStart -> ClearSavedRun,
	// ShowCampaignModeSelection -> TryStartResumedCampaign).
	private readonly CampaignRunSaveService runSaveService = new CampaignRunSaveService();

	private bool HasResumableRun => runSaveService.HasSave;

	// --- Salvataggio ---

	private CampaignRunSave CaptureRunSave()
	{
		var save = new CampaignRunSave();
		if (runProgress != null)
			CampaignRunMapper.WriteProgress(save, runProgress);
		if (campaignDeck != null)
			CampaignRunMapper.WriteDeck(save, campaignDeck);

		save.campaignScenarioId = campaignScenarioId;
		save.campaignScenarioBossId = campaignScenarioBossId;
		save.merchantRoomsBlockedUntilMonster = merchantRoomsBlockedUntilMonster;
		save.rewardRoomsBlockedUntilMonster = rewardRoomsBlockedUntilMonster;
		save.nextMonsterTierBonus = nextMonsterTierBonus;
		save.nextDoorChoiceRevealed = nextDoorChoiceRevealed;

		save.consumables = new List<CampaignConsumableSave>();
		foreach (CampaignConsumableType type in Enum.GetValues(typeof(CampaignConsumableType)))
		{
			int count = campaignConsumables != null ? campaignConsumables.GetQuantity(type) : 0;
			if (count > 0)
				save.consumables.Add(new CampaignConsumableSave { type = type.ToString(), count = count });
		}
		return save;
	}

	// Salva lo stato tra una stanza e l'altra: chiamato da BeginRoomChoice, dove il
	// combattimento è smontato e lo stato è coerente.
	private void SaveCurrentRun()
	{
		if (campaignDeck == null || runProgress == null || pvpPresentationActive)
			return;
		try
		{
			runSaveService.Save(CaptureRunSave());
		}
		catch (Exception exception)
		{
			Debug.LogWarning($"[Campaign] Salvataggio run fallito: {exception.Message}");
		}
	}

	private void ClearSavedRun()
	{
		try
		{
			runSaveService.Clear();
		}
		catch (Exception exception)
		{
			Debug.LogWarning($"[Campaign] Pulizia save run fallita: {exception.Message}");
		}
	}

	// --- Ripresa ---

	// Tenta di riprendere una run salvata e di aprire la scelta della via. Ritorna true se
	// la run è stata ripristinata, false se non c'era nulla di riprendibile (o era inservibile,
	// es. carte non più presenti nel database dopo un aggiornamento).
	private bool TryStartResumedCampaign()
	{
		if (!runSaveService.TryLoad(out CampaignRunSave save) || save.deck == null || save.deck.Count == 0)
			return false;

		// Ambiente minimo che LoadBattle prepara prima di far girare la campagna
		// (configuration/random/runProgress esistono già da Awake).
		if ((Object)(object)cardDatabase == (Object)null)
			cardDatabase = Resources.Load<CardDatabase>("CardDatabase");
		if ((Object)(object)cardDatabase == (Object)null)
		{
			SetMessage("Database carte non trovato. Impossibile riprendere la run.");
			return false;
		}
		if (formationDraftService == null)
			formationDraftService = new FormationDraftService(random);

		// Mazzo
		campaignDeck = new CampaignDeckState(new List<CardDefinition>());
		CampaignRunMapper.ReadDeck(save, campaignDeck, cardDatabase.FindById);
		if (campaignDeck.Cards.Count == 0)
		{
			// Nessuna carta del salvataggio è più nel database: salvataggio inservibile.
			ClearSavedRun();
			campaignDeck = null;
			return false;
		}

		// Progressione
		ResetRunProgress();
		CampaignRunMapper.ReadProgress(save, runProgress);

		// Scenario / regole di stanza
		campaignScenarioId = string.IsNullOrWhiteSpace(save.campaignScenarioId) ? null : save.campaignScenarioId;
		campaignScenarioBossId = string.IsNullOrWhiteSpace(save.campaignScenarioBossId) ? null : save.campaignScenarioBossId;
		merchantRoomsBlockedUntilMonster = save.merchantRoomsBlockedUntilMonster;
		rewardRoomsBlockedUntilMonster = save.rewardRoomsBlockedUntilMonster;
		nextMonsterTierBonus = save.nextMonsterTierBonus;
		nextDoorChoiceRevealed = save.nextDoorChoiceRevealed;

		// Consumabili
		campaignConsumables.Clear();
		if (save.consumables != null)
		{
			foreach (CampaignConsumableSave consumable in save.consumables)
			{
				if (Enum.TryParse(consumable.type, out CampaignConsumableType type))
					campaignConsumables.Add(type, consumable.count);
			}
		}

		initialDeckBuilder = null;

		if ((Object)(object)modeSelectionPanel != (Object)null)
			modeSelectionPanel.SetActive(false);
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
			campaignModeSelectionPanel.SetActive(false);
		if ((Object)(object)deckBuilderPanel != (Object)null)
			deckBuilderPanel.SetActive(false);

		AppendLog($"CAMPAGNA RIPRESA - livello {runProgress.PlayerLevel}, stanze superate {runProgress.RoomsCleared}, {campaignDeck.Cards.Count} carte nel mazzo.");
		PlayTransitionSfx();
		BeginRoomChoice();
		return true;
	}

	// --- Ciclo di vita app: salva uscendo, ma solo fuori dal combattimento (uno stato
	// mid-combattimento non è un punto di ripresa valido). Il salvataggio autorevole resta
	// quello di BeginRoomChoice. ---

	private void OnApplicationPause(bool paused)
	{
		if (paused && campaignDeck != null && !IsCampaignBattleActive())
			SaveCurrentRun();
	}

	private void OnApplicationFocus(bool focused)
	{
		if (!focused && campaignDeck != null && !IsCampaignBattleActive())
			SaveCurrentRun();
	}
}
}
