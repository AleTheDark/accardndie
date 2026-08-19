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

	/// <summary>
	/// true da quando il giocatore ha varcato una porta a quando la stanza è finita e si
	/// torna alla scelta della via. È quello che distingue "fermo davanti alle porte" da
	/// "già dentro la stanza": senza, una run ripresa tornava alle porte e la stanza
	/// appena vista si poteva cambiare.
	/// </summary>
	private bool campaignRoomEntered;

	// Dove stavano i dadi quando la porta è stata varcata. Restano fermi qui per tutta la
	// stanza: è da questo punto che una stanza ripresa si rimonta, e solo così quello che
	// c'è dentro esce identico a prima.
	private int campaignRoomEntryRandomSeed;
	private int campaignRoomEntryRandomDraws;
	private int campaignRoomEntryCpuRandomSeed;
	private int campaignRoomEntryCpuRandomDraws;

	/// <summary>
	/// Segna la soglia: da qui in poi il giocatore è dentro la stanza, e i dadi di questo
	/// istante sono quelli da cui la stanza si rimonta se il gioco viene riaperto.
	/// </summary>
	private void MarkCampaignRoomEntered()
	{
		campaignRoomEntered = true;
		campaignRoomEntryRandomSeed = battleRandom?.Seed ?? 0;
		campaignRoomEntryRandomDraws = battleRandom?.Draws ?? 0;
		campaignRoomEntryCpuRandomSeed = battleCpuRandom?.Seed ?? 0;
		campaignRoomEntryCpuRandomDraws = battleCpuRandom?.Draws ?? 0;
	}

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

		// Le regole a colpo singolo armate fuori dal combattimento: un oggetto speso o
		// un'opportunità del bottino valgono per la stanza dopo, e devono attraversare
		// anche una chiusura del gioco.
		save.skipNextCombatCooldown = skipNextCombatCooldown;
		save.nextCombatFallenHeroesGrantExperience = nextCombatFallenHeroesGrantExperience;
		save.nextCombatAssassinsActLast = nextCombatAssassinsActLast;
		save.nextCombatWarriorsLowerVigor = nextCombatWarriorsLowerVigor;
		save.nextCombatTankDuel = nextCombatTankDuel;
		save.nextRoomEmpowered = nextRoomEmpowered;
		save.nextRoomDoubleExperience = nextRoomDoubleExperience;

		// I dadi della run: da dove riprenderanno a estrarre porte, stanze e formazioni.
		save.randomSeed = battleRandom?.Seed ?? 0;
		save.randomDraws = battleRandom?.Draws ?? 0;
		save.cpuRandomSeed = battleCpuRandom?.Seed ?? 0;
		save.cpuRandomDraws = battleCpuRandom?.Draws ?? 0;

		// Dove si è fermata la run: davanti alle porte estratte, o dentro la stanza scelta.
		save.roomState = CaptureRoomState();

		// La battaglia in corso, se c'è: fuori dal combattimento resta null e il
		// salvataggio è quello di sempre, fermo alla stanza in corso.
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

	/// <summary>
	/// Le tre porte come sono state estratte e, se il giocatore ne ha già varcata una, quale.
	/// </summary>
	private CampaignRoomStateSave CaptureRoomState()
	{
		var room = new CampaignRoomStateSave
		{
			backgroundIndex = roomChoiceBackgroundIndex,
			roomEntered = campaignRoomEntered,
			roomType = (int)currentRoomType,
			scenarioId = pendingScenarioId,
			roomDifficulty = (int)pendingRoomDifficulty,
			entryRandomSeed = campaignRoomEntryRandomSeed,
			entryRandomDraws = campaignRoomEntryRandomDraws,
			entryCpuRandomSeed = campaignRoomEntryCpuRandomSeed,
			entryCpuRandomDraws = campaignRoomEntryCpuRandomDraws
		};
		foreach (CampaignDoor door in campaignDoors)
		{
			var doorSave = new CampaignDoorSave();
			if (door.RevealedRoom.HasValue)
			{
				CampaignRoomRoll revealed = door.RevealedRoom.Value;
				doorSave.revealed = true;
				doorSave.roomType = (int)revealed.RoomType;
				doorSave.scenarioId = revealed.ScenarioId;
				doorSave.difficulty = (int)revealed.Difficulty;
			}
			room.doors.Add(doorSave);
		}
		return room;
	}

	/// <summary>
	/// Mette per iscritto un oggetto appena usato. Vale davanti alle porte, dove serve: il
	/// Detector speso lì mostra tre stanze, e senza salvare bastava riaprire il gioco per
	/// riavere l'oggetto tenendosi quello che si era visto.
	///
	/// Dentro una stanza invece non si scrive. Lì il punto di ripresa è la soglia, e ci si
	/// torna tutti insieme: l'oggetto torna nella bisaccia, il suo effetto se ne va con lui
	/// e la stanza si rimonta identica. Scrivere a metà stanza spezzerebbe proprio questo -
	/// il salvataggio direbbe "oggetto speso" mentre la stanza riparte da prima - e con la
	/// vetrina del mercante diventerebbe un modo di riestrarre la merce.
	/// </summary>
	private void SaveAfterCampaignItemUse()
	{
		if (campaignDeck == null || campaignRoomEntered)
			return;
		SaveCurrentRun();
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
		if (campaignDeck != null || pvpPresentationActive)
			return;
		// Durante il tour guidato il popup coprirebbe il passo in corso.
		if (IsTutorialOnboardingActive())
			return;

		CampaignRunLoadResult result = runSaveService.Load(out CampaignRunSave save);
		if (result == CampaignRunLoadResult.OtherGameVersion)
		{
			ShowRunFromAnotherVersionPrompt(save);
			return;
		}
		if (result != CampaignRunLoadResult.Loaded)
		{
			// Illeggibile vuol dire inservibile: si toglie di mezzo adesso, o resterebbe a
			// farsi riproporre a ogni ingresso in campagna.
			if (result == CampaignRunLoadResult.Unreadable)
				ClearSavedRun();
			return;
		}

		ShowCampaignRecoveryPopup(
			GameText.Get(GameTextKeys.Campaign.RecoverySavedBody),
			GameText.Get(GameTextKeys.Campaign.RecoveryCancel),
			ResumeSavedRun,
			CancelSavedRun);
	}

	/// <summary>
	/// La run è stata giocata con un'altra patch: non si riprende. Una campagna porta con sé
	/// le carte, i costi, le stanze e le regole della versione con cui è cominciata, e
	/// rimetterla in piedi con un'altra vorrebbe dire ricostruire uno stato che questa
	/// versione non sa più leggere. Il popup lo dice e offre solo di ricominciare: fare
	/// sparire il salvataggio in silenzio sembrerebbe un bug.
	/// </summary>
	private void ShowRunFromAnotherVersionPrompt(CampaignRunSave save)
	{
		string savedVersion = save != null && !string.IsNullOrWhiteSpace(save.gameVersion)
			? save.gameVersion
			: GameText.Get(GameTextKeys.Campaign.RecoveryVersionUnknown);
		AppendLog($"CAMPAGNA - salvataggio di un'altra versione ({savedVersion}, ora {runSaveService.CurrentGameVersion}): non riprendibile.");
		ShowCampaignRecoveryPopup(
			GameText.Format(GameTextKeys.Campaign.RecoveryVersionBody, savedVersion, runSaveService.CurrentGameVersion),
			GameText.Get(GameTextKeys.Common.Close),
			CancelSavedRun,
			CancelSavedRun,
			resumeAvailable: false);
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
		SetMessage(GameText.Get(GameTextKeys.Campaign.RecoveryUnusableSave));
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
		skipNextCombatCooldown = save.skipNextCombatCooldown;
		nextCombatFallenHeroesGrantExperience = save.nextCombatFallenHeroesGrantExperience;
		nextCombatAssassinsActLast = save.nextCombatAssassinsActLast;
		nextCombatWarriorsLowerVigor = save.nextCombatWarriorsLowerVigor;
		nextCombatTankDuel = save.nextCombatTankDuel;
		nextRoomEmpowered = save.nextRoomEmpowered;
		nextRoomDoubleExperience = save.nextRoomDoubleExperience;

		// I dadi ripartono da dove si erano fermati, non da un seme nuovo: le porte e il
		// contenuto della stanza che la run aveva già estratto devono riuscire uguali.
		if (save.randomSeed != 0 || save.randomDraws > 0)
			RestoreRunRandom(save.randomSeed, save.randomDraws, save.cpuRandomSeed, save.cpuRandomDraws);

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
		if (save.HasBattle)
		{
			if (TryRestoreBattle(save.battle))
			{
				AppendLog($"CAMPAGNA RIPRESA - si torna in battaglia al round {roundNumber}.");
				return true;
			}

			// Battaglia irricostruibile: si riparte dalla scelta della via e non dalla
			// stanza. Quel salvataggio è stato preso a metà scontro, con le carte ancora
			// in campo: rientrare nella stanza le rimetterebbe a schierare da un mazzo che
			// le considera già schierate.
			BeginRoomChoice();
			return true;
		}

		// Nessuna battaglia da rimontare: si torna dove il giocatore si era fermato, che
		// sia dentro una stanza o davanti alle porte che aveva già estratto.
		if (TryResumeSavedRoom(save.roomState))
			return true;

		BeginRoomChoice();
		return true;
	}

	/// <summary>
	/// Rimette la run dove si era fermata fra due battaglie. Ritorna false se il
	/// salvataggio non lo sa (i salvataggi scritti prima della v3), e allora si riparte
	/// dalla scelta della via come si è sempre fatto.
	/// </summary>
	private bool TryResumeSavedRoom(CampaignRoomStateSave room)
	{
		if (room == null || !room.HasState)
			return false;

		roomChoiceBackgroundIndex = Mathf.Clamp(room.backgroundIndex, 1, 5);

		if (room.roomEntered)
		{
			ResumeEnteredRoom(room);
			return true;
		}

		ResumeRoomChoice(room);
		return true;
	}

	/// <summary>
	/// Riapre la scelta della via con le porte di prima. Non se ne estraggono di nuove:
	/// erano già state decise, e il Detector speso su quelle deve valere ancora.
	/// </summary>
	private void ResumeRoomChoice(CampaignRoomStateSave room)
	{
		ClearBoardForRoomTransition();
		campaignDoors.Clear();
		foreach (CampaignDoorSave door in room.doors)
		{
			campaignDoors.Add(door.revealed
				? new CampaignDoor(new CampaignRoomRoll(
					(RoomType)door.roomType,
					string.IsNullOrWhiteSpace(door.scenarioId) ? null : door.scenarioId,
					(RoomDifficulty)door.difficulty))
				: new CampaignDoor());
		}
		RefreshRoomChoiceRevealLabels();
		ShowRoomChoicePanel();
		AppendLog($"CAMPAGNA RIPRESA - si torna davanti alle stesse {campaignDoors.Count} porte.");
	}

	/// <summary>
	/// Rientra nella stanza che il giocatore aveva già aperto. La stanza si rimonta da capo
	/// - il salvataggio è stato preso sulla soglia - ma con i dadi fermi dove erano: quello
	/// che c'è dentro esce identico, e riaprire il gioco non è un modo di cambiare stanza.
	/// </summary>
	private void ResumeEnteredRoom(CampaignRoomStateSave room)
	{
		ClearBoardForRoomTransition();
		// I dadi tornano sulla soglia, non a dove erano quando il gioco è stato chiuso:
		// la stanza si rimonta da capo e deve rifare le stesse estrazioni.
		RestoreRunRandom(room.entryRandomSeed, room.entryRandomDraws,
			room.entryCpuRandomSeed, room.entryCpuRandomDraws);
		currentRoomType = (RoomType)room.roomType;
		pendingScenarioId = string.IsNullOrWhiteSpace(room.scenarioId) ? null : room.scenarioId;
		pendingRoomDifficulty = (RoomDifficulty)room.roomDifficulty;
		MarkCampaignRoomEntered();
		AppendLog($"CAMPAGNA RIPRESA - si rientra nella stanza {currentRoomType}, la stessa di prima.");
		EnterChosenCampaignRoom();
	}

	// --- Ciclo di vita app ---
	//
	// Uscendo si salva solo fuori da una stanza: dentro, quello che c'e' gia' su disco e'
	// il checkpoint della soglia, ed e' coerente. Sovrascriverlo qui fotograferebbe la
	// stanza a meta' - durante uno scontro con la timeline ferma su un turno gia'
	// cominciato, in una stanza mercato con la merce gia' comprata - e la stanza ripresa
	// si rimonterebbe su uno stato che non e' piu' quello da cui era partita.

	private bool CanSaveOutsideARoom => campaignDeck != null && !campaignRoomEntered && !IsCampaignBattleActive();

	private void OnApplicationPause(bool paused)
	{
		if (paused && CanSaveOutsideARoom)
			SaveCurrentRun();
	}

	private void OnApplicationFocus(bool focused)
	{
		if (!focused && CanSaveOutsideARoom)
			SaveCurrentRun();
	}
}
}
