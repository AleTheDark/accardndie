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
			if (IsFinalBossRoom())
				RecordCampaignBossVictory();
			SetTurnBanner(playerTurn: true, "VITTORIA  -  STANZA SUPERATA");
			RoomReward roomReward;
			if (activeComposableGolem != null)
			{
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
			canAdvanceToNextRoom = true;
		}
		else
		{
			SetTurnBanner(playerTurn: false, "SCONFITTA  -  FORMAZIONE ELIMINATA");
			SetMessage("SCONFITTA. La CPU ha eliminato la tua formazione.");
			canAdvanceToNextRoom = false;
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
			SetMessage("GAME OVER. La CPU ha eliminato la tua formazione. Ritorno all'inizio tra 5 secondi.");
			((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
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
			yield return WaitForCardInspectionPause(5f);
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
		AccardND.PvpUi.PvpCampaignKillTracker.RecordBossDefeat(MedusaBossCardId);
		AppendLog("CAMPAGNA - boss finale battuto: achievement/icona boss registrati per il profilo.");
	}

	private void CompleteCampaign()
	{
		canAdvanceToNextRoom = false;
		canRetryCampaignRoom = false;
		SetTurnBanner(playerTurn: true, "CAMPAGNA COMPLETATA");
		SetMessage("CAMPAGNA COMPLETATA. Boss finale sconfitto: icona achievement sbloccata. Ritorno all'inizio tra 5 secondi.");
		((Component)restartButton).gameObject.SetActive(false);
		((MonoBehaviour)this).StartCoroutine(ReturnToStartAfterGameOver());
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
		((Component)restartButton).gameObject.SetActive(false);
		((Component)confirmActionButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		((Component)merchantBuyButton).gameObject.SetActive(false);
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
		playerTitleText.text = "LA TUA FORMAZIONE";
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
			((Component)messagePanelRect).gameObject.SetActive(visible);
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
				playerCards[0].View.PlayDiceRoll(diceCatalog, 12, eventRoll, "IMPREVISTO / OPPORTUNITA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
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
		if (roomType == RoomType.Merchant && IsGodMerchantRoom())
		{
			text = GrantGodMerchantWelcomeGift();
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
			RoomType.Merchant => "STANZA MERCATO: compra carte casuali, per classe o per valore. Puoi recuperare le carte dal cimitero prima di venderle.", 
			RoomType.UnexpectedOpportunity => "IMPREVISTO / OPPORTUNITA:", 
			_ => "Stanza superata.", 
		};
		SetTurnBanner(playerTurn: true, roomType switch
		{
			RoomType.Loot => "TESORO  -  RICOMPENSA OTTENUTA", 
			RoomType.Merchant => "MERCANTE  -  SPENDI EXP O CONTINUA", 
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
		((Component)restartButton).gameObject.SetActive(true);
		if (roomType == RoomType.Merchant)
		{
			bool godMerchant = IsGodMerchantRoom();
			merchantBuyButtonText.text = "APRI MERCATO";
			merchantBuyButton.interactable = GetMerchantRewardPool(godMerchant).Count > 0 || GetMerchantDeckCards().Count > 0 || GetMerchantGraveyardCards().Count > 0;
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
