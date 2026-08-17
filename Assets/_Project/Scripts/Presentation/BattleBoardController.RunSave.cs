using System;
using System.Collections.Generic;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private bool IsComposableGolemDebugSession => debugForceFirstRoomComposableGolem;

	// Persistenza del save/resume della run di campagna. Isolato in questo partial per
	// contenere la superficie di modifica sul controller: gli altri file lo agganciano solo
	// nei punti di salvataggio/pulizia. La ripresa non deve bypassare la scelta single player.
	// Il salvataggio è dell'account che sta giocando: il playerId viene letto a ogni
	// accesso, così un cambio di account durante la sessione cambia anche la campagna
	// che il gioco propone di riprendere.
	private readonly CampaignRunSaveService runSaveService = new CampaignRunSaveService(
		new PlayerPrefsCampaignRunStore(() => AccardND.Network.AccountServerSession.PlayerId));

	private bool HasResumableRun => runSaveService.HasSave;

	// --- Salvataggio ---

	private CampaignRunSave CaptureRunSave()
	{
		var save = new CampaignRunSave();
		if (runProgress != null)
			CampaignRunMapper.WriteProgress(save, runProgress);
		if (campaignDeck != null)
			CampaignRunMapper.WriteDeck(save, campaignDeck);
		save.playerMana = CampaignPlayerManaCurrent;

		save.runRewardId = campaignRunRewardId;
		save.campaignScenarioId = campaignScenarioId;
		save.campaignScenarioBossId = campaignScenarioBossId;
		save.adventureChapterId = activeAdventureChapterId;
		save.defeatedBossIds = new List<string>(defeatedBossIdsInRun);
		save.runBagItemIds = new List<string>(runBagItemIds);
		save.consumedBagItemIds = new List<string>(consumedBagItemIds);
		save.merchantRoomsBlockedUntilMonster = merchantRoomsBlockedUntilMonster;
		save.rewardRoomsBlockedUntilMonster = rewardRoomsBlockedUntilMonster;
		save.freeMerchantUpgradeUsed = !freeMerchantUpgradeAvailable;
		save.secondWindUsed = !secondWindAvailable;
		save.nextMonsterDifficultyIncrease = nextMonsterDifficultyIncrease;
		save.nextDoorChoiceRevealed = nextDoorChoiceRevealed;
		save.nextMonsterRewardHalved = nextMonsterRewardHalved;

		// La battaglia in corso, se c'è: fuori dal combattimento resta null e il
		// salvataggio è quello di sempre, fermo alla scelta della via.
		save.battle = CaptureBattle();

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
		// Le scene di test non devono sporcare il save della campagna vera.
		if (campaignDeck == null || runProgress == null || pvpPresentationActive || debugMerchantScene || IsComposableGolemDebugSession)
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
		if (IsComposableGolemDebugSession)
			return;

		try
		{
			runSaveService.Clear();
		}
		catch (Exception exception)
		{
			Debug.LogWarning($"[Campaign] Pulizia save run fallita: {exception.Message}");
		}
	}

	// --- Proposta di ripresa ---

	/// <summary>
	/// Primo ingresso in campagna: se c'è una run lasciata a metà la si propone prima di
	/// far scegliere la modalità. Il salvataggio resta lì finché il giocatore non decide -
	/// annullare è l'unico modo di buttarlo via - così chiudere il gioco a metà campagna
	/// non è più un abbandono silenzioso.
	///
	/// È il fratello freddo del popup della sessione rifatta: quello riprende un oggetto
	/// ancora vivo in memoria, questo rilegge il checkpoint da disco.
	/// </summary>
	private void ShowResumableRunPromptIfAny()
	{
		// Una campagna già in corso in questa sessione non va riproposta: quel salvataggio
		// è il suo, e a rimetterla in piedi ci pensa semmai il recupero di sessione.
		if (campaignDeck != null || pvpPresentationActive || !HasResumableRun)
			return;
		// Durante il tour guidato il popup coprirebbe il passo in corso.
		if (IsTutorialOnboardingActive())
			return;

		ShowCampaignRecoveryPopup(
			GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RecoverySavedBody,
				"Hai una campagna lasciata a meta'. Vuoi riprenderla da dov'eri o annullarla e ricominciare?"),
			GameText.GetOrFallbackSilent(GameTextKeys.Campaign.RecoveryCancel, "ANNULLA"),
			ResumeSavedRun,
			CancelSavedRun);
	}

	private void ResumeSavedRun()
	{
		campaignRecoveryPopup.SetActive(false);
		if (TryStartResumedCampaign())
			return;

		// Salvataggio inservibile (carte sparite in un aggiornamento, file illeggibile):
		// via anche quello, o al prossimo ingresso in campagna riproporrebbe una ripresa
		// che non può riuscire, all'infinito.
		ClearSavedRun();
		SetMessage(GameText.GetOrFallbackSilent(
			GameTextKeys.Campaign.RecoveryUnusableSave,
			"La campagna salvata non e' piu' riprendibile: si riparte da capo."));
	}

	private void CancelSavedRun()
	{
		campaignRecoveryPopup.SetActive(false);
		ClearSavedRun();
		AppendLog("CAMPAGNA - salvataggio annullato dal giocatore: si riparte da capo.");
	}

	// --- Ripresa ---

	// Tenta di riprendere una run salvata e di aprire la scelta della via. Ritorna true se
	// la run è stata ripristinata, false se non c'era nulla di riprendibile (o era inservibile,
	// es. carte non più presenti nel database dopo un aggiornamento).
	private bool TryStartResumedCampaign()
	{
		if (IsComposableGolemDebugSession)
			return false;

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
		RestoreCampaignMana(save.playerMana);

		// Id della run: la riga dello storico aperta all'avvio si chiude con questo, e una
		// run ripresa non deve restare fra le abbandonate.
		campaignRunRewardId = string.IsNullOrWhiteSpace(save.runRewardId) ? null : save.runRewardId;

		// Scenario / regole di stanza
		campaignScenarioId = string.IsNullOrWhiteSpace(save.campaignScenarioId) ? null : save.campaignScenarioId;
		campaignScenarioBossId = string.IsNullOrWhiteSpace(save.campaignScenarioBossId) ? null : save.campaignScenarioBossId;
		activeAdventureChapterId = string.IsNullOrWhiteSpace(save.adventureChapterId) ? null : save.adventureChapterId;
		defeatedBossIdsInRun.Clear();
		if (save.defeatedBossIds != null)
			defeatedBossIdsInRun.AddRange(save.defeatedBossIds);
		runBagItemIds.Clear();
		if (save.runBagItemIds != null)
			runBagItemIds.AddRange(save.runBagItemIds);
		consumedBagItemIds.Clear();
		if (save.consumedBagItemIds != null)
			consumedBagItemIds.AddRange(save.consumedBagItemIds);
		merchantRoomsBlockedUntilMonster = save.merchantRoomsBlockedUntilMonster;
		rewardRoomsBlockedUntilMonster = save.rewardRoomsBlockedUntilMonster;
		// ResetRunProgress() qui sopra ha riarmato i talenti una-tantum: il salvataggio dice
		// quali erano gia' stati spesi, e va riletto dopo.
		RestoreTalentRunState(save.freeMerchantUpgradeUsed, save.secondWindUsed);
		nextMonsterDifficultyIncrease = save.nextMonsterDifficultyIncrease;
		nextDoorChoiceRevealed = save.nextDoorChoiceRevealed;
		nextMonsterRewardHalved = save.nextMonsterRewardHalved;

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

		SetAccountHubHudActive(false);
		AppendLog($"CAMPAGNA RIPRESA - livello {runProgress.PlayerLevel}, stanze superate {runProgress.RoomsCleared}, {campaignDeck.Cards.Count} carte nel mazzo.");
		// Una run ripresa non passa da LoadCampaignConsumablesFromBag: senza questa riga
		// sarebbe l'unico modo di giocare senza che nessuno prepari gli annunci, e a fine run
		// il TRIPLICA non comparirebbe mai.
		WarmCampaignRunAds();
		PlayTransitionSfx();

		// Se il salvataggio è stato preso a metà scontro si torna in campo, non alla
		// scelta della via. Se la battaglia non è ricostruibile - una carta sparita da un
		// aggiornamento - si ripiega sulla scelta della via, che è sempre un punto valido:
		// si perde lo scontro in corso, non la run.
		if (save.HasBattle && TryRestoreBattle(save.battle))
		{
			AppendLog($"CAMPAGNA RIPRESA - si torna in battaglia al round {roundNumber}.");
			return true;
		}

		BeginRoomChoice();
		return true;
	}

	// --- Ciclo di vita app ---
	//
	// Uscendo si salva solo fuori dal combattimento: durante uno scontro l'unico a
	// scrivere e' il confine di turno (vedi SaveCurrentBattleTurn), e quello che c'e' gia'
	// su disco e' coerente. Sovrascriverlo qui vorrebbe dire fotografare la battaglia a
	// meta' di un'animazione, con la timeline ferma su un turno gia' cominciato.

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
