using System;
using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.GameCore.Pvp;
using AccardND.Localization;

namespace AccardND.NetProtocol
{
    public enum PvpClientPhase
    {
        Waiting,
        Deployment,
        Battle,
        DecisiveSelection,
        Finished
    }

    /// <summary>Stato di una carta schierata come lo vede il client.</summary>
    public sealed class PvpClientCard
    {
        public int Slot;
        public string CardId;
        public string CardName;
        public HeroClass HeroClass;
        public int Strength;
        public int Lives;
        public int Initiative;
        public bool Eliminated;
        public bool IsSpirit;
        public bool Inhibited;
        public bool Marked;
        public bool Protecting;
        public bool AbilityUsed;
        public bool AbilityUsedThisTurn;
        public bool AbilityArmed;
        public int PermanentBonus;
        public int MightAuraBonus;
        public int PendingBonus;
        public PvpPendingBonusKind PendingBonusKind;
        public int DiePenaltySteps;

        /// <summary>La suprema e' gia' stata usata da questa pedina nel round corrente.</summary>
        public bool SupremeUsedThisRound;

        /// <summary>Invisibilita' dell'Assassino: non selezionabile come bersaglio.</summary>
        public bool Untargetable;

        public int NecromancerMinions;
    }

    public sealed class PvpClientDeploymentToken
    {
        public int Order;
        public int Player;
        public int Initiative;
    }

    /// <summary>
    /// Ricostruisce lo stato del match dagli eventi del server. La UI legge da
    /// qui e non contiene logica di gioco: qualunque discrepanza col server è
    /// un bug di questo replay, non una decisione del client.
    /// </summary>
    public sealed class PvpClientMatchState
    {
        private readonly List<string> log = new();

        public int MyIndex { get; private set; } = -1;
        public string OpponentName { get; private set; } = string.Empty;
        public PvpClientPhase Phase { get; private set; } = PvpClientPhase.Waiting;
        public int MatchRound { get; private set; }
        public int VigorDieSides { get; private set; }
        public int Cycle { get; private set; }
        public int DeployPlayer { get; private set; } = -1;
        public int ActivePlayer { get; private set; } = -1;
        public int ActiveSlot { get; private set; } = -1;
        public int Winner { get; private set; } = -1;
        public bool EndedByForfeit { get; private set; }
        public int DecisiveRequiredCount { get; private set; }
        public IReadOnlyList<string> Log => log;

        public List<PvpClientCard>[] Boards { get; } = { new(), new() };
        public List<PvpClientDeploymentToken> DeploymentOrder { get; } = new();
        public int[] Wins { get; } = new int[2];

        /// <summary>Riserva di mana dei due giocatori, allineata dagli eventi del server.</summary>
        public int[] Mana { get; } = new int[2];
        private readonly Dictionary<HeroClass, int>[] supremeUsesThisMatch = { new(), new() };
        public PvpAuraType[] Auras { get; } = new PvpAuraType[2];
        public bool[] FormationAuraUsed { get; } = new bool[2];

        /// <summary>Mano corrente come coppie (indice loadout, id definizione).</summary>
        public List<(int LoadoutIndex, string DefinitionId)> Hand { get; } = new();

        public bool IsMyDeployTurn => Phase == PvpClientPhase.Deployment && DeployPlayer == MyIndex;
        public bool IsMyBattleTurn => Phase == PvpClientPhase.Battle && ActivePlayer == MyIndex;
        public bool HasEliminatedFormation => FormationEliminated(0) || FormationEliminated(1);

        public event Action Changed;

        public void ApplyMatchStart(MatchStart start)
        {
            MyIndex = start.yourPlayerIndex;
            OpponentName = start.opponentName ?? string.Empty;
            AddLog(GameText.Format(GameTextKeys.PvpLog.MatchAgainst, OpponentName));
            Changed?.Invoke();
        }

        public void ApplyHand(MatchHand hand)
        {
            Hand.Clear();
            if (hand.handIndices != null && hand.handDefinitionIds != null)
            {
                for (int position = 0; position < hand.handIndices.Length; position++)
                    Hand.Add((hand.handIndices[position], hand.handDefinitionIds[position]));
            }
            Changed?.Invoke();
        }

        public void Apply(MatchEventDto e)
        {
            switch (e.type)
            {
                case "RoundStarted":
                    MatchRound = e.matchRound;
                    VigorDieSides = e.vigorDieSides;
                    Cycle = 1;
                    Boards[0].Clear();
                    Boards[1].Clear();
                    DeploymentOrder.Clear();
                    Auras[0] = PvpAuraType.None;
                    Auras[1] = PvpAuraType.None;
                    FormationAuraUsed[0] = false;
                    FormationAuraUsed[1] = false;
                    Phase = PvpClientPhase.Waiting;
                    ActivePlayer = -1;
                    DeployPlayer = -1;
                    AddLog(GameText.Format(GameTextKeys.PvpLog.RoundStarted, MatchRound, VigorDieSides));
                    break;

                case "DecisiveSelectionStarted":
                    Phase = PvpClientPhase.DecisiveSelection;
                    DecisiveRequiredCount = e.requiredCount;
                    AddLog(GameText.Format(GameTextKeys.PvpLog.DecisiveSelection, e.requiredCount));
                    break;

                case "DeploymentStarted":
                    Phase = PvpClientPhase.Deployment;
                    DeploymentOrder.Clear();
                    AddLog(GameText.Format(GameTextKeys.PvpLog.DeploymentStarted, PlayerName(e.firstPlayer)));
                    break;

                case "DeploymentInitiative":
                    DeploymentOrder.Add(new PvpClientDeploymentToken
                    {
                        Order = e.slot,
                        Player = e.player,
                        Initiative = e.initiative
                    });
                    DeploymentOrder.Sort((left, right) => left.Order.CompareTo(right.Order));
                    break;

                case "DeployTurn":
                    Phase = PvpClientPhase.Deployment;
                    DeployPlayer = e.player;
                    break;

                case "CardDeployed":
                {
                    string localizedCardName = GameText.GetOrFallback(
                        GameTextKeys.Data.CardName(e.cardId),
                        e.cardName);
                    Boards[e.player].Add(new PvpClientCard
                    {
                        Slot = e.slot,
                        CardId = e.cardId,
                        CardName = localizedCardName,
                        HeroClass = (HeroClass)e.heroClass,
                        Strength = e.strength,
                        Lives = e.lives,
                        Initiative = e.initiative
                    });
                    if (e.player == MyIndex)
                        RemoveFromHand(e.cardId);
                    AddLog(GameText.Format(
                        GameTextKeys.PvpLog.CardDeployed,
                        PlayerName(e.player),
                        localizedCardName,
                        e.strength));
                    break;
                }

                case "BattleStarted":
                    Phase = PvpClientPhase.Battle;
                    Auras[0] = (PvpAuraType)e.auraPlayer0;
                    Auras[1] = (PvpAuraType)e.auraPlayer1;
                    FormationAuraUsed[0] = false;
                    FormationAuraUsed[1] = false;
                    if (Auras[0] != PvpAuraType.None || Auras[1] != PvpAuraType.None)
                        AddLog(GameText.Format(GameTextKeys.PvpLog.Auras, Auras[0], Auras[1]));
                    break;

                case "CardInitiative":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                        card.Initiative = e.initiative;
                    break;
                }

                case "TurnStarted":
                    Phase = PvpClientPhase.Battle;
                    ClearAbilityUsedThisTurn();
                    ActivePlayer = e.player;
                    ActiveSlot = e.slot;
                    Cycle = e.cycle;
					PvpClientCard activeCard = CardAt(e.player, e.slot);
					if (activeCard != null && !activeCard.AbilityArmed)
						activeCard.AbilityUsed = false;
                    break;

                case "TurnSkipped":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.Inhibited = false;
                        AddLog(GameText.Format(GameTextKeys.PvpLog.TurnSkipped, card.CardName));
                    }
                    break;
                }

                case "AbilityUsed":
                    ApplyAbility(e);
                    break;

                case "ManaChanged":
                    // Il server manda il valore assoluto: il client si allinea invece di
                    // ricalcolare, cosi' non puo' divergere dall'autorita'.
                    if (e.player >= 0 && e.player < Mana.Length)
                        Mana[e.player] = e.mana;
                    break;

                case "SupremeUsed":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.SupremeUsedThisRound = true;
                        // La suprema conta come azione ai fini del recupero, ma non
                        // consuma l'abilita' primaria: la UI deve continuare a
                        // proporla e il server eseguira' il normale check del mana.
                        card.AbilityUsedThisTurn = true;
                        SupremeAbilityType supreme = (SupremeAbilityType)e.supreme;
                        if (supreme == SupremeAbilityType.Vanish)
                            card.Untargetable = true;
                        else if (supreme == SupremeAbilityType.StealBuffs)
                            ApplyRogueSupremeReplay(card, CardAt(e.targetPlayer, e.targetSlot), e.magnitude);
                    }
                    if (e.player >= 0 && e.player < supremeUsesThisMatch.Length)
                    {
                        HeroClass heroClass = (HeroClass)e.ability;
                        supremeUsesThisMatch[e.player][heroClass] = SupremeUsesThisMatch(e.player, heroClass) + 1;
                    }
                    break;
                }

                case "CardRevived":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.Eliminated = false;
                        card.Lives = e.lives;
                        card.AbilityUsed = false;
                        card.AbilityUsedThisTurn = false;
                        card.AbilityArmed = false;
                        AddLog(GameText.Format(GameTextKeys.PvpLog.CardRevived, card.CardName, e.lives));
                    }
                    break;
                }

                case "NecromancerMinionsChanged":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                        card.NecromancerMinions = e.amount;
                    if (e.bonus > 0 && e.player >= 0 && e.player < Boards.Length)
                    {
                        foreach (PvpClientCard ally in Boards[e.player])
                            if (!ally.Eliminated)
                                ally.PermanentBonus += e.bonus;
                    }
                    break;
                }

                case "ProtectionTriggered":
                {
                    PvpClientCard paladin = CardAt(e.player, e.slot);
                    if (paladin != null)
                    {
                        paladin.Protecting = false;
                        paladin.AbilityArmed = false;
                        paladin.AbilityUsed = true;
                        AddLog(GameText.Format(
                            e.redirected
                                ? GameTextKeys.PvpLog.ProtectionRedirect
                                : GameTextKeys.PvpLog.ProtectionAdvantage,
                            paladin.CardName));
                    }
                    break;
                }

                case "AttackResolved":
                    ApplyAttack(e);
                    break;

                case "AttachmentApplied":
                {
                    PvpClientCard source = CardAt(e.player, e.slot);
                    PvpClientCard target = CardAt(e.player, e.targetSlot);
                    if (source != null)
                    {
                        source.Eliminated = true;
                        source.Lives = 0;
                    }
                    if (target != null)
                        target.PermanentBonus += e.bonus;
                    AddLog(GameText.Format(
                        GameTextKeys.PvpLog.Attachment,
                        source?.CardName,
                        e.bonus,
                        target?.CardName));
                    break;
                }

                case "FuryGained":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.PendingBonus = e.amount;
                        card.PendingBonusKind = PvpPendingBonusKind.Fury;
                        AddLog(GameText.Format(GameTextKeys.PvpLog.Fury, card.CardName, e.amount));
                    }
                    break;
                }

                case "MightAuraBonus":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.PermanentBonus++;
                        card.MightAuraBonus++;
                        AddLog(GameText.Format(GameTextKeys.PvpLog.MightAura, card.CardName));
                    }
                    break;
                }

                case "MageAuraPenalty":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.PermanentBonus -= e.amount;
                        AddLog(GameText.Format(GameTextKeys.PvpLog.MageAura, card.CardName, e.amount));
                    }
                    break;
                }

                case "SpiritExpired":
                {
                    PvpClientCard card = CardAt(e.player, e.slot);
                    if (card != null)
                    {
                        card.IsSpirit = false;
                        card.Eliminated = true;
                        AddLog(GameText.Format(GameTextKeys.PvpLog.SpiritExpired, card.CardName));
                    }
                    break;
                }

                case "ActionTimeout":
                    AddLog(GameText.Format(GameTextKeys.PvpLog.Timeout, PlayerName(e.player), e.amount));
                    break;

                case "MatchForfeited":
                    EndedByForfeit = true;
                    AddLog(GameText.Format(
                        GameTextKeys.PvpLog.Forfeit,
                        PlayerName(e.player),
                        PlayerName(e.winner)));
                    break;

                case "RoundEnded":
                    Wins[0] = e.winsPlayer0;
                    Wins[1] = e.winsPlayer1;
                    AddLog(GameText.Format(
                        GameTextKeys.PvpLog.RoundEnded,
                        e.matchRound,
                        PlayerName(e.winner),
                        Wins[0],
                        Wins[1]));
                    break;

                case "MatchEnded":
                    Wins[0] = e.winsPlayer0;
                    Wins[1] = e.winsPlayer1;
                    Winner = e.winner;
                    Phase = PvpClientPhase.Finished;
                    if (!EndedByForfeit && !HasEliminatedFormation)
                        AddLog(GameText.Get(GameTextKeys.PvpLog.ReplayMismatch));
                    AddLog(EndedByForfeit
                        ? Winner == MyIndex
                            ? GameText.Get(GameTextKeys.PvpLog.WonByForfeit)
                            : GameText.Get(GameTextKeys.PvpLog.LostByForfeit)
                        : Winner == MyIndex
                            ? GameText.Format(GameTextKeys.PvpLog.WonMatch, Wins[MyIndex], Wins[1 - MyIndex])
                            : GameText.Format(GameTextKeys.PvpLog.LostMatch, Wins[MyIndex], Wins[1 - MyIndex]));
                    break;
            }
            Changed?.Invoke();
        }

        private void ApplyAbility(MatchEventDto e)
        {
            PvpClientCard actor = CardAt(e.player, e.slot);
            PvpClientCard target = CardAt(e.targetPlayer, e.targetSlot);
            var ability = (HeroClass)e.ability;
            MarkAbilityState(actor, ability);
            switch (ability)
            {
                case HeroClass.Assassin:
                    if (target != null)
                    {
                        target.Inhibited = true;
                        if (e.magnitude > 0)
                            target.PermanentBonus -= e.magnitude;
                    }
                    AddLog(GameText.Format(GameTextKeys.PvpLog.Assassin, actor?.CardName, target?.CardName));
                    break;
                case HeroClass.Mage:
                    if (target != null)
                        target.DiePenaltySteps = e.magnitude;
                    AddLog(GameText.Format(GameTextKeys.PvpLog.Mage, actor?.CardName, target?.CardName, e.magnitude));
                    break;
                case HeroClass.Hunter:
                    if (target != null)
                        target.Marked = true;
                    AddLog(GameText.Format(GameTextKeys.PvpLog.Hunter, actor?.CardName, target?.CardName, e.magnitude));
                    break;
                case HeroClass.Paladin:
                    if (actor != null)
                        actor.Protecting = true;
                    AddLog(actor == target
                        ? GameText.Format(GameTextKeys.PvpLog.PaladinSelf, actor?.CardName)
                        : GameText.Format(GameTextKeys.PvpLog.PaladinOther, actor?.CardName, target?.CardName));
                    break;
                case HeroClass.Priest:
                    if (target != null)
                    {
                        target.Inhibited = false;
                        target.Marked = false;
                        target.DiePenaltySteps = 0;
                        if (target.PermanentBonus < 0)
                            target.PermanentBonus = 0;
                        target.PendingBonus += e.magnitude;
                        if (target.PendingBonusKind != PvpPendingBonusKind.Fury)
                            target.PendingBonusKind = PvpPendingBonusKind.Blessing;
                    }
                    AddLog(GameText.Format(GameTextKeys.PvpLog.Priest, actor?.CardName, target?.CardName, e.magnitude));
                    break;
                case HeroClass.Warrior:
                    AddLog(GameText.Format(GameTextKeys.PvpLog.Warrior, actor?.CardName));
                    break;
                case HeroClass.Necromancer:
                    AddLog(GameText.Format(GameTextKeys.PvpLog.Necromancer, actor?.CardName, target?.CardName));
                    break;
            }
        }

        private void ApplyAttack(MatchEventDto e)
        {
            PvpClientCard attacker = CardAt(e.player, e.slot);
            PvpClientCard defender = CardAt(e.targetPlayer, e.targetSlot);
            if (attacker != null)
            {
                attacker.PendingBonus = 0;
                attacker.PendingBonusKind = PvpPendingBonusKind.None;
                attacker.DiePenaltySteps = 0;
                if (attacker.AbilityArmed && attacker.HeroClass == HeroClass.Warrior)
                {
                    attacker.AbilityArmed = false;
                    attacker.AbilityUsed = true;
                }
            }
            if (defender != null && !e.interceptedByNecromancerMinion)
            {
                defender.DiePenaltySteps = 0;
                defender.Lives = e.defenderRemainingLives;
                if (e.defenderEliminated)
                {
                    defender.Eliminated = true;
                    defender.IsSpirit = false;
                }
                else if (e.becameSpirit)
                {
                    defender.IsSpirit = true;
                }
            }

            string prefix = e.isCounter ? GameText.Get(GameTextKeys.PvpLog.CounterattackPrefix) : string.Empty;
            string outcome = e.certainty == "Impossible"
                ? GameText.Get(GameTextKeys.PvpLog.Impossible)
                : GameText.Format(
                    GameTextKeys.PvpLog.RollOutcome,
                    e.attackerTotal,
                    e.defenderTotal,
                    FormatCombatantRoll(attacker, e.attackerDieSides, e.attackerRollFirst, e.attackerRollSecond, e.attackerRollHasSecond, e.attackerRollSelected, e.attackerRollSelectionMode),
                    FormatCombatantRoll(defender, e.defenderDieSides, e.defenderRollFirst, e.defenderRollSecond, e.defenderRollHasSecond, e.defenderRollSelected, e.defenderRollSelectionMode));
            string overkillTag = e.overkill ? GameText.Get(GameTextKeys.PvpLog.OverkillTag) : string.Empty;
            string effect = !e.defenderLostLife
                ? defender != null ? GameText.Format(GameTextKeys.PvpLog.Resists, defender.CardName) : string.Empty
                : e.becameSpirit
                    ? GameText.Format(GameTextKeys.PvpLog.BecomesSpirit, overkillTag, defender?.CardName)
                    : e.defenderEliminated
                        ? GameText.Format(GameTextKeys.PvpLog.Eliminated, overkillTag, defender?.CardName)
                        : GameText.Format(GameTextKeys.PvpLog.LosesLife, defender?.CardName, e.defenderRemainingLives);
            AddLog(GameText.Format(
                GameTextKeys.PvpLog.Attack,
                prefix,
                attacker?.CardName,
                defender?.CardName,
                outcome,
                effect));
        }

        private static string FormatCombatantRoll(
            PvpClientCard card,
            int dieSides,
            int firstRoll,
            int secondRoll,
            bool hasSecondRoll,
            int selectedRoll,
            int selectionMode)
        {
            string name = card?.CardName ?? GameText.Get(GameTextKeys.Common.Card);
            string die = dieSides > 0 ? $"D{dieSides}" : "D?";
            if (!hasSecondRoll)
            {
                int roll = selectedRoll > 0 ? selectedRoll : firstRoll;
                return GameText.Format(GameTextKeys.PvpLog.RollSingle, name, die, roll);
            }

            string mode = ((VigorSelectionMode)selectionMode) switch
            {
                VigorSelectionMode.Highest => GameText.Get(GameTextKeys.PvpLog.RollHighest),
                VigorSelectionMode.Lowest => GameText.Get(GameTextKeys.PvpLog.RollLowest),
                VigorSelectionMode.Sum => GameText.Get(GameTextKeys.PvpLog.RollSum),
                _ => GameText.Get(GameTextKeys.PvpLog.RollResult)
            };
            return GameText.Format(
                GameTextKeys.PvpLog.RollDouble,
                name,
                die,
                firstRoll,
                secondRoll,
                mode,
                selectedRoll);
        }

        private static void MarkAbilityState(PvpClientCard actor, HeroClass ability)
        {
            if (actor == null)
                return;

            actor.AbilityUsedThisTurn = true;
            if (ability is HeroClass.Warrior or HeroClass.Rogue or HeroClass.Paladin)
                actor.AbilityArmed = true;
            else
                actor.AbilityUsed = true;
        }

        private void ClearAbilityUsedThisTurn()
        {
            foreach (List<PvpClientCard> board in Boards)
            {
                foreach (PvpClientCard card in board)
                    card.AbilityUsedThisTurn = false;
            }
        }

        /// <summary>
        /// Il server applica lo Scippo prima di emettere SupremeUsed. Il client non riceve
        /// uno snapshot completo dopo ogni azione, quindi deve riprodurre anche il
        /// trasferimento di buff/Potenza; prima questo ramo mancava e sul tavolo PvP le
        /// due pedine restavano visivamente invariate pur essendo corrette sul server.
        /// </summary>
        private static void ApplyRogueSupremeReplay(PvpClientCard rogue, PvpClientCard target, int magnitude)
        {
            if (rogue == null || target == null || magnitude <= 0)
                return;

            if (target.PendingBonus > 0)
            {
                target.PendingBonus = 0;
                target.PendingBonusKind = PvpPendingBonusKind.None;
            }
            else if (target.PermanentBonus > 0)
            {
                target.PermanentBonus = 0;
            }
            else
            {
                target.PermanentBonus -= magnitude;
            }

            rogue.PermanentBonus += magnitude;
        }

        /// <summary>Costo autorevole ricostruito dagli eventi ricevuti nella partita.</summary>
        public int SupremeCostFor(int player, HeroClass heroClass)
        {
            int uses = SupremeUsesThisMatch(player, heroClass);
            return AbilityManaCosts.Supreme(heroClass) + uses * ManaRules.CreateDefault().SupremeRepeatSurcharge;
        }

		/// <summary>Numero autorevole di supreme della classe gia' usate dal giocatore.</summary>
		public int SupremeUsesFor(int player, HeroClass heroClass) =>
			SupremeUsesThisMatch(player, heroClass);

        private int SupremeUsesThisMatch(int player, HeroClass heroClass)
        {
            return player >= 0 && player < supremeUsesThisMatch.Length
                && supremeUsesThisMatch[player].TryGetValue(heroClass, out int uses)
                ? uses
                : 0;
        }

        private void RemoveFromHand(string definitionId)
        {
            for (int position = 0; position < Hand.Count; position++)
            {
                if (Hand[position].DefinitionId == definitionId)
                {
                    Hand.RemoveAt(position);
                    return;
                }
            }
        }

        private PvpClientCard CardAt(int player, int slot)
        {
            if (player is < 0 or > 1)
                return null;
            foreach (PvpClientCard card in Boards[player])
            {
                if (card.Slot == slot)
                    return card;
            }
            return null;
        }

        private bool FormationEliminated(int player)
        {
            if (player is < 0 or > 1 || Boards[player].Count == 0)
                return false;
            foreach (PvpClientCard card in Boards[player])
            {
                if (card != null && !card.Eliminated && card.Lives > 0)
                    return false;
            }
            return true;
        }

        private string PlayerName(int player) =>
            player == MyIndex
                ? GameText.Get(GameTextKeys.Common.You)
                : OpponentName.Length > 0
                    ? OpponentName
                    : GameText.Format(GameTextKeys.PvpLog.UnknownPlayer, player);

        /// <summary>
        /// Avviso di rete nel registro del match (disconnessioni, rientri): non viene
        /// dal motore, ma il giocatore lo legge nello stesso posto.
        /// </summary>
        public void AddNotice(string message)
        {
            AddLog(message);
            Changed?.Invoke();
        }

        private void AddLog(string message)
        {
            log.Add(message);
            if (log.Count > 200)
                log.RemoveAt(0);
        }
    }
}
