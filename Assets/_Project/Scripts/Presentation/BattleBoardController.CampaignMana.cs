using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using AccardND.Localization;
using UnityEngine;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	// Non e' readonly perche' il talento "Riserva" alza il tetto, e il tetto vive nelle
	// regole: la riserva del giocatore va quindi ricostruita quando la run comincia, cioe'
	// quando i talenti sono noti. Nessuno tiene un riferimento a questo oggetto oltre la
	// singola chiamata, quindi sostituirlo non lascia in giro riserve vecchie.
	private ManaPool campaignPlayerMana = new ManaPool();
	private readonly ManaPool campaignCpuMana = new ManaPool();
	private readonly HashSet<BattleCardState> campaignManaEliminations = new HashSet<BattleCardState>();
	private readonly HashSet<BattleCardState> campaignPaidPrimaryAbilities = new HashSet<BattleCardState>();

	/// <summary>
	/// Se la "Trance" ha ancora la sua abilita' gratuita in questa stanza. E' stato di
	/// stanza e non di run, quindi si riarma da solo a ogni ingresso e non ha bisogno di
	/// finire nel salvataggio: una run ripresa rientra comunque da BeginCampaignRoomMana.
	/// </summary>
	private bool freePrimaryAbilityAvailable;

	private bool CampaignManaEnabled => campaignDeck != null && !pvpPresentationActive;
	private bool BattleManaHudEnabled =>
		(CampaignManaEnabled
			&& currentRoomType != RoomType.Loot
			&& currentRoomType != RoomType.QuickChallenge
			&& currentRoomType != RoomType.Merchant)
		|| (pvpPresentationActive && pvpState != null);
	private int BattlePlayerManaCurrent => pvpPresentationActive && pvpState != null && pvpState.MyIndex >= 0
		? pvpState.Mana[pvpState.MyIndex]
		: CampaignPlayerManaCurrent;
	private int BattleCpuManaCurrent => pvpPresentationActive && pvpState != null
		? pvpState.Mana[OpponentIndex()]
		: CampaignCpuManaCurrent;

	/// <summary>
	/// Boss e miniboss stanno fuori dall'economia del mana: non pagano e non accumulano,
	/// combattono con le regole scritte per loro. Il giocatore continua pero' a guadagnare
	/// mana uccidendoli e a riceverne quando perde una pedina contro di loro: quello e'
	/// mana della sua riserva, non della loro.
	/// </summary>
	private bool IsCampaignManaExempt(BattleCardState card)
	{
		return card != null
			&& (IsComposableGolemProxy(card)
				|| IsMedusaBossProxy(card)
				|| IsTrentorBossProxy(card)
				|| IsBragusBossProxy(card)
				|| IsPalatirBossProxy(card));
	}

	private int CampaignPlayerManaCurrent => campaignPlayerMana.Current;

	private int CampaignCpuManaCurrent => campaignCpuMana.Current;

	private int CampaignManaMaximum => campaignPlayerMana.Rules.Maximum;

	/// <summary>
	/// Rifa' la riserva del giocatore con il tetto che i talenti gli danno. Va chiamata dove
	/// la run comincia o riprende: prima di quel momento il pacchetto dei talenti puo' non
	/// essere ancora arrivato, e la riserva nascerebbe con il tetto di base.
	///
	/// La riserva della CPU non si tocca: i talenti sono del giocatore.
	/// </summary>
	private void RebuildPlayerManaPool()
	{
		int maximum = AccardND.GameData.TalentRunModifiers.MaximumMana(
			ManaRules.CreateDefault().Maximum, ActiveTalents);
		campaignPlayerMana = new ManaPool(ManaRules.CreateDefault().WithMaximum(maximum));
	}

	private void ResetCampaignManaForNewRun()
	{
		RebuildPlayerManaPool();
		campaignPlayerMana.StartRun();
		if (bossDebugSceneSession)
		{
			campaignPlayerMana.Restore(10);
		}
		campaignCpuMana.StartRun();
		campaignManaEliminations.Clear();
		campaignPaidPrimaryAbilities.Clear();
		RefreshCampaignManaPresentation();
	}

	private void RestoreCampaignMana(int value)
	{
		// Prima il tetto, poi il valore: ripristinare su una riserva col tetto vecchio
		// taglierebbe a 10 il mana di chi ha la Riserva e aveva salvato a 12.
		RebuildPlayerManaPool();
		campaignPlayerMana.Restore(value);
		campaignCpuMana.StartRun();
		campaignManaEliminations.Clear();
		campaignPaidPrimaryAbilities.Clear();
		RefreshCampaignManaPresentation();
	}

	private void BeginCampaignRoomMana()
	{
		if (!CampaignManaEnabled)
		{
			return;
		}

		if (bossDebugSceneSession)
		{
			// BossDebug e' un banco prova: ogni avvio del combattimento deve avere
			// la riserva piena, senza modificare le regole della campagna reale.
			campaignPlayerMana.Restore(10);
		}
		else if (adventureScriptedTutorialActive)
		{
			campaignPlayerMana.Restore(10);
		}

		int before = campaignPlayerMana.Current;
		campaignPlayerMana.StartRound();

		// "Concentrazione": il recupero del cambio stanza. Passa da Gain, quindi la riserva
		// lo taglia comunque al proprio tetto - il talento accelera il recupero, non alza il
		// massimo, e i due numeri restano quelli che la barra sa mostrare.
		int roomChangeMana = AccardND.GameData.TalentRunModifiers.RoomChangeMana(ActiveTalents);
		if (roomChangeMana > 0)
		{
			int gained = campaignPlayerMana.Gain(roomChangeMana);
			if (gained > 0)
				AppendLog($"CONCENTRAZIONE - nuova stanza, +{gained} mana.");
		}

		campaignCpuMana.StartRun();
		campaignCpuMana.Restore(RoomDifficultyRules.For(pendingRoomDifficulty).CpuStartingMana);
		campaignManaEliminations.Clear();
		campaignPaidPrimaryAbilities.Clear();
		freePrimaryAbilityAvailable =
			AccardND.GameData.TalentRunModifiers.FirstAbilityFreeEachRoom(ActiveTalents);

		if (campaignPlayerMana.Current > before)
		{
			AppendLog($"MANA - riserva riportata al minimo di stanza: {campaignPlayerMana.Current}/{CampaignManaMaximum}.");
		}
		else
		{
			AppendLog($"MANA - inizio stanza con {campaignPlayerMana.Current}/{CampaignManaMaximum}.");
		}
		RefreshCampaignManaPresentation();
	}

	private void BeginCampaignManaRound()
	{
		if (!CampaignManaEnabled)
		{
			return;
		}

		int playerBefore = campaignPlayerMana.Current;
		int cpuBefore = campaignCpuMana.Current;
		campaignPlayerMana.StartRound();
		if (RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesMana)
		{
			campaignCpuMana.StartRound();
		}
		else
		{
			campaignCpuMana.StartRun();
		}
		if (campaignPlayerMana.Current != playerBefore || campaignCpuMana.Current != cpuBefore)
		{
			// Ogni riserva col proprio tetto: con la "Riserva" il giocatore arriva a 12 e la
			// CPU resta a 10, e stampare il tetto del giocatore per entrambi direbbe il falso.
			AppendLog($"MANA - nuovo round: tu {campaignPlayerMana.Current}/{CampaignManaMaximum}, CPU {campaignCpuMana.Current}/{campaignCpuMana.Rules.Maximum}.");
		}
		RefreshCampaignManaPresentation();
	}

	private void FinishCampaignManaActivation(BattleCardState card, bool skipped)
	{
		if (!CampaignManaEnabled || card == null || card.IsSpirit || card.IsAttachment || IsCampaignManaExempt(card))
		{
			return;
		}
		if (!card.BelongsToPlayer && !RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesMana)
		{
			return;
		}

		int amount = ManaActionPolicy.ActivationReward(
			CampaignManaFor(card).Rules,
			skipped,
			card.AbilityUsedThisTurn);
		if (IsJurinashorBossProxy(card) && activeJurinashorBoss?.IsPhaseTwo == true)
		{
			amount *= 2;
		}
		if (amount <= 0)
		{
			AppendLog($"MANA - {card.Card.Name} salta dopo aver agito: nessun recupero.");
			return;
		}
		GainCampaignMana(card, amount, skipped ? "salto" : "fine attivazione");
	}

	/// <summary>
	/// Sopra questa probabilita' di eliminazione la CPU colpisce e basta: mettere via mana
	/// vale meno di un bersaglio tolto dal campo adesso.
	/// </summary>
	private const double CpuSkipKillProbabilityCeiling = 0.6;

	/// <summary>
	/// La CPU rinuncia all'attacco quando lo skip da +3 le fa raggiungere una mossa
	/// attualmente inaccessibile - in Diabolica una Suprema, altrove l'abilita' primaria
	/// della pedina - e solo se in questo turno non ha gia' un colpo che vale la pena.
	/// </summary>
	private bool ShouldCpuSkipToSaveMana(BattleCardState card, out string objective)
	{
		objective = string.Empty;
		// Il mana di Jurinashor alimenta le spade, non i suoi attacchi. Se entrasse
		// nella politica di risparmio standard, lo skip lo porterebbe alla soglia 3,
		// l'evocazione consumerebbe subito il mana e il boss salterebbe per sempre.
		if (card != null && IsJurinashorBossProxy(card))
		{
			return false;
		}
		if (!CampaignManaEnabled || card == null || card.BelongsToPlayer
			|| card.AbilityUsedThisTurn || card.AbilityArmed || IsCampaignManaExempt(card))
		{
			return false;
		}

		RoomDifficultyRules difficulty = RoomDifficultyRules.For(pendingRoomDifficulty);
		if (!difficulty.CpuCanSkip || !difficulty.CpuUsesMana)
			return false;

		ManaPool pool = CampaignManaFor(card);
		int manaAfterSkip = Mathf.Min(pool.Rules.Maximum, pool.Current + pool.Rules.GainOnSkip);
		if (difficulty.CpuUsesSupremes
			&& CardRulesGlossary.HasSupreme(card.Card.HeroClass)
			&& AbilityManaCosts.IsSupremeImplemented(card.Card.HeroClass))
		{
			int supremeCost = pool.CostOfSupreme(card.Card.HeroClass);
			if (pool.Current < supremeCost && manaAfterSkip >= supremeCost)
			{
				objective = $"la Suprema di {card.Card.Name}";
			}
		}

		if (objective.Length == 0)
		{
			if (!difficulty.CpuUsesAbilities
				|| !ManaActionPolicy.HasActivatablePrimary(card.Card.HeroClass)
				|| !ClassAbilitiesEnabled(card))
			{
				return false;
			}

			int primaryCost = pool.CostOfPrimary(card.Card.HeroClass);
			if (pool.Current >= primaryCost || manaAfterSkip < primaryCost)
				return false;

			objective = $"l'abilita di {card.Card.Name}";
		}

		double bestKill = BestCpuKillProbability(card);
		if (bestKill >= CpuSkipKillProbabilityCeiling)
		{
			AppendLog($"MANA - {card.Card.Name} rinuncia a conservare mana: ha un colpo al {bestKill:P0}.");
			objective = string.Empty;
			return false;
		}

		return true;
	}

	private bool IsCampaignPrimaryAffordable(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return true;
		}
		ManaPool pool = CampaignManaFor(card);
		return pool.CanAfford(pool.CostOfPrimary(card.Card.HeroClass));
	}

	/// <summary>
	/// La CPU usa le abilita' come apertura della propria attivazione e poi deve ancora
	/// attaccare. Valutare soltanto il costo dell'abilita' le permetterebbe di esaurire
	/// la riserva e completare comunque gratuitamente l'attacco.
	/// </summary>
	private bool IsCampaignPrimaryAndAttackAffordable(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return true;
		}

		ManaPool pool = CampaignManaFor(card);
		int totalCost = pool.CostOfPrimary(card.Card.HeroClass) + pool.Rules.AttackCost;
		return pool.CanAfford(totalCost);
	}

	private bool TrySpendCampaignPrimaryMana(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return true;
		}
		if (campaignPaidPrimaryAbilities.Contains(card))
		{
			return true;
		}

		ManaPool pool = CampaignManaFor(card);
		int cost = pool.CostOfPrimary(card.Card.HeroClass);

		// "Trance": la prima abilita' di classe della stanza non si paga. Si consuma qui e
		// non al calcolo del costo mostrato, cosi' aprire e chiudere il pannello non la
		// brucia; e vale solo per il giocatore, perche' la CPU non ha talenti.
		if (card.BelongsToPlayer && freePrimaryAbilityAvailable && cost > 0)
		{
			freePrimaryAbilityAvailable = false;
			campaignPaidPrimaryAbilities.Add(card);
			AppendLog($"TRANCE - l'abilita di {card.Card.Name} non costa mana in questa stanza.");
			return true;
		}

		if (!pool.CanAfford(cost))
		{
			if (card.BelongsToPlayer)
			{
				// Anche il rifiuto differito (abilita' innescata e pagata piu' tardi)
				// deve parlare dalla pedina, non solo dalla barra dei messaggi.
				ShowNoManaCallout(card);
				SetMessage(GameText.Format(GameTextKeys.Campaign.ManaInsufficient, cost, pool.Current));
			}
			return false;
		}

		if (cost > 0)
		{
			pool.Spend(cost);
			AppendLog($"MANA - {(card.BelongsToPlayer ? "tu" : "CPU")} spendi {cost} per l'abilita di {card.Card.Name}: {pool.Current}/{pool.Rules.Maximum}.");
			if (card.BelongsToPlayer)
			{
				PlayManaDeltaCallout(-cost);
			}
			else
			{
				PlayEnemyManaDeltaCallout(-cost);
			}
			RefreshCampaignManaPresentation();
		}
		campaignPaidPrimaryAbilities.Add(card);
		return true;
	}

	// --- Badge di costo sui bottoni d'azione ---

	/// <summary>
	/// Variazione di mana da mostrare sopra il bottone, o null se il badge non ha senso
	/// (fuori campagna, boss esenti). I valori escono dal pool, quindi includono la
	/// ripetizione di classe della suprema e coincidono con quello che verrebbe
	/// addebitato premendo davvero.
	/// </summary>
	private int? AttackManaBadge(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return null;
		}
		int cost = CampaignManaFor(card).Rules.AttackCost;
		return cost > 0 ? -cost : (int?)null;
	}

	private int? PrimaryManaBadge(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return null;
		}
		int cost = CampaignManaFor(card).CostOfPrimary(card.Card.HeroClass);
		return cost > 0 ? -cost : (int?)null;
	}

	private int? SupremeManaBadge(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return null;
		}
		int cost = CampaignSupremeCost(card);
		return cost > 0 ? -cost : (int?)null;
	}

	/// <summary>
	/// Lo skip rende +3 mana, ma zero se hai gia' agito in questa attivazione:
	/// il badge deve dire la verita' su quel caso, non il valore da manuale.
	/// </summary>
	private int? SkipManaBadge(BattleCardState card)
	{
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return null;
		}
		return ManaActionPolicy.ActivationReward(
			CampaignManaFor(card).Rules,
			skipped: true,
			usedAbilityBeforeSkip: card.AbilityUsedThisTurn);
	}

	// --- Attacco base ---

	private bool IsCampaignAttackAffordable(BattleCardState card)
	{
		if (card != null && IsJurinashorBossProxy(card))
		{
			return true;
		}
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return true;
		}
		if (!card.BelongsToPlayer && !RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesMana)
		{
			return true;
		}
		ManaPool pool = CampaignManaFor(card);
		return pool.CanAfford(pool.Rules.AttackCost);
	}

	/// <summary>
	/// Paga l'attacco base. Restituisce false se la riserva non basta: in quel caso
	/// il chiamante annulla l'azione e mostra il rifiuto sulla pedina.
	/// </summary>
	private bool TrySpendCampaignAttackMana(BattleCardState card)
	{
		if (card != null && IsJurinashorBossProxy(card))
		{
			return true;
		}
		if (!CampaignManaEnabled || card == null || IsCampaignManaExempt(card))
		{
			return true;
		}
		if (!card.BelongsToPlayer && !RoomDifficultyRules.For(pendingRoomDifficulty).CpuUsesMana)
		{
			return true;
		}

		ManaPool pool = CampaignManaFor(card);
		int cost = pool.Rules.AttackCost;
		if (cost <= 0)
		{
			return true;
		}
		if (!pool.CanAfford(cost))
		{
			return false;
		}

		pool.Spend(cost);
		AppendLog($"MANA - {(card.BelongsToPlayer ? "tu" : "CPU")} spendi {cost} per l'attacco di {card.Card.Name}: {pool.Current}/{pool.Rules.Maximum}.");
		if (card.BelongsToPlayer)
		{
			PlayManaDeltaCallout(-cost);
		}
		else
		{
			PlayEnemyManaDeltaCallout(-cost);
		}
		RefreshCampaignManaPresentation();
		return true;
	}

	private bool TryPayForSelectedCampaignAttack(BattleCardState card, bool primaryAbilityAttack)
	{
		bool paid = primaryAbilityAttack
			? TrySpendCampaignPrimaryMana(card)
			: TrySpendCampaignAttackMana(card);
		if (paid)
			return true;

		ShowNoManaCallout(card);
		if (primaryAbilityAttack)
			card.AbilityArmed = false;
		attackTargetingActive = false;
		inputLocked = false;
		UpdateInteractions();
		return false;
	}

	/// <summary>
	/// Rifiuto visibile: i bottoni restano cliccabili, ma la pedina dice perche'
	/// non succede niente invece di lasciare il giocatore davanti a un tasto muto.
	/// </summary>
	private void ShowNoManaCallout(BattleCardState card)
	{
		if (card?.View == null)
		{
			return;
		}
		// Lo swipe/click dell'azione puo inoltrare il rilascio alla carta. Quando
		// l'azione viene rifiutata, impedisce che lo stesso input apra l'ispezione.
		suppressCardInspectionUntilFrame = Time.frameCount + 1;
		card.View.PlayActionCallout("NO MANA", NoManaCalloutColor);
		// Suono e scritta partono insieme: e' l'unico punto da cui passano tutti i
		// rifiuti per mana, quindi non serve ripeterlo su ogni chiamante.
		PlaySfx(noManaSfx);
	}

	// Stesso blu del callout "ABILITA'" in PrototypeCardView: il rifiuto deve sembrare
	// parte della stessa fazione di etichette, non un elemento estraneo.
	private static readonly Color NoManaCalloutColor = new Color(0.05f, 0.28f, 0.76f);

	private void ResetCampaignPrimaryManaPayment(BattleCardState card)
	{
		if (card != null)
		{
			campaignPaidPrimaryAbilities.Remove(card);
		}
	}

	private void RegisterCampaignParryMana(BattleCardState defender)
	{
		if (!CampaignManaEnabled || defender == null || defender.IsSpirit || defender.IsAttachment || IsCampaignManaExempt(defender))
		{
			return;
		}

		int amount = CampaignManaFor(defender).Rules.GainOnParry;
		if (IsJurinashorBossProxy(defender) && activeJurinashorBoss?.IsPhaseTwo == true)
		{
			amount *= 2;
		}
		GainCampaignMana(defender, amount, "parata");
	}

	private void RegisterCampaignEliminationMana(BattleCardState killer, BattleCardState victim)
	{
		if (!CampaignManaEnabled || victim == null || victim.IsAttachment || !campaignManaEliminations.Add(victim))
		{
			return;
		}

		if (killer != null && !killer.IsSpirit && !killer.IsAttachment)
		{
			GainCampaignMana(killer, CampaignManaFor(killer).Rules.GainOnKill, "eliminazione");
			if (IsJurinashorBossDefinition(killer.Definition))
			{
				TrySummonJurinashorSwordOnKill(killer);
			}
		}
		if (!victim.IsSpirit)
		{
			GainCampaignMana(victim, CampaignManaFor(victim).Rules.GainOnLoss, "perdita pedina");
		}
		if (IsJurinashorSword(victim))
		{
			RefreshJurinashorSwordBonusPresentation();
		}
	}

	private void GainCampaignMana(BattleCardState ownerCard, int amount, string reason)
	{
		ManaPool pool = CampaignManaFor(ownerCard);
		int gained = pool.Gain(amount);
		if (gained <= 0)
		{
			return;
		}

		AppendLog($"MANA - {(ownerCard.BelongsToPlayer ? "tu" : "CPU")} +{gained} ({reason}): {pool.Current}/{pool.Rules.Maximum}.");
		if (ownerCard.BelongsToPlayer)
		{
			PlayManaDeltaCallout(gained);
		}
		else
		{
			PlayEnemyManaDeltaCallout(gained);
		}
		BattleCardState activeJurinashor = !ownerCard.BelongsToPlayer
			? cpuCards.FirstOrDefault(card => card != null && !card.Eliminated && IsJurinashorBossDefinition(card.Definition))
			: null;
		if (activeJurinashor != null)
		{
			TrySummonJurinashorSwords(activeJurinashor, pool);
		}
		if (!ownerCard.BelongsToPlayer
			&& IsSeraphelBossProxy(ownerCard)
			&& activeSeraphelBoss != null
			&& pool.Current >= SeraphelBoss.ManaHealingThreshold)
		{
			pool.Spend(SeraphelBoss.ManaHealingThreshold);
			int healed = activeSeraphelBoss.Heal(activeSeraphelBoss.ManaHealingAmount);
			PlayEnemyManaDeltaCallout(-SeraphelBoss.ManaHealingThreshold);
			RefreshSeraphelBossPawn(ownerCard);
			PlaySeraphelHealSfx();
			ownerCard.View?.PlayActionCallout($"RIGENERAZIONE +{healed}", Color.white);
			((MonoBehaviour)this).StartCoroutine(PlaySeraphelRegenerationVfx());
			SetMessage(GameText.Format(GameTextKeys.Campaign.SeraphelManaRegeneration, healed, activeSeraphelBoss.HitPoints, activeSeraphelBoss.MaxHitPoints));
			AppendLog(GameText.Format(GameTextKeys.Campaign.SeraphelManaRegenerationLog, healed, activeSeraphelBoss.HitPoints, activeSeraphelBoss.MaxHitPoints));
		}
		RefreshCampaignManaPresentation();
	}

	private ManaPool CampaignManaFor(BattleCardState card)
	{
		return card.BelongsToPlayer ? campaignPlayerMana : campaignCpuMana;
	}

	private void RefreshCampaignManaPresentation()
	{
		RefreshPlayerHud();
		RefreshCpuHud();

		// Il costo crescente della Suprema appartiene alla riserva della fazione ed e'
		// condiviso per classe. Quando cambia, aggiorna quindi tutte le pedine: una
		// inspection ricalcola gia' il dato al volo e non deve risultare piu' aggiornata
		// dei badge visibili sul campo.
		foreach (BattleCardState card in playerCards)
		{
			if (card?.View != null)
				RefreshPersistentStatus(card);
		}
		foreach (BattleCardState card in cpuCards)
		{
			if (card?.View != null)
				RefreshPersistentStatus(card);
		}
	}
}
}
