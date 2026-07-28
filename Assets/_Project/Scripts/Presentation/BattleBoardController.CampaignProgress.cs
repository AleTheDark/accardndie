using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
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
		if (card == null)
			return;

		card.AbilityUsed = true;
		if (runProgress != null && playerCards.Contains(card))
			runProgress.RecordAbilityUsed();
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
			RoomReward roomReward;
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
					select card.Card.Strength).Concat((num <= 0) ?((IEnumerable<int>)Array.Empty<int>()) : ((IEnumerable<int>)new int[1] { num })), ConsumeNextRoomExperienceMultiplier());
			}
			SetMessage($"Hai guadagnato {roomReward.TotalExperience} punti esperienza!");
			if (roomReward.LevelsGained > 0)
			{
				ShowLevelUpVigorHint();
			}
			restartButtonText.text = "VAI AVANTI!";
			ApplyBattleButtonVariant(restartButton, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Arcane);
			canAdvanceToNextRoom = true;
		}
		else
		{
			SetTurnBanner(playerTurn: false, "SCONFITTA  -  FORMAZIONE ELIMINATA");
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
					SetTurnBanner(playerTurn: false, "SCONFITTA - RITIRATA");
					SetMessage($"SCONFITTA. Puoi continuare: hai {combatReadyCount}/" + $"{formationSize} carte disponibili. Restano {survivingCpuFormation.Count} mostri nella stanza.");
					restartButtonText.text = "RIPROVA STANZA";
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
			ClaimCampaignRunAccountReward(completed: false);
		}
		RefreshInitiativeDisplay();
		((Component)restartButton).gameObject.SetActive(canAdvanceToNextRoom || flag);
		UpdateInteractions();
		ClearConsumedCombatRules();
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
			float returnDelay = pendingCampaignRewardBaseAccountExperience > 0 && !pendingCampaignRewardAdClaimed ?15f : 5f;
			yield return WaitForCardInspectionPause(returnDelay);
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
		if (retryComposableGolemForms != null)
		{
			activeComposableGolem = CreateComposableGolemForCurrentRoom();
			AppendLog("GOLEM - bonus Potenza delle forme conservati per il nuovo tentativo.");
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
		ClearAdventureChapterForBoss(bossId);
		AppendLog("CAMPAGNA - boss finale battuto: achievement/icona boss registrati per il profilo.");
	}

	/// <summary>
	/// Segnala al server il capitolo completato: e' il server a mappare boss -> capitolo, a
	/// segnarlo completato e a concedere il capitolo successivo. Senza server raggiungibile si
	/// ripiega sullo sblocco locale, che pero' vale solo finche' non arriva un'istantanea
	/// autoritativa: la fonte di verita' resta il server.
	/// </summary>
	private async void ClearAdventureChapterForBoss(string bossId)
	{
		if (ServerProgressReady)
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

		UnlockNextAdventureChapterForBoss(bossId);
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

	// Ripiego offline: rispecchia in cache locale quello che il server avrebbe registrato.
	private void UnlockNextAdventureChapterForBoss(string bossId)
	{
		string clearedChapterId = AdventureChapterForBoss(bossId);
		if (!string.IsNullOrWhiteSpace(clearedChapterId)
			&& !singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.ChapterCleared, clearedChapterId))
		{
			singlePlayerProgressService.Unlock(SinglePlayerUnlockType.ChapterCleared, clearedChapterId);
		}

		string nextChapterId = NextAdventureChapterForBoss(bossId);
		if (string.IsNullOrWhiteSpace(nextChapterId)
			|| singlePlayerProgressService.IsUnlocked(SinglePlayerUnlockType.Chapter, nextChapterId))
		{
			return;
		}

		singlePlayerProgressService.Unlock(SinglePlayerUnlockType.Chapter, nextChapterId);
		AppendLog($"AVVENTURA - {nextChapterId} sbloccato offline sconfiggendo {bossId}; da riconfermare col server.");
	}

	private static string AdventureChapterForBoss(string bossId)
	{
		if (string.Equals(bossId, BragusBossCardId, StringComparison.OrdinalIgnoreCase))
			return "chapter-1";
		if (string.Equals(bossId, TrentorBossCardId, StringComparison.OrdinalIgnoreCase))
			return "chapter-2";
		if (string.Equals(bossId, MedusaBossCardId, StringComparison.OrdinalIgnoreCase))
			return "chapter-3";
		if (string.Equals(bossId, PalatirBossCardId, StringComparison.OrdinalIgnoreCase))
			return "chapter-4";
		return null;
	}

	private static string NextAdventureChapterForBoss(string bossId)
	{
		if (string.Equals(bossId, BragusBossCardId, StringComparison.OrdinalIgnoreCase))
			return "chapter-2";
		if (string.Equals(bossId, TrentorBossCardId, StringComparison.OrdinalIgnoreCase))
			return "chapter-3";
		if (string.Equals(bossId, MedusaBossCardId, StringComparison.OrdinalIgnoreCase))
			return "chapter-4";
		return null;
	}

	private void CompleteCampaign()
	{
		canAdvanceToNextRoom = false;
		canRetryCampaignRoom = false;
		SetTurnBanner(playerTurn: true, "CAMPAGNA COMPLETATA");
		SetMessage("CAMPAGNA COMPLETATA. Boss finale sconfitto: icona achievement sbloccata. Ritorno all'inizio tra poco.");
		ClaimCampaignRunAccountReward(completed: true);
		((Component)restartButton).gameObject.SetActive(false);
		((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
	}

	private async void ClaimCampaignRunAccountReward(bool completed)
	{
		if (runProgress == null)
		{
			if (!completed)
				ShowCampaignDefeatRewardPopup(0);
			return;
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

		if (ServerProgressReady)
		{
			try
			{
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
				AccardND.Network.SinglePlayerRewardOutcome outcome =
					await serverProgress.ClaimDeathRewardAsync(summary);
				MirrorServerProgress();
				pendingCampaignRewardClaimId = outcome.RewardClaimId;
				pendingCampaignRewardBaseAccountExperience = outcome.GrantedAccountExperience;
				pendingCampaignRewardAdClaimed = false;
				AppendLog($"ACCOUNT - fine campagna ({outcomeLabel}): {availableExperience} EXP disponibili /10 = +{outcome.GrantedAccountExperience} EXP account.");
				SetMessage($"Fine campagna: +{outcome.GrantedAccountExperience} EXP account ({availableExperience}/10). Guarda ADV per triplicare la ricompensa.");
				if (completed)
					ShowCampaignRewardAdButton();
				else
					ShowCampaignDefeatRewardPopup(outcome.GrantedAccountExperience);
				RefreshSinglePlayerProgressView();
			}
			catch (System.Exception exception)
			{
				AppendLog($"ACCOUNT - reward fine campagna rifiutata dal server: {exception.Message}");
				if (!completed)
					ShowCampaignDefeatRewardPopup(0);
			}
			return;
		}

		int levelsGained = singlePlayerProgressService.AddAccountExperience(baseAccountExperience);
		pendingCampaignRewardClaimId = null;
		pendingCampaignRewardBaseAccountExperience = baseAccountExperience;
		pendingCampaignRewardAdClaimed = false;
		AppendLog($"ACCOUNT - fine campagna offline ({outcomeLabel}): {availableExperience} EXP disponibili /10 = +{baseAccountExperience} EXP account.");
		SetMessage($"Fine campagna: +{baseAccountExperience} EXP account ({availableExperience}/10). Guarda ADV per triplicare la ricompensa.");
		if (levelsGained > 0)
			AppendLog($"ACCOUNT - level up account: +{levelsGained} livelli.");
		if (completed)
			ShowCampaignRewardAdButton();
		else
			ShowCampaignDefeatRewardPopup(baseAccountExperience);
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

		Text title = CreateText("Campaign Defeat Reward Title", ((Component)dialog).transform, font, 31, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(title);
		Font campaignTitleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
		if (campaignTitleFont != null)
			title.font = campaignTitleFont;
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
		SetRect((RectTransform)((Component)continueButton).transform, new Vector2(0.07f, 0.09f), new Vector2(0.46f, 0.28f));

		campaignDefeatRewardDoubleButton = CreateButton("Triple Campaign Defeat Reward", ((Component)dialog).transform, font, "TRIPLICA");
		campaignDefeatRewardDoubleButtonText = ((Component)campaignDefeatRewardDoubleButton).GetComponentInChildren<Text>();
		((UnityEvent)campaignDefeatRewardDoubleButton.onClick).AddListener(new UnityAction(ClaimCampaignRewardAdMultiplier));
		SetRect((RectTransform)((Component)campaignDefeatRewardDoubleButton).transform, new Vector2(0.54f, 0.09f), new Vector2(0.93f, 0.28f));

		campaignDefeatRewardPopup.SetActive(false);
	}

	private void ShowCampaignDefeatRewardPopup(int earnedExperience)
	{
		if ((Object)(object)campaignDefeatRewardPopup == (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
			return;
		}

		pendingCampaignRewardBaseAccountExperience = Mathf.Max(0, earnedExperience);
		pendingCampaignRewardAdClaimed = false;
		campaignDefeatRewardBodyText.text =
			$"Hai guadagnato\n<size=40><b>+{pendingCampaignRewardBaseAccountExperience} EXP</b></size>\n\n" +
			"Vuoi triplicarla guardando una pubblicita?";
		campaignDefeatRewardDoubleButton.interactable = pendingCampaignRewardBaseAccountExperience > 0;
		campaignDefeatRewardDoubleButtonText.text = "TRIPLICA";
		campaignDefeatRewardPopup.SetActive(true);
		campaignDefeatRewardPopup.transform.SetAsLastSibling();
	}

	private void ContinueAfterCampaignDefeatReward()
	{
		if ((Object)(object)campaignDefeatRewardPopup != (Object)null)
			campaignDefeatRewardPopup.SetActive(false);
		((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
	}

	private void ShowCampaignRewardAdButton()
	{
		if ((Object)(object)merchantBuyButton == (Object)null || pendingCampaignRewardBaseAccountExperience <= 0)
			return;

		((UnityEvent)merchantBuyButton.onClick).RemoveAllListeners();
		((UnityEvent)merchantBuyButton.onClick).AddListener(new UnityAction(ClaimCampaignRewardAdMultiplier));
		merchantBuyButtonText.text = "GUARDA ADV EXP";
		merchantBuyButton.interactable = true;
		((Component)merchantBuyButton).gameObject.SetActive(true);
		ConfigureActionButtonLayout(merchantVisible: true);
	}

	private async void ClaimCampaignRewardAdMultiplier()
	{
		if (pendingCampaignRewardAdClaimed || pendingCampaignRewardBaseAccountExperience <= 0)
			return;

		pendingCampaignRewardAdClaimed = true;
		merchantBuyButton.interactable = false;
		if (ServerProgressReady && !string.IsNullOrWhiteSpace(pendingCampaignRewardClaimId))
		{
			try
			{
				AccardND.Network.SinglePlayerRewardOutcome outcome =
					await serverProgress.ClaimAdMultiplierAsync(pendingCampaignRewardClaimId, System.Guid.NewGuid().ToString("N"));
				MirrorServerProgress();
				AppendLog($"ACCOUNT - ADV fine campagna: +{outcome.GrantedAccountExperience} EXP account extra.");
				SetMessage($"ADV completata: +{outcome.GrantedAccountExperience} EXP account extra. Ricompensa triplicata.");
				RefreshSinglePlayerProgressView();
			}
			catch (System.Exception exception)
			{
				pendingCampaignRewardAdClaimed = false;
				merchantBuyButton.interactable = true;
				AppendLog($"ACCOUNT - ADV fine campagna rifiutata dal server: {exception.Message}");
				return;
			}
		}
		else
		{
			int extraAccountExperience = pendingCampaignRewardBaseAccountExperience * 2;
			int levelsGained = singlePlayerProgressService.AddAccountExperience(extraAccountExperience);
			AppendLog($"ACCOUNT - ADV fine campagna offline: +{extraAccountExperience} EXP account extra.");
			if (levelsGained > 0)
				AppendLog($"ACCOUNT - level up account: +{levelsGained} livelli.");
			SetMessage("ADV completata: premio account triplicato.");
			RefreshSinglePlayerProgressView();
		}
		if ((Object)(object)campaignDefeatRewardPopup != (Object)null && campaignDefeatRewardPopup.activeSelf)
		{
			int tripledExperience = pendingCampaignRewardBaseAccountExperience * 3;
			campaignDefeatRewardBodyText.text =
				$"Pubblicita placeholder completata!\n\n<size=30><b>+{tripledExperience} EXP totali</b></size>\n\nLa ricompensa e stata triplicata.";
			campaignDefeatRewardDoubleButton.interactable = false;
			campaignDefeatRewardDoubleButtonText.text = "EXP TRIPLICATA";
		}
		else
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
		activeMedusaBoss = null;
		activeTrentorBoss = null;
		activeBragusBoss = null;
		campaignScenarioId = null;
		campaignScenarioBossId = null;
		activeAdventureChapterId = null;
		defeatedBossIdsInRun.Clear();
		runBagItemIds.Clear();
		consumedBagItemIds.Clear();
		pendingCampaignRewardClaimId = null;
		pendingCampaignRewardBaseAccountExperience = 0;
		pendingCampaignRewardAdClaimed = false;
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
			(string eventText, int eventExperience) = ResolveOpportunity(eventRoll);
			if (!string.IsNullOrWhiteSpace(eventText))
			{
				text += eventText;
				num += eventExperience;
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
		string text4 = ((roomReward.LevelsGained > 0) ?$" LEVEL UP: livello {runProgress.PlayerLevel}, D{runProgress.PlayerVigorDieSides}!" : string.Empty);
		SetMessage(text2 + text + text3 + text4);
		if (roomReward.LevelsGained > 0)
		{
			ShowLevelUpVigorHint();
		}
		restartButtonText.text = "CONTINUA";
		if (roomType == RoomType.UnexpectedOpportunity)
		{
			AccardND.Battlefield.MmoUiTheme.ApplyConfirmButtonStyle(restartButton, restartButtonText);
		}
		else
		{
			ApplyBattleButtonVariant(restartButton, AccardND.Battlefield.MmoUiTheme.ButtonVariant.Arcane);
		}
		((Component)restartButton).gameObject.SetActive(true);
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
		SetCombatChromeVisible(showCombatChrome);
		ApplyResponsiveLayout();
		SetCombatChromeVisible(showCombatChrome);
	}

	private static void ApplyMerchantRoomCta(Button button, Text label, string spriteResource)
	{
		if ((Object)(object)button == (Object)null)
			return;

		Image image = ((Component)button).GetComponent<Image>();
		Sprite sprite = LoadSpriteResource(spriteResource);
		if ((Object)(object)image != (Object)null && (Object)(object)sprite != (Object)null)
		{
			image.sprite = sprite;
			image.type = Image.Type.Simple;
			image.preserveAspect = true;
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
		label.resizeTextMinSize = AccardND.Battlefield.MmoUiTheme.LoreFontMinSize;
	}

	private void SetCombatChromeVisible(bool visible)
	{
		combatChromeVisible = visible;
		if (playerHud != null && (Object)(object)playerHud.Rect != (Object)null)
		{
			((Component)playerHud.Rect).gameObject.SetActive(visible);
		}
		if (cpuHud != null && (Object)(object)cpuHud.Rect != (Object)null)
		{
			((Component)cpuHud.Rect).gameObject.SetActive(visible);
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
