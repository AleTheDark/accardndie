using System;
using System.Collections;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private IEnumerator ExecutePlayerTurnAgainstSeraphel(BattleCardState attacker, BattleCardState boss)
	{
		if (activeSeraphelBoss == null) { FinishTurn(); yield break; }
		int attackerDie = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDie = EffectiveDefenseVigorDieSides(boss, activeSeraphelBoss.VigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, boss, false, false);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, boss) > 0;
		if (!UsesStationaryClassAttack(attacker)) yield return MoveDuelToCenter(attacker, boss);
		CombatResult combat = combatResolver.ResolveAttack(attacker.Card, boss.Card, attackerDie, defenderDie, modifiers,
			AdventureRollBiases(attacker, boss));
		SeraphelDefenseResult defense = activeSeraphelBoss.ApplyResolvedDefense(combat.AttackerTotal, combat.DefenderTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		bool hidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDie, TrackDiceRoll(combat.AttackerRoll), "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		boss.View.PlayVigorRoll(diceCatalog, defenderDie, TrackDiceRoll(combat.DefenderRoll), "DIFESA SERAPHEL", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(combat.AttackerRoll, combat.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(hidden);
		yield return ShowCombatResult(combat, attacker, boss);
		if (hunterMarkUsed)
			ConsumeHunterMarks(boss);
		PlayResolvedAttackSfx(attacker, defense.Damage > 0, modifiers.SumAttackerVigor);
		if (defense.Damage > 0)
			yield return PlayHunterRangedAttackIfNeeded(attacker, boss, defense.Damage, combat.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
		else
			yield return PlayHunterMissIfNeeded(attacker, boss);

		if (defense.PhaseChanged)
			yield return TransformSeraphelToPhaseTwo(boss);
		RefreshSeraphelBossPawn(boss);
		if (activeSeraphelBoss.IsDefeated)
		{
			boss.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, boss);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(boss, attacker.Card.HeroClass);
		}
		yield return ReturnDuelSurvivors(attacker, boss);
		ConsumeVigorPenalties(attacker, boss);
		UpdateAttackerClassStateAfterExchange(attacker, defense.Damage > 0);
		SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.SeraphelDamaged, "{0} infligge {1} danni a Seraphel. HP {2}/{3}.", "{0} deals {1} damage to Seraphel. HP {2}/{3}.", "{0} fügt Seraphel {1} Schaden zu. LP {2}/{3}.", "{0} inflige {1} de daño a Seraphel. PV {2}/{3}.", "{0} inflige {1} dégâts à Seraphel. PV {2}/{3}.", attacker.Card.Name, defense.Damage, defense.HitPointsAfter, activeSeraphelBoss.MaxHitPoints));
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator TransformSeraphelToPhaseTwo(BattleCardState boss)
	{
		PlaySeraphelTransformationSfx();
		CardDefinition phaseTwo = FindCardDefinition(SeraphelPhaseTwoCardId);
		if ((Object)(object)phaseTwo != (Object)null)
			boss.TransformSeraphel(phaseTwo, SeraphelBoss.PhaseTwoStrength);
		boss.InhibitedTurns = 0;
		boss.WasInhibited = false;
		boss.PendingVigorStepPenalty = 0;
		boss.Petrified = false;
		boss.PermanentCombatBonus = Math.Max(0, boss.PermanentCombatBonus);
		foreach (BattleCardState hunter in playerCards.Concat(cpuCards))
		{
			if (hunter == null || hunter.MarkedTarget != boss)
				continue;
			hunter.MarkedTarget = null;
			RefreshPersistentStatus(hunter);
		}
		RefreshPersistentStatus(boss);
		boss.View?.PlayAbilityActionCallout();
		SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.SeraphelPhaseTwo, "SERAPHEL - FASE II: Luce Inesorabile. Potenza 10, D12, immune ai malus e applica due Sigilli.", "SERAPHEL - PHASE II: Relentless Light. Strength 10, D12, immune to debuffs and applies two Seals.", "SERAPHEL - PHASE II: Unerbittliches Licht. Stärke 10, W12, immun gegen Schwächungen und wendet zwei Siegel an.", "SERAPHEL - FASE II: Luz Implacable. Fuerza 10, D12, inmune a debilitaciones y aplica dos Sellos.", "SERAPHEL - PHASE II : Lumière implacable. Force 10, D12, immunisé contre les malus et applique deux Sceaux."));
		TransitionToScenarioBackground();
		RefreshSeraphelBossPawn(boss);
		ApplySeraphelExclusiveLayout();
		Canvas.ForceUpdateCanvases();
		yield return PlaySeraphelPhaseTwoElectricFury();
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal + 0.5f);
	}

	private IEnumerator ExecuteSeraphelBossTurn(BattleCardState boss)
	{
		// Se Seraphel possiede la prima iniziativa, il suo turno viene accodato alla
		// conclusione del reveal: nessun Giudizio deve partire mentre le croci salgono.
		while (seraphelEntranceVfxRunning)
			yield return null;

		var targets = playerCards.Where(card => card != null && !card.Eliminated).ToList();
		if (activeSeraphelBoss == null || targets.Count == 0) { FinishTurn(); yield break; }
		BattleCardState defender = targets.OrderByDescending(card => card.SeraphelSeals)
			.ThenByDescending(DisplayStrength).First();
		int defenseDie = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		int seraphelDie = EffectiveVigorDieSides(boss, activeSeraphelBoss.VigorDieSides);
		// Il Giudizio deve partire dalla Potenza effettiva mostrata sulla pedina:
		// Suprema del Guerriero, equipaggiamenti e gli altri bonus persistenti non
		// possono essere persi quando Seraphel costruisce il suo confronto speciale.
		int defenderEffectiveStrength = DisplayStrength(defender);
		SeraphelAttackResult attack = activeSeraphelBoss.Attack(
			defender.Card,
			defenseDie,
			defender.SeraphelSeals,
			seraphelDie,
			defenderEffectiveStrength);
		VigorRollResult attackRoll = SingleRoll(seraphelDie, attack.AttackRoll);
		VigorRollResult defenseRoll = SingleRoll(defenseDie, attack.DefenseRoll);
		CombatResult combat = new CombatResult(attackRoll, defenseRoll, attack.AttackTotal, attack.DefenseTotal);
		// Il normale callout d'attacco usa l'aura rossa delle pedine. Giudizio di Luce
		// usa invece esclusivamente i suoi VFX dedicati quando il Sigillo riesce.
		SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.SeraphelJudgement, "SERAPHEL: Giudizio di Luce su {0}.", "SERAPHEL: Judgement of Light on {0}.", "SERAPHEL: Urteil des Lichts gegen {0}.", "SERAPHEL: Juicio de Luz sobre {0}.", "SERAPHEL : Jugement de Lumière sur {0}.", defender.Card.Name));
		bool hidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		boss.View.PlayVigorRoll(diceCatalog, seraphelDie, TrackDiceRoll(attackRoll),
			attack.SealsBefore > 0 ? $"GIUDIZIO +{attack.SealDamageBonus}" : "GIUDIZIO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenseDie, TrackDiceRoll(defenseRoll), "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(attackRoll, defenseRoll));
		RestoreMessagePanelAfterDiceRoll(hidden);
		yield return ShowCombatResult(combat, boss, defender);
		if (attack.AttackSucceeded)
		{
			yield return PlaySeraphelSealRay(boss.View, defender.View);
			defender.SeraphelSeals = Math.Min(
				SeraphelBoss.SealExecutionThreshold,
				defender.SeraphelSeals + attack.SealsApplied);
			yield return PlaySeraphelSealApplicationVfx(defender.View, defender.SeraphelSeals);
			RefreshPersistentStatus(defender);
			if (defender.SeraphelSeals >= SeraphelBoss.SealExecutionThreshold)
			{
				defender.Eliminated = true;
				RegisterCampaignEliminationMana(boss, defender);
				ApplyMageAuraDeathPenalty(defender, boss);
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.SeraphelThreeSeals, "CONDANNA DEI TRE SIGILLI: {0} raggiunge 3 Sigilli e viene eliminata automaticamente.", "THREE-SEAL CONDEMNATION: {0} reaches 3 Seals and is eliminated automatically.", "VERURTEILUNG DER DREI SIEGEL: {0} erreicht 3 Siegel und wird automatisch eliminiert.", "CONDENA DE LOS TRES SELLOS: {0} alcanza 3 Sellos y es eliminado automáticamente.", "CONDAMNATION DES TROIS SCEAUX : {0} atteint 3 Sceaux et est éliminé automatiquement.", defender.Card.Name));
				yield return PlayTimelineAwareDefeatAnimation(defender, boss.Card.HeroClass);
			}
			else
			{
				SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.SeraphelSealsApplied, "SIGILLO SACRO: {0} riceve {1} Sigillo/i. Totale {2}/3; la sua Potenza non cambia e Seraphel ottiene +{3} contro questa pedina.", "SACRED SEAL: {0} receives {1} Seal(s). Total {2}/3; its Strength does not change and Seraphel gains +{3} against this unit.", "HEILIGES SIEGEL: {0} erhält {1} Siegel. Insgesamt {2}/3; seine Stärke ändert sich nicht und Seraphel erhält +{3} gegen diese Einheit.", "SELLO SAGRADO: {0} recibe {1} Sello(s). Total {2}/3; su Fuerza no cambia y Seraphel obtiene +{3} contra esta unidad.", "SCEAU SACRÉ : {0} reçoit {1} Sceau(x). Total {2}/3 ; sa Force ne change pas et Seraphel gagne +{3} contre cette unité.", defender.Card.Name, attack.SealsApplied, defender.SeraphelSeals, defender.SeraphelSeals * SeraphelBoss.DamagePerSeal));
			}
		}
		else
		{
			SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.SeraphelJudgementResisted, "{0} resiste al Giudizio di Seraphel. Nessun Sigillo applicato.", "{0} resists Seraphel's Judgement. No Seal is applied.", "{0} widersteht Seraphels Urteil. Kein Siegel wird angewendet.", "{0} resiste el Juicio de Seraphel. No se aplica ningún Sello.", "{0} résiste au Jugement de Seraphel. Aucun Sceau n'est appliqué.", defender.Card.Name));
		}
		// ShowCombatResult visualizza temporaneamente i totali del confronto sui badge
		// Potenza. Il Sigillo appartiene a Seraphel e non deve lasciare il totale di
		// difesa come nuova Potenza della pedina bersaglio.
		yield return RestoreCombatStrengthPresentation(boss, defender);
		ConsumeVigorPenalties(boss, defender);
		UpdateDefenderClassStateAfterExchange(defender, attack.AttackSucceeded);
		// Il confronto non infligge morte diretta: l'eliminazione avviene soltanto al terzo Sigillo.
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}
}
}
