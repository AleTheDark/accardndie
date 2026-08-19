using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	/// <summary>
	/// I due flussi di dadi, tenuti come sorgenti concrete e non come IRandomSource:
	/// solo così si può salvare a che punto sono arrivati. Il primo è quello della
	/// partita (iniziative, attacchi, vigore), il secondo quello con cui la CPU decide.
	/// </summary>
	private SeededRandomSource battleRandom;

	private SeededRandomSource battleCpuRandom;

	/// <summary>
	/// Fotografare la battaglia ha senso solo in uno scontro vero e vivo. Le lezioni del
	/// tutorial, le scene di prova e le stanze che non sono combattimenti restano fuori:
	/// sono percorsi guidati, riprenderli a metà non vuol dire niente.
	/// </summary>
	private bool CanSnapshotBattle =>
		IsCampaignBattleActive()
		&& !pvpPresentationActive
		&& !gameFinished
		&& !adventureScriptedTutorialActive
		&& !bossDebugSceneSession
		&& !debugMerchantScene
		&& !debugLootRoomScene
		&& !IsComposableGolemDebugSession
		&& (currentRoomType == RoomType.Monster || currentRoomType == RoomType.Boss)
		&& !draftActive
		&& !deploymentDraftActive
		&& roundNumber > 0
		&& turnOrder.Count > 0
		&& currentTurnIndex >= 0 && currentTurnIndex < turnOrder.Count
		&& playerCards.Count > 0
		&& cpuCards.Count > 0;

	// --- Cattura ---

	/// <summary>
	/// Salva la run con dentro la battaglia in corso. Va chiamata al confine fra due
	/// turni, l'unico istante in cui non c'è niente a mezz'aria: nessun dado che rotola,
	/// nessuna animazione a metà, nessun bersaglio da scegliere.
	/// </summary>
	private void SaveCurrentBattleTurn()
	{
		if (!CanSnapshotBattle)
			return;
		SaveCurrentRun();
	}

	/// <summary>
	/// La battaglia com'è adesso, o null se non c'è niente da fotografare. La chiama
	/// CaptureRunSave: lo snapshot viaggia dentro il salvataggio della run, non da solo,
	/// perché mazzo, progressione e bisaccia devono restare coerenti con lui.
	/// </summary>
	private CampaignBattleSave CaptureBattle()
	{
		if (!CanSnapshotBattle)
			return null;

		var battle = new CampaignBattleSave
		{
			roundNumber = roundNumber,
			currentTurnIndex = currentTurnIndex,
			roomType = (int)currentRoomType,
			playerAura = (int)playerAura,
			cpuAura = (int)cpuAura,
			formationAuraUsed = formationAuraUsed,
			necromancerSpiritUsed = necromancerSpiritUsed,
			skipNextCombatCooldown = skipNextCombatCooldown,
			nextCombatFallenHeroesGrantExperience = nextCombatFallenHeroesGrantExperience,
			nextCombatAssassinsActLast = nextCombatAssassinsActLast,
			nextCombatWarriorsLowerVigor = nextCombatWarriorsLowerVigor,
			nextCombatTankDuel = nextCombatTankDuel,
			freePrimaryAbilityAvailable = freePrimaryAbilityAvailable,
			boss = CaptureBoss(),
			playerMana = CaptureMana(campaignPlayerMana),
			cpuMana = CaptureMana(campaignCpuMana),
			randomSeed = battleRandom?.Seed ?? 0,
			randomDraws = battleRandom?.Draws ?? 0,
			cpuRandomSeed = battleCpuRandom?.Seed ?? 0,
			cpuRandomDraws = battleCpuRandom?.Draws ?? 0
		};

		foreach (BattleCardState card in playerCards)
			battle.playerPawns.Add(CapturePawn(card));
		foreach (BattleCardState card in cpuCards)
			battle.cpuPawns.Add(CapturePawn(card));
		foreach (BattleCardState card in turnOrder)
			battle.turnOrder.Add(EncodePawn(card));

		// Le formazioni di partenza: sono quelle che "riprova stanza" rimette in campo.
		foreach (CardDefinition definition in initialPlayerFormation)
			battle.initialPlayerFormation.Add(definition != null ? definition.Id : string.Empty);
		foreach (CampaignCardInstance instance in initialPlayerCampaignFormation)
			battle.initialPlayerCampaignInstances.Add(instance?.InstanceId ?? 0);
		foreach (CardDefinition definition in initialCpuFormation)
			battle.initialCpuFormation.Add(definition != null ? definition.Id : string.Empty);

		foreach (BattleCardState card in campaignManaEliminations)
			battle.manaEliminations.Add(EncodePawn(card));
		foreach (BattleCardState card in campaignPaidPrimaryAbilities)
			battle.paidPrimaryAbilities.Add(EncodePawn(card));

		return battle;
	}

	private CampaignBattlePawnSave CapturePawn(BattleCardState card)
	{
		return new CampaignBattlePawnSave
		{
			definitionId = card.Definition != null ? card.Definition.Id : string.Empty,
			campaignInstanceId = card.CampaignCard?.InstanceId ?? 0,
			combatStrength = card.Card?.Strength ?? 0,
			initiative = card.Initiative,
			initiativeTalentBonus = card.InitiativeTalentBonus,
			opensTheFight = card.OpensTheFight,
			tieBreaker = card.TieBreaker,
			eliminated = card.Eliminated,
			abilityArmed = card.AbilityArmed,
			abilityUsed = card.AbilityUsed,
			abilityUsedThisTurn = card.AbilityUsedThisTurn,
			supremeUsedThisTurn = card.SupremeUsedThisTurn,
			pendingAttackBonus = card.PendingAttackBonus,
			pendingAttackBonusKind = (int)card.PendingAttackBonusKind,
			permanentCombatBonus = card.PermanentCombatBonus,
			mightAuraCombatBonus = card.MightAuraCombatBonus,
			inhibitedTurns = card.InhibitedTurns,
			wasInhibited = card.WasInhibited,
			pendingVigorStepPenalty = card.PendingVigorStepPenalty,
			isSpirit = card.IsSpirit,
			revivedRound = card.RevivedRound,
			isAttachment = card.IsAttachment,
			hasEquipment = card.HasEquipment,
			isUntargetable = card.IsUntargetable,
			necromancerMinions = card.NecromancerMinions,
			petrified = card.Petrified,
			seraphelSeals = card.SeraphelSeals,
			markedTarget = EncodePawn(card.MarkedTarget),
			hunterMarkedTargets = card.HunterMarkedTargets.Select(EncodePawn).ToList(),
			protectedAlly = EncodePawn(card.ProtectedAlly),
			attachedTo = EncodePawn(card.AttachedTo)
		};
	}

	/// <summary>Indica una pedina con un intero solo; NoPawn quando non c'è.</summary>
	private int EncodePawn(BattleCardState card)
	{
		if (card == null)
			return CampaignBattlePawnSave.NoPawn;
		int index = playerCards.IndexOf(card);
		if (index >= 0)
			return CampaignBattleSave.EncodePawn(belongsToPlayer: true, index);
		index = cpuCards.IndexOf(card);
		return index >= 0
			? CampaignBattleSave.EncodePawn(belongsToPlayer: false, index)
			: CampaignBattlePawnSave.NoPawn;
	}

	private CampaignBattleBossSave CaptureBoss()
	{
		var boss = new CampaignBattleBossSave();
		if (activeComposableGolem != null)
		{
			boss.kind = CampaignBattleBossSave.Golem;
			boss.maxHitPoints = activeComposableGolem.MaxHitPoints;
			boss.hitPoints = activeComposableGolem.HitPoints;
			boss.activeForm = activeComposableGolem.ActiveFormIndex;
			boss.roundsInActiveForm = activeComposableGolem.RoundsInActiveForm;
			boss.hasInitiative = activeComposableGolem.Initiative.HasValue;
			boss.initiative = activeComposableGolem.Initiative ?? 0;
			boss.roundsPerForm = ComposableGolem.DefaultRoundsPerForm;
			foreach (ComposableGolemFormStats form in activeComposableGolem.Forms)
			{
				boss.forms.Add(new CampaignGolemFormSave
				{
					form = (int)form.Form,
					basePower = form.BasePower,
					powerBonus = form.PowerBonus,
					vigorDieSides = form.VigorDieSides
				});
			}
			return boss;
		}
		if (activeMedusaBoss != null)
		{
			boss.kind = CampaignBattleBossSave.Medusa;
			boss.maxHitPoints = activeMedusaBoss.MaxHitPoints;
			boss.hitPoints = activeMedusaBoss.HitPoints;
			return boss;
		}
		if (activeSeraphelBoss != null)
		{
			boss.kind = CampaignBattleBossSave.Seraphel;
			boss.maxHitPoints = activeSeraphelBoss.MaxHitPoints;
			boss.hitPoints = activeSeraphelBoss.HitPoints;
			boss.phaseTwo = activeSeraphelBoss.IsPhaseTwo;
			return boss;
		}
		if (activeTrentorBoss != null)
		{
			boss.kind = CampaignBattleBossSave.Trentor;
			boss.maxHitPoints = activeTrentorBoss.MaxHitPoints;
			boss.hitPoints = activeTrentorBoss.HitPoints;
			boss.turnsTaken = activeTrentorBoss.TurnsTaken;
			return boss;
		}
		if (activeBragusBoss != null)
		{
			boss.kind = CampaignBattleBossSave.Bragus;
			boss.maxHitPoints = activeBragusBoss.MaxHitPoints;
			boss.hitPoints = activeBragusBoss.HitPoints;
			return boss;
		}
		if (activePalatirBoss != null)
		{
			boss.kind = CampaignBattleBossSave.Palatir;
			boss.maxHitPoints = activePalatirBoss.MaxHitPoints;
			boss.hitPoints = activePalatirBoss.HitPoints;
			return boss;
		}
		return boss;
	}

	private static CampaignManaSave CaptureMana(ManaPool pool)
	{
		var mana = new CampaignManaSave { current = pool?.Current ?? 0 };
		if (pool == null)
			return mana;
		foreach (KeyValuePair<HeroClass, int> used in pool.SupremeUsesByClass)
		{
			mana.supremeUses.Add(new CampaignSupremeUseSave
			{
				heroClass = used.Key.ToString(),
				uses = used.Value
			});
		}
		return mana;
	}

	// --- Ripristino ---

	/// <summary>
	/// Rimonta la battaglia salvata e la fa ripartire dal turno di chi doveva giocare.
	/// false se lo snapshot non è utilizzabile - una carta sparita da un aggiornamento,
	/// una timeline che non torna - e in quel caso chi chiama riparte dalla scelta della
	/// via, che è sempre un punto valido.
	///
	/// L'ordine conta: prima i dadi (tutto il resto ne dipende), poi le pedine, poi i
	/// riferimenti fra pedine, e per ultima la scena.
	/// </summary>
	private bool TryRestoreBattle(CampaignBattleSave battle)
	{
		if (battle == null || battle.roundNumber <= 0 || battle.playerPawns.Count == 0)
			return false;
		if ((Object)(object)cardDatabase == (Object)null || (Object)(object)playerRow == (Object)null
			|| (Object)(object)cpuRow == (Object)null)
		{
			return false;
		}

		RestoreBattleRandom(battle);

		currentRoomType = (RoomType)battle.roomType;
		playerAura = (BattleAuraType)battle.playerAura;
		cpuAura = (BattleAuraType)battle.cpuAura;
		formationAuraUsed = battle.formationAuraUsed;
		necromancerSpiritUsed = battle.necromancerSpiritUsed;
		skipNextCombatCooldown = battle.skipNextCombatCooldown;
		nextCombatFallenHeroesGrantExperience = battle.nextCombatFallenHeroesGrantExperience;
		nextCombatAssassinsActLast = battle.nextCombatAssassinsActLast;
		nextCombatWarriorsLowerVigor = battle.nextCombatWarriorsLowerVigor;
		nextCombatTankDuel = battle.nextCombatTankDuel;
		freePrimaryAbilityAvailable = battle.freePrimaryAbilityAvailable;

		RestoreBoss(battle.boss);

		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);
		playerCards.Clear();
		cpuCards.Clear();
		turnOrder.Clear();

		if (!RestorePawns(battle.playerPawns, playerCards, playerRow, belongsToPlayer: true)
			|| !RestorePawns(battle.cpuPawns, cpuCards, cpuRow, belongsToPlayer: false))
		{
			// Mezza board non serve a nessuno: si sgombra e chi ha chiamato riparte dalla
			// scelta della via.
			AbandonHalfRestoredBattle();
			return false;
		}

		// Secondo giro: i riferimenti fra pedine si possono sciogliere solo quando
		// esistono tutte e due le parti.
		RestorePawnLinks(battle.playerPawns, playerCards);
		RestorePawnLinks(battle.cpuPawns, cpuCards);

		RestoreInitialFormations(battle);

		turnOrder.Clear();
		foreach (int encoded in battle.turnOrder)
		{
			BattleCardState card = DecodePawn(encoded);
			if (card != null && IsTimelineParticipant(card))
				turnOrder.Add(card);
		}
		if (turnOrder.Count == 0)
		{
			AbandonHalfRestoredBattle();
			return false;
		}

		roundNumber = battle.roundNumber;
		currentTurnIndex = Mathf.Clamp(battle.currentTurnIndex, 0, turnOrder.Count - 1);

		RestoreBattleMana(battle);

		gameFinished = false;
		draftActive = false;
		deploymentDraftActive = false;
		deploymentInitiativesReady = false;
		attackTargetingActive = false;
		abilityTargetMode = AbilityTargetMode.None;
		activeAbilityUser = null;
		activeAttachmentSource = null;
		selectedPlayerIndex = -1;
		// L'annuncio dell'aura appartiene all'inizio della battaglia: qui la battaglia
		// è già cominciata, e rifarlo sarebbe una finestra modale in faccia a chi
		// riprende.
		suppressInitialCarouselAfterAura = false;

		RestoreBattleScene();
		BeginCurrentTurn();
		return true;
	}

	/// <summary>Sgombra il campo di una ripresa andata storta a metà strada.</summary>
	private void AbandonHalfRestoredBattle()
	{
		DestroyCardViews(playerCards);
		DestroyCardViews(cpuCards);
		ClearCardRowChildren(playerRow);
		ClearCardRowChildren(cpuRow);
		playerCards.Clear();
		cpuCards.Clear();
		turnOrder.Clear();
		roundNumber = 0;
		currentTurnIndex = 0;
	}

	private void RestoreBattleRandom(CampaignBattleSave battle)
	{
		RestoreRunRandom(battle.randomSeed, battle.randomDraws, battle.cpuRandomSeed, battle.cpuRandomDraws);
	}

	/// <summary>
	/// Rimette i due flussi di dadi dove li aveva lasciati il salvataggio, e ricabla su di
	/// loro tutti i servizi che pescano. Vale per la battaglia ripresa come per la stanza
	/// ripresa: da qui in poi la run deve riestrarre esattamente quello che aveva estratto,
	/// o riaprire il gioco diventa un modo di ritirare porte, stanze e dadi.
	/// </summary>
	private void RestoreRunRandom(int seed, int draws, int cpuSeed, int cpuDraws)
	{
		battleRandom = SeededRandomSource.Restore(seed, draws);
		random = battleRandom;
		combatResolver = new CombatResolver(random);
		formationDraftService = new FormationDraftService(random);
		battleCpuRandom = SeededRandomSource.Restore(cpuSeed, cpuDraws);
		cpuDecisionService = new CpuDecisionService(battleCpuRandom);
	}

	private bool RestorePawns(
		List<CampaignBattlePawnSave> saved,
		List<BattleCardState> destination,
		RectTransform row,
		bool belongsToPlayer)
	{
		for (int index = 0; index < saved.Count; index++)
		{
			CampaignBattlePawnSave pawn = saved[index];
			CardDefinition definition = cardDatabase.FindById(pawn.definitionId);
			if ((Object)(object)definition == (Object)null)
			{
				AppendLog($"RIPRESA - carta '{pawn.definitionId}' non piu' nel database: battaglia non ripristinabile.");
				return false;
			}

			CampaignCardInstance campaignCard = FindCampaignCard(pawn.campaignInstanceId);
			BattleCardState card = AddCard(destination, row, definition, belongsToPlayer, index, campaignCard);
			if (card == null)
			{
				AppendLog($"RIPRESA - pedina '{pawn.definitionId}' non ricostruibile: battaglia non ripristinabile.");
				return false;
			}
			ApplyPawnSnapshot(card, pawn);
		}
		return true;
	}

	private CampaignCardInstance FindCampaignCard(int instanceId)
	{
		if (instanceId <= 0 || campaignDeck == null)
			return null;
		foreach (CampaignCardInstance instance in campaignDeck.Cards)
		{
			if (instance.InstanceId == instanceId)
				return instance;
		}
		return null;
	}

	private void ApplyPawnSnapshot(BattleCardState card, CampaignBattlePawnSave pawn)
	{
		// Seraphel cambia carta a metà scontro: se la forza salvata non è quella che la
		// definizione produrrebbe, la pedina era trasformata e va rimessa così.
		if (pawn.combatStrength > 0 && card.Card != null && card.Card.Strength != pawn.combatStrength)
			card.TransformSeraphel(card.Definition, pawn.combatStrength);

		card.Initiative = pawn.initiative;
		card.InitiativeTalentBonus = pawn.initiativeTalentBonus;
		card.OpensTheFight = pawn.opensTheFight;
		card.TieBreaker = pawn.tieBreaker;
		card.Eliminated = pawn.eliminated;
		card.AbilityArmed = pawn.abilityArmed;
		card.AbilityUsed = pawn.abilityUsed;
		card.AbilityUsedThisTurn = pawn.abilityUsedThisTurn;
		card.SupremeUsedThisTurn = pawn.supremeUsedThisTurn;
		card.PendingAttackBonus = pawn.pendingAttackBonus;
		card.PendingAttackBonusKind = (PendingAttackBonusKind)pawn.pendingAttackBonusKind;
		card.PermanentCombatBonus = pawn.permanentCombatBonus;
		card.MightAuraCombatBonus = pawn.mightAuraCombatBonus;
		card.InhibitedTurns = pawn.inhibitedTurns;
		card.WasInhibited = pawn.wasInhibited;
		card.PendingVigorStepPenalty = pawn.pendingVigorStepPenalty;
		card.IsSpirit = pawn.isSpirit;
		card.RevivedRound = pawn.revivedRound;
		card.IsAttachment = pawn.isAttachment;
		card.HasEquipment = pawn.hasEquipment;
		card.IsUntargetable = pawn.isUntargetable;
		card.NecromancerMinions = pawn.necromancerMinions;
		card.Petrified = pawn.petrified;
		card.SeraphelSeals = pawn.seraphelSeals;
		card.View.SetInitiative(card.Initiative);
	}

	private void RestorePawnLinks(List<CampaignBattlePawnSave> saved, List<BattleCardState> cards)
	{
		for (int index = 0; index < saved.Count && index < cards.Count; index++)
		{
			CampaignBattlePawnSave pawn = saved[index];
			BattleCardState card = cards[index];
			card.MarkedTarget = DecodePawn(pawn.markedTarget);
			card.HunterMarkedTargets.Clear();
			if (pawn.hunterMarkedTargets != null)
			{
				foreach (int encodedTarget in pawn.hunterMarkedTargets)
				{
					BattleCardState target = DecodePawn(encodedTarget);
					if (target != null)
						card.HunterMarkedTargets.Add(target);
				}
			}
			if (card.Card.HeroClass == HeroClass.Hunter && card.HunterMarkedTargets.Count == 0 && card.MarkedTarget != null)
			{
				// Compatibilita' con i salvataggi creati prima del supporto a piu' marchi.
				card.HunterMarkedTargets.Add(card.MarkedTarget);
				card.MarkedTarget = null;
			}
			card.ProtectedAlly = DecodePawn(pawn.protectedAlly);
			card.AttachedTo = DecodePawn(pawn.attachedTo);
		}
	}

	private BattleCardState DecodePawn(int encoded)
	{
		if (encoded == CampaignBattlePawnSave.NoPawn)
			return null;
		List<BattleCardState> side = CampaignBattleSave.DecodeBelongsToPlayer(encoded) ? playerCards : cpuCards;
		int index = CampaignBattleSave.DecodeIndex(encoded);
		return index >= 0 && index < side.Count ? side[index] : null;
	}

	private void RestoreInitialFormations(CampaignBattleSave battle)
	{
		initialPlayerFormation.Clear();
		initialPlayerCampaignFormation.Clear();
		initialCpuFormation.Clear();
		survivingCpuFormation.Clear();

		for (int index = 0; index < battle.initialPlayerFormation.Count; index++)
		{
			CardDefinition definition = cardDatabase.FindById(battle.initialPlayerFormation[index]);
			if ((Object)(object)definition == (Object)null)
				continue;
			initialPlayerFormation.Add(definition);
			int instanceId = index < battle.initialPlayerCampaignInstances.Count
				? battle.initialPlayerCampaignInstances[index]
				: 0;
			CampaignCardInstance instance = FindCampaignCard(instanceId);
			if (instance != null)
				initialPlayerCampaignFormation.Add(instance);
		}

		foreach (string definitionId in battle.initialCpuFormation)
		{
			CardDefinition definition = cardDatabase.FindById(definitionId);
			if ((Object)(object)definition != (Object)null)
				initialCpuFormation.Add(definition);
		}
	}

	private void RestoreBoss(CampaignBattleBossSave boss)
	{
		activeComposableGolem = null;
		activeMedusaBoss = null;
		activeSeraphelBoss = null;
		activeTrentorBoss = null;
		activeBragusBoss = null;
		activePalatirBoss = null;
		if (boss == null || string.IsNullOrEmpty(boss.kind))
			return;

		switch (boss.kind)
		{
			case CampaignBattleBossSave.Golem:
			{
				var forms = new List<ComposableGolemFormStats>();
				foreach (CampaignGolemFormSave form in boss.forms)
				{
					forms.Add(new ComposableGolemFormStats(
						(ComposableGolemForm)form.form,
						Mathf.Max(1, form.basePower),
						Mathf.Max(2, form.vigorDieSides),
						Mathf.Max(0, form.powerBonus)));
				}
				if (forms.Count == 0)
					return;
				activeComposableGolem = new ComposableGolem(
					random,
					Mathf.Max(1, boss.maxHitPoints),
					Mathf.Clamp(boss.hitPoints, 0, Mathf.Max(1, boss.maxHitPoints)),
					Mathf.Max(1, boss.roundsPerForm),
					forms);
				activeComposableGolem.Restore(
					boss.activeForm,
					boss.roundsInActiveForm,
					boss.hasInitiative ? boss.initiative : (int?)null);
				break;
			}
			case CampaignBattleBossSave.Medusa:
				activeMedusaBoss = new MedusaBoss(random, Mathf.Max(1, boss.maxHitPoints));
				activeMedusaBoss.Restore(boss.hitPoints);
				break;
			case CampaignBattleBossSave.Seraphel:
				activeSeraphelBoss = new SeraphelBoss(random, Mathf.Max(2, boss.maxHitPoints));
				activeSeraphelBoss.Restore(boss.hitPoints, boss.phaseTwo);
				break;
			case CampaignBattleBossSave.Trentor:
				activeTrentorBoss = new TrentorBoss(random, Mathf.Max(1, boss.maxHitPoints));
				activeTrentorBoss.Restore(boss.hitPoints, boss.turnsTaken);
				break;
			case CampaignBattleBossSave.Bragus:
				activeBragusBoss = new BragusBoss(random, Mathf.Max(1, boss.maxHitPoints));
				activeBragusBoss.Restore(boss.hitPoints);
				break;
			case CampaignBattleBossSave.Palatir:
				activePalatirBoss = new PalatirBoss(random, Mathf.Max(1, boss.maxHitPoints));
				activePalatirBoss.Restore(boss.hitPoints);
				break;
		}
	}

	private void RestoreBattleMana(CampaignBattleSave battle)
	{
		// Prima il tetto dei talenti, poi i valori: ripristinare su una riserva col tetto
		// di base taglierebbe il mana di chi ha la Riserva.
		RebuildPlayerManaPool();
		campaignPlayerMana.Restore(battle.playerMana.current, ReadSupremeUses(battle.playerMana));
		campaignCpuMana.Restore(battle.cpuMana.current, ReadSupremeUses(battle.cpuMana));

		campaignManaEliminations.Clear();
		foreach (int encoded in battle.manaEliminations)
		{
			BattleCardState card = DecodePawn(encoded);
			if (card != null)
				campaignManaEliminations.Add(card);
		}

		campaignPaidPrimaryAbilities.Clear();
		foreach (int encoded in battle.paidPrimaryAbilities)
		{
			BattleCardState card = DecodePawn(encoded);
			if (card != null)
				campaignPaidPrimaryAbilities.Add(card);
		}
	}

	private static IEnumerable<KeyValuePair<HeroClass, int>> ReadSupremeUses(CampaignManaSave mana)
	{
		foreach (CampaignSupremeUseSave used in mana.supremeUses)
		{
			if (System.Enum.TryParse(used.heroClass, out HeroClass heroClass))
				yield return new KeyValuePair<HeroClass, int>(heroClass, used.uses);
		}
	}

	/// <summary>
	/// Rimette in scena quello che il modello si è appena ricordato. Niente animazioni:
	/// chi riprende deve trovare il campo già com'era, non vederlo ricomporsi.
	/// </summary>
	private void RestoreBattleScene()
	{
		// Lo scenario: senza, la battaglia riaprirebbe sul fondale di un altro capitolo
		// (il controller e' persistente e si porta dietro l'ultimo che ha visto).
		if ((Object)(object)scenarioCatalog == (Object)null)
			scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		ScenarioDefinition scenario = (Object)(object)scenarioCatalog != (Object)null
			&& !string.IsNullOrWhiteSpace(campaignScenarioId)
			? scenarioCatalog.FindById(campaignScenarioId)
			: null;
		if ((Object)(object)scenario != (Object)null)
			ApplyScenario(scenario);
		else
			RefreshScenarioBackground();

		SetCombatChromeVisible(visible: true);
		ApplyPlayerAuraVisuals(appendLog: false);
		ApplyCpuAuraVisuals(appendLog: false);
		ApplyResponsiveLayout();
		RestoreBattlefieldCardVisibility();

		foreach (BattleCardState card in playerCards)
			RestorePawnVisuals(card);
		foreach (BattleCardState card in cpuCards)
			RestorePawnVisuals(card);

		RefreshCombatPawnCarousel(animate: false);
		RefreshInitiativeDisplay();
		RefreshCampaignManaPresentation();
		UpdateInteractions();
	}

	private void RestorePawnVisuals(BattleCardState card)
	{
		if ((Object)(object)card.View == (Object)null)
			return;
		card.View.SetSelected(selected: false);
		card.View.SetTurnAura(active: false, playerOwned: card.BelongsToPlayer);
		if (card.Eliminated)
		{
			// La morte è già avvenuta e non va rigiocata: la vista ha un percorso
			// apposta per rimettere il residuo visivo senza animazione.
			card.View.RestoreDefeatedState();
		}
		RestoreBossHealthBar(card);
		RefreshPersistentStatus(card);
	}

	/// <summary>
	/// La barra della vita di un boss vive nella sua pedina, non nel boss: va riscritta
	/// a mano, o il giocatore ritrova un boss pieno di HP che al primo colpo salta al
	/// valore vero.
	/// </summary>
	private void RestoreBossHealthBar(BattleCardState card)
	{
		if (IsComposableGolemProxy(card))
			UpdateComposableGolemHealthBar(card);
		else if (IsMedusaBossProxy(card))
			UpdateMedusaBossHealthBar(card);
		else if (IsTrentorBossProxy(card))
			UpdateTrentorBossHealthBar(card);
		else if (IsBragusBossProxy(card))
			UpdateBragusBossHealthBar(card);
		else if (IsPalatirBossProxy(card))
			UpdatePalatirBossHealthBar(card);
	}
}
}
