using System.Collections.Generic;
using System.Collections;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using AccardND.Localization;
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
		if (!TutorialWarriorDuelAllowsSupreme())
			return;
		LogInspectionState("SUPREME_BUTTON_ENTER");
		if (inputLocked || selectedPlayerIndex < 0 || selectedPlayerIndex >= playerCards.Count)
		{
			LogInspectionState("SUPREME_BUTTON_REJECTED");
			return;
		}
		// Il selettore swipe/click inoltra il rilascio alla carta sottostante. Le
		// supreme che si risolvono subito (per esempio Paladino e Barbaro) chiudono
		// l'overlay prima di quell'inoltro: senza questa guardia lo stesso input
		// viene quindi interpretato anche come apertura dell'ispezione della carta.
		suppressCardInspectionUntilFrame = Time.frameCount + 1;
		StartCoroutine(RestoreInteractionsAfterSupremeRelease());
		BattleCardState card = playerCards[selectedPlayerIndex];
		LogInspectionState("SUPREME_BEFORE_ACTIVATE", card);
		if (!TryActivateCampaignSupreme(card))
		{
			LogInspectionState("SUPREME_ACTIVATION_FAILED", card);
			return;
		}
		LogInspectionState("SUPREME_AFTER_EFFECT", card);
		// Le supreme istantanee non possono lasciare in memoria una selezione iniziata
		// prima: altrimenti i clic successivi vengono trattati come scelta bersaglio.
		if (card.Card.HeroClass is HeroClass.Paladin or HeroClass.Barbarian or HeroClass.Priest or HeroClass.Necromancer)
		{
			attackTargetingActive = false;
			abilityTargetMode = AbilityTargetMode.None;
			activeAbilityUser = null;
			activeAttachmentSource = null;
			pendingAbilityUser = null;
			ClearTargetHints();
			// L'alone verde e' usato anche dal targeting: dopo una suprema istantanea
			// lo spegniamo per non comunicare una scelta bersaglio inesistente.
			BattleCardState activeTurnCard = currentTurnIndex >= 0 && currentTurnIndex < turnOrder.Count
				? turnOrder[currentTurnIndex]
				: null;
			SetActiveTurnAura(activeTurnCard);
			LogInspectionState("SUPREME_INSTANT_STATE_CLEARED", card);
		}
		RefreshCardActionOverlays();
		UpdateInteractions();
		LogInspectionState("SUPREME_AFTER_REFRESH", card);
	}

	private IEnumerator RestoreInteractionsAfterSupremeRelease()
	{
		// Lo swipe inoltra il rilascio alla pedina anche nel frame successivo. Durante
		// questa finestra UpdateInteractions la rende non interagibile; appena la
		// soppressione scade dobbiamo rivalutarla, altrimenti resta disabilitata.
		while (Time.frameCount <= suppressCardInspectionUntilFrame)
		{
			yield return null;
		}
		UpdateInteractions();
	}

	private bool IsSupremeUnlockedForCampaign(HeroClass heroClass)
	{
		return CardRulesGlossary.HasSupreme(heroClass) && IsSupremeUnlocked(heroClass);
	}

	/// <summary>La suprema e' proponibile: sbloccata, pedina viva, nessuna abilita' innescata.</summary>
	private bool IsSupremeActionAvailable(BattleCardState card)
	{
		if (card == null || card.Eliminated || card.AbilityArmed
			|| card.SupremeUsedThisTurn || !CampaignManaEnabled)
		{
			return false;
		}
		if (IsCampaignManaExempt(card))
		{
			return false;
		}
		bool supremeAvailable = card.BelongsToPlayer
			? IsSupremeUnlockedForCampaign(card.Card.HeroClass)
			: CardRulesGlossary.HasSupreme(card.Card.HeroClass)
				&& AbilityManaCosts.IsSupremeImplemented(card.Card.HeroClass);
		if (!supremeAvailable)
		{
			return false;
		}
		// Le supreme d'attacco sostituiscono l'attacco; le altre no. In entrambi i casi
		// vale il limite di una sola abilita' non-d'attacco per attivazione.
		// La Riserva e' una manovra di recupero: il Paladino puo usarla anche dopo
		// aver armato la propria protezione nella stessa attivazione.
		return AbilityManaCosts.IsAttackSupreme(card.Card.HeroClass)
			|| card.Card.HeroClass == HeroClass.Paladin
			|| !card.AbilityUsedThisTurn;
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
	/// Registra la suprema sul pool (sovrapprezzo di ripetizione) e, se la pedina e' del
	/// giocatore, sul contatore delle quest della taverna. Le supreme si risolvono su tre
	/// rami diversi (immediata, scippo del Ladro, attacco di massa): passare tutti da qui
	/// evita di dover ricordare quale di quei rami incrementa il contatore.
	/// </summary>
	private void TrackSupremeUsed(ManaPool pool, BattleCardState card)
	{
		pool.RegisterSupremeUse(card.Card.HeroClass);
		if (runProgress != null && ShouldTrackQuestProgress && playerCards.Contains(card))
			runProgress.RecordSupremeUsed();
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
			SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.SupremeManaInsufficient, "Mana insufficiente per la suprema di {0}.", "Not enough Mana for {0}'s supreme ability.", "Nicht genug Mana für die höchste Fähigkeit von {0}.", "Maná insuficiente para la suprema de {0}.", "Mana insuffisant pour la capacité suprême de {0}.", card.Card.Name));
			return false;
		}

		int cost = CampaignSupremeCost(card);
		if (card.Card.HeroClass == HeroClass.Rogue)
		{
			activeAbilityUser = card;
			abilityTargetMode = AbilityTargetMode.RogueSupremeEnemy;
			SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.RogueSupremePrompt, "SCIPPO: scegli un nemico. Rubi 1 buff e 2 mana, oppure 1 Potenza se non ha buff.", "HEIST: choose an enemy. Steal 1 buff and 2 Mana, or 1 Strength if it has no buff.", "RAUB: Wähle einen Gegner. Stiehl 1 Buff und 2 Mana oder 1 Stärke, falls er keinen Buff hat.", "ROBO: elige un enemigo. Roba 1 mejora y 2 de Maná, o 1 de Fuerza si no tiene mejoras.", "VOL : choisissez un ennemi. Volez 1 bonus et 2 Mana, ou 1 de Force s'il n'a aucun bonus."));
			ClearTargetHints();
			UpdateInteractions();
			return true;
		}
		if (card.Card.HeroClass is HeroClass.Mage or HeroClass.Hunter)
		{
			StartCoroutine(ResolveCampaignMassAttackSupreme(card, cost));
			return true;
		}
		List<BattleCardState> dispelTargets = card.Card.HeroClass == HeroClass.Priest
			? CampaignDispelTargets(card)
			: null;
		if (card.Card.HeroClass != HeroClass.Priest)
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

		int paladinManaGained = 0;
		// La Riserva del Paladino agisce dopo il pagamento: e' quello che la rende una
		// soglia e non un guadagno secco.
		if (card.Card.HeroClass == HeroClass.Paladin)
		{
			int gained = pool.RaiseTo(pool.Rules.PaladinReserveThreshold);
			paladinManaGained = gained;
			PlayManaRuneEnergyBurst(enemy: !card.BelongsToPlayer);
			if (gained > 0)
			{
				AppendLog($"MANA - riserva del Paladino: +{gained} fino a {pool.Current}/{pool.Rules.Maximum}.");
			}
		}

		TrackSupremeUsed(pool, card);
		card.SupremeUsedThisTurn = true;
		if (card.BelongsToPlayer)
		{
			NotifyAdventureTutorial(AdventureTutorialAction.SupremeUsed);
		}
		RefreshCampaignManaPresentation();
		List<PrototypeCardView> dispelViews = dispelTargets?.Select(target => target.View).Where(view => view != null).ToList();
		List<PrototypeCardView> barbarianFuryViews = card.Card.HeroClass == HeroClass.Barbarian
			? AlliesOf(card).Where(ally => !ally.Eliminated).Select(ally => ally.View).Where(view => view != null).ToList()
			: null;
		int removedEffects = 0;
		int impactedTargets = 0;
		StartCoroutine(PlaySupremePresentationRoutine(card, playCallout: true, paladinManaStrikes: paladinManaGained,
			dispelTargets: dispelViews,
			barbarianFuryTargets: barbarianFuryViews,
			onDispelImpact: view =>
			{
				BattleCardState target = dispelTargets?.FirstOrDefault(candidate => candidate.View == view);
				if (target != null)
					removedEffects += ApplyCampaignDispelToTarget(card, target);
				impactedTargets++;
				if (impactedTargets >= (dispelViews?.Count ?? 0))
					AppendLog($"SUPREMA - {card.Card.Name} purifica il campo: {removedEffects} effetti rimossi.");
			}));
		if (card.Card.HeroClass == HeroClass.Priest && (dispelViews == null || dispelViews.Count == 0))
			AppendLog($"SUPREMA - {card.Card.Name} purifica il campo: 0 effetti rimossi.");
		// Segna che la pedina ha agito per il recupero mana, senza consumare o
		// nascondere l'abilita' primaria: IsClassAbilityActionAvailable la lascia
		// disponibile e ActivateCurrentAbility verifica il mana rimasto.
		card.AbilityUsedThisTurn = true;
		RefreshPersistentStatus(card);
		return true;
	}

	/// <summary>
	/// Nelle stanze Diaboliche la CPU usa una suprema quando puo' pagarla. Le supreme
	/// d'attacco chiudono da sole il turno; le altre lasciano proseguire l'attivazione.
	/// </summary>
	private bool TryUseCpuSupreme(BattleCardState card, out bool endsTurn)
	{
		endsTurn = false;
		if (card == null || card.BelongsToPlayer
			|| !RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesSupremes
			|| !IsSupremeActionAvailable(card)
			|| !IsCampaignSupremeAffordable(card))
		{
			return false;
		}

		if (card.Card.HeroClass == HeroClass.Rogue)
		{
			BattleCardState target = EnemiesOf(card)
				.Where(candidate => candidate != null && !candidate.Eliminated)
				.OrderByDescending(DisplayStrength)
				.FirstOrDefault();
			if (target == null || !TryActivateCampaignSupreme(card))
				return false;
			ResolveCampaignRogueSupreme(card, target);
			abilityTargetMode = AbilityTargetMode.None;
			activeAbilityUser = null;
			ClearTargetHints();
		}
		else if (!TryActivateCampaignSupreme(card))
		{
			return false;
		}

		endsTurn = AbilityManaCosts.IsAttackSupreme(card.Card.HeroClass);
		return true;
	}

	private void ResolveCampaignRogueSupreme(BattleCardState rogue, BattleCardState target)
	{
		if (rogue == null || target == null || target.Eliminated || target.BelongsToPlayer == rogue.BelongsToPlayer)
			return;

		bool hadBuff = target.PendingAttackBonus > 0 || target.PermanentCombatBonus > 0;
		int stolenPower;
		if (target.PendingAttackBonus > 0)
		{
			stolenPower = target.PendingAttackBonus;
			target.PendingAttackBonus = 0;
			target.PendingAttackBonusKind = PendingAttackBonusKind.None;
		}
		else if (target.PermanentCombatBonus > 0)
		{
			stolenPower = target.PermanentCombatBonus;
			target.PermanentCombatBonus = 0;
		}
		else
		{
			stolenPower = 1;
			target.PermanentCombatBonus -= 1;
		}
		rogue.PermanentCombatBonus += stolenPower;

		ManaPool thiefPool = CampaignManaFor(rogue);
		ManaPool victimPool = CampaignManaFor(target);
		int cost = CampaignSupremeCost(rogue);
		if (cost > 0)
		{
			thiefPool.Spend(cost);
			if (rogue.BelongsToPlayer) PlayManaDeltaCallout(-cost); else PlayEnemyManaDeltaCallout(-cost);
			AppendLog($"MANA - {(rogue.BelongsToPlayer ? "tu" : "CPU")} spendi {cost} per la suprema di {rogue.Card.Name}: {thiefPool.Current}/{thiefPool.Rules.Maximum}.");
		}
		int stolenMana = hadBuff ? Mathf.Min(2, victimPool.Current) : 0;
		if (stolenMana > 0)
		{
			victimPool.Spend(stolenMana);
			int gained = thiefPool.Gain(stolenMana);
			if (target.BelongsToPlayer) PlayManaDeltaCallout(-stolenMana); else PlayEnemyManaDeltaCallout(-stolenMana);
			if (rogue.BelongsToPlayer) PlayManaDeltaCallout(gained); else PlayEnemyManaDeltaCallout(gained);
		}

		TrackSupremeUsed(thiefPool, rogue);
		rogue.SupremeUsedThisTurn = true;
		rogue.AbilityUsedThisTurn = true;
		rogue.View?.PlaySupremeActionCallout();
		rogue.View?.PlayStrengthIncreaseCallout(stolenPower);
		RefreshPersistentStatus(target);
		RefreshPersistentStatus(rogue);
		RefreshCampaignManaPresentation();
		if (battleAnimationPlayer != null && rogue.View != null && target.View != null)
			StartCoroutine(battleAnimationPlayer.PlayRogueSupremeBlackHand(rogue.View, target.View));
		StartCoroutine(PlaySupremePresentationRoutine(rogue, playCallout: false));
		AppendLog($"SUPREMA - {rogue.Card.Name} ruba {stolenPower} Potenza e {stolenMana} mana a {target.Card.Name}.");
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
		TrackSupremeUsed(pool, attacker);
		attacker.SupremeUsedThisTurn = true;
		attacker.AbilityUsedThisTurn = true;
		if (attacker.BelongsToPlayer)
			NotifyAdventureTutorial(AdventureTutorialAction.SupremeUsed);
		RefreshCampaignManaPresentation();
		attacker.View.PlaySupremeActionCallout();

		// Palla di Fuoco e Raffica usano il dado Vigore attuale dell'attaccante,
		// abbassato di uno step, come nel motore PvP.
		int attackerBaseDieSides = tutorialMageDuelActive
			? 6
			: attacker.BelongsToPlayer
				? runProgress.PlayerVigorDieSides
				: runProgress.MasterVigorDieSides;
		int attackerDieSides = EffectiveVigorDieSides(attacker, attackerBaseDieSides);
		attackerDieSides = LowerVigorDie(attackerDieSides);
		// Nella lezione la Palla di Fuoco deve chiudere l'ondata: un tiro casuale che
		// lasciasse vivo un nemico renderebbe il copione impossibile da completare.
		int attackerValue = tutorialMageDuelActive
			? attackerDieSides
			: random.NextInclusive(1, attackerDieSides);
		VigorRollResult attackerRoll = SingleRoll(attackerDieSides, attackerValue);
		List<int> enemyDice = new List<int>(enemies.Count);
		List<int> enemyValues = new List<int>(enemies.Count);
		List<BattleCardState> defeated = new List<BattleCardState>();

		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(attackerRoll), supremeName.ToUpperInvariant(), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		foreach (BattleCardState enemy in enemies)
		{
			int baseDie = tutorialMageDuelActive
				? 6
				: enemy.BelongsToPlayer ? runProgress.PlayerVigorDieSides : runProgress.MasterVigorDieSides;
			int dieSides = EffectiveDefenseVigorDieSides(enemy, baseDie);
			int value = tutorialMageDuelActive
				? enemy.Card.Strength switch
				{
					2 => 3,
					3 => 2,
					4 => 1,
					_ => 1
				}
				: random.NextInclusive(1, dieSides);
			enemyDice.Add(dieSides);
			enemyValues.Add(value);
			enemy.View.PlayVigorRoll(diceCatalog, dieSides, TrackDiceRoll(SingleRoll(dieSides, value)), GameText.Get(GameTextKeys.Combat.RollResistance), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
			if (DisplayStrength(attacker) + attackerValue > DisplayStrength(enemy) + value
				&& !IsHealthBackedCampaignBoss(enemy))
				defeated.Add(enemy);
		}

		int attackerTotal = DisplayStrength(attacker) + attackerValue;
		SetMessage(GameText.GetLocalizedFallback(GameTextKeys.Campaign.MassSupremeStarted, "{0} scatena {1}: totale {2} contro ogni nemico.", "{0} unleashes {1}: total {2} against every enemy.", "{0} entfesselt {1}: insgesamt {2} gegen jeden Gegner.", "{0} desata {1}: total {2} contra cada enemigo.", "{0} déchaîne {1} : total {2} contre chaque ennemi.", attacker.Card.Name, supremeName, attackerTotal));
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);

		for (int index = 0; index < enemies.Count; index++)
		{
			BattleCardState enemy = enemies[index];
			int defenderTotal = DisplayStrength(enemy) + enemyValues[index];
			bool loses = attackerTotal > defenderTotal;
			AppendLog($"SUPREMA - {attacker.Card.Name} {DisplayStrength(attacker)} + D{attackerDieSides}={attackerValue} ({attackerTotal}) vs {enemy.Card.Name} {DisplayStrength(enemy)} + D{enemyDice[index]}={enemyValues[index]} ({defenderTotal}): {(loses ? "VINCE" : "RESISTE")}.");
		}
		yield return ShowCampaignMassAttackTotals(attacker, enemies, attackerValue, enemyValues);
		yield return ResolveCampaignMassSupremeBossHits(attacker, enemies, attackerTotal, enemyValues);

		int[] pendingDefeatAnimations = { 0 };
		bool impactProcessed = false;
		if (battleAnimationPlayer != null)
		{
			if (isHunter)
					yield return battleAnimationPlayer.PlayHunterVolleySupreme(
						attacker.View,
						enemies.Select(enemy => enemy.View).ToList(),
						enemies.Select(enemy => defeated.Contains(enemy)).ToList(),
						() => PlayClassAbilitySfx(HeroClass.Hunter),
						() => battleSfx?.PlayAttackResult(HeroClass.Hunter, hit: true));
			else if (defeated.Count > 0)
			{
				// Il suono deve sincronizzarsi con l'arrivo della Palla, non con il click
				// sulla suprema (che puo' anche non produrre alcuna eliminazione).
				PlayMageSupremeSfx();
				yield return battleAnimationPlayer.PlayMageFireballSupreme(
					attacker.View,
					enemies.Select(enemy => enemy.View).ToList(),
					() =>
					{
						impactProcessed = true;
						StartCampaignSupremeDefeats(attacker, defeated, pendingDefeatAnimations);
					});
			}
		}

		// Se non esiste il player VFX, risolvi comunque tutte le morti nello stesso frame.
		if (defeated.Count > 0 && !impactProcessed)
		{
			StartCampaignSupremeDefeats(attacker, defeated, pendingDefeatAnimations);
		}
		while (pendingDefeatAnimations[0] > 0)
		{
			yield return null;
		}
		yield return RestoreCampaignMassAttackTotals(attacker, enemies, attackerValue, enemyValues);

		SetMessage(defeated.Count > 0
			? GameText.GetLocalizedFallback(GameTextKeys.Campaign.MassSupremeDefeated, "{0} travolge {1} nemici!", "{0} overwhelms {1} enemies!", "{0} überwältigt {1} Gegner!", "¡{0} arrolla a {1} enemigos!", "{0} submerge {1} ennemis !", supremeName, defeated.Count)
			: GameText.GetLocalizedFallback(GameTextKeys.Campaign.MassSupremeResisted, "I nemici resistono a {0}.", "The enemies resist {0}.", "Die Gegner widerstehen {0}.", "Los enemigos resisten {0}.", "Les ennemis résistent à {0}.", supremeName));
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private bool IsHealthBackedCampaignBoss(BattleCardState enemy) =>
		IsComposableGolemProxy(enemy) || IsMedusaBossProxy(enemy) || IsTrentorBossProxy(enemy)
		|| IsBragusBossProxy(enemy) || IsJurinashorBossProxy(enemy) || IsPalatirBossProxy(enemy)
		|| IsSeraphelBossProxy(enemy);

	/// <summary>
	/// Le pedine normali perdono il confronto e vengono eliminate; i boss, invece,
	/// possiedono una riserva HP separata. Anche gli attacchi ad area devono quindi
	/// attraversare il rispettivo modello di difesa, incluse fasi e scudi.
	/// </summary>
	private IEnumerator ResolveCampaignMassSupremeBossHits(
		BattleCardState attacker,
		IReadOnlyList<BattleCardState> enemies,
		int attackerTotal,
		IReadOnlyList<int> defenderRolls)
	{
		for (int index = 0; index < enemies.Count; index++)
		{
			BattleCardState boss = enemies[index];
			if (boss == null || boss.Eliminated || !IsHealthBackedCampaignBoss(boss))
				continue;

			int defenseRoll = defenderRolls[index];
			int defenseTotal = DisplayStrength(boss) + defenseRoll;
			if (attackerTotal <= defenseTotal)
				continue;

			bool phaseChanged = false;
			bool defeated = false;
			if (IsComposableGolemProxy(boss) && activeComposableGolem != null)
			{
				activeComposableGolem.DefendAgainstRoll(attackerTotal,
					EffectiveDefenseVigorDieSides(boss, activeComposableGolem.ActiveForm.VigorDieSides),
					defenseRoll, TotalPermanentCombatBonus(boss));
				UpdateComposableGolemHealthBar(boss);
				defeated = activeComposableGolem.IsDefeated;
			}
			else if (IsMedusaBossProxy(boss) && activeMedusaBoss != null)
			{
				activeMedusaBoss.ApplyResolvedDefense(attackerTotal, defenseRoll, defenseTotal);
				UpdateMedusaBossHealthBar(boss);
				defeated = activeMedusaBoss.IsDefeated;
			}
			else if (IsTrentorBossProxy(boss) && activeTrentorBoss != null)
			{
				activeTrentorBoss.ApplyResolvedDefense(attackerTotal, defenseRoll, defenseTotal);
				UpdateTrentorBossHealthBar(boss);
				defeated = activeTrentorBoss.IsDefeated;
			}
			else if (IsBragusBossProxy(boss) && activeBragusBoss != null)
			{
				activeBragusBoss.ApplyResolvedDefense(attackerTotal, defenseRoll, defenseTotal,
					attacker.Card, DisplayStrength(attacker),
					EffectiveDefenseVigorDieSides(attacker, runProgress.PlayerVigorDieSides));
				UpdateBragusBossHealthBar(boss);
				defeated = activeBragusBoss.IsDefeated;
			}
			else if (IsJurinashorBossProxy(boss) && activeJurinashorBoss != null)
			{
				JurinashorDefenseResult result = activeJurinashorBoss.ApplyResolvedDefense(attackerTotal, defenseTotal);
				phaseChanged = result.PhaseChanged;
				UpdateJurinashorBossHealthBar(boss);
				defeated = activeJurinashorBoss.IsDefeated;
				if (!defeated) RefreshJurinashorBossPawn(boss);
			}
			else if (IsPalatirBossProxy(boss) && activePalatirBoss != null)
			{
				activePalatirBoss.ApplyResolvedDefense(attacker.Card, attackerTotal, defenseRoll, defenseTotal);
				UpdatePalatirBossHealthBar(boss);
				boss.View?.SetPalatirShields(activePalatirBoss.ActiveShields);
				defeated = activePalatirBoss.IsDefeated;
			}
			else if (IsSeraphelBossProxy(boss) && activeSeraphelBoss != null)
			{
				SeraphelDefenseResult result = activeSeraphelBoss.ApplyResolvedDefense(attackerTotal, defenseTotal);
				phaseChanged = result.PhaseChanged;
				defeated = activeSeraphelBoss.IsDefeated;
				RefreshSeraphelBossPawn(boss);
			}

			if (phaseChanged && IsJurinashorBossProxy(boss))
			{
				CleanseJurinashorPhaseTwoMaluses(boss);
				RefreshScenarioBackground();
				yield return PlayJurinashorPhaseTwoTransformation();
			}
			else if (phaseChanged && IsSeraphelBossProxy(boss))
			{
				yield return TransformSeraphelToPhaseTwo(boss);
			}

			if (!defeated)
			{
				RefreshPersistentStatus(boss);
				continue;
			}

			boss.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, boss);
			ApplyMageAuraDeathPenalty(boss, attacker);
			ApplyMightAuraDeathBonuses(boss);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(boss, attacker.Card.HeroClass);
		}
	}

	private void StartCampaignSupremeDefeats(
		BattleCardState attacker,
		IReadOnlyList<BattleCardState> defeated,
		int[] pendingDefeatAnimations)
	{
		if (defeated == null || defeated.Count == 0)
			return;

		bool startedAny = false;
		foreach (BattleCardState enemy in defeated)
		{
			if (enemy == null || enemy.Eliminated)
				continue;
			enemy.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, enemy);
			ApplyMageAuraDeathPenalty(enemy, attacker);
			ApplyMightAuraDeathBonuses(enemy);
			// Anche le carte eliminate devono uscire dal confronto con il colore base:
			// non passano dal restore riservato alle sopravvissute.
			enemy.View?.EndCombatStrengthPresentation(DisplayStrength(enemy));
			enemy.View?.SetStrengthColor(Color.white);
			enemy.View?.SetCombatStrengthScale(1f);
			RefreshPersistentStatus(enemy);
			pendingDefeatAnimations[0]++;
			startedAny = true;
			StartCoroutine(PlayCampaignSupremeDefeatAnimation(enemy, () => pendingDefeatAnimations[0]--));
		}
		if (startedAny)
			PlayDeathCardSfx();
	}

	private IEnumerator PlayCampaignSupremeDefeatAnimation(BattleCardState enemy, System.Action completed)
	{
		yield return PlayTimelineAwareDefeatAnimation(enemy, HeroClass.Mage);
		completed?.Invoke();
	}

	private IEnumerator ShowCampaignMassAttackTotals(
		BattleCardState attacker,
		IReadOnlyList<BattleCardState> enemies,
		int attackerRoll,
		IReadOnlyList<int> defenderRolls)
	{
		if (attacker == null)
			yield break;

		int attackerStart = DisplayStrength(attacker);
		int attackerTotal = attackerStart + attackerRoll;
		attacker.View?.BeginCombatStrengthPresentation(attackerStart);
		foreach (BattleCardState enemy in enemies)
			enemy?.View?.BeginCombatStrengthPresentation(DisplayStrength(enemy));

		float duration = Mathf.Clamp(configuration.Animation.CombatResultHold * 0.38f, 0.42f, 0.78f);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
			attacker.View?.SetCombatStrengthValue(Mathf.RoundToInt(Mathf.Lerp(attackerStart, attackerTotal, eased)));
			attacker.View?.SetCombatStrengthScale(Mathf.Lerp(1f, 1f + attackerRoll * 0.02f, eased));
			for (int index = 0; index < enemies.Count; index++)
			{
				BattleCardState enemy = enemies[index];
				if (enemy == null || index >= defenderRolls.Count)
					continue;
				int start = DisplayStrength(enemy);
				int total = start + defenderRolls[index];
				enemy.View?.SetCombatStrengthValue(Mathf.RoundToInt(Mathf.Lerp(start, total, eased)));
				enemy.View?.SetCombatStrengthScale(Mathf.Lerp(1f, 1f + defenderRolls[index] * 0.02f, eased));
			}
			yield return null;
		}

		int wins = 0;
		for (int index = 0; index < enemies.Count; index++)
		{
			BattleCardState enemy = enemies[index];
			if (enemy == null || index >= defenderRolls.Count)
				continue;
			int defenderTotal = DisplayStrength(enemy) + defenderRolls[index];
			bool win = attackerTotal > defenderTotal;
			wins += win ? 1 : 0;
			// Terza copia dei colori del verdetto, ora anche lei sulla tabella
			// condivisa: e il pareggio qui diventa giallo come ovunque, invece
			// di passare per una vittoria del difensore.
			enemy.View?.SetStrengthColor(
				BattlePresentationAnimationPlayer.ResolvedStrengthColor(defenderTotal, attackerTotal));
		}
		// L'attaccante ne affronta molti in una volta: verde se le vince tutte,
		// rosso se le perde tutte, giallo quando il bilancio e' misto.
		attacker.View?.SetStrengthColor(wins == enemies.Count
			? BattlePresentationAnimationPlayer.ResolvedStrengthWinnerColor
			: wins == 0
				? BattlePresentationAnimationPlayer.ResolvedStrengthLoserColor
				: BattlePresentationAnimationPlayer.ResolvedStrengthTieColor);
		yield return WaitForCardInspectionPause(Mathf.Max(0.35f, configuration.Animation.CombatResultHold * 0.42f));
	}

	private IEnumerator RestoreCampaignMassAttackTotals(
		BattleCardState attacker,
		IReadOnlyList<BattleCardState> enemies,
		int attackerRoll,
		IReadOnlyList<int> defenderRolls)
	{
		float duration = Mathf.Clamp(configuration.Animation.CombatResultHold * 0.28f, 0.28f, 0.5f);
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.unscaledDeltaTime;
			float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
			if (attacker != null)
			{
				int start = DisplayStrength(attacker);
				attacker.View?.SetCombatStrengthValue(Mathf.RoundToInt(Mathf.Lerp(start + attackerRoll, start, eased)));
				attacker.View?.SetCombatStrengthScale(Mathf.Lerp(1f + attackerRoll * 0.02f, 1f, eased));
			}
			for (int index = 0; index < enemies.Count; index++)
			{
				BattleCardState enemy = enemies[index];
				if (enemy == null || enemy.Eliminated || index >= defenderRolls.Count)
					continue;
				int start = DisplayStrength(enemy);
				enemy.View?.SetCombatStrengthValue(Mathf.RoundToInt(Mathf.Lerp(start + defenderRolls[index], start, eased)));
				enemy.View?.SetCombatStrengthScale(Mathf.Lerp(1f + defenderRolls[index] * 0.02f, 1f, eased));
			}
			yield return null;
		}
		if (attacker != null)
		{
			attacker.View?.EndCombatStrengthPresentation(DisplayStrength(attacker));
			attacker.View?.SetStrengthColor(Color.white);
			attacker.View?.SetCombatStrengthScale(1f);
		}
		foreach (BattleCardState enemy in enemies)
		{
			if (enemy == null || enemy.Eliminated)
				continue;
			enemy.View?.EndCombatStrengthPresentation(DisplayStrength(enemy));
			enemy.View?.SetStrengthColor(Color.white);
			enemy.View?.SetCombatStrengthScale(1f);
		}
	}

	/// <summary>
	/// Unica regia visiva delle supreme per campagna e PvP. Ogni nuovo VFX di
	/// suprema va aggiunto qui, mai in un ramo specifico della modalita'.
	/// </summary>
	private IEnumerator PlaySupremePresentationRoutine(BattleCardState card, bool playCallout, int paladinManaStrikes = 1,
		IReadOnlyList<PrototypeCardView> dispelTargets = null, System.Action<PrototypeCardView> onDispelImpact = null,
		IReadOnlyList<PrototypeCardView> mageMeteorTargets = null,
		IReadOnlyList<PrototypeCardView> barbarianFuryTargets = null)
	{
		if (card == null || card.View == null)
			yield break;

		if (playCallout)
			card.View.PlaySupremeActionCallout();

		if (card.Card.HeroClass == HeroClass.Warrior)
			PlayWarriorSupremeSfx();
		else if (card.Card.HeroClass == HeroClass.Mage)
			PlayMageSupremeSfx();
		else if (card.Card.HeroClass == HeroClass.Barbarian)
			PlayBarbarianSupremeSfx();
		else if (card.Card.HeroClass == HeroClass.Assassin)
			PlayAssassinSupremeSfx();
		else if (card.Card.HeroClass == HeroClass.Priest)
			PlayPriestSupremeSfx();
		else if (card.Card.HeroClass == HeroClass.Necromancer)
			PlayNecromancerSupremeSfx();

		if (card.Card.HeroClass == HeroClass.Mage && battleAnimationPlayer != null
			&& mageMeteorTargets != null && mageMeteorTargets.Count > 0)
		{
			yield return battleAnimationPlayer.PlayMageFireballSupreme(card.View, mageMeteorTargets);
		}
		else if (card.Card.HeroClass == HeroClass.Paladin && battleAnimationPlayer != null && paladinManaStrikes > 0)
		{
			// Riserva non ha bersaglio: il pulse resta visivo e non trattiene input o selezione.
			RectTransform manaIcon = card.BelongsToPlayer
				? manaRuneImage?.rectTransform
				: enemyManaRuneImage?.rectTransform;
			bool enemyReserve = !card.BelongsToPlayer;
			int finalMana = enemyReserve ? BattleCpuManaCurrent : BattlePlayerManaCurrent;
			if (enemyReserve)
				SetPresentedEnemyManaValue(finalMana - paladinManaStrikes);
			else
				SetPresentedManaValue(finalMana - paladinManaStrikes);
			battleAnimationPlayer.StartCoroutine(battleAnimationPlayer.PlayPaladinSupremePulse(
				card.View,
				manaIcon,
				enemyReserve: enemyReserve,
				manaStrikes: paladinManaStrikes,
				onManaImpact: () =>
				{
					if (enemyReserve)
					{
						SetPresentedEnemyManaValue(enemyManaDisplayedValue + 1);
						PlayEnemyManaDeltaCallout(1);
					}
					else
					{
						SetPresentedManaValue(manaDisplayedValue + 1);
						PlayManaDeltaCallout(1);
					}
				}));
		}
		else if (card.Card.HeroClass == HeroClass.Barbarian && battleAnimationPlayer != null)
		{
			// E' un VFX puramente cosmetico: non deve tenere bloccata la coda eventi
			// ne' l'input sulle pedine per i suoi 2.8 secondi di durata.
			battleAnimationPlayer.StartCoroutine(battleAnimationPlayer.PlayBarbarianSupreme(card.View));
			if (barbarianFuryTargets != null)
			{
				foreach (PrototypeCardView ally in barbarianFuryTargets)
					battleAnimationPlayer.StartCoroutine(battleAnimationPlayer.PlayBarbarianFury(ally));
			}
		}
		else if (card.Card.HeroClass == HeroClass.Priest)
		{
			// Purificazione e' istantanea e senza bersaglio. L'esplosione centrale
			// raggiunge soltanto le pedine sulle quali e' stato davvero rimosso uno stato.
			inputLocked = true;
			UpdateInteractions();
			yield return PriestSupremeVfx.Play(card.View.RectTransform, dispelTargets, onDispelImpact);
			inputLocked = false;
			UpdateInteractions();
		}
		else if (card.Card.HeroClass == HeroClass.Necromancer && battleAnimationPlayer != null)
		{
			yield return battleAnimationPlayer.PlayNecromancerMinionSupreme(card.View, card.BelongsToPlayer);
		}
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
		case HeroClass.Necromancer:
			card.NecromancerMinions += 2;
			AppendLog($"SUPREMA - {card.Card.Name} evoca due sgherri di Potenza 2.");
			break;
		}
	}

	/// <summary>Guerriero: +2, oppure +4 se e' rimasto solo. I due non si sommano.</summary>
	private void ApplyCampaignEmpower(BattleCardState card)
	{
		int bonus = AlliesOf(card).Count(ally => !ally.Eliminated) <= 1 ? 4 : 2;
		card.PermanentCombatBonus += bonus;
		WarriorSupremeVfx.Activate(card.View, bonus);
		AppendLog($"SUPREMA - {card.Card.Name} si potenzia di +{bonus} fino a fine stanza.");
	}

	/// <summary>Barbaro: la cornamusa aggiunge Furia a tutta la squadra.</summary>
	private void ApplyCampaignWarHorn(BattleCardState card)
	{
		int bonus = configuration?.ClassBalance?.BarbarianRageBonus ?? 2;
		foreach (BattleCardState ally in AlliesOf(card))
		{
			if (ally.Eliminated)
			{
				continue;
			}
			// Stessa regola del PvP: chi e' gia' infuriato non accumula una seconda
			// Furia, la cornamusa la accende soltanto a chi non ce l'ha.
			if (ally.PendingAttackBonusKind == PendingAttackBonusKind.Fury)
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
		foreach (BattleCardState target in CampaignDispelTargets(card))
			cleared += ApplyCampaignDispelToTarget(card, target);
		AppendLog($"SUPREMA - {card.Card.Name} purifica il campo: {cleared} effetti rimossi.");
	}

	private int ApplyCampaignDispelToTarget(BattleCardState caster, BattleCardState target)
	{
		if (caster == null || target == null || target.Eliminated)
			return 0;
		int cleared = 0;
		if (target.BelongsToPlayer == caster.BelongsToPlayer)
		{
			if (target.InhibitedTurns > 0) { target.InhibitedTurns = 0; cleared++; }
			if (target.PendingVigorStepPenalty > 0) { target.PendingVigorStepPenalty = 0; cleared++; }
			if (target.PermanentCombatBonus < 0) { target.PermanentCombatBonus = 0; cleared++; }
		}
		else
		{
			if (target.PendingAttackBonus > 0)
			{
				target.PendingAttackBonus = 0;
				target.PendingAttackBonusKind = PendingAttackBonusKind.None;
				cleared++;
			}
			if (target.PermanentCombatBonus > 0) { target.PermanentCombatBonus = 0; cleared++; }
			if (target.IsUntargetable) { target.IsUntargetable = false; cleared++; }
		}
		RefreshPersistentStatus(target);
		return cleared;
	}

	private List<BattleCardState> CampaignDispelTargets(BattleCardState card)
	{
		List<BattleCardState> targets = new List<BattleCardState>();
		if (card == null)
			return targets;
		foreach (BattleCardState ally in AlliesOf(card))
		{
			if (!ally.Eliminated && (ally.InhibitedTurns > 0 || ally.PendingVigorStepPenalty > 0 || ally.PermanentCombatBonus < 0))
				targets.Add(ally);
		}
		foreach (BattleCardState foe in EnemiesOf(card))
		{
			if (!foe.Eliminated && (foe.PendingAttackBonus > 0 || foe.PermanentCombatBonus > 0 || foe.IsUntargetable))
				targets.Add(foe);
		}
		return targets;
	}

	private List<BattleCardState> AlliesOf(BattleCardState card) =>
		card.BelongsToPlayer ? playerCards : cpuCards;

	private List<BattleCardState> EnemiesOf(BattleCardState card) =>
		card.BelongsToPlayer ? cpuCards : playerCards;

	/// <summary>
	/// L'invisibilita' protegge finche' esiste almeno un altro alleato attivo e
	/// visibile. Se restano solo invisibili, diventano tutti bersagliabili per evitare
	/// un deadlock.
	/// </summary>
	private bool IsShieldedByInvisibility(BattleCardState card)
	{
		return card != null
			&& card.IsUntargetable
			&& AlliesOf(card).Any(ally =>
				ally != card && !ally.Eliminated && !ally.IsUntargetable);
	}
}
}
