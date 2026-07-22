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
	private void StartBattle()
	{
		SetCombatChromeVisible(visible: true);
		ShowCombatHint();
		inputLocked = true;
		abilityTargetMode = AbilityTargetMode.None;
		attackTargetingActive = false;
		activeAbilityUser = null;
		activeAttachmentSource = null;
		selectedPlayerIndex = -1;
		turnOrder.Clear();
		turnOrder.AddRange(playerCards.Where(IsTimelineParticipant));
		turnOrder.AddRange(cpuCards.Where(IsTimelineParticipant));
		foreach (BattleCardState bragus in cpuCards.Where(IsBragusBossProxy))
		{
			bragus.Initiative = 0;
			bragus.View.SetInitiative(0);
		}
		SetActiveTurnAura(null);
		playerAura = DetermineAura(playerCards);
		cpuAura = ((currentRoomType != RoomType.Monster || currentMonsterTier > 1) ?DetermineAura(cpuCards) : BattleAuraType.None);
		necromancerSpiritUsed = false;
		ApplyPlayerAuraVisuals(appendLog: true);
		ApplyCpuAuraVisuals(appendLog: true);
		roundNumber = 1;
		currentTurnIndex = 0;
		gameFinished = false;
		if (deploymentInitiativesReady)
		{
			HashSet<int> hashSet = new HashSet<int>();
			foreach (BattleCardState item in turnOrder)
			{
				ApplyOneShotCombatRules(item);
				if (IsComposableGolemProxy(item))
				{
					item.Initiative = activeComposableGolem.RollInitiative(configuration.Gameplay.InitiativeDieSides);
					hashSet.Add(item.Initiative);
				}
				else if (nextCombatAssassinsActLast && item.Card.HeroClass == HeroClass.Assassin)
				{
					item.Initiative = AssignUniqueLastInitiative(hashSet);
				}
				else
				{
					hashSet.Add(item.Initiative);
				}
				item.TieBreaker = random.NextInclusive(1, 10000);
				item.View.SetInitiative(item.Initiative);
			}
			turnOrder.Sort(delegate(BattleCardState left, BattleCardState right)
			{
				int num = right.Initiative.CompareTo(left.Initiative);
				return (num == 0) ?right.TieBreaker.CompareTo(left.TieBreaker) : num;
			});
			deploymentInitiativesReady = false;
			SetMessage("Schieramento completato: in combattimento agiscono prima le iniziative piu alte." + AuraStartMessage());
			RefreshInitiativeDisplay();
			BeginCurrentTurn();
		}
		else
		{
			SetTurnBanner(playerTurn: true, "INIZIATIVE  -  TUTTI I D20 IN LANCIO");
			SetMessage("Tiro di iniziativa: ogni carta lancia un D20." + AuraStartMessage());
			UpdateInteractions();
			((MonoBehaviour)this).StartCoroutine(RollInitiatives());
		}
	}

	private IEnumerator RollInitiatives()
	{
		yield return WaitForHintToClose();
		HashSet<int> usedInitiatives = new HashSet<int>();
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		if (turnOrder.Count > 0)
		{
			PlayRollingDiceSfx();
		}
		foreach (BattleCardState item in turnOrder)
		{
			int initiativeDieSides = configuration.Gameplay.InitiativeDieSides;
			ApplyOneShotCombatRules(item);
			if (IsComposableGolemProxy(item))
			{
				item.Initiative = activeComposableGolem.RollInitiative(initiativeDieSides);
			}
			else if (nextCombatAssassinsActLast && item.Card.HeroClass == HeroClass.Assassin)
			{
				item.Initiative = AssignUniqueLastInitiative(usedInitiatives);
			}
			else
			{
				item.Initiative = RollUniqueInitiative(initiativeDieSides, usedInitiatives);
			}
			item.TieBreaker = random.NextInclusive(1, 10000);
			string text = (item.BelongsToPlayer ?"TU" : "CPU");
			AppendLog($"INIZIATIVA {text} - {item.Card.Name}: D{initiativeDieSides} = {item.Initiative}");
			item.View.PlayDiceRoll(diceCatalog, initiativeDieSides, item.Initiative, "INIZIATIVA " + text, configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		}
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		foreach (BattleCardState item2 in turnOrder)
		{
			item2.View.SetInitiative(item2.Initiative);
		}
		turnOrder.Sort(delegate(BattleCardState left, BattleCardState right)
		{
			int num = right.Initiative.CompareTo(left.Initiative);
			return (num == 0) ?right.TieBreaker.CompareTo(left.TieBreaker) : num;
		});
		RefreshInitiativeDisplay();
		BeginCurrentTurn();
	}

	private bool IsTimelineParticipant(BattleCardState card)
	{
		return card != null && !IsBragusBossProxy(card);
	}

	private void ApplyOneShotCombatRules(BattleCardState card)
	{
		if (card != null)
		{
			if (nextCombatWarriorsLowerVigor && card.Card.HeroClass == HeroClass.Warrior)
			{
				card.PendingVigorStepPenalty = Math.Max(card.PendingVigorStepPenalty, 1);
				RefreshPersistentStatus(card);
			}
			if (nextCombatTankDuel && card.Card.HeroClass == HeroClass.Paladin)
			{
				card.PermanentCombatBonus += (card.BelongsToPlayer ?2 : (-1));
				RefreshPersistentStatus(card);
			}
		}
	}

	private float CombatRollPresentationDuration(VigorRollResult attackerRoll, VigorRollResult defenderRoll)
	{
		float rollDuration = configuration.Animation.DiceRollDuration;
		float resultHold = configuration.Animation.DiceResultHold;
		return Mathf.Max(
			PrototypeCardView.VigorRollPresentationDuration(attackerRoll, rollDuration, resultHold),
			PrototypeCardView.VigorRollPresentationDuration(defenderRoll, rollDuration, resultHold));
	}

	private void BeginCurrentTurn()
	{
		if (IsHintBlockingGame())
		{
			((MonoBehaviour)this).StartCoroutine(BeginCurrentTurnAfterHint());
			return;
		}
		if (CheckEndGame())
		{
			return;
		}
		if (TryAutoWinCampaignWhenCpuIsLocked())
		{
			return;
		}
		while (ShouldSkipCurrentRoundTurn(turnOrder[currentTurnIndex]))
		{
			AdvanceTurnIndex();
		}
		BattleCardState battleCardState = turnOrder[currentTurnIndex];
		if (battleCardState.InhibitedTurns > 0)
		{
			battleCardState.InhibitedTurns--;
			RefreshPersistentStatus(battleCardState);
			SetMessage(battleCardState.Card.Name + " e inibito e salta il turno.");
			FinishTurn();
			return;
		}
		if (battleCardState.Petrified)
		{
			((MonoBehaviour)this).StartCoroutine(ResolvePetrifiedTurnStart(battleCardState));
			return;
		}
		SetActiveTurnAura(battleCardState);
		RefreshInitiativeDisplay();
		if (battleCardState.BelongsToPlayer)
		{
			pendingAbilityUser = null;
			attackTargetingActive = false;
			activeAttachmentSource = null;
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			SetTurnBanner(playerTurn: true, "IL TUO TURNO");
			inputLocked = false;
			selectedPlayerIndex = playerCards.IndexOf(battleCardState);
			battleCardState.View.SetSelected(selected: true);
			ClearTargetHints();
			SetMessage("Scegli un'azione sopra la pedina o ispeziona una carta.");
			RefreshAbilityButton(battleCardState);
			RefreshAttachmentButton(battleCardState);
			UpdateInteractions();
			NotifyAdventureTutorial(AdventureTutorialAction.PlayerTurnStarted);
		}
		else
		{
			SetTurnBanner(playerTurn: false, "TURNO CPU  -  " + battleCardState.Card.Name.ToUpperInvariant());
			inputLocked = true;
			selectedPlayerIndex = -1;
			attackTargetingActive = false;
			((Component)abilityButton).gameObject.SetActive(false);
			((Component)attachmentButton).gameObject.SetActive(false);
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			ClearTargetHints();
			SetMessage("Turno CPU: " + battleCardState.Card.Name + " sta scegliendo un bersaglio...");
			UpdateInteractions();
			((MonoBehaviour)this).StartCoroutine(ExecuteCpuTurn(battleCardState));
		}
	}

	private IEnumerator BeginCurrentTurnAfterHint()
	{
		yield return WaitForHintToClose();
		BeginCurrentTurn();
	}

	private void SetActiveTurnAura(BattleCardState activeCard)
	{
		foreach (BattleCardState playerCard in playerCards)
		{
			playerCard.View.SetTurnAura(playerCard == activeCard && !playerCard.Eliminated, playerOwned: true);
		}
		foreach (BattleCardState cpuCard in cpuCards)
		{
			cpuCard.View.SetTurnAura(cpuCard == activeCard && !cpuCard.Eliminated, playerOwned: false);
		}
	}

	private IEnumerator ExecutePlayerTurn(int cpuTargetIndex)
	{
		inputLocked = true;
		attackTargetingActive = false;
		pendingAbilityUser = null;
		((Component)confirmActionButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		ClearTargetHints();
		UpdateInteractions();
		BattleCardState attacker = turnOrder[currentTurnIndex];
		BattleCardState defender = cpuCards[cpuTargetIndex];
		if (IsComposableGolemProxy(defender))
		{
			yield return ExecutePlayerTurnAgainstComposableGolem(attacker, defender);
			yield break;
		}
		if (IsMedusaBossProxy(defender))
		{
			yield return ExecutePlayerTurnAgainstMedusa(attacker, defender);
			yield break;
		}
		if (IsTrentorBossProxy(defender))
		{
			yield return ExecutePlayerTurnAgainstTrentor(attacker, defender);
			yield break;
		}
		if (IsBragusBossProxy(defender))
		{
			yield return ExecutePlayerTurnAgainstBragus(attacker, defender);
			yield break;
		}
		if (IsPalatirBossProxy(defender))
		{
			yield return ExecutePlayerTurnAgainstPalatir(attacker, defender);
			yield break;
		}
		BattleCardState protectingPaladin = cpuCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage("PALADINO CPU: " + protectingPaladin.Card.Name + " devia su di se l'attacco diretto a " + defender.Card.Name + ".");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			defender = protectingPaladin;
			protectingPaladin.AbilityArmed = false;
			protectingPaladin.AbilityUsed = true;
			protectingPaladin.ProtectedAlly = null;
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO CPU: " + selfProtectingPaladin.Card.Name + " si difende con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			selfProtectingPaladin.AbilityUsed = true;
			selfProtectingPaladin.ProtectedAlly = null;
			RefreshPersistentStatus(selfProtectingPaladin);
		}
		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.MasterVigorDieSides);
		BattleCardState battleCardState = protectingPaladin ?? selfProtectingPaladin;
		CombatModifiers modifiers = BuildAttackModifiers(attacker, defender, battleCardState != null, battleCardState != null);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, defender) > 0;
		CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		if (certainty != CombatCertainty.Impossible && (Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(attacker.View, defender.View, AttackTargetLineColor);
		if (certainty == CombatCertainty.Impossible)
		{
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: false);
			yield return ShowAutomaticOutcome(guaranteedKill: false);
			AppendLog(FormatImpossibleAttackDetailed(attacker, defender, attackerDieSides, defenderDieSides, modifiers) + " Turno saltato.");
			SetBattlefieldMessage("Attacco impossibile: turno saltato.");
			selectedPlayerIndex = -1;
			attacker.View.SetSelected(selected: false);
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, defender);
		if (certainty == CombatCertainty.Guaranteed)
		{
			ConsumeArmedAttackAbility(attacker, modifiers);
			((Component)abilityButton).gameObject.SetActive(false);
			((Component)attachmentButton).gameObject.SetActive(false);
			yield return ShowAutomaticOutcome(guaranteedKill: true);
			PlayResolvedAttackSfx(attacker, hit: true, modifiers.SumAttackerVigor);
			yield return PlayHunterRangedAttackIfNeeded(attacker, defender, 6, modifiers.SumAttackerVigor);
			if (hunterMarkUsed)
				ConsumeHunterMarks(defender);
			defender.Eliminated = true;
			ApplyMightAuraDeathBonuses(defender);
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: true);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(defender, attacker.Card.HeroClass);
			yield return ReturnDuelSurvivors(attacker, defender);
			SetMessage("100%: " + attacker.Card.Name + " elimina direttamente " + defender.Card.Name + ". Nessun dado necessario.");
			selectedPlayerIndex = -1;
			attacker.View.SetSelected(selected: false);
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, "DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, defender);
		if (hunterMarkUsed)
			ConsumeHunterMarks(defender);
		PlayResolvedAttackSfx(attacker, result.DefenderIsDefeated, modifiers.SumAttackerVigor);
		if (result.DefenderIsDefeated)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, defender, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
			defender.Eliminated = true;
			ApplyMightAuraDeathBonuses(defender);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(defender, attacker.Card.HeroClass);
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, defender);
		}
		yield return ReturnDuelSurvivors(attacker, defender);
		string combatLog = FormatResultDetailed("TU", attacker, defender, result, modifiers);
		ConsumeVigorPenalties(attacker, defender);
		UpdatePostAttackClassState(attacker, result.DefenderIsDefeated);
		UpdatePostDefenseClassState(defender, result.DefenderIsDefeated);
		AppendLog(combatLog);
		SetBattlefieldMessage(FormatResultSummary(attacker, defender, result));
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecuteCpuTurn(BattleCardState attacker)
	{
		yield return WaitForHintToClose();
		yield return WaitForCardInspectionPause(configuration.Animation.CpuThinkDelay);
		yield return WaitForHintToClose();
		if (IsComposableGolemProxy(attacker))
		{
			yield return ExecuteComposableGolemTurn(attacker);
			yield break;
		}
		if (IsMedusaBossProxy(attacker))
		{
			yield return ExecuteMedusaBossTurn(attacker);
			yield break;
		}
		if (IsTrentorBossProxy(attacker))
		{
			yield return ExecuteTrentorBossTurn(attacker);
			yield break;
		}
		if (IsBragusBossProxy(attacker))
		{
			yield return ExecuteBragusBossTurn(attacker);
			yield break;
		}
		if (IsPalatirBossProxy(attacker))
		{
			yield return ExecutePalatirBossTurn(attacker);
			yield break;
		}
		if (CanCpuUseAdvancedActions(attacker) && TryChooseCpuAttachment(attacker, out var target))
		{
			yield return ExecuteCpuAttachment(attacker, target);
			yield break;
		}
		if (CanCpuUseAdvancedActions(attacker) && TryUseCpuClassAbility(attacker, out var message))
		{
			SetMessage(message);
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		}
		string decisionReason;
		int index = ChooseCpuTarget(attacker, out decisionReason);
		BattleCardState defender = playerCards[index];
		AppendLog("CPU: " + attacker.Card.Name + " sceglie " + defender.Card.Name + " - " + decisionReason + ".");
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		BattleCardState protectingPaladin = playerCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage("PALADINO: " + protectingPaladin.Card.Name + " devia su di se l'attacco diretto a " + defender.Card.Name + ".");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			defender = protectingPaladin;
			protectingPaladin.AbilityArmed = false;
			protectingPaladin.AbilityUsed = true;
			protectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO: " + selfProtectingPaladin.Card.Name + " si difende con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			selfProtectingPaladin.AbilityUsed = true;
			selfProtectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(selfProtectingPaladin);
		}
		BattleCardState paladinProtectionUser = protectingPaladin ?? selfProtectingPaladin;
		int attackerDieSides = EffectiveVigorDieSides(attacker, runProgress.MasterVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, defender, paladinProtectionUser != null, paladinProtectionUser != null);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, defender) > 0;
		CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		if (certainty != CombatCertainty.Impossible && (Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(attacker.View, defender.View, AttackTargetLineColor);
		if (certainty == CombatCertainty.Impossible)
		{
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: false);
			yield return ShowAutomaticOutcome(guaranteedKill: false);
			AppendLog(FormatImpossibleAttackDetailed(attacker, defender, attackerDieSides, defenderDieSides, modifiers) + " La CPU salta il turno.");
			SetBattlefieldMessage("Attacco CPU impossibile: turno saltato.");
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, defender);
		if (certainty == CombatCertainty.Guaranteed)
		{
			ConsumeArmedAttackAbility(attacker, modifiers);
			yield return ShowAutomaticOutcome(guaranteedKill: true);
			PlayResolvedAttackSfx(attacker, hit: true, modifiers.SumAttackerVigor);
			yield return PlayHunterRangedAttackIfNeeded(attacker, defender, 6, modifiers.SumAttackerVigor);
			if (hunterMarkUsed)
				ConsumeHunterMarks(defender);
			defender.Eliminated = true;
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: true);
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, attacker.Card.HeroClass);
			}
			yield return ReturnDuelSurvivors(attacker, defender);
			SetMessage("100%: " + attacker.Card.Name + " elimina direttamente " + defender.Card.Name + ". Nessun dado necessario.");
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		ConsumeArmedAttackAbility(attacker, modifiers);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO CPU", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, defender);
		if (hunterMarkUsed)
			ConsumeHunterMarks(defender);
		PlayResolvedAttackSfx(attacker, result.DefenderIsDefeated, modifiers.SumAttackerVigor);
		if (result.DefenderIsDefeated)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, defender, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
			defender.Eliminated = true;
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, attacker.Card.HeroClass);
			}
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, defender);
		}
		yield return ReturnDuelSurvivors(attacker, defender);
		string combatLog = FormatResultDetailed("CPU", attacker, defender, result, modifiers);
		ConsumeVigorPenalties(attacker, defender);
		UpdatePostAttackClassState(attacker, result.DefenderIsDefeated);
		UpdatePostDefenseClassState(defender, result.DefenderIsDefeated);
		AppendLog(combatLog);
		if (playerAura == BattleAuraType.Paladin && paladinProtectionUser != null && !paladinProtectionUser.Eliminated && !attacker.Eliminated)
		{
			yield return ExecutePaladinCounter(paladinProtectionUser, attacker);
		}
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstComposableGolem(BattleCardState attacker, BattleCardState golemProxy)
	{
		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, golemProxy, defenderAdvantage: false, neutralizeAttackerMatchup: true);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, golemProxy) > 0;
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, golemProxy);
		VigorRollResult attackerRoll = RollGolemAttackerVigor(attackerDieSides, modifiers);
		int attackerTotal = attacker.Card.Strength + attackerRoll.SelectedRoll + modifiers.AttackerFlatBonus;
		ComposableGolemDefenseResult golemResult = activeComposableGolem.DefendAgainst(attackerTotal);
		VigorRollResult golemRoll = SingleRoll(golemResult.Form.VigorDieSides, golemResult.VigorRoll);
		CombatResult result = new CombatResult(attackerRoll, golemRoll, attackerTotal, golemResult.DefenseTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, attackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		golemProxy.View.PlayVigorRoll(diceCatalog, golemResult.Form.VigorDieSides, golemRoll, "DIFESA " + GolemFormName(golemResult.Form.Form), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(attackerRoll, golemRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, golemProxy);
		yield return golemProxy.View.PlayComposableGolemDefenseEffect(golemResult.Form.Form, golemResult.Damage <= 0);
		if (hunterMarkUsed)
			ConsumeHunterMarks(golemProxy);
		PlayResolvedAttackSfx(attacker, golemResult.Damage > 0, modifiers.SumAttackerVigor);
		if (golemResult.Damage > 0)
		{
			yield return PlayHunterRangedAttackIfNeeded(
				attacker,
				golemProxy,
				result.AttackerTotal - result.DefenderTotal,
				attackerRoll.SelectionMode == VigorSelectionMode.Sum,
				() => golemProxy.View.PlayComposableGolemHitEffect(golemResult.Form.Form));
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, golemProxy);
		}
		UpdateComposableGolemHealthBar(golemProxy);
		if (activeComposableGolem.IsDefeated)
		{
			golemProxy.Eliminated = true;
			ApplyMightAuraDeathBonuses(golemProxy);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(golemProxy, attacker.Card.HeroClass);
		}
		else
		{
			RefreshPersistentStatus(golemProxy);
		}
		yield return ReturnDuelSurvivors(attacker, golemProxy);
		ConsumeVigorPenalties(attacker, golemProxy);
		UpdatePostAttackClassState(attacker, activeComposableGolem.IsDefeated);
		string text = golemResult.Damage > 0
			?$"{attacker.Card.Name} infligge {golemResult.Damage} danni al Golem Componibile. HP {golemResult.HitPointsAfter}/{activeComposableGolem.MaxHitPoints}."
			:golemResult.Healing > 0
				?$"VETRO - il Golem non viene superato e si cura di {golemResult.Healing}. HP {golemResult.HitPointsAfter}/{activeComposableGolem.MaxHitPoints}."
				:$"{attacker.Card.Name} non supera la difesa del Golem. HP {golemResult.HitPointsAfter}/{activeComposableGolem.MaxHitPoints}.";
		SetMessage(text);
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstMedusa(BattleCardState attacker, BattleCardState medusaProxy)
	{
		if (activeMedusaBoss == null)
		{
			FinishTurn();
			yield break;
		}

		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(medusaProxy, runProgress.MasterVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, medusaProxy, defenderAdvantage: false, neutralizeAttackerMatchup: false);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, medusaProxy) > 0;
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, medusaProxy);

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, medusaProxy.Card, attackerDieSides, defenderDieSides, modifiers);
		MedusaDefenseResult medusaResult = activeMedusaBoss.ApplyResolvedDefense(
			result.AttackerTotal,
			result.DefenderRoll.SelectedRoll,
			result.DefenderTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		medusaProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, "DIFESA MEDUSA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, medusaProxy);

		if (hunterMarkUsed)
			ConsumeHunterMarks(medusaProxy);
		PlayResolvedAttackSfx(attacker, medusaResult.Damage > 0, modifiers.SumAttackerVigor);
		if (medusaResult.Damage > 0)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, medusaProxy, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, medusaProxy);
		}

		UpdateMedusaBossHealthBar(medusaProxy);
		if (activeMedusaBoss.IsDefeated)
		{
			medusaProxy.Eliminated = true;
			ApplyMightAuraDeathBonuses(medusaProxy);
			PlayMedusaDeathSfx();
			yield return PlayTimelineAwareDefeatAnimation(medusaProxy, attacker.Card.HeroClass);
		}
		else
		{
			RefreshPersistentStatus(medusaProxy);
		}
		yield return ReturnDuelSurvivors(attacker, medusaProxy);
		ConsumeVigorPenalties(attacker, medusaProxy);
		UpdatePostAttackClassState(attacker, activeMedusaBoss.IsDefeated);
		SetMessage(medusaResult.Damage > 0
			?$"{attacker.Card.Name} infligge {medusaResult.Damage} danni a Medusa. HP {medusaResult.HitPointsAfter}/{activeMedusaBoss.MaxHitPoints}."
			:$"{attacker.Card.Name} non supera la difesa di Medusa. HP {medusaResult.HitPointsAfter}/{activeMedusaBoss.MaxHitPoints}.");
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstTrentor(BattleCardState attacker, BattleCardState trentorProxy)
	{
		if (activeTrentorBoss == null)
		{
			FinishTurn();
			yield break;
		}

		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(trentorProxy, runProgress.MasterVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, trentorProxy, defenderAdvantage: false, neutralizeAttackerMatchup: false);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, trentorProxy) > 0;
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, trentorProxy);

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, trentorProxy.Card, attackerDieSides, defenderDieSides, modifiers);
		TrentorDefenseResult trentorResult = activeTrentorBoss.ApplyResolvedDefense(
			result.AttackerTotal,
			result.DefenderRoll.SelectedRoll,
			result.DefenderTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		trentorProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, "DIFESA TRENTOR", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, trentorProxy);

		if (hunterMarkUsed)
			ConsumeHunterMarks(trentorProxy);
		PlayResolvedAttackSfx(attacker, trentorResult.Damage > 0, modifiers.SumAttackerVigor);
		if (trentorResult.Damage > 0)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, trentorProxy, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
			trentorProxy.MarkedTarget = attacker;
			if ((Object)(object)battleAnimationPlayer != (Object)null)
				yield return battleAnimationPlayer.PlayHunterMarkReticle(attacker.View);
			RefreshPersistentStatus(attacker);
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, trentorProxy);
		}

		UpdateTrentorBossHealthBar(trentorProxy);
		if (activeTrentorBoss.IsDefeated)
		{
			trentorProxy.Eliminated = true;
			ApplyMightAuraDeathBonuses(trentorProxy);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(trentorProxy, attacker.Card.HeroClass);
		}
		else
		{
			RefreshPersistentStatus(trentorProxy);
		}
		yield return ReturnDuelSurvivors(attacker, trentorProxy);
		ConsumeVigorPenalties(attacker, trentorProxy);
		UpdatePostAttackClassState(attacker, activeTrentorBoss.IsDefeated);
		string reactiveRoots = trentorResult.Damage > 0 ? $" Radici Reattive: {attacker.Card.Name} viene marcato." : string.Empty;
		SetMessage(trentorResult.Damage > 0
			?$"{attacker.Card.Name} infligge {trentorResult.Damage} danni a Trentor. HP {trentorResult.HitPointsAfter}/{activeTrentorBoss.MaxHitPoints}.{reactiveRoots}"
			:$"{attacker.Card.Name} non supera la corteccia di Trentor. HP {trentorResult.HitPointsAfter}/{activeTrentorBoss.MaxHitPoints}.");
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstBragus(BattleCardState attacker, BattleCardState bragusProxy)
	{
		if (activeBragusBoss == null)
		{
			FinishTurn();
			yield break;
		}

		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(bragusProxy, runProgress.MasterVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, bragusProxy, defenderAdvantage: false, neutralizeAttackerMatchup: false);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, bragusProxy) > 0;
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, bragusProxy);

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, bragusProxy.Card, attackerDieSides, defenderDieSides, modifiers);
		int attackerDefenseDieSides = EffectiveDefenseVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		BattleCardState counterProtectingPaladin = playerCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == attacker) && card != attacker);
		BattleCardState counterSelfProtectingPaladin = ((attacker.Card.HeroClass == HeroClass.Paladin && attacker.AbilityArmed && (attacker.ProtectedAlly == null || attacker.ProtectedAlly == attacker)) ?attacker : null);
		BattleCardState counterPaladinProtectionUser = counterProtectingPaladin ?? counterSelfProtectingPaladin;
		BragusDefenseResult bragusResult = activeBragusBoss.ApplyResolvedDefense(
			result.AttackerTotal,
			result.DefenderRoll.SelectedRoll,
			result.DefenderTotal,
			attacker.Card,
			DisplayStrength(attacker),
			attackerDefenseDieSides,
			counterPaladinProtectionUser != null);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		bragusProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, "DIFESA BRAGUS", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, bragusProxy);

		if (hunterMarkUsed)
			ConsumeHunterMarks(bragusProxy);
		PlayResolvedAttackSfx(attacker, bragusResult.Damage > 0, modifiers.SumAttackerVigor);
		if (bragusResult.Damage > 0)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, bragusProxy, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, bragusProxy);
		}

		UpdateBragusBossHealthBar(bragusProxy);
		if (bragusResult.Damage > 0)
		{
			PlayBragusTakeDamageSfx();
		}
		if (activeBragusBoss.IsDefeated)
		{
			bragusProxy.Eliminated = true;
			ApplyMightAuraDeathBonuses(bragusProxy);
			PlayBragusDeathSfx();
			yield return PlayTimelineAwareDefeatAnimation(bragusProxy, attacker.Card.HeroClass);
		}
		else
		{
			RefreshPersistentStatus(bragusProxy);
		}

		if (!activeBragusBoss.IsDefeated && bragusResult.Counterattacks)
		{
			if (counterPaladinProtectionUser != null)
			{
				counterPaladinProtectionUser.AbilityArmed = false;
				counterPaladinProtectionUser.AbilityUsed = true;
				counterPaladinProtectionUser.ProtectedAlly = null;
				TriggerMagicAuraAfterAbility();
				RefreshPersistentStatus(counterPaladinProtectionUser);
				AppendLog("PALADINO - " + counterPaladinProtectionUser.Card.Name + " attiva la difesa contro il contrattacco di Bragus: vantaggio al tiro difesa.");
			}
			VigorRollResult counterRoll = SingleRoll(BragusBoss.DefaultVigorDieSides, bragusResult.CounterRoll);
			int attackerDefenseRollValue = Math.Max(1, bragusResult.TargetDefenseTotal - DisplayStrength(attacker));
			VigorRollResult attackerDefenseRoll = SingleRoll(attackerDefenseDieSides, attackerDefenseRollValue);
			CombatResult counterResult = new CombatResult(counterRoll, attackerDefenseRoll, bragusResult.CounterTotal, bragusResult.TargetDefenseTotal);
			PlayBragusAttackSfx();
			messagePanelWasHidden = HideMessagePanelForDiceRoll();
			PlayRollingDiceSfx();
			bragusProxy.View.PlayVigorRoll(diceCatalog, BragusBoss.DefaultVigorDieSides, counterRoll, "CONTRATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
			attacker.View.PlayVigorRoll(diceCatalog, attackerDefenseDieSides, attackerDefenseRoll, "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
			yield return WaitForCardInspectionPause(CombatRollPresentationDuration(counterResult.AttackerRoll, counterResult.DefenderRoll));
			RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
			yield return ShowCombatResult(counterResult, bragusProxy, attacker);
			if ((Object)(object)battleAnimationPlayer != (Object)null)
				yield return battleAnimationPlayer.PlayBragusCleaverCounterattack(bragusProxy.View, attacker.View, bragusResult.CounterDefeatsAttacker);
			if (bragusResult.CounterDefeatsAttacker)
			{
				PlayBragusAttackHitSfx();
				attacker.Eliminated = true;
				if (!TryCreateNecromancerSpirit(attacker))
				{
					ApplyMightAuraDeathBonuses(attacker);
					PlayDeathCardSfx();
					yield return PlayTimelineAwareDefeatAnimation(attacker, bragusProxy.Card.HeroClass);
				}
			}
		}

		yield return ReturnDuelSurvivors(attacker, bragusProxy);
		ConsumeVigorPenalties(attacker, bragusProxy);
		UpdatePostAttackClassState(attacker, activeBragusBoss.IsDefeated);
		string counterText = bragusResult.Counterattacks
			?(bragusResult.CounterDefeatsAttacker ? $" Contrattacco: {attacker.Card.Name} viene abbattuto." : $" Contrattacco: {attacker.Card.Name} resiste.")
			:string.Empty;
		SetMessage(bragusResult.Damage > 0
			?$"{attacker.Card.Name} infligge {bragusResult.Damage} danni a Bragus. HP {bragusResult.HitPointsAfter}/{activeBragusBoss.MaxHitPoints}."
			:$"{attacker.Card.Name} non supera Bragus. HP {bragusResult.HitPointsAfter}/{activeBragusBoss.MaxHitPoints}.{counterText}");
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstPalatir(BattleCardState attacker, BattleCardState palatirProxy)
	{
		if (activePalatirBoss == null)
		{
			FinishTurn();
			yield break;
		}

		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(palatirProxy, runProgress.MasterVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, palatirProxy, defenderAdvantage: activePalatirBoss.HasActiveShields, neutralizeAttackerMatchup: true);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, palatirProxy) > 0;
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, palatirProxy);

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, palatirProxy.Card, attackerDieSides, defenderDieSides, modifiers);
		PalatirDefenseResult palatirResult = activePalatirBoss.ApplyResolvedDefense(
			attacker.Card,
			result.AttackerTotal,
			result.DefenderRoll.SelectedRoll,
			result.DefenderTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		palatirProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, activePalatirBoss.HasActiveShields ? "DIFESA SCUDI" : "DIFESA PALATIR", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, palatirProxy);

		if (hunterMarkUsed)
			ConsumeHunterMarks(palatirProxy);
		PlayResolvedAttackSfx(attacker, palatirResult.ShieldWasBroken || palatirResult.Damage > 0, modifiers.SumAttackerVigor);
		if (palatirResult.ShieldWasBroken)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, palatirProxy, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
			palatirProxy.View.SetPalatirShields(activePalatirBoss.ActiveShields);
			yield return palatirProxy.View.PlayPalatirShieldBreakEffect(palatirResult.TargetedShield.Value);
		}
		else if (palatirResult.Damage > 0)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, palatirProxy, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, palatirProxy);
			if (palatirResult.TargetedShield.HasValue)
				yield return palatirProxy.View.PlayPalatirShieldBlockEffect(palatirResult.TargetedShield.Value);
		}

		UpdatePalatirBossHealthBar(palatirProxy);
		if (activePalatirBoss.IsDefeated)
		{
			palatirProxy.Eliminated = true;
			ApplyMightAuraDeathBonuses(palatirProxy);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(palatirProxy, attacker.Card.HeroClass);
		}
		else
		{
			RefreshPalatirBossPawn(palatirProxy);
		}
		yield return ReturnDuelSurvivors(attacker, palatirProxy);
		ConsumeVigorPenalties(attacker, palatirProxy);
		UpdatePostAttackClassState(attacker, activePalatirBoss.IsDefeated);
		SetMessage(FormatPalatirDefenseMessage(attacker, palatirResult));
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecuteComposableGolemTurn(BattleCardState golemProxy)
	{
		List<BattleCardState> availableTargets = playerCards.Where((BattleCardState card) => card != null && !card.Eliminated).ToList();
		if (availableTargets.Count == 0)
		{
			FinishTurn();
			yield break;
		}
		int targetIndex = ComposableGolem.SelectHighestStrengthTarget(
			availableTargets.Select((BattleCardState card) => card.Card).ToList(),
			availableTargets.Select((BattleCardState card) => card.Initiative).ToList());
		BattleCardState defender = availableTargets[targetIndex];
		BattleCardState originalTarget = defender;
		SetMessage("GOLEM COMPONIBILE: " + GolemFormName(activeComposableGolem.ActiveForm.Form) + " colpisce la carta piu alta: " + defender.Card.Name + ".");
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		BattleCardState protectingPaladin = playerCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage("PALADINO: " + protectingPaladin.Card.Name + " devia su di se l'attacco del Golem diretto a " + defender.Card.Name + ".");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			defender = protectingPaladin;
			protectingPaladin.AbilityArmed = false;
			protectingPaladin.AbilityUsed = true;
			protectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO: " + selfProtectingPaladin.Card.Name + " si difende dal Golem con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			selfProtectingPaladin.AbilityUsed = true;
			selfProtectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(selfProtectingPaladin);
		}
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		ComposableGolemAttackResult golemResult = activeComposableGolem.Attack(defender.Card, defenderDieSides);
		VigorRollResult golemRoll = SingleRoll(golemResult.Form.VigorDieSides, golemResult.VigorRoll);
		VigorRollResult defenderRoll = SingleRoll(defenderDieSides, golemResult.TargetVigorRoll);
		CombatResult result = new CombatResult(golemRoll, defenderRoll, golemResult.AttackTotal, golemResult.TargetDefenseTotal);
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(golemProxy.View, defender.View, AttackTargetLineColor);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		golemProxy.View.PlayVigorRoll(diceCatalog, golemResult.Form.VigorDieSides, golemRoll, "ATTACCO " + GolemFormName(golemResult.Form.Form), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, defenderRoll, "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, golemProxy, defender);
		PlayComposableGolemAttackSfx(golemResult.Form.Form);
		yield return golemProxy.View.PlayComposableGolemAttackEffect(defender.View, golemResult.Form.Form, golemResult.TargetIsDefeated);
		if (golemResult.TargetIsDefeated)
		{
			defender.Eliminated = true;
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, golemProxy.Card.HeroClass);
			}
		}
		string protectionText = defender != originalTarget ?$" {defender.Card.Name} ha protetto {originalTarget.Card.Name}." : string.Empty;
		SetMessage(golemResult.TargetIsDefeated
			?$"GOLEM {GolemFormName(golemResult.Form.Form)}: {defender.Card.Name} viene travolto." + protectionText
			:$"GOLEM {GolemFormName(golemResult.Form.Form)}: {defender.Card.Name} resiste." + protectionText);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecuteMedusaBossTurn(BattleCardState medusaProxy)
	{
		if (activeMedusaBoss == null)
		{
			FinishTurn();
			yield break;
		}

		List<BattleCardState> targets = playerCards.Where((BattleCardState card) => card != null && !card.Eliminated).ToList();
		if (targets.Count == 0)
		{
			FinishTurn();
			yield break;
		}

		SetMessage("MEDUSA: Sguardo Pietrificante contro tutto il gruppo.");
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);

		List<int> targetDice = targets
			.Select((BattleCardState card) => EffectiveDefenseVigorDieSides(card, runProgress.PlayerVigorDieSides))
			.ToList();
		MedusaPetrifyingGazeResult gaze = activeMedusaBoss.PetrifyingGaze(
			targets.Select((BattleCardState card) => card.Card).ToList(),
			targetDice,
			EffectiveVigorDieSides(medusaProxy, runProgress.MasterVigorDieSides));

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		yield return PlayMedusaGazeGroupRoll(gaze, targets, targetDice);
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);

		if (gaze.PetrifiesTargets)
		{
			PlayMedusaPetrifyingGazeSfx();
			if ((Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)medusaProxy.View != (Object)null)
			{
				List<PrototypeCardView> targetViews = targets
					.Where((BattleCardState target) => (Object)(object)target.View != (Object)null)
					.Select((BattleCardState target) => target.View)
					.ToList();
				yield return battleAnimationPlayer.PlayMedusaPetrifyingGaze(medusaProxy.View, targetViews, gaze.MedusaTotal - gaze.AlliesTotal);
			}
			foreach (BattleCardState target in targets)
			{
				target.Petrified = true;
				RefreshPersistentStatus(target);
			}
			AppendLog($"MEDUSA - {FormatMedusaGazeRolls(gaze)} = {gaze.MedusaTotal} contro party {FormatMedusaAllyRolls(targets, gaze, targetDice)} = {gaze.AlliesTotal}: tutte le pedine vive sono pietrificate.");
			SetMessage($"SGUARDO PIETRIFICANTE: Medusa supera il gruppo ({gaze.MedusaTotal} > {gaze.AlliesTotal}). Tutte le pedine vive sono pietrificate.");
		}
		else
		{
			AppendLog($"MEDUSA - {FormatMedusaGazeRolls(gaze)} = {gaze.MedusaTotal} contro party {FormatMedusaAllyRolls(targets, gaze, targetDice)} = {gaze.AlliesTotal}: il gruppo resiste.");
			SetMessage($"Il gruppo resiste allo sguardo di Medusa ({gaze.AlliesTotal} >= {gaze.MedusaTotal}).");
		}

		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecuteTrentorBossTurn(BattleCardState trentorProxy)
	{
		if (activeTrentorBoss == null)
		{
			FinishTurn();
			yield break;
		}

		List<BattleCardState> availableTargets = playerCards.Where((BattleCardState card) => card != null && !card.Eliminated).ToList();
		if (availableTargets.Count == 0)
		{
			FinishTurn();
			yield break;
		}

		BattleCardState markedTarget = trentorProxy.MarkedTarget != null && !trentorProxy.MarkedTarget.Eliminated
			? trentorProxy.MarkedTarget
			: null;
		if (markedTarget == null)
		{
			markedTarget = availableTargets
				.Where((BattleCardState target) => !IsHunterMarked(target))
				.OrderByDescending(DisplayStrength)
				.ThenByDescending((BattleCardState target) => target.Initiative)
				.FirstOrDefault()
				?? availableTargets.OrderByDescending(DisplayStrength).First();
			trentorProxy.MarkedTarget = markedTarget;
			SetMessage($"TRENTOR: Marchio dei Rami su {markedTarget.Card.Name}.");
			PlayClassAbilitySfx(HeroClass.Hunter);
			if ((Object)(object)battleAnimationPlayer != (Object)null)
				yield return battleAnimationPlayer.PlayHunterMarkReticle(markedTarget.View);
			RefreshPersistentStatus(markedTarget);
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		}

		BattleCardState defender = markedTarget;
		BattleCardState originalTarget = defender;
		SetMessage("TRENTOR: i rampicanti convergono su " + defender.Card.Name + ".");
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		BattleCardState protectingPaladin = playerCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage("PALADINO: " + protectingPaladin.Card.Name + " devia su di se i rampicanti diretti a " + defender.Card.Name + ".");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			defender = protectingPaladin;
			protectingPaladin.AbilityArmed = false;
			protectingPaladin.AbilityUsed = true;
			protectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO: " + selfProtectingPaladin.Card.Name + " si difende dai rampicanti con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			selfProtectingPaladin.AbilityUsed = true;
			selfProtectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(selfProtectingPaladin);
		}

		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		bool markedTargetBonus = defender == markedTarget;
		TrentorAttackResult trentorResult = activeTrentorBoss.Attack(defender.Card, defenderDieSides, markedTargetBonus);
		VigorRollResult trentorRoll = SingleRoll(TrentorBoss.DefaultVigorDieSides, trentorResult.VigorRoll);
		VigorRollResult defenderRoll = SingleRoll(defenderDieSides, trentorResult.TargetVigorRoll);
		CombatResult result = new CombatResult(trentorRoll, defenderRoll, trentorResult.AttackTotal, trentorResult.TargetDefenseTotal);
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(trentorProxy.View, defender.View, new Color(0.22f, 0.92f, 0.24f, 1f));
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		trentorProxy.View.PlayVigorRoll(diceCatalog, TrentorBoss.DefaultVigorDieSides, trentorRoll, trentorResult.MarkedTargetBonus ? "ATTACCO MARCATO" : "ATTACCO TRENTOR", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, defenderRoll, "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, trentorProxy, defender);

		PlayTrentorAttackSfx();
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTrentorVineAttack(trentorProxy.View, defender.View, trentorResult.TargetIsDefeated, trentorResult.RootsApplied);
		if (trentorResult.RootsApplied && !trentorResult.TargetIsDefeated && !defender.Eliminated)
		{
			defender.PendingVigorStepPenalty = Math.Max(defender.PendingVigorStepPenalty, TrentorBoss.RootsVigorPenaltySteps);
			RefreshPersistentStatus(defender);
		}
		if (trentorResult.TargetIsDefeated)
		{
			defender.Eliminated = true;
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, trentorProxy.Card.HeroClass);
			}
		}
		string protectionText = defender != originalTarget ?$" {defender.Card.Name} ha protetto {originalTarget.Card.Name}." : string.Empty;
		string rootsText = trentorResult.RootsApplied && !trentorResult.TargetIsDefeated ? " Rampicanti Avvolgenti: prossimo Vigore difensivo -1 step." : string.Empty;
		string markText = trentorResult.MarkedTargetBonus ? $" Predatore Rampicante: +{TrentorBoss.MarkedTargetAttackBonus} sul bersaglio marcato." : string.Empty;
		SetMessage(trentorResult.TargetIsDefeated
			?$"TRENTOR: {defender.Card.Name} viene strangolato dai rampicanti." + protectionText + markText
			:$"TRENTOR: {defender.Card.Name} resiste alla morsa." + protectionText + markText + rootsText);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecuteBragusBossTurn(BattleCardState bragusProxy)
	{
		SetMessage("BRAGUS: resta in guardia. Non attacca: aspetta il prossimo colpo per contrattaccare.");
		AppendLog("BRAGUS - passa il turno: il boss attacca solo in contrattacco.");
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		FinishTurn();
	}

	private IEnumerator ExecutePalatirBossTurn(BattleCardState palatirProxy)
	{
		if (activePalatirBoss == null)
		{
			FinishTurn();
			yield break;
		}

		List<BattleCardState> availableTargets = playerCards.Where((BattleCardState card) => card != null && !card.Eliminated).ToList();
		if (availableTargets.Count == 0)
		{
			FinishTurn();
			yield break;
		}

		int targetIndex = PalatirBoss.SelectCosmicTarget(
			availableTargets.Select((BattleCardState card) => card.Card).ToList(),
			availableTargets.Select((BattleCardState card) => card.Initiative).ToList());
		BattleCardState defender = availableTargets[targetIndex];
		BattleCardState originalTarget = defender;
		SetMessage("PALATIR: una cometa astrale punta " + defender.Card.Name + ".");
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);

		BattleCardState protectingPaladin = playerCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage("PALADINO: " + protectingPaladin.Card.Name + " devia su di se la cometa diretta a " + defender.Card.Name + ".");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			defender = protectingPaladin;
			protectingPaladin.AbilityArmed = false;
			protectingPaladin.AbilityUsed = true;
			protectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO: " + selfProtectingPaladin.Card.Name + " si difende dalla cometa con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			selfProtectingPaladin.AbilityUsed = true;
			selfProtectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(selfProtectingPaladin);
		}

		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		PalatirAttackResult palatirResult = activePalatirBoss.Attack(defender.Card, defenderDieSides);
		VigorRollResult palatirRoll = SingleRoll(PalatirBoss.DefaultVigorDieSides, palatirResult.VigorRoll);
		VigorRollResult defenderRoll = SingleRoll(defenderDieSides, palatirResult.TargetVigorRoll);
		CombatResult result = new CombatResult(palatirRoll, defenderRoll, palatirResult.AttackTotal, palatirResult.TargetDefenseTotal);
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(palatirProxy.View, defender.View, new Color(0.58f, 0.2f, 1f, 1f));
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		palatirProxy.View.PlayVigorRoll(diceCatalog, PalatirBoss.DefaultVigorDieSides, palatirRoll, "ATTACCO COSMICO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, defenderRoll, "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, palatirProxy, defender);
		PlayPalatirCosmicAttackSfx();
		yield return palatirProxy.View.PlayPalatirCosmicAttackEffect(defender.View, palatirResult.TargetIsDefeated);
		PlayAttackResultSfx(palatirProxy, palatirResult.TargetIsDefeated);
		if (palatirResult.TargetIsDefeated)
		{
			defender.Eliminated = true;
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, palatirProxy.Card.HeroClass);
			}
		}
		string protectionText = defender != originalTarget ?$" {defender.Card.Name} ha protetto {originalTarget.Card.Name}." : string.Empty;
		SetMessage(palatirResult.TargetIsDefeated
			?$"PALATIR: {defender.Card.Name} viene dissolto dalla cometa." + protectionText
			:$"PALATIR: {defender.Card.Name} resiste alla cometa." + protectionText);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ResolvePetrifiedTurnStart(BattleCardState card)
	{
		inputLocked = true;
		SetActiveTurnAura(card);
		RefreshInitiativeDisplay();
		SetTurnBanner(card.BelongsToPlayer, "PIETRIFICATO  -  " + card.Card.Name.ToUpperInvariant());
		int dieSides = EffectiveDefenseVigorDieSides(card, card.BelongsToPlayer ?runProgress.PlayerVigorDieSides : runProgress.MasterVigorDieSides);
		MedusaUnpetrifyResult result = activeMedusaBoss != null
			?activeMedusaBoss.RollUnpetrify(dieSides)
			:new MedusaUnpetrifyResult(random.NextInclusive(1, dieSides), MedusaBoss.UnpetrifyRequiredRoll(dieSides));

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		card.View.PlayVigorRoll(diceCatalog, dieSides, SingleRoll(dieSides, result.Roll), "SPIETRIFICA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);

		if (result.Freed)
		{
			yield return card.View.PlayPetrifiedOverlayCrumble();
			card.Petrified = false;
			RefreshPersistentStatus(card);
			AppendLog($"PIETRA - {card.Card.Name} tira {result.Roll} su D{dieSides}: supera {result.RequiredRoll} e si libera.");
			SetMessage($"{card.Card.Name} si libera dalla pietra.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			BeginCurrentTurn();
			yield break;
		}

		card.Petrified = false;
		card.Eliminated = true;
		ApplyMightAuraDeathBonuses(card);
		RefreshPersistentStatus(card);
		AppendLog($"PIETRA - {card.Card.Name} tira {result.Roll} su D{dieSides}: non supera {result.RequiredRoll}, fallisce e muore.");
		SetMessage($"{card.Card.Name} non riesce a spietrificarsi e muore.");
		PlayDeathCardSfx();
		yield return PlayTimelineAwareDefeatAnimation(card, HeroClass.Mage);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private static string FormatMedusaGazeRolls(MedusaPetrifyingGazeResult gaze)
	{
		if (gaze.MedusaRolls.Count == 0)
			return "nessun tiro";

		List<string> parts = new List<string>(gaze.MedusaRolls.Count);
		foreach (VigorRollResult roll in gaze.MedusaRolls)
		{
			if (!roll.HasSecondRoll)
			{
				parts.Add(roll.SelectedRoll.ToString());
				continue;
			}

			string selector = roll.SelectionMode == VigorSelectionMode.Highest ? "max" : "min";
			parts.Add($"{selector}({roll.FirstRoll},{roll.SecondRoll})={roll.SelectedRoll}");
		}
		return string.Join("+", parts);
	}

	private static string FormatMedusaAllyRolls(
		IReadOnlyList<BattleCardState> targets,
		MedusaPetrifyingGazeResult gaze,
		IReadOnlyList<int> targetDice)
	{
		List<string> parts = new List<string>();
		int count = Math.Min(targets?.Count ?? 0, Math.Min(gaze.TargetRolls.Count, targetDice?.Count ?? 0));
		for (int index = 0; index < count; index++)
		{
			string name = targets[index]?.Card.Name ?? "Alleato";
			parts.Add($"{name} D{targetDice[index]}={gaze.TargetRolls[index]}");
		}
		return parts.Count > 0 ?string.Join(" + ", parts) : "nessun tiro";
	}

	private IEnumerator PlayMedusaGazeGroupRoll(
		MedusaPetrifyingGazeResult gaze,
		IReadOnlyList<BattleCardState> targets,
		IReadOnlyList<int> targetDice)
	{
		RectTransform root = CreateMedusaGazeRollRoot();
		List<AccardND.Battlefield.Dice3DRollView> diceViews = new List<AccardND.Battlefield.Dice3DRollView>();
		List<Action> rollStarters = new List<Action>();
		int count = Math.Min(targets?.Count ?? 0, Math.Min(gaze.MedusaRolls.Count, Math.Min(gaze.TargetRolls.Count, targetDice?.Count ?? 0)));
		for (int index = 0; index < count; index++)
		{
			CreateMedusaGazeDie(root, diceViews, rollStarters, "MEDUSA", gaze.MedusaRolls[index].DieSides, HeroClass.Mage, gaze.MedusaRolls[index].SelectedRoll, new Color(0.86f, 0.22f, 0.22f));
			CreateMedusaGazeDie(root, diceViews, rollStarters, targets[index]?.Card.Name ?? "ALLEATO", targetDice[index], targets[index]?.Card.HeroClass ?? HeroClass.Warrior, gaze.TargetRolls[index], new Color(0.22f, 0.62f, 0.95f));
		}

		Canvas.ForceUpdateCanvases();
		foreach (Action startRoll in rollStarters)
			startRoll();
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		if ((Object)(object)root != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)root).gameObject);
		}
	}

	private RectTransform CreateMedusaGazeRollRoot()
	{
		GameObject rootObject = new GameObject("Medusa Gaze Group Roll", typeof(RectTransform), typeof(HorizontalLayoutGroup));
		rootObject.transform.SetParent((Transform)(object)safeAreaRoot, false);
		RectTransform root = (RectTransform)rootObject.transform;
		root.anchorMin = new Vector2(0.08f, 0.38f);
		root.anchorMax = new Vector2(0.92f, 0.62f);
		root.offsetMin = Vector2.zero;
		root.offsetMax = Vector2.zero;

		HorizontalLayoutGroup layout = rootObject.GetComponent<HorizontalLayoutGroup>();
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.spacing = 10f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = true;
		return root;
	}

	private void CreateMedusaGazeDie(
		RectTransform root,
		List<AccardND.Battlefield.Dice3DRollView> diceViews,
		List<Action> rollStarters,
		string label,
		int dieSides,
		HeroClass heroClass,
		int result,
		Color glow)
	{
		GameObject slotObject = new GameObject("Medusa Gaze Die", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
		slotObject.transform.SetParent((Transform)(object)root, false);
		LayoutElement layoutElement = slotObject.GetComponent<LayoutElement>();
		layoutElement.preferredWidth = 96f;
		layoutElement.flexibleWidth = 1f;
		layoutElement.flexibleHeight = 1f;
		VerticalLayoutGroup layout = slotObject.GetComponent<VerticalLayoutGroup>();
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.spacing = 2f;
		layout.childControlWidth = true;
		layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		Text labelText = CreateText("Medusa Gaze Label", slotObject.transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
		labelText.text = label.ToUpperInvariant();
		labelText.color = new Color(1f, 0.92f, 0.58f);
		labelText.resizeTextForBestFit = true;
		labelText.resizeTextMinSize = 9;
		labelText.resizeTextMaxSize = 15;
		LayoutElement labelLayout = ((Component)labelText).gameObject.AddComponent<LayoutElement>();
		labelLayout.preferredHeight = 24f;

		GameObject dieObject = new GameObject("Medusa Gaze Die Area", typeof(RectTransform), typeof(LayoutElement));
		dieObject.transform.SetParent(slotObject.transform, false);
		LayoutElement dieLayout = dieObject.GetComponent<LayoutElement>();
		dieLayout.preferredHeight = 82f;
		dieLayout.flexibleHeight = 1f;
		AccardND.Battlefield.Dice3DRollView dieView = AccardND.Battlefield.Dice3DRollView.Create(dieObject.transform);
		rollStarters.Add(() =>
		{
			dieView.StartScriptedRoll(dieSides, heroClass, result, configuration.Animation.DiceRollDuration);
			dieView.OverrideGlow(glow, $"medusa-gaze-{dieSides}-{glow}");
		});
		diceViews.Add(dieView);

		Text resultText = CreateText("Medusa Gaze Result", slotObject.transform, AccardND.Battlefield.MmoUiTheme.BodyFont, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
		resultText.text = $"D{dieSides}: {result}";
		resultText.color = Color.white;
		LayoutElement resultLayout = ((Component)resultText).gameObject.AddComponent<LayoutElement>();
		resultLayout.preferredHeight = 24f;
	}

	private VigorRollResult RollGolemAttackerVigor(int dieSides, CombatModifiers modifiers)
	{
		if (modifiers.SumAttackerVigor)
		{
			int first = random.NextInclusive(1, dieSides);
			int second = random.NextInclusive(1, dieSides);
			return new VigorRollResult(dieSides, first, second, hasSecondRoll: true, first + second, MatchupResult.Neutral, VigorSelectionMode.Sum);
		}
		return SingleRoll(dieSides, random.NextInclusive(1, dieSides));
	}

	private static VigorRollResult SingleRoll(int dieSides, int roll)
	{
		return new VigorRollResult(dieSides, roll, 0, hasSecondRoll: false, roll, MatchupResult.Neutral, VigorSelectionMode.Single);
	}

	private IEnumerator MoveDuelToCenter(BattleCardState attacker, BattleCardState defender)
	{
		SetMessagePanelHiddenForDuel(hidden: true);
		Vector3 worldPosition = DuelWorldPoint(attacker, attacker: true);
		Vector3 worldPosition2 = DuelWorldPoint(defender, attacker: false);
		if ((Object)(object)battleAnimationPlayer != (Object)null)
		{
			yield return battleAnimationPlayer.MoveToDuelPoints(attacker.View, defender.View, worldPosition, worldPosition2);
		}
		else
		{
			((MonoBehaviour)this).StartCoroutine(attacker.View.MoveToDuelPoint(worldPosition, 0.34f, 1.16f));
			((MonoBehaviour)this).StartCoroutine(defender.View.MoveToDuelPoint(worldPosition2, 0.34f, 1.16f));
			yield return WaitForCardInspectionPause(0.37f);
		}
	}

	private bool UsesHunterRangedAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Hunter;
	}

	private bool UsesMageArcaneAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Mage;
	}

	private bool UsesAssassinShadowAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Assassin;
	}

	private bool UsesBarbarianAxeSmash(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Barbarian;
	}

	private bool UsesWarriorSwordAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Warrior;
	}

	private bool UsesPaladinShieldAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Paladin;
	}

	private bool UsesPriestSacredAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Priest;
	}

	private bool UsesRogueDaggerAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Rogue;
	}

	private bool UsesNecromancerSoulAttack(BattleCardState attacker)
	{
		return attacker != null
			&& attacker.Card != null
			&& attacker.Card.HeroClass == HeroClass.Necromancer;
	}

	private bool UsesStationaryClassAttack(BattleCardState attacker)
	{
		return UsesHunterRangedAttack(attacker) || UsesMageArcaneAttack(attacker) || UsesAssassinShadowAttack(attacker) || UsesBarbarianAxeSmash(attacker) || UsesWarriorSwordAttack(attacker) || UsesPaladinShieldAttack(attacker) || UsesPriestSacredAttack(attacker) || UsesRogueDaggerAttack(attacker) || UsesNecromancerSoulAttack(attacker);
	}

	private IEnumerator PlayHunterRangedAttackIfNeeded(BattleCardState attacker, BattleCardState defender, int attackMargin = 1, bool abilityAttack = false, Action onHit = null)
	{
		if (!UsesStationaryClassAttack(attacker)
			|| attacker.View == null
			|| defender == null
			|| defender.View == null)
			yield break;

		if ((Object)(object)battleAnimationPlayer != (Object)null)
		{
			if (UsesAssassinShadowAttack(attacker))
			{
				yield return battleAnimationPlayer.PlayAssassinShadowStrike(attacker.View, defender.View);
				onHit?.Invoke();
			}
			else if (UsesBarbarianAxeSmash(attacker))
			{
				yield return battleAnimationPlayer.PlayBarbarianAxeSmash(attacker.View, defender.View);
				onHit?.Invoke();
			}
			else if (UsesWarriorSwordAttack(attacker))
			{
				yield return battleAnimationPlayer.PlayWarriorSwordRush(attacker.View, defender.View, abilityAttack);
				onHit?.Invoke();
			}
			else if (UsesPaladinShieldAttack(attacker))
			{
				yield return battleAnimationPlayer.PlayPaladinDivineShieldBash(attacker.View, defender.View);
				onHit?.Invoke();
			}
			else if (UsesMageArcaneAttack(attacker))
			{
				yield return battleAnimationPlayer.PlayMageArcaneBoltAttack(attacker.View, defender.View);
				onHit?.Invoke();
			}
			else if (UsesPriestSacredAttack(attacker))
			{
				yield return battleAnimationPlayer.PlayPriestSacredJudgement(attacker.View, defender.View);
				onHit?.Invoke();
			}
			else if (UsesRogueDaggerAttack(attacker))
			{
				yield return battleAnimationPlayer.PlayRogueDaggerFlurry(attacker.View, defender.View, attackMargin, onHit);
			}
			else if (UsesNecromancerSoulAttack(attacker))
			{
				yield return battleAnimationPlayer.PlayNecromancerSoulSwarm(attacker.View, defender.View);
				onHit?.Invoke();
			}
			else
			{
				yield return battleAnimationPlayer.PlayHunterArrowAttack(attacker.View, defender.View);
				onHit?.Invoke();
			}
		}
		else
		{
			yield return attacker.View.PlayAttackAnimation();
			onHit?.Invoke();
		}
	}

	private IEnumerator PlayHunterMissIfNeeded(BattleCardState attacker, BattleCardState defender = null)
	{
		if ((!UsesHunterRangedAttack(attacker) && !UsesWarriorSwordAttack(attacker) && !UsesPaladinShieldAttack(attacker) && !UsesMageArcaneAttack(attacker) && !UsesPriestSacredAttack(attacker) && !UsesRogueDaggerAttack(attacker) && !UsesNecromancerSoulAttack(attacker))
			|| attacker.View == null)
			yield break;

		if ((Object)(object)battleAnimationPlayer != (Object)null)
		{
			if (UsesWarriorSwordAttack(attacker))
			{
				if (defender != null && defender.View != null)
					yield return battleAnimationPlayer.PlayWarriorSwordBlocked(attacker.View, defender.View);
			}
			else if (UsesPaladinShieldAttack(attacker))
			{
				if (defender != null && defender.View != null)
					yield return battleAnimationPlayer.PlayPaladinAegisBlocked(attacker.View, defender.View);
			}
			else if (UsesPriestSacredAttack(attacker))
			{
				if (defender != null && defender.View != null)
					yield return battleAnimationPlayer.PlayPriestJudgementBlocked(attacker.View, defender.View);
			}
			else if (UsesMageArcaneAttack(attacker))
			{
				if (defender != null && defender.View != null)
					yield return battleAnimationPlayer.PlayMageArcaneBoltBlocked(attacker.View, defender.View);
			}
			else if (UsesRogueDaggerAttack(attacker))
			{
				if (defender != null && defender.View != null)
					yield return battleAnimationPlayer.PlayRogueDaggerBlocked(attacker.View, defender.View);
			}
			else if (UsesNecromancerSoulAttack(attacker))
			{
				if (defender != null && defender.View != null)
					yield return battleAnimationPlayer.PlayNecromancerSoulWardBlocked(attacker.View, defender.View);
			}
			else
			{
				yield return battleAnimationPlayer.PlayHunterArrowMiss(attacker.View);
			}
		}
		else
		{
			yield return attacker.View.PlayAttackAnimation();
		}
	}

	private IEnumerator ReturnDuelSurvivors(BattleCardState attacker, BattleCardState defender)
	{
		bool num = attacker != null && !attacker.Eliminated;
		bool flag = defender != null && !defender.Eliminated;
		if ((Object)(object)battleAnimationPlayer != (Object)null)
		{
			yield return battleAnimationPlayer.ReturnDuelParticipants(
				attacker?.View,
				defender?.View,
				num,
				flag);
		}
		else
		{
			if (num)
			{
				((MonoBehaviour)this).StartCoroutine(attacker.View.ReturnFromDuelPoint(0.26f));
			}
			if (flag)
			{
				((MonoBehaviour)this).StartCoroutine(defender.View.ReturnFromDuelPoint(0.26f));
			}
			if (num || flag)
			{
				yield return WaitForCardInspectionPause(0.28f);
			}
		}
		SetMessagePanelHiddenForDuel(hidden: false);
	}

	private void SetMessagePanelHiddenForDuel(bool hidden)
	{
		messagePanelHiddenForDuel = hidden;
		if ((Object)(object)messagePanelRect != (Object)null)
		{
			((Component)messagePanelRect).gameObject.SetActive(!messagePanelHiddenForDuel);
		}
	}

	private bool HideMessagePanelForDiceRoll()
	{
		bool wasHidden = messagePanelHiddenForDuel;
		SetMessagePanelHiddenForDuel(hidden: true);
		return wasHidden;
	}

	private void RestoreMessagePanelAfterDiceRoll(bool wasHidden)
	{
		if (!wasHidden)
		{
			SetMessagePanelHiddenForDuel(hidden: false);
		}
	}

	private Vector3 DuelWorldPoint(BattleCardState card, bool attacker)
	{
		RectTransform obj = (((Object)(object)safeAreaRoot != (Object)null) ?safeAreaRoot : canvasRect);
		Rect rect = obj.rect;
		float num = ((card != null && card.BelongsToPlayer) ?(-1f) : 1f);
		if (!attacker)
		{
			num *= 1f;
		}
		float num2 = Mathf.Clamp(rect.width * 0.16f, 160f, 260f);
		Vector3 val = default(Vector3);
		val = new Vector3(rect.center.x + num * num2, rect.center.y - 18f, 0f);
		return ((Transform)obj).TransformPoint(val);
	}

	private CombatModifiers BuildAttackModifiers(BattleCardState attacker, BattleCardState defender, bool defenderAdvantage, bool neutralizeAttackerMatchup = false, bool updateVisuals = true)
	{
		ClassBalanceConfiguration classBalance = configuration.ClassBalance;
		int num = attacker.PendingAttackBonus + TotalPermanentCombatBonus(attacker);
		int defenderFlatBonus = TotalPermanentCombatBonus(defender) + PendingDefenseBonus(defender);
		bool flag = ClassAbilitiesEnabled(attacker);
		if (attacker.BelongsToPlayer && playerAura == BattleAuraType.Warrior && attacker.Card.HeroClass == HeroClass.Warrior && attacker.AbilityArmed)
		{
			num++;
		}
		int num2 = HunterMarkAttackBonus(attacker, defender);
		if (num2 > 0)
		{
			num += num2;
		}
		else if (attacker.Card.HeroClass == HeroClass.Rogue && classBalance.RogueRerollsOnes && flag && updateVisuals)
		{
			attacker.View.SetStatus("REROLL 1", new Color(0.75f, 0.9f, 1f));
		}
		bool forceAttackerAdvantage = false;
		if (attacker.BelongsToPlayer && playerAura == BattleAuraType.Cunning && HeroClassFamily.Of(attacker.Card.HeroClass) == ClassFamily.Cunning && HasBonusOrMalusForCunning(defender))
		{
			forceAttackerAdvantage = true;
			if (updateVisuals)
			{
				attacker.View.SetStatus("AURA ASTUZIA", new Color(0.75f, 0.65f, 1f));
			}
		}
		if (attacker.BelongsToPlayer && playerAura == BattleAuraType.Formation && !formationAuraUsed && ClassMatchup.Compare(attacker.Card.HeroClass, defender.Card.HeroClass) == MatchupResult.Disadvantage)
		{
			neutralizeAttackerMatchup = true;
			if (updateVisuals)
			{
				formationAuraUsed = true;
				attacker.View.SetStatus("AURA FORMAZIONE", new Color(0.55f, 1f, 0.85f));
			}
		}
		return new CombatModifiers(
			flag && attacker.AbilityArmed && attacker.Card.HeroClass == HeroClass.Warrior,
			defenderAdvantage,
			flag && classBalance.RogueRerollsOnes && attacker.Card.HeroClass == HeroClass.Rogue,
			AuraFor(attacker) == BattleAuraType.Rogue && attacker.Card.HeroClass == HeroClass.Rogue,
			num,
			defenderFlatBonus,
			neutralizeAttackerMatchup,
			forceAttackerAdvantage,
			ClassAbilitiesEnabled(defender) && classBalance.RogueRerollsOnes && defender.Card.HeroClass == HeroClass.Rogue,
			AuraFor(defender) == BattleAuraType.Rogue && defender.Card.HeroClass == HeroClass.Rogue);
	}

	private BattleAuraType AuraFor(BattleCardState card)
	{
		return card != null && card.BelongsToPlayer ? playerAura : cpuAura;
	}

	private void ConsumeArmedAttackAbility(BattleCardState attacker, CombatModifiers modifiers)
	{
		if (attacker == null || !attacker.AbilityArmed)
		{
			return;
		}
		if (!modifiers.SumAttackerVigor)
		{
			return;
		}
		attacker.AbilityArmed = false;
		attacker.AbilityUsed = true;
		TriggerMagicAuraAfterAbility();
	}

	private bool ClassAbilitiesEnabled(BattleCardState card)
	{
		if (card != null && !card.BelongsToPlayer && currentRoomType == RoomType.Monster)
		{
			return currentMonsterTier > 1;
		}
		return true;
	}

	private bool HasBonusOrMalusForCunning(BattleCardState target)
	{
		return target != null
			&& (target.WasInhibited
				|| target.InhibitedTurns > 0
				|| target.PendingVigorStepPenalty > 0
				|| target.PendingAttackBonus != 0
				|| TotalPermanentCombatBonus(target) != 0
				|| HunterMarkCount(target) > 0);
	}

	private static int EffectiveVigorDieSides(BattleCardState card, int baseDieSides)
	{
		if (card == null || card.PendingVigorStepPenalty <= 0)
		{
			return baseDieSides;
		}
		int num = baseDieSides;
		for (int i = 0; i < card.PendingVigorStepPenalty; i++)
		{
			num = LowerVigorDie(num);
		}
		return num;
	}

	private int EffectivePlayerAttackVigorDieSides(BattleCardState card, int baseDieSides)
	{
		int dieSides = EffectiveVigorDieSides(card, baseDieSides);
		if (nextRoomEmpowered && card != null && card.BelongsToPlayer)
		{
			return RaiseVigorDie(dieSides);
		}
		return dieSides;
	}

	private static int LowerVigorDie(int dieSides)
	{
		if (dieSides > 3)
		{
			return dieSides switch
			{
				4 => 3, 
				6 => 4, 
				8 => 6, 
				10 => 8, 
				12 => 10, 
				20 => 12, 
				_ => (dieSides <= 6) ?4 : ((dieSides <= 8) ?6 : ((dieSides <= 10) ?8 : ((dieSides <= 12) ?10 : 12))), 
			};
		}
		return 3;
	}

	private int EffectiveDefenseVigorDieSides(BattleCardState card, int baseDieSides)
	{
		int num = EffectiveVigorDieSides(card, baseDieSides);
		if (!HasMagicDefenseAura(card))
		{
			return num;
		}
		return RaiseVigorDie(num);
	}

	private bool HasMagicDefenseAura(BattleCardState card)
	{
		if (card != null && !card.Eliminated && AuraForCard(card) == BattleAuraType.Magic)
		{
			return HeroClassFamily.Of(card.Card.HeroClass) == ClassFamily.Magic;
		}
		return false;
	}

	private static int RaiseVigorDie(int dieSides)
	{
		if (dieSides > 3)
		{
			return dieSides switch
			{
				4 => 6, 
				6 => 8, 
				8 => 10, 
				10 => 12, 
				12 => 20, 
				_ => (dieSides < 6) ?6 : ((dieSides < 8) ?8 : ((dieSides < 10) ?10 : ((dieSides < 12) ?12 : 20))), 
			};
		}
		return 4;
	}

	private void ConsumeVigorPenalties(BattleCardState first, BattleCardState second)
	{
		if (first != null && first.PendingVigorStepPenalty > 0)
		{
			first.PendingVigorStepPenalty = 0;
			RefreshPersistentStatus(first);
		}
		if (second != null && second.PendingVigorStepPenalty > 0)
		{
			second.PendingVigorStepPenalty = 0;
			RefreshPersistentStatus(second);
		}
	}

	private void TriggerMagicAuraAfterAbility()
	{
	}

	private bool TryCreateNecromancerSpirit(BattleCardState defeated)
	{
		if (defeated == null || !defeated.BelongsToPlayer || playerAura != BattleAuraType.Necromancer || necromancerSpiritUsed)
		{
			return false;
		}
		necromancerSpiritUsed = true;
		defeated.Eliminated = false;
		defeated.IsSpirit = true;
		defeated.AbilityUsed = false;
		defeated.AbilityArmed = false;
		defeated.View.ResetState();
		defeated.View.SetInitiative(defeated.Initiative);
		ApplyPlayerAuraVisuals(appendLog: false);
		RefreshPersistentStatus(defeated);
		SetMessage("AURA NECROMANTE: " + defeated.Card.Name + " resta in campo e avra un ultimo turno.");
		return true;
	}

	private IEnumerator ExecutePaladinCounter(BattleCardState paladin, BattleCardState target)
	{
		int num = EffectiveVigorDieSides(paladin, runProgress.PlayerVigorDieSides);
		int num2 = EffectiveDefenseVigorDieSides(target, runProgress.MasterVigorDieSides);
		CombatModifiers modifiers = new CombatModifiers(sumAttackerVigor: false, defenderAdvantage: false, rerollAttackerOnes: false, rerollAttackerTwos: false, 1);
		CombatResult result = combatResolver.ResolveAttack(paladin.Card, target.Card, num, num2, modifiers);
		SetMessage("AURA PALADINO: " + paladin.Card.Name + " contrattacca " + target.Card.Name + " con +1.");
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		paladin.View.PlayVigorRoll(diceCatalog, num, result.AttackerRoll, "CONTRATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		target.View.PlayVigorRoll(diceCatalog, num2, result.DefenderRoll, "DIFESA CPU", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, paladin, target);
		PlayAttackResultSfx(paladin, result.DefenderIsDefeated);
		if (result.DefenderIsDefeated)
		{
			target.Eliminated = true;
			ApplyMightAuraDeathBonuses(target);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(target, paladin.Card.HeroClass);
		}
		ConsumeVigorPenalties(paladin, target);
	}

	private int HunterMarkAttackBonus(BattleCardState attacker, BattleCardState defender)
	{
		return HunterMarkBonusForTarget(defender);
	}

	private int HunterMarkCount(BattleCardState target)
	{
		if (target == null)
		{
			return 0;
		}
		return playerCards.Concat(cpuCards).Count((BattleCardState card) => card != null && card.Card.HeroClass == HeroClass.Hunter && card.MarkedTarget == target);
	}

	private bool IsHunterMarked(BattleCardState target)
	{
		return HunterMarkCount(target) > 0;
	}

	private int HunterMarkBonusForTarget(BattleCardState target)
	{
		if (target == null)
		{
			return 0;
		}
		int normalBonus = 0;
		int auraBonus = 0;
		foreach (BattleCardState hunter in playerCards.Concat(cpuCards))
		{
			if (hunter == null || hunter.Card.HeroClass != HeroClass.Hunter || hunter.MarkedTarget != target)
			{
				continue;
			}
			normalBonus = configuration.ClassBalance.HunterStrongTargetBonus;
			auraBonus = Math.Max(auraBonus, HunterMarkValueFor(hunter));
		}
		return Math.Max(normalBonus, auraBonus);
	}

	private void ConsumeHunterMarks(BattleCardState target)
	{
		if (target == null)
		{
			return;
		}

		bool consumed = false;
		foreach (BattleCardState hunter in playerCards.Concat(cpuCards))
		{
			if (hunter == null || hunter.Card.HeroClass != HeroClass.Hunter || hunter.MarkedTarget != target)
			{
				continue;
			}

			hunter.MarkedTarget = null;
			consumed = true;
		}

		if (consumed)
		{
			RefreshPersistentStatus(target);
		}
	}

	private int HunterMarkValueFor(BattleCardState hunter)
	{
		if (hunter == null)
		{
			return configuration.ClassBalance.HunterStrongTargetBonus;
		}
		BattleAuraType aura = hunter.BelongsToPlayer ?playerAura : cpuAura;
		return aura == BattleAuraType.Hunter ?configuration.ClassBalance.HunterStrongTargetBonus * 2 : configuration.ClassBalance.HunterStrongTargetBonus;
	}

	private void UpdatePostAttackClassState(BattleCardState attacker, bool defeatedTarget)
	{
		attacker.PendingAttackBonus = 0;
		attacker.PendingAttackBonusKind = PendingAttackBonusKind.None;
		attacker.View.SetStrengthValue(DisplayStrength(attacker));
		if (ClassAbilitiesEnabled(attacker) && attacker.Card.HeroClass == HeroClass.Barbarian && !defeatedTarget)
		{
			ApplyBarbarianFury(attacker);
		}
		else
		{
			_ = HunterMarkBonusForTarget(attacker);
			_ = 0;
			RefreshPersistentStatus(attacker);
		}
	}

	private void UpdatePostDefenseClassState(BattleCardState defender, bool defeatedDefender)
	{
		if (!defeatedDefender && ClassAbilitiesEnabled(defender) && defender.Card.HeroClass == HeroClass.Barbarian)
		{
			ApplyBarbarianFury(defender);
		}
	}

	private void ApplyBarbarianFury(BattleCardState card)
	{
		BattleAuraType aura = card.BelongsToPlayer ?playerAura : cpuAura;
		int pendingAttackBonus = aura == BattleAuraType.Barbarian
			? configuration.ClassBalance.BarbarianRageBonus + 1
			: configuration.ClassBalance.BarbarianRageBonus;
		card.PendingAttackBonus = pendingAttackBonus;
		card.PendingAttackBonusKind = PendingAttackBonusKind.Fury;
		card.View.SetStrengthValue(DisplayStrength(card));
		RefreshPersistentStatus(card);
		PlayBarbarianFurySfx();
	}

	private void ApplyMightAuraDeathBonuses(BattleCardState defeated)
	{
		if (defeated == null || !defeated.Eliminated)
		{
			return;
		}

		ApplyMightAuraDeathBonusesForSide(playerCards, playerAura, "TU");
		ApplyMightAuraDeathBonusesForSide(cpuCards, cpuAura, "CPU");
	}

	private void ApplyMightAuraDeathBonusesForSide(List<BattleCardState> cards, BattleAuraType aura, string ownerLabel)
	{
		if (aura != BattleAuraType.Might || cards == null)
		{
			return;
		}

		foreach (BattleCardState card in cards)
		{
			if (card == null
				|| card.Eliminated
				|| card.Card == null
				|| HeroClassFamily.Of(card.Card.HeroClass) != ClassFamily.Might)
			{
				continue;
			}

			card.MightAuraCombatBonus++;
			card.View?.SetStrengthValue(DisplayStrength(card));
			RefreshPersistentStatus(card);
			AppendLog($"AURA FORZUTA DA MORTE ({ownerLabel}) - {card.Card.Name} ottiene +1 permanente.");
		}
	}

	private static int TotalPermanentCombatBonus(BattleCardState card)
	{
		return card == null ?0 : card.PermanentCombatBonus + card.MightAuraCombatBonus;
	}

	private int DisplayStrength(BattleCardState card)
	{
		if (IsComposableGolemProxy(card) && activeComposableGolem != null)
		{
			return activeComposableGolem.ActiveForm.Power;
		}
		if (IsMedusaBossProxy(card) && activeMedusaBoss != null)
		{
			return MedusaBoss.CardStrength;
		}
		if (IsTrentorBossProxy(card) && activeTrentorBoss != null)
		{
			return TrentorBoss.CardStrength;
		}
		if (IsBragusBossProxy(card) && activeBragusBoss != null)
		{
			return BragusBoss.CardStrength;
		}
		return card.Card.Strength + card.PendingAttackBonus + TotalPermanentCombatBonus(card);
	}

	private BattleCardState ChooseHighestThreat(IEnumerable<BattleCardState> cards, bool includeEliminated)
	{
		return cards.Where((BattleCardState card) => card != null && (includeEliminated || !card.Eliminated)).OrderByDescending(DisplayStrength).ThenByDescending((BattleCardState card) => card.Card.Strength)
			.FirstOrDefault();
	}

	private static int PendingDefenseBonus(BattleCardState card)
	{
		if (card.PendingAttackBonusKind != PendingAttackBonusKind.Fury)
		{
			return 0;
		}
		return card.PendingAttackBonus;
	}

	private void RefreshPersistentStatus(BattleCardState card)
	{
		card.View.SetStrengthValue(DisplayStrength(card));
		if (card.Eliminated)
		{
			card.View.SetStatus("MORTE", new Color(0.95f, 0.12f, 0.12f));
			return;
		}
		List<PrototypeCardView.StatusToken> list = new List<PrototypeCardView.StatusToken>();
		if (IsComposableGolemProxy(card) && activeComposableGolem != null)
		{
			ComposableGolemForm activeGolemForm = activeComposableGolem.ActiveForm.Form;
			card.View.SetComposableGolemForm(activeGolemForm);
		}
		if (IsTrentorBossProxy(card) && activeTrentorBoss != null)
		{
			UpdateTrentorBossHealthBar(card);
		}
		if (HasMagicDefenseAura(card))
		{
			list.Add(new PrototypeCardView.StatusToken("DIFESA DADO +1", new Color(0.45f, 0.75f, 1f)));
		}
		else
		{
			BattleAuraType battleAuraType = AuraForCard(card);
			if (battleAuraType != BattleAuraType.None)
			{
				list.Add(new PrototypeCardView.StatusToken("AURA " + AuraShortLabel(battleAuraType), AuraColor(battleAuraType)));
			}
		}
		if (card.AbilityArmed && card.Card.HeroClass == HeroClass.Paladin)
		{
			list.Add(new PrototypeCardView.StatusToken("PROTEZIONE PRONTA", new Color(0.35f, 0.75f, 1f)));
		}
		if (IsWaitingAfterRevive(card))
		{
			list.Add(new PrototypeCardView.StatusToken("RIALZATA", new Color(0.45f, 1f, 0.82f)));
		}
		if (card.InhibitedTurns > 0)
		{
			list.Add(new PrototypeCardView.StatusToken("INIBITO", new Color(0.6f, 0.5f, 1f)));
		}
		if (card.Petrified)
		{
			list.Add(new PrototypeCardView.StatusToken("PIETRA", new Color(0.62f, 0.68f, 0.7f)));
		}
		if (card.PendingVigorStepPenalty > 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"DADO -{card.PendingVigorStepPenalty}", new Color(0.55f, 0.8f, 1f)));
		}
		if (card.PermanentCombatBonus > 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"EQUIP +{card.PermanentCombatBonus}", new Color(0.7f, 1f, 0.45f)));
		}
		if (card.MightAuraCombatBonus > 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"AURA +{card.MightAuraCombatBonus}", new Color(1f, 0.16f, 0.12f)));
		}
		if (card.PermanentCombatBonus < 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"{card.PermanentCombatBonus}", new Color(1f, 0.42f, 0.42f)));
		}
		if (card.PendingAttackBonus > 0)
		{
			list.Add(new PrototypeCardView.StatusToken(PendingAttackBonusLabel(card), new Color(1f, 0.75f, 0.25f)));
		}
		int num = HunterMarkBonusForTarget(card);
		if (num > 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"BERSAGLIO MARCATO +{num}", new Color(1f, 0.65f, 0.2f)));
		}
		card.View.SetStatuses(list.ToArray());
	}

	private BattleAuraType AuraForCard(BattleCardState card)
	{
		if (card == null || card.Eliminated)
		{
			return BattleAuraType.None;
		}
		if (!card.BelongsToPlayer)
		{
			return cpuAura;
		}
		return playerAura;
	}

	private void ActivateCurrentAttack()
	{
		if (!inputLocked && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState = playerCards[selectedPlayerIndex];
			if (battleCardState != null && !battleCardState.Eliminated)
			{
				attackTargetingActive = true;
				abilityTargetMode = AbilityTargetMode.None;
				activeAbilityUser = null;
				activeAttachmentSource = null;
				pendingAbilityUser = null;
				((Component)abilityButton).gameObject.SetActive(false);
				((Component)attachmentButton).gameObject.SetActive(false);
				ShowTargetHints(battleCardState);
				SetMessage("ATTACCO: scegli una pedina avversaria da colpire con " + battleCardState.Card.Name + ".");
				UpdateInteractions();
			}
		}
	}

	private void ActivateCurrentAbility()
	{
		if (!inputLocked && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState = playerCards[selectedPlayerIndex];
			if (!battleCardState.AbilityUsed && !battleCardState.AbilityArmed)
			{
				attackTargetingActive = false;
				pendingAbilityUser = battleCardState;
				ConfirmPendingAbility();
			}
		}
	}

	private void ConfirmPendingAbility()
	{
		BattleCardState battleCardState = pendingAbilityUser;
		if (battleCardState == null)
		{
			return;
		}
		pendingAbilityUser = null;
		((Component)confirmActionButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		attackTargetingActive = false;
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		switch (battleCardState.Card.HeroClass)
		{
		default:
			return;
		case HeroClass.Assassin:
			battleCardState.AbilityArmed = true;
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.AssassinEnemy;
			SetMessage("ABILITA ASSASSINO: scegli un nemico da inibire.");
			UpdateInteractions();
			break;
		case HeroClass.Warrior:
			battleCardState.AbilityArmed = true;
			attackTargetingActive = true;
			ShowTargetHints(battleCardState);
			SetMessage("ABILITA GUERRIERO: " + battleCardState.Card.Name + " sommera due dadi nel prossimo attacco.");
			break;
		case HeroClass.Mage:
			battleCardState.AbilityArmed = true;
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.MageEnemy;
			SetMessage("ABILITA MAGO: scegli un nemico a cui abbassare il dado Vigore.");
			UpdateInteractions();
			break;
		case HeroClass.Paladin:
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.PaladinAlly;
			SetMessage("ABILITA PALADINO: scegli una pedina alleata o " + battleCardState.Card.Name + " stesso da proteggere.");
			break;
		case HeroClass.Hunter:
			battleCardState.AbilityArmed = true;
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.HunterEnemy;
			ShowTargetHints(battleCardState);
			SetMessage("ABILITA CACCIATORE: scegli un nemico da marcare.");
			UpdateInteractions();
			break;
		case HeroClass.Necromancer:
			if (!playerCards.Any(CanReviveWithNecromancer))
			{
				return;
			}
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.NecromancerAlly;
			SetMessage("ABILITA NECROMANTE: scegli una carta alleata eliminata da rialzare.");
			UpdateInteractions();
			break;
		case HeroClass.Priest:
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.PriestAlly;
			SetMessage("ABILITA SACERDOTE: scegli una carta alleata da benedire.");
			UpdateInteractions();
			break;
		case HeroClass.Barbarian:
			return;
		}
		RefreshAbilityButton(battleCardState);
		UpdateInteractions();
	}

	private void RefreshAbilityButton(BattleCardState card)
	{
		((Component)abilityButton).gameObject.SetActive(false);
		abilityButton.interactable = false;
		RefreshCardActionOverlays();
	}

	private void RefreshAttachmentButton(BattleCardState card)
	{
		if (!((Object)(object)attachmentButton == (Object)null))
		{
			((Component)attachmentButton).gameObject.SetActive(false);
			attachmentButton.interactable = false;
		}
	}

	private void ActivateCurrentAttachment()
	{
		if (!inputLocked && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState = playerCards[selectedPlayerIndex];
			if (CanUseAttachment(battleCardState))
			{
				attackTargetingActive = false;
				activeAttachmentSource = battleCardState;
				abilityTargetMode = AbilityTargetMode.AttachmentAlly;
				attachmentButton.interactable = false;
				((Component)abilityButton).gameObject.SetActive(false);
				ClearTargetHints();
				SetActiveTurnAura(null);
				SetMessage($"Puoi equipaggiare questa carta sacrificandola, aumentando di +{AttachmentBonus(battleCardState)} la forza di una carta alleata.");
				UpdateInteractions();
			}
		}
	}

	private bool CanUseAttachment(BattleCardState card)
	{
		if (IsBragusEquipmentLockActive(card?.BelongsToPlayer ?? true))
		{
			return false;
		}
		if (card != null && !card.Eliminated && card.BelongsToPlayer && card.Card.Strength >= 2 && card.Card.Strength < 5)
		{
			return playerCards.Any((BattleCardState ally) => CanTargetAttachment(card, ally));
		}
		return false;
	}

	private bool IsBragusEquipmentLockActive(bool blockedSideBelongsToPlayer)
	{
		if (activeBragusBoss == null || activeBragusBoss.IsDefeated)
		{
			return false;
		}
		return cpuCards.Any((BattleCardState card) => IsBragusBossProxy(card) && !card.Eliminated)
			&& blockedSideBelongsToPlayer;
	}

	private bool CanCpuUseAdvancedActions(BattleCardState card)
	{
		if (card != null && !card.Eliminated && !card.BelongsToPlayer && currentRoomType == RoomType.Monster)
		{
			return currentMonsterTier >= 3;
		}
		return false;
	}

	private bool TryChooseCpuAttachment(BattleCardState source, out BattleCardState target)
	{
		target = null;
		if (!CanCpuUseAdvancedActions(source) || source.Card.Strength < 2 || source.Card.Strength >= 5 || cpuCards.Count((BattleCardState card) => card != null && !card.Eliminated) <= 1)
		{
			return false;
		}
		target = cpuCards.Where((BattleCardState card) => CanTargetAttachment(source, card)).OrderByDescending(DisplayStrength).FirstOrDefault();
		if (target == null)
		{
			return false;
		}
		if (currentMonsterTier < 4)
		{
			return DisplayStrength(target) >= source.Card.Strength + 3;
		}
		return true;
	}

	private bool TryAutoWinCampaignWhenCpuIsLocked()
	{
		if (!CanEvaluateCampaignCpuLock())
		{
			return false;
		}

		List<BattleCardState> aliveCpuCards = cpuCards.Where((BattleCardState card) => card != null && !card.Eliminated).ToList();
		if (aliveCpuCards.Count == 0)
		{
			return false;
		}

		foreach (BattleCardState cpuCard in aliveCpuCards)
		{
			if (CpuHasAnyUsefulAction(cpuCard))
			{
				return false;
			}
		}

		((MonoBehaviour)this).StartCoroutine(ResolveAutoWinCampaignWhenCpuIsLocked(aliveCpuCards));
		return true;
	}

	private IEnumerator ResolveAutoWinCampaignWhenCpuIsLocked(List<BattleCardState> defeatedCpuCards)
	{
		inputLocked = true;
		ClearTargetHints();
		SetActiveTurnAura(null);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		((Component)confirmActionButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);

		AppendLog("AUTO-VITTORIA - la CPU non ha attacchi possibili, abilita disponibili o equip utili: la stanza viene risolta automaticamente.");
		SetBattlefieldMessage("AUTO-VITTORIA: la CPU non puo piu colpirti ne usare azioni utili.");
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);

		HeroClass killerHeroClass = AutoWinDefeatKillerClass();
		PlayDeathCardSfx();
		int pendingDefeatAnimations = 0;
		foreach (BattleCardState cpuCard in defeatedCpuCards)
		{
			if (cpuCard == null || cpuCard.Eliminated)
			{
				continue;
			}
			cpuCard.Eliminated = true;
			cpuCard.View.SetSelected(selected: false);
			RefreshPersistentStatus(cpuCard);
			pendingDefeatAnimations++;
			((MonoBehaviour)this).StartCoroutine(PlayAutoWinDefeatAnimation(cpuCard, killerHeroClass, () => pendingDefeatAnimations--));
		}

		while (pendingDefeatAnimations > 0)
		{
			yield return null;
		}

		RefreshInitiativeDisplay();
		CheckEndGame();
	}

	private IEnumerator PlayAutoWinDefeatAnimation(BattleCardState card, HeroClass killerHeroClass, Action completed)
	{
		yield return PlayTimelineAwareDefeatAnimation(card, killerHeroClass);
		completed?.Invoke();
	}

	private HeroClass AutoWinDefeatKillerClass()
	{
		BattleCardState rogue = playerCards.FirstOrDefault((BattleCardState card) => card != null && !card.Eliminated && card.Card.HeroClass == HeroClass.Rogue);
		if (rogue != null)
		{
			return HeroClass.Rogue;
		}
		BattleCardState strongest = playerCards.Where((BattleCardState card) => card != null && !card.Eliminated).OrderByDescending(DisplayStrength).FirstOrDefault();
		return strongest != null ? strongest.Card.HeroClass : HeroClass.Rogue;
	}

	private bool CanEvaluateCampaignCpuLock()
	{
		return campaignDeck != null
			&& currentRoomType == RoomType.Monster
			&& activeComposableGolem == null
			&& activeMedusaBoss == null
			&& !gameFinished
			&& HasAliveCard(playerCards)
			&& HasAliveCard(cpuCards);
	}

	private bool CpuHasAnyUsefulAction(BattleCardState cpuCard)
	{
		if (cpuCard == null || cpuCard.Eliminated)
		{
			return false;
		}
		if (CanCpuUseAdvancedActions(cpuCard) && CpuCanUseAttachment(cpuCard))
		{
			return true;
		}
		if (CanCpuUseAdvancedActions(cpuCard) && CpuHasAvailableClassAbility(cpuCard))
		{
			return true;
		}
		return CpuCanDefeatAnyPlayerCard(cpuCard);
	}

	private bool CpuCanDefeatAnyPlayerCard(BattleCardState cpuCard)
	{
		int attackerDieSides = EffectiveVigorDieSides(cpuCard, runProgress.MasterVigorDieSides);
		foreach (BattleCardState playerCard in playerCards)
		{
			if (playerCard == null || playerCard.Eliminated)
			{
				continue;
			}

			BattleCardState protectingPaladin = playerCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == playerCard) && card != playerCard);
			BattleCardState selfProtectingPaladin = playerCard.Card.HeroClass == HeroClass.Paladin && playerCard.AbilityArmed && (playerCard.ProtectedAlly == null || playerCard.ProtectedAlly == playerCard) ? playerCard : null;
			BattleCardState defender = protectingPaladin ?? selfProtectingPaladin ?? playerCard;
			int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
			CombatModifiers modifiers = BuildAttackModifiers(cpuCard, defender, protectingPaladin != null || selfProtectingPaladin != null, protectingPaladin != null || selfProtectingPaladin != null, updateVisuals: false);
			if (CombatCertaintyCalculator.Evaluate(cpuCard.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers) != CombatCertainty.Impossible)
			{
				return true;
			}
		}
		return false;
	}

	private bool CpuCanUseAttachment(BattleCardState source)
	{
		if (!CanCpuUseAdvancedActions(source) || source.Card.Strength < 2 || source.Card.Strength >= 5 || cpuCards.Count((BattleCardState card) => card != null && !card.Eliminated) <= 1)
		{
			return false;
		}
		BattleCardState target = cpuCards.Where((BattleCardState card) => CanTargetAttachment(source, card)).OrderByDescending(DisplayStrength).FirstOrDefault();
		if (target == null)
		{
			return false;
		}
		return currentMonsterTier >= 4 || DisplayStrength(target) >= source.Card.Strength + 3;
	}

	private bool CpuHasAvailableClassAbility(BattleCardState card)
	{
		if (!CanCpuUseAdvancedActions(card) || card.AbilityUsed || card.AbilityArmed || !ClassAbilitiesEnabled(card))
		{
			return false;
		}
		switch (card.Card.HeroClass)
		{
		case HeroClass.Warrior:
		case HeroClass.Assassin:
		case HeroClass.Mage:
		case HeroClass.Paladin:
		case HeroClass.Priest:
			return true;
		case HeroClass.Hunter:
			return playerCards.Any((BattleCardState target) => target != null && !target.Eliminated && !IsHunterMarked(target));
		case HeroClass.Necromancer:
			return cpuCards.Any(CanReviveWithNecromancer);
		default:
			return false;
		}
	}

	private bool TryUseCpuClassAbility(BattleCardState card, out string message)
	{
		message = null;
		if (!CanCpuUseAdvancedActions(card) || card.AbilityUsed || card.AbilityArmed || !ClassAbilitiesEnabled(card))
		{
			return false;
		}
		switch (card.Card.HeroClass)
		{
		case HeroClass.Warrior:
			card.AbilityArmed = true;
			RefreshPersistentStatus(card);
			message = "CPU ABILITA: " + card.Card.Name + " prepara un colpo pesante e tirera due dadi Vigore.";
			return true;
		case HeroClass.Assassin:
			return TryUseCpuAssassinAbility(card, out message);
		case HeroClass.Mage:
			return TryUseCpuMageAbility(card, out message);
		case HeroClass.Paladin:
			return TryUseCpuPaladinAbility(card, out message);
		case HeroClass.Hunter:
			return TryUseCpuHunterAbility(card, out message);
		case HeroClass.Necromancer:
			return TryUseCpuNecromancerAbility(card, out message);
		case HeroClass.Priest:
			return TryUseCpuPriestAbility(card, out message);
		default:
			return false;
		}
	}

	private bool TryUseCpuAssassinAbility(BattleCardState card, out string message)
	{
		BattleCardState battleCardState = ChooseHighestThreat(playerCards, includeEliminated: false);
		if (battleCardState == null)
		{
			message = null;
			return false;
		}
		battleCardState.InhibitedTurns = Math.Max(battleCardState.InhibitedTurns, 1);
		battleCardState.WasInhibited = true;
		if (cpuAura == BattleAuraType.Assassin)
		{
			battleCardState.PermanentCombatBonus--;
		}
		card.AbilityUsed = true;
		RefreshPersistentStatus(battleCardState);
		PlayClassAbilitySfx(HeroClass.Assassin);
		if ((Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)card.View != (Object)null
			&& (Object)(object)battleCardState.View != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(card.View, battleCardState.View, AbilityTargetLineColor));
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayAssassinInhibitSmoke(battleCardState.View));
		}
		string text = ((cpuAura == BattleAuraType.Assassin) ?" e infligge -1 permanente" : string.Empty);
		message = "CPU ASSASSINO: " + card.Card.Name + " inibisce " + battleCardState.Card.Name + text + ".";
		return true;
	}

	private bool TryUseCpuMageAbility(BattleCardState card, out string message)
	{
		BattleCardState battleCardState = ChooseHighestThreat(playerCards, includeEliminated: false);
		if (battleCardState == null)
		{
			message = null;
			return false;
		}
		int num = ((cpuAura != BattleAuraType.Mage) ?1 : 2);
		int baseDieSides = runProgress != null ? runProgress.PlayerVigorDieSides : configuration.Gameplay.VigorDieSides;
		int startDieSides = EffectiveVigorDieSides(battleCardState, baseDieSides);
		battleCardState.PendingVigorStepPenalty = Math.Max(battleCardState.PendingVigorStepPenalty, num);
		int endDieSides = EffectiveVigorDieSides(battleCardState, baseDieSides);
		card.AbilityUsed = true;
		RefreshPersistentStatus(battleCardState);
		PlayClassAbilitySfx(HeroClass.Mage);
		if ((Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)card.View != (Object)null
			&& (Object)(object)battleCardState.View != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(card.View, battleCardState.View, AbilityTargetLineColor));
		}
		((MonoBehaviour)this).StartCoroutine(PlayMageVigorConstellation(
			battleCardState,
			startDieSides,
			endDieSides));
		message = $"Grazie all'abilita del mago, il prossimo dado Vigore di {battleCardState.Card.Name} scende di {num} step: usera un D{endDieSides}.";
		return true;
	}

	private bool TryUseCpuPaladinAbility(BattleCardState card, out string message)
	{
		BattleCardState battleCardState = cpuCards.Where((BattleCardState ally) => ally != null && !ally.Eliminated).OrderByDescending(DisplayStrength).FirstOrDefault();
		if (battleCardState == null)
		{
			message = null;
			return false;
		}
		card.AbilityArmed = true;
		card.ProtectedAlly = battleCardState;
		RefreshPersistentStatus(card);
		PlayClassAbilitySfx(HeroClass.Paladin);
		if ((Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)card.View != (Object)null
			&& (Object)(object)battleCardState.View != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(card.View, battleCardState.View, AbilityTargetLineColor));
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayPaladinProtectionConstellation(battleCardState.View));
		}
		string text = ((battleCardState == card) ?"si prepara a difendersi con vantaggio" : ("proteggera " + battleCardState.Card.Name));
		message = "CPU PALADINO: " + card.Card.Name + " " + text + ".";
		return true;
	}

	private bool TryUseCpuHunterAbility(BattleCardState card, out string message)
	{
		BattleCardState battleCardState = playerCards.Where((BattleCardState target) => target != null && !target.Eliminated && !IsHunterMarked(target)).OrderByDescending(DisplayStrength).FirstOrDefault();
		if (battleCardState == null)
		{
			message = null;
			return false;
		}
		if (card.MarkedTarget != null && !card.MarkedTarget.Eliminated)
		{
			RefreshPersistentStatus(card.MarkedTarget);
		}
		card.MarkedTarget = battleCardState;
		card.AbilityUsed = true;
		RefreshPersistentStatus(battleCardState);
		PlayClassAbilitySfx(HeroClass.Hunter);
		if ((Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)card.View != (Object)null
			&& (Object)(object)battleCardState.View != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(card.View, battleCardState.View, AbilityTargetLineColor));
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayHunterMarkReticle(battleCardState.View));
		}
		message = $"CPU CACCIATORE: {card.Card.Name} marca {battleCardState.Card.Name}. Bersaglio marcato: chi lo attacca prende +{HunterMarkValueFor(card)}.";
		return true;
	}

	private bool TryUseCpuNecromancerAbility(BattleCardState card, out string message)
	{
		BattleCardState battleCardState = (from dead in cpuCards.Where(CanReviveWithNecromancer)
			orderby dead.Card.Strength descending
			select dead).FirstOrDefault();
		if (battleCardState == null)
		{
			message = null;
			return false;
		}
		battleCardState.Eliminated = false;
		battleCardState.RevivedRound = 0;
		MoveTurnAfter(card, battleCardState);
		battleCardState.View.ResetState();
		battleCardState.View.SetInitiative(battleCardState.Initiative);
		RefreshPersistentStatus(battleCardState);
		ApplyCpuAuraVisuals(appendLog: false);
		card.AbilityUsed = true;
		PlayClassAbilitySfx(HeroClass.Necromancer);
		if ((Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)card.View != (Object)null
			&& (Object)(object)battleCardState.View != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(card.View, battleCardState.View, AbilityTargetLineColor));
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayNecromancerReviveSkullConvergence(battleCardState.View));
		}
		message = "CPU NECROMANTE: " + card.Card.Name + " rialza " + battleCardState.Card.Name + ".";
		return true;
	}

	private bool TryUseCpuPriestAbility(BattleCardState card, out string message)
	{
		BattleCardState battleCardState = cpuCards.Where((BattleCardState ally) => ally != null && !ally.Eliminated).OrderByDescending(DisplayStrength).FirstOrDefault();
		if (battleCardState == null)
		{
			message = null;
			return false;
		}
		int num = ((cpuAura == BattleAuraType.Priest) ?(configuration.ClassBalance.PriestBlessingBonus + 1) : configuration.ClassBalance.PriestBlessingBonus);
		battleCardState.PendingAttackBonus += num;
		if (battleCardState.PendingAttackBonusKind != PendingAttackBonusKind.Fury)
		{
			battleCardState.PendingAttackBonusKind = PendingAttackBonusKind.Blessing;
		}
		card.AbilityUsed = true;
		RefreshPersistentStatus(battleCardState);
		PlayClassAbilitySfx(HeroClass.Priest);
		if ((Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)card.View != (Object)null
			&& (Object)(object)battleCardState.View != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(card.View, battleCardState.View, AbilityTargetLineColor));
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayPriestBlessing(card.View, battleCardState.View, num));
		}
		message = $"CPU SACERDOTE: {card.Card.Name} benedice {battleCardState.Card.Name} con +{num}.";
		return true;
	}

	private static bool CanTargetAttachment(BattleCardState source, BattleCardState target)
	{
		if (source != null && target != null && source != target && target.BelongsToPlayer == source.BelongsToPlayer)
		{
			return !target.Eliminated;
		}
		return false;
	}

	private static int AttachmentBonus(BattleCardState card)
	{
		if (card != null)
		{
			return 5 - card.Card.Strength;
		}
		return 0;
	}

	private IEnumerator ExecuteAttachment(BattleCardState source, BattleCardState target)
	{
		inputLocked = true;
		attackTargetingActive = false;
		activeAttachmentSource = null;
		abilityTargetMode = AbilityTargetMode.None;
		((Component)attachmentButton).gameObject.SetActive(false);
		((Component)cancelActionButton).gameObject.SetActive(false);
		ClearTargetHints();
		RefreshCardActionOverlays();
		UpdateInteractions();
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(source.View, target.View, AttachmentTargetLineColor);
		int num = AttachmentBonus(source);
		target.PermanentCombatBonus += num;
		RefreshPersistentStatus(target);
		target.View.PlayAttachmentEquipEffect();
		source.Eliminated = true;
		source.IsAttachment = true;
		source.AttachedTo = target;
		source.View.SetSelected(selected: false);
		SetMessage($"POTENZIA: {source.Card.Name} viene sacrificata e potenzia {target.Card.Name} di +{num} per tutta la battaglia.");
		AppendLog($"POTENZIA - {source.Card.Name} sacrificata: {target.Card.Name} ottiene +{num} permanente.");
		PlayAttachmentSfx();
		yield return PlayTimelineAwareDefeatAnimation(source, source.Card.HeroClass);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		selectedPlayerIndex = -1;
		FinishTurn();
	}

	private IEnumerator ExecuteCpuAttachment(BattleCardState source, BattleCardState target)
	{
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(source.View, target.View, AttachmentTargetLineColor);
		int num = AttachmentBonus(source);
		target.PermanentCombatBonus += num;
		RefreshPersistentStatus(target);
		target.View.PlayAttachmentEquipEffect();
		source.Eliminated = true;
		source.IsAttachment = true;
		source.AttachedTo = target;
		source.View.SetSelected(selected: false);
		SetMessage($"CPU POTENZIA: {source.Card.Name} viene sacrificata e potenzia {target.Card.Name} di +{num} per tutta la battaglia.");
		AppendLog($"CPU POTENZIA - {source.Card.Name} sacrificata: {target.Card.Name} ottiene +{num} permanente.");
		PlayAttachmentSfx();
		yield return PlayTimelineAwareDefeatAnimation(source, source.Card.HeroClass);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private void RefreshCardActionOverlays()
	{
		foreach (PrototypeCardView draftView in draftViews)
		{
			draftView.ClearActionOverlay();
		}
		foreach (BattleCardState playerCard in playerCards)
		{
			playerCard.View.ClearActionOverlay();
		}
		foreach (BattleCardState cpuCard in cpuCards)
		{
			cpuCard.View.ClearActionOverlay();
		}
		if (pendingDeploymentIndex >= 0 && pendingDeploymentIndex < draftViews.Count)
		{
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			draftViews[pendingDeploymentIndex].ShowConfirmInfoActions(confirmActionSprite, infoActionSprite, new UnityAction(ConfirmPendingDeployment), new UnityAction(ShowPendingDeploymentInspection));
			for (int i = 0; i < draftViews.Count; i++)
			{
				if (i != pendingDeploymentIndex && !selectedDraftCards.Contains(i))
				{
					int capturedIndex = i;
					draftViews[i].ShowCardClickAction((UnityAction)delegate
					{
						ToggleDraftCard(capturedIndex);
					});
				}
			}
		}
		else if (pendingAbilityUser != null)
		{
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			pendingAbilityUser.View.ShowConfirmCancelActions(confirmActionSprite, cancelActionSprite, new UnityAction(ConfirmPendingAbility), new UnityAction(CancelPendingAction));
		}
		else if (attackTargetingActive || activeAbilityUser != null || abilityTargetMode != AbilityTargetMode.None)
		{
			BattleCardState battleCardState = activeAbilityUser ?? activeAttachmentSource;
			if (battleCardState == null && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
			{
				battleCardState = playerCards[selectedPlayerIndex];
			}
			((Component)cancelActionButton).gameObject.SetActive(false);
			battleCardState?.View.ShowCancelAction(cancelActionSprite, new UnityAction(CancelPendingAction));
		}
		else if (!inputLocked && !gameFinished && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState2 = playerCards[selectedPlayerIndex];
			bool flag = IsClassAbilityActionAvailable(battleCardState2);
			bool flag2 = CanUseAttachment(battleCardState2);
			((Component)abilityButton).gameObject.SetActive(false);
			if ((Object)(object)attachmentButton != (Object)null)
			{
				((Component)attachmentButton).gameObject.SetActive(false);
			}
			if (flag && flag2)
			{
				battleCardState2.View.ShowTripleActions(GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack), GetAbilityButtonSprite(), new UnityAction(ActivateCurrentAbility), GetAttachmentButtonSprite(), new UnityAction(ActivateCurrentAttachment));
			}
			else if (flag || flag2)
			{
				battleCardState2.View.ShowDualActions(GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack), flag ?GetAbilityButtonSprite() : GetAttachmentButtonSprite(), flag ?new UnityAction(ActivateCurrentAbility) : new UnityAction(ActivateCurrentAttachment));
			}
			else
			{
				battleCardState2.View.ShowClassAction(GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack));
			}
		}
	}

	private bool IsClassAbilityActionAvailable(BattleCardState card)
	{
		if (card == null || card.Eliminated || card.AbilityUsed || card.AbilityArmed)
		{
			return false;
		}
		if (card.Card.HeroClass != HeroClass.Assassin && card.Card.HeroClass != HeroClass.Warrior && card.Card.HeroClass != HeroClass.Mage && card.Card.HeroClass != HeroClass.Paladin && card.Card.HeroClass != HeroClass.Hunter && card.Card.HeroClass != HeroClass.Necromancer && card.Card.HeroClass != HeroClass.Priest)
		{
			return false;
		}
		if (card.Card.HeroClass == HeroClass.Necromancer)
		{
			return playerCards.Any(CanReviveWithNecromancer);
		}
		if (card.Card.HeroClass == HeroClass.Hunter)
		{
			IEnumerable<BattleCardState> targets = card.BelongsToPlayer ?cpuCards : playerCards;
			return targets.Any((BattleCardState target) => target != null && !target.Eliminated && !IsHunterMarked(target));
		}
		return true;
	}

	private Sprite GetAbilityButtonSprite()
	{
		return LoadSpriteResource("UI/ability_button");
	}

	private Sprite GetAttackButtonSprite()
	{
		return LoadSpriteResource("UI/attack_button");
	}

	private Sprite GetAttachmentButtonSprite()
	{
		return LoadSpriteResource("UI/attachment_button");
	}

	private static Sprite LoadSpriteResource(string resourcePath)
	{
		if (string.IsNullOrWhiteSpace(resourcePath))
		{
			return null;
		}
		if (spriteResourceCache.TryGetValue(resourcePath, out Sprite cached) && (Object)(object)cached != (Object)null)
		{
			return cached;
		}
		Sprite val = Resources.Load<Sprite>(resourcePath);
		if ((Object)(object)val != (Object)null)
		{
			spriteResourceCache[resourcePath] = val;
			return val;
		}
		Texture2D val2 = Resources.Load<Texture2D>(resourcePath);
		if (!((Object)(object)val2 == (Object)null))
		{
			Sprite generated = Sprite.Create(val2, new Rect(0f, 0f, (float)((Texture)val2).width, (float)((Texture)val2).height), new Vector2(0.5f, 0.5f), 100f);
			generated.name = val2.name;
			generated.hideFlags = HideFlags.DontSave;
			spriteResourceCache[resourcePath] = generated;
			return generated;
		}
		return null;
	}

	private void FinishTurn()
	{
		if (turnOrder.Count > 0 && currentTurnIndex >= 0 && currentTurnIndex < turnOrder.Count)
		{
			BattleCardState battleCardState = turnOrder[currentTurnIndex];
			if (battleCardState.IsSpirit)
			{
				battleCardState.IsSpirit = false;
				battleCardState.Eliminated = true;
				battleCardState.RevivedRound = 0;
				battleCardState.View.SetSelected(selected: false);
				ApplyMightAuraDeathBonuses(battleCardState);
				RefreshPersistentStatus(battleCardState);
				AppendLog("ULTIMO TURNO - " + battleCardState.Card.Name + " svanisce dopo aver agito.");
			}
		}
		if (!CheckEndGame())
		{
			AdvanceComposableGolemTurnCounter();
			AdvanceTurnIndex();
			BeginCurrentTurn();
		}
	}

	private void AdvanceComposableGolemTurnCounter()
	{
		if (activeComposableGolem == null || activeComposableGolem.IsDefeated)
		{
			return;
		}
		if (!activeComposableGolem.EndRound())
		{
			return;
		}

		AppendLog("GOLEM - nuova forma attiva: " + GolemFormName(activeComposableGolem.ActiveForm.Form) + ".");
		BattleCardState golemProxy = cpuCards.FirstOrDefault((BattleCardState card) => IsComposableGolemProxy(card));
		RefreshComposableGolemPawn(golemProxy);
		RefreshInitiativeDisplay();
	}

	private IEnumerator ShowCombatResult(CombatResult result, BattleCardState attacker, BattleCardState defender)
	{
		int attackerStart = attacker?.Card?.Strength ?? 0;
		int defenderStart = defender?.Card?.Strength ?? 0;
		int attackerDelta = result.AttackerTotal - attackerStart;
		int defenderDelta = result.DefenderTotal - defenderStart;
		combatScoreText.text = $"{attackerStart}  VS  {defenderStart}";
		if ((Object)(object)combatDiceText != (Object)null)
		{
			combatDiceText.text = $"+{attackerDelta}  VS  +{defenderDelta}";
			combatDiceText.color = new Color(0.72f, 0.9f, 1f, 1f);
			((Component)combatDiceText).gameObject.SetActive(true);
		}
		combatOutcomeText.text = string.Empty;
		combatOutcomeText.color = Color.white;
		combatResultRoot.SetActive(true);

		yield return WaitForCardInspectionPause(0.28f);

		float duration = Mathf.Clamp(configuration.Animation.CombatResultHold * 0.38f, 0.42f, 0.78f);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = 1f - Mathf.Pow(1f - t, 3f);
			int attackerValue = Mathf.RoundToInt(Mathf.Lerp(attackerStart, result.AttackerTotal, eased));
			int defenderValue = Mathf.RoundToInt(Mathf.Lerp(defenderStart, result.DefenderTotal, eased));
			combatScoreText.text = $"{attackerValue}  VS  {defenderValue}";
			if ((Object)(object)combatDiceText != (Object)null)
			{
				Color diceColor = combatDiceText.color;
				diceColor.a = 1f - eased;
				combatDiceText.color = diceColor;
			}
			yield return null;
		}

		if ((Object)(object)combatDiceText != (Object)null)
		{
			((Component)combatDiceText).gameObject.SetActive(false);
		}
		combatScoreText.text = $"{result.AttackerTotal}  VS  {result.DefenderTotal}";
		bool overkill = result.DefenderIsDefeated && result.AttackerTotal >= result.DefenderTotal * 2;
		combatOutcomeText.text = overkill ? "OVERKILL" : (result.DefenderIsDefeated ? "COLPO A SEGNO" : "DIFESA RIUSCITA");
		combatOutcomeText.color = overkill
			? new Color(1f, 0.25f, 0.18f)
			: (result.DefenderIsDefeated ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.72f, 0.25f));

		yield return WaitForCardInspectionPause(Mathf.Max(0.35f, configuration.Animation.CombatResultHold * 0.42f) + 0.2f);
		combatResultRoot.SetActive(false);
	}

	private IEnumerator ShowAutomaticOutcome(bool guaranteedKill)
	{
		combatScoreText.text = (guaranteedKill ?"100%" : "0%");
		combatOutcomeText.text = (guaranteedKill ?"ELIMINAZIONE CERTA" : "ATTACCO IMPOSSIBILE - TURNO SALTATO");
		combatOutcomeText.color = (guaranteedKill ?new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.38f, 0.25f));
		combatResultRoot.SetActive(true);
		yield return WaitForCardInspectionPause(configuration.Animation.CombatResultHold);
		combatResultRoot.SetActive(false);
	}

	private void AdvanceTurnIndex()
	{
		currentTurnIndex++;
		if (currentTurnIndex >= turnOrder.Count)
		{
			currentTurnIndex = 0;
			roundNumber++;
		}
	}

	private static BattleAuraType DetermineAura(IReadOnlyList<BattleCardState> formation)
	{
		List<BattleCardState> list = formation.Where((BattleCardState card) => card != null).ToList();
		if (list.Count != 3)
		{
			return BattleAuraType.None;
		}
		HeroClass firstClass = list[0].Card.HeroClass;
		if (list.All((BattleCardState card) => card.Card.HeroClass == firstClass))
		{
			return firstClass switch
			{
				HeroClass.Warrior => BattleAuraType.Warrior, 
				HeroClass.Barbarian => BattleAuraType.Barbarian, 
				HeroClass.Paladin => BattleAuraType.Paladin, 
				HeroClass.Rogue => BattleAuraType.Rogue, 
				HeroClass.Assassin => BattleAuraType.Assassin, 
				HeroClass.Hunter => BattleAuraType.Hunter, 
				HeroClass.Mage => BattleAuraType.Mage, 
				HeroClass.Necromancer => BattleAuraType.Necromancer, 
				HeroClass.Priest => BattleAuraType.Priest, 
				_ => BattleAuraType.None, 
			};
		}
		List<ClassFamily> list2 = list.Select((BattleCardState card) => HeroClassFamily.Of(card.Card.HeroClass)).ToList();
		if (list2.Contains(ClassFamily.Might) && list2.Contains(ClassFamily.Cunning) && list2.Contains(ClassFamily.Magic))
		{
			return BattleAuraType.Formation;
		}
		if (list2.All((ClassFamily family) => family == ClassFamily.Might))
		{
			return BattleAuraType.Might;
		}
		if (list2.All((ClassFamily family) => family == ClassFamily.Cunning))
		{
			return BattleAuraType.Cunning;
		}
		if (list2.All((ClassFamily family) => family == ClassFamily.Magic))
		{
			return BattleAuraType.Magic;
		}
		return BattleAuraType.None;
	}

	private void ApplyPlayerAuraVisuals(bool appendLog)
	{
		bool flag = playerAura != BattleAuraType.None;
		foreach (BattleCardState playerCard in playerCards)
		{
			playerCard.View.SetBattleAura(false, Color.clear, string.Empty);
			RefreshPersistentStatus(playerCard);
		}
		if (flag && appendLog)
		{
			AppendLog("AURA ATTIVA - " + AuraDisplayName(playerAura));
			ShowFirstAuraHint(playerAura);
		}
	}

	private void ApplyCpuAuraVisuals(bool appendLog)
	{
		bool flag = cpuAura != BattleAuraType.None;
		foreach (BattleCardState cpuCard in cpuCards)
		{
			cpuCard.View.SetBattleAura(false, Color.clear, string.Empty);
			RefreshPersistentStatus(cpuCard);
		}
		if (flag && appendLog)
		{
			AppendLog("AURA CPU ATTIVA - " + AuraDisplayName(cpuAura));
		}
	}

	private static Color AuraColor(BattleAuraType aura)
	{
		return (Color)(aura switch
		{
			BattleAuraType.Might => new Color(1f, 0.16f, 0.12f), 
			BattleAuraType.Cunning => new Color(0.1f, 0.92f, 0.36f), 
			BattleAuraType.Magic => new Color(0.2f, 0.5f, 1f), 
			BattleAuraType.Formation => new Color(1f, 0.86f, 0.22f), 
			BattleAuraType.Warrior => new Color(0.95f, 0.2f, 0.12f), 
			BattleAuraType.Barbarian => new Color(1f, 0.34f, 0.08f), 
			BattleAuraType.Paladin => new Color(1f, 0.78f, 0.22f), 
			BattleAuraType.Rogue => new Color(0.35f, 1f, 0.55f), 
			BattleAuraType.Assassin => new Color(0.05f, 0.86f, 0.3f), 
			BattleAuraType.Hunter => new Color(0.58f, 1f, 0.22f), 
			BattleAuraType.Mage => new Color(0.25f, 0.58f, 1f), 
			BattleAuraType.Necromancer => new Color(0.35f, 0.85f, 1f), 
			BattleAuraType.Priest => new Color(0.78f, 0.9f, 1f), 
			_ => Color.clear, 
		});
	}

	private static string AuraShortLabel(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => "FORTUZA",
			BattleAuraType.Cunning => "ASTUTA",
			BattleAuraType.Magic => "MAGICA",
			BattleAuraType.Formation => "FORMAZIONE", 
			BattleAuraType.Warrior => "GUERRIERO",
			BattleAuraType.Barbarian => "BARBARO", 
			BattleAuraType.Paladin => "PALADINO",
			BattleAuraType.Rogue => "LADRO",
			BattleAuraType.Assassin => "ASSASSINO",
			BattleAuraType.Hunter => "CACCIATORE",
			BattleAuraType.Mage => "MAGO",
			BattleAuraType.Necromancer => "NECROMANTE",
			BattleAuraType.Priest => "SACERDOTE",
			_ => string.Empty, 
		};
	}

	private static string AuraDisplayName(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => "Famiglia Fortuza",
			BattleAuraType.Cunning => "Famiglia Astuta",
			BattleAuraType.Magic => "Famiglia Magica",
			BattleAuraType.Formation => "Formazione bilanciata", 
			BattleAuraType.Warrior => "Classe Guerriero",
			BattleAuraType.Barbarian => "Classe Barbaro",
			BattleAuraType.Paladin => "Classe Paladino",
			BattleAuraType.Rogue => "Classe Ladro",
			BattleAuraType.Assassin => "Classe Assassino",
			BattleAuraType.Hunter => "Classe Cacciatore",
			BattleAuraType.Mage => "Classe Mago",
			BattleAuraType.Necromancer => "Classe Necromante",
			BattleAuraType.Priest => "Classe Sacerdote",
			_ => "Nessuna", 
		};
	}

	private string AuraStartMessage()
	{
		string obj = ((playerAura == BattleAuraType.None) ?string.Empty : (" Aura attiva: " + AuraDisplayName(playerAura) + "."));
		string text = ((cpuAura == BattleAuraType.None) ?string.Empty : (" Aura CPU: " + AuraDisplayName(cpuAura) + "."));
		return obj + text;
	}

	private void RefreshInitiativeDisplay()
	{
		string text = ((!string.IsNullOrWhiteSpace(currentScenarioDisplayOverride)) ?currentScenarioDisplayOverride.ToUpperInvariant() : (((Object)(object)currentScenario != (Object)null) ?currentScenario.DisplayName.ToUpperInvariant() : "SCENARIO"));
		string text2 = ((playerAura != BattleAuraType.None) ?("  |  AURA " + AuraShortLabel(playerAura)) : string.Empty);
		string text3 = ((cpuAura != BattleAuraType.None) ?("  |  CPU " + AuraShortLabel(cpuAura)) : string.Empty);
		string text4 = deploymentDraftActive ?"SCHIERAMENTO" : (draftActive ?"PREPARAZIONE" : (roundNumber > 0 ?$"ROUND {roundNumber}" : "ROUND 0"));
		string text5 = text + text2 + text3;
		string text6 = $"STANZA {runProgress.RoomsCleared + 1}  |  CPU D{EffectiveCpuHudVigorDieSides()}  |  {text4}  |  {text5}";
		if ((Object)(object)topInfoText != (Object)null)
		{
			RefreshRoomHud(text4, text5);
		}
		if ((Object)(object)roundText != (Object)null)
		{
			roundText.text = text6;
		}
		RefreshPlayerHud();
		RefreshCpuHud();
		if ((Object)(object)campaignZoneRect != (Object)null)
		{
			((Component)campaignZoneRect).gameObject.SetActive(false);
		}
		if ((Object)(object)implementationArchivePanel != (Object)null && implementationArchivePanel.activeSelf)
		{
			RefreshImplementationArchive();
		}
		if ((Object)(object)initiativeTimelineRoot == (Object)null)
		{
			return;
		}
		RestoreTimelineBaseRect();
		List<string> previousTimelineOrder = new List<string>(campaignTimelineOrderKeys);
		List<string> expectedTimelineOrder = BuildCampaignTimelineOrder();
		if (pvpTimelineSlideRoutine != null && previousTimelineOrder.SequenceEqual(expectedTimelineOrder))
			return;

		StopTimelineSlideAnimation();

		for (int num = ((Transform)initiativeTimelineRoot).childCount - 1; num >= 0; num--)
		{
			GameObject childObject = ((Component)((Transform)initiativeTimelineRoot).GetChild(num)).gameObject;
			childObject.SetActive(false);
			Object.Destroy((Object)(object)childObject);
		}
		campaignTimelineOrderKeys.Clear();
		if (turnOrder.Count == 0)
		{
			ResizeTimelineTiles(0);
			return;
		}
		Font builtinResource = AccardND.Battlefield.MmoUiTheme.BodyFont;
		int visibleTimelineTileCount = GetVisibleTimelineTileCount();
		float timelineTileSize = GetTimelineTileSize(visibleTimelineTileCount);
		int golemFormChangeAfterTurns = TurnsUntilComposableGolemFormChange();
		int visibleBattleTurnCount = 0;
		bool golemFormChangeTileAdded = false;
		List<string> currentTimelineOrder = new List<string>(visibleTimelineTileCount);
		for (int i = 0; i < turnOrder.Count; i++)
		{
			int num2 = (currentTurnIndex + i) % turnOrder.Count;
			BattleCardState battleCardState = turnOrder[num2];
			if (!battleCardState.Eliminated && !IsWaitingAfterRevive(battleCardState))
			{
				bool num3 = num2 == currentTurnIndex;
				Image image = CreateImage(color: num3 ?new Color(0.72f, 0.48f, 0.12f, 0.98f) : (battleCardState.BelongsToPlayer ?new Color(0.08f, 0.25f, 0.32f, 0.94f) : new Color(0.32f, 0.1f, 0.12f, 0.94f)), name: "Timeline " + battleCardState.Card.Name, parent: (Transform)(object)initiativeTimelineRoot);
				LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
				ConfigureTimelineTileLayout(layoutElement, timelineTileSize);
				Outline factionOutline = ((Component)image).gameObject.AddComponent<Outline>();
				factionOutline.effectColor = battleCardState.BelongsToPlayer ?new Color(0.1f, 0.82f, 1f, 0.95f) : new Color(1f, 0.16f, 0.12f, 0.95f);
				factionOutline.effectDistance = new Vector2(2.2f, -2.2f);
				image.raycastTarget = true;
				Button button = ((Component)image).gameObject.AddComponent<Button>();
				button.targetGraphic = (Graphic)(object)image;
				BattleCardState inspectedState = battleCardState;
				((UnityEvent)button.onClick).AddListener((UnityAction)delegate
				{
					if (CanInspectBattleCard(inspectedState))
					{
						ShowCardInspection(inspectedState);
					}
				});
				Image image2 = CreateImage("Portrait", ((Component)image).transform, Color.white);
				image2.sprite = battleCardState.Definition.Artwork;
				image2.preserveAspect = false;
				SetRect(image2.rectTransform, new Vector2(0.045f, 0.045f), new Vector2(0.955f, 0.955f));
				if (IsComposableGolemProxy(battleCardState) && activeComposableGolem != null)
				{
					ComposableGolemFormStats activeForm = activeComposableGolem.ActiveForm;
					image.color = GolemFormColor(activeForm.Form);
				}
				if (num3)
				{
					Outline outline = ((Component)image).gameObject.AddComponent<Outline>();
					outline.effectColor = new Color(1f, 0.86f, 0.25f);
					outline.effectDistance = new Vector2(3f, -3f);
				}
				visibleBattleTurnCount++;
				string timelineKey = CampaignTimelineKeyFor(battleCardState);
				currentTimelineOrder.Add(timelineKey);
				campaignTimelineOrderKeys.Add(timelineKey);
				if (!golemFormChangeTileAdded && golemFormChangeAfterTurns > 0 && visibleBattleTurnCount >= golemFormChangeAfterTurns)
				{
					AddGolemFormChangeTimelineTile(timelineTileSize, builtinResource);
					currentTimelineOrder.Add(CampaignGolemTimelineKey());
					campaignTimelineOrderKeys.Add(CampaignGolemTimelineKey());
					golemFormChangeTileAdded = true;
				}
			}
		}
		if (!golemFormChangeTileAdded && golemFormChangeAfterTurns > 0)
		{
			AddGolemFormChangeTimelineTile(timelineTileSize, builtinResource);
			currentTimelineOrder.Add(CampaignGolemTimelineKey());
			campaignTimelineOrderKeys.Add(CampaignGolemTimelineKey());
		}
		ResizeTimelineTiles(visibleTimelineTileCount);
		TryPlayPvpTimelineSlide(previousTimelineOrder, currentTimelineOrder);
	}

	private List<string> BuildCampaignTimelineOrder()
	{
		List<string> order = new List<string>();
		if (turnOrder.Count == 0)
			return order;

		int golemFormChangeAfterTurns = TurnsUntilComposableGolemFormChange();
		int visibleBattleTurnCount = 0;
		bool golemFormChangeTileAdded = false;
		for (int i = 0; i < turnOrder.Count; i++)
		{
			int orderIndex = (currentTurnIndex + i) % turnOrder.Count;
			BattleCardState card = turnOrder[orderIndex];
			if (card == null || card.Eliminated || IsWaitingAfterRevive(card))
				continue;

			visibleBattleTurnCount++;
			order.Add(CampaignTimelineKeyFor(card));
			if (!golemFormChangeTileAdded && golemFormChangeAfterTurns > 0 && visibleBattleTurnCount >= golemFormChangeAfterTurns)
			{
				order.Add(CampaignGolemTimelineKey());
				golemFormChangeTileAdded = true;
			}
		}

		if (!golemFormChangeTileAdded && golemFormChangeAfterTurns > 0)
			order.Add(CampaignGolemTimelineKey());

		return order;
	}

	private string CampaignTimelineKeyFor(BattleCardState card)
	{
		if (card == null)
			return string.Empty;

		int slot = card.BelongsToPlayer ? playerCards.IndexOf(card) : cpuCards.IndexOf(card);
		string side = card.BelongsToPlayer ? "player" : "cpu";
		string cardId = card.Card?.Id ?? card.Card?.Name ?? "card";
		return $"{side}:{slot}:{cardId}";
	}

	private static string CampaignGolemTimelineKey()
	{
		return "campaign:golem-form-change";
	}

	private int TurnsUntilComposableGolemFormChange()
	{
		if (activeComposableGolem == null || activeComposableGolem.IsDefeated)
		{
			return 0;
		}
		return Mathf.Max(1, ComposableGolem.DefaultRoundsPerForm - activeComposableGolem.RoundsInActiveForm);
	}

	private void AddGolemFormChangeTimelineTile(float timelineTileSize, Font font)
	{
		if ((Object)(object)initiativeTimelineRoot == (Object)null || activeComposableGolem == null)
		{
			return;
		}

		ComposableGolemForm nextForm = activeComposableGolem.NextForm.Form;
		Image image = CreateImage("Timeline Golem Form Change", (Transform)(object)initiativeTimelineRoot, GolemFormColor(nextForm));
		LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
		ConfigureTimelineTileLayout(layoutElement, timelineTileSize);

		Outline outline = ((Component)image).gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(1f, 1f, 1f, 0.82f);
		outline.effectDistance = new Vector2(2.5f, -2.5f);

		Text text = CreateText("Turn", ((Component)image).transform, font, 12, (FontStyle)1, (TextAnchor)4);
		text.text = "CAMBIO\n" + GolemFormName(nextForm);
		text.color = Color.white;
		SetRect(text.rectTransform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.98f));
	}

	private GameObject FindTimelineTileForCard(BattleCardState card)
	{
		if ((Object)(object)initiativeTimelineRoot == (Object)null || card == null || card.Card == null)
		{
			return null;
		}
		Transform val = ((Transform)initiativeTimelineRoot).Find("Timeline " + card.Card.Name);
		if (!((Object)(object)val != (Object)null))
		{
			return null;
		}
		return ((Component)val).gameObject;
	}

	private IEnumerator PlayTimelineAwareDefeatAnimation(BattleCardState card, HeroClass killerHeroClass)
	{
		if (card == null || (Object)(object)card.View == (Object)null)
		{
			yield break;
		}

		GameObject timelineTile = FindTimelineTileForCard(card);
		yield return card.View.PlayDefeatAnimation(timelineTile, () =>
		{
			Canvas.ForceUpdateCanvases();
		}, killerHeroClass);
	}

	private static string GolemFormName(ComposableGolemForm form)
	{
		return form switch
		{
			ComposableGolemForm.Iron => "FERRO",
			ComposableGolemForm.Crystal => "CRISTALLO",
			ComposableGolemForm.Glass => "VETRO",
			_ => "FORMA",
		};
	}

	private string FormatPalatirDefenseMessage(BattleCardState attacker, PalatirDefenseResult result)
	{
		if (result.ShieldWasBroken)
		{
			string remaining = activePalatirBoss != null && activePalatirBoss.HasActiveShields
				?$" Scudi rimasti: {string.Join(", ", activePalatirBoss.ActiveShields.Select(PalatirShieldName))}."
				:" Tutti gli scudi sono distrutti: ora Palatir puo perdere HP.";
			return $"{attacker.Card.Name} rompe lo scudo {PalatirShieldName(result.TargetedShield.Value)} di Palatir.{remaining}";
		}

		if (result.Damage > 0)
			return $"{attacker.Card.Name} infligge {result.Damage} danni a Palatir. HP {result.HitPointsAfter}/{activePalatirBoss.MaxHitPoints}.";

		if (activePalatirBoss != null && activePalatirBoss.HasActiveShields)
		{
			string target = result.TargetedShield.HasValue ?PalatirShieldName(result.TargetedShield.Value) : "sconosciuto";
			return $"{attacker.Card.Name} non frantuma lo scudo {target}. Solo chi ha vantaggio sulla famiglia dello scudo puo aprire Palatir.";
		}

		return $"{attacker.Card.Name} non supera la difesa di Palatir. HP {result.HitPointsAfter}/{activePalatirBoss.MaxHitPoints}.";
	}

	private static string PalatirShieldName(ClassFamily family)
	{
		return family switch
		{
			ClassFamily.Might => "Might",
			ClassFamily.Cunning => "Cunning",
			ClassFamily.Magic => "Magic",
			_ => "Scudo",
		};
	}

	private static Color GolemFormColor(ComposableGolemForm form)
	{
		return form switch
		{
			ComposableGolemForm.Iron => new Color(0.78f, 0.68f, 0.48f, 0.98f),
			ComposableGolemForm.Crystal => new Color(0.04f, 0.55f, 0.95f, 0.98f),
			ComposableGolemForm.Glass => new Color(0.08f, 0.78f, 0.66f, 0.98f),
			_ => new Color(0.18f, 0.18f, 0.18f, 0.96f),
		};
	}

	private static Color GolemHealthColor(ComposableGolemForm form)
	{
		return form switch
		{
			ComposableGolemForm.Iron => new Color(0.95f, 0.72f, 0.32f, 0.98f),
			ComposableGolemForm.Crystal => new Color(0.1f, 0.82f, 1f, 0.98f),
			ComposableGolemForm.Glass => new Color(0.42f, 1f, 0.78f, 0.98f),
			_ => new Color(0.9f, 0.12f, 0.12f, 0.98f),
		};
	}

	private float GetTimelineTileSize(int visibleTimelineTileCount = -1)
	{
		Rect val = Screen.safeArea;
		float num = Mathf.Max(1f, val.width);
		val = Screen.safeArea;
		float num2 = Mathf.Max(1f, val.height);
		bool num3 = IsCompactLayout(num / num2, configuration.ResponsiveLayout);
		float num4 = (num3 ?84f : 52f);
		float num5 = (num3 ?48f : 36f);
		if (visibleTimelineTileCount < 0)
		{
			visibleTimelineTileCount = GetVisibleTimelineTileCount();
		}
		if ((Object)(object)initiativeTimelineRoot == (Object)null || visibleTimelineTileCount <= 0)
		{
			return num4;
		}
		val = initiativeTimelineRoot.rect;
		bool vertical = IsTimelineVerticalLayout();
		float num6 = vertical ?val.height : val.width;
		if (num6 <= 0f && (Object)(object)timelineBackgroundRect != (Object)null)
		{
			val = timelineBackgroundRect.rect;
			num6 = (vertical ?val.height : val.width) - 16f;
		}
		if (num6 <= 0f)
		{
			return num4;
		}
		RectOffset padding = TimelinePadding(vertical);
		num6 -= vertical ?padding.vertical : padding.horizontal;
		return Mathf.Clamp((num6 - TimelineTileSpacing * (float)Mathf.Max(0, visibleTimelineTileCount - 1)) / (float)visibleTimelineTileCount, num5, num4);
	}

	private int GetVisibleTimelineTileCount()
	{
		if (turnOrder.Count > 0)
		{
			int count = turnOrder.Count((BattleCardState card) => card != null && !card.Eliminated);
			return count + ((activeComposableGolem != null && !activeComposableGolem.IsDefeated) ?1 : 0);
		}
		if (deploymentOrder.Count > 0)
		{
			return deploymentOrder.Count;
		}
		if (!((Object)(object)initiativeTimelineRoot != (Object)null))
		{
			return 0;
		}
		return ((Transform)initiativeTimelineRoot).childCount;
	}

	private void ResizeTimelineTiles(int timelineTileCount = -1)
	{
		if ((Object)(object)initiativeTimelineRoot == (Object)null)
		{
			return;
		}
		int visibleTileCount = timelineTileCount >= 0 ?timelineTileCount : GetVisibleTimelineTileCount();
		float timelineTileSize = GetTimelineTileSize(visibleTileCount);
		Vector2[] positions = GetTimelineLocalPositions(visibleTileCount, timelineTileSize);
		int visibleIndex = 0;
		for (int i = 0; i < ((Transform)initiativeTimelineRoot).childCount; i++)
		{
			Transform child = ((Transform)initiativeTimelineRoot).GetChild(i);
			RectTransform val = (RectTransform)(object)((child is RectTransform) ?child : null);
			if (!((Object)(object)val == (Object)null))
			{
				if (!((Component)child).gameObject.activeSelf || IsTransientTimelineObject(child))
				{
					continue;
				}
				LayoutElement layoutElement = ((Component)val).GetComponent<LayoutElement>();
				if ((Object)(object)layoutElement == (Object)null)
				{
					layoutElement = ((Component)val).gameObject.AddComponent<LayoutElement>();
				}
				ConfigureTimelineTileLayout(layoutElement, timelineTileSize);
				val.anchorMin = val.anchorMax = new Vector2(0.5f, 0.5f);
				val.pivot = new Vector2(0.5f, 0.5f);
				val.sizeDelta = new Vector2(timelineTileSize, timelineTileSize);
				if (visibleIndex < positions.Length)
				{
					val.anchoredPosition = positions[visibleIndex];
				}
				visibleIndex++;
			}
		}
		ResizeTimelineBackgroundToContent(timelineTileSize, visibleTileCount);
	}

	private const float TimelineTileSpacing = 6f;
	private const float VerticalTimelineTileHorizontalOffset = 0f;

	private static RectOffset TimelinePadding(bool vertical)
	{
		return vertical ?new RectOffset(7, 7, 4, 4) : new RectOffset(4, 4, 2, 2);
	}

	private Vector2[] GetTimelineLocalPositions(int count, float tileSize = -1f)
	{
		Vector2[] positions = new Vector2[Mathf.Max(0, count)];
		if (count <= 0 || (Object)(object)initiativeTimelineRoot == (Object)null)
			return positions;

		if (tileSize <= 0f)
			tileSize = GetTimelineTileSize(count);

		bool vertical = IsTimelineVerticalLayout();
		RectOffset padding = TimelinePadding(vertical);
		Rect rect = initiativeTimelineRoot.rect;
		float totalLength = tileSize * count + TimelineTileSpacing * Mathf.Max(0, count - 1);
		float availableLength = Mathf.Max(0f, (vertical ?rect.height : rect.width) - (vertical ?padding.vertical : padding.horizontal));
		float availableCross = Mathf.Max(0f, (vertical ?rect.width : rect.height) - (vertical ?padding.horizontal : padding.vertical));
		float startOffset = Mathf.Max(0f, (availableLength - totalLength) * 0.5f);
		float crossOffset = Mathf.Max(0f, (availableCross - tileSize) * 0.5f);

		for (int index = 0; index < count; index++)
		{
			if (vertical)
			{
				positions[index] = new Vector2(
					rect.center.x + TimelineVerticalHorizontalOffset(),
					rect.yMax - padding.top - startOffset - tileSize * 0.5f - (tileSize + TimelineTileSpacing) * index);
			}
			else
			{
				positions[index] = new Vector2(
					rect.xMin + padding.left + startOffset + tileSize * 0.5f + (tileSize + TimelineTileSpacing) * index,
					rect.yMax - padding.top - crossOffset - tileSize * 0.5f);
			}
		}

		return positions;
	}

	private static bool IsTransientTimelineObject(Transform child)
	{
		return child != null && child.name == "Timeline Slide VFX";
	}

	private float TimelineVerticalHorizontalOffset()
	{
		if (!IsTimelineVerticalLayout())
			return 0f;
		if (pvpPresentationActive && pvpState != null)
			return pvpState.Phase == AccardND.NetProtocol.PvpClientPhase.Battle ?VerticalTimelineTileHorizontalOffset : 0f;
		return turnOrder.Count > 0 && deploymentOrder.Count == 0 ?VerticalTimelineTileHorizontalOffset : 0f;
	}

	private void ResizeTimelineBackgroundToContent(float timelineTileSize, int visibleTileCount)
	{
		if ((Object)(object)timelineBackgroundRect == (Object)null || (Object)(object)initiativeTimelineRoot == (Object)null)
		{
			return;
		}
		((Component)timelineBackgroundRect).gameObject.SetActive(visibleTileCount > 0);
		if (visibleTileCount <= 0 || !hasTimelineBackgroundBaseRect)
		{
			return;
		}
		bool vertical = IsTimelineVerticalLayout();
		float spacing = TimelineTileSpacing;
		float neededPixels = timelineTileSize * visibleTileCount + spacing * Mathf.Max(0, visibleTileCount - 1);
		if (vertical)
		{
			neededPixels += 14f;
		}
		RectTransform parent = (RectTransform)(object)((Transform)timelineBackgroundRect).parent;
		Rect parentRect = parent.rect;
		float parentLength = Mathf.Max(1f, vertical ?parentRect.height : parentRect.width);
		float baseLength = parentLength * (vertical ?timelineBackgroundBaseMax.y - timelineBackgroundBaseMin.y : timelineBackgroundBaseMax.x - timelineBackgroundBaseMin.x);
		float normalizedLength = Mathf.Clamp(neededPixels / parentLength, 0.01f, baseLength / parentLength);
		if (vertical)
		{
			float parentWidth = Mathf.Max(1f, parentRect.width);
			float neededWidth = timelineTileSize + 14f;
			float normalizedWidth = Mathf.Clamp(neededWidth / parentWidth, 0.01f, timelineBackgroundBaseMax.x - timelineBackgroundBaseMin.x);
			float centerX = (timelineBackgroundBaseMin.x + timelineBackgroundBaseMax.x) * 0.5f + 0.025f;
			float halfWidth = normalizedWidth * 0.5f;
			float centerY = (timelineBackgroundBaseMin.y + timelineBackgroundBaseMax.y) * 0.5f;
			float halfHeight = normalizedLength * 0.5f;
			SetRect(
				timelineBackgroundRect,
				new Vector2(Mathf.Max(timelineBackgroundBaseMin.x, centerX - halfWidth), Mathf.Max(timelineBackgroundBaseMin.y, centerY - halfHeight)),
				new Vector2(Mathf.Min(timelineBackgroundBaseMax.x, centerX + halfWidth), Mathf.Min(timelineBackgroundBaseMax.y, centerY + halfHeight)));
		}
		else
		{
			float center = (timelineBackgroundBaseMin.x + timelineBackgroundBaseMax.x) * 0.5f;
			float half = normalizedLength * 0.5f;
			SetRect(timelineBackgroundRect, new Vector2(Mathf.Max(timelineBackgroundBaseMin.x, center - half), timelineBackgroundBaseMin.y), new Vector2(Mathf.Min(timelineBackgroundBaseMax.x, center + half), timelineBackgroundBaseMax.y));
		}
	}

	private void ConfigureTimelineTileLayout(LayoutElement layoutElement, float timelineTileSize)
	{
		if ((Object)(object)layoutElement == (Object)null)
		{
			return;
		}
		layoutElement.minWidth = timelineTileSize;
		layoutElement.preferredWidth = timelineTileSize;
		layoutElement.flexibleWidth = 0f;
		layoutElement.minHeight = timelineTileSize;
		layoutElement.preferredHeight = timelineTileSize;
		layoutElement.flexibleHeight = 0f;
	}

	private int ChooseCpuTarget(BattleCardState attacker, out string decisionReason)
	{
		GameplayConfiguration gameplay = configuration.Gameplay;
		List<CombatCard> list = new List<CombatCard>(playerCards.Count);
		List<bool> list2 = new List<bool>(playerCards.Count);
		foreach (BattleCardState playerCard in playerCards)
		{
			list.Add(playerCard.Card);
			list2.Add(playerCard.Eliminated);
		}
		CpuDecisionWeights weights = new CpuDecisionWeights(gameplay.KillProbabilityWeight, gameplay.ClassAdvantageWeight, gameplay.WeakerTargetWeight, gameplay.RandomTieBreaker);
		int attackerVigorDieSides = EffectiveVigorDieSides(attacker, runProgress.MasterVigorDieSides);
		CpuTargetDecision cpuTargetDecision = cpuDecisionService.ChooseTarget(attacker.Card, list, list2, attackerVigorDieSides, runProgress.PlayerVigorDieSides, (CpuDifficulty)gameplay.CpuDifficulty, weights, (int targetIndex) => BuildAttackModifiers(attacker, playerCards[targetIndex], defenderAdvantage: false, neutralizeAttackerMatchup: false, updateVisuals: false));
		string text = ((gameplay.CpuDifficulty != CpuDifficultySetting.Easy) ?(cpuTargetDecision.Matchup switch
		{
			MatchupResult.Advantage => "vantaggio di classe", 
			MatchupResult.Disadvantage => "migliore probabilita nonostante lo svantaggio", 
			_ => "migliore probabilita", 
		}) : "scelta casuale");
		string arg = text;
		decisionReason = $"{arg}, {cpuTargetDecision.DefeatProbability:P0} di eliminazione";
		return cpuTargetDecision.TargetIndex;
	}

	private void ShowTargetHints(BattleCardState attacker)
	{
		foreach (BattleCardState cpuCard in cpuCards)
		{
			RefreshPersistentStatus(cpuCard);
			bool unavailable = cpuCard.Eliminated || (abilityTargetMode == AbilityTargetMode.HunterEnemy && IsHunterMarked(cpuCard));
			MatchupResult matchup = IsPalatirBossProxy(cpuCard)
				? MatchupResult.Neutral
				: ClassMatchup.Compare(attacker.Card.HeroClass, cpuCard.Card.HeroClass);
			cpuCard.View.SetTargetHint(unavailable ?((MatchupResult?)null) : new MatchupResult?(matchup));
		}
	}

	private void ClearTargetHints()
	{
		foreach (BattleCardState cpuCard in cpuCards)
		{
			cpuCard.View.SetTargetHint(null);
		}
	}

	private static bool CanReviveWithNecromancer(BattleCardState card)
	{
		if (card != null && card.Eliminated)
		{
			return !card.IsAttachment;
		}
		return false;
	}

	private bool ShouldSkipCurrentRoundTurn(BattleCardState card)
	{
		if (card != null && !card.Eliminated)
		{
			return IsWaitingAfterRevive(card);
		}
		return true;
	}

	private bool IsWaitingAfterRevive(BattleCardState card)
	{
		if (card != null && !card.Eliminated && card.RevivedRound > 0)
		{
			return card.RevivedRound == roundNumber;
		}
		return false;
	}

	private static bool IsCampaignDefeated(BattleCardState card)
	{
		if (card == null || !card.Eliminated)
		{
			return false;
		}
		if (!card.IsAttachment)
		{
			return true;
		}
		if (card.AttachedTo != null)
		{
			return card.AttachedTo.Eliminated;
		}
		return true;
	}
}
}
