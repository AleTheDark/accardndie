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
			SetTurnBanner(playerTurn: true, "VITTORIA  -  STANZA SUPERATA");
			int num = (nextCombatFallenHeroesGrantExperience ?playerCards.Where(IsCampaignDefeated).Sum((BattleCardState card) => card.Card.Strength) : 0);
			RoomReward roomReward = runProgress.CompleteMonsterRoom((from card in cpuCards
				where card.Eliminated
				select card.Card.Strength).Concat((num <= 0) ?((IEnumerable<int>)Array.Empty<int>()) : ((IEnumerable<int>)new int[1] { num })));
			string text = ((roomReward.LevelsGained > 0) ?$" LEVEL UP! Ora sei livello {runProgress.PlayerLevel}: D{runProgress.PlayerVigorDieSides}." : string.Empty);
			string text2 = ((num > 0) ?$" +{num} EXP eroi caduti." : string.Empty);
			string arg = ((num > 0) ?"EXP combattimento" : "EXP mostri");
			SetMessage($"STANZA SUPERATA! +{roomReward.RoomExperience} EXP stanza + " + $"{roomReward.DefeatedMonsterExperience} {arg} = +{roomReward.TotalExperience} EXP." + text2 + text);
			restartButtonText.text = "STANZA SUCCESSIVA";
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
		((Component)restartButton).gameObject.SetActive(flag);
		UpdateInteractions();
		ClearConsumedCombatRules();
		return true;
	}

	private void ClearConsumedCombatRules()
	{
		skipNextCombatCooldown = false;
		nextCombatFallenHeroesGrantExperience = false;
		nextCombatAssassinsActLast = false;
		nextCombatWarriorsLowerVigor = false;
		nextCombatTankDuel = false;
	}

	private void ResetScenarioRuleState()
	{
		ClearConsumedCombatRules();
		nextMonsterTierBonus = 0;
		merchantRoomsBlockedUntilMonster = false;
		rewardRoomsBlockedUntilMonster = false;
	}

	private IEnumerator ReturnToStartAfterGameOver()
	{
		if (!returningToStartAfterGameOver)
		{
			returningToStartAfterGameOver = true;
			yield return (object)new WaitForSecondsRealtime(5f);
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
		survivingCpuFormation.Clear();
		canRetryCampaignRoom = false;
		BeginRoomChoice();
	}

	private void ReturnToStart()
	{
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
		((Component)restartButton).gameObject.SetActive(false);
		((Component)confirmFormationButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		((Component)merchantBuyButton).gameObject.SetActive(false);
		CloseMerchantPanel();
		deckBuilderPanel.SetActive(false);
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
		SetCombatChromeVisible(visible: true);
		ShowModeSelection();
		ApplyResponsiveLayout();
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
			int eventRoll = random.NextInclusive(1, 12);
			AppendLog($"EVENTO - D12 = {eventRoll}");
			if (playerCards.Count > 0)
			{
				PlayRollingDiceSfx();
				playerCards[0].View.PlayDiceRoll(diceCatalog, 12, eventRoll, "IMPREVISTO / OPPORTUNITA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
				yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
			}
			(text, num) = ResolveOpportunity(eventRoll);
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
		RoomReward roomReward = runProgress.CompleteNonCombatRoom(num2 + num);
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
	}

	private void SetCombatChromeVisible(bool visible)
	{
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
