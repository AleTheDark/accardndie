using System;
using System.Collections.Generic;
using AccardND.GameCore.Mana;

namespace AccardND.GameCore.Pvp
{
    public enum PvpMatchPhase
    {
        NotStarted,
        DecisiveSelection,
        Deployment,
        Battle,
        Finished
    }

    public sealed class PvpActionException : Exception
    {
        public PvpActionException(string message) : base(message)
        {
        }

        public PvpActionException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; }
    }

    public static class PvpActionErrorCodes
    {
        public const string AbilityRequiresAction = "ability_requires_action";
        public const string NotEnoughMana = "not_enough_mana";
        public const string SupremeNotAvailable = "supreme_not_available";
    }

    /// <summary>
    /// Motore autoritativo del match PvP best-of-3. Replica le regole di
    /// combattimento della campagna (abilità, aure, attachment, spiriti) con
    /// le varianti PvP: 2 vite per carta schierata, dado vigore unico che
    /// scala col round, schieramento alternato dopo il tiro di iniziativa.
    /// Tutta la casualità passa da IRandomSource: su server è il solo posto
    /// dove si tirano i dadi.
    /// </summary>
    public sealed class PvpMatchEngine
    {
        private sealed class PlayerState
        {
            public CombatCard[] Loadout;
            public List<int> Hand = new();
            public IReadOnlyList<int> Round1Hand;
            public IReadOnlyList<int> Round1Unseen;
            public readonly List<int> Round1Deployed = new();
            // Indici loadout delle carte morte in un round qualunque del match:
            // servono a comporre la mano del round decisivo (le sopravvissute).
            public readonly HashSet<int> DiedLoadout = new();
            public int[] DecisiveChoice;
            public PvpAuraType Aura;
            public bool NecromancerSpiritUsed;
            public bool FormationAuraUsed;
            public readonly List<PvpCardState> Board = new();
            public int RoundWins;
            public ManaPool Mana;
        }

        private readonly PvpMatchRules rules;
        private readonly ManaRules manaRules;
        private readonly IRandomSource random;
        private readonly CombatResolver resolver;
        private readonly PlayerState[] players = { new(), new() };
        private readonly List<PvpCardState> turnOrder = new();
        private readonly List<DeploymentToken> deploymentOrder = new();

        private int turnIndex;
        private int cycle;
        private int deployTurnPlayer;
        private int deploymentIndex;

        private readonly struct DeploymentToken
        {
            public DeploymentToken(int player, int initiative, int tieBreaker)
            {
                Player = player;
                Initiative = initiative;
                TieBreaker = tieBreaker;
            }

            public int Player { get; }
            public int Initiative { get; }
            public int TieBreaker { get; }
        }

        public PvpMatchEngine(
            IReadOnlyList<CombatCard> loadoutPlayer0,
            IReadOnlyList<CombatCard> loadoutPlayer1,
            PvpMatchRules rules,
            IRandomSource random,
            ManaRules manaRules = null)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.manaRules = manaRules ?? ManaRules.CreateDefault();
            resolver = new CombatResolver(random);
            players[0].Loadout = CopyLoadout(loadoutPlayer0, nameof(loadoutPlayer0));
            players[1].Loadout = CopyLoadout(loadoutPlayer1, nameof(loadoutPlayer1));
            players[0].Mana = new ManaPool(this.manaRules);
            players[1].Mana = new ManaPool(this.manaRules);
        }

        /// <summary>Riserva di mana del giocatore. Il mana e' globale, non della singola pedina.</summary>
        public int ManaOf(int player) => players[ValidPlayer(player)].Mana.Current;

        /// <summary>Costo dell'abilita' base della pedina attiva. E' fisso, non sale mai.</summary>
        public int PrimaryCostFor(int player) =>
            players[ValidPlayer(player)].Mana.CostOfPrimary(RequireActiveCard(player).Card.HeroClass);

        /// <summary>Costo della suprema della pedina attiva, ripetizione di classe inclusa.</summary>
        public int SupremeCostFor(int player) =>
            players[ValidPlayer(player)].Mana.CostOfSupreme(RequireActiveCard(player).Card.HeroClass);

        public PvpMatchPhase Phase { get; private set; } = PvpMatchPhase.NotStarted;
        public int MatchRound { get; private set; }
        public int MatchWinner { get; private set; } = -1;
        public int WinsOf(int player) => players[player].RoundWins;
        public PvpAuraType AuraOf(int player) => players[player].Aura;
        public IReadOnlyList<PvpCardState> BoardOf(int player) => players[player].Board;
        public IReadOnlyList<int> HandOf(int player) => players[player].Hand;
        public CombatCard LoadoutCard(int player, int loadoutIndex) => players[player].Loadout[loadoutIndex];

        /// <summary>Giocatore da cui il motore attende input; -1 quando attende entrambi o nessuno.</summary>
        public int ActivePlayer => Phase switch
        {
            PvpMatchPhase.Deployment => deployTurnPlayer,
            PvpMatchPhase.Battle => ActiveCard.Owner,
            _ => -1
        };

        public PvpCardState ActiveCard =>
            Phase == PvpMatchPhase.Battle ? turnOrder[turnIndex] : null;

        public IReadOnlyList<PvpEvent> Start()
        {
            if (Phase != PvpMatchPhase.NotStarted)
                throw new PvpActionException("Il match è già iniziato.");
            var events = new List<PvpEvent>();
            MatchRound = 1;
            StartRound(events);
            return events;
        }

        public IReadOnlyList<PvpEvent> SubmitDecisiveSelection(int player, IReadOnlyList<int> loadoutIndices)
        {
            RequirePhase(PvpMatchPhase.DecisiveSelection);
            PlayerState state = players[ValidPlayer(player)];
            if (state.DecisiveChoice != null)
                throw new PvpActionException("Hai già scelto le carte del round decisivo.");
            var chosen = loadoutIndices != null ? new List<int>(loadoutIndices) : new List<int>();
            if (!PvpHandDealer.TryValidateDecisiveSelection(
                    state.Loadout.Length, chosen, rules.DecisiveHandSize, out string error))
                throw new PvpActionException(error);

            state.DecisiveChoice = chosen.ToArray();
            var events = new List<PvpEvent>();
            if (players[0].DecisiveChoice != null && players[1].DecisiveChoice != null)
            {
                players[0].Hand = new List<int>(players[0].DecisiveChoice);
                players[1].Hand = new List<int>(players[1].DecisiveChoice);
                BeginDeployment(events);
            }
            return events;
        }

        public IReadOnlyList<PvpEvent> Deploy(int player, int handIndex)
        {
            RequirePhase(PvpMatchPhase.Deployment);
            if (ValidPlayer(player) != deployTurnPlayer)
                throw new PvpActionException("Non è il tuo turno di schieramento.");
            PlayerState state = players[player];
            if (handIndex < 0 || handIndex >= state.Hand.Count)
                throw new PvpActionException("Carta non presente nella mano.");

            int loadoutIndex = state.Hand[handIndex];
            state.Hand.RemoveAt(handIndex);
            CombatCard card = state.Loadout[loadoutIndex];
            DeploymentToken token = deploymentOrder[deploymentIndex];
            var deployed = new PvpCardState(player, state.Board.Count, loadoutIndex, card, rules.CardLives);
            deployed.Initiative = token.Initiative;
            deployed.TieBreaker = token.TieBreaker;
            state.Board.Add(deployed);
            if (MatchRound == 1)
                state.Round1Deployed.Add(loadoutIndex);

            var events = new List<PvpEvent>
            {
                new CardDeployedEvent(
                    player,
                    deployed.Slot,
                    card.Id,
                    card.Name,
                    card.HeroClass,
                    card.Strength,
                    deployed.Lives,
                    deployed.Initiative)
            };

            if (players[0].Board.Count >= rules.FormationSize && players[1].Board.Count >= rules.FormationSize)
            {
                BeginBattle(events);
            }
            else
            {
                AdvanceDeploymentIndex();
                events.Add(new DeployTurnEvent(deployTurnPlayer));
            }
            return events;
        }

        private void AdvanceDeploymentIndex()
        {
            deploymentIndex++;
            while (deploymentIndex < deploymentOrder.Count
                && players[deploymentOrder[deploymentIndex].Player].Board.Count >= rules.FormationSize)
            {
                deploymentIndex++;
            }
            deployTurnPlayer = deploymentIndex < deploymentOrder.Count
                ? deploymentOrder[deploymentIndex].Player
                : -1;
        }

        public IReadOnlyList<PvpEvent> UseAbility(int player, int targetPlayer, int targetSlot)
        {
            PvpCardState actor = RequireActiveCard(player);
            if (actor.AbilityUsed || actor.AbilityArmed)
                throw new PvpActionException("Abilità già usata in questo round.");

            // La disponibilita' si verifica prima di toccare qualsiasi stato: un
            // bersaglio non valido piu' avanti non deve lasciare il mana scalato.
            int cost = players[player].Mana.CostOfPrimary(actor.Card.HeroClass);
            RequireAffordable(player, cost);

            var events = new List<PvpEvent>();
            switch (actor.Card.HeroClass)
            {
                case HeroClass.Warrior:
                    actor.AbilityArmed = true;
                    events.Add(new AbilityUsedEvent(player, actor.Slot, HeroClass.Warrior, player, actor.Slot, 0));
                    break;

                case HeroClass.Assassin:
                {
                    PvpCardState target = RequireEnemyTarget(player, targetPlayer, targetSlot);
                    target.InhibitedTurns = Math.Max(target.InhibitedTurns, 1);
                    target.WasInhibited = true;
                    int malus = 0;
                    if (players[player].Aura == PvpAuraType.Assassin)
                    {
                        target.PermanentCombatBonus--;
                        malus = 1;
                    }
                    actor.AbilityUsed = true;
                    events.Add(new AbilityUsedEvent(player, actor.Slot, HeroClass.Assassin, targetPlayer, targetSlot, malus));
                    break;
                }

                case HeroClass.Mage:
                {
                    PvpCardState target = RequireEnemyTarget(player, targetPlayer, targetSlot);
                    int steps = 1;
                    target.PendingVigorStepPenalty = Math.Max(target.PendingVigorStepPenalty, steps);
                    actor.AbilityUsed = true;
                    events.Add(new AbilityUsedEvent(player, actor.Slot, HeroClass.Mage, targetPlayer, targetSlot, steps));
                    break;
                }

                case HeroClass.Hunter:
                {
                    PvpCardState target = RequireEnemyTarget(player, targetPlayer, targetSlot);
                    if (IsMarked(target))
                        throw new PvpActionException("Quel bersaglio è già marcato.");
                    actor.MarkedTarget = target;
                    actor.AbilityUsed = true;
                    events.Add(new AbilityUsedEvent(player, actor.Slot, HeroClass.Hunter, targetPlayer, targetSlot, MarkBonusOf(actor)));
                    break;
                }

                case HeroClass.Paladin:
                {
                    PvpCardState ally = RequireAllyTarget(player, targetPlayer, targetSlot);
                    actor.AbilityArmed = true;
                    actor.ProtectedAlly = ally;
                    events.Add(new AbilityUsedEvent(player, actor.Slot, HeroClass.Paladin, targetPlayer, targetSlot, 0));
                    break;
                }

                case HeroClass.Necromancer:
                {
                    if (targetPlayer != player)
                        throw new PvpActionException("Il Necromancer rialza solo carte alleate.");
                    PvpCardState target = BoardCard(targetPlayer, targetSlot);
                    if (!target.Eliminated || target.IsAttachment || target.IsSpirit)
                        throw new PvpActionException("Quella carta non può essere rialzata.");
                    target.Eliminated = false;
                    target.Lives = 1;
                    MoveTurnAfter(actor, target);
                    actor.AbilityUsed = true;
                    events.Add(new AbilityUsedEvent(player, actor.Slot, HeroClass.Necromancer, targetPlayer, targetSlot, 1));
                    events.Add(new CardRevivedEvent(targetPlayer, targetSlot, target.Lives));
                    break;
                }

                case HeroClass.Priest:
                {
                    PvpCardState ally = RequireAllyTarget(player, targetPlayer, targetSlot);
                    int bonus = players[player].Aura == PvpAuraType.Priest
                        ? rules.PriestBlessingBonus + 1
                        : rules.PriestBlessingBonus;
                    ally.PendingAttackBonus += bonus;
                    if (ally.PendingBonusKind != PvpPendingBonusKind.Fury)
                        ally.PendingBonusKind = PvpPendingBonusKind.Blessing;
                    actor.AbilityUsed = true;
                    events.Add(new AbilityUsedEvent(player, actor.Slot, HeroClass.Priest, targetPlayer, targetSlot, bonus));
                    break;
                }

                default:
                    throw new PvpActionException($"{actor.Card.HeroClass} ha un'abilità passiva, non attivabile.");
            }
            // Speso una volta che l'effetto e' andato a buon fine. Da qui in poi il
            // mana e' perso comunque: se l'attacco successivo fallisce, non torna.
            SpendMana(player, cost, events);
            actor.AbilityUsedThisTurn = true;
            return events;
        }

        /// <summary>
        /// Abilita' suprema. Le supreme d'attacco (Mago, Cacciatore) consumano l'azione
        /// d'attacco e chiudono l'attivazione; le altre no e lasciano la pedina libera
        /// di attaccare, com'e' per le abilita' base. Vedi Docs/mana-design.md.
        /// </summary>
        public IReadOnlyList<PvpEvent> UseSupreme(int player, int targetPlayer, int targetSlot)
        {
            PvpCardState actor = RequireActiveCard(player);
            HeroClass heroClass = actor.Card.HeroClass;

            if (!AbilityManaCosts.IsSupremeImplemented(heroClass))
                throw new PvpActionException(
                    PvpActionErrorCodes.SupremeNotAvailable,
                    $"La suprema di {heroClass} non è ancora disponibile.");

            bool attackSupreme = AbilityManaCosts.IsAttackSupreme(heroClass);
            // Per attivazione: una sola abilita' non-d'attacco, piu' una azione d'attacco.
            if (!attackSupreme && actor.AbilityUsedThisTurn)
                throw new PvpActionException(
                    PvpActionErrorCodes.AbilityRequiresAction,
                    "Hai già usato un'abilità in questa attivazione.");

            int cost = players[player].Mana.CostOfSupreme(heroClass);
            RequireAffordable(player, cost);

            var events = new List<PvpEvent>();
            int magnitude = ApplySupreme(actor, heroClass, targetPlayer, targetSlot, events);

            SpendMana(player, cost, events);
            // La Riserva del Paladino agisce dopo il pagamento: e' quello che la rende
            // una soglia e non un guadagno secco (vedi Docs/mana-design.md).
            if (heroClass == HeroClass.Paladin)
                magnitude = ApplyManaReserve(actor, events);
            players[player].Mana.RegisterSupremeUse(heroClass);
            events.Add(new SupremeUsedEvent(
                player,
                actor.Slot,
                heroClass,
                AbilityManaCosts.SupremeOf(heroClass),
                targetPlayer,
                targetSlot,
                magnitude));

            if (attackSupreme)
                EndTurn(events);
            else
                actor.AbilityUsedThisTurn = true;
            return events;
        }

        private int ApplySupreme(
            PvpCardState actor,
            HeroClass heroClass,
            int targetPlayer,
            int targetSlot,
            List<PvpEvent> events)
        {
            switch (heroClass)
            {
                case HeroClass.Warrior:
                    return ApplyWarriorEmpower(actor);
                case HeroClass.Rogue:
                    return ApplyRogueStealBuffs(actor, targetPlayer, targetSlot);
                case HeroClass.Mage:
                case HeroClass.Hunter:
                    return ApplyAreaStrike(actor, events);
                case HeroClass.Barbarian:
                    return ApplyWarHorn(actor);
                case HeroClass.Paladin:
                    // Applicata dopo la spesa, in UseSupreme: qui non c'e' nulla da fare.
                    return 0;
                case HeroClass.Priest:
                    return ApplyDispel(actor);
                case HeroClass.Assassin:
                    return ApplyVanish(actor);
                default:
                    throw new PvpActionException(
                        PvpActionErrorCodes.SupremeNotAvailable,
                        $"La suprema di {heroClass} non è ancora disponibile.");
            }
        }

        /// <summary>Guerriero: +2 permanente, +4 se e' rimasto solo. I due non si cumulano.</summary>
        private int ApplyWarriorEmpower(PvpCardState actor)
        {
            int bonus = ActiveCount(players[actor.Owner].Board) <= 1 ? 4 : 2;
            actor.PermanentCombatBonus += bonus;
            return bonus;
        }

        /// <summary>
        /// Ladro: ruba tutti i potenziamenti del bersaglio. Se non ne ha, gli sottrae
        /// 2 di Potenza e se li prende: e' un furto, non una semplice riduzione.
        /// </summary>
        private int ApplyRogueStealBuffs(PvpCardState actor, int targetPlayer, int targetSlot)
        {
            PvpCardState target = RequireEnemyTarget(actor.Owner, targetPlayer, targetSlot);
            int stolen = target.PendingAttackBonus + Math.Max(0, target.PermanentCombatBonus);
            if (stolen > 0)
            {
                target.PendingAttackBonus = 0;
                if (target.PermanentCombatBonus > 0)
                    target.PermanentCombatBonus = 0;
                target.PendingBonusKind = PvpPendingBonusKind.None;
                actor.PermanentCombatBonus += stolen;
                return stolen;
            }

            const int theft = 2;
            target.PermanentCombatBonus -= theft;
            actor.PermanentCombatBonus += theft;
            return theft;
        }

        /// <summary>
        /// Mago e Cacciatore: colpiscono tutte le pedine avversarie attive con il dado
        /// vigore abbassato di uno step. La protezione del Paladino non si applica:
        /// un colpo ad area raggiunge comunque tutti, non c'e' niente da deviare.
        /// </summary>
        private int ApplyAreaStrike(PvpCardState actor, List<PvpEvent> events)
        {
            int enemy = 1 - actor.Owner;
            // Copia: la lista puo' cambiare durante gli scambi (spiriti, eliminazioni).
            var targets = new List<PvpCardState>(players[enemy].Board);
            // Un malus gia' subito (es. dal Mago avversario) si somma allo step
            // dell'area invece di essere sovrascritto.
            int carriedPenalty = actor.PendingVigorStepPenalty;
            int hits = 0;
            foreach (PvpCardState target in targets)
            {
                if (!target.IsActive || !actor.IsActive)
                    continue;
                // Il penalty viene consumato da ogni scambio: va rimesso per ciascun bersaglio.
                actor.PendingVigorStepPenalty = carriedPenalty + 1;
                ResolveExchange(actor, target, defenderAdvantage: false, isCounter: false, counterFlatBonus: 0, events);
                hits++;
            }
            return hits;
        }

        /// <summary>Barbaro: la cornamusa potenzia tutto il party fino a fine round.</summary>
        private int ApplyWarHorn(PvpCardState actor)
        {
            int bonus = rules.BarbarianRageBonus;
            foreach (PvpCardState ally in players[actor.Owner].Board)
            {
                if (!ally.IsActive)
                    continue;
                ally.PendingAttackBonus += bonus;
                // Furia e non Benedizione: la cornamusa vale anche in difesa.
                ally.PendingBonusKind = PvpPendingBonusKind.Fury;
            }
            return bonus;
        }

        /// <summary>Paladino: la Riserva porta il mana alla soglia se e' sotto. Mai oltre.</summary>
        private int ApplyManaReserve(PvpCardState actor, List<PvpEvent> events)
        {
            ManaPool pool = players[actor.Owner].Mana;
            int gained = pool.RaiseTo(manaRules.PaladinReserveThreshold);
            if (gained > 0)
                events.Add(new ManaChangedEvent(actor.Owner, pool.Current, gained, ManaChangeReasons.Reserve));
            return gained;
        }

        /// <summary>
        /// Sacerdote: toglie i malus agli alleati e i potenziamenti agli avversari.
        /// Non tocca le aure, che nascono dalla formazione e non da una giocata.
        /// </summary>
        private int ApplyDispel(PvpCardState actor)
        {
            int cleared = 0;
            foreach (PvpCardState ally in players[actor.Owner].Board)
            {
                if (!ally.IsActive)
                    continue;
                if (ally.InhibitedTurns > 0) { ally.InhibitedTurns = 0; cleared++; }
                if (ally.PendingVigorStepPenalty > 0) { ally.PendingVigorStepPenalty = 0; cleared++; }
                if (ally.PermanentCombatBonus < 0) { ally.PermanentCombatBonus = 0; cleared++; }
            }

            int enemy = 1 - actor.Owner;
            foreach (PvpCardState foe in players[enemy].Board)
            {
                if (!foe.IsActive)
                    continue;
                if (foe.PendingAttackBonus > 0)
                {
                    foe.PendingAttackBonus = 0;
                    foe.PendingBonusKind = PvpPendingBonusKind.None;
                    cleared++;
                }
                if (foe.PermanentCombatBonus > 0) { foe.PermanentCombatBonus = 0; cleared++; }
                if (foe.IsUntargetable) { foe.IsUntargetable = false; cleared++; }
            }
            return cleared;
        }

        /// <summary>Assassino: diventa non bersagliabile e non decade.</summary>
        private int ApplyVanish(PvpCardState actor)
        {
            actor.IsUntargetable = true;
            return 1;
        }

        private static int ActiveCount(IReadOnlyList<PvpCardState> board)
        {
            int count = 0;
            foreach (PvpCardState card in board)
                if (card.IsActive)
                    count++;
            return count;
        }

        /// <summary>
        /// L'invisibilita' protegge finche' l'Assassino non e' l'unica pedina attiva
        /// del suo schieramento: a quel punto torna bersagliabile e difende con vantaggio.
        /// </summary>
        private bool IsShieldedByInvisibility(PvpCardState card) =>
            card.IsUntargetable && ActiveCount(players[card.Owner].Board) > 1;

        public IReadOnlyList<PvpEvent> Attack(int player, int targetSlot)
        {
            PvpCardState attacker = RequireActiveCard(player);
            int enemy = 1 - player;
            PvpCardState defender = BoardCard(enemy, targetSlot);
            if (!defender.IsActive)
                throw new PvpActionException("Il bersaglio è già eliminato.");
            if (IsShieldedByInvisibility(defender))
                throw new PvpActionException("Quel bersaglio non è raggiungibile: è invisibile.");

            var events = new List<PvpEvent>();

            // Protezione Paladin: deviazione su un altro paladino o autodifesa con vantaggio.
            bool defenderAdvantage = false;
            PvpCardState protectionUser = null;
            PvpCardState redirecting = FindProtectingPaladin(enemy, defender);
            if (redirecting != null)
            {
                events.Add(new ProtectionTriggeredEvent(enemy, redirecting.Slot, redirected: true));
                defender = redirecting;
                protectionUser = redirecting;
                ConsumeProtection(redirecting);
                defenderAdvantage = true;
            }
            else if (defender.Card.HeroClass == HeroClass.Paladin
                && defender.AbilityArmed
                && (defender.ProtectedAlly == null || defender.ProtectedAlly == defender))
            {
                events.Add(new ProtectionTriggeredEvent(enemy, defender.Slot, redirected: false));
                protectionUser = defender;
                ConsumeProtection(defender);
                defenderAdvantage = true;
            }

            // Assassino invisibile rimasto solo: torna bersagliabile, ma difende con vantaggio.
            if (defender.IsUntargetable)
                defenderAdvantage = true;

            ResolveExchange(attacker, defender, defenderAdvantage, isCounter: false, counterFlatBonus: 0, events);

            // Aura Paladin: contrattacco immediato se la protezione è scattata e i due sono vivi.
            if (players[enemy].Aura == PvpAuraType.Paladin
                && protectionUser is { IsActive: true }
                && attacker.IsActive)
            {
                ResolveExchange(protectionUser, attacker, defenderAdvantage: false, isCounter: true, counterFlatBonus: 1, events);
            }

            EndTurn(events);
            return events;
        }

        public IReadOnlyList<PvpEvent> Attach(int player, int allySlot)
        {
            PvpCardState source = RequireActiveCard(player);
            if (source.Card.Strength < 2 || source.Card.Strength >= 5)
                throw new PvpActionException("Solo carte di valore 2-4 possono diventare attachment.");
            PvpCardState target = BoardCard(player, allySlot);
            if (target == source || !target.IsActive)
                throw new PvpActionException("Bersaglio attachment non valido.");

            int bonus = 5 - source.Card.Strength;
            target.PermanentCombatBonus += bonus;
            source.Lives = 0;
            source.Eliminated = true;
            source.IsAttachment = true;

            var events = new List<PvpEvent>
            {
                new AttachmentAppliedEvent(player, source.Slot, allySlot, bonus)
            };
            EndTurn(events);
            return events;
        }

        public IReadOnlyList<PvpEvent> Pass(int player)
        {
#if false // Regola precedente: obbligava ad attaccare dopo avere usato un'abilita'.
            PvpCardState actor = RequireActiveCard(player);
            if (actor.AbilityUsedThisTurn)
                throw new PvpActionException(
                    PvpActionErrorCodes.AbilityRequiresAction,
                    "Dopo aver usato un'abilitÃ  devi attaccare o equipaggiarti.");
            var events = new List<PvpEvent>();
            EndTurn(events, skipped: true);
            return events;
#endif
            PvpCardState actor = RequireActiveCard(player);
            bool actedBeforePassing = actor.AbilityUsedThisTurn;
            var passEvents = new List<PvpEvent>();
            EndTurn(passEvents, skipped: true, usedAbilityBeforeSkip: actedBeforePassing);
            return passEvents;
        }

        public bool HasDecisiveChoice(int player) => players[ValidPlayer(player)].DecisiveChoice != null;

        /// <summary>Abbandono (disconnessione o troppi timeout): vince l'avversario.</summary>
        public IReadOnlyList<PvpEvent> Forfeit(int player)
        {
            ValidPlayer(player);
            if (Phase is PvpMatchPhase.NotStarted or PvpMatchPhase.Finished)
                throw new PvpActionException("Nessun match in corso da abbandonare.");

            Phase = PvpMatchPhase.Finished;
            MatchWinner = 1 - player;
            return new List<PvpEvent>
            {
                new MatchEndedEvent(MatchWinner, players[0].RoundWins, players[1].RoundWins)
            };
        }

        // --- Flusso round ---

        private void StartRound(List<PvpEvent> events)
        {
            for (int player = 0; player < players.Length; player++)
            {
                PlayerState state = players[player];
                state.Board.Clear();
                state.Aura = PvpAuraType.None;
                state.NecromancerSpiritUsed = false;
                state.FormationAuraUsed = false;
                state.DecisiveChoice = null;
                // Il mana attraversa i round: si azzerano solo i contatori di
                // ripetizione, e la riserva risale al pavimento se e' sotto.
                int before = state.Mana.Current;
                state.Mana.StartRound();
				// Invia sempre il valore assoluto: al primo round il pool nasce gia' a
				// RunStart e quindi il delta e' zero, ma il client deve comunque sapere
				// che la partita comincia con quella riserva.
				events.Add(new ManaChangedEvent(
					player,
					state.Mana.Current,
					state.Mana.Current - before,
					ManaChangeReasons.RoundFloor));
            }
            turnOrder.Clear();
            turnIndex = 0;
            cycle = 1;
            events.Add(new RoundStartedEvent(MatchRound, rules.VigorDieForRound(MatchRound)));

            if (MatchRound == 1)
            {
                foreach (PlayerState state in players)
                {
                    PvpFirstDeal deal = PvpHandDealer.DealFirstHand(random, state.Loadout.Length, rules.HandSize);
                    state.Round1Hand = deal.HandIndices;
                    state.Round1Unseen = deal.UnseenIndices;
                    state.Hand = new List<int>(deal.HandIndices);
                }
                BeginDeployment(events);
            }
            else if (MatchRound == 2)
            {
                foreach (PlayerState state in players)
                    state.Hand = PvpHandDealer.BuildSecondHand(
                        state.Round1Unseen, state.Round1Hand, state.Round1Deployed);
                BeginDeployment(events);
            }
            else
            {
                // Round decisivo: la mano è composta dalle sole carte mai morte nei
                // round precedenti (sopravvissute in campo + mai schierate). Poi si
                // schiera normalmente come nei round 1 e 2. Si arriva qui solo sull'1-1,
                // quindi ogni giocatore ha perso un round (3 carte morte) e ne ha 6-4
                // di scorta: mai meno di FormationSize.
                foreach (PlayerState state in players)
                    state.Hand = BuildSurvivorHand(state);
                BeginDeployment(events);
            }
        }

        private List<int> BuildSurvivorHand(PlayerState state)
        {
            var hand = new List<int>();
            for (int index = 0; index < state.Loadout.Length; index++)
            {
                if (!state.DiedLoadout.Contains(index))
                    hand.Add(index);
            }
            hand.Sort();
            return hand;
        }

        private void BeginDeployment(List<PvpEvent> events)
        {
            Phase = PvpMatchPhase.Deployment;
            events.Add(new HandReadyEvent(0));
            events.Add(new HandReadyEvent(1));
            BuildDeploymentOrder();
            deploymentIndex = 0;
            deployTurnPlayer = deploymentOrder.Count > 0 ? deploymentOrder[0].Player : 0;
            int firstPlayerRoll = FirstDeploymentInitiativeFor(0);
            int secondPlayerRoll = FirstDeploymentInitiativeFor(1);
            events.Add(new DeploymentStartedEvent(deployTurnPlayer, firstPlayerRoll, secondPlayerRoll));
            for (int index = 0; index < deploymentOrder.Count; index++)
            {
                DeploymentToken token = deploymentOrder[index];
                events.Add(new DeploymentInitiativeEvent(index, token.Player, token.Initiative));
            }
            events.Add(new DeployTurnEvent(deployTurnPlayer));
        }

        private void BuildDeploymentOrder()
        {
            deploymentOrder.Clear();
            var usedInitiatives = new HashSet<int>();
            for (int player = 0; player < players.Length; player++)
            {
                for (int slot = 0; slot < rules.FormationSize; slot++)
                {
                    deploymentOrder.Add(new DeploymentToken(
                        player,
                        RollUniqueInitiative(usedInitiatives),
                        random.NextInclusive(1, 10000)));
                }
            }
            deploymentOrder.Sort((left, right) =>
            {
                int byInitiative = left.Initiative.CompareTo(right.Initiative);
                return byInitiative != 0 ? byInitiative : left.TieBreaker.CompareTo(right.TieBreaker);
            });
        }

        private int FirstDeploymentInitiativeFor(int player)
        {
            foreach (DeploymentToken token in deploymentOrder)
            {
                if (token.Player == player)
                    return token.Initiative;
            }
            return 0;
        }

        private void BeginBattle(List<PvpEvent> events)
        {
            players[0].Aura = PvpAura.Determine(CardsOf(players[0].Board));
            players[1].Aura = PvpAura.Determine(CardsOf(players[1].Board));
            events.Add(new BattleStartedEvent(players[0].Aura, players[1].Aura));

            turnOrder.Clear();
            foreach (PlayerState state in players)
            {
                foreach (PvpCardState card in state.Board)
                {
                    turnOrder.Add(card);
                }
            }
            turnOrder.Sort((left, right) =>
            {
                int byInitiative = right.Initiative.CompareTo(left.Initiative);
                return byInitiative != 0 ? byInitiative : right.TieBreaker.CompareTo(left.TieBreaker);
            });
            turnIndex = 0;
            cycle = 1;
            Phase = PvpMatchPhase.Battle;
            BeginTurn(events, advanceFirst: false);
        }

        private void BeginTurn(List<PvpEvent> events, bool advanceFirst)
        {
            if (advanceFirst)
                AdvanceTurnIndex();
            while (true)
            {
                PvpCardState card = turnOrder[turnIndex];
                if (!card.IsActive)
                {
                    AdvanceTurnIndex();
                    continue;
                }
                if (card.InhibitedTurns > 0)
                {
                    card.InhibitedTurns--;
                    events.Add(new TurnSkippedEvent(card.Owner, card.Slot, "inhibited"));
                    ExpireSpiritIfNeeded(card, events);
                    if (CheckRoundEnd(events))
                        return;
                    AdvanceTurnIndex();
                    continue;
                }
				// Le abilita' primarie sono riutilizzabili: il mana e' il loro limite.
				// AbilityArmed resta separato per gli effetti ancora in attesa di risolversi.
				card.AbilityUsed = false;
                events.Add(new TurnStartedEvent(card.Owner, card.Slot, cycle));
                return;
            }
        }

        private void EndTurn(
            List<PvpEvent> events,
            bool skipped = false,
            bool usedAbilityBeforeSkip = false)
        {
            PvpCardState card = turnOrder[turnIndex];
            card.AbilityUsedThisTurn = false;
            // Recupero di fine attivazione: +3 se la pedina ha rinunciato del tutto.
            // Le pedine evocate non generano mana (vedi Docs/mana-design.md).
            int reward = ManaActionPolicy.ActivationReward(
                manaRules,
                skipped,
                usedAbilityBeforeSkip);
            if (!card.IsSpirit && reward > 0)
                GainMana(card.Owner, reward,
                    skipped ? ManaChangeReasons.Skip : ManaChangeReasons.Activation, events);
            ExpireSpiritIfNeeded(card, events);
            if (CheckRoundEnd(events))
                return;
            BeginTurn(events, advanceFirst: true);
        }

        // --- Mana ---

        private void GainMana(int player, int amount, string reason, List<PvpEvent> events)
        {
            ManaPool pool = players[player].Mana;
            int gained = pool.Gain(amount);
            if (gained > 0)
                events.Add(new ManaChangedEvent(player, pool.Current, gained, reason));
        }

        private void RequireAffordable(int player, int cost)
        {
            ManaPool pool = players[player].Mana;
            if (!pool.CanAfford(cost))
                throw new PvpActionException(
                    PvpActionErrorCodes.NotEnoughMana,
                    $"Mana insufficiente: servono {cost} rune, ne hai {pool.Current}.");
        }

        private void SpendMana(int player, int cost, List<PvpEvent> events)
        {
            RequireAffordable(player, cost);
            ManaPool pool = players[player].Mana;
            pool.Spend(cost);
            events.Add(new ManaChangedEvent(player, pool.Current, -cost, ManaChangeReasons.Spend));
        }

        /// <summary>
        /// Mana da parata: ogni volta che la pedina regge uno scontro reale senza
        /// perdere vite, il suo proprietario guadagna mana.
        /// </summary>
        private void GainParryMana(PvpCardState defender, List<PvpEvent> events)
        {
            if (defender.IsSpirit)
                return;
            GainMana(defender.Owner, manaRules.GainOnParry, ManaChangeReasons.Parry, events);
        }

        /// <summary>
        /// Un'eliminazione paga entrambi: chi uccide e chi perde la pedina. Il secondo
        /// compensa la perdita di un generatore di mana ed e' l'anti-valanga del sistema.
        /// </summary>
        private void GainEliminationMana(PvpCardState killer, PvpCardState victim, List<PvpEvent> events)
        {
            if (killer != null)
                GainMana(killer.Owner, manaRules.GainOnKill, ManaChangeReasons.Kill, events);
            if (!victim.IsAttachment)
                GainMana(victim.Owner, manaRules.GainOnLoss, ManaChangeReasons.Loss, events);
        }

        private void ExpireSpiritIfNeeded(PvpCardState card, List<PvpEvent> events)
        {
            if (!card.IsSpirit)
                return;
            card.IsSpirit = false;
            card.Eliminated = true;
            events.Add(new SpiritExpiredEvent(card.Owner, card.Slot));
            ApplyMightAuraDeathBonuses(events);
        }

        private void AdvanceTurnIndex()
        {
            turnIndex++;
            if (turnIndex < turnOrder.Count)
                return;
            turnIndex = 0;
            cycle++;
        }

        private bool CheckRoundEnd(List<PvpEvent> events)
        {
            int loser = -1;
            if (AllEliminated(players[0].Board))
                loser = 0;
            else if (AllEliminated(players[1].Board))
                loser = 1;
            if (loser < 0)
                return false;

            int winner = 1 - loser;
            players[winner].RoundWins++;
            events.Add(new RoundEndedEvent(
                MatchRound, winner, players[0].RoundWins, players[1].RoundWins));
            RecordRoundCasualties();

            if (players[winner].RoundWins >= rules.RoundsToWin)
            {
                Phase = PvpMatchPhase.Finished;
                MatchWinner = winner;
                events.Add(new MatchEndedEvent(winner, players[0].RoundWins, players[1].RoundWins));
            }
            else
            {
                MatchRound++;
                StartRound(events);
            }
            return true;
        }

        // Segna come morte, per il round decisivo, tutte le carte eliminate nel
        // round appena concluso (va chiamato prima che StartRound svuoti le board).
        private void RecordRoundCasualties()
        {
            foreach (PlayerState state in players)
            {
                foreach (PvpCardState card in state.Board)
                {
                    if (card.Eliminated || card.Lives <= 0)
                        state.DiedLoadout.Add(card.LoadoutIndex);
                }
            }
        }

        // --- Risoluzione attacco ---

        private void ResolveExchange(
            PvpCardState attacker,
            PvpCardState defender,
            bool defenderAdvantage,
            bool isCounter,
            int counterFlatBonus,
            List<PvpEvent> events)
        {
            int baseDie = rules.VigorDieForRound(MatchRound);
            int attackerDie = PvpVigorScale.LowerBySteps(baseDie, attacker.PendingVigorStepPenalty);
            int defenderDie = PvpVigorScale.LowerBySteps(baseDie, defender.PendingVigorStepPenalty);
            if (HasMagicDefenseAura(defender))
                defenderDie = PvpVigorScale.Raise(defenderDie);

            CombatModifiers modifiers = isCounter
                ? new CombatModifiers(
                    sumAttackerVigor: false,
                    defenderAdvantage: false,
                    rerollAttackerOnes: false,
                    rerollAttackerTwos: false,
                    attackerFlatBonus: counterFlatBonus,
                    rerollDefenderOnes: false,
                    rerollDefenderTwos: false,
                    attackerConditionalRerollMax: (rules.RogueRerollsOnes || players[attacker.Owner].Aura == PvpAuraType.Rogue)
                        && attacker.Card.HeroClass == HeroClass.Rogue
                        ? RogueConditionalRerollMaximum(baseDie)
                        : 0,
                    defenderConditionalRerollMax: players[defender.Owner].Aura == PvpAuraType.Rogue
                        && defender.Card.HeroClass == HeroClass.Rogue
                        ? RogueConditionalRerollMaximum(baseDie)
                        : 0)
                : BuildAttackModifiers(attacker, defender, defenderAdvantage);

            CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(
                attacker.Card, defender.Card, attackerDie, defenderDie, modifiers);

            bool defenderLostLife = false;
            bool overkill = false;
            var emptyRoll = default(VigorRollResult);
            VigorRollResult attackerRoll = emptyRoll;
            VigorRollResult defenderRoll = emptyRoll;
            int attackerTotal = 0;
            int defenderTotal = 0;

            // In PvP si tirano sempre i dadi quando l'attaccante può vincere
            // (Guaranteed o RollRequired): anche con la vittoria matematicamente
            // certa serve il numero reale per stabilire se scatta l'Overkill.
            if (certainty != CombatCertainty.Impossible)
            {
                CombatResult result = resolver.ResolveAttack(
                    attacker.Card, defender.Card, attackerDie, defenderDie, modifiers);
                ConsumeArmedAttackAbility(attacker, modifiers);
                attackerRoll = result.AttackerRoll;
                defenderRoll = result.DefenderRoll;
                attackerTotal = result.AttackerTotal;
                defenderTotal = result.DefenderTotal;
                defenderLostLife = result.DefenderIsDefeated;
                // Overkill: se l'attaccante totalizza almeno il doppio del
                // difensore, la carta perde entrambe le vite in un colpo solo.
                overkill = defenderLostLife && attackerTotal >= 2 * defenderTotal;
            }

            bool defenderEliminated = false;
            bool becameSpirit = false;
            if (defenderLostLife)
            {
                if (defender.IsSpirit)
                {
                    defender.IsSpirit = false;
                    defender.Eliminated = true;
                    defenderEliminated = true;
                }
                else
                {
                    defender.Lives -= overkill ? 2 : 1;
                    if (defender.Lives <= 0)
                    {
                        if (TryBecomeSpirit(defender))
                        {
                            becameSpirit = true;
                        }
                        else
                        {
                            defender.Eliminated = true;
                            defenderEliminated = true;
                        }
                    }
                }
            }

            ConsumeVigorPenalties(attacker, defender);
            if (!isCounter)
                ApplyPostAttackState(attacker, defender, defenderLostLife, events);
            if (defenderEliminated)
            {
                ApplyMageAuraDeathPenalty(defender, attacker, events);
                ApplyMightAuraDeathBonuses(events);
                GainEliminationMana(attacker, defender, events);
            }
            bool defenderParried = !defenderEliminated
                && certainty != CombatCertainty.Impossible
                && !defenderLostLife;

            events.Add(new AttackResolvedEvent(
                attacker.Owner,
                attacker.Slot,
                defender.Owner,
                defender.Slot,
                certainty,
                attackerDie,
                defenderDie,
                attackerRoll,
                defenderRoll,
                attackerTotal,
                defenderTotal,
                defenderLostLife,
                Math.Max(defender.Lives, 0),
                defenderEliminated,
                becameSpirit,
                overkill,
                isCounter));

            // L'evento mana segue la risoluzione: la UI presenta prima la parata e
            // accredita il premio soltanto al termine della relativa animazione.
            if (defenderParried)
                GainParryMana(defender, events);
        }

        private CombatModifiers BuildAttackModifiers(
            PvpCardState attacker, PvpCardState defender, bool defenderAdvantage)
        {
            PlayerState attackerTeam = players[attacker.Owner];
            PlayerState defenderTeam = players[defender.Owner];
            int attackerFlat = attacker.PendingAttackBonus + attacker.PermanentCombatBonus;
            attackerFlat += MarkBonusForTarget(defender);
            int defenderFlat = defender.PermanentCombatBonus + defender.PendingDefenseBonus;

            // L'aura del Guerriero confronta la Potenza effettiva nello scontro,
            // non il valore base stampato sulla carta. Il confronto deve quindi
            // includere benedizioni applicabili, equipaggiamenti, malus e gli altri
            // bonus gia' attivi, ma non il +2 dell'aura stessa.
            int attackerEffectiveStrength = attacker.Card.Strength + attackerFlat;
            int defenderEffectiveStrength = defender.Card.Strength + defenderFlat;
            if (attackerTeam.Aura == PvpAuraType.Warrior
                && attacker.Card.HeroClass == HeroClass.Warrior
                && attackerEffectiveStrength < defenderEffectiveStrength)
                attackerFlat += 2;

            if (defenderTeam.Aura == PvpAuraType.Warrior
                && defender.Card.HeroClass == HeroClass.Warrior
                && defenderEffectiveStrength < attackerEffectiveStrength)
                defenderFlat += 2;

            bool forceAdvantage = attackerTeam.Aura == PvpAuraType.Cunning
                && HeroClassFamily.Of(attacker.Card.HeroClass) == ClassFamily.Cunning
                && HasBonusOrMalusForCunning(defender);

            bool neutralize = false;
            if (attackerTeam.Aura == PvpAuraType.Formation
                && ClassMatchup.Compare(attacker.Card.HeroClass, defender.Card.HeroClass) == MatchupResult.Disadvantage)
            {
                neutralize = true;
            }

            return new CombatModifiers(
                sumAttackerVigor: attacker.AbilityArmed && attacker.Card.HeroClass == HeroClass.Warrior,
                defenderAdvantage: defenderAdvantage,
                rerollAttackerOnes: false,
                rerollAttackerTwos: false,
                attackerFlatBonus: attackerFlat,
                defenderFlatBonus: defenderFlat,
                neutralizeAttackerMatchup: neutralize,
                forceAttackerAdvantage: forceAdvantage,
                rerollDefenderOnes: false,
                rerollDefenderTwos: false,
                attackerConditionalRerollMax: (rules.RogueRerollsOnes || attackerTeam.Aura == PvpAuraType.Rogue)
                    && attacker.Card.HeroClass == HeroClass.Rogue
                    ? RogueConditionalRerollMaximum(rules.VigorDieForRound(MatchRound))
                    : 0,
                defenderConditionalRerollMax: defenderTeam.Aura == PvpAuraType.Rogue
                    && defender.Card.HeroClass == HeroClass.Rogue
                    ? RogueConditionalRerollMaximum(rules.VigorDieForRound(MatchRound))
                    : 0);
        }

        private static int RogueConditionalRerollMaximum(int attackerDieForReroll)
        {
            return attackerDieForReroll switch
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

        private void ApplyPostAttackState(PvpCardState attacker, PvpCardState defender, bool defeatedTarget, List<PvpEvent> events)
        {
            attacker.PendingAttackBonus = 0;
            attacker.PendingBonusKind = PvpPendingBonusKind.None;
            if (attacker.Card.HeroClass == HeroClass.Barbarian && !defeatedTarget)
            {
                ApplyBarbarianFury(attacker, events);
            }

            if (defender.Card.HeroClass == HeroClass.Barbarian && !defeatedTarget)
                ApplyBarbarianFury(defender, events);
        }

        private void ApplyBarbarianFury(PvpCardState card, List<PvpEvent> events)
        {
            PlayerState team = players[card.Owner];
            int fury = team.Aura == PvpAuraType.Barbarian
                ? rules.BarbarianRageBonus + 1
                : rules.BarbarianRageBonus;
            card.PendingAttackBonus = fury;
            card.PendingBonusKind = PvpPendingBonusKind.Fury;
            events.Add(new FuryGainedEvent(card.Owner, card.Slot, fury));
        }

        private void ApplyMightAuraDeathBonuses(List<PvpEvent> events)
        {
            for (int player = 0; player < players.Length; player++)
            {
                PlayerState team = players[player];
                if (team.Aura != PvpAuraType.Might)
                    continue;

                foreach (PvpCardState card in team.Board)
                {
                    if (!card.IsActive || HeroClassFamily.Of(card.Card.HeroClass) != ClassFamily.Might)
                        continue;

                    card.PermanentCombatBonus++;
                    events.Add(new MightAuraBonusEvent(player, card.Slot));
                }
            }
        }

        private void ApplyMageAuraDeathPenalty(
            PvpCardState defeated,
            PvpCardState attacker,
            List<PvpEvent> events)
        {
            if (defeated == null
                || attacker == null
                || defeated.Card.HeroClass != HeroClass.Mage
                || players[defeated.Owner].Aura != PvpAuraType.Mage)
                return;

            attacker.PermanentCombatBonus -= 2;
            events.Add(new MageAuraPenaltyEvent(attacker.Owner, attacker.Slot, 2));
        }

        private void ConsumeArmedAttackAbility(PvpCardState attacker, CombatModifiers modifiers)
        {
            if (!attacker.AbilityArmed || !modifiers.SumAttackerVigor)
                return;
            attacker.AbilityArmed = false;
            attacker.AbilityUsed = true;
        }

        private static void ConsumeVigorPenalties(PvpCardState first, PvpCardState second)
        {
            first.PendingVigorStepPenalty = 0;
            second.PendingVigorStepPenalty = 0;
        }

        private bool TryBecomeSpirit(PvpCardState defeated)
        {
            PlayerState team = players[defeated.Owner];
            if (team.Aura != PvpAuraType.Necromancer || team.NecromancerSpiritUsed || defeated.IsAttachment)
                return false;
            team.NecromancerSpiritUsed = true;
            defeated.IsSpirit = true;
            defeated.AbilityUsed = false;
            defeated.AbilityArmed = false;
            return true;
        }

        private PvpCardState FindProtectingPaladin(int team, PvpCardState defender)
        {
            foreach (PvpCardState card in players[team].Board)
            {
                if (card != defender
                    && card.IsActive
                    && card.Card.HeroClass == HeroClass.Paladin
                    && card.AbilityArmed
                    && (card.ProtectedAlly == null || card.ProtectedAlly == defender))
                    return card;
            }
            return null;
        }

        private static void ConsumeProtection(PvpCardState paladin)
        {
            paladin.AbilityArmed = false;
            paladin.AbilityUsed = true;
            paladin.ProtectedAlly = null;
        }

        private bool HasMagicDefenseAura(PvpCardState card) =>
            card.IsActive
            && players[card.Owner].Aura == PvpAuraType.Magic
            && HeroClassFamily.Of(card.Card.HeroClass) == ClassFamily.Magic;

        private bool HasBonusOrMalusForCunning(PvpCardState target) =>
            target != null
            && (target.WasInhibited
                || target.InhibitedTurns > 0
                || target.PendingVigorStepPenalty > 0
                || target.PendingAttackBonus != 0
                || target.PermanentCombatBonus != 0
                || IsMarked(target));

        private bool IsMarked(PvpCardState target)
        {
            foreach (PlayerState state in players)
            {
                foreach (PvpCardState card in state.Board)
                {
                    if (card.Card.HeroClass == HeroClass.Hunter && card.MarkedTarget == target)
                        return true;
                }
            }
            return false;
        }

        private int MarkBonusForTarget(PvpCardState target)
        {
            int best = 0;
            foreach (PlayerState state in players)
            {
                foreach (PvpCardState card in state.Board)
                {
                    if (card.Card.HeroClass == HeroClass.Hunter && card.MarkedTarget == target)
                        best = Math.Max(best, MarkBonusOf(card));
                }
            }
            return best;
        }

        private int MarkBonusOf(PvpCardState hunter) =>
            players[hunter.Owner].Aura == PvpAuraType.Hunter
                ? rules.HunterMarkBonus * 2
                : rules.HunterMarkBonus;

        private void MoveTurnAfter(PvpCardState actor, PvpCardState target)
        {
            int actorIndex = turnOrder.IndexOf(actor);
            if (actorIndex < 0)
                actorIndex = Math.Clamp(turnIndex, 0, turnOrder.Count - 1);
            int targetIndex = turnOrder.IndexOf(target);
            if (targetIndex >= 0)
            {
                turnOrder.RemoveAt(targetIndex);
                if (targetIndex < actorIndex)
                    actorIndex--;
            }
            turnOrder.Insert(Math.Clamp(actorIndex + 1, 0, turnOrder.Count), target);
            turnIndex = Math.Clamp(actorIndex, 0, turnOrder.Count - 1);
        }

        private int RollUniqueInitiative(HashSet<int> used)
        {
            int roll;
            do
            {
                roll = random.NextInclusive(1, rules.InitiativeDieSides);
            }
            while (!used.Add(roll) && used.Count < rules.InitiativeDieSides);
            return roll;
        }

        // --- Validazione input ---

        private void RequirePhase(PvpMatchPhase expected)
        {
            if (Phase != expected)
                throw new PvpActionException($"Azione non valida in fase {Phase}.");
        }

        private static int ValidPlayer(int player)
        {
            if (player is < 0 or > 1)
                throw new PvpActionException("Giocatore non valido.");
            return player;
        }

        private PvpCardState RequireActiveCard(int player)
        {
            RequirePhase(PvpMatchPhase.Battle);
            PvpCardState card = turnOrder[turnIndex];
            if (card.Owner != ValidPlayer(player))
                throw new PvpActionException("Non è il turno di una tua carta.");
            return card;
        }

        private PvpCardState BoardCard(int player, int slot)
        {
            List<PvpCardState> board = players[ValidPlayer(player)].Board;
            if (slot < 0 || slot >= board.Count)
                throw new PvpActionException("Slot carta non valido.");
            return board[slot];
        }

        private PvpCardState RequireEnemyTarget(int player, int targetPlayer, int targetSlot)
        {
            if (targetPlayer != 1 - player)
                throw new PvpActionException("Devi scegliere un bersaglio nemico.");
            PvpCardState target = BoardCard(targetPlayer, targetSlot);
            if (!target.IsActive)
                throw new PvpActionException("Il bersaglio è già eliminato.");
            return target;
        }

        private PvpCardState RequireAllyTarget(int player, int targetPlayer, int targetSlot)
        {
            if (targetPlayer != player)
                throw new PvpActionException("Devi scegliere un bersaglio alleato.");
            PvpCardState target = BoardCard(targetPlayer, targetSlot);
            if (!target.IsActive)
                throw new PvpActionException("Il bersaglio è eliminato.");
            return target;
        }

        private static bool AllEliminated(List<PvpCardState> board)
        {
            foreach (PvpCardState card in board)
            {
                if (card.IsActive)
                    return false;
            }
            return true;
        }

        private static List<CombatCard> CardsOf(List<PvpCardState> board)
        {
            var cards = new List<CombatCard>(board.Count);
            foreach (PvpCardState state in board)
                cards.Add(state.Card);
            return cards;
        }

        private static CombatCard[] CopyLoadout(IReadOnlyList<CombatCard> loadout, string parameterName)
        {
            if (loadout == null || loadout.Count < 1)
                throw new ArgumentException("Loadout vuoto.", parameterName);
            var copy = new CombatCard[loadout.Count];
            for (int index = 0; index < loadout.Count; index++)
                copy[index] = loadout[index] ?? throw new ArgumentException("Carta nulla nel loadout.", parameterName);
            return copy;
        }
    }
}
