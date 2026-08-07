using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private CardDefinition pendingMasterGiftReward;

	private List<CampaignCardInstance> GetCampaignDefeatedCards()
	{
		return (from card in playerCards
			where IsCampaignDefeated(card) && card.CampaignCard != null
			select card.CampaignCard).ToList();
	}

	/// <summary>Segna le famiglie dei mostri appena sconfitti per lo sblocco icone PvP.</summary>
	private void RecordCampaignMonsterKills()
	{
		foreach (BattleCardState card in cpuCards)
		{
			if (card.Eliminated && card.Definition != null && card.Definition.Category == CardCategory.Monster)
				AccardND.PvpUi.PvpCampaignKillTracker.RecordDefeatFromCardId(card.Definition.Id);
		}
	}

	/// <summary>
	/// Somma i mostri eliminati nella stanza al contatore della run. Conta solo la categoria
	/// Monster: boss e miniboss hanno contatori propri e non devono essere contati due volte.
	/// </summary>
	private void RecordCampaignEnemiesDefeated()
	{
		if (runProgress == null)
			return;

		int defeated = 0;
		foreach (BattleCardState card in cpuCards)
		{
			if (card.Eliminated && card.Definition != null && card.Definition.Category == CardCategory.Monster)
				defeated++;
		}
		if (defeated > 0)
			runProgress.RecordEnemiesDefeated(defeated);
	}

	/// <summary>
	/// Conta i dadi che stanno per essere lanciati e restituisce il tiro invariato, cosi' da
	/// poter avvolgere l'argomento della chiamata di presentazione. Il conteggio vive in un
	/// punto solo invece di essere sparso sulle decine di rami di combattimento (boss,
	/// miniboss, contrattacchi), che e' esattamente il motivo per cui prima non esisteva.
	/// Un tiro a due dadi ne conta due: e' quello che il giocatore vede rotolare.
	/// </summary>
	private VigorRollResult TrackDiceRoll(VigorRollResult roll)
	{
		if (runProgress != null)
			runProgress.RecordDiceRolled(roll.HasSecondRoll ? 2 : 1);
		return roll;
	}

	/// <summary>Come sopra per i tiri a dado singolo gia' risolti (iniziativa, eventi).</summary>
	private int TrackDiceRoll(int roll)
	{
		if (runProgress != null)
			runProgress.RecordDiceRolled(1);
		return roll;
	}

	/// <summary>
	/// Segna un'abilita' come consumata e, se la pedina e' del giocatore, la conta per le
	/// quest della taverna. Le abilita' si spendono su parecchi rami diversi (attacco armato,
	/// protezione del paladino, contrattacco): passare tutti da qui evita di dover ricordare
	/// quale di quei rami incrementa il contatore.
	/// </summary>
	private void MarkAbilityUsed(BattleCardState card)
	{
		if (card == null || !TrySpendCampaignPrimaryMana(card))
			return;

		card.AbilityUsed = true;
		card.AbilityUsedThisTurn = true;
		if (runProgress != null && playerCards.Contains(card))
			runProgress.RecordAbilityUsed();

		// L'abilita' e' risolta: si libera il segna-pagamento, che serve solo a non
		// addebitare due volte lo stesso uso (arma + consuma, come fa il Guerriero).
		// Cosi' l'uso successivo torna a pagare, ed e' il mana l'unico limite.
		ResetCampaignPrimaryManaPayment(card);
	}

	/// <summary>Registra un boss o miniboss sconfitto nella run (id normalizzato, senza duplicati adiacenti).</summary>
	private void RecordDefeatedBossInRun(string bossId)
	{
		if (string.IsNullOrWhiteSpace(bossId))
			return;
		defeatedBossIdsInRun.Add(bossId.Trim().ToLowerInvariant());
	}

	private bool CheckEndGame()
	{
		if (gameFinished)
		{
			return true;
		}
		bool flag = HasAliveCard(playerCards);
		bool flag2 = HasAliveCard(cpuCards);
		if (flag && flag2)
		{
			return false;
		}
		inputLocked = true;
		gameFinished = true;
		SetActiveTurnAura(null);
		int previousLevel = 0;
		int previousExperience = 0;
		RoomReward roomReward = default;
		if (!flag2)
		{
			FadeOutMusic(1.6f);
			survivingCpuFormation.Clear();
			canRetryCampaignRoom = false;
			if (campaignDeck != null)
			{
				List<CampaignCardInstance> campaignDefeatedCards = GetCampaignDefeatedCards();
				campaignDeck.CompleteCombat(campaignDefeatedCards, skipNextCombatCooldown);
				AppendLog($"ZONE MAZZO - disponibili {campaignDeck.AvailableCount}, " + $"cooldown {campaignDeck.CooldownCount}, cimitero {campaignDeck.GraveyardCount}.");
			}
			RecordCampaignMonsterKills();
			RecordCampaignEnemiesDefeated();
			if (IsFinalBossRoom())
				RecordCampaignBossVictory();
			SetTurnBanner(playerTurn: true, "VITTORIA  -  STANZA SUPERATA");
			previousLevel = runProgress.PlayerLevel;
			previousExperience = runProgress.CurrentExperience;
			if (activeComposableGolem != null)
			{
				RecordDefeatedBossInRun(ComposableGolemCardId);
				roomReward = runProgress.CompleteMinibossRoom(configuration.Progression.MinibossClearExperience, ConsumeNextRoomExperienceMultiplier());
			}
			else
			{
				int num = (nextCombatFallenHeroesGrantExperience ?playerCards.Where(IsCampaignDefeated).Sum((BattleCardState card) => card.Card.Strength) : 0);
				roomReward = runProgress.CompleteMonsterRoom((from card in cpuCards
					where card.Eliminated
					select card.Card.Strength).Concat((num <= 0) ?((IEnumerable<int>)Array.Empty<int>()) : ((IEnumerable<int>)new int[1] { num })),
					RoomDifficultyRules.For(pendingRoomDifficulty).BaseExperience,
					ConsumeNextRoomExperienceMultiplier());
			}
			SetMessage(GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RoomReward,
				"Hai guadagnato {0} punti esperienza e {1} oro!",
				roomReward.TotalExperience,
				roomReward.Gold));
			if (roomReward.LevelsGained > 0)
			{
				ShowLevelUpVigorHint();
			}
			SetPrimaryActionLabel(restartButtonText, PrimaryActionLabel.Advance);
			ApplyBattleButtonVariant(restartButton, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Arcane);
			canAdvanceToNextRoom = true;
			campaignLevelUpPending = roomReward.LevelsGained > 0;
		}
		else
		{
			SetTurnBanner(
				playerTurn: false,
				GameText.GetOrFallbackSilent(
					GameTextKeys.Campaign.DefeatFormationBanner,
					"SCONFITTA  -  FORMAZIONE ELIMINATA"),
				defeat: true);
			SetMessage("SCONFITTA. La CPU ha eliminato la tua formazione.");
			canAdvanceToNextRoom = false;
			// Anche i mostri abbattuti nella stanza in cui si muore vanno contati: prima il
			// conteggio stava solo nel ramo della vittoria, e l'ultima stanza di ogni run
			// spariva dalle statistiche. Su una ritirata non conta due volte, perche' la
			// stanza ritentata viene ricostruita con i soli sopravvissuti.
			RecordCampaignEnemiesDefeated();
			if (campaignDeck != null)
			{
				List<CampaignCardInstance> campaignDefeatedCards2 = GetCampaignDefeatedCards();
				campaignDeck.CompleteCombat(campaignDefeatedCards2, skipNextCombatCooldown);
				survivingCpuFormation.Clear();
				survivingCpuFormation.AddRange(from card in cpuCards
					where !card.Eliminated
					select card.Definition);
				AppendLog($"ZONE MAZZO - disponibili {campaignDeck.AvailableCount}, " + $"cooldown {campaignDeck.CooldownCount}, cimitero {campaignDeck.GraveyardCount}.");
				int formationSize = configuration.Gameplay.FormationSize;
				int combatReadyCount = campaignDeck.CombatReadyCount;
				if (combatReadyCount >= formationSize && survivingCpuFormation.Count > 0)
				{
					canRetryCampaignRoom = true;
					SetTurnBanner(
						playerTurn: false,
						GameText.GetOrFallbackSilent(
							GameTextKeys.Campaign.DefeatRetreatBanner,
							"SCONFITTA - RITIRATA"),
						defeat: true);
					SetMessage($"SCONFITTA. Puoi continuare: hai {combatReadyCount}/" + $"{formationSize} carte disponibili. Restano {survivingCpuFormation.Count} mostri nella stanza.");
					SetPrimaryActionLabel(restartButtonText, PrimaryActionLabel.RetryRoom);
					ApplyBattleButtonVariant(restartButton, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Arcane);
					((Component)restartButton).gameObject.SetActive(true);
					ShowFirstDefeatHint();
					RefreshInitiativeDisplay();
					UpdateInteractions();
					ClearConsumedCombatRules();
					return true;
				}
			}
			canRetryCampaignRoom = false;
			SetTurnBanner(playerTurn: false, "GAME OVER");
			SetMessage("GAME OVER. La CPU ha eliminato la tua formazione.");
			pendingCampaignRewardTask = ClaimCampaignRunAccountReward(completed: false);
		}
		RefreshInitiativeDisplay();
		((Component)restartButton).gameObject.SetActive((canAdvanceToNextRoom || flag) && !campaignLevelUpPending);
		UpdateInteractions();
		ClearConsumedCombatRules();
		// ClearConsumedCombatRules aggiorna la HUD e interrompe l'animazione EXP attiva.
		// La sequenza di level-up deve quindi partire per ultima, altrimenti non raggiunge
		// mai ShowCampaignLevelUpPopup e il giocatore resta senza un'azione disponibile.
		if (flag && !flag2)
			PlayCampaignExperienceReward(previousLevel, previousExperience, roomReward);
		NotifyAdventureTutorial(AdventureTutorialAction.BattleFinished);
		return true;
	}

	private void ClearConsumedCombatRules()
	{
		skipNextCombatCooldown = false;
		nextCombatFallenHeroesGrantExperience = false;
		nextCombatAssassinsActLast = false;
		nextCombatWarriorsLowerVigor = false;
		nextCombatTankDuel = false;
		nextRoomEmpowered = false;
		RefreshPlayerHud();
	}

	private void ResetScenarioRuleState()
	{
		ResetCampaignManaForNewRun();
		ClearConsumedCombatRules();
		nextMonsterTierBonus = 0;
		nextDoorChoiceRevealed = false;
		nextRoomEmpowered = false;
		nextRoomDoubleExperience = false;
		merchantRoomsBlockedUntilMonster = false;
		rewardRoomsBlockedUntilMonster = false;
	}

	private IEnumerator ReturnToStartAfterGameOver()
	{
		if (!returningToStartAfterGameOver)
		{
			returningToStartAfterGameOver = true;
			// Non resettare la schermata mentre clear capitolo/reward stanno ancora
			// applicando snapshot autoritativi alla cache della progressione.
			while (!pendingCampaignRewardTask.IsCompleted)
			{
				yield return null;
			}
			// La ricompensa e' gia' stata applicata prima di arrivare qui: dopo che il
			// giocatore ha proseguito basta una pausa breve per rendere leggibile il cambio
			// schermata, senza trattenerlo ancora sul riepilogo di fine campagna.
			yield return WaitForCardInspectionPause(1f);
			returningToStartAfterGameOver = false;
			if ((Object)(object)roomTransition != (Object)null && !roomTransition.IsPlaying)
			{
				AnimationConfiguration animation = configuration.Animation;
				PlayTransitionSfx();
				roomTransition.Play(ReturnToStart, animation.RoomFadeOutDuration, animation.RoomBlackHoldDuration, animation.RoomFadeInDuration);
			}
			else
			{
				ReturnToStart();
			}
		}
	}

	private void HandlePrimaryAction()
	{
		if (!((Object)(object)roomTransition == (Object)null) && !roomTransition.IsPlaying)
		{
			AnimationConfiguration animation = configuration.Animation;
			Action changeSceneContent = (canAdvanceToNextRoom ?new Action(StartNextRoom) : (canRetryCampaignRoom ?new Action(RetryCurrentCampaignRoom) : new Action(ResetBattle)));
			PlayTransitionSfx();
			roomTransition.Play(changeSceneContent, animation.RoomFadeOutDuration, animation.RoomBlackHoldDuration, animation.RoomFadeInDuration);
		}
	}

	private void RetryCurrentCampaignRoom()
	{
		AppendLog("RIPROVA STANZA - " + DescribeRoomRoll(new CampaignRoomRoll(currentRoomType, currentMonsterTier, pendingScenarioId, pendingRoomDifficulty)));
		retryComposableGolemForms = SnapshotComposableGolemForms(activeComposableGolem);
		retryComposableGolemHitPoints = activeComposableGolem?.HitPoints;
		if (retryComposableGolemForms != null)
		{
			activeComposableGolem = CreateComposableGolemForCurrentRoom();
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.GolemRetryStatePreservedLog,
				"GOLEM - {0}/{1} HP e bonus Potenza delle forme conservati per il nuovo tentativo.",
				activeComposableGolem.HitPoints,
				activeComposableGolem.MaxHitPoints));
		}
		if (!LoadCampaignRoomScenario())
		{
			currentScenarioDisplayOverride = DescribeRoomRoll(new CampaignRoomRoll(currentRoomType, currentMonsterTier, pendingScenarioId, pendingRoomDifficulty));
			AppendLog("SCENARIO - fallback nome stanza: scenario non trovato o non valido.");
		}
		PlayCurrentRoomEnterSfx();
		PrepareNextCampaignCombatDraft();
	}

	private void StartNextRoom()
	{
		if (IsCampaignComplete())
		{
			CompleteCampaign();
			return;
		}
		survivingCpuFormation.Clear();
		canRetryCampaignRoom = false;
		BeginRoomChoice();
	}

	private bool IsFinalBossRoom()
	{
		return runProgress != null
			&& currentRoomType == RoomType.Boss
			&& runProgress.RoomsCleared + 1 == configuration.Progression.FinalBossRoom;
	}

	private bool IsCampaignComplete()
	{
		return runProgress != null
			&& runProgress.RoomsCleared >= configuration.Progression.FinalBossRoom;
	}

	private void RecordCampaignBossVictory()
	{
		string bossId = CurrentCampaignBossId();
		if (string.IsNullOrWhiteSpace(bossId))
		{
			return;
		}

		AccardND.PvpUi.PvpCampaignKillTracker.RecordBossDefeat(bossId);
		RecordDefeatedBossInRun(bossId);
		pendingAdventureChapterClearTask = ClearAdventureChapterForBoss(bossId);
		AppendLog("CAMPAGNA - boss finale battuto: achievement/icona boss registrati per il profilo.");
	}

	/// <summary>
	/// Segnala al server il capitolo completato: e' il server a mappare boss -> capitolo, a
	/// segnarlo completato e a concedere il capitolo successivo. Senza server la progressione
	/// permanente non viene modificata.
	/// </summary>
	private async System.Threading.Tasks.Task ClearAdventureChapterForBoss(string bossId)
	{
		// Se il repository esiste gia', inviamo anche durante una riconnessione: la
		// mutazione persistente viene salvata nell'outbox e riprodotta automaticamente.
		if (serverProgress != null || await EnsureServerProgressAsync())
		{
			try
			{
				await serverProgress.ClearChapterAsync(bossId);
				MirrorServerProgress();
				AppendLog($"AVVENTURA - capitolo completato registrato dal server (boss {bossId}).");
				RefreshSinglePlayerProgressView();
				return;
			}
			catch (System.Exception exception)
			{
				AppendLog($"AVVENTURA - completamento capitolo rifiutato dal server: {exception.Message}");
			}
		}

		bool queued = AccardND.Network.ServerSinglePlayerProgressClient.QueueChapterClearForReplay(bossId);
		AppendLog(queued
			? GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.ChapterClearQueuedLog,
				"AVVENTURA - completamento capitolo {0} salvato: verra' registrato alla riconnessione.",
				bossId)
			: GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.ChapterClearNotRecordedLog,
				"AVVENTURA - completamento capitolo {0} non registrato: server non disponibile e account assente.",
				bossId));
	}

	private void ShowPendingClassChoiceIfAny()
	{
		if ((Object)(object)classChoicePopup == (Object)null || classChoiceSubmitting)
			return;

		List<string> choices = singlePlayerProgressService.Progress?.pendingClassChoices?
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList() ?? new List<string>();
		if (choices.Count == 0)
		{
			classChoicePopup.SetActive(false);
			return;
		}

		foreach (GameObject view in classChoiceButtonViews)
			if ((Object)(object)view != (Object)null) Object.Destroy(view);
		classChoiceButtonViews.Clear();
		classChoiceStatusText.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Adventure.ClassChoiceFinal,
			"La scelta e definitiva.");

		int count = choices.Count;
		for (int index = 0; index < count; index++)
		{
			string classId = choices[index].Trim().ToLowerInvariant();
			float width = 1f / count;
			Button button = CreateButton("Choose Class " + classId, classChoiceButtonsRoot,
				AccardND.Battlefield.MmoUiTheme.BodyFont, ClassChoiceDisplayName(classId));
			AccardND.Battlefield.MmoUiTheme.ApplyConfirmButtonStyle(button);
			SetRect((RectTransform)((Component)button).transform,
				new Vector2(index * width + 0.015f, 0.08f),
				new Vector2((index + 1) * width - 0.015f, 0.92f));
			((UnityEvent)button.onClick).AddListener((UnityAction)(() => SubmitClassChoice(classId)));
			classChoiceButtonViews.Add(((Component)button).gameObject);
		}

		classChoicePopup.SetActive(true);
		classChoicePopup.transform.SetAsLastSibling();
	}

	// Scena isolata per verificare la ricompensa di completamento del primo scenario.
	// Le opzioni sono solo in memoria: il test non modifica account o salvataggi locali.
	private void StartClassChoiceDebug()
	{
		inputLocked = true;
		singlePlayerProgressService.Progress.pendingClassChoices = new List<string>
		{
			"barbarian",
			"hunter",
			"priest"
		};

		if ((Object)(object)modeSelectionPanel != (Object)null)
			modeSelectionPanel.SetActive(false);
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
			campaignModeSelectionPanel.SetActive(false);
		SetAccountHubHudActive(false);

		ShowPendingClassChoiceIfAny();
		Image backdrop = classChoicePopup != null ? classChoicePopup.GetComponent<Image>() : null;
		if ((Object)(object)backdrop != (Object)null)
			backdrop.color = Color.black;
	}

	private async void SubmitClassChoice(string classId)
	{
		if (classChoiceSubmitting) return;
		if (debugClassChoiceScene)
		{
			foreach (GameObject view in classChoiceButtonViews)
			{
				Button debugButton = ((Object)(object)view != (Object)null) ? view.GetComponent<Button>() : null;
				if ((Object)(object)debugButton != (Object)null) debugButton.interactable = false;
			}
			classChoiceStatusText.text = GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.ClassChoiceTestComplete,
				"TEST COMPLETATO: {0} SELEZIONATO",
				ClassChoiceDisplayName(classId));
			return;
		}
		classChoiceSubmitting = true;
		foreach (GameObject view in classChoiceButtonViews)
		{
			Button button = ((Object)(object)view != (Object)null) ? view.GetComponent<Button>() : null;
			if ((Object)(object)button != (Object)null) button.interactable = false;
		}
		classChoiceStatusText.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Adventure.ClassChoiceUnlocking,
			"Sblocco in corso...");

		try
		{
			if (serverProgress == null && !await EnsureServerProgressAsync())
				throw new InvalidOperationException(GameText.GetOrFallbackSilent(
					GameTextKeys.Adventure.ClassChoiceConnectionRequired,
					"Connessione al server non disponibile."));
			await serverProgress.ChooseClassAsync(classId);
			MirrorServerProgress();
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.ClassUnlockedLog,
				"PROGRESSIONE - classe scelta e sbloccata: {0}.",
				classId));
			classChoiceSubmitting = false;
			RefreshSinglePlayerProgressView();
		}
		catch (Exception exception)
		{
			classChoiceSubmitting = false;
			classChoiceStatusText.text = GameText.GetOrFallbackSilent(
				GameTextKeys.Adventure.ClassChoiceFailed,
				"Scelta non registrata: {0}",
				exception.Message);
			foreach (GameObject view in classChoiceButtonViews)
			{
				Button button = ((Object)(object)view != (Object)null) ? view.GetComponent<Button>() : null;
				if ((Object)(object)button != (Object)null) button.interactable = true;
			}
		}
	}

	private static string ClassChoiceDisplayName(string classId)
	{
		string fallback = classId switch
		{
			"barbarian" => "BARBARO",
			"hunter" => "CACCIATORE",
			"priest" => "SACERDOTE",
			_ => classId.ToUpperInvariant()
		};
		return GameText.GetOrFallbackSilent(
			GameTextKeys.Rules.HeroClassName(classId),
			fallback).ToUpperInvariant();
	}

	private string CurrentCampaignBossId()
	{
		if (!string.IsNullOrWhiteSpace(campaignScenarioBossId))
			return campaignScenarioBossId;
		if ((Object)(object)currentScenario != (Object)null && !string.IsNullOrWhiteSpace(currentScenario.BossId))
			return currentScenario.BossId;
		if (activeBragusBoss != null)
			return BragusBossCardId;
		if (activeTrentorBoss != null)
			return TrentorBossCardId;
		if (activeMedusaBoss != null)
			return MedusaBossCardId;
		if (activePalatirBoss != null)
			return PalatirBossCardId;
		return null;
	}

	private void CompleteCampaign()
	{
		canAdvanceToNextRoom = false;
		canRetryCampaignRoom = false;
		SetTurnBanner(playerTurn: true, "CAMPAGNA COMPLETATA");
		SetMessage("CAMPAGNA COMPLETATA. Boss finale sconfitto: icona achievement sbloccata. Ritorno all'inizio tra poco.");
		pendingCampaignRewardTask = ClaimCampaignRunAccountReward(completed: true);
		((Component)restartButton).gameObject.SetActive(false);
	}

	/// <summary>
	/// Apre la run nello storico del server. Vale solo per le statistiche: la fine della run
	/// arriva con la death reward, quindi senza questo annuncio una run mollata a meta' non
	/// lascerebbe traccia e il pannello admin vedrebbe solo chi arriva in fondo.
	/// Silenziosa per costruzione: nessun messaggio al giocatore, nessuna attesa.
	/// </summary>
	private async System.Threading.Tasks.Task NotifyCampaignRunStarted(string runId)
	{
		if (IsComposableGolemDebugSession || string.IsNullOrWhiteSpace(runId))
			return;

		if (serverProgress == null && !await EnsureServerProgressAsync())
			return;

		// Stessi valori del sommario di fine run: inizio e fine devono descrivere la
		// stessa run anche quando il capitolo non c'e' (run libera).
		await serverProgress.NotifyRunStartedAsync(
			runId,
			"campaign",
			string.IsNullOrWhiteSpace(activeAdventureChapterId) ? "free-run" : activeAdventureChapterId,
			string.IsNullOrWhiteSpace(campaignScenarioId) ? "default" : campaignScenarioId);
	}

	private async System.Threading.Tasks.Task ClaimCampaignRunAccountReward(bool completed)
	{
		if (IsComposableGolemDebugSession)
		{
			pendingCampaignRewardClaimId = null;
			pendingCampaignRewardBaseAccountExperience = 0;
			pendingCampaignRewardAdClaimed = false;
			AppendLog("DEBUG GOLEM - ricompense e progressione account disabilitate.");
			ShowCampaignDefeatRewardPopup(0, completed);
			return;
		}

		if (runProgress == null)
		{
			ShowCampaignDefeatRewardPopup(0, completed);
			return;
		}

		// Il clear capitolo e la reward restituiscono entrambi uno snapshot completo.
		// Se partono insieme, una reward calcolata prima del clear puo' arrivare per ultima
		// e rimpiazzare la cache con lo stato vecchio, facendo sparire i capitoli dalla UI.
		// Serializziamo le due mutazioni server: prima progressione capitolo, poi reward.
		if (completed)
		{
			await pendingAdventureChapterClearTask;
		}

		if (string.IsNullOrWhiteSpace(campaignRunRewardId))
			campaignRunRewardId = System.Guid.NewGuid().ToString("N");

		// Solo l'EXP non spesa al mercato viene convertita in EXP account.
		// TotalExperience resta invece lo storico lordo della run e non diminuisce
		// quando il giocatore compra carte o oggetti.
		int availableExperience = Mathf.Max(0, runProgress.AvailableExperience);
		int baseAccountExperience = availableExperience / 10;
		int roomsCleared = runProgress.RoomsCleared;
		int bossesDefeated = completed ?1 : 0;
		string outcomeLabel = completed ?"vittoria" : "sconfitta";
		var summary = new AccardND.Network.DeathRewardSummary(
			campaignRunRewardId,
			"campaign",
			string.IsNullOrWhiteSpace(activeAdventureChapterId) ? "free-run" : activeAdventureChapterId,
			string.IsNullOrWhiteSpace(campaignScenarioId) ? "default" : campaignScenarioId,
			roomsCleared,
			runProgress.EnemiesDefeated,
			bossesDefeated,
			availableExperience,
			runProgress.MinibossesDefeated,
			defeatedBossIdsInRun.ToArray(),
			consumedBagItemIds.ToArray(),
			runProgress.DiceRolled,
			runProgress.AbilitiesUsed,
			runProgress.TotalExperience);

		// Non richiedere che il link sia pronto in questo esatto frame. ClaimDeathRewardAsync
		// salva prima il riepilogo nell'outbox persistente: anche se il tentativo immediato
		// fallisce, reward e contatori della taverna verranno applicati alla riconnessione.
		if (serverProgress != null || await EnsureServerProgressAsync())
		{
			try
			{
				AccardND.Network.SinglePlayerRewardOutcome outcome =
					await serverProgress.ClaimDeathRewardAsync(summary);
				MirrorServerProgress();
				pendingCampaignRewardClaimId = outcome.RewardClaimId;
				pendingCampaignRewardBaseAccountExperience = outcome.GrantedAccountExperience;
				pendingCampaignRewardAdClaimed = false;
				pendingCampaignRewardRecoverable = true;
				AppendLog($"ACCOUNT - fine campagna ({outcomeLabel}): {availableExperience} EXP disponibili /10 = +{outcome.GrantedAccountExperience} EXP account.");
				string reward = GameText.GetOrFallbackSilent(
					GameTextKeys.Campaign.AccountRewardSummary,
					"Fine campagna: +{0} EXP account ({1}/10).",
					outcome.GrantedAccountExperience,
					availableExperience);
				if (completed)
				{
					SetMessage(reward);
					ShowCampaignDefeatRewardPopup(outcome.GrantedAccountExperience, completed: true);
				}
				else
				{
					SetMessage(reward);
					ShowCampaignDefeatRewardPopup(outcome.GrantedAccountExperience, completed: false);
				}
				RefreshSinglePlayerProgressView();
			}
			catch (System.Exception exception)
			{
				AppendLog($"ACCOUNT - reward fine campagna rifiutata dal server: {exception.Message}");
				pendingCampaignRewardRecoverable = false;
				ShowCampaignDefeatRewardPopup(baseAccountExperience, completed);
			}
			return;
		}

		bool queued = AccardND.Network.ServerSinglePlayerProgressClient.QueueDeathRewardForReplay(summary);
		pendingCampaignRewardClaimId = null;
		pendingCampaignRewardBaseAccountExperience = 0;
		pendingCampaignRewardAdClaimed = false;
		// In coda vuol dire che alla riconnessione nascera' una reward col moltiplicatore
		// ancora da riscuotere: il profilo la ritrovera'. Senza coda non nascera' niente.
		pendingCampaignRewardRecoverable = queued;
		AppendLog(queued
			? GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RewardQueuedLog,
				"ACCOUNT - reward fine campagna ({0}) salvata: verra' registrata alla riconnessione.",
				outcomeLabel)
			: GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RewardNotRecordedLog,
				"ACCOUNT - reward fine campagna ({0}) non registrata: server non disponibile e account assente.",
				outcomeLabel));
		SetMessage(queued
			// La reward viaggia alla riconnessione e nasce col moltiplicatore ancora da
			// riscuotere: il x3 non e' bruciato dalla rete caduta, e' solo rimandato.
			? GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.OfflineSummarySaved,
				"Riepilogo della run salvato: EXP, statistiche e quest verranno sincronizzati alla riconnessione.")
				+ " " + GameText.GetOrFallbackSilent(
					GameTextKeys.Campaign.TripleSavedToProfile,
					"Il triplicatore ti aspetta fra i messaggi del profilo.")
			: GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RewardConnectionRequired,
				"Connessione al server necessaria per registrare la ricompensa di fine campagna."));
		ShowCampaignDefeatRewardPopup(baseAccountExperience, completed);
		RefreshSinglePlayerProgressView();
	}

	private void CreateCampaignDefeatRewardPopup(Transform parent, Font font)
	{
		Image overlay = CreateImage("Campaign Defeat Reward Popup", parent, new Color(0f, 0f, 0f, 0.78f));
		overlay.raycastTarget = true;
		Stretch(overlay.rectTransform);
		campaignDefeatRewardPopup = ((Component)overlay).gameObject;
		Canvas canvas = campaignDefeatRewardPopup.AddComponent<Canvas>();
		canvas.overrideSorting = true;
		canvas.sortingOrder = 950;
		campaignDefeatRewardPopup.AddComponent<GraphicRaycaster>();

		Image dialog = CreateImage("Campaign Defeat Reward Dialog", ((Component)overlay).transform, new Color(0.01f, 0.018f, 0.028f, 0.99f));
		dialog.raycastTarget = true;
		StylePanel(dialog);
		SetRect(dialog.rectTransform, new Vector2(0.16f, 0.27f), new Vector2(0.84f, 0.73f));

		Text title = CreateText("Campaign Defeat Reward Title", ((Component)dialog).transform, font, 50, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
		Font campaignTitleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
		if (campaignTitleFont != null)
			title.font = campaignTitleFont;
		title.fontSize = 50;
		title.resizeTextForBestFit = false;
		title.text = "FINE CAMPAGNA";
		title.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(title.rectTransform, new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.92f));

		campaignDefeatRewardBodyText = CreateText("Campaign Defeat Reward Body", ((Component)dialog).transform, font, 28, (FontStyle)0, (TextAnchor)4);
		campaignDefeatRewardBodyText.color = new Color(0.88f, 0.94f, 0.97f);
		campaignDefeatRewardBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
		campaignDefeatRewardBodyText.verticalOverflow = VerticalWrapMode.Truncate;
		campaignDefeatRewardBodyText.resizeTextForBestFit = true;
		campaignDefeatRewardBodyText.resizeTextMinSize = 20;
		campaignDefeatRewardBodyText.resizeTextMaxSize = 28;
		SetRect(campaignDefeatRewardBodyText.rectTransform, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.7f));

		Button continueButton = CreateButton("Continue After Campaign Defeat Reward", ((Component)dialog).transform, font, "CONTINUA");
		((UnityEvent)continueButton.onClick).AddListener(new UnityAction(ContinueAfterCampaignDefeatReward));
		SetRect((RectTransform)((Component)continueButton).transform, new Vector2(0.04f, 0.08f), new Vector2(0.48f, 0.29f));
		ApplyCampaignRewardContinueStyle(continueButton);

		campaignDefeatRewardDoubleButton = CreateButton("Triple Campaign Defeat Reward", ((Component)dialog).transform, font, "TRIPLICA");
		campaignDefeatRewardDoubleButtonText = ((Component)campaignDefeatRewardDoubleButton).GetComponentInChildren<Text>();
		((UnityEvent)campaignDefeatRewardDoubleButton.onClick).AddListener(new UnityAction(ClaimCampaignRewardAdMultiplier));
		SetRect((RectTransform)((Component)campaignDefeatRewardDoubleButton).transform, new Vector2(0.52f, 0.08f), new Vector2(0.96f, 0.29f));
		ApplyMerchantRoomCta(
			campaignDefeatRewardDoubleButton,
			campaignDefeatRewardDoubleButtonText,
			"UI/CampaignRestyle/campaign_cta_blue",
			preserveAspect: false);

		campaignDefeatRewardPopup.SetActive(false);
	}

	/// <summary>
	/// Chiede alla rete gli annunci che questa run puo' arrivare a mostrare. Si fa all'inizio
	/// e non alla fine perche' il TRIPLICA compare solo se l'annuncio c'e' gia': chiederlo
	/// quando la run e' finita vorrebbe dire non offrirlo mai. Una run dura molto meno della
	/// scadenza di un annuncio, quindi quello caricato adesso e' ancora buono al popup finale.
	///
	/// L'interstitial della bisaccia si chiede solo se c'e' qualcosa dentro: una run senza
	/// oggetti non lo mostrera' mai, e una richiesta che nessuno guardera' e' proprio quello
	/// che si vuole smettere di fare.
	/// </summary>
	private void WarmCampaignRunAds()
	{
		AccardND.Ads.AdService.Warm(AccardND.Ads.AdPlacement.CampaignExperienceTriple);
		if (runBagItemIds.Count > 0)
			AccardND.Ads.AdService.Warm(AccardND.Ads.AdPlacement.BagItemUsed);
	}

	/// <summary>
	/// La run e' finita: quello che e' gia' caricato resta pronto per la prossima, ma nessuno
	/// ne chiede altri finche' non ne comincia una.
	/// </summary>
	private void CoolCampaignRunAds()
	{
		AccardND.Ads.AdService.Cool(AccardND.Ads.AdPlacement.CampaignExperienceTriple);
		AccardND.Ads.AdService.Cool(AccardND.Ads.AdPlacement.BagItemUsed);
	}

	private void ShowCampaignDefeatRewardPopup(int earnedExperience, bool completed = false)
	{
		if ((Object)(object)campaignDefeatRewardPopup == (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
			return;
		}

		pendingCampaignRewardBaseAccountExperience = Mathf.Max(0, earnedExperience);
		pendingCampaignRewardAdClaimed = false;
		Text title = campaignDefeatRewardPopup.transform.Find("Campaign Defeat Reward Dialog/Campaign Defeat Reward Title")?.GetComponent<Text>();
		if ((Object)(object)title != (Object)null)
			title.text = completed ? "CAPITOLO COMPLETATO" : "FINE CAMPAGNA";
		// Senza claim id non c'e' niente da far verificare al server: il moltiplicatore
		// verrebbe rifiutato prima ancora di mostrare il video. Succede quando la reward di
		// fine campagna e' stata rifiutata o messa in coda offline, e in quei casi qui arriva
		// comunque un'EXP di base positiva: senza questo controllo il TRIPLICA compare e poi
		// il tocco non fa niente.
		bool rewardClaimable = ServerProgressReady
			&& !string.IsNullOrWhiteSpace(pendingCampaignRewardClaimId);
		// Il bottone esiste solo se c'e' davvero un annuncio pronto. Offrirlo e poi dire
		// "riprova piu' tardi" e' peggio che non offrirlo: il giocatore ha gia' chiuso la run
		// e non torna indietro a riprovare.
		bool adOffered = pendingCampaignRewardBaseAccountExperience > 0
			&& rewardClaimable
			&& AccardND.Ads.AdService.IsReady(AccardND.Ads.AdPlacement.CampaignExperienceTriple);
		// La domanda nomina la pubblicita' solo dove ce n'e' davvero una: dove il x3 e'
		// condonato, prometterne una che non partira' e' una bugia gratuita.
		string tripleQuestion = AccardND.Ads.AdService.RewardsWaivedWithoutAds
			? GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.TripleQuestion,
				"Vuoi triplicare la ricompensa?")
			: GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.TripleQuestionWithAd,
				"Vuoi triplicare la ricompensa guardando una pubblicità?");
		// Il x3 saltato non e' perso: la reward resta a moltiplicatore 1 sul server e il
		// profilo la ripropone. Dirlo qui e' l'unico modo perche' il giocatore vada a
		// cercarla, invece di credere che l'occasione sia finita con la run.
		string rewardReady = pendingCampaignRewardBaseAccountExperience > 0 && pendingCampaignRewardRecoverable
			? GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.TripleSavedToProfile,
				"Il triplicatore ti aspetta fra i messaggi del profilo.")
			: GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RewardReady,
				"La ricompensa è pronta.");
		string unlockSummary = completed ? BuildChapterCompletionUnlockSummary() : string.Empty;
		campaignDefeatRewardBodyText.text = completed
			? $"{unlockSummary}\n\n<size=40><b>+{pendingCampaignRewardBaseAccountExperience} EXP</b></size>\n\n{(adOffered ? tripleQuestion : rewardReady)}"
			: GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RewardPopupBody,
				"Hai guadagnato\n<size=40><b>+{0} EXP</b></size>\n\n{1}",
				pendingCampaignRewardBaseAccountExperience,
				adOffered ? tripleQuestion : rewardReady);
		((Component)campaignDefeatRewardDoubleButton).gameObject.SetActive(adOffered);
		campaignDefeatRewardDoubleButton.interactable = adOffered;
		Button continueButton = campaignDefeatRewardPopup.transform.Find("Campaign Defeat Reward Dialog/Continue After Campaign Defeat Reward")?.GetComponent<Button>();
		if ((Object)(object)continueButton != (Object)null)
		{
			SetRect((RectTransform)((Component)continueButton).transform,
				new Vector2(0.04f, 0.08f),
				adOffered ? new Vector2(0.48f, 0.29f) : new Vector2(0.96f, 0.29f));
		}
		// Un bottone che non compare e' indistinguibile da un bug: se manca perche' l'annuncio
		// non e' ancora arrivato, va detto, altrimenti si cerca il guasto dalla parte sbagliata.
		if (!adOffered && pendingCampaignRewardBaseAccountExperience > 0)
			AppendLog(rewardClaimable
				? "ACCOUNT - TRIPLICA non offerto: nessun annuncio pronto per il placement."
				: "ACCOUNT - TRIPLICA non offerto: la reward di fine campagna non ha un claim id dal server.");
		campaignDefeatRewardDoubleButtonText.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Campaign.Triple,
			"TRIPLICA");
		campaignDefeatRewardPopup.SetActive(true);
		campaignDefeatRewardPopup.transform.SetAsLastSibling();
	}

	private string BuildChapterCompletionUnlockSummary()
	{
		AccardND.GameData.AdventureChapter chapter =
			AccardND.GameData.AdventureChapterCatalog.Find(activeAdventureChapterId);
		if (chapter == null)
			return "NUOVE RICOMPENSE SBLOCCATE";

		var lines = new List<string>();
		if (!string.IsNullOrWhiteSpace(chapter.RewardClassId))
			lines.Add($"CLASSE  <b>{ClassChoiceDisplayName(chapter.RewardClassId)}</b>");

		AccardND.GameData.AdventureChapter nextChapter =
			AccardND.GameData.AdventureChapterCatalog.Find($"chapter-{chapter.Number + 1}");
		if (nextChapter != null && !string.IsNullOrWhiteSpace(nextChapter.ScenarioLabel))
			lines.Add($"SCENARIO  <b>{nextChapter.ScenarioLabel}</b>");

		return lines.Count > 0
			? "HAI SBLOCCATO\n" + string.Join("\n", lines)
			: "CAPITOLO COMPLETATO";
	}

	private static void ApplyCampaignRewardContinueStyle(Button button)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Text label = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)label != (Object)null)
		{
			label.font = AccardND.Battlefield.MmoUiTheme.LoreFont;
			label.fontSize = 30;
			label.fontStyle = FontStyle.Normal;
			label.resizeTextForBestFit = false;
		}

		Image image = ((Component)button).GetComponent<Image>();
		Sprite sprite = LoadSpriteResource("UI/CampaignRestyle/campaign_cta_confirm_green");
		if ((Object)(object)image != (Object)null && (Object)(object)sprite != (Object)null)
		{
			image.sprite = sprite;
			image.type = Image.Type.Simple;
			image.preserveAspect = false;
			image.color = Color.white;
			button.targetGraphic = image;
		}
	}

	private void ContinueAfterCampaignDefeatReward()
	{
		if ((Object)(object)campaignDefeatRewardPopup != (Object)null)
			campaignDefeatRewardPopup.SetActive(false);
		((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
	}

	/// <summary>
	/// Offre il x3 sulla plancia a fine campagna vinta. Restituisce se l'offerta e' davvero
	/// comparsa: quando non c'e' un annuncio pronto la riga di riepilogo deve mandare il
	/// giocatore ai messaggi del profilo, dove la stessa reward resta riscuotibile.
	/// </summary>
	private bool ShowCampaignRewardAdButton()
	{
		if ((Object)(object)merchantBuyButton == (Object)null || pendingCampaignRewardBaseAccountExperience <= 0)
			return false;
		// Stessa regola del popup: niente claim id, niente da far verificare al server, e il
		// bottone sarebbe solo un tocco a vuoto.
		if (!ServerProgressReady || string.IsNullOrWhiteSpace(pendingCampaignRewardClaimId))
		{
			AppendLog("ACCOUNT - GUARDA ADV EXP non offerto: la reward di fine campagna non ha un claim id dal server.");
			return false;
		}
		// Stessa regola del popup: niente annuncio pronto, niente proposta.
		if (!AccardND.Ads.AdService.IsReady(AccardND.Ads.AdPlacement.CampaignExperienceTriple))
			return false;

		((UnityEvent)merchantBuyButton.onClick).RemoveAllListeners();
		((UnityEvent)merchantBuyButton.onClick).AddListener(new UnityAction(ClaimCampaignRewardAdMultiplier));
		merchantBuyButtonText.text = GameText.GetOrFallbackSilent(
			GameTextKeys.Campaign.WatchAdExperience,
			"GUARDA ADV EXP");
		merchantBuyButton.interactable = true;
		((Component)merchantBuyButton).gameObject.SetActive(true);
		ConfigureActionButtonLayout(merchantVisible: true);
		return true;
	}

	/// <summary>
	/// Il popup di fine campagna e' a schermo. Serve a capire quale delle due superfici che
	/// offrono il TRIPLICA sta parlando col giocatore: quella sbagliata e' coperta, e
	/// scriverci sopra equivale a non rispondere.
	/// </summary>
	private bool CampaignRewardPopupVisible =>
		(Object)(object)campaignDefeatRewardPopup != (Object)null && campaignDefeatRewardPopup.activeSelf;

	/// <summary>
	/// Accende o spegne il bottone che il giocatore ha davvero premuto. Il TRIPLICA del popup
	/// e il MERCATO della plancia chiamano lo stesso metodo, ma solo uno dei due e' visibile:
	/// toccare sempre l'altro lascia premuto un bottone che nessuno vede cambiare.
	/// </summary>
	private void SetCampaignRewardAdButtonInteractable(bool interactable)
	{
		if (CampaignRewardPopupVisible)
		{
			if ((Object)(object)campaignDefeatRewardDoubleButton != (Object)null)
				campaignDefeatRewardDoubleButton.interactable = interactable;
			return;
		}
		if ((Object)(object)merchantBuyButton != (Object)null)
			merchantBuyButton.interactable = interactable;
	}

	/// <summary>
	/// Un esito che il giocatore deve leggere. SetMessage scrive sulla plancia, che mentre il
	/// popup e' aperto sta sotto un overlay opaco: senza ripetere il testo dentro il popup,
	/// ogni rifiuto diventa un tocco che non fa niente.
	/// </summary>
	private void ReportCampaignRewardAdOutcome(string message)
	{
		SetMessage(message);
		if (CampaignRewardPopupVisible && (Object)(object)campaignDefeatRewardBodyText != (Object)null)
			campaignDefeatRewardBodyText.text = GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.RewardPopupBody,
				"Hai guadagnato\n<size=40><b>+{0} EXP</b></size>\n\n{1}",
				pendingCampaignRewardBaseAccountExperience,
				message);
	}

	private async void ClaimCampaignRewardAdMultiplier()
	{
		if (pendingCampaignRewardAdClaimed || pendingCampaignRewardBaseAccountExperience <= 0)
			return;

		pendingCampaignRewardAdClaimed = true;
		SetCampaignRewardAdButtonInteractable(false);
		if (!ServerProgressReady || string.IsNullOrWhiteSpace(pendingCampaignRewardClaimId))
		{
			pendingCampaignRewardAdClaimed = false;
			SetCampaignRewardAdButtonInteractable(true);
			ReportCampaignRewardAdOutcome(GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.AdMultiplierConnectionRequired,
				"Connessione al server necessaria per applicare il moltiplicatore."));
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.AdMultiplierNotAppliedLog,
				"ACCOUNT - moltiplicatore ADV non applicato: {0}.",
				"server non disponibile"));
			return;
		}

		// Qui la pubblicita' non e' un cancello ma il prezzo dell'extra: l'EXP di base e' gia'
		// stata accreditata, il x3 no. Se il video viene chiuso a meta' non si chiama il
		// server, che pagherebbe un moltiplicatore che nessuno ha guardato. Dove la rete non
		// c'e' proprio, invece, il x3 e' condonato: lo decide AdService, non questo punto.
		AccardND.Ads.AdResult ad = await AccardND.Ads.AdService.ShowAsync(
			AccardND.Ads.AdPlacement.CampaignExperienceTriple,
			// Il claim viaggia fino ad AdMob e torna nella verifica lato server: e' quello
			// che permettera' al server di accreditare il x3 senza fidarsi del client.
			AccardND.Ads.AdRewardContext.ForClaim(pendingCampaignRewardClaimId));
		if (!ad.Grants)
		{
			pendingCampaignRewardAdClaimed = false;
			SetCampaignRewardAdButtonInteractable(true);
			ReportCampaignRewardAdOutcome(ad.Unavailable
				? GameText.GetOrFallbackSilent(
					GameTextKeys.Campaign.AdUnavailable,
					"Nessuna pubblicita' disponibile in questo momento: riprova piu' tardi.")
				: GameText.GetOrFallbackSilent(
					GameTextKeys.Campaign.AdWatchIncomplete,
					"Il video va guardato per intero per triplicare l'EXP."));
			AppendLog(GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.AdMultiplierNotAppliedLog,
				"ACCOUNT - moltiplicatore ADV non applicato: {0}.",
				ad.Outcome));
			return;
		}

		try
		{
			AccardND.Network.SinglePlayerRewardOutcome outcome =
				await serverProgress.ClaimAdMultiplierAsync(pendingCampaignRewardClaimId, ad.ImpressionId);
			MirrorServerProgress();
			AppendLog($"ACCOUNT - ADV fine campagna: +{outcome.GrantedAccountExperience} EXP account extra.");
			SetMessage($"+{outcome.GrantedAccountExperience} EXP account extra. Ricompensa triplicata.");
			RefreshSinglePlayerProgressView();
		}
		catch (System.Exception exception)
		{
			pendingCampaignRewardAdClaimed = false;
			SetCampaignRewardAdButtonInteractable(true);
			// Il video l'ha guardato: se il server rifiuta, il giocatore ha diritto di sapere
			// perche' non e' arrivato niente, altrimenti sembra che l'abbia guardato per nulla.
			ReportCampaignRewardAdOutcome(GameText.GetOrFallbackSilent(
				GameTextKeys.Campaign.AdMultiplierConnectionRequired,
				"Connessione al server necessaria per applicare il moltiplicatore."));
			AppendLog($"ACCOUNT - ADV fine campagna rifiutata dal server: {exception.Message}");
			return;
		}
		if (CampaignRewardPopupVisible)
		{
			int tripledExperience = pendingCampaignRewardBaseAccountExperience * 3;
			campaignDefeatRewardBodyText.text =
				$"<size=30><b>+{tripledExperience} EXP totali</b></size>\n\nLa ricompensa e stata triplicata.";
			campaignDefeatRewardDoubleButton.interactable = false;
			campaignDefeatRewardDoubleButtonText.text = "EXP TRIPLICATA";
		}
		else if ((Object)(object)merchantBuyButton != (Object)null)
		{
			((Component)merchantBuyButton).gameObject.SetActive(false);
			RestoreMerchantBuyButtonAction();
		}
	}

	private void RestoreMerchantBuyButtonAction()
	{
		if ((Object)(object)merchantBuyButton == (Object)null)
			return;

		((UnityEvent)merchantBuyButton.onClick).RemoveAllListeners();
		((UnityEvent)merchantBuyButton.onClick).AddListener(new UnityAction(OpenMerchantPanel));
		merchantBuyButtonText.text = "MERCATO";
		merchantBuyButton.interactable = true;
	}

	private void ReturnToStart()
	{
		ReturnToStart(showModeSelection: true);
	}

	private void ReturnToStart(bool showModeSelection)
	{
		AbandonActivePvpSession();
		ClearRuntimeSessionVisuals();
		((MonoBehaviour)this).StopAllCoroutines();
		ClearDraftEntranceState();
		StopMusic();
		returningToStartAfterGameOver = false;
		abilityTargetMode = AbilityTargetMode.None;
		activeAbilityUser = null;
		activeAttachmentSource = null;
		pendingAbilityUser = null;
		selectedPlayerIndex = -1;
		pendingDeploymentIndex = -1;
		currentDeploymentIndex = 0;
		currentTurnIndex = 0;
		roundNumber = 0;
		currentMonsterTier = 2;
		pendingScenarioId = null;
		pendingRoomDifficulty = RoomDifficulty.Normal;
		currentScenarioDisplayOverride = null;
		activeComposableGolem = null;
		retryComposableGolemForms = null;
		retryComposableGolemHitPoints = null;
		activeMedusaBoss = null;
		activeTrentorBoss = null;
		activeBragusBoss = null;
		campaignScenarioId = null;
		campaignScenarioBossId = null;
		activeAdventureChapterId = null;
		defeatedBossIdsInRun.Clear();
		runBagItemIds.Clear();
		consumedBagItemIds.Clear();
		CoolCampaignRunAds();
		pendingCampaignRewardClaimId = null;
		pendingCampaignRewardBaseAccountExperience = 0;
		pendingCampaignRewardAdClaimed = false;
		pendingCampaignRewardRecoverable = false;
		inputLocked = true;
		gameFinished = false;
		draftActive = false;
		deploymentDraftActive = false;
		deploymentInitiativesReady = false;
		canAdvanceToNextRoom = false;
		canRetryCampaignRoom = false;
		currentRoomType = configuration.StartingRoom.RoomType;
		campaignDeck = null;
		initialDeckBuilder = null;
		campaignConsumables.Clear();
		// La run è terminata (sconfitta/completata/abbandono): via il salvataggio.
		ClearSavedRun();
		ResetScenarioRuleState();
		selectedDraftCards.Clear();
		selectedPlayerDeploymentIndices.Clear();
		selectedCpuDeploymentCards.Clear();
		selectedPlayerDeploymentInitiatives.Clear();
		selectedCpuDeploymentInitiatives.Clear();
		deploymentOrder.Clear();
		draftCandidates.Clear();
		draftCampaignCards.Clear();
		turnOrder.Clear();
		cpuDeploymentHand.Clear();
		playerReserve.Clear();
		initialPlayerReserve.Clear();
		initialPlayerFormation.Clear();
		initialPlayerCampaignFormation.Clear();
		initialCpuFormation.Clear();
		survivingCpuFormation.Clear();
		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		DestroyPrototypeViews(draftViews);
		DestroyPrototypeViews(playerDeploymentPreviewViews);
		DestroyPrototypeViews(cpuDeploymentPreviewViews);
		DestroyPrototypeViews(deckBuilderCardViews);
		DestroyPrototypeViews(initialDraftOfferViews);
		DestroyPrototypeViews(initialDraftDeckViews);
		initialDraftOffers.Clear();
		initialDraftDeck.Clear();
		initialDraftSelectedIndices.Clear();
		initialDraftCaptainClass = null;
		DestroyPrototypeViews(merchantOwnedCardViews);
		ClearImplementationArchiveCards();
		ClearImplementationConsumables();
		CloseCardInspection();
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);
		ClearCardRowChildren(playerHandRow);
		ClearInitiativeTimeline();
		ClearRuntimeSessionVisuals();
		ClearLootRewardReveal();
		((Component)restartButton).gameObject.SetActive(false);
		((Component)confirmActionButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		((Component)merchantBuyButton).gameObject.SetActive(false);
		RestoreMerchantBuyButtonAction();
		CloseMerchantPanel();
		deckBuilderPanel.SetActive(false);
		if ((Object)(object)initialDraftPanel != (Object)null)
		{
			initialDraftPanel.SetActive(false);
		}
		if ((Object)(object)campaignModeSelectionPanel != (Object)null)
		{
			campaignModeSelectionPanel.SetActive(false);
		}
		if ((Object)(object)sanctuaryPanel != (Object)null)
		{
			sanctuaryPanel.SetActive(false);
		}
		if ((Object)(object)roomChoicePanel != (Object)null)
		{
			roomChoicePanel.SetActive(false);
		}
		combatResultRoot.SetActive(false);
		((Component)campaignZoneRect).gameObject.SetActive(false);
		ConfigureActionButtonLayout(merchantVisible: false);
		ResetRunProgress();
		RefreshInitiativeDisplay();
		SetTurnBanner(playerTurn: true, "PREPARAZIONE");
		SetMessage("Scegli una modalita' per iniziare.");
		if ((Object)(object)playerTitleText != (Object)null)
		{
			playerTitleText.text = "LA TUA FORMAZIONE";
		}
		SetBattlefieldSurfaceVisible(showModeSelection);
		if (showModeSelection)
		{
			ShowModeSelection();
		}
		ApplyResponsiveLayout();
	}

	private void ClearRuntimeSessionVisuals()
	{
		foreach (AccardND.Battlefield.Dice3DRollView diceView in ((Component)this).GetComponentsInChildren<AccardND.Battlefield.Dice3DRollView>(true))
		{
			if ((Object)(object)diceView == (Object)null)
				continue;

			GameObject viewObject = ((Component)diceView).gameObject;
			viewObject.SetActive(false);
			Object.Destroy((Object)(object)viewObject);
		}

		DestroySafeAreaChildrenNamed(
			"Medusa Gaze Group Roll",
			"Player Initiative Die 3D",
			"Opponent Initiative Die 3D",
			"Player Initiative Dice Board",
			"Opponent Initiative Dice Board");
	}

	private void DestroySafeAreaChildrenNamed(params string[] names)
	{
		if ((Object)(object)safeAreaRoot == (Object)null || names == null || names.Length == 0)
			return;

		for (int index = ((Transform)safeAreaRoot).childCount - 1; index >= 0; index--)
		{
			Transform child = ((Transform)safeAreaRoot).GetChild(index);
			if (Array.IndexOf(names, child.name) < 0)
				continue;

			((Component)child).gameObject.SetActive(false);
			Object.Destroy((Object)(object)((Component)child).gameObject);
		}
	}

	private void SetBattlefieldSurfaceVisible(bool visible)
	{
		SetCombatChromeVisible(visible);
		if ((Object)(object)topInfoBarRect != (Object)null)
			((Component)topInfoBarRect).gameObject.SetActive(false);
		if ((Object)(object)cpuTitleText != (Object)null)
			((Component)cpuTitleText).gameObject.SetActive(false);
		if ((Object)(object)roundText != (Object)null)
			((Component)roundText).gameObject.SetActive(false);
		if ((Object)(object)messagePanelRect != (Object)null)
			((Component)messagePanelRect).gameObject.SetActive(visible && !adventureScriptedTutorialActive);
		if ((Object)(object)campaignZoneRect != (Object)null)
			((Component)campaignZoneRect).gameObject.SetActive(visible && campaignDeck != null);
		if ((Object)(object)combatResultRoot != (Object)null)
			combatResultRoot.SetActive(false);
	}

	private static void DestroyCardViews(List<BattleCardState> cards)
	{
		foreach (BattleCardState card in cards)
		{
			if ((Object)(object)card.View != (Object)null)
			{
				GameObject viewObject = ((Component)card.View).gameObject;
				viewObject.SetActive(false);
				Object.Destroy((Object)(object)viewObject);
			}
		}
		cards.Clear();
	}

	private static void DestroyPrototypeViews(List<PrototypeCardView> views)
	{
		foreach (PrototypeCardView view in views)
		{
			if ((Object)(object)view != (Object)null)
			{
				GameObject viewObject = ((Component)view).gameObject;
				viewObject.SetActive(false);
				Object.Destroy((Object)(object)viewObject);
			}
		}
		views.Clear();
	}

	private void PrepareNextCampaignCombatDraft()
	{
		((MonoBehaviour)this).StopAllCoroutines();
		ClearDraftEntranceState();
		abilityTargetMode = AbilityTargetMode.None;
		attackTargetingActive = false;
		activeAbilityUser = null;
		activeAttachmentSource = null;
		selectedPlayerIndex = -1;
		inputLocked = true;
		gameFinished = false;
		canRetryCampaignRoom = false;
		((Component)restartButton).gameObject.SetActive(false);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		((Component)merchantBuyButton).gameObject.SetActive(false);
		RestoreMerchantBuyButtonAction();
		CloseMerchantPanel();
		if ((Object)(object)roomChoicePanel != (Object)null)
		{
			roomChoicePanel.SetActive(false);
		}
		foreach (BattleCardState playerCard in playerCards)
		{
			Object.Destroy((Object)(object)((Component)playerCard.View).gameObject);
		}
		foreach (BattleCardState cpuCard in cpuCards)
		{
			Object.Destroy((Object)(object)((Component)cpuCard.View).gameObject);
		}
		playerCards.Clear();
		cpuCards.Clear();
		turnOrder.Clear();
		initialPlayerFormation.Clear();
		initialPlayerCampaignFormation.Clear();
		initialCpuFormation.Clear();
		BeginFormationDraft();
	}

	private IEnumerator EnterNonCombatRoom(RoomType roomType)
	{
		inputLocked = true;
		((Component)restartButton).gameObject.SetActive(false);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)merchantBuyButton).gameObject.SetActive(false);
		CloseMerchantPanel();
		bool showCombatChrome = ShouldShowNonCombatChrome(roomType);
		SetCombatChromeVisible(showCombatChrome);
		int num = 0;
		string text = string.Empty;
		switch (roomType)
		{
		case RoomType.UnexpectedOpportunity:
		{
			(string scenarioText, int scenarioExperience) = RevealCampaignScenario();
			if (!string.IsNullOrWhiteSpace(scenarioText))
			{
				text += scenarioText;
				num += scenarioExperience;
			}
			int eventRoll = random.NextInclusive(1, 12);
			AppendLog($"EVENTO - D12 = {eventRoll}");
			if (playerCards.Count > 0)
			{
				bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
				PlayRollingDiceSfx();
				playerCards[0].View.PlayDiceRoll(diceCatalog, 12, TrackDiceRoll(eventRoll), "IMPREVISTO / OPPORTUNITA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
				yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
				RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
			}
			pendingMasterGiftReward = null;
			(string eventText, int eventExperience) = ResolveOpportunity(eventRoll);
			if (!string.IsNullOrWhiteSpace(eventText))
			{
				text += eventText;
				num += eventExperience;
			}
			if ((Object)(object)pendingMasterGiftReward != (Object)null)
			{
				CardDefinition masterGiftReward = pendingMasterGiftReward;
				pendingMasterGiftReward = null;
				PlayLootRoomEnterSfx();
				yield return PlayLootRewardReveal(new[] { masterGiftReward });
			}
			break;
		}
		case RoomType.Loot:
		{
			PlayLootRoomEnterSfx();
			int lootReserveCards = configuration.Progression.LootReserveCards;
			List<CardDefinition> campaignRewardPool = GetCampaignRewardPool();
			lootReserveCards = Mathf.Min(lootReserveCards, campaignRewardPool.Count);
			if (lootReserveCards > 0)
			{
				List<CardDefinition> list = formationDraftService.DrawCandidates(campaignRewardPool, lootReserveCards);
				List<CardDefinition> addedCards = new List<CardDefinition>();
				foreach (CardDefinition item in list)
				{
					if (TryAddCardToPlayerCollection(item))
						addedCards.Add(item);
				}
				AppendLog("LOOT - " + string.Join(", ", addedCards.Select(CardDisplayNames.MarketName)));
				yield return PlayLootRewardReveal(addedCards);
				text = " Carte ottenute: " + string.Join(", ", addedCards.Select(CardDisplayNames.MarketName)) + ".";
			}
			else
			{
				text = " Hai gia trovato tutte le carte disponibili: ottieni solo le altre ricompense.";
			}
			break;
		}
		}
		foreach (BattleCardState playerCard in playerCards)
		{
			Object.Destroy((Object)(object)((Component)playerCard.View).gameObject);
		}
		foreach (BattleCardState cpuCard in cpuCards)
		{
			Object.Destroy((Object)(object)((Component)cpuCard.View).gameObject);
		}
		playerCards.Clear();
		cpuCards.Clear();
		turnOrder.Clear();
		ClearInitiativeTimeline();
		ResizeTimelineTiles(0);
		if (roomType == RoomType.Merchant)
		{
			ResetMerchantStock();
		}
		ProgressionConfiguration progression = configuration.Progression;
		int num2 = roomType switch
		{
			RoomType.Loot => progression.LootRoomExperience, 
			RoomType.Merchant => progression.MerchantRoomExperience, 
			RoomType.UnexpectedOpportunity => progression.OpportunityRoomExperience, 
			_ => 0, 
		};
		int previousLevel = runProgress.PlayerLevel;
		int previousExperience = runProgress.CurrentExperience;
		RoomReward roomReward = runProgress.CompleteNonCombatRoom(num2 + num, ConsumeNextRoomExperienceMultiplier());
		int num3 = campaignDeck?.ReleaseCooldown() ?? 0;
		if (num3 > 0)
		{
			AppendLog($"COOLDOWN - {num3} carte tornano disponibili nella stanza non-combat.");
		}
		roundNumber = 0;
		inputLocked = true;
		gameFinished = true;
		canAdvanceToNextRoom = true;
		string text2 = roomType switch
		{
			RoomType.Loot => "STANZA RICOMPENSA: hai trovato un tesoro.", 
			RoomType.Merchant => "STANZA MERCATO: scegli il banco delle carte o quello degli oggetti, non entrambi. Puoi sempre vendere e recuperare carte.",
			RoomType.UnexpectedOpportunity => "IMPREVISTO / OPPORTUNITA:", 
			_ => "Stanza superata.", 
		};
		SetTurnBanner(playerTurn: true, roomType switch
		{
			RoomType.Loot => "TESORO  -  RICOMPENSA OTTENUTA", 
			RoomType.Merchant => "MERCATO  -  SPENDI EXP O CONTINUA",
			RoomType.UnexpectedOpportunity => "OPPORTUNITA  -  EVENTO RISOLTO", 
			_ => "STANZA COMPLETATA", 
		});
		string text3 = ((roomReward.TotalExperience > 0) ?$" +{roomReward.TotalExperience} EXP." : string.Empty);
		SetMessage(text2 + text + text3);
		if (roomReward.LevelsGained > 0)
		{
			ShowLevelUpVigorHint();
		}
		SetPrimaryActionLabel(restartButtonText, PrimaryActionLabel.Continue);
		if (roomType == RoomType.UnexpectedOpportunity)
		{
			AccardND.Battlefield.MmoUiTheme.ApplyConfirmButtonStyle(restartButton, restartButtonText);
		}
		else
		{
			ApplyBattleButtonVariant(restartButton, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Arcane);
		}
		((Component)restartButton).gameObject.SetActive(roomReward.LevelsGained == 0);
		if (roomType == RoomType.Merchant)
		{
			merchantBuyButtonText.text = "APRI MERCATO";
			ApplyMerchantRoomCta(
				restartButton,
				restartButtonText,
				"UI/CampaignRestyle/campaign_cta_blue");
			ApplyMerchantRoomCta(
				merchantBuyButton,
				merchantBuyButtonText,
				"UI/CampaignRestyle/campaign_cta_olive");
			merchantBuyButton.interactable = true;
			((Component)merchantBuyButton).gameObject.SetActive(true);
			ConfigureActionButtonLayout(merchantVisible: true);
		}
		else
		{
			ConfigureActionButtonLayout(merchantVisible: false);
		}
		RefreshInitiativeDisplay();
		PlayCampaignExperienceReward(previousLevel, previousExperience, roomReward);
		SetCombatChromeVisible(showCombatChrome);
		ApplyResponsiveLayout();
		SetCombatChromeVisible(showCombatChrome);
	}

	private static void ApplyMerchantRoomCta(Button button, Text label, string spriteResource, bool preserveAspect = true)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Image image = ((Component)button).GetComponent<Image>();
		Sprite sprite = LoadSpriteResource(spriteResource);
		if ((Object)(object)image != (Object)null && (Object)(object)sprite != (Object)null)
		{
			image.sprite = sprite;
			image.type = Image.Type.Simple;
			image.preserveAspect = preserveAspect;
			image.color = Color.white;
			button.targetGraphic = image;
		}

		ColorBlock colors = button.colors;
		colors.normalColor = Color.white;
		colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
		colors.pressedColor = new Color(0.82f, 0.86f, 0.92f, 1f);
		colors.selectedColor = Color.white;
		button.colors = colors;

		if ((Object)(object)label == (Object)null)
			return;

		label.font = AccardND.Battlefield.MmoUiTheme.LoreFont;
		label.fontStyle = FontStyle.Normal;
		label.fontSize = 30;
		label.resizeTextForBestFit = true;
		label.resizeTextMinSize = 22;
		label.resizeTextMaxSize = 30;
	}

	private void SetCombatChromeVisible(bool visible)
	{
		combatChromeVisible = visible;
		if (playerHud != null && (Object)(object)playerHud.Rect != (Object)null)
		{
			// La vecchia plancia giocatore e' stata scorporata: EXP e Vigore
			// vivono ora nei widget dedicati della HUD di combattimento.
			((Component)playerHud.Rect).gameObject.SetActive(false);
		}
		SetCombatHudRefactorVisible(visible);
		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
		{
			((Component)cpuHud.Rect).gameObject.SetActive(
				visible && !bragusBossPresentationActive && !trentorBossPresentationActive);
		}
		if ((Object)(object)playerTitleText != (Object)null)
		{
			((Component)playerTitleText).gameObject.SetActive(visible);
		}
		if ((Object)(object)timelineBackgroundRect != (Object)null)
		{
			((Component)timelineBackgroundRect).gameObject.SetActive(visible);
		}
	}

	private static bool ShouldShowNonCombatChrome(RoomType roomType)
	{
		if (roomType != RoomType.Loot && roomType != RoomType.UnexpectedOpportunity)
		{
			return roomType != RoomType.Merchant;
		}
		return false;
	}
}
}
