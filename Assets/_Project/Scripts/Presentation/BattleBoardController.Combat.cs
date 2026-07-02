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
		inputLocked = true;
		abilityTargetMode = AbilityTargetMode.None;
		attackTargetingActive = false;
		activeAbilityUser = null;
		activeAttachmentSource = null;
		selectedPlayerIndex = -1;
		turnOrder.Clear();
		turnOrder.AddRange(playerCards);
		turnOrder.AddRange(cpuCards);
		SetActiveTurnAura(null);
		playerAura = DetermineAura(playerCards);
		cpuAura = ((currentRoomType != RoomType.Monster || currentMonsterTier > 1) ?DetermineAura(cpuCards) : BattleAuraType.None);
		necromancerSpiritUsed = false;
		ResetRoundAuraUsage();
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
		HashSet<int> usedInitiatives = new HashSet<int>();
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
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
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

	private void BeginCurrentTurn()
	{
		if (CheckEndGame())
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
		SetActiveTurnAura(battleCardState);
		RefreshInitiativeDisplay();
		if (battleCardState.BelongsToPlayer)
		{
			pendingAbilityUser = null;
			attackTargetingActive = false;
			activeAttachmentSource = null;
			((Component)confirmFormationButton).gameObject.SetActive(false);
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
		}
		else
		{
			SetTurnBanner(playerTurn: false, "TURNO CPU  -  " + battleCardState.Card.Name.ToUpperInvariant());
			inputLocked = true;
			selectedPlayerIndex = -1;
			attackTargetingActive = false;
			((Component)abilityButton).gameObject.SetActive(false);
			((Component)attachmentButton).gameObject.SetActive(false);
			((Component)confirmFormationButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			ClearTargetHints();
			SetMessage("Turno CPU: " + battleCardState.Card.Name + " sta scegliendo un bersaglio...");
			UpdateInteractions();
			((MonoBehaviour)this).StartCoroutine(ExecuteCpuTurn(battleCardState));
		}
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
		((Component)confirmFormationButton).gameObject.SetActive(false);
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
		BattleCardState protectingPaladin = cpuCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage("PALADINO CPU: " + protectingPaladin.Card.Name + " devia su di se l'attacco diretto a " + defender.Card.Name + ".");
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
			defender = protectingPaladin;
			protectingPaladin.AbilityArmed = false;
			protectingPaladin.AbilityUsed = true;
			protectingPaladin.ProtectedAlly = null;
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO CPU: " + selfProtectingPaladin.Card.Name + " si difende con vantaggio.");
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			selfProtectingPaladin.AbilityUsed = true;
			selfProtectingPaladin.ProtectedAlly = null;
			RefreshPersistentStatus(selfProtectingPaladin);
		}
		int attackerDieSides = EffectiveVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.MasterVigorDieSides);
		BattleCardState battleCardState = protectingPaladin ?? selfProtectingPaladin;
		CombatModifiers modifiers = BuildAttackModifiers(attacker, defender, battleCardState != null, battleCardState != null);
		CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		if (certainty == CombatCertainty.Impossible)
		{
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: false);
			yield return ShowAutomaticOutcome(guaranteedKill: false);
			PlayAttackResultSfx(attacker, hit: false);
			SetMessage(FormatImpossibleAttackDetailed(attacker, defender, attackerDieSides, defenderDieSides, modifiers) + " Turno saltato.");
			selectedPlayerIndex = -1;
			attacker.View.SetSelected(selected: false);
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		yield return MoveDuelToCenter(attacker, defender);
		if (certainty == CombatCertainty.Guaranteed)
		{
			if (modifiers.SumAttackerVigor)
			{
				attacker.AbilityArmed = false;
				attacker.AbilityUsed = true;
				TriggerMagicAuraAfterAbility();
			}
			((Component)abilityButton).gameObject.SetActive(false);
			((Component)attachmentButton).gameObject.SetActive(false);
			yield return ShowAutomaticOutcome(guaranteedKill: true);
			PlayAttackResultSfx(attacker, hit: true);
			defender.Eliminated = true;
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: true);
			PlayDeathCardSfx();
			yield return defender.View.PlayDefeatAnimation();
			yield return ReturnDuelSurvivors(attacker, defender);
			SetMessage("100%: " + attacker.Card.Name + " elimina direttamente " + defender.Card.Name + ". Nessun dado necessario.");
			selectedPlayerIndex = -1;
			attacker.View.SetSelected(selected: false);
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		if (modifiers.SumAttackerVigor)
		{
			attacker.AbilityArmed = false;
			attacker.AbilityUsed = true;
			TriggerMagicAuraAfterAbility();
		}
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, "DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		yield return ShowCombatResult(result);
		PlayAttackResultSfx(attacker, result.DefenderIsDefeated);
		if (result.DefenderIsDefeated)
		{
			defender.Eliminated = true;
			PlayDeathCardSfx();
			yield return defender.View.PlayDefeatAnimation();
		}
		yield return ReturnDuelSurvivors(attacker, defender);
		string combatLog = FormatResultDetailed("TU", attacker, defender, result, modifiers);
		ConsumeVigorPenalties(attacker, defender);
		UpdatePostAttackClassState(attacker, result.DefenderIsDefeated);
		SetMessage(combatLog);
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecuteCpuTurn(BattleCardState attacker)
	{
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuThinkDelay);
		if (IsComposableGolemProxy(attacker))
		{
			yield return ExecuteComposableGolemTurn(attacker);
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
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
		}
		string decisionReason;
		int index = ChooseCpuTarget(attacker, out decisionReason);
		BattleCardState defender = playerCards[index];
		SetMessage("CPU: " + attacker.Card.Name + " sceglie " + defender.Card.Name + " - " + decisionReason + ".");
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
		BattleCardState protectingPaladin = playerCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage("PALADINO: " + protectingPaladin.Card.Name + " devia su di se l'attacco diretto a " + defender.Card.Name + ".");
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
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
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
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
		CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		if (certainty == CombatCertainty.Impossible)
		{
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: false);
			yield return ShowAutomaticOutcome(guaranteedKill: false);
			PlayAttackResultSfx(attacker, hit: false);
			SetMessage(FormatImpossibleAttackDetailed(attacker, defender, attackerDieSides, defenderDieSides, modifiers) + " La CPU salta il turno.");
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		yield return MoveDuelToCenter(attacker, defender);
		if (certainty == CombatCertainty.Guaranteed)
		{
			yield return ShowAutomaticOutcome(guaranteedKill: true);
			PlayAttackResultSfx(attacker, hit: true);
			defender.Eliminated = true;
			ConsumeVigorPenalties(attacker, defender);
			UpdatePostAttackClassState(attacker, defeatedTarget: true);
			if (!TryCreateNecromancerSpirit(defender))
			{
				PlayDeathCardSfx();
				yield return defender.View.PlayDefeatAnimation();
			}
			yield return ReturnDuelSurvivors(attacker, defender);
			SetMessage("100%: " + attacker.Card.Name + " elimina direttamente " + defender.Card.Name + ". Nessun dado necessario.");
			yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, result.AttackerRoll, "ATTACCO CPU", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, result.DefenderRoll, "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		yield return ShowCombatResult(result);
		PlayAttackResultSfx(attacker, result.DefenderIsDefeated);
		if (result.DefenderIsDefeated)
		{
			defender.Eliminated = true;
			if (!TryCreateNecromancerSpirit(defender))
			{
				PlayDeathCardSfx();
				yield return defender.View.PlayDefeatAnimation();
			}
		}
		yield return ReturnDuelSurvivors(attacker, defender);
		string combatLog = FormatResultDetailed("CPU", attacker, defender, result, modifiers);
		ConsumeVigorPenalties(attacker, defender);
		UpdatePostAttackClassState(attacker, result.DefenderIsDefeated);
		SetMessage(combatLog);
		if (playerAura == BattleAuraType.Paladin && paladinProtectionUser != null && !paladinProtectionUser.Eliminated && !attacker.Eliminated)
		{
			yield return ExecutePaladinCounter(paladinProtectionUser, attacker);
		}
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstComposableGolem(BattleCardState attacker, BattleCardState golemProxy)
	{
		int attackerDieSides = EffectiveVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, golemProxy, defenderAdvantage: false, neutralizeAttackerMatchup: true);
		yield return MoveDuelToCenter(attacker, golemProxy);
		VigorRollResult attackerRoll = RollGolemAttackerVigor(attackerDieSides, modifiers);
		int attackerTotal = attacker.Card.Strength + attackerRoll.SelectedRoll + modifiers.AttackerFlatBonus;
		ComposableGolemDefenseResult golemResult = activeComposableGolem.DefendAgainst(attackerTotal);
		VigorRollResult golemRoll = SingleRoll(golemResult.Form.VigorDieSides, golemResult.VigorRoll);
		CombatResult result = new CombatResult(attackerRoll, golemRoll, attackerTotal, golemResult.DefenseTotal);
		if (modifiers.SumAttackerVigor)
		{
			attacker.AbilityArmed = false;
			attacker.AbilityUsed = true;
			TriggerMagicAuraAfterAbility();
		}
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, attackerRoll, "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		golemProxy.View.PlayVigorRoll(diceCatalog, golemResult.Form.VigorDieSides, golemRoll, "DIFESA " + GolemFormName(golemResult.Form.Form), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		yield return ShowCombatResult(result);
		PlayAttackResultSfx(attacker, golemResult.Damage > 0);
		UpdateComposableGolemHealthBar(golemProxy);
		if (activeComposableGolem.IsDefeated)
		{
			golemProxy.Eliminated = true;
			PlayDeathCardSfx();
			yield return golemProxy.View.PlayDefeatAnimation();
		}
		else
		{
			golemProxy.View.SetStatus($"HP {activeComposableGolem.HitPoints}/{activeComposableGolem.MaxHitPoints}", GolemFormColor(golemResult.Form.Form));
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
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
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
		SetMessage("GOLEM COMPONIBILE: " + GolemFormName(activeComposableGolem.ActiveForm.Form) + " colpisce la carta piu alta: " + defender.Card.Name + ".");
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.CpuDecisionReveal);
		yield return MoveDuelToCenter(golemProxy, defender);
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		ComposableGolemAttackResult golemResult = activeComposableGolem.Attack(defender.Card, defenderDieSides);
		VigorRollResult golemRoll = SingleRoll(golemResult.Form.VigorDieSides, golemResult.VigorRoll);
		VigorRollResult defenderRoll = SingleRoll(defenderDieSides, golemResult.TargetVigorRoll);
		CombatResult result = new CombatResult(golemRoll, defenderRoll, golemResult.AttackTotal, golemResult.TargetDefenseTotal);
		PlayRollingDiceSfx();
		golemProxy.View.PlayVigorRoll(diceCatalog, golemResult.Form.VigorDieSides, golemRoll, "ATTACCO " + GolemFormName(golemResult.Form.Form), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, defenderRoll, "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		yield return ShowCombatResult(result);
		if (golemResult.TargetIsDefeated)
		{
			defender.Eliminated = true;
			if (!TryCreateNecromancerSpirit(defender))
			{
				PlayDeathCardSfx();
				yield return defender.View.PlayDefeatAnimation();
			}
		}
		yield return ReturnDuelSurvivors(golemProxy, defender);
		SetMessage(golemResult.TargetIsDefeated
			?$"GOLEM {GolemFormName(golemResult.Form.Form)}: {defender.Card.Name} viene travolto."
			:$"GOLEM {GolemFormName(golemResult.Form.Form)}: {defender.Card.Name} resiste.");
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
		FinishTurn();
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
		((MonoBehaviour)this).StartCoroutine(attacker.View.MoveToDuelPoint(worldPosition, 0.34f, 1.16f));
		((MonoBehaviour)this).StartCoroutine(defender.View.MoveToDuelPoint(worldPosition2, 0.34f, 1.16f));
		yield return (object)new WaitForSecondsRealtime(0.37f);
	}

	private IEnumerator ReturnDuelSurvivors(BattleCardState attacker, BattleCardState defender)
	{
		bool num = attacker != null && !attacker.Eliminated;
		bool flag = defender != null && !defender.Eliminated;
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
			yield return (object)new WaitForSecondsRealtime(0.28f);
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
		int num = attacker.PendingAttackBonus + attacker.PermanentCombatBonus;
		int defenderFlatBonus = defender.PermanentCombatBonus + PendingDefenseBonus(defender);
		bool flag = ClassAbilitiesEnabled(attacker);
		if (attacker.BelongsToPlayer && playerAura == BattleAuraType.Warrior && attacker.Card.HeroClass == HeroClass.Warrior && attacker.AbilityArmed)
		{
			num++;
		}
		int num2 = HunterMarkAttackBonus(attacker, defender);
		if (num2 > 0)
		{
			num += num2;
			if (updateVisuals)
			{
				attacker.View.SetStatus($"PREDA +{num2}", new Color(1f, 0.72f, 0.25f));
			}
		}
		else if (attacker.Card.HeroClass == HeroClass.Rogue && classBalance.RogueRerollsOnes && flag && updateVisuals)
		{
			attacker.View.SetStatus("REROLL 1 ATTIVO", new Color(0.75f, 0.9f, 1f));
		}
		bool forceAttackerAdvantage = false;
		if (attacker.BelongsToPlayer && playerAura == BattleAuraType.Cunning && HeroClassFamily.Of(attacker.Card.HeroClass) == ClassFamily.Cunning && IsMarkedOrInhibitedForCunning(defender))
		{
			forceAttackerAdvantage = true;
			if (updateVisuals)
			{
				attacker.View.SetStatus("AURA CUNNING", new Color(0.75f, 0.65f, 1f));
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
		return new CombatModifiers(flag && attacker.AbilityArmed && attacker.Card.HeroClass == HeroClass.Warrior, defenderAdvantage, flag && classBalance.RogueRerollsOnes && attacker.Card.HeroClass == HeroClass.Rogue, playerAura == BattleAuraType.Rogue && attacker.BelongsToPlayer && attacker.Card.HeroClass == HeroClass.Rogue, num, defenderFlatBonus, neutralizeAttackerMatchup, forceAttackerAdvantage);
	}

	private bool ClassAbilitiesEnabled(BattleCardState card)
	{
		if (card != null && !card.BelongsToPlayer && currentRoomType == RoomType.Monster)
		{
			return currentMonsterTier > 1;
		}
		return true;
	}

	private bool IsMarkedOrInhibitedForCunning(BattleCardState target)
	{
		if (target != null)
		{
			if (!target.WasInhibited && target.InhibitedTurns <= 0)
			{
				return HunterMarkCount(target) > 0;
			}
			return true;
		}
		return false;
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
		SetMessage("AURA NECROMANCER: " + defeated.Card.Name + " resta come Spirito e avra un ultimo turno.");
		return true;
	}

	private IEnumerator ExecutePaladinCounter(BattleCardState paladin, BattleCardState target)
	{
		int num = EffectiveVigorDieSides(paladin, runProgress.PlayerVigorDieSides);
		int num2 = EffectiveDefenseVigorDieSides(target, runProgress.MasterVigorDieSides);
		CombatModifiers modifiers = new CombatModifiers(sumAttackerVigor: false, defenderAdvantage: false, rerollAttackerOnes: false, rerollAttackerTwos: false, 1);
		CombatResult result = combatResolver.ResolveAttack(paladin.Card, target.Card, num, num2, modifiers);
		SetMessage("AURA PALADINO: " + paladin.Card.Name + " contrattacca " + target.Card.Name + " con +1.");
		PlayRollingDiceSfx();
		paladin.View.PlayVigorRoll(diceCatalog, num, result.AttackerRoll, "CONTRATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		target.View.PlayVigorRoll(diceCatalog, num2, result.DefenderRoll, "DIFESA CPU", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		yield return ShowCombatResult(result);
		PlayAttackResultSfx(paladin, result.DefenderIsDefeated);
		if (result.DefenderIsDefeated)
		{
			target.Eliminated = true;
			PlayDeathCardSfx();
			yield return target.View.PlayDefeatAnimation();
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
		if (attacker.BelongsToPlayer && playerAura == BattleAuraType.Might && !mightAuraUsedThisRound && HeroClassFamily.Of(attacker.Card.HeroClass) == ClassFamily.Might && !defeatedTarget)
		{
			mightAuraUsedThisRound = true;
			attacker.PermanentCombatBonus++;
			RefreshPersistentStatus(attacker);
		}
		else if (ClassAbilitiesEnabled(attacker) && attacker.Card.HeroClass == HeroClass.Barbarian && !defeatedTarget)
		{
			int pendingAttackBonus = ((playerAura == BattleAuraType.Barbarian && attacker.BelongsToPlayer) ?(configuration.ClassBalance.BarbarianRageBonus + 1) : configuration.ClassBalance.BarbarianRageBonus);
			attacker.PendingAttackBonus = pendingAttackBonus;
			attacker.PendingAttackBonusKind = PendingAttackBonusKind.Fury;
			RefreshPersistentStatus(attacker);
			PlayBarbarianFurySfx();
		}
		else
		{
			_ = HunterMarkBonusForTarget(attacker);
			_ = 0;
			RefreshPersistentStatus(attacker);
		}
	}

	private static int DisplayStrength(BattleCardState card)
	{
		return card.Card.Strength + card.PendingAttackBonus + card.PermanentCombatBonus;
	}

	private static BattleCardState ChooseHighestThreat(IEnumerable<BattleCardState> cards, bool includeEliminated)
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
		if (card.IsSpirit)
		{
			list.Add(new PrototypeCardView.StatusToken("SPIRITO", new Color(0.65f, 0.75f, 1f)));
		}
		if (IsWaitingAfterRevive(card))
		{
			list.Add(new PrototypeCardView.StatusToken("RIALZATA", new Color(0.45f, 1f, 0.82f)));
		}
		if (card.InhibitedTurns > 0)
		{
			list.Add(new PrototypeCardView.StatusToken("INIBITO", new Color(0.6f, 0.5f, 1f)));
		}
		if (card.PendingVigorStepPenalty > 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"DADO -{card.PendingVigorStepPenalty}", new Color(0.55f, 0.8f, 1f)));
		}
		if (card.PermanentCombatBonus > 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"ATTACH +{card.PermanentCombatBonus}", new Color(0.7f, 1f, 0.45f)));
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
			list.Add(new PrototypeCardView.StatusToken($"MARCATO +{num}", new Color(1f, 0.65f, 0.2f)));
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
				((Component)cancelActionButton).gameObject.SetActive(true);
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
		((Component)confirmFormationButton).gameObject.SetActive(false);
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
			((Component)cancelActionButton).gameObject.SetActive(true);
			SetMessage("ABILITA ASSASSINO: scegli un nemico da inibire.");
			UpdateInteractions();
			break;
		case HeroClass.Warrior:
			battleCardState.AbilityArmed = true;
			attackTargetingActive = true;
			((Component)cancelActionButton).gameObject.SetActive(true);
			ShowTargetHints(battleCardState);
			SetMessage("ABILITA GUERRIERO: " + battleCardState.Card.Name + " sommera due dadi nel prossimo attacco.");
			break;
		case HeroClass.Mage:
			battleCardState.AbilityArmed = true;
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.MageEnemy;
			((Component)cancelActionButton).gameObject.SetActive(true);
			SetMessage("ABILITA MAGO: scegli un nemico a cui abbassare il dado Vigore.");
			UpdateInteractions();
			break;
		case HeroClass.Paladin:
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.PaladinAlly;
			((Component)cancelActionButton).gameObject.SetActive(true);
			SetMessage("ABILITA PALADINO: scegli una pedina alleata o " + battleCardState.Card.Name + " stesso da proteggere.");
			break;
		case HeroClass.Hunter:
			battleCardState.AbilityArmed = true;
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.HunterEnemy;
			((Component)cancelActionButton).gameObject.SetActive(true);
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
			((Component)cancelActionButton).gameObject.SetActive(true);
			SetMessage("ABILITA NEGROMANTE: scegli una carta alleata eliminata da rialzare.");
			UpdateInteractions();
			break;
		case HeroClass.Priest:
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.PriestAlly;
			((Component)cancelActionButton).gameObject.SetActive(true);
			SetMessage("ABILITA SACERDOTE: scegli una carta alleata da benedire.");
			UpdateInteractions();
			break;
		case HeroClass.Rogue:
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
				((Component)cancelActionButton).gameObject.SetActive(true);
				ClearTargetHints();
				SetMessage($"ATTACH: sacrifica {battleCardState.Card.Name} per dare +{AttachmentBonus(battleCardState)} a una carta alleata.");
				UpdateInteractions();
			}
		}
	}

	private bool CanUseAttachment(BattleCardState card)
	{
		if (card != null && !card.Eliminated && card.BelongsToPlayer && card.Card.Strength >= 2 && card.Card.Strength < 5)
		{
			return playerCards.Any((BattleCardState ally) => CanTargetAttachment(card, ally));
		}
		return false;
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
		battleCardState.PendingVigorStepPenalty = Math.Max(battleCardState.PendingVigorStepPenalty, num);
		card.AbilityUsed = true;
		RefreshPersistentStatus(battleCardState);
		message = $"CPU MAGO: {card.Card.Name} abbassa il Vigore di {battleCardState.Card.Name} di {num} step.";
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
		PlayHunterMarkSfx();
		message = $"CPU CACCIATORE: {card.Card.Name} marca {battleCardState.Card.Name}. Preda persistente: chi lo attacca prende +{HunterMarkValueFor(card)}.";
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
		message = "CPU NEGROMANTE: " + card.Card.Name + " rialza " + battleCardState.Card.Name + ".";
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
		RefreshCardActionOverlays();
		UpdateInteractions();
		int num = AttachmentBonus(source);
		target.PermanentCombatBonus += num;
		RefreshPersistentStatus(target);
		source.Eliminated = true;
		source.IsAttachment = true;
		source.AttachedTo = target;
		source.View.SetSelected(selected: false);
		SetMessage($"ATTACH: {source.Card.Name} viene sacrificata e potenzia {target.Card.Name} di +{num} per tutto il fight.");
		AppendLog($"ATTACH - {source.Card.Name} sacrificata: {target.Card.Name} ottiene +{num} permanente.");
		PlayDeathCardSfx();
		yield return source.View.PlayDefeatAnimation();
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
		selectedPlayerIndex = -1;
		FinishTurn();
	}

	private IEnumerator ExecuteCpuAttachment(BattleCardState source, BattleCardState target)
	{
		int num = AttachmentBonus(source);
		target.PermanentCombatBonus += num;
		RefreshPersistentStatus(target);
		source.Eliminated = true;
		source.IsAttachment = true;
		source.AttachedTo = target;
		source.View.SetSelected(selected: false);
		SetMessage($"CPU ATTACH: {source.Card.Name} viene sacrificata e potenzia {target.Card.Name} di +{num} per tutto il fight.");
		AppendLog($"CPU ATTACH - {source.Card.Name} sacrificata: {target.Card.Name} ottiene +{num} permanente.");
		PlayDeathCardSfx();
		yield return source.View.PlayDefeatAnimation();
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.TurnResultPause);
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
			((Component)confirmFormationButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			draftViews[pendingDeploymentIndex].ShowConfirmCancelActions(confirmActionSprite, cancelActionSprite, new UnityAction(ConfirmFormation), new UnityAction(CancelPendingAction));
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
			((Component)confirmFormationButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			pendingAbilityUser.View.ShowConfirmCancelActions(confirmActionSprite, cancelActionSprite, new UnityAction(ConfirmFormation), new UnityAction(CancelPendingAction));
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
		Sprite val = Resources.Load<Sprite>(resourcePath);
		if ((Object)(object)val != (Object)null)
		{
			return val;
		}
		Texture2D val2 = Resources.Load<Texture2D>(resourcePath);
		if (!((Object)(object)val2 == (Object)null))
		{
			return Sprite.Create(val2, new Rect(0f, 0f, (float)((Texture)val2).width, (float)((Texture)val2).height), new Vector2(0.5f, 0.5f), 100f);
		}
		return null;
	}

	private IEnumerator ExecuteAssassinSummon(BattleCardState assassin)
	{
		inputLocked = true;
		((Component)abilityButton).gameObject.SetActive(false);
		RefreshCardActionOverlays();
		UpdateInteractions();
		CardDefinition definition = TakeReserveCard();
		BattleCardState summoned = AddCard(playerCards, playerRow, definition, belongsToPlayer: true, playerCards.Count);
		summoned.AbilityUsed = true;
		HashSet<int> usedInitiatives = new HashSet<int>(turnOrder.Select((BattleCardState card) => card.Initiative));
		summoned.Initiative = RollUniqueInitiative(configuration.Gameplay.InitiativeDieSides, usedInitiatives);
		summoned.TieBreaker = random.NextInclusive(1, 10000);
		turnOrder.Add(summoned);
		ApplyResponsiveLayout();
		PlayPawnEnteringBattlefieldSfx(summoned);
		SetMessage("ABILITA ASSASSINO: " + assassin.Card.Name + " evoca " + summoned.Card.Name + " dalla riserva.");
		PlayRollingDiceSfx();
		summoned.View.PlayDiceRoll(diceCatalog, configuration.Gameplay.InitiativeDieSides, summoned.Initiative, "NUOVA INIZIATIVA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		summoned.View.SetInitiative(summoned.Initiative);
		RefreshInitiativeDisplay();
		inputLocked = false;
		RefreshAbilityButton(assassin);
		UpdateInteractions();
	}

	private void ReplaceMageFromReserve(BattleCardState mage)
	{
		int num = playerCards.IndexOf(mage);
		if (num >= 0)
		{
			CardDefinition definition = TakeReserveCard();
			PrototypeCardView prototypeCardView = PrototypeCardView.CreateBattlefieldPreview((Transform)(object)playerRow, definition, configuration);
			BattleCardState replacement = new BattleCardState(definition, prototypeCardView, belongsToPlayer: true)
			{
				Initiative = mage.Initiative,
				TieBreaker = mage.TieBreaker,
				AbilityUsed = true
			};
			((UnityEvent)prototypeCardView.Button.onClick).AddListener((UnityAction)delegate
			{
				HandlePlayerCardClick(replacement);
			});
			playerCards[num] = replacement;
			turnOrder[currentTurnIndex] = replacement;
			Object.Destroy((Object)(object)((Component)mage.View).gameObject);
			replacement.View.SetInitiative(replacement.Initiative);
			replacement.View.SetSelected(selected: true);
			PlayPawnEnteringBattlefieldSfx(replacement);
			SetActiveTurnAura(replacement);
			selectedPlayerIndex = num;
			ApplyResponsiveLayout();
			ClearTargetHints();
			ShowTargetHints(replacement);
			RefreshAbilityButton(replacement);
			UpdateInteractions();
			SetMessage("ABILITA MAGO: " + mage.Card.Name + " viene sostituito da " + replacement.Card.Name + ". Ora scegli il bersaglio.");
		}
	}

	private CardDefinition TakeReserveCard()
	{
		CardDefinition result = playerReserve[0];
		playerReserve.RemoveAt(0);
		return result;
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
				RefreshPersistentStatus(battleCardState);
				AppendLog("SPIRITO - " + battleCardState.Card.Name + " svanisce dopo il suo ultimo turno.");
			}
		}
		if (!CheckEndGame())
		{
			AdvanceTurnIndex();
			BeginCurrentTurn();
		}
	}

	private IEnumerator ShowCombatResult(CombatResult result)
	{
		combatScoreText.text = $"{result.AttackerTotal}  VS  {result.DefenderTotal}";
		combatOutcomeText.text = (result.DefenderIsDefeated ?"COLPO A SEGNO" : "DIFESA RIUSCITA");
		combatOutcomeText.color = (result.DefenderIsDefeated ?new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.72f, 0.25f));
		combatResultRoot.SetActive(true);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.CombatResultHold);
		combatResultRoot.SetActive(false);
	}

	private IEnumerator ShowAutomaticOutcome(bool guaranteedKill)
	{
		combatScoreText.text = (guaranteedKill ?"100%" : "0%");
		combatOutcomeText.text = (guaranteedKill ?"ELIMINAZIONE CERTA" : "ATTACCO IMPOSSIBILE - TURNO SALTATO");
		combatOutcomeText.color = (guaranteedKill ?new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.38f, 0.25f));
		combatResultRoot.SetActive(true);
		yield return (object)new WaitForSecondsRealtime(configuration.Animation.CombatResultHold);
		combatResultRoot.SetActive(false);
	}

	private void AdvanceTurnIndex()
	{
		currentTurnIndex++;
		if (currentTurnIndex >= turnOrder.Count)
		{
			currentTurnIndex = 0;
			roundNumber++;
			ResetRoundAuraUsage();
			if (activeComposableGolem != null && !activeComposableGolem.IsDefeated && activeComposableGolem.EndRound())
			{
				AppendLog("GOLEM - nuova forma attiva: " + GolemFormName(activeComposableGolem.ActiveForm.Form) + ".");
				if ((Object)(object)activeComposableGolemPreview != (Object)null)
				{
					activeComposableGolemPreview.SetActiveForm(activeComposableGolem.ActiveForm.Form);
				}
				BattleCardState golemProxy = cpuCards.FirstOrDefault((BattleCardState card) => IsComposableGolemProxy(card));
				UpdateComposableGolemHealthBar(golemProxy);
			}
		}
	}

	private void ResetRoundAuraUsage()
	{
		mightAuraUsedThisRound = false;
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
		Color color = AuraColor(playerAura);
		bool flag = playerAura != BattleAuraType.None;
		foreach (BattleCardState playerCard in playerCards)
		{
			playerCard.View.SetBattleAura(flag && !playerCard.Eliminated, color, string.Empty);
			RefreshPersistentStatus(playerCard);
		}
		if (flag && appendLog)
		{
			AppendLog("AURA ATTIVA - " + AuraDisplayName(playerAura));
		}
	}

	private void ApplyCpuAuraVisuals(bool appendLog)
	{
		Color color = AuraColor(cpuAura);
		bool flag = cpuAura != BattleAuraType.None;
		foreach (BattleCardState cpuCard in cpuCards)
		{
			cpuCard.View.SetBattleAura(flag && !cpuCard.Eliminated, color, string.Empty);
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
			BattleAuraType.Might => "MIGHT", 
			BattleAuraType.Cunning => "CUNNING", 
			BattleAuraType.Magic => "MAGIC", 
			BattleAuraType.Formation => "FORMAZIONE", 
			BattleAuraType.Warrior => "WARRIOR", 
			BattleAuraType.Barbarian => "BARBARO", 
			BattleAuraType.Paladin => "PALADIN", 
			BattleAuraType.Rogue => "ROGUE", 
			BattleAuraType.Assassin => "ASSASSIN", 
			BattleAuraType.Hunter => "HUNTER", 
			BattleAuraType.Mage => "MAGE", 
			BattleAuraType.Necromancer => "NECRO", 
			BattleAuraType.Priest => "PRIEST", 
			_ => string.Empty, 
		};
	}

	private static string AuraDisplayName(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => "Famiglia Might", 
			BattleAuraType.Cunning => "Famiglia Cunning", 
			BattleAuraType.Magic => "Famiglia Magic", 
			BattleAuraType.Formation => "Formazione bilanciata", 
			BattleAuraType.Warrior => "Classe Warrior", 
			BattleAuraType.Barbarian => "Classe Barbarian", 
			BattleAuraType.Paladin => "Classe Paladin", 
			BattleAuraType.Rogue => "Classe Rogue", 
			BattleAuraType.Assassin => "Classe Assassin", 
			BattleAuraType.Hunter => "Classe Hunter", 
			BattleAuraType.Mage => "Classe Mage", 
			BattleAuraType.Necromancer => "Classe Necromancer", 
			BattleAuraType.Priest => "Classe Priest", 
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
		string text6 = $"STANZA {runProgress.RoomsCleared + 1}  |  CPU D{runProgress.MasterVigorDieSides}  |  {text4}  |  {text5}";
		if ((Object)(object)topInfoText != (Object)null)
		{
			RefreshRoomHud(text4, text5);
		}
		if ((Object)(object)roundText != (Object)null)
		{
			roundText.text = text6;
		}
		RefreshPlayerHud();
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
		for (int num = ((Transform)initiativeTimelineRoot).childCount - 1; num >= 0; num--)
		{
			Object.Destroy((Object)(object)((Component)((Transform)initiativeTimelineRoot).GetChild(num)).gameObject);
		}
		if (turnOrder.Count == 0)
		{
			return;
		}
		Font builtinResource = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		float timelineTileSize = GetTimelineTileSize();
		for (int i = 0; i < turnOrder.Count; i++)
		{
			int num2 = (currentTurnIndex + i) % turnOrder.Count;
			BattleCardState battleCardState = turnOrder[num2];
			if (!battleCardState.Eliminated && !IsWaitingAfterRevive(battleCardState))
			{
				bool num3 = num2 == currentTurnIndex;
				Image image = CreateImage(color: num3 ?new Color(0.72f, 0.48f, 0.12f, 0.98f) : (battleCardState.BelongsToPlayer ?new Color(0.08f, 0.25f, 0.32f, 0.94f) : new Color(0.32f, 0.1f, 0.12f, 0.94f)), name: "Timeline " + battleCardState.Card.Name, parent: (Transform)(object)initiativeTimelineRoot);
				LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
				layoutElement.minWidth = timelineTileSize;
				layoutElement.preferredWidth = timelineTileSize;
				layoutElement.flexibleWidth = 0f;
				Image image2 = CreateImage("Portrait", ((Component)image).transform, Color.white);
				image2.sprite = battleCardState.Definition.Artwork;
				image2.preserveAspect = true;
				SetRect(image2.rectTransform, new Vector2(0.02f, 0.08f), new Vector2(0.36f, 0.92f));
				string arg = (battleCardState.BelongsToPlayer ?"TU" : "CPU");
				Text text7 = CreateText("Turn", ((Component)image).transform, builtinResource, 20, (FontStyle)1, (TextAnchor)4);
				if (IsComposableGolemProxy(battleCardState) && activeComposableGolem != null)
				{
					ComposableGolemFormStats activeForm = activeComposableGolem.ActiveForm;
					text7.text = $"GOLEM\n{GolemFormName(activeForm.Form)}\n{battleCardState.Initiative}";
					text7.fontSize = 13;
					image.color = GolemFormColor(activeForm.Form);
				}
				else
				{
					text7.text = $"{arg}\n{battleCardState.Initiative}";
				}
				text7.color = Color.white;
				SetRect(text7.rectTransform, new Vector2(0.38f, 0.02f), new Vector2(0.98f, 0.98f));
				if (num3)
				{
					Outline outline = ((Component)image).gameObject.AddComponent<Outline>();
					outline.effectColor = new Color(1f, 0.86f, 0.25f);
					outline.effectDistance = new Vector2(3f, -3f);
				}
			}
		}
	}

	private void AppendComposableGolemTimelineTile(Font font, float timelineTileSize)
	{
		if (activeComposableGolem == null || activeComposableGolem.IsDefeated || (Object)(object)initiativeTimelineRoot == (Object)null)
		{
			return;
		}
		ComposableGolemFormStats activeForm = activeComposableGolem.ActiveForm;
		ComposableGolemFormStats nextForm = activeComposableGolem.NextForm;
		Image image = CreateImage("Timeline Golem Form", (Transform)(object)initiativeTimelineRoot, GolemFormColor(activeForm.Form));
		LayoutElement layoutElement = ((Component)image).gameObject.AddComponent<LayoutElement>();
		layoutElement.minWidth = timelineTileSize * 1.85f;
		layoutElement.preferredWidth = timelineTileSize * 1.85f;
		layoutElement.flexibleWidth = 0f;
		Text title = CreateText("Title", ((Component)image).transform, font, 15, (FontStyle)1, (TextAnchor)4);
		title.text = "GOLEM";
		title.color = Color.white;
		SetRect(title.rectTransform, new Vector2(0.04f, 0.58f), new Vector2(0.34f, 0.96f));
		Text active = CreateText("Active Form", ((Component)image).transform, font, 18, (FontStyle)1, (TextAnchor)4);
		active.text = GolemFormName(activeForm.Form);
		active.color = Color.white;
		SetRect(active.rectTransform, new Vector2(0.34f, 0.56f), new Vector2(0.96f, 0.98f));
		Text next = CreateText("Next Form", ((Component)image).transform, font, 13, (FontStyle)0, (TextAnchor)4);
		next.text = $"PROSSIMA {GolemFormName(nextForm.Form)}";
		next.color = new Color(0.82f, 0.9f, 1f);
		SetRect(next.rectTransform, new Vector2(0.04f, 0.27f), new Vector2(0.96f, 0.58f));
		Text countdown = CreateText("Round Countdown", ((Component)image).transform, font, 13, (FontStyle)0, (TextAnchor)4);
		int roundsLeft = Mathf.Max(1, ComposableGolem.DefaultRoundsPerForm - activeComposableGolem.RoundsInActiveForm);
		countdown.text = roundsLeft == 1 ?"CAMBIO TRA 1 ROUND" : $"CAMBIO TRA {roundsLeft} ROUND";
		countdown.color = new Color(1f, 0.9f, 0.55f);
		SetRect(countdown.rectTransform, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.3f));
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

	private float GetTimelineTileSize()
	{
		Rect val = Screen.safeArea;
		float num = Mathf.Max(1f, val.width);
		val = Screen.safeArea;
		float num2 = Mathf.Max(1f, val.height);
		bool num3 = IsCompactLayout(num / num2, configuration.ResponsiveLayout);
		float num4 = (num3 ?84f : 52f);
		float num5 = (num3 ?48f : 36f);
		int visibleTimelineTileCount = GetVisibleTimelineTileCount();
		if ((Object)(object)initiativeTimelineRoot == (Object)null || visibleTimelineTileCount <= 0)
		{
			return num4;
		}
		val = initiativeTimelineRoot.rect;
		float num6 = val.width;
		if (num6 <= 0f && (Object)(object)timelineBackgroundRect != (Object)null)
		{
			val = timelineBackgroundRect.rect;
			num6 = val.width - 16f;
		}
		if (num6 <= 0f)
		{
			return num4;
		}
		return Mathf.Clamp((num6 - 6f * (float)Mathf.Max(0, visibleTimelineTileCount - 1)) / (float)visibleTimelineTileCount, num5, num4);
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

	private void ResizeTimelineTiles()
	{
		if ((Object)(object)initiativeTimelineRoot == (Object)null)
		{
			return;
		}
		float timelineTileSize = GetTimelineTileSize();
		for (int i = 0; i < ((Transform)initiativeTimelineRoot).childCount; i++)
		{
			Transform child = ((Transform)initiativeTimelineRoot).GetChild(i);
			RectTransform val = (RectTransform)(object)((child is RectTransform) ?child : null);
			if (!((Object)(object)val == (Object)null))
			{
				LayoutElement layoutElement = ((Component)val).GetComponent<LayoutElement>();
				if ((Object)(object)layoutElement == (Object)null)
				{
					layoutElement = ((Component)val).gameObject.AddComponent<LayoutElement>();
				}
				layoutElement.minWidth = timelineTileSize;
				layoutElement.preferredWidth = timelineTileSize;
				layoutElement.flexibleWidth = 0f;
			}
		}
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
			cpuCard.View.SetTargetHint(unavailable ?((MatchupResult?)null) : new MatchupResult?(ClassMatchup.Compare(attacker.Card.HeroClass, cpuCard.Card.HeroClass)));
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
