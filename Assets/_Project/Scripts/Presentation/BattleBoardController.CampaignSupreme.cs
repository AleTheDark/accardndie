using System.Collections.Generic;
using System.Collections;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using UnityEngine;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	/// <summary>
	/// Abilita' supreme lato campagna. Gli effetti sono gli stessi del motore PvP
	/// (PvpMatchEngine.UseSupreme) ma scritti contro BattleCardState: le due modalita'
	/// hanno strutture di stato diverse, come gia' avviene per tutto il combattimento.
	/// I costi invece sono condivisi, arrivano da ManaPool/AbilityManaCosts.
	/// </summary>

	private Sprite GetSupremeButtonSprite()
	{
		return LoadSpriteResource("UI/ability_secondary_button");
	}

	/// <summary>Premuta della suprema dal bottone sulla pedina.</summary>
	private void ActivateCurrentSupreme()
	{
		if (inputLocked || selectedPlayerIndex < 0 || selectedPlayerIndex >= playerCards.Count)
		{
			return;
		}
		// Il selettore swipe/click inoltra il rilascio alla carta sottostante. Le
		// supreme che si risolvono subito (per esempio Paladino e Barbaro) chiudono
		// l'overlay prima di quell'inoltro: senza questa guardia lo stesso input
		// viene quindi interpretato anche come apertura dell'ispezione della carta.
		suppressCardInspectionUntilFrame = Time.frameCount + 1;
		BattleCardState card = playerCards[selectedPlayerIndex];
		if (!TryActivateCampaignSupreme(card))
		{
			return;
		}
		RefreshCardActionOverlays();
		UpdateInteractions();
	}

	private bool IsSupremeUnlockedForCampaign(HeroClass heroClass)
	{
		return CardRulesGlossary.HasSupreme(heroClass) && IsSupremeUnlocked(heroClass);
	}

	/// <summary>La suprema e' proponibile: sbloccata, pedina viva, nessuna abilita' innescata.</summary>
	private bool IsSupremeActionAvailable(BattleCardState card)
	{
		if (card == null || card.Eliminated || card.AbilityArmed || !CampaignManaEnabled)
		{
			return false;
		}
		if (IsCampaignManaExempt(card))
		{
			return false;
		}
		if (!IsSupremeUnlockedForCampaign(card.Card.HeroClass))
		{
			return false;
		}
		// Le supreme d'attacco sostituiscono l'attacco; le altre no. In entrambi i casi
		// vale il limite di una sola abilita' non-d'attacco per attivazione.
		return AbilityManaCosts.IsAttackSupreme(card.Card.HeroClass) || !card.AbilityUsedThisTurn;
	}

	private int CampaignSupremeCost(BattleCardState card)
	{
		return CampaignManaFor(card).CostOfSupreme(card.Card.HeroClass);
	}

	private bool IsCampaignSupremeAffordable(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return true;
		}
		return CampaignManaFor(card).CanAfford(CampaignSupremeCost(card));
	}

	/// <summary>
	/// Punto unico d'attivazione. Verifica prima, applica, poi paga: cosi' un bersaglio
	/// non valido non lascia il mana scalato su un'azione che non e' avvenuta.
	/// </summary>
	private bool TryActivateCampaignSupreme(BattleCardState card)
	{
		if (!IsSupremeActionAvailable(card))
		{
			return false;
		}
		if (!IsCampaignSupremeAffordable(card))
		{
			ShowNoManaCallout(card);
			SetMessage($"Mana insufficiente per la suprema di {card.Card.Name}.");
			return false;
		}

		int cost = CampaignSupremeCost(card);
		if (card.Card.HeroClass is HeroClass.Mage or HeroClass.Hunter)
		{
			StartCoroutine(ResolveCampaignMassAttackSupreme(card, cost));
			return true;
		}
		ApplyCampaignSupreme(card);

		ManaPool pool = CampaignManaFor(card);
		if (cost > 0)
		{
			pool.Spend(cost);
			AppendLog($"MANA - {(card.BelongsToPlayer ? "tu" : "CPU")} spendi {cost} per la suprema di {card.Card.Name}: {pool.Current}/{pool.Rules.Maximum}.");
			if (card.BelongsToPlayer)
			{
				PlayManaDeltaCallout(-cost);
			}
			else
			{
				PlayEnemyManaDeltaCallout(-cost);
			}
		}

		// La Riserva del Paladino agisce dopo il pagamento: e' quello che la rende una
		// soglia e non un guadagno secco.
		if (card.Card.HeroClass == HeroClass.Paladin)
		{
			int gained = pool.RaiseTo(pool.Rules.PaladinReserveThreshold);
			if (gained > 0)
			{
				AppendLog($"MANA - riserva del Paladino: +{gained} fino a {pool.Current}/{pool.Rules.Maximum}.");
				if (card.BelongsToPlayer)
				{
					PlayManaDeltaCallout(gained);
				}
				else
				{
					PlayEnemyManaDeltaCallout(gained);
				}
			}
		}

		pool.RegisterSupremeUse(card.Card.HeroClass);
		StartCoroutine(PlaySupremePresentationRoutine(card, playCallout: true));
		card.AbilityUsedThisTurn = true;
		RefreshCampaignManaPresentation();
		RefreshPersistentStatus(card);
		return true;
	}

	private IEnumerator ResolveCampaignMassAttackSupreme(BattleCardState attacker, int cost)
	{
		bool isHunter = attacker.Card.HeroClass == HeroClass.Hunter;
		string supremeName = isHunter ? "Raffica" : "Palla di Fuoco";
		inputLocked = true;
		attackTargetingActive = false;
		ClearTargetHints();
		UpdateInteractions();

		List<BattleCardState> enemies = EnemiesOf(attacker)
			.Where(enemy => enemy != null && !enemy.Eliminated)
			.ToList();
		if (enemies.Count == 0)
		{
			inputLocked = false;
			yield break;
		}

		ManaPool pool = CampaignManaFor(attacker);
		if (cost > 0)
		{
			pool.Spend(cost);
			AppendLog($"MANA - {(attacker.BelongsToPlayer ? "tu" : "CPU")} spendi {cost} per la suprema di {attacker.Card.Name}: {pool.Current}/{pool.Rules.Maximum}.");
			if (attacker.BelongsToPlayer) PlayManaDeltaCallout(-cost);
			else PlayEnemyManaDeltaCallout(-cost);
		}
		pool.RegisterSupremeUse(attacker.Card.HeroClass);
		attacker.AbilityUsedThisTurn = true;
		RefreshCampaignManaPresentation();
		attacker.View.PlaySupremeActionCallout();
		if (!isHunter)
			PlayMageSupremeSfx();

		// Prima versione volutamente neutra: nessuno step in piu' o in meno. Il
		// bilanciamento della suprema restera' confinato a questi dadi.
		int attackerDieSides = attacker.BelongsToPlayer
			? runProgress.PlayerVigorDieSides
			: runProgress.MasterVigorDieSides;
		int attackerValue = random.NextInclusive(1, attackerDieSides);
		VigorRollResult attackerRoll = SingleRoll(attackerDieSides, attackerValue);
		List<int> enemyDice = new List<int>(enemies.Count);
		List<int> enemyValues = new List<int>(enemies.Count);
		List<BattleCardState> defeated = new List<BattleCardState>();

		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(attackerRoll), supremeName.ToUpperInvariant(), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		foreach (BattleCardState enemy in enemies)
		{
			int baseDie = enemy.BelongsToPlayer ? runProgress.PlayerVigorDieSides : runProgress.MasterVigorDieSides;
			int dieSides = baseDie;
			int value = random.NextInclusive(1, dieSides);
			enemyDice.Add(dieSides);
			enemyValues.Add(value);
			enemy.View.PlayVigorRoll(diceCatalog, dieSides, TrackDiceRoll(SingleRoll(dieSides, value)), "RESISTENZA AL FUOCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
			if (attackerValue > value)
				defeated.Add(enemy);
		}

		SetMessage($"{attacker.Card.Name} scatena {supremeName}: ogni nemico affronta il suo {attackerValue}.");
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);

		for (int index = 0; index < enemies.Count; index++)
		{
			BattleCardState enemy = enemies[index];
			bool loses = attackerValue > enemyValues[index];
			AppendLog($"SUPREMA - {attacker.Card.Name} D{attackerDieSides}={attackerValue} vs {enemy.Card.Name} D{enemyDice[index]}={enemyValues[index]}: {(loses ? "VINCE" : "RESISTE")}.");
		}

		if (battleAnimationPlayer != null)
		{
			if (isHunter)
				yield return battleAnimationPlayer.PlayHunterVolleySupreme(
					attacker.View,
					enemies.Select(enemy => enemy.View).ToList(),
					enemies.Select(enemy => defeated.Contains(enemy)).ToList(),
					() => PlayClassAbilitySfx(HeroClass.Hunter));
			else if (defeated.Count > 0)
				yield return battleAnimationPlayer.PlayMageFireballSupreme(attacker.View, enemies.Select(enemy => enemy.View).ToList());
		}

		foreach (BattleCardState enemy in defeated)
		{
			enemy.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, enemy);
			ApplyMageAuraDeathPenalty(enemy, attacker);
			ApplyMightAuraDeathBonuses(enemy);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(enemy, HeroClass.Mage);
		}

		SetMessage(defeated.Count > 0
			? $"{supremeName} travolge {defeated.Count} nemic{(defeated.Count == 1 ? "o" : "i")}!"
			: $"I nemici resistono a {supremeName}.");
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	/// <summary>
	/// Unica regia visiva delle supreme per campagna e PvP. Ogni nuovo VFX di
	/// suprema va aggiunto qui, mai in un ramo specifico della modalita'.
	/// </summary>
	private IEnumerator PlaySupremePresentationRoutine(BattleCardState card, bool playCallout)
	{
		if (card == null || card.View == null)
			yield break;

		if (playCallout)
			card.View.PlaySupremeActionCallout();

		if (card.Card.HeroClass == HeroClass.Warrior)
			PlayWarriorSupremeSfx();
		else if (card.Card.HeroClass == HeroClass.Mage)
			PlayMageSupremeSfx();

		if (card.Card.HeroClass == HeroClass.Paladin && battleAnimationPlayer != null)
			yield return battleAnimationPlayer.PlayPaladinSupremePulse(card.View);
	}

	private void ApplyCampaignSupreme(BattleCardState card)
	{
		switch (card.Card.HeroClass)
		{
		case HeroClass.Warrior:
			ApplyCampaignEmpower(card);
			break;
		case HeroClass.Barbarian:
			ApplyCampaignWarHorn(card);
			break;
		case HeroClass.Assassin:
			ApplyCampaignVanish(card);
			break;
		case HeroClass.Priest:
			ApplyCampaignDispel(card);
			break;
		case HeroClass.Paladin:
			// Solo mana: l'effetto e' applicato dopo il pagamento.
			break;
		}
	}

	/// <summary>Guerriero: +2, oppure +4 se e' rimasto solo. I due non si sommano.</summary>
	private void ApplyCampaignEmpower(BattleCardState card)
	{
		int bonus = AlliesOf(card).Count(ally => !ally.Eliminated) <= 1 ? 4 : 2;
		card.PermanentCombatBonus += bonus;
		AppendLog($"SUPREMA - {card.Card.Name} si potenzia di +{bonus} fino a fine stanza.");
	}

	/// <summary>Barbaro: la cornamusa potenzia tutta la squadra, anche in difesa.</summary>
	private void ApplyCampaignWarHorn(BattleCardState card)
	{
		int bonus = configuration?.ClassBalance?.BarbarianRageBonus ?? 2;
		foreach (BattleCardState ally in AlliesOf(card))
		{
			if (ally.Eliminated)
			{
				continue;
			}
			ally.PendingAttackBonus += bonus;
			ally.PendingAttackBonusKind = PendingAttackBonusKind.Fury;
			RefreshPersistentStatus(ally);
		}
		AppendLog($"SUPREMA - {card.Card.Name} suona la cornamusa: +{bonus} a tutta la squadra.");
	}

	/// <summary>Assassino: non bersagliabile finche' non resta solo.</summary>
	private void ApplyCampaignVanish(BattleCardState card)
	{
		card.IsUntargetable = true;
		AppendLog($"SUPREMA - {card.Card.Name} sparisce alla vista: non puo' essere bersagliato.");
	}

	/// <summary>
	/// Sacerdote: toglie i malus agli alleati e i potenziamenti agli avversari.
	/// Non tocca le aure, che nascono dalla formazione e non da una giocata.
	/// </summary>
	private void ApplyCampaignDispel(BattleCardState card)
	{
		int cleared = 0;
		foreach (BattleCardState ally in AlliesOf(card))
		{
			if (ally.Eliminated)
			{
				continue;
			}
			if (ally.InhibitedTurns > 0) { ally.InhibitedTurns = 0; cleared++; }
			if (ally.PendingVigorStepPenalty > 0) { ally.PendingVigorStepPenalty = 0; cleared++; }
			if (ally.PermanentCombatBonus < 0) { ally.PermanentCombatBonus = 0; cleared++; }
			RefreshPersistentStatus(ally);
		}
		foreach (BattleCardState foe in EnemiesOf(card))
		{
			if (foe.Eliminated)
			{
				continue;
			}
			if (foe.PendingAttackBonus > 0)
			{
				foe.PendingAttackBonus = 0;
				foe.PendingAttackBonusKind = PendingAttackBonusKind.None;
				cleared++;
			}
			if (foe.PermanentCombatBonus > 0) { foe.PermanentCombatBonus = 0; cleared++; }
			if (foe.IsUntargetable) { foe.IsUntargetable = false; cleared++; }
			RefreshPersistentStatus(foe);
		}
		AppendLog($"SUPREMA - {card.Card.Name} purifica il campo: {cleared} effetti rimossi.");
	}

	private List<BattleCardState> AlliesOf(BattleCardState card) =>
		card.BelongsToPlayer ? playerCards : cpuCards;

	private List<BattleCardState> EnemiesOf(BattleCardState card) =>
		card.BelongsToPlayer ? cpuCards : playerCards;

	/// <summary>
	/// L'invisibilita' protegge finche' l'Assassino non e' l'unica pedina attiva del
	/// suo schieramento: a quel punto torna bersagliabile.
	/// </summary>
	private bool IsShieldedByInvisibility(BattleCardState card)
	{
		return card != null
			&& card.IsUntargetable
			&& AlliesOf(card).Count(ally => !ally.Eliminated) > 1;
	}
}
}
