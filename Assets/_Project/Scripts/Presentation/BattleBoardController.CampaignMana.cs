using System.Collections.Generic;
using AccardND.GameCore.Mana;
using AccardND.GameData;
using UnityEngine;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController
{
	private readonly ManaPool campaignPlayerMana = new ManaPool();
	private readonly ManaPool campaignCpuMana = new ManaPool();
	private readonly HashSet<BattleCardState> campaignManaEliminations = new HashSet<BattleCardState>();
	private readonly HashSet<BattleCardState> campaignPaidPrimaryAbilities = new HashSet<BattleCardState>();

	private bool CampaignManaEnabled => campaignDeck != null && !pvpPresentationActive;
	private bool BattleManaHudEnabled =>
		(CampaignManaEnabled
			&& currentRoomType != RoomType.Loot
			&& currentRoomType != RoomType.UnexpectedOpportunity
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

	private void ResetCampaignManaForNewRun()
	{
		campaignPlayerMana.StartRun();
		campaignCpuMana.StartRun();
		campaignManaEliminations.Clear();
		campaignPaidPrimaryAbilities.Clear();
		RefreshCampaignManaPresentation();
	}

	private void RestoreCampaignMana(int value)
	{
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

		if (adventureScriptedTutorialActive)
		{
			campaignPlayerMana.Restore(10);
		}

		int before = campaignPlayerMana.Current;
		campaignPlayerMana.StartRound();
		campaignCpuMana.StartRun();
		campaignManaEliminations.Clear();
		campaignPaidPrimaryAbilities.Clear();

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
			AppendLog($"MANA - nuovo round: tu {campaignPlayerMana.Current}/{CampaignManaMaximum}, CPU {campaignCpuMana.Current}/{CampaignManaMaximum}.");
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
		if (amount <= 0)
		{
			AppendLog($"MANA - {card.Card.Name} salta dopo aver agito: nessun recupero.");
			return;
		}
		GainCampaignMana(card, amount, skipped ? "salto" : "fine attivazione");
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
		if (!pool.CanAfford(cost))
		{
			if (card.BelongsToPlayer)
			{
				// Anche il rifiuto differito (abilita' innescata e pagata piu' tardi)
				// deve parlare dalla pedina, non solo dalla barra dei messaggi.
				ShowNoManaCallout(card);
				SetMessage($"Mana insufficiente: servono {cost}, disponibili {pool.Current}.");
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
		card.View.PlayActionCallout("NO MANA", NoManaCalloutColor);
		// Suono e scritta partono insieme: e' l'unico punto da cui passano tutti i
		// rifiuti per mana, quindi non serve ripeterlo su ogni chiamante.
		PlaySfx(noManaSfx);
	}

	// Stesso blu del callout "ABILITA'" in PrototypeCardView: il rifiuto deve sembrare
	// parte della stessa famiglia di etichette, non un elemento estraneo.
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

		GainCampaignMana(defender, CampaignManaFor(defender).Rules.GainOnParry, "parata");
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
		}
		if (!victim.IsSpirit)
		{
			GainCampaignMana(victim, CampaignManaFor(victim).Rules.GainOnLoss, "perdita pedina");
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
	}
}
}
