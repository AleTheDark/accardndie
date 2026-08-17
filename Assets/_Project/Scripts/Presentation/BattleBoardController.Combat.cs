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
	private readonly Dictionary<BattleCardState, int> combatStrengthPresentationStarts = new Dictionary<BattleCardState, int>();
	private readonly Dictionary<BattleCardState, int> combatStrengthPresentationTotals = new Dictionary<BattleCardState, int>();
	private readonly Dictionary<BattleCardState, float> combatStrengthPresentationScales = new Dictionary<BattleCardState, float>();
	private Coroutine auraActivationCalloutCoroutine;
	private Coroutine beginTurnAfterAuraCalloutCoroutine;
	private bool auraActivationCalloutVisible;
	private bool suppressInitialCarouselAfterAura;
	private const float AuraActivationCalloutDuration = 2.85f;
	private const float AuraActivationRowTransitionDuration = 0.45f;

	/// <summary>
	/// Spegne lo stato transitorio delle aure quando una run o una stanza viene lasciata.
	/// I callout di formazione sono figli della safe area, non della carta che ne esegue la
	/// coroutine: distruggere la carta a meta' animazione altrimenti li lascia orfani.
	/// </summary>
	private void ResetAuraPresentationState()
	{
		if (auraActivationCalloutCoroutine != null)
			((MonoBehaviour)this).StopCoroutine(auraActivationCalloutCoroutine);
		if (beginTurnAfterAuraCalloutCoroutine != null)
			((MonoBehaviour)this).StopCoroutine(beginTurnAfterAuraCalloutCoroutine);

		auraActivationCalloutCoroutine = null;
		beginTurnAfterAuraCalloutCoroutine = null;
		auraActivationCalloutVisible = false;
		suppressInitialCarouselAfterAura = false;
		playerAura = BattleAuraType.None;
		cpuAura = BattleAuraType.None;
		SetActiveTurnAura(null);
		DestroySafeAreaChildrenStartingWith("Formation Aura Callout - ");
	}

	private void StartBattle()
	{
		SetCombatChromeVisible(visible: true);
		ShowCombatHint();
		BeginCampaignRoomMana();
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
		ArmChallengerBonus();
		// Le aure seguono sempre la formazione schierata, in ogni stanza mostro: spegnerle
		// nelle stanze accessibili faceva vedere tre mostri della stessa fazione senza niente
		// di attivo. L'unica eccezione e' la stanza guidata del tutorial, che le aure non le
		// ha ancora spiegate: li' non se ne attiva nessuna, ne' del giocatore ne' della CPU.
		bool aurasDisabled = adventureScriptedTutorialActive;
		playerAura = aurasDisabled ? BattleAuraType.None : DetermineAura(playerCards);
		cpuAura = aurasDisabled ? BattleAuraType.None : DetermineAura(cpuCards);
		necromancerSpiritUsed = false;
		// Nelle battaglie normali annunciamo sempre l'esito della composizione:
		// anche "NO AURA" deve avere la stessa finestra e la stessa transizione.
		// Il tutorial guidato resta escluso perche' non ha ancora introdotto le aure.
		bool showAuraAnnouncement = !aurasDisabled;
		suppressInitialCarouselAfterAura = showAuraAnnouncement;
		ApplyPlayerAuraVisuals(appendLog: false);
		ApplyCpuAuraVisuals(appendLog: false);
		if (showAuraAnnouncement)
			StartAuraActivationCalloutWindow(AuraActivationRowTransitionDuration);
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
					// "Per ultimi" deve restare tale: un bonus talento rimasto attaccato
					// al dado lo rialzerebbe sopra le iniziative piu' basse in campo.
					item.InitiativeTalentBonus = 0;
					item.OpensTheFight = false;
				}
				else
				{
					hashSet.Add(item.Initiative);
				}
				// Il tie-breaker NON si ritira: e' quello con cui la timeline dello
				// schieramento ha gia' sciolto le parita' sotto gli occhi del giocatore.
				// I tiri sono unici, ma i bonus dei talenti no - un +3 su un 5 pareggia
				// l'8 di chiunque altro - e ritirarlo qui rigiocava a testa o croce
				// l'ordine appena mostrato: due pedine si scambiavano di posto in campo
				// e la timeline si invertiva appena cominciata la battaglia.
				if (item.TieBreaker == 0)
				{
					item.TieBreaker = random.NextInclusive(1, 10000);
				}
				item.View.SetInitiative(item.Initiative);
			}
			turnOrder.Sort(CompareByInitiative);
			LogDeploymentTurnOrderReorder();
			// Completa la posa di formazione (pedina centrale rialzata) prima
			// dell'annuncio aura, anche se il primo turno appartiene alla CPU.
			// Le pedine arrivano dalla griglia dello schieramento: ci scivolano,
			// perche' scriverne la posa di colpo era lo scatto che si vedeva
			// appena finito di schierare.
			RefreshCombatPawnCarousel(animate: true);
			deploymentInitiativesReady = false;
			SetMessage(GameText.Get(GameTextKeys.Combat.DeploymentComplete) + AuraStartMessage());
			RefreshInitiativeDisplay();
			if (adventureScriptedTutorialActive && adventureScriptedTutorialStep == 4 && !adventureScriptedTutorialInspectionOpened)
			{
				((MonoBehaviour)this).StartCoroutine(ShowAdventureTutorialInspectionStepAfterBattlefieldMove());
				return;
			}
			BeginCurrentTurn();
		}
		else
		{
			SetTurnBanner(playerTurn: true, GameText.Get(GameTextKeys.Combat.InitiativeBanner));
			SetMessage(GameText.Get(GameTextKeys.Combat.InitiativeStarted) + AuraStartMessage());
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
			// Qui i dadi si tirano a formazione gia' schierata: lo slot del talento e'
			// la posizione in fila, e questa e' l'unica strada che non passa dai dadi
			// d'iniziativa dello schieramento.
			ApplyInitiativeTalentsBySlot(item);
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
			string text = GameText.Get(item.BelongsToPlayer ? GameTextKeys.Common.You : GameTextKeys.Common.Cpu);
			AppendLog(GameText.Format(GameTextKeys.Combat.InitiativeLog, text, item.Card.Name, initiativeDieSides, item.Initiative)
				+ InitiativeBonusLogSuffix(item));
			item.View.PlayDiceRoll(diceCatalog, initiativeDieSides, TrackDiceRoll(item.Initiative), GameText.Format(GameTextKeys.Combat.InitiativeCallout, text), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		}
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		foreach (BattleCardState item2 in turnOrder)
		{
			item2.View.SetInitiative(item2.Initiative);
		}
		turnOrder.Sort(CompareByInitiative);
		RefreshInitiativeDisplay();
		BeginCurrentTurn();
	}

	private bool IsTimelineParticipant(BattleCardState card)
	{
		return card != null && !IsBragusBossProxy(card) && !IsJurinashorSword(card);
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
				if (card.BelongsToPlayer)
					card.PermanentCombatBonus += 2;
				else
					ReducePower(card, 1);
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

	private float CombatRollResultRevealDuration(VigorRollResult attackerRoll, VigorRollResult defenderRoll)
	{
		float duration = Mathf.Max(0.75f, configuration.Animation.DiceRollDuration);
		if (HasVigorReroll(attackerRoll) || HasVigorReroll(defenderRoll))
		{
			duration += 0.32f + Mathf.Max(0.45f, configuration.Animation.DiceRollDuration * 0.66f);
		}
		return duration + 0.25f;
	}

	private static bool HasVigorReroll(VigorRollResult roll)
	{
		return roll.FirstRollBeforeReroll > 0 || (roll.HasSecondRoll && roll.SecondRollBeforeReroll > 0);
	}

	private void StartAuraActivationCalloutWindow(float rowTransitionDelay)
	{
		if (auraActivationCalloutCoroutine != null)
			((MonoBehaviour)this).StopCoroutine(auraActivationCalloutCoroutine);

		auraActivationCalloutVisible = true;
		auraActivationCalloutCoroutine = ((MonoBehaviour)this).StartCoroutine(WaitForAuraActivationCallouts(rowTransitionDelay));
	}

	private IEnumerator WaitForAuraActivationCallouts(float rowTransitionDelay)
	{
		// Prima completa l'allineamento della formazione, poi mostra l'aura.
		if (rowTransitionDelay > 0f)
			yield return new WaitForSecondsRealtime(rowTransitionDelay);

		// Il callout parte dopo la transizione delle righe. In quel breve intervallo i
		// campi aura possono essere stati azzerati o risincronizzati da un refresh di
		// stato: ricalcoliamoli dalle tre pedine effettivamente entrate in battaglia,
		// così l'annuncio non può mostrare NO AURA per una formazione valida.
		if (!adventureScriptedTutorialActive)
		{
			playerAura = DetermineAura(playerCards);
			cpuAura = DetermineAura(cpuCards);
		}

		ApplyPlayerAuraVisuals(appendLog: true);
		ApplyCpuAuraVisuals(appendLog: true);
		// Deve corrispondere alla durata del callout in PrototypeCardView.
		yield return new WaitForSecondsRealtime(AuraActivationCalloutDuration);
		auraActivationCalloutVisible = false;
		auraActivationCalloutCoroutine = null;
		RefreshCardActionOverlays();
	}

	private void SynchronizedCombatResultHolds(
		VigorRollResult attackerRoll,
		VigorRollResult defenderRoll,
		float baseResultHold,
		out float attackerResultHold,
		out float defenderResultHold)
	{
		float rollDuration = configuration.Animation.DiceRollDuration;
		float attackerDuration = PrototypeCardView.VigorRollPresentationDuration(attackerRoll, rollDuration, baseResultHold);
		float defenderDuration = PrototypeCardView.VigorRollPresentationDuration(defenderRoll, rollDuration, baseResultHold);
		float synchronizedDuration = Mathf.Max(attackerDuration, defenderDuration);
		attackerResultHold = baseResultHold + Mathf.Max(0f, synchronizedDuration - attackerDuration);
		defenderResultHold = baseResultHold + Mathf.Max(0f, synchronizedDuration - defenderDuration);
	}

	private void BeginCurrentTurn()
	{
		// L'annuncio dell'aura e' una fase di apertura condivisa: anche la CPU
		// deve attendere che sparisca prima di eseguire il proprio primo turno.
		if (auraActivationCalloutVisible)
		{
			if (beginTurnAfterAuraCalloutCoroutine == null)
				beginTurnAfterAuraCalloutCoroutine = ((MonoBehaviour)this).StartCoroutine(BeginCurrentTurnAfterAuraCallout());
			return;
		}

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
		if (adventureScriptedTutorialActive)
		{
			int checkedTurns = 0;
			while (!turnOrder[currentTurnIndex].BelongsToPlayer && checkedTurns++ < turnOrder.Count)
				AdvanceTurnIndex();
		}
		// Qui la battaglia è ferma su un confine: la timeline ha già scelto chi tocca e
		// non c'è niente a mezz'aria. È l'unico istante in cui fotografarla ha senso, ed
		// è quello da cui una run ripresa riparte.
		SaveCurrentBattleTurn();
		BattleCardState battleCardState = turnOrder[currentTurnIndex];
		battleCardState.AbilityUsedThisTurn = false;
		battleCardState.SupremeUsedThisTurn = false;
		if (battleCardState.InhibitedTurns > 0)
		{
			battleCardState.InhibitedTurns--;
			RefreshPersistentStatus(battleCardState);
			// Il turno viene consumato automaticamente: rendiamolo evidente sulla
			// pedina, come per l'azione Salta scelta dal giocatore.
			battleCardState.View?.PlaySkipActionCallout();
			SetMessage(GameText.Format(GameTextKeys.Combat.InhibitedSkipsTurn, battleCardState.Card.Name));
			FinishTurn();
			return;
		}
		if (battleCardState.Petrified)
		{
			((MonoBehaviour)this).StartCoroutine(ResolvePetrifiedTurnStart(battleCardState));
			return;
		}
		SetActiveTurnAura(battleCardState);
		if (suppressInitialCarouselAfterAura)
		{
			// La formazione e' gia' nella sua posa finale: evitare un terzo
			// spostamento tra testo aura e pulsanti azione.
			suppressInitialCarouselAfterAura = false;
		}
		else
		{
			RefreshCombatPawnCarousel(animate: true);
		}
		RefreshInitiativeDisplay();
		if (adventureScriptedTutorialActive)
			SetAdventureTutorialTimelineVisible(visible: false);
		if (battleCardState.BelongsToPlayer)
		{
			pendingAbilityUser = null;
			attackTargetingActive = false;
			activeAttachmentSource = null;
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			SetTurnBanner(playerTurn: true, GameText.Get(GameTextKeys.Combat.PlayerTurnBanner));
			inputLocked = false;
			selectedPlayerIndex = playerCards.IndexOf(battleCardState);
			battleCardState.View.SetSelected(selected: true);
			ClearTargetHints();
			SetMessage(GameText.Get(GameTextKeys.Combat.ChooseAction));
			RefreshAbilityButton(battleCardState);
			RefreshAttachmentButton(battleCardState);
			UpdateInteractions();
			NotifyAdventureTutorial(AdventureTutorialAction.PlayerTurnStarted);
		}
		else
		{
			SetTurnBanner(playerTurn: false, GameText.Format(GameTextKeys.Combat.CpuTurnBanner, battleCardState.Card.Name.ToUpperInvariant()));
			inputLocked = true;
			selectedPlayerIndex = -1;
			attackTargetingActive = false;
			((Component)abilityButton).gameObject.SetActive(false);
			((Component)attachmentButton).gameObject.SetActive(false);
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			ClearTargetHints();
			SetMessage(GameText.Format(GameTextKeys.Combat.CpuChoosingTarget, battleCardState.Card.Name));
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
			if (playerCard != null && (Object)(object)playerCard.View != (Object)null)
				playerCard.View.SetTurnAura(playerCard == activeCard && !playerCard.Eliminated, playerOwned: true);
		}
		foreach (BattleCardState cpuCard in cpuCards)
		{
			if (cpuCard != null && (Object)(object)cpuCard.View != (Object)null)
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
		// L'attacco potenziato del Guerriero costa soltanto il mana dell'abilita':
		// il costo dell'attacco base non si somma. Il pagamento avviene qui, dopo
		// che il giocatore ha scelto un bersaglio valido.
		bool warriorAbilityAttack = attacker.Card.HeroClass == HeroClass.Warrior && attacker.AbilityArmed;
		if (IsComposableGolemProxy(defender))
		{
			if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack)) yield break;
			yield return ExecutePlayerTurnAgainstComposableGolem(attacker, defender);
			yield break;
		}
		if (IsMedusaBossProxy(defender))
		{
			if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack)) yield break;
			yield return ExecutePlayerTurnAgainstMedusa(attacker, defender);
			yield break;
		}
		if (IsTrentorBossProxy(defender))
		{
			if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack)) yield break;
			yield return ExecutePlayerTurnAgainstTrentor(attacker, defender);
			yield break;
		}
		if (IsBragusBossProxy(defender))
		{
			if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack)) yield break;
			yield return ExecutePlayerTurnAgainstBragus(attacker, defender);
			yield break;
		}
		if (IsJurinashorBossProxy(defender))
		{
			if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack)) yield break;
			yield return ExecutePlayerTurnAgainstJurinashor(attacker, defender);
			yield break;
		}
		if (IsPalatirBossProxy(defender))
		{
			if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack)) yield break;
			yield return ExecutePlayerTurnAgainstPalatir(attacker, defender);
			yield break;
		}
		if (defender.Card.HeroClass == HeroClass.Necromancer && defender.NecromancerMinions > 0)
		{
			if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack)) yield break;
			yield return ResolveCampaignNecromancerMinionDefense(attacker, defender,
				EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides));
			selectedPlayerIndex = -1;
			attacker.View.SetSelected(false);
			FinishTurn();
			yield break;
		}
		BattleCardState protectingPaladin = cpuCards.FirstOrDefault((BattleCardState card) => !card.Eliminated && card.Card.HeroClass == HeroClass.Paladin && card.AbilityArmed && (card.ProtectedAlly == null || card.ProtectedAlly == defender) && card != defender);
		BattleCardState selfProtectingPaladin = ((defender.Card.HeroClass == HeroClass.Paladin && defender.AbilityArmed && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender)) ?defender : null);
		if (protectingPaladin != null)
		{
			SetMessage(GameText.Format(GameTextKeys.Combat.CpuPaladinRedirect, protectingPaladin.Card.Name, defender.Card.Name));
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			defender = protectingPaladin;
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage(GameText.Format(GameTextKeys.Combat.CpuPaladinSelfDefense, selfProtectingPaladin.Card.Name));
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		}
		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker,
			tutorialMageDuelActive ? 6 : runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender,
			tutorialMageDuelActive ? 6 : runProgress.MasterVigorDieSides);
		BattleCardState battleCardState = protectingPaladin ?? selfProtectingPaladin;
		CombatModifiers modifiers = BuildAttackModifiers(attacker, defender, battleCardState != null, battleCardState != null);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, defender) > 0;
		CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		bool forceTutorialImpossibleRoll = ShouldRollImpossibleTutorialWarriorDuelAttack(attacker, defender);
		if ((certainty != CombatCertainty.Impossible || forceTutorialImpossibleRoll)
			&& (Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(attacker.View, defender.View, AttackTargetLineColor);
		if (certainty == CombatCertainty.Impossible && !forceTutorialImpossibleRoll)
		{
			UpdateAttackerClassStateAfterExchange(attacker, attackSucceeded: false);
			// Anche senza tiro il confronto ha un vincitore: il difensore ha retto,
			// quindi un suo Barbaro scarica la Furia esattamente come dopo una parata.
			UpdateDefenderClassStateAfterExchange(defender, attackSucceeded: false);
			AppendLog(FormatImpossibleAttackDetailed(attacker, defender, attackerDieSides, defenderDieSides, modifiers) + GameText.Get(GameTextKeys.Combat.PlayerTurnSkippedSuffix));
			SetBattlefieldMessage(GameText.Get(GameTextKeys.Combat.ImpossiblePlayerAttack));
			attacker.View?.PlaySkipActionCallout();
			selectedPlayerIndex = -1;
			attacker.View.SetSelected(selected: false);
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn(skipped: true);
			yield break;
		}
		if (IsSeraphelBossProxy(defender))
		{
			yield return ExecutePlayerTurnAgainstSeraphel(attacker, defender);
			yield break;
		}
		if (!TryPayForSelectedCampaignAttack(attacker, warriorAbilityAttack))
			yield break;
		if (battleCardState != null)
		{
			if ((Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)battleCardState.View != (Object)null)
				yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(battleCardState.View);
			battleCardState.AbilityArmed = false;
			MarkAbilityUsed(battleCardState);
			battleCardState.ProtectedAlly = null;
			RefreshPersistentStatus(battleCardState);
		}
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, defender);
		if (certainty == CombatCertainty.Guaranteed && !adventureScriptedTutorialActive)
		{
			ConsumeArmedAttackAbility(attacker, modifiers);
			((Component)abilityButton).gameObject.SetActive(false);
			((Component)attachmentButton).gameObject.SetActive(false);
			PlayResolvedAttackSfx(attacker, hit: true, modifiers.SumAttackerVigor);
			yield return PlayHunterRangedAttackIfNeeded(attacker, defender, 6, modifiers.SumAttackerVigor);
			if (hunterMarkUsed)
				ConsumeHunterMarks(defender);
			defender.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, defender);
			ApplyMageAuraDeathPenalty(defender, attacker);
			ApplyMightAuraDeathBonuses(defender);
			ConsumeVigorPenalties(attacker, defender);
			UpdateAttackerClassStateAfterExchange(attacker, attackSucceeded: true);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(defender, attacker.Card.HeroClass);
			yield return ReturnDuelSurvivors(attacker, defender);
			selectedPlayerIndex = -1;
			attacker.View.SetSelected(selected: false);
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, attackerDieSides, defenderDieSides,
			modifiers, AdventureRollBiases(attacker, defender));
		result = ScriptAdventureTutorialCombatResult(attacker, defender, result);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		bool holdDiceForTutorial = adventureScriptedTutorialActive && adventureScriptedTutorialStep == 5;
		float diceResultHold = holdDiceForTutorial ? 999f : configuration.Animation.DiceResultHold;
		SynchronizedCombatResultHolds(result.AttackerRoll, result.DefenderRoll, diceResultHold,
			out float attackerResultHold, out float defenderResultHold);
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(
			diceCatalog,
			attackerDieSides,
			TrackDiceRoll(result.AttackerRoll),
			GameText.GetOrFallbackSilent(GameTextKeys.Combat.RollAttack, "ATTACCO"),
			configuration.Animation.DiceRollDuration,
			attackerResultHold,
			empowered: nextRoomEmpowered && attacker.BelongsToPlayer,
			hideCaption: adventureScriptedTutorialActive,
			preserveDiscardedDie: adventureScriptedTutorialActive);
		defender.View.PlayVigorRoll(
			diceCatalog,
			defenderDieSides,
			TrackDiceRoll(result.DefenderRoll),
			GameText.GetOrFallbackSilent(GameTextKeys.Combat.RollDefense, "DIFESA"),
			configuration.Animation.DiceRollDuration,
			defenderResultHold,
			hideCaption: adventureScriptedTutorialActive,
			preserveDiscardedDie: adventureScriptedTutorialActive);
		yield return WaitForCardInspectionPause(holdDiceForTutorial
			? CombatRollResultRevealDuration(result.AttackerRoll, result.DefenderRoll)
			: CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		if (!IsAdventureTutorialStrengthFourTarget(defender)
			&& ShowAdventureTutorialCombatRollResult(attacker, defender, result))
		{
			yield return WaitForAdventureTutorialStepAcknowledged(6);
			attacker.View.HideActiveDiceRoll();
			defender.View.HideActiveDiceRoll();
		}
		yield return ShowCombatResult(result, attacker, defender);
		if (hunterMarkUsed)
			ConsumeHunterMarks(defender);
		PlayResolvedAttackSfx(attacker, result.DefenderIsDefeated, modifiers.SumAttackerVigor);
		if (result.DefenderIsDefeated)
		{
			yield return PlayHunterRangedAttackIfNeeded(attacker, defender, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
			defender.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, defender);
			ApplyMageAuraDeathPenalty(defender, attacker);
			ApplyMightAuraDeathBonuses(defender);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(defender, attacker.Card.HeroClass);
			ShowAdventureTutorialAfterStrengthFourDefeat(defender);
		}
		else
		{
			yield return PlayHunterMissIfNeeded(attacker, defender);
		}
		yield return ReturnDuelSurvivors(attacker, defender);
		string combatLog = FormatResultDetailed(GameText.Get(GameTextKeys.Common.You), attacker, defender, result, modifiers);
		ConsumeVigorPenalties(attacker, defender);
		UpdateClassStateAfterExchange(attacker, defender, result.DefenderIsDefeated);
		AppendLog(combatLog);
		SetBattlefieldMessage(FormatResultSummary(attacker, defender, result));
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ResolveCampaignNecromancerMinionDefense(
		BattleCardState attacker, BattleCardState necromancer, int attackerDieSides)
	{
		var minion = new CombatCard("necromancer-minion", "Sgherro", HeroClass.Necromancer, 2);
		int necromancerDie = necromancer.BelongsToPlayer
			? EffectiveDefenseVigorDieSides(necromancer, runProgress.PlayerVigorDieSides)
			: EffectiveDefenseVigorDieSides(necromancer, runProgress.MasterVigorDieSides);
		int minionDie = AccardND.GameCore.Pvp.PvpVigorScale.Lower(necromancerDie);
		CombatModifiers source = BuildAttackModifiers(attacker, necromancer, false, false);
		var modifiers = new CombatModifiers(
			source.SumAttackerVigor, false,
			source.RerollAttackerOnes, source.RerollAttackerTwos,
			source.AttackerFlatBonus, 0,
			source.NeutralizeAttackerMatchup, source.ForceAttackerAdvantage,
			attackerConditionalRerollMax: source.AttackerConditionalRerollMax);

		CombatResult result = combatResolver.ResolveAttack(
			attacker.Card, minion, attackerDieSides, minionDie, modifiers,
			AdventureRollBiases(attacker, necromancer));
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(
			diceCatalog, attackerDieSides, TrackDiceRoll(result.AttackerRoll),
			"ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		necromancer.View.PlayVigorRoll(
			diceCatalog, minionDie, TrackDiceRoll(result.DefenderRoll),
			"SGHERRO · DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		SetBattlefieldMessage($"Uno sgherro protegge {necromancer.Card.Name}: Potenza 2, Vigore D{minionDie}.");
		yield return WaitForCardInspectionPause(
			CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		yield return AccardND.Battlefield.NecromancerMinionVfx.PlayCombatStrength(
			necromancer.View?.RectTransform,
			result.DefenderTotal,
			minionSurvived: !result.DefenderIsDefeated);

		if (result.DefenderIsDefeated)
		{
			necromancer.NecromancerMinions = Math.Max(0, necromancer.NecromancerMinions - 1);
			AccardND.Battlefield.NecromancerMinionVfx.RemoveOne(necromancer.View?.RectTransform);
			bool lastMinionDied = necromancer.NecromancerMinions == 0;
			if (lastMinionDied)
			{
				foreach (BattleCardState ally in AlliesOf(necromancer))
				{
					if (ally.Eliminated) continue;
					ally.PermanentCombatBonus++;
					RefreshPersistentStatus(ally);
				}
				AppendLog($"SGHERRO - l'ultimo sgherro di {necromancer.Card.Name} viene eliminato; " +
					"tutte le pedine alleate ottengono +1 Potenza.");
				SetBattlefieldMessage("L'ultimo sgherro viene distrutto: tutte le pedine alleate ottengono +1 Potenza.");
			}
			else
			{
				AppendLog($"SGHERRO - uno sgherro di {necromancer.Card.Name} viene eliminato. " +
					$"Ne resta {necromancer.NecromancerMinions}; nessun bonus viene ancora applicato.");
				SetBattlefieldMessage("Il primo sgherro viene distrutto. Il bonus si attivera' alla morte dell'ultimo.");
			}
		}
		else
		{
			AppendLog($"SGHERRO - lo sgherro di {necromancer.Card.Name} para l'attacco e sopravvive.");
			SetBattlefieldMessage("Lo sgherro para l'attacco e sopravvive.");
		}
		ConsumeVigorPenalties(attacker, necromancer);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
	}

	private IEnumerator ExecuteCpuTurn(BattleCardState attacker)
	{
		yield return WaitForHintToClose();
		yield return WaitForCardInspectionPause(configuration.Animation.CpuThinkDelay);
		yield return WaitForHintToClose();
		if (IsTutorialWarriorDuelActive)
		{
			// Nella lezione i tre Guerrieri sono bersagli, non avversari: se attaccassero,
			// il 10 spazzerebbe via l'unica pedina del giocatore prima che la lezione arrivi
			// a spiegare la tecnica che serve proprio a batterlo.
			FinishTurn(skipped: true);
			yield break;
		}
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
		// Seraphel sceglie da se' il bersaglio (Sigilli, poi Potenza effettiva): va deviato
		// qui come gli altri boss. Lasciandolo in fondo passava prima dal calcolo del
		// bersaglio normale e, se quello risultava impossibile, il turno del boss saltava.
		if (IsSeraphelBossProxy(attacker))
		{
			yield return ExecuteSeraphelBossTurn(attacker);
			yield break;
		}
		if (TryUseCpuSupreme(attacker, out bool supremeEndsTurn))
		{
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			if (supremeEndsTurn)
				yield break;
		}
		if (ShouldCpuSkipToSaveMana(attacker, out string manaObjective))
		{
			AppendLog($"MANA - CPU conserva risorse per {manaObjective}: {CampaignCpuManaCurrent}/{CampaignManaMaximum}, salta per recuperare.");
			SetBattlefieldMessage($"{attacker.Card.Name} prepara una mossa piu forte e conserva mana.");
			attacker.View?.PlaySkipActionCallout();
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn(skipped: true);
			yield break;
		}
		if (CanCpuUseAdvancedActions(attacker) && TryChooseCpuAttachment(attacker, out var target))
		{
			yield return ExecuteCpuAttachment(attacker, target);
			yield break;
		}
		if (CanCpuUseAdvancedActions(attacker) && TryUseCpuClassAbility(attacker, out var message))
		{
			attacker.View?.PlayAbilityActionCallout();
			SetMessage(message);
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		}
		// L'attacco CPU segue la stessa economia di quello del giocatore: costa 1 mana.
		// Le abilita' CPU vengono scelte solo se lasciano disponibile anche questo costo;
		// il controllo resta qui come garanzia per attivazioni partite senza mana.
		string decisionReason;
		int index = ChooseCpuTarget(attacker, out decisionReason);
		BattleCardState intendedTarget = playerCards[index];
		AppendLog(GameText.Format(GameTextKeys.Combat.CpuTargetChoiceLog, attacker.Card.Name, intendedTarget.Card.Name, decisionReason));
		yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		BattleCardState defender = ResolveCpuAttackDefender(intendedTarget, out BattleCardState paladinProtectionUser);
		if (defender != intendedTarget)
		{
			SetMessage(GameText.Format(GameTextKeys.Combat.PaladinRedirect, defender.Card.Name, intendedTarget.Card.Name));
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		}
		else if (paladinProtectionUser != null)
		{
			SetMessage(GameText.Format(GameTextKeys.Combat.PaladinSelfDefense, paladinProtectionUser.Card.Name));
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
		}
		if (defender.Card.HeroClass == HeroClass.Necromancer && defender.NecromancerMinions > 0)
		{
			if (!TrySpendCampaignAttackMana(attacker))
			{
				FinishTurn(skipped: true);
				yield break;
			}
			yield return ResolveCampaignNecromancerMinionDefense(attacker, defender,
				EffectiveVigorDieSides(attacker, runProgress.MasterVigorDieSides));
			FinishTurn();
			yield break;
		}
		int attackerDieSides = EffectiveVigorDieSides(attacker, runProgress.MasterVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, defender, paladinProtectionUser != null, paladinProtectionUser != null);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, defender) > 0;
		CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(attacker.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers);
		if (certainty != CombatCertainty.Impossible)
		{
			attacker.View?.PlayAttackActionCallout();
			if ((Object)(object)battleAnimationPlayer != (Object)null)
				yield return battleAnimationPlayer.PlayTargetLine(attacker.View, defender.View, AttackTargetLineColor);
		}
		if (certainty == CombatCertainty.Impossible)
		{
			UpdateAttackerClassStateAfterExchange(attacker, attackSucceeded: false);
			// Come nel turno del giocatore: il difensore ha vinto il confronto e un
			// suo Barbaro deve scaricare la Furia che gli ha reso l'attacco impossibile.
			UpdateDefenderClassStateAfterExchange(defender, attackSucceeded: false);
			AppendLog(FormatImpossibleAttackDetailed(attacker, defender, attackerDieSides, defenderDieSides, modifiers) + GameText.Get(GameTextKeys.Combat.CpuTurnSkippedSuffix));
			SetBattlefieldMessage(GameText.Get(GameTextKeys.Combat.ImpossibleCpuAttack));
			attacker.View?.PlaySkipActionCallout();
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn(skipped: true);
			yield break;
		}
		if (!TrySpendCampaignAttackMana(attacker))
		{
			AppendLog($"MANA - CPU senza mana per l'attacco di {attacker.Card.Name}: salta il turno.");
			SetBattlefieldMessage($"{attacker.Card.Name} non ha mana per attaccare e salta il turno.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			FinishTurn(skipped: true);
			yield break;
		}
		// La protezione viene consumata solo quando l'attacco viene davvero risolto.
		// Un attacco impossibile non produce alcuna difesa e lascia quindi attivo il buff.
		if (paladinProtectionUser != null)
		{
			if ((Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)paladinProtectionUser.View != (Object)null)
				yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(paladinProtectionUser.View);
			paladinProtectionUser.AbilityArmed = false;
			MarkAbilityUsed(paladinProtectionUser);
			paladinProtectionUser.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(paladinProtectionUser);
		}
		bool jurinashorSwordAttack = IsJurinashorBossProxy(attacker) && ActiveJurinashorSwordCount() > 0;
		if (jurinashorSwordAttack)
			yield return AimJurinashorSwordsAt(defender);
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, defender);
		if (certainty == CombatCertainty.Guaranteed)
		{
			ConsumeArmedAttackAbility(attacker, modifiers);
			if (jurinashorSwordAttack)
				yield return PlayJurinashorSwordExecution(attacker, defender, 6, modifiers.SumAttackerVigor, aimFirst: false);
			else
			{
				PlayResolvedAttackSfx(attacker, hit: true, modifiers.SumAttackerVigor);
				yield return PlayHunterRangedAttackIfNeeded(attacker, defender, 6, modifiers.SumAttackerVigor);
			}
			if (hunterMarkUsed)
				ConsumeHunterMarks(defender);
			defender.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, defender);
			ConsumeVigorPenalties(attacker, defender);
			UpdateAttackerClassStateAfterExchange(attacker, attackSucceeded: true);
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMageAuraDeathPenalty(defender, attacker);
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, attacker.Card.HeroClass);
			}
			yield return ReturnDuelSurvivors(attacker, defender);
			yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
			FinishTurn();
			yield break;
		}
		CombatResult result = combatResolver.ResolveAttack(attacker.Card, defender.Card, attackerDieSides, defenderDieSides,
			modifiers, AdventureRollBiases(attacker, defender));
		result = ScriptAdventureTutorialCombatResult(attacker, defender, result);
		ConsumeArmedAttackAbility(attacker, modifiers);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		SynchronizedCombatResultHolds(result.AttackerRoll, result.DefenderRoll, configuration.Animation.DiceResultHold,
			out float attackerResultHold, out float defenderResultHold);
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(
			diceCatalog,
			attackerDieSides,
			TrackDiceRoll(result.AttackerRoll),
			GameText.GetOrFallbackSilent(GameTextKeys.Combat.RollCpuAttack, "ATTACCO CPU"),
			configuration.Animation.DiceRollDuration,
			attackerResultHold,
			hideCaption: adventureScriptedTutorialActive,
			preserveDiscardedDie: adventureScriptedTutorialActive);
		defender.View.PlayVigorRoll(
			diceCatalog,
			defenderDieSides,
			TrackDiceRoll(result.DefenderRoll),
			GameText.GetOrFallbackSilent(GameTextKeys.Combat.RollYourDefense, "TUA DIFESA"),
			configuration.Animation.DiceRollDuration,
			defenderResultHold,
			hideCaption: adventureScriptedTutorialActive,
			preserveDiscardedDie: adventureScriptedTutorialActive);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, defender);
		if (jurinashorSwordAttack)
		{
			if (result.DefenderIsDefeated)
				yield return PlayJurinashorSwordExecution(
					attacker,
					defender,
					result.AttackerTotal - result.DefenderTotal,
					result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum,
					aimFirst: false);
			else
				ReleaseJurinashorSwordAim();
		}
		if (hunterMarkUsed)
			ConsumeHunterMarks(defender);
		if (!jurinashorSwordAttack || !result.DefenderIsDefeated)
			PlayResolvedAttackSfx(attacker, result.DefenderIsDefeated, modifiers.SumAttackerVigor);
		if (result.DefenderIsDefeated)
		{
			if (!jurinashorSwordAttack)
				yield return PlayHunterRangedAttackIfNeeded(attacker, defender, result.AttackerTotal - result.DefenderTotal, result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
			defender.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, defender);
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMageAuraDeathPenalty(defender, attacker);
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
		string combatLog = FormatResultDetailed(GameText.Get(GameTextKeys.Common.Cpu), attacker, defender, result, modifiers);
		ConsumeVigorPenalties(attacker, defender);
		UpdateClassStateAfterExchange(attacker, defender, result.DefenderIsDefeated);
		AppendLog(combatLog);
		BattleCardState counterPaladin = paladinProtectionUser
			?? (defender.Card.HeroClass == HeroClass.Paladin ? defender : null);
		if (counterPaladin != null && AuraFor(counterPaladin) == BattleAuraType.Paladin && !counterPaladin.Eliminated && !attacker.Eliminated)
		{
			yield return ExecutePaladinCounter(counterPaladin, attacker);
		}
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstComposableGolem(BattleCardState attacker, BattleCardState golemProxy)
	{
		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, golemProxy, defenderAdvantage: false, neutralizeAttackerMatchup: false);
		bool hunterMarkUsed = HunterMarkAttackBonus(attacker, golemProxy) > 0;
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, golemProxy);
		VigorRollResult attackerRoll = RollGolemAttackerVigor(attacker, golemProxy, attackerDieSides, modifiers);
		int golemVigorDieSides = EffectiveVigorDieSides(golemProxy, activeComposableGolem.ActiveForm.VigorDieSides);
		int golemVigorRoll = random.NextInclusive(1, golemVigorDieSides);
		int golemDefenseTotal = activeComposableGolem.ActiveForm.Power + modifiers.DefenderFlatBonus + golemVigorRoll;
		int initialAttackerTotal = attacker.Card.Strength + attackerRoll.SelectedRoll + modifiers.AttackerFlatBonus;
		if (modifiers.AttackerConditionalRerollMax > 0 && initialAttackerTotal <= golemDefenseTotal)
		{
			attackerRoll = RerollGolemAttackerVigor(attackerRoll, modifiers.AttackerConditionalRerollMax);
		}
		int attackerTotal = attacker.Card.Strength + attackerRoll.SelectedRoll + modifiers.AttackerFlatBonus;
		ComposableGolemDefenseResult golemResult = activeComposableGolem.DefendAgainstRoll(
			attackerTotal,
			golemVigorDieSides,
			golemVigorRoll,
			modifiers.DefenderFlatBonus);
		VigorRollResult golemRoll = SingleRoll(golemResult.VigorDieSides, golemResult.VigorRoll);
		CombatResult result = new CombatResult(attackerRoll, golemRoll, attackerTotal, golemResult.DefenseTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(attackerRoll), "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		golemProxy.View.PlayVigorRoll(
			diceCatalog,
			golemResult.VigorDieSides,
			TrackDiceRoll(golemRoll),
			GameText.GetOrFallbackSilent(
				GameTextKeys.Combat.RollDefenseNamed,
				"DIFESA {0}",
				GolemFormName(golemResult.Form.Form)),
			configuration.Animation.DiceRollDuration,
			configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(attackerRoll, golemRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, golemProxy);
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
			yield return golemProxy.View.PlayComposableGolemDefenseEffect(golemResult.Form.Form, resisted: true);
		}
		UpdateComposableGolemHealthBar(golemProxy);
		if (activeComposableGolem.IsDefeated)
		{
			golemProxy.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, golemProxy);
			ApplyMageAuraDeathPenalty(golemProxy, attacker);
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
		UpdateAttackerClassStateAfterExchange(attacker, golemResult.Damage > 0);
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

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, medusaProxy.Card, attackerDieSides, defenderDieSides,
			modifiers, AdventureRollBiases(attacker, medusaProxy));
		MedusaDefenseResult medusaResult = activeMedusaBoss.ApplyResolvedDefense(
			result.AttackerTotal,
			result.DefenderRoll.SelectedRoll,
			result.DefenderTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(result.AttackerRoll), "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		medusaProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(result.DefenderRoll), "DIFESA MEDUSA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
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
			RegisterCampaignEliminationMana(attacker, medusaProxy);
			ApplyMageAuraDeathPenalty(medusaProxy, attacker);
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
		UpdateAttackerClassStateAfterExchange(attacker, medusaResult.Damage > 0);
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

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, trentorProxy.Card, attackerDieSides, defenderDieSides,
			modifiers, AdventureRollBiases(attacker, trentorProxy));
		TrentorDefenseResult trentorResult = activeTrentorBoss.ApplyResolvedDefense(
			result.AttackerTotal,
			result.DefenderRoll.SelectedRoll,
			result.DefenderTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(result.AttackerRoll), "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		trentorProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(result.DefenderRoll), "DIFESA TRENTOR", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, trentorProxy);

		if (hunterMarkUsed)
			ConsumeHunterMarks(trentorProxy);
		PlayResolvedAttackSfx(attacker, trentorResult.Damage > 0, modifiers.SumAttackerVigor);
		if (trentorResult.Damage > 0)
		{
			PlayTrentorTakeDamageSfx();
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
			RegisterCampaignEliminationMana(attacker, trentorProxy);
			ApplyMageAuraDeathPenalty(trentorProxy, attacker);
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
		UpdateAttackerClassStateAfterExchange(attacker, trentorResult.Damage > 0);
		string reactiveRoots = trentorResult.Damage > 0 ? $" Radici Reattive: {attacker.Card.Name} viene marcato." : string.Empty;
		SetMessage(trentorResult.Damage > 0
			?$"{attacker.Card.Name} infligge {trentorResult.Damage} danni a Trentor. HP {trentorResult.HitPointsAfter}/{activeTrentorBoss.MaxHitPoints}.{reactiveRoots}"
			:$"{attacker.Card.Name} non supera la corteccia di Trentor. HP {trentorResult.HitPointsAfter}/{activeTrentorBoss.MaxHitPoints}.");
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(selected: false);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		FinishTurn();
	}

	private IEnumerator ExecutePlayerTurnAgainstJurinashor(BattleCardState attacker, BattleCardState boss)
	{
		if (activeJurinashorBoss == null)
		{
			FinishTurn();
			yield break;
		}

		int attackerDieSides = EffectivePlayerAttackVigorDieSides(attacker, runProgress.PlayerVigorDieSides);
		int defenderDieSides = EffectiveDefenseVigorDieSides(boss, JurinashorBoss.DefaultVigorDieSides);
		CombatModifiers modifiers = BuildAttackModifiers(attacker, boss, false, false);
		if (!UsesStationaryClassAttack(attacker))
			yield return MoveDuelToCenter(attacker, boss);

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, boss.Card,
			attackerDieSides, defenderDieSides, modifiers, AdventureRollBiases(attacker, boss));
		JurinashorDefenseResult defense = activeJurinashorBoss.ApplyResolvedDefense(
			result.AttackerTotal, result.DefenderTotal);
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(result.AttackerRoll),
			"ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		boss.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(result.DefenderRoll),
			"DIFESA JURINASHOR", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, attacker, boss);

		PlayResolvedAttackSfx(attacker, defense.Damage > 0, modifiers.SumAttackerVigor);
		if (defense.Damage > 0)
			yield return PlayHunterRangedAttackIfNeeded(attacker, boss,
				result.AttackerTotal - result.DefenderTotal,
				result.AttackerRoll.SelectionMode == VigorSelectionMode.Sum);
		else
			yield return PlayJurinashorBlockedAttack(attacker, boss);

		UpdateJurinashorBossHealthBar(boss);
		if (activeJurinashorBoss.IsDefeated)
		{
			boss.Eliminated = true;
			RegisterCampaignEliminationMana(attacker, boss);
			ApplyMageAuraDeathPenalty(boss, attacker);
			ApplyMightAuraDeathBonuses(boss);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(boss, attacker.Card.HeroClass);
		}
		else
		{
			RefreshJurinashorBossPawn(boss);
		}
		if (defense.PhaseChanged)
		{
			CleanseJurinashorPhaseTwoMaluses(boss);
			RefreshScenarioBackground();
			yield return PlayJurinashorPhaseTwoTransformation();
		}

		yield return ReturnDuelSurvivors(attacker, boss);
		ConsumeVigorPenalties(attacker, boss);
		UpdateAttackerClassStateAfterExchange(attacker, defense.Damage > 0);
		SetMessage(defense.PhaseChanged
			? $"JURINASHOR - FASE II: recupera {activeJurinashorBoss.MaxHitPoints} HP, può evocare fino a 5 spade e raddoppia il mana da parata e fine turno."
			: defense.Damage > 0
			? $"{attacker.Card.Name} infligge {defense.Damage} danni a Jurinashor. HP {defense.HitPointsAfter}/{activeJurinashorBoss.MaxHitPoints}."
			: $"Jurinashor devia il colpo. HP {defense.HitPointsAfter}/{activeJurinashorBoss.MaxHitPoints}.");
		selectedPlayerIndex = -1;
		attacker.View.SetSelected(false);
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

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, bragusProxy.Card, attackerDieSides, defenderDieSides,
			modifiers, AdventureRollBiases(attacker, bragusProxy));
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
			counterPaladinProtectionUser != null,
			AdventurePlayerHighRollChancePercent());
		ConsumeArmedAttackAbility(attacker, modifiers);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)attachmentButton).gameObject.SetActive(false);

		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(result.AttackerRoll), "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		bragusProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(result.DefenderRoll), "DIFESA BRAGUS", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
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
			RegisterCampaignEliminationMana(attacker, bragusProxy);
			ApplyMageAuraDeathPenalty(bragusProxy, attacker);
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
				if ((Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)counterPaladinProtectionUser.View != (Object)null)
					yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(counterPaladinProtectionUser.View);
				counterPaladinProtectionUser.AbilityArmed = false;
				MarkAbilityUsed(counterPaladinProtectionUser);
				counterPaladinProtectionUser.ProtectedAlly = null;
				TriggerMagicAuraAfterAbility();
				RefreshPersistentStatus(counterPaladinProtectionUser);
				AppendLog("PALADINO - " + counterPaladinProtectionUser.Card.Name + " attiva la difesa contro il contrattacco di Bragus: vantaggio al tiro difesa.");
			}
			VigorRollResult counterRoll = SingleRoll(BragusBoss.DefaultVigorDieSides, bragusResult.CounterRoll);
			VigorRollResult attackerDefenseRoll = bragusResult.TargetDefenseRoll;
			CombatResult counterResult = new CombatResult(counterRoll, attackerDefenseRoll, bragusResult.CounterTotal, bragusResult.TargetDefenseTotal);
			bragusProxy.View?.PlayAttackActionCallout();
			PlayBragusAttackSfx();
			messagePanelWasHidden = HideMessagePanelForDiceRoll();
			PlayRollingDiceSfx();
			bragusProxy.View.PlayVigorRoll(diceCatalog, BragusBoss.DefaultVigorDieSides, TrackDiceRoll(counterRoll), "CONTRATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
			attacker.View.PlayVigorRoll(diceCatalog, attackerDefenseDieSides, TrackDiceRoll(attackerDefenseRoll), "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
			yield return WaitForCardInspectionPause(CombatRollPresentationDuration(counterResult.AttackerRoll, counterResult.DefenderRoll));
			RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
			yield return ShowCombatResult(counterResult, bragusProxy, attacker);
			if ((Object)(object)battleAnimationPlayer != (Object)null)
				yield return battleAnimationPlayer.PlayBragusCleaverCounterattack(bragusProxy.View, attacker.View, bragusResult.CounterDefeatsAttacker, PlayBragusAttackHitSfx);
			else if (bragusResult.CounterDefeatsAttacker)
				PlayBragusAttackHitSfx();
			if (bragusResult.CounterDefeatsAttacker)
			{
				attacker.Eliminated = true;
				RegisterCampaignEliminationMana(bragusProxy, attacker);
				if (!TryCreateNecromancerSpirit(attacker))
				{
					ApplyMageAuraDeathPenalty(attacker, bragusProxy);
					ApplyMightAuraDeathBonuses(attacker);
					PlayDeathCardSfx();
					yield return PlayTimelineAwareDefeatAnimation(attacker, bragusProxy.Card.HeroClass);
				}
			}
		}

		yield return ReturnDuelSurvivors(attacker, bragusProxy);
		ConsumeVigorPenalties(attacker, bragusProxy);
		UpdateAttackerClassStateAfterExchange(attacker, bragusResult.Damage > 0);
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

		CombatResult result = combatResolver.ResolveAttack(attacker.Card, palatirProxy.Card, attackerDieSides, defenderDieSides,
			modifiers, AdventureRollBiases(attacker, palatirProxy));
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
		attacker.View.PlayVigorRoll(diceCatalog, attackerDieSides, TrackDiceRoll(result.AttackerRoll), "ATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		palatirProxy.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(result.DefenderRoll), activePalatirBoss.HasActiveShields ? "DIFESA SCUDI" : "DIFESA PALATIR", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
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
			RegisterCampaignEliminationMana(attacker, palatirProxy);
			ApplyMageAuraDeathPenalty(palatirProxy, attacker);
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
		UpdateAttackerClassStateAfterExchange(attacker, palatirResult.ShieldWasBroken || palatirResult.Damage > 0);
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
			MarkAbilityUsed(protectingPaladin);
			protectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO: " + selfProtectingPaladin.Card.Name + " si difende dal Golem con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			MarkAbilityUsed(selfProtectingPaladin);
			selfProtectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(selfProtectingPaladin);
		}
		BattleCardState paladinProtectionUser = protectingPaladin ?? selfProtectingPaladin;
		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		int golemVigorDieSides = EffectiveVigorDieSides(golemProxy, activeComposableGolem.ActiveForm.VigorDieSides);
		ComposableGolemAttackResult golemResult = activeComposableGolem.Attack(
			defender.Card,
			defenderDieSides,
			golemVigorDieSides,
			TotalPermanentCombatBonus(golemProxy));
		VigorRollResult golemRoll = SingleRoll(golemResult.VigorDieSides, golemResult.VigorRoll);
		VigorRollResult defenderRoll = SingleRoll(defenderDieSides, golemResult.TargetVigorRoll);
		CombatResult result = new CombatResult(golemRoll, defenderRoll, golemResult.AttackTotal, golemResult.TargetDefenseTotal);
		golemProxy.View?.PlayAttackActionCallout();
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(golemProxy.View, defender.View, AttackTargetLineColor);
		if (paladinProtectionUser != null && (Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)paladinProtectionUser.View != (Object)null)
			yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(paladinProtectionUser.View);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		golemProxy.View.PlayVigorRoll(
			diceCatalog,
			golemResult.VigorDieSides,
			TrackDiceRoll(golemRoll),
			GameText.GetOrFallbackSilent(
				GameTextKeys.Combat.RollAttackNamed,
				"ATTACCO {0}",
				GolemFormName(golemResult.Form.Form)),
			configuration.Animation.DiceRollDuration,
			configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(defenderRoll), "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, golemProxy, defender);
		PlayComposableGolemAttackSfx(golemResult.Form.Form);
		yield return golemProxy.View.PlayComposableGolemAttackEffect(defender.View, golemResult.Form.Form, golemResult.TargetIsDefeated);
		if (golemResult.TargetIsDefeated)
		{
			defender.Eliminated = true;
			RegisterCampaignEliminationMana(golemProxy, defender);
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMageAuraDeathPenalty(defender, golemProxy);
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, golemProxy.Card.HeroClass);
			}
		}
		yield return RestoreCombatStrengthPresentation(golemProxy, defender);
		ConsumeVigorPenalties(golemProxy, defender);
		// Come per gli altri boss: chi difende alimenta o scarica la propria Furia.
		UpdateDefenderClassStateAfterExchange(defender, golemResult.TargetIsDefeated);
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

		medusaProxy.View?.PlayAbilityActionCallout();
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
		yield return PlayMedusaGazeGroupRoll(medusaProxy, gaze, targets, targetDice);
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);

		if (gaze.PetrifiesTargets)
		{
			PlayMedusaPetrifyingGazeSfx();
			List<BattleCardState> petrifiedTargets = targets
				.Where((BattleCardState target, int index) => gaze.PetrifiesTarget(index))
				.ToList();
			if ((Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)medusaProxy.View != (Object)null)
			{
				List<PrototypeCardView> targetViews = petrifiedTargets
					.Where((BattleCardState target) => (Object)(object)target.View != (Object)null)
					.Select((BattleCardState target) => target.View)
					.ToList();
				int strongestMargin = petrifiedTargets
					.Select((BattleCardState target) => targets.IndexOf(target))
					.Max((int index) => gaze.MedusaTotal - gaze.TargetTotals[index]);
				yield return battleAnimationPlayer.PlayMedusaPetrifyingGaze(medusaProxy.View, targetViews, strongestMargin);
			}
			foreach (BattleCardState target in petrifiedTargets)
			{
				target.Petrified = true;
				RefreshPersistentStatus(target);
				target.View?.PlayPetrifiedCallout();
			}
			string petrifiedNames = string.Join(", ", petrifiedTargets.Select((BattleCardState target) => target.Card.Name));
			AppendLog($"MEDUSA - {FormatMedusaGazeRolls(gaze)} = {gaze.MedusaTotal} contro {FormatMedusaAllyRolls(targets, gaze, targetDice)}: pietrifica solo {petrifiedNames}.");
			SetMessage($"SGUARDO PIETRIFICANTE: Medusa pietrifica solo le carte che supera nel confronto: {petrifiedNames}.");
		}
		else
		{
			AppendLog($"MEDUSA - {FormatMedusaGazeRolls(gaze)} = {gaze.MedusaTotal} contro {FormatMedusaAllyRolls(targets, gaze, targetDice)}: nessuna carta viene superata.");
			SetMessage("Tutte le carte resistono allo sguardo di Medusa.");
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
			trentorProxy.View?.PlayAbilityActionCallout();
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
			MarkAbilityUsed(protectingPaladin);
			protectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO: " + selfProtectingPaladin.Card.Name + " si difende dai rampicanti con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			MarkAbilityUsed(selfProtectingPaladin);
			selfProtectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(selfProtectingPaladin);
		}
		BattleCardState paladinProtectionUser = protectingPaladin ?? selfProtectingPaladin;

		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		bool markedTargetBonus = defender == markedTarget;
		TrentorAttackResult trentorResult = activeTrentorBoss.Attack(
			defender.Card, defenderDieSides, markedTargetBonus, AdventurePlayerHighRollChancePercent());
		VigorRollResult trentorRoll = SingleRoll(TrentorBoss.DefaultVigorDieSides, trentorResult.VigorRoll);
		VigorRollResult defenderRoll = SingleRoll(defenderDieSides, trentorResult.TargetVigorRoll);
		CombatResult result = new CombatResult(trentorRoll, defenderRoll, trentorResult.AttackTotal, trentorResult.TargetDefenseTotal);
		trentorProxy.View?.PlayAttackActionCallout();
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(trentorProxy.View, defender.View, new Color(0.22f, 0.92f, 0.24f, 1f));
		if (paladinProtectionUser != null && (Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)paladinProtectionUser.View != (Object)null)
			yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(paladinProtectionUser.View);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		trentorProxy.View.PlayVigorRoll(diceCatalog, TrentorBoss.DefaultVigorDieSides, TrackDiceRoll(trentorRoll), trentorResult.MarkedTargetBonus ? "ATTACCO MARCATO" : "ATTACCO TRENTOR", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(defenderRoll), "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
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
			RegisterCampaignEliminationMana(trentorProxy, defender);
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMageAuraDeathPenalty(defender, trentorProxy);
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, trentorProxy.Card.HeroClass);
			}
		}
		yield return RestoreCombatStrengthPresentation(trentorProxy, defender);
		// Anche l'attacco di un boss e' un confronto in difesa: il Barbaro si infuria
		// se lo perde e scarica la Furia se regge. Va dopo il ripristino dei badge,
		// altrimenti la presentazione dei totali sovrascrive la Potenza aggiornata.
		UpdateDefenderClassStateAfterExchange(defender, trentorResult.TargetIsDefeated);
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
			MarkAbilityUsed(protectingPaladin);
			protectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(protectingPaladin);
		}
		else if (selfProtectingPaladin != null)
		{
			SetMessage("PALADINO: " + selfProtectingPaladin.Card.Name + " si difende dalla cometa con vantaggio.");
			yield return WaitForCardInspectionPause(configuration.Animation.CpuDecisionReveal);
			selfProtectingPaladin.AbilityArmed = false;
			MarkAbilityUsed(selfProtectingPaladin);
			selfProtectingPaladin.ProtectedAlly = null;
			TriggerMagicAuraAfterAbility();
			RefreshPersistentStatus(selfProtectingPaladin);
		}
		BattleCardState paladinProtectionUser = protectingPaladin ?? selfProtectingPaladin;

		int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
		PalatirAttackResult palatirResult = activePalatirBoss.Attack(defender.Card, defenderDieSides);
		VigorRollResult palatirRoll = SingleRoll(PalatirBoss.DefaultVigorDieSides, palatirResult.VigorRoll);
		VigorRollResult defenderRoll = SingleRoll(defenderDieSides, palatirResult.TargetVigorRoll);
		CombatResult result = new CombatResult(palatirRoll, defenderRoll, palatirResult.AttackTotal, palatirResult.TargetDefenseTotal);
		palatirProxy.View?.PlayAttackActionCallout();
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(palatirProxy.View, defender.View, new Color(0.58f, 0.2f, 1f, 1f));
		if (paladinProtectionUser != null && (Object)(object)battleAnimationPlayer != (Object)null && (Object)(object)paladinProtectionUser.View != (Object)null)
			yield return battleAnimationPlayer.PlayPaladinProtectionConstellation(paladinProtectionUser.View);
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		palatirProxy.View.PlayVigorRoll(diceCatalog, PalatirBoss.DefaultVigorDieSides, TrackDiceRoll(palatirRoll), "ATTACCO COSMICO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		defender.View.PlayVigorRoll(diceCatalog, defenderDieSides, TrackDiceRoll(defenderRoll), "TUA DIFESA", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, palatirProxy, defender);
		PlayPalatirCosmicAttackSfx();
		yield return palatirProxy.View.PlayPalatirCosmicAttackEffect(defender.View, palatirResult.TargetIsDefeated);
		PlayAttackResultSfx(palatirProxy, palatirResult.TargetIsDefeated);
		if (palatirResult.TargetIsDefeated)
		{
			defender.Eliminated = true;
			RegisterCampaignEliminationMana(palatirProxy, defender);
			if (!TryCreateNecromancerSpirit(defender))
			{
				ApplyMageAuraDeathPenalty(defender, palatirProxy);
				ApplyMightAuraDeathBonuses(defender);
				PlayDeathCardSfx();
				yield return PlayTimelineAwareDefeatAnimation(defender, palatirProxy.Card.HeroClass);
			}
		}
		yield return RestoreCombatStrengthPresentation(palatirProxy, defender);
		// Stessa regola di Trentor e Seraphel: la difesa contro il boss alimenta o
		// scarica la Furia del Barbaro.
		UpdateDefenderClassStateAfterExchange(defender, palatirResult.TargetIsDefeated);
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
		card.View.PlayVigorRoll(diceCatalog, dieSides, TrackDiceRoll(SingleRoll(dieSides, result.Roll)), GameText.Get(GameTextKeys.Combat.UnpetrifyRoll), configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);

		if (result.Freed)
		{
			card.View?.PlayFreedCallout();
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
		card.View?.PlayCrumbledCallout();
		RegisterCampaignEliminationMana(null, card);
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
		BattleCardState medusaProxy,
		MedusaPetrifyingGazeResult gaze,
		IReadOnlyList<BattleCardState> targets,
		IReadOnlyList<int> targetDice)
	{
		if (medusaProxy?.View != null && gaze.MedusaRolls.Count > 0)
		{
			VigorRollResult medusaRoll = gaze.MedusaRolls[0];
			medusaProxy.View.PlayVigorRoll(
				diceCatalog,
				medusaRoll.DieSides,
				TrackDiceRoll(medusaRoll),
				"SGUARDO PIETRIFICANTE",
				configuration.Animation.DiceRollDuration,
				configuration.Animation.DiceResultHold);
		}

		int count = Math.Min(targets?.Count ?? 0, Math.Min(gaze.TargetRolls.Count, targetDice?.Count ?? 0));
		for (int index = 0; index < count; index++)
		{
			BattleCardState target = targets[index];
			if (target?.View == null)
				continue;
			target.View.PlayVigorRoll(
				diceCatalog,
				targetDice[index],
				TrackDiceRoll(SingleRoll(targetDice[index], gaze.TargetRolls[index])),
				GameText.Get(GameTextKeys.Combat.RollResistance),
				configuration.Animation.DiceRollDuration,
				configuration.Animation.DiceResultHold);
		}
		yield return WaitForCardInspectionPause(configuration.Animation.DiceRollDuration + configuration.Animation.DiceResultHold);
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

	private VigorRollResult RollGolemAttackerVigor(
		BattleCardState attacker,
		BattleCardState golemProxy,
		int dieSides,
		CombatModifiers modifiers)
	{
		if (modifiers.SumAttackerVigor)
		{
			int first = random.NextInclusive(1, dieSides);
			int second = random.NextInclusive(1, AccardND.GameCore.Pvp.PvpVigorScale.Lower(dieSides));
			return new VigorRollResult(dieSides, first, second, hasSecondRoll: true, first + second, MatchupResult.Neutral, VigorSelectionMode.Sum);
		}

		MatchupResult matchup = modifiers.ForceAttackerAdvantage
			? MatchupResult.Advantage
			: modifiers.NeutralizeAttackerMatchup
				? MatchupResult.Neutral
				: ClassMatchup.Compare(attacker.Card.HeroClass, golemProxy.Card.HeroClass);
		int firstRoll = random.NextInclusive(1, dieSides);
		if (matchup == MatchupResult.Neutral)
			return SingleRoll(dieSides, firstRoll);

		int secondRoll = random.NextInclusive(1, dieSides);
		VigorSelectionMode selectionMode = matchup == MatchupResult.Advantage
			? VigorSelectionMode.Highest
			: VigorSelectionMode.Lowest;
		int selectedRoll = selectionMode == VigorSelectionMode.Highest
			? Mathf.Max(firstRoll, secondRoll)
			: Mathf.Min(firstRoll, secondRoll);
		return new VigorRollResult(
			dieSides,
			firstRoll,
			secondRoll,
			hasSecondRoll: true,
			selectedRoll,
			matchup,
			selectionMode);
	}

	private VigorRollResult RerollGolemAttackerVigor(VigorRollResult roll, int maximumResult)
	{
		int first = roll.FirstRoll;
		int second = roll.SecondRoll;
		int firstBeforeReroll = 0;
		int secondBeforeReroll = 0;
		if (first <= maximumResult)
		{
			firstBeforeReroll = first;
			first = random.NextInclusive(1, roll.DieSides);
		}
		if (roll.HasSecondRoll && second <= maximumResult)
		{
			secondBeforeReroll = second;
			int secondDieSides = roll.SelectionMode == VigorSelectionMode.Sum
				? AccardND.GameCore.Pvp.PvpVigorScale.Lower(roll.DieSides)
				: roll.DieSides;
			second = random.NextInclusive(1, secondDieSides);
		}
		int selected = roll.SelectionMode switch
		{
			VigorSelectionMode.Highest => Mathf.Max(first, second),
			VigorSelectionMode.Lowest => Mathf.Min(first, second),
			VigorSelectionMode.Sum => first + second,
			_ => first
		};
		return new VigorRollResult(
			roll.DieSides,
			first,
			second,
			roll.HasSecondRoll,
			selected,
			roll.Matchup,
			roll.SelectionMode,
			firstBeforeReroll,
			secondBeforeReroll);
	}

	private static VigorRollResult SingleRoll(int dieSides, int roll)
	{
		return new VigorRollResult(dieSides, roll, 0, hasSecondRoll: false, roll, MatchupResult.Neutral, VigorSelectionMode.Single);
	}

	private CombatRollBiases AdventureRollBiases(BattleCardState attacker, BattleCardState defender)
	{
		int chance = AdventurePlayerHighRollChancePercent();
		if (chance == 0 || playerCards == null)
			return CombatRollBiases.None;

		return new CombatRollBiases(
			attacker != null && playerCards.Contains(attacker) ? chance : 0,
			defender != null && playerCards.Contains(defender) ? chance : 0);
	}

	private int AdventurePlayerHighRollChancePercent()
	{
		if (string.Equals(activeAdventureChapterId, "chapter-1", StringComparison.OrdinalIgnoreCase))
			return 30;
		if (string.Equals(activeAdventureChapterId, "chapter-2", StringComparison.OrdinalIgnoreCase))
			return 15;
		return 0;
	}

	private IEnumerator MoveDuelToCenter(BattleCardState attacker, BattleCardState defender)
	{
		SetMessagePanelHiddenForDuel(hidden: true);
		bool attackerIsBackdropBoss = IsBragusBossProxy(attacker) || IsTrentorBossProxy(attacker)
			|| IsSeraphelBossProxy(attacker) || IsJurinashorBossProxy(attacker);
		bool defenderIsBackdropBoss = IsBragusBossProxy(defender) || IsTrentorBossProxy(defender)
			|| IsSeraphelBossProxy(defender) || IsJurinashorBossProxy(defender);
		bool backdropBossDuel = attackerIsBackdropBoss || defenderIsBackdropBoss;
		Vector3 worldPosition = backdropBossDuel
			? BackdropBossDuelWorldPoint(attackerIsBackdropBoss)
			: DuelWorldPoint(attacker, attacker: true);
		Vector3 worldPosition2 = backdropBossDuel
			? BackdropBossDuelWorldPoint(defenderIsBackdropBoss)
			: DuelWorldPoint(defender, attacker: false);

		if (backdropBossDuel)
		{
			((MonoBehaviour)this).StartCoroutine(attacker.View.MoveToDuelPoint(
				worldPosition, 0.34f, attackerIsBackdropBoss ? 0.58f : 1.08f));
			((MonoBehaviour)this).StartCoroutine(defender.View.MoveToDuelPoint(
				worldPosition2, 0.34f, defenderIsBackdropBoss ? 0.58f : 1.08f));
			yield return WaitForCardInspectionPause(0.37f);
			yield break;
		}

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

	private bool UsesStationaryClassAttack(BattleCardState attacker)
	{
		return attacker?.Card != null
			&& AccardND.Battlefield.BattlePresentationAnimationPlayer.HasClassAttackAnimation(attacker.Card.HeroClass);
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
			yield return battleAnimationPlayer.PlayClassAttack(
				attacker.View,
				defender.View,
				attacker.Card.HeroClass,
				hit: true,
				abilityAttack: abilityAttack,
				attackMargin: attackMargin,
				onHit: onHit);
		}
		else
		{
			yield return attacker.View.PlayAttackAnimation();
			onHit?.Invoke();
		}
	}

	private IEnumerator PlayHunterMissIfNeeded(BattleCardState attacker, BattleCardState defender = null)
	{
		if (UsesStationaryClassAttack(attacker) && attacker.View != null)
		{
			if ((Object)(object)battleAnimationPlayer != (Object)null && defender != null && defender.View != null)
			{
				// La parata del Barbaro ha un'animazione dedicata che finora
				// vedeva solo il PvP: la tabella condivisa la porta anche qui.
				yield return battleAnimationPlayer.PlayClassAttack(
					attacker.View,
					defender.View,
					attacker.Card.HeroClass,
					hit: false);
			}
			else
			{
				yield return attacker.View.PlayAttackAnimation();
			}
		}

		ShowSuccessfulDefenseCallout(defender);
	}

	private IEnumerator PlayJurinashorBlockedAttack(BattleCardState attacker, BattleCardState boss)
	{
		if (attacker == null || boss == null)
			yield break;

		if (UsesStationaryClassAttack(attacker)
			&& (Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)attacker.View != (Object)null
			&& (Object)(object)boss.View != (Object)null)
		{
			bool incomingHitPending = true;
			bool deflectionSfxPlayed = false;
			void PlayDeflectionImpactSfx()
			{
				if (deflectionSfxPlayed)
					return;
				deflectionSfxPlayed = true;
				battleSfx?.PlayAttackResult(HeroClass.Necromancer, hit: false);
			}
			Coroutine incomingAttack = StartCoroutine(battleAnimationPlayer.PlayClassAttack(
				attacker.View,
				boss.View,
				attacker.Card.HeroClass,
				hit: false,
				onBlocked: () =>
				{
					PlayDeflectionImpactSfx();
					incomingHitPending = false;
				}));
			// Il viaggio iniziale del colpo resta visibile; la lama di Jurinashor entra
			// in rotazione quando il colpo raggiunge la sua sagoma, non al lancio.
			yield return new WaitForSecondsRealtime(0.28f);
			yield return battleAnimationPlayer.PlayJurinashorPhaseOneDeflection(
				attacker.View,
				boss.View,
				() => incomingHitPending);
			yield return incomingAttack;
		}
		else if ((Object)(object)battleAnimationPlayer != (Object)null)
		{
			// Le pedine melee sono già arrivate al punto di duello: questo è il loro
			// frame d'impatto equivalente.
			battleSfx?.PlayAttackResult(HeroClass.Necromancer, hit: false);
			yield return battleAnimationPlayer.PlayJurinashorPhaseOneDeflection(attacker.View, boss.View);
		}

		ShowSuccessfulDefenseCallout(boss);
	}

	private static void ShowSuccessfulDefenseCallout(BattleCardState defender)
	{
		if (defender?.Card == null || defender.View == null)
			return;

		string key = defender.Card.HeroClass switch
		{
			HeroClass.Warrior => GameTextKeys.Combat.DefenseWarrior,
			HeroClass.Paladin => GameTextKeys.Combat.DefensePaladin,
			HeroClass.Barbarian => GameTextKeys.Combat.DefenseBarbarian,
			HeroClass.Hunter => GameTextKeys.Combat.DefenseHunter,
			HeroClass.Assassin => GameTextKeys.Combat.DefenseAssassin,
			HeroClass.Rogue => GameTextKeys.Combat.DefenseRogue,
			HeroClass.Mage => GameTextKeys.Combat.DefenseMage,
			HeroClass.Priest => GameTextKeys.Combat.DefensePriest,
			HeroClass.Necromancer => GameTextKeys.Combat.DefenseNecromancer,
			_ => null
		};

		if (!string.IsNullOrEmpty(key))
			defender.View.PlayActionCallout(LocalizedDefenseCallout(key), Color.white);
	}

	/// <summary>
	/// I callout possono partire nello stesso frame del cambio lingua, prima che
	/// Unity abbia finito di caricare la String Table. Non devono mai mostrare
	/// la chiave tecnica: per ciascuna difesa manteniamo un fallback completo.
	/// </summary>
	private static string LocalizedDefenseCallout(string key)
	{
		return key switch
		{
			GameTextKeys.Combat.DefenseWarrior => GameText.GetLocalizedFallback(key, "PARATO", "PARRIED", "PARIERT", "BLOQUEADO", "PARÉ"),
			GameTextKeys.Combat.DefensePaladin => GameText.GetLocalizedFallback(key, "BLOCCATO", "BLOCKED", "GEBLOCKT", "BLOQUEADO", "BLOQUÉ"),
			GameTextKeys.Combat.DefenseBarbarian => GameText.GetLocalizedFallback(key, "RESISTITO", "RESISTED", "WIDERSTANDEN", "RESISTIDO", "RÉSISTÉ"),
			GameTextKeys.Combat.DefenseHunter => GameText.GetLocalizedFallback(key, "SCHIVATO", "DODGED", "AUSGEWICHEN", "ESQUIVADO", "ESQUIVÉ"),
			GameTextKeys.Combat.DefenseAssassin => GameText.GetLocalizedFallback(key, "ELUSO", "EVADED", "ENTKOMMEN", "EVADIDO", "ÉVITÉ"),
			GameTextKeys.Combat.DefenseRogue => GameText.GetLocalizedFallback(key, "SVANITO", "VANISHED", "VERSCHWUNDEN", "DESVANECIDO", "ÉVANOUÏ"),
			GameTextKeys.Combat.DefenseMage => GameText.GetLocalizedFallback(key, "PROTETTO", "SHIELDED", "GESCHÜTZT", "PROTEGIDO", "PROTÉGÉ"),
			GameTextKeys.Combat.DefensePriest => GameText.GetLocalizedFallback(key, "ASSORBITO", "ABSORBED", "ABSORBIERT", "ABSORBIDO", "ABSORBÉ"),
			GameTextKeys.Combat.DefenseNecromancer => GameText.GetLocalizedFallback(key, "DEFLESSO", "DEFLECTED", "ABGELENKT", "DESVIADO", "DÉVIÉ"),
			_ => GameText.GetOrFallbackSilent(key, string.Empty)
		};
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
		attacker?.View?.ReapplyCombatStrengthScale();
		defender?.View?.ReapplyCombatStrengthScale();
		yield return RestoreCombatStrengthPresentation(attacker, defender);
		SetMessagePanelHiddenForDuel(hidden: false);
	}

	private IEnumerator BeginCurrentTurnAfterAuraCallout()
	{
		while (auraActivationCalloutVisible)
			yield return null;

		beginTurnAfterAuraCalloutCoroutine = null;
		BeginCurrentTurn();
	}

	private void SetMessagePanelHiddenForDuel(bool hidden)
	{
		messagePanelHiddenForDuel = hidden;
		if ((Object)(object)messagePanelRect != (Object)null)
		{
			if (adventureScriptedTutorialActive)
			{
				((Component)messagePanelRect).gameObject.SetActive(false);
			}
			else
			{
				RefreshMessagePanelVisibility();
			}
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
		// L'Invisibilita' resta memorizzata anche quando l'Assassino non puo' piu'
		// nascondersi dietro un alleato. In quel momento torna bersagliabile, ma la
		// Suprema gli concede vantaggio su ogni difesa (anche nelle anteprime CPU).
		if (defender != null && defender.IsUntargetable && !IsShieldedByInvisibility(defender))
		{
			defenderAdvantage = true;
		}

		ClassBalanceConfiguration classBalance = configuration.ClassBalance;
		int num = attacker.PendingAttackBonus + TotalPermanentCombatBonus(attacker);
		num += JurinashorSwordPowerBonus(attacker);

		// "Sfidante": solo quando i modificatori sono quelli veri dell'attacco. Con
		// updateVisuals a false questa funzione gira anche per le anteprime del pannello, e
		// li' il bonus si brucerebbe senza che nessuno abbia attaccato.
		if (updateVisuals)
		{
			int challengerBonus = ConsumeChallengerBonus(attacker, defender);
			if (challengerBonus > 0)
			{
				num += challengerBonus;
				AppendLog($"SFIDANTE - {attacker.Card.Name} attacca il boss con +{challengerBonus} Potenza.");
			}
		}

		int defenderFlatBonus = TotalPermanentCombatBonus(defender) + PendingDefenseBonus(defender)
			+ JurinashorSwordPowerBonus(defender);
		bool flag = ClassAbilitiesEnabled(attacker);
		int num2 = HunterMarkAttackBonus(attacker, defender);
		if (num2 > 0)
		{
			num += num2;
		}
		else if (attacker.Card.HeroClass == HeroClass.Rogue && classBalance.RogueRerollsOnes && flag && updateVisuals)
		{
			int rogueRerollMaximum = RogueConditionalRerollMaximum(
				attacker.BelongsToPlayer ? runProgress.PlayerVigorDieSides : runProgress.MasterVigorDieSides);
			attacker.View.SetStatus(
				GameText.GetOrFallbackSilent(
					GameTextKeys.Combat.RogueRerollStatus,
					"REROLL 1-{0} SE SERVE",
					rogueRerollMaximum),
				new Color(0.75f, 0.9f, 1f));
		}
		// Confronta la Potenza effettiva prima dell'aura: benedizioni applicabili,
		// equipaggiamenti, malus e gli altri bonus attivi fanno parte dello scontro.
		int attackerEffectiveStrength = CombatBaseStrength(attacker) + num;
		int defenderEffectiveStrength = CombatBaseStrength(defender) + defenderFlatBonus;
		if (AuraFor(defender) == BattleAuraType.Warrior
			&& defender.Card.HeroClass == HeroClass.Warrior
			&& defenderEffectiveStrength < attackerEffectiveStrength)
		{
			defenderFlatBonus += 2;
		}
		if (AuraFor(attacker) == BattleAuraType.Warrior
			&& attacker.Card.HeroClass == HeroClass.Warrior
			&& attackerEffectiveStrength < defenderEffectiveStrength)
		{
			num += 2;
		}
		bool forceAttackerAdvantage = false;
		if (AuraFor(attacker) == BattleAuraType.Cunning && HeroClassFamily.Of(attacker.Card.HeroClass) == ClassFamily.Cunning && HasBonusOrMalusForCunning(defender))
		{
			forceAttackerAdvantage = true;
			if (updateVisuals)
			{
				attacker.View.SetStatus("AURA ASTUZIA", new Color(0.75f, 0.65f, 1f));
			}
		}
		if (attacker.BelongsToPlayer && playerAura == BattleAuraType.Formation && ClassMatchup.Compare(attacker.Card.HeroClass, defender.Card.HeroClass) == MatchupResult.Disadvantage)
		{
			neutralizeAttackerMatchup = true;
			if (updateVisuals)
			{
				attacker.View.SetStatus("AURA FORMAZIONE", new Color(0.55f, 1f, 0.85f));
			}
		}
		return new CombatModifiers(
			flag && attacker.AbilityArmed && attacker.Card.HeroClass == HeroClass.Warrior,
			defenderAdvantage,
			false,
			false,
			num,
			defenderFlatBonus,
			neutralizeAttackerMatchup,
			forceAttackerAdvantage,
			false,
			false,
			(flag && classBalance.RogueRerollsOnes || AuraFor(attacker) == BattleAuraType.Rogue)
				&& attacker.Card.HeroClass == HeroClass.Rogue
				? RogueConditionalRerollMaximum(
					attacker.BelongsToPlayer ? runProgress.PlayerVigorDieSides : runProgress.MasterVigorDieSides)
				: 0,
			AuraFor(defender) == BattleAuraType.Rogue && defender.Card.HeroClass == HeroClass.Rogue
				? RogueConditionalRerollMaximum(
					defender.BelongsToPlayer ? runProgress.PlayerVigorDieSides : runProgress.MasterVigorDieSides)
				: 0);
	}

	private static int RogueConditionalRerollMaximum(int dieSides)
	{
		return dieSides switch
		{
			4 => 1,
			6 => 2,
			8 => 3,
			10 => 4,
			12 => 5,
			20 => 6,
			_ => 0
		};
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
		MarkAbilityUsed(attacker);
		TriggerMagicAuraAfterAbility();
	}

	private bool ClassAbilitiesEnabled(BattleCardState card)
	{
		if (card != null && !card.BelongsToPlayer && currentRoomType == RoomType.Monster)
		{
			return RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesAbilities;
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
		if (dieSides > 2)
		{
			return dieSides switch
			{
				4 => 2, 
				6 => 4, 
				8 => 6, 
				10 => 8, 
				12 => 10, 
				20 => 12, 
				_ => (dieSides <= 6) ?4 : ((dieSides <= 8) ?6 : ((dieSides <= 10) ?8 : ((dieSides <= 12) ?10 : 12))), 
			};
		}
		return 2;
	}

	private static int StackMageVigorPenalty(int currentSteps, int baseDieSides)
	{
		int maximumSteps = 0;
		int dieSides = baseDieSides;
		while (dieSides > 2)
		{
			dieSides = LowerVigorDie(dieSides);
			maximumSteps++;
		}

		return Math.Min(Math.Max(0, currentSteps) + 1, maximumSteps);
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
		if (dieSides > 2)
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
		ResetCampaignPrimaryManaPayment(defeated);
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
		// Chi difende dal contrattacco porta i suoi bonus come in ogni altro
		// confronto: senza, la Furia del Barbaro non contava nulla e ciononostante
		// si sarebbe scaricata sull'esito.
		CombatModifiers modifiers = new CombatModifiers(
			sumAttackerVigor: false,
			defenderAdvantage: false,
			rerollAttackerOnes: false,
			rerollAttackerTwos: false,
			attackerFlatBonus: 1,
			defenderFlatBonus: TotalPermanentCombatBonus(target) + PendingDefenseBonus(target));
		CombatResult result = combatResolver.ResolveAttack(paladin.Card, target.Card, num, num2, modifiers,
			AdventureRollBiases(paladin, target));
		SetMessage("AURA PALADINO: " + paladin.Card.Name + " contrattacca " + target.Card.Name + " con +1.");
		bool messagePanelWasHidden = HideMessagePanelForDiceRoll();
		PlayRollingDiceSfx();
		paladin.View.PlayVigorRoll(diceCatalog, num, TrackDiceRoll(result.AttackerRoll), "CONTRATTACCO", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		target.View.PlayVigorRoll(diceCatalog, num2, TrackDiceRoll(result.DefenderRoll), "DIFESA CPU", configuration.Animation.DiceRollDuration, configuration.Animation.DiceResultHold);
		yield return WaitForCardInspectionPause(CombatRollPresentationDuration(result.AttackerRoll, result.DefenderRoll));
		RestoreMessagePanelAfterDiceRoll(messagePanelWasHidden);
		yield return ShowCombatResult(result, paladin, target);
		PlayAttackResultSfx(paladin, result.DefenderIsDefeated);
		if (result.DefenderIsDefeated)
		{
			// Il contrattacco dell'aura e' un vero attacco del Paladino: deve usare
			// lo stesso VFX dello scudo, inclusa la scia ancestrale.
			yield return PlayHunterRangedAttackIfNeeded(
				paladin,
				target,
				result.AttackerTotal - result.DefenderTotal);
			target.Eliminated = true;
			RegisterCampaignEliminationMana(paladin, target);
			ApplyMageAuraDeathPenalty(target, paladin);
			ApplyMightAuraDeathBonuses(target);
			PlayDeathCardSfx();
			yield return PlayTimelineAwareDefeatAnimation(target, paladin.Card.HeroClass);
		}
		else
		{
			// Anche un contrattacco parato mantiene il feedback visivo standard.
			yield return PlayHunterMissIfNeeded(paladin, target);
		}
		yield return RestoreCombatStrengthPresentation(paladin, target);
		ConsumeVigorPenalties(paladin, target);
		// Solo il difensore: il contrattacco non consuma i bonus pendenti del
		// Paladino, che infatti non entrano nel suo tiro (vale il +1 secco).
		UpdateDefenderClassStateAfterExchange(target, result.DefenderIsDefeated);
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
		bool bossTarget = target.Definition != null && target.Definition.Category == CardCategory.Boss;

		bool consumed = false;
		foreach (BattleCardState hunter in playerCards.Concat(cpuCards))
		{
			if (hunter == null || hunter.Card.HeroClass != HeroClass.Hunter || hunter.MarkedTarget != target)
			{
				continue;
			}
			BattleAuraType hunterAura = hunter.BelongsToPlayer ? playerAura : cpuAura;
			if (bossTarget && hunterAura == BattleAuraType.Hunter)
			{
				// Con Aura Cacciatore il Marchio diventa persistente sui boss.
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

	/// <summary>
	/// Aggiorna gli effetti che dipendono dall'esito di un confronto. Il parametro
	/// indica chi ha vinto il confronto, non se una pedina o un boss e' stato eliminato.
	/// </summary>
	private void UpdateClassStateAfterExchange(
		BattleCardState attacker,
		BattleCardState defender,
		bool attackSucceeded)
	{
		UpdateAttackerClassStateAfterExchange(attacker, attackSucceeded);
		UpdateDefenderClassStateAfterExchange(defender, attackSucceeded);
	}

	private void UpdateAttackerClassStateAfterExchange(BattleCardState attacker, bool attackSucceeded)
	{
		bool hasFury = attacker.PendingAttackBonusKind == PendingAttackBonusKind.Fury;
		bool isBarbarian = ClassAbilitiesEnabled(attacker) && attacker.Card.HeroClass == HeroClass.Barbarian;
		if (attackSucceeded)
		{
			if (hasFury)
			{
				ClearBarbarianFury(attacker);
			}
			else
			{
				attacker.PendingAttackBonus = 0;
				attacker.PendingAttackBonusKind = PendingAttackBonusKind.None;
				attacker.View.SetStrengthValue(DisplayStrength(attacker));
				RefreshPersistentStatus(attacker);
			}
		}
		else
		{
			if (!hasFury)
			{
				attacker.PendingAttackBonus = 0;
				attacker.PendingAttackBonusKind = PendingAttackBonusKind.None;
				attacker.View.SetStrengthValue(DisplayStrength(attacker));
				RefreshPersistentStatus(attacker);
			}
			if (isBarbarian)
			{
				ApplyBarbarianFury(attacker);
			}
		}
	}

	private void UpdateDefenderClassStateAfterExchange(BattleCardState defender, bool attackSucceeded)
	{
		bool hasFury = defender.PendingAttackBonusKind == PendingAttackBonusKind.Fury;
		bool isBarbarian = ClassAbilitiesEnabled(defender) && defender.Card.HeroClass == HeroClass.Barbarian;
		if (attackSucceeded)
		{
			if (isBarbarian && !defender.Eliminated)
			{
				ApplyBarbarianFury(defender);
			}
		}
		else if (hasFury)
		{
			ClearBarbarianFury(defender);
		}
	}

	private void ClearBarbarianFury(BattleCardState card)
	{
		if (card.PendingAttackBonusKind != PendingAttackBonusKind.Fury)
		{
			return;
		}
		card.PendingAttackBonus = 0;
		card.PendingAttackBonusKind = PendingAttackBonusKind.None;
		card.View.SetStrengthValue(DisplayStrength(card));
		RefreshPersistentStatus(card);
	}

	private void ApplyBarbarianFury(BattleCardState card)
	{
		BattleAuraType aura = card.BelongsToPlayer ?playerAura : cpuAura;
		int pendingAttackBonus = aura == BattleAuraType.Barbarian
			? configuration.ClassBalance.BarbarianRageBonus + 1
			: configuration.ClassBalance.BarbarianRageBonus;
		card.PendingAttackBonus = card.PendingAttackBonusKind == PendingAttackBonusKind.Fury
			? card.PendingAttackBonus + pendingAttackBonus
			: pendingAttackBonus;
		card.PendingAttackBonusKind = PendingAttackBonusKind.Fury;
		card.View.SetStrengthValue(DisplayStrength(card));
		RefreshPersistentStatus(card);
		card.View.PlayAbilityActionCallout();
		PlayBarbarianFurySfx();
		if ((Object)(object)card.View != (Object)null
			&& (Object)(object)battleAnimationPlayer != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayBarbarianFury(card.View));
		}
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

	private void ApplyMageAuraDeathPenalty(BattleCardState defeated, BattleCardState attacker)
	{
		if (defeated == null
			|| attacker == null
			|| !defeated.Eliminated
			|| defeated.Card.HeroClass != HeroClass.Mage
			|| AuraFor(defeated) != BattleAuraType.Mage)
		{
			return;
		}

		if ((IsSeraphelBossProxy(attacker)
				&& activeSeraphelBoss != null
				&& activeSeraphelBoss.IsImmuneToDebuffs)
			|| IsJurinashorImmuneToDebuffs(attacker))
		{
			attacker.View?.PlayActionCallout("IMMUNITÀ", Color.white);
			AppendLog($"IMMUNITÀ - {attacker.Card.Name} ignora l'esplosione dell'Aura Magica di {defeated.Card.Name}.");
			return;
		}

		ReducePower(attacker, 2);
		RefreshPersistentStatus(attacker);
		AppendLog($"AURA MAGO - {attacker.Card.Name} subisce -2 permanente per aver eliminato {defeated.Card.Name}.");
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

	/// <summary>
	/// Il bonus permanente che finisce sotto l'etichetta EQUIP: equipaggiamento,
	/// tempra del fabbro e upgrade del mercante stanno insieme, l'unico scorporato
	/// e' il Sigillo Oscuro, che ha token, icona e descrizione tutti suoi.
	/// </summary>
	private static int EquipmentBonusOf(BattleCardState card)
	{
		if (card == null || !card.HasEquipment)
			return 0;
		int bonus = card.PermanentCombatBonus;
		if (card.CampaignCard?.HasRubySeal == true)
			bonus -= RubySealPowerBonus;
		return Mathf.Max(0, bonus);
	}

	private int CombatBaseStrength(BattleCardState card)
	{
		if (IsComposableGolemProxy(card) && activeComposableGolem != null)
			return activeComposableGolem.ActiveForm.Power;
		return card.Card.Strength;
	}

	private int DisplayStrength(BattleCardState card)
	{
		if (IsComposableGolemProxy(card) && activeComposableGolem != null)
		{
			return activeComposableGolem.ActiveForm.Power + TotalPermanentCombatBonus(card);
		}
		if (IsMedusaBossProxy(card) && activeMedusaBoss != null)
		{
			return MedusaBoss.CardStrength;
		}
		if (IsTrentorBossProxy(card) && activeTrentorBoss != null)
		{
			return TrentorBoss.CardStrength;
		}
		if (IsJurinashorBossProxy(card) && activeJurinashorBoss != null)
		{
			return JurinashorBoss.CardStrength + JurinashorSwordPowerBonus(card);
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
		card.View.SetAssassinSilverFilm(card.IsUntargetable && !card.Eliminated);
		if (card.Eliminated)
		{
			card.View.SetStatus("MORTE", new Color(0.95f, 0.12f, 0.12f));
			return;
		}
		List<PrototypeCardView.StatusToken> list = new List<PrototypeCardView.StatusToken>();
		int supremeUses = SupremeUsesForInspection(card.Card.HeroClass, card);
		if (supremeUses > 0)
		{
			int supremeCost = SupremeCostForInspection(card.Card.HeroClass, card);
			list.Add(new PrototypeCardView.StatusToken(
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.SupremeCostMalusStatus, "MALUS SUPREMA {0} MANA", "SUPREME PENALTY {0} MANA", "HÖCHSTE-FÄHIGKEIT-MALUS {0} MANA", "PENALIZACIÓN SUPREMA {0} MANÁ", "MALUS SUPRÊME {0} MANA", supremeCost) + $" ({supremeUses})",
				new Color(1f, 0.38f, 0.32f),
				GetSupremeButtonSprite()));
		}
		if (IsComposableGolemProxy(card) && activeComposableGolem != null)
		{
			ComposableGolemForm activeGolemForm = activeComposableGolem.ActiveForm.Form;
			card.View.SetComposableGolemForm(
				activeGolemForm,
				actionColor: GolemFormColor(activeGolemForm));
		}
		if (IsTrentorBossProxy(card) && activeTrentorBoss != null)
		{
			UpdateTrentorBossHealthBar(card);
		}
		if (IsJurinashorBossProxy(card) && activeJurinashorBoss != null)
		{
			UpdateJurinashorBossHealthBar(card);
		}
		if (IsSeraphelBossProxy(card) && activeSeraphelBoss != null)
		{
			RefreshSeraphelBossPawn(card);
		}
		if (card.SeraphelSeals > 0)
		{
			list.Add(new PrototypeCardView.StatusToken(
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.SeraphelSealsStatus, "SIGILLI {0} - MALUS", "SEALS {0} - PENALTY", "SIEGEL {0} - MALUS", "SELLOS {0} - PENALIZACIÓN", "SCEAUX {0} - MALUS", card.SeraphelSeals) + $" +{card.SeraphelSeals * SeraphelBoss.DamagePerSeal}",
				new Color(1f, 0.22f, 0.18f)));
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
		if (card.IsUntargetable)
		{
			list.Add(new PrototypeCardView.StatusToken("INVISIBILE", new Color(0.82f, 0.88f, 0.96f)));
		}
		if (card.PendingVigorStepPenalty > 0)
		{
			list.Add(new PrototypeCardView.StatusToken($"DADO -{card.PendingVigorStepPenalty}", new Color(0.55f, 0.8f, 1f)));
		}
		if (card.CampaignCard?.HasRubySeal == true)
		{
			list.Add(new PrototypeCardView.StatusToken("SIGILLO OSCURO +2", new Color(0.72f, 0.35f, 0.9f)));
		}
		int equipmentBonus = EquipmentBonusOf(card);
		if (equipmentBonus > 0)
		{
			int merchantUpgradeLevel = card.CampaignCard?.MerchantUpgradeCount ?? 0;
			Sprite forgeUpgradeIcon = merchantUpgradeLevel > 0
				? LoadSpriteResource($"UI/merchant_upgrade_relic_{Math.Min(merchantUpgradeLevel, 2)}")
				: null;
			list.Add(new PrototypeCardView.StatusToken(
				$"EQUIP +{equipmentBonus}",
				new Color(0.7f, 1f, 0.45f),
				forgeUpgradeIcon));
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
			list.Add(new PrototypeCardView.StatusToken(
				GameText.GetLocalizedFallback(GameTextKeys.Inspection.HunterMarkStatus, "BERSAGLIO MARCATO +{0}", "MARKED TARGET +{0}", "MARKIERTES ZIEL +{0}", "OBJETIVO MARCADO +{0}", "CIBLE MARQUÉE +{0}", num),
				new Color(1f, 0.65f, 0.2f),
				LoadSpriteResource("StatusIcons/hunter_debuff")));
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
		if (!TutorialWarriorDuelAllowsAttack())
			return;
		// Il bottone vive sopra la pedina e il suo pointer-up puo essere osservato anche
		// dal Button della carta nello stesso frame. Non permettere che quel rilascio
		// apra l'inspection mentre stiamo armando (o rifiutando) l'attacco.
		suppressCardInspectionUntilFrame = Mathf.Max(
			suppressCardInspectionUntilFrame,
			Time.frameCount + 1);
		if (!inputLocked && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState = playerCards[selectedPlayerIndex];
			if (battleCardState != null && !battleCardState.Eliminated)
			{
				// Il bottone resta cliccabile anche a secco: la pedina dice perche'
				// non parte l'attacco invece di lasciare un tasto che non fa nulla.
				if (!IsCampaignAttackAffordable(battleCardState))
				{
					ShowNoManaCallout(battleCardState);
					return;
				}
				attackTargetingActive = true;
				PlayActionTargetingSfx();
				abilityTargetMode = AbilityTargetMode.None;
				activeAbilityUser = null;
				activeAttachmentSource = null;
				pendingAbilityUser = null;
				((Component)abilityButton).gameObject.SetActive(false);
				((Component)attachmentButton).gameObject.SetActive(false);
				ShowTargetHints(battleCardState);
				SetMessage(GameText.Format(GameTextKeys.Combat.AttackTargetPrompt, battleCardState.Card.Name));
				UpdateInteractions();
				NotifyAdventureTutorial(AdventureTutorialAction.AttackPressed);
			}
		}
	}

	private void ActivateCurrentAbility()
	{
		if (!TutorialWarriorDuelAllowsAbility())
			return;
		if (!inputLocked && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState = playerCards[selectedPlayerIndex];
			if (IsClassAbilityActionAvailable(battleCardState) && !IsCampaignPrimaryAffordable(battleCardState))
			{
				ShowNoManaCallout(battleCardState);
				return;
			}
			if (IsClassAbilityActionAvailable(battleCardState))
			{
				attackTargetingActive = false;
				pendingAbilityUser = battleCardState;
				ConfirmPendingAbility();
				NotifyAdventureTutorial(AdventureTutorialAction.AbilityPressed);
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
		// Guard nell'imbuto: qui ci si arriva sia dal bottone abilita' sia dai bottoni
		// conferma/annulla. Senza mana non si entra in selezione bersaglio, altrimenti
		// la pedina resta in mira con un'azione che non potra' mai pagare e il turno
		// finisce per saltare.
		if (!IsCampaignPrimaryAffordable(battleCardState))
		{
			ShowNoManaCallout(battleCardState);
			// Ripristina bottoni e AbilityUsedThisTurn: la pedina resta giocabile.
			CancelPendingAction();
			SetMessage(GameText.GetOrFallbackSilent(
				GameTextKeys.Combat.ManaInsufficientAbility,
				"Mana insufficiente per l'abilita di {0}.",
				battleCardState.Card.Name));
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
			SetMessage(GameText.Get(GameTextKeys.Combat.AssassinTargetPrompt));
			UpdateInteractions();
			break;
		case HeroClass.Warrior:
			// Si arma senza addebito: il mana viene scalato solo quando un bersaglio
			// valido fa partire davvero l'attacco. Annullare la mira non costa nulla.
			battleCardState.AbilityArmed = true;
			attackTargetingActive = true;
			ShowTargetHints(battleCardState);
			SetMessage(GameText.Format(GameTextKeys.Combat.WarriorAbilityReady, battleCardState.Card.Name));
			break;
		case HeroClass.Mage:
			battleCardState.AbilityArmed = true;
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.MageEnemy;
			SetMessage(GameText.Get(GameTextKeys.Combat.MageTargetPrompt));
			UpdateInteractions();
			break;
		case HeroClass.Paladin:
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.PaladinAlly;
			// Il bottone abilita' vive sulla carta attiva: evita che lo stesso click
			// venga interpretato anche come selezione automatica del Paladino.
			suppressPaladinTargetSelectionUntilFrame = Time.frameCount + 1;
			SetMessage(GameText.Format(GameTextKeys.Combat.PaladinTargetPrompt, battleCardState.Card.Name));
			break;
		case HeroClass.Hunter:
			battleCardState.AbilityArmed = true;
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.HunterEnemy;
			ShowTargetHints(battleCardState);
			SetMessage(GameText.Get(GameTextKeys.Combat.HunterTargetPrompt));
			UpdateInteractions();
			break;
		case HeroClass.Necromancer:
			if (!playerCards.Any(CanReviveWithNecromancer))
			{
				return;
			}
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.NecromancerAlly;
			SetMessage(GameText.Get(GameTextKeys.Combat.NecromancerTargetPrompt));
			UpdateInteractions();
			break;
		case HeroClass.Priest:
			activeAbilityUser = battleCardState;
			abilityTargetMode = AbilityTargetMode.PriestAlly;
			SetMessage(GameText.Get(GameTextKeys.Combat.PriestTargetPrompt));
			UpdateInteractions();
			break;
		case HeroClass.Barbarian:
			return;
		}
		battleCardState.AbilityUsedThisTurn = true;
		PlayActionTargetingSfx();
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
		if (IsTutorialWarriorDuelActive)
			return;
		if (!inputLocked && selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			BattleCardState battleCardState = playerCards[selectedPlayerIndex];
			if (CanUseAttachment(battleCardState))
			{
				attackTargetingActive = false;
				activeAttachmentSource = battleCardState;
				abilityTargetMode = AbilityTargetMode.AttachmentAlly;
				PlayActionTargetingSfx();
				attachmentButton.interactable = false;
				((Component)abilityButton).gameObject.SetActive(false);
				ClearTargetHints();
				SetActiveTurnAura(null);
				SetMessage(GameText.Format(GameTextKeys.Combat.AttachmentTargetPrompt, AttachmentBonus(battleCardState)));
				UpdateInteractions();
			}
		}
	}

	private void ActivateCurrentSkip()
	{
		if (IsTutorialWarriorDuelActive)
			return;
		if (inputLocked || gameFinished || turnOrder.Count == 0 || currentTurnIndex < 0 || currentTurnIndex >= turnOrder.Count)
		{
			return;
		}

		BattleCardState activeCard = turnOrder[currentTurnIndex];
		// Niente controllo su AbilityUsedThisTurn: saltare dopo un'abilita' e' permesso
		// apposta, altrimenti a 0 mana la pedina resterebbe senza nessuna azione legale.
		if (activeCard == null || activeCard.Eliminated || !activeCard.BelongsToPlayer
			|| !IsSkipAvailableAgainstBragus(activeCard))
		{
			return;
		}

		inputLocked = true;
		attackTargetingActive = false;
		abilityTargetMode = AbilityTargetMode.None;
		activeAbilityUser = null;
		activeAttachmentSource = null;
		pendingAbilityUser = null;
		activeCard.View.SetSelected(selected: false);
		SetActiveTurnAura(null);
		ClearTargetHints();
		SetMessage(GameText.GetLocalizedFallback(
			GameTextKeys.Combat.SkipTurnMessage,
			"{0} salta il turno.",
			"{0} skips the turn.",
			activeCard.Card.Name));
		AppendLog(GameText.GetLocalizedFallback(
			GameTextKeys.Combat.SkipTurnLog,
			"SALTA - {0} passa senza agire.",
			"SKIP - {0} passes without acting.",
			activeCard.Card.Name));
		UpdateInteractions();
		FinishTurn(skipped: true);
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
		return blockedSideBelongsToPlayer && IsBragusPlayerActionLockActive();
	}

	private static void ReducePower(BattleCardState card, int amount)
	{
		if (card == null || card.Card == null || amount <= 0)
			return;

		card.PermanentCombatBonus = Math.Max(
			1 - card.Card.Strength - card.MightAuraCombatBonus,
			card.PermanentCombatBonus - amount);
	}

	private bool IsBragusPlayerActionLockActive()
	{
		return activeBragusBoss != null
			&& !activeBragusBoss.IsDefeated
			&& cpuCards.Any((BattleCardState card) => IsBragusBossProxy(card) && !card.Eliminated);
	}

	/// <summary>
	/// Bragus impedisce di rinunciare volontariamente all'attacco, ma non deve
	/// bloccare la partita quando il costo dell'attacco non e' pagabile.
	/// </summary>
	private bool IsSkipAvailableAgainstBragus(BattleCardState card)
	{
		return !IsBragusPlayerActionLockActive() || !IsCampaignAttackAffordable(card);
	}

	private bool CanCpuUseAdvancedActions(BattleCardState card)
	{
		if (card != null && !card.Eliminated && !card.BelongsToPlayer && currentRoomType == RoomType.Monster)
		{
			return RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesAbilities;
		}
		return false;
	}

	/// <summary>
	/// Equipaggiare costa la pedina: sotto questo bonus lo scambio e' sempre in perdita
	/// (una pedina da 4 Potenza regalerebbe +1) e la CPU preferisce combattere.
	/// </summary>
	private const int MinimumCpuAttachmentBonus = 2;

	private bool TryChooseCpuAttachment(BattleCardState source, out BattleCardState target)
	{
		target = null;
		if (!RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesAttachments || !CanCpuUseAdvancedActions(source) || source.Card.Strength < 2 || AttachmentBonus(source) < MinimumCpuAttachmentBonus || cpuCards.Count((BattleCardState card) => card != null && !card.Eliminated) <= 1)
		{
			return false;
		}
		target = cpuCards.Where((BattleCardState card) => CanTargetAttachment(source, card)).OrderByDescending(DisplayStrength).FirstOrDefault();
		if (target == null)
		{
			return false;
		}
		if (pendingRoomDifficulty != RoomDifficulty.Hard)
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

		AppendLog(GameText.Get(GameTextKeys.Combat.AutoVictoryLog));
		SetBattlefieldMessage(GameText.Get(GameTextKeys.Combat.AutoVictoryMessage));
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

	/// <summary>
	/// Chi incassera' davvero l'attacco della CPU su <paramref name="intendedTarget"/>.
	/// Un Paladino con l'abilita' armata devia il colpo su di se' (o si difende con
	/// vantaggio se il bersaglio e' lui): e' l'unico punto in cui si decide la deviazione,
	/// cosi' la scelta del bersaglio e la risoluzione dell'attacco non possono divergere.
	/// </summary>
	private BattleCardState ResolveCpuAttackDefender(BattleCardState intendedTarget, out BattleCardState paladinProtectionUser)
	{
		paladinProtectionUser = null;
		if (intendedTarget == null || intendedTarget.Eliminated)
		{
			return intendedTarget;
		}

		BattleCardState protectingPaladin = playerCards.FirstOrDefault((BattleCardState card) =>
			!card.Eliminated
			&& card.Card.HeroClass == HeroClass.Paladin
			&& card.AbilityArmed
			&& (card.ProtectedAlly == null || card.ProtectedAlly == intendedTarget)
			&& card != intendedTarget);
		if (protectingPaladin != null)
		{
			paladinProtectionUser = protectingPaladin;
			return protectingPaladin;
		}

		if (intendedTarget.Card.HeroClass == HeroClass.Paladin
			&& intendedTarget.AbilityArmed
			&& (intendedTarget.ProtectedAlly == null || intendedTarget.ProtectedAlly == intendedTarget))
		{
			paladinProtectionUser = intendedTarget;
		}
		return intendedTarget;
	}

	/// <summary>
	/// La migliore probabilita' di eliminazione che l'attaccante ha in questo istante,
	/// deviazioni del Paladino comprese. Non consuma numeri casuali e non tocca la scena:
	/// serve alle decisioni che vengono prima della scelta del bersaglio.
	/// </summary>
	private double BestCpuKillProbability(BattleCardState attacker)
	{
		if (attacker == null || attacker.Eliminated || runProgress == null)
		{
			return 0.0;
		}

		int attackerDieSides = EffectiveVigorDieSides(attacker, runProgress.MasterVigorDieSides);
		double best = 0.0;
		foreach (BattleCardState playerCard in playerCards)
		{
			if (playerCard == null || playerCard.Eliminated || IsShieldedByInvisibility(playerCard))
			{
				continue;
			}

			BattleCardState defender = ResolveCpuAttackDefender(playerCard, out BattleCardState paladinProtectionUser);
			CombatModifiers modifiers = BuildAttackModifiers(attacker, defender, paladinProtectionUser != null, paladinProtectionUser != null, updateVisuals: false);
			double probability = cpuDecisionService.EstimateDefeatProbability(
				attacker.Card,
				defender.Card,
				attackerDieSides,
				EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides),
				modifiers);
			if (probability > best)
			{
				best = probability;
			}
		}
		return best;
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

			BattleCardState defender = ResolveCpuAttackDefender(playerCard, out BattleCardState paladinProtectionUser);
			int defenderDieSides = EffectiveDefenseVigorDieSides(defender, runProgress.PlayerVigorDieSides);
			CombatModifiers modifiers = BuildAttackModifiers(cpuCard, defender, paladinProtectionUser != null, paladinProtectionUser != null, updateVisuals: false);
			if (CombatCertaintyCalculator.Evaluate(cpuCard.Card, defender.Card, attackerDieSides, defenderDieSides, modifiers) != CombatCertainty.Impossible)
			{
				return true;
			}
		}
		return false;
	}

	// Una regola sola per "posso equipaggiare?" e "chi equipaggio?": tenerle separate
	// significava riscrivere due volte la stessa condizione e vederle divergere.
	private bool CpuCanUseAttachment(BattleCardState source)
	{
		return TryChooseCpuAttachment(source, out _);
	}

	private bool CpuHasAvailableClassAbility(BattleCardState card)
	{
		if (!CanCpuUseAdvancedActions(card) || card.AbilityUsed || card.AbilityArmed || !ClassAbilitiesEnabled(card) || !IsCampaignPrimaryAndAttackAffordable(card))
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
		if (!CanCpuUseAdvancedActions(card) || card.AbilityUsed || card.AbilityArmed || !ClassAbilitiesEnabled(card) || !IsCampaignPrimaryAndAttackAffordable(card))
		{
			return false;
		}
		switch (card.Card.HeroClass)
		{
		case HeroClass.Warrior:
			if (!TrySpendCampaignPrimaryMana(card))
				return false;
			card.AbilityArmed = true;
			RefreshPersistentStatus(card);
			message = "CPU ABILITA: " + card.Card.Name + " prepara un colpo pesante: tirera il dado Vigore e un dado di uno step inferiore.";
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
			ReducePower(battleCardState, 1);
		}
		MarkAbilityUsed(card);
		RefreshPersistentStatus(battleCardState);
		PlayClassAbilitySfx(HeroClass.Assassin);
		if ((Object)(object)battleAnimationPlayer != (Object)null
			&& (Object)(object)card.View != (Object)null
			&& (Object)(object)battleCardState.View != (Object)null)
		{
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayTargetLine(card.View, battleCardState.View, AbilityTargetLineColor));
			((MonoBehaviour)this).StartCoroutine(battleAnimationPlayer.PlayAssassinSmokeBomb(
				card.View,
				battleCardState.View,
				() => battleCardState.InhibitedTurns > 0));
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
		int num = 1;
		int baseDieSides = runProgress != null ? runProgress.PlayerVigorDieSides : configuration.Gameplay.VigorDieSides;
		int startDieSides = EffectiveVigorDieSides(battleCardState, baseDieSides);
		battleCardState.PendingVigorStepPenalty = StackMageVigorPenalty(
			battleCardState.PendingVigorStepPenalty,
			baseDieSides);
		int endDieSides = EffectiveVigorDieSides(battleCardState, baseDieSides);
		MarkAbilityUsed(card);
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
		if (!TrySpendCampaignPrimaryMana(card))
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
		MarkAbilityUsed(card);
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
		MarkAbilityUsed(card);
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
		MarkAbilityUsed(card);
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
			return !target.Eliminated && target.CampaignCard?.HasRubySeal != true;
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
		target.HasEquipment = true;
		RefreshPersistentStatus(target);
		target.View.PlayAttachmentEquipEffect();
		source.Eliminated = true;
		source.IsAttachment = true;
		source.AttachedTo = target;
		source.View.SetSelected(selected: false);
		SetMessage(GameText.Format(GameTextKeys.Combat.AttachmentApplied, source.Card.Name, target.Card.Name, num));
		AppendLog(GameText.Format(GameTextKeys.Combat.AttachmentAppliedLog, source.Card.Name, target.Card.Name, num));
		PlayAttachmentSfx();
		yield return PlayTimelineAwareDefeatAnimation(source, source.Card.HeroClass);
		yield return WaitForCardInspectionPause(configuration.Animation.TurnResultPause);
		selectedPlayerIndex = -1;
		FinishTurn();
	}

	private IEnumerator ExecuteCpuAttachment(BattleCardState source, BattleCardState target)
	{
		source.View?.PlayEquipActionCallout();
		if ((Object)(object)battleAnimationPlayer != (Object)null)
			yield return battleAnimationPlayer.PlayTargetLine(source.View, target.View, AttachmentTargetLineColor);
		int num = AttachmentBonus(source);
		target.PermanentCombatBonus += num;
		target.HasEquipment = true;
		RefreshPersistentStatus(target);
		target.View.PlayAttachmentEquipEffect();
		source.Eliminated = true;
		source.IsAttachment = true;
		source.AttachedTo = target;
		source.View.SetSelected(selected: false);
		SetMessage(GameText.Format(GameTextKeys.Combat.CpuAttachmentApplied, source.Card.Name, target.Card.Name, num));
		AppendLog(GameText.Format(GameTextKeys.Combat.CpuAttachmentAppliedLog, source.Card.Name, target.Card.Name, num));
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
			if (playerCard != null && (Object)(object)playerCard.View != (Object)null)
				playerCard.View.ClearActionOverlay();
		}
		foreach (BattleCardState cpuCard in cpuCards)
		{
			if (cpuCard != null && (Object)(object)cpuCard.View != (Object)null)
				cpuCard.View.ClearActionOverlay();
		}
		if (auraActivationCalloutVisible)
			return;
		if (pendingDeploymentIndex >= 0 && pendingDeploymentIndex < draftViews.Count)
		{
			((Component)confirmActionButton).gameObject.SetActive(false);
			((Component)cancelActionButton).gameObject.SetActive(false);
			if (adventureScriptedTutorialActive)
			{
				draftViews[pendingDeploymentIndex].ShowConfirmAction(confirmActionSprite, new UnityAction(ConfirmPendingDeployment));
			}
			else
			{
				draftViews[pendingDeploymentIndex].ShowConfirmInfoActions(confirmActionSprite, infoActionSprite, new UnityAction(ConfirmPendingDeployment), new UnityAction(ShowPendingDeploymentInspection));
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
			if (IsTutorialWarriorDuelActive)
				return;
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
			if (IsTutorialWarriorDuelActive)
			{
				// Guerriero e Mago condividono il flag storico; la pratica del Ladro ha
				// invece il proprio sblocco. Controllare sempre quello del Guerriero
				// impediva di creare ATTACCA dopo "I COLORI DEL BERSAGLIO".
				bool tutorialActionUnlocked = tutorialRoguePracticeActive
					? tutorialRogueActionUnlocked
					: tutorialWarriorDuelActionUnlocked;
				if (!tutorialActionUnlocked)
					return;
				if (TutorialWarriorDuelAllowsAbility())
				{
					battleCardState2.View.ShowAbilityAction(GetAbilityButtonSprite(), new UnityAction(ActivateCurrentAbility));
					return;
				}
				if (TutorialWarriorDuelAllowsSupreme())
				{
					battleCardState2.View.ShowSupremeAction(GetSupremeButtonSprite(), new UnityAction(ActivateCurrentSupreme), SupremeManaBadge(battleCardState2));
					return;
				}
				if (TutorialWarriorDuelAllowsAttack() && !attackTargetingActive)
				{
					battleCardState2.View.ShowClassAction(GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack), AttackManaBadge(battleCardState2));
				}
				return;
			}
			bool flag = IsClassAbilityActionAvailable(battleCardState2);
			bool flag2 = CanUseAttachment(battleCardState2);
			((Component)abilityButton).gameObject.SetActive(false);
			if ((Object)(object)attachmentButton != (Object)null)
			{
				((Component)attachmentButton).gameObject.SetActive(false);
			}
			// Lo skip resta sempre disponibile: e' l'uscita di sicurezza da uno stallo in cui
		// hai usato un'abilita', sei a 0 mana e non puoi nemmeno attaccare. Il prezzo di
		// quell'uscita e' il recupero azzerato, gestito in FinishCampaignManaActivation.
			bool skipAvailable = IsSkipAvailableAgainstBragus(battleCardState2);
			bool tutorialRestrictsActions = adventureScriptedTutorialActive && adventureScriptedTutorialStep == 5;
			if (skipAvailable && !tutorialRestrictsActions)
			{
				bool supremeAvailable = IsSupremeActionAvailable(battleCardState2);
				battleCardState2.View.ShowTurnActions(
					GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack),
					flag ? GetAbilityButtonSprite() : null, flag ? new UnityAction(ActivateCurrentAbility) : null,
					flag2 ? GetAttachmentButtonSprite() : null, flag2 ? new UnityAction(ActivateCurrentAttachment) : null,
					GetSkipButtonSprite(), new UnityAction(ActivateCurrentSkip),
					supremeAvailable ? GetSupremeButtonSprite() : null,
					supremeAvailable ? new UnityAction(ActivateCurrentSupreme) : null,
					AttackManaBadge(battleCardState2),
					flag ? PrimaryManaBadge(battleCardState2) : null,
					SkipManaBadge(battleCardState2),
					supremeAvailable ? SupremeManaBadge(battleCardState2) : null);
				battleCardState2.View.SetAbilityActionInteractable(
					!flag || IsCampaignPrimaryAffordable(battleCardState2));
				battleCardState2.View.SetSupremeActionInteractable(
					!supremeAvailable || IsCampaignSupremeAffordable(battleCardState2));
			}
			else if (flag && flag2)
			{
				if (adventureScriptedTutorialActive && adventureScriptedTutorialStep == 5)
				{
					battleCardState2.View.ShowClassAction(
						GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack),
						AttackManaBadge(battleCardState2));
				}
				else
				{
					battleCardState2.View.ShowTripleActions(GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack), GetAbilityButtonSprite(), new UnityAction(ActivateCurrentAbility), GetAttachmentButtonSprite(), new UnityAction(ActivateCurrentAttachment));
				}
			}
			else if (flag || flag2)
			{
				if (adventureScriptedTutorialActive && adventureScriptedTutorialStep == 5)
				{
					battleCardState2.View.ShowClassAction(
						GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack),
						AttackManaBadge(battleCardState2));
				}
				else
				{
					battleCardState2.View.ShowDualActions(
						GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack),
						flag ? GetAbilityButtonSprite() : GetAttachmentButtonSprite(),
						flag ? new UnityAction(ActivateCurrentAbility) : new UnityAction(ActivateCurrentAttachment),
						AttackManaBadge(battleCardState2),
						flag ? PrimaryManaBadge(battleCardState2) : null);
				}
			}
			else
			{
				battleCardState2.View.ShowClassAction(
					GetAttackButtonSprite(), new UnityAction(ActivateCurrentAttack),
					AttackManaBadge(battleCardState2));
			}
		}
	}

	private bool IsClassAbilityActionAvailable(BattleCardState card)
	{
		// Il mana non entra qui: il bottone resta visibile e cliccabile anche senza
		// riserva, e il rifiuto arriva come callout sulla pedina quando lo premi.
		// Non c'e' nemmeno un limite d'uso: l'abilita' si puo' ripetere, anche nello
		// stesso turno, finche' la riserva regge. AbilityArmed resta perche' un'abilita'
		// gia' innescata va prima consumata, altrimenti se ne perderebbe una pagata.
		if (card == null || card.Eliminated || card.AbilityArmed)
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

	private Sprite GetSkipButtonSprite()
	{
		return LoadSpriteResource("UI/skip_button");
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

	private static Sprite LoadHoneyPotCurrencySprite()
	{
		HoneyPotCurrencyReference reference = Resources.Load<HoneyPotCurrencyReference>("UI/HoneyPotCurrencyReference");
		return reference != null && reference.Sprite != null
			? reference.Sprite
			: LoadSpriteResource("UI/honey_pot_currency");
	}

	private void FinishTurn(bool skipped = false)
	{
		if (turnOrder.Count > 0 && currentTurnIndex >= 0 && currentTurnIndex < turnOrder.Count)
		{
			BattleCardState battleCardState = turnOrder[currentTurnIndex];
			FinishCampaignManaActivation(battleCardState, skipped);
			if (battleCardState.IsSpirit)
			{
				battleCardState.IsSpirit = false;
				battleCardState.Eliminated = true;
				battleCardState.RevivedRound = 0;
				battleCardState.View.SetSelected(selected: false);
				ApplyMightAuraDeathBonuses(battleCardState);
				RefreshPersistentStatus(battleCardState);
				AppendLog(GameText.Format(GameTextKeys.Combat.SpiritLastTurnEndedLog, battleCardState.Card.Name));
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

		AppendLog(GameText.Format(GameTextKeys.Combat.GolemNewFormLog, GolemFormName(activeComposableGolem.ActiveForm.Form)));
		BattleCardState golemProxy = cpuCards.FirstOrDefault((BattleCardState card) => IsComposableGolemProxy(card));
		RefreshComposableGolemPawn(golemProxy);
		RefreshInitiativeDisplay();
	}

	private IEnumerator ShowCombatResult(CombatResult result, BattleCardState attacker, BattleCardState defender)
	{
		int attackerStart = attacker != null ? DisplayStrength(attacker) : 0;
		int defenderStart = defender != null ? DisplayStrength(defender) : 0;
		PrototypeCardView attackerView = attacker?.View;
		PrototypeCardView defenderView = defender?.View;
		bool invigorateAttacker = result.AttackerTotal >= result.DefenderTotal;
		bool invigorateDefender = result.DefenderTotal >= result.AttackerTotal;
		if (attacker != null)
		{
			combatStrengthPresentationStarts[attacker] = attackerStart;
			combatStrengthPresentationTotals[attacker] = result.AttackerTotal;
			if (invigorateAttacker)
				combatStrengthPresentationScales[attacker] = 1f + Mathf.Max(0, result.AttackerRoll.SelectedRoll) * 0.02f;
			else
				combatStrengthPresentationScales.Remove(attacker);
		}
		if (defender != null)
		{
			combatStrengthPresentationStarts[defender] = defenderStart;
			combatStrengthPresentationTotals[defender] = result.DefenderTotal;
			if (invigorateDefender)
				combatStrengthPresentationScales[defender] = 1f + Mathf.Max(0, result.DefenderRoll.SelectedRoll) * 0.02f;
			else
				combatStrengthPresentationScales.Remove(defender);
		}
		if ((Object)(object)combatResultRoot != (Object)null)
			combatResultRoot.SetActive(false);

		// Conteggio, ingrossamento e colore del verdetto stanno nel player
		// condiviso: e' lo stesso identico gesto che fa il PvP. L'attesa di
		// lettura resta qui perche' deve sospendersi se apri una carta, cosa
		// che il player non sa fare.
		yield return AccardND.Battlefield.BattlePresentationAnimationPlayer.AnimateResolvedStrength(
			attackerView,
			defenderView,
			attackerStart,
			defenderStart,
			result.AttackerTotal,
			result.DefenderTotal,
			result.AttackerRoll.SelectedRoll,
			result.DefenderRoll.SelectedRoll,
			Mathf.Clamp(configuration.Animation.CombatResultHold * 0.38f, 0.42f, 0.78f),
			resultHold: 0f);

		yield return WaitForCardInspectionPause(Mathf.Max(0.35f, configuration.Animation.CombatResultHold * 0.42f) + 0.2f);
		if (!result.DefenderIsDefeated && result.DefenderTotal >= result.AttackerTotal)
		{
			RegisterCampaignParryMana(defender);
		}
	}

	private IEnumerator RestoreCombatStrengthPresentation(BattleCardState attacker, BattleCardState defender)
	{
		int attackerStart = attacker != null && combatStrengthPresentationStarts.TryGetValue(attacker, out int savedAttacker)
			? savedAttacker : (attacker != null ? DisplayStrength(attacker) : 0);
		int defenderStart = defender != null && combatStrengthPresentationStarts.TryGetValue(defender, out int savedDefender)
			? savedDefender : (defender != null ? DisplayStrength(defender) : 0);
		int attackerCurrent = attacker != null && combatStrengthPresentationTotals.TryGetValue(attacker, out int savedAttackerTotal)
			? savedAttackerTotal : attackerStart;
		int defenderCurrent = defender != null && combatStrengthPresentationTotals.TryGetValue(defender, out int savedDefenderTotal)
			? savedDefenderTotal : defenderStart;
		float attackerScale = attacker != null && combatStrengthPresentationScales.TryGetValue(attacker, out float savedAttackerScale)
			? savedAttackerScale : 1f;
		float defenderScale = defender != null && combatStrengthPresentationScales.TryGetValue(defender, out float savedDefenderScale)
			? savedDefenderScale : 1f;

		// Stesso ritorno del PvP: i totali scendono verso le potenze vere e il
		// colore del verdetto si spegne. Sta nel player condiviso.
		yield return AccardND.Battlefield.BattlePresentationAnimationPlayer.RestoreResolvedStrength(
			attacker?.View,
			defender?.View,
			attackerStart,
			defenderStart,
			attackerCurrent,
			defenderCurrent,
			attackerScale,
			defenderScale,
			Mathf.Clamp(configuration.Animation.CombatResultHold * 0.38f, 0.42f, 0.78f));
		if (attacker != null)
		{
			combatStrengthPresentationStarts.Remove(attacker);
			combatStrengthPresentationTotals.Remove(attacker);
			combatStrengthPresentationScales.Remove(attacker);
		}
		if (defender != null)
		{
			combatStrengthPresentationStarts.Remove(defender);
			combatStrengthPresentationTotals.Remove(defender);
			combatStrengthPresentationScales.Remove(defender);
		}
	}

	private Vector3 BackdropBossDuelWorldPoint(bool isBackdropBoss)
	{
		RectTransform root = (Object)(object)safeAreaRoot != (Object)null ? safeAreaRoot : canvasRect;
		Rect rect = root.rect;
		Vector3 localPoint = new Vector3(
			rect.center.x,
			Mathf.Lerp(rect.yMin, rect.yMax, isBackdropBoss ? 0.84f : 0.30f),
			0f);
		return ((Transform)root).TransformPoint(localPoint);
	}

	private IEnumerator ShowAutomaticOutcome(bool guaranteedKill)
	{
		combatScoreText.text = (guaranteedKill ?"100%" : "0%");
		combatOutcomeText.text = guaranteedKill
			? GameText.GetOrFallbackSilent(GameTextKeys.Combat.GuaranteedKill, "ELIMINAZIONE CERTA")
			: GameText.GetOrFallbackSilent(GameTextKeys.Combat.ImpossiblePlayerAttack, "ATTACCO IMPOSSIBILE - TURNO SALTATO");
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
			BeginCampaignManaRound();
		}
	}

	private BattleAuraType DetermineAura(IReadOnlyList<BattleCardState> formation)
	{
		// Le spade di Jurinashor sono evocazioni passive: non sono membri della
		// formazione e non possono comporre l'aura Necromante.
		List<BattleCardState> list = formation
			.Where(card => card != null && !IsJurinashorSword(card))
			.ToList();
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
			// Le aure restano indicate dal token di stato e dal callout iniziale:
			// non applicare un alone persistente attorno alle carte.
			playerCard?.View?.SetBattleAura(false, Color.clear, string.Empty);
			RefreshPersistentStatus(playerCard);
		}
		if (appendLog)
		{
			if (flag)
				AppendLog(GameText.Format(GameTextKeys.Combat.AuraActiveLog, AuraDisplayName(playerAura)));
			ShowPlayerAuraActivationCallout(playerAura);
			if (flag)
				ShowFirstAuraHint(playerAura);
		}
	}

	/// <summary>
	/// Annuncia l'aura appena composta con lo stesso callout animato delle azioni
	/// della carta (Attacca, Abilita', Equipaggia). Viene chiamato solo alla
	/// partenza dello scontro, quindi non si ripete durante gli aggiornamenti UI.
	/// </summary>
	private void ShowPlayerAuraActivationCallout(BattleAuraType aura)
	{
		BattleCardState source = playerCards.FirstOrDefault(card => card?.View != null);
		if (source == null)
			return;

		bool hasAura = aura != BattleAuraType.None;
		source.View.PlayFormationCallout(
			hasAura ? AuraActivationLabel(aura) : "NO AURA",
			hasAura ? AuraColor(aura) : Color.white,
			safeAreaRoot,
			playerOwned: true);
	}

	private void ApplyCpuAuraVisuals(bool appendLog)
	{
		bool flag = cpuAura != BattleAuraType.None;
		foreach (BattleCardState cpuCard in cpuCards)
		{
			cpuCard?.View?.SetBattleAura(false, Color.clear, string.Empty);
			RefreshPersistentStatus(cpuCard);
		}
		if (appendLog)
		{
			if (flag)
				AppendLog(GameText.Format(GameTextKeys.Combat.CpuAuraActiveLog, AuraDisplayName(cpuAura)));
			ShowCpuAuraActivationCallout(cpuAura);
		}
	}

	private void ShowCpuAuraActivationCallout(BattleAuraType aura)
	{
		BattleCardState source = cpuCards.FirstOrDefault(card => card?.View != null);
		if (source == null)
			return;

		bool hasAura = aura != BattleAuraType.None;
		source.View.PlayFormationCallout(
			hasAura ? AuraActivationLabel(aura) : "NO AURA",
			hasAura ? AuraColor(aura) : Color.white,
			safeAreaRoot,
			playerOwned: false);
	}

	private static Color AuraColor(BattleAuraType aura)
	{
		return (Color)(aura switch
		{
			BattleAuraType.Might => new Color(1f, 0.16f, 0.12f), 
			BattleAuraType.Cunning => new Color(0.1f, 0.92f, 0.36f), 
			BattleAuraType.Magic => new Color(0.2f, 0.5f, 1f), 
			BattleAuraType.Formation => new Color(1f, 0.86f, 0.22f), 
			BattleAuraType.Warrior => new Color(0.24f, 0.3f, 0.38f),
			BattleAuraType.Barbarian => new Color(0.46f, 0.24f, 0.12f),
			BattleAuraType.Paladin => new Color(0.95f, 0.75f, 0.15f),
			BattleAuraType.Rogue => new Color(0.45f, 0.45f, 0.5f),
			BattleAuraType.Assassin => new Color(1f, 0.08f, 0.04f),
			BattleAuraType.Hunter => new Color(0.92f, 0.45f, 0.08f),
			BattleAuraType.Mage => new Color(0.66f, 0.24f, 1f),
			BattleAuraType.Necromancer => new Color(0.12f, 0.42f, 0.24f),
			BattleAuraType.Priest => new Color(0.9f, 0.9f, 0.95f),
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

	private static string AuraActivationLabel(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => LocalizedAuraActivation("might", "Aura Forzuta", "Might Faction Aura", "Aura der Stärke-Fraktion", "Aura de Facción Fuerte", "Aura de la faction Puissance"),
			BattleAuraType.Cunning => LocalizedAuraActivation("cunning", "Aura Astuta", "Cunning Faction Aura", "Aura der List-Fraktion", "Aura de Facción Astuta", "Aura de la faction Ruse"),
			BattleAuraType.Magic => LocalizedAuraActivation("magic", "Aura Magica", "Magic Faction Aura", "Aura der Magie-Fraktion", "Aura de Facción Mágica", "Aura de la faction Magie"),
			BattleAuraType.Formation => LocalizedAuraActivation("formation", "Aura Formazione", "Formation Aura", "Formationsaura", "Aura de Formación", "Aura de formation"),
			BattleAuraType.Warrior => LocalizedAuraActivation("warrior", "Aura Guerrieri", "Warrior Class Aura", "Krieger-Klassenaura", "Aura de Clase Guerrero", "Aura de classe Guerrier"),
			BattleAuraType.Barbarian => LocalizedAuraActivation("barbarian", "Aura Barbari", "Barbarian Class Aura", "Barbaren-Klassenaura", "Aura de Clase Bárbaro", "Aura de classe Barbare"),
			BattleAuraType.Paladin => LocalizedAuraActivation("paladin", "Aura Paladini", "Paladin Class Aura", "Paladin-Klassenaura", "Aura de Clase Paladín", "Aura de classe Paladin"),
			BattleAuraType.Rogue => LocalizedAuraActivation("rogue", "Aura Ladri", "Thief Class Aura", "Diebes-Klassenaura", "Aura de Clase Ladrón", "Aura de classe Voleur"),
			BattleAuraType.Assassin => LocalizedAuraActivation("assassin", "Aura Assassini", "Assassin Class Aura", "Assassinen-Klassenaura", "Aura de Clase Asesino", "Aura de classe Assassin"),
			BattleAuraType.Hunter => LocalizedAuraActivation("hunter", "Aura Cacciatori", "Hunter Class Aura", "Jäger-Klassenaura", "Aura de Clase Cazador", "Aura de classe Chasseur"),
			BattleAuraType.Mage => LocalizedAuraActivation("mage", "Aura Maghi", "Mage Class Aura", "Magier-Klassenaura", "Aura de Clase Mago", "Aura de classe Mage"),
			BattleAuraType.Necromancer => LocalizedAuraActivation("necromancer", "Aura Necromanti", "Necromancer Class Aura", "Nekromanten-Klassenaura", "Aura de Clase Nigromante", "Aura de classe Nécromancien"),
			BattleAuraType.Priest => LocalizedAuraActivation("priest", "Aura Sacerdoti", "Priest Class Aura", "Priester-Klassenaura", "Aura de Clase Sacerdote", "Aura de classe Prêtre"),
			_ => string.Empty
		};
	}

	private static string LocalizedAuraActivation(string auraId, string italian, string english, string german, string spanish, string french)
	{
		return GameText.GetLocalizedFallback("combat.aura.activation." + auraId, italian, english, german, spanish, french);
	}

	private static string AuraDisplayName(BattleAuraType aura)
	{
		return aura switch
		{
			BattleAuraType.Might => "Fazione Fortuza",
			BattleAuraType.Cunning => "Fazione Astuta",
			BattleAuraType.Magic => "Fazione Magica",
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
		string roomLabel = CurrentRoomHudLabel();
		string text6 = $"{roomLabel}  |  CPU D{EffectiveCpuHudVigorDieSides()}  |  {text4}  |  {text5}";
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
		List<string> previousTimelineOrder = new List<string>(campaignTimelineOrderKeys);
		List<string> expectedTimelineOrder = BuildCampaignTimelineOrder();
		if (pvpTimelineSlideRoutine != null && previousTimelineOrder.SequenceEqual(expectedTimelineOrder))
			return;

		RestoreTimelineBaseRect();
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
		text.text = GameText.GetOrFallbackSilent(GameTextKeys.Combat.ChangeForm, "CAMBIO FORMA\n{0}", GolemFormName(nextForm));
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
		if (IsJurinashorSword(card))
		{
			yield return RemoveDefeatedJurinashorSword(card);
			yield break;
		}

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
			return $"{attacker.Card.Name} non frantuma lo scudo {target}. Solo chi ha vantaggio sulla fazione dello scudo puo aprire Palatir.";
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
		// Nelle stanze boss il chrome vuoto resta nascosto all'ingresso, ma la
		// timeline deve comparire appena i dadi iniziativa vi vengono inseriti.
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
		if (adventureScriptedTutorialActive)
		{
			string requiredTargetId = IsTutorialCard(attacker.Card.Id, "6-chimera-rogue")
				? "7-whitealien-rogue"
				: IsTutorialCard(attacker.Card.Id, "7-whitealien-mage")
					? "10-champion-warrior"
					: null;
			if (requiredTargetId != null)
			{
				int scriptedIndex = playerCards.FindIndex(card =>
					card != null
					&& !card.Eliminated
					&& !IsShieldedByInvisibility(card)
					&& IsTutorialCard(card.Card.Id, requiredTargetId));
				if (scriptedIndex >= 0)
				{
					decisionReason = "bersaglio previsto dal tutorial";
					return scriptedIndex;
				}
			}
		}

		GameplayConfiguration gameplay = configuration.Gameplay;
		// La CPU valuta chi incassera' davvero il colpo, non chi punta: se un Paladino devia
		// l'attacco, probabilita', dado di difesa e modificatori sono i suoi.
		List<CombatCard> list = new List<CombatCard>(playerCards.Count);
		List<bool> list2 = new List<bool>(playerCards.Count);
		BattleCardState[] effectiveDefenders = new BattleCardState[playerCards.Count];
		bool[] defenderProtected = new bool[playerCards.Count];
		for (int index = 0; index < playerCards.Count; index++)
		{
			BattleCardState playerCard = playerCards[index];
			bool unavailable = playerCard.Eliminated || IsShieldedByInvisibility(playerCard);
			BattleCardState defender = playerCard;
			if (!unavailable)
			{
				defender = ResolveCpuAttackDefender(playerCard, out BattleCardState paladinProtectionUser);
				defenderProtected[index] = paladinProtectionUser != null;
			}
			effectiveDefenders[index] = defender;
			list.Add(defender.Card);
			list2.Add(unavailable);
		}
		CpuDecisionWeights weights = new CpuDecisionWeights(gameplay.KillProbabilityWeight, gameplay.ClassAdvantageWeight, gameplay.ThreatWeight, gameplay.RandomTieBreaker);
		int attackerVigorDieSides = EffectiveVigorDieSides(attacker, runProgress.MasterVigorDieSides);
		CpuTargetDecision cpuTargetDecision = cpuDecisionService.ChooseTarget(
			attacker.Card,
			list,
			list2,
			attackerVigorDieSides,
			(int targetIndex) => EffectiveDefenseVigorDieSides(effectiveDefenders[targetIndex], runProgress.PlayerVigorDieSides),
			(CpuDifficulty)gameplay.CpuDifficulty,
			weights,
			(int targetIndex) => BuildAttackModifiers(attacker, effectiveDefenders[targetIndex], defenderProtected[targetIndex], defenderProtected[targetIndex], updateVisuals: false));
		string text = ((gameplay.CpuDifficulty != CpuDifficultySetting.Easy) ?(cpuTargetDecision.Matchup switch
		{
			MatchupResult.Advantage => "vantaggio di classe",
			MatchupResult.Disadvantage => "migliore probabilita nonostante lo svantaggio",
			_ => "migliore probabilita",
		}) : "scelta casuale");
		string arg = text;
		string redirect = effectiveDefenders[cpuTargetDecision.TargetIndex] != playerCards[cpuTargetDecision.TargetIndex]
			? $" contro {effectiveDefenders[cpuTargetDecision.TargetIndex].Card.Name} che protegge"
			: string.Empty;
		decisionReason = $"{arg}, {cpuTargetDecision.DefeatProbability:P0} di eliminazione{redirect}";
		return cpuTargetDecision.TargetIndex;
	}

	private void ShowTargetHints(BattleCardState attacker)
	{
		foreach (BattleCardState cpuCard in cpuCards)
		{
			// Le spade eliminate restano nello stato della battaglia, ma il loro VFX
			// distrugge la View: non devono più partecipare al refresh dei bersagli.
			if (cpuCard == null || cpuCard.Eliminated
				|| (Object)(object)cpuCard.View == (Object)null)
			{
				continue;
			}
			RefreshPersistentStatus(cpuCard);
			bool unavailable = (attackTargetingActive && IsShieldedByInvisibility(cpuCard))
				|| (abilityTargetMode == AbilityTargetMode.HunterEnemy && IsHunterMarked(cpuCard));
			if (IsTutorialWarriorDuelActive && !TutorialWarriorDuelAllowsEnemyTarget(cpuCard))
				unavailable = true;
			MatchupResult matchup = IsPalatirBossProxy(cpuCard)
				? MatchupResult.Neutral
				: ClassMatchup.Compare(attacker.Card.HeroClass, cpuCard.Card.HeroClass);
			if (attacker.BelongsToPlayer
				&& playerAura == BattleAuraType.Formation
				&& matchup == MatchupResult.Disadvantage)
			{
				matchup = MatchupResult.Neutral;
			}
			cpuCard.View.SetTargetHint(unavailable ?((MatchupResult?)null) : new MatchupResult?(matchup));
		}
	}

	private void ShowDeploymentMatchupHints(CardDefinition selectedCard)
	{
		if ((Object)(object)selectedCard == (Object)null)
		{
			ClearTargetHints();
			return;
		}

		foreach (BattleCardState cpuCard in cpuCards)
		{
			if (cpuCard == null || (Object)(object)cpuCard.View == (Object)null)
			{
				continue;
			}

			RefreshPersistentStatus(cpuCard);
			MatchupResult matchup = DeploymentMatchup(selectedCard, cpuCard);
			cpuCard.View.SetTargetHint(cpuCard.Eliminated
				? ((MatchupResult?)null)
				: new MatchupResult?(matchup));
		}

		// Durante lo schieramento campagna le pedine CPU visibili sono ancora
		// preview: cpuCards viene popolata soltanto quando la formazione è completa.
		for (int index = 0; index < cpuDeploymentPreviewViews.Count; index++)
		{
			PrototypeCardView preview = cpuDeploymentPreviewViews[index];
			if ((Object)(object)preview == (Object)null)
				continue;

			CardDefinition enemyDefinition = index < selectedCpuDeploymentCards.Count
				? selectedCpuDeploymentCards[index]
				: null;
			preview.SetTargetHint((Object)(object)enemyDefinition == (Object)null
				? ((MatchupResult?)null)
				: DeploymentMatchup(selectedCard, enemyDefinition));
		}
	}

	private MatchupResult DeploymentMatchup(CardDefinition selectedCard, BattleCardState enemyCard)
	{
		if ((Object)(object)selectedCard == (Object)null || enemyCard == null)
			return MatchupResult.Neutral;

		return IsPalatirBossProxy(enemyCard)
			? MatchupResult.Neutral
			: DeploymentMatchup(selectedCard, enemyCard.Definition);
	}

	private static MatchupResult DeploymentMatchup(CardDefinition selectedCard, CardDefinition enemyCard)
	{
		if ((Object)(object)selectedCard == (Object)null || (Object)(object)enemyCard == (Object)null)
			return MatchupResult.Neutral;

		return IsPalatirBossDefinition(enemyCard)
			? MatchupResult.Neutral
			: ClassMatchup.Compare(selectedCard.HeroClass, enemyCard.HeroClass);
	}

	private void ClearTargetHints()
	{
		foreach (BattleCardState cpuCard in cpuCards)
		{
			if (cpuCard != null && (Object)(object)cpuCard.View != (Object)null)
				cpuCard.View.SetTargetHint(null);
		}
		foreach (PrototypeCardView preview in cpuDeploymentPreviewViews)
		{
			if ((Object)(object)preview != (Object)null)
				preview.SetTargetHint(null);
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
