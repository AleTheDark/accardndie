using System;
using System.Collections.Generic;

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
            public int[] DecisiveChoice;
            public PvpAuraType Aura;
            public bool NecromancerSpiritUsed;
            public bool FormationAuraUsed;
            public bool MightAuraUsedThisCycle;
            public readonly List<PvpCardState> Board = new();
            public int RoundWins;
        }

        private readonly PvpMatchRules rules;
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
            IRandomSource random)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            resolver = new CombatResolver(random);
            players[0].Loadout = CopyLoadout(loadoutPlayer0, nameof(loadoutPlayer0));
            players[1].Loadout = CopyLoadout(loadoutPlayer1, nameof(loadoutPlayer1));
        }

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
                    int steps = players[player].Aura == PvpAuraType.Mage ? 2 : 1;
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
            return events;
        }

        public IReadOnlyList<PvpEvent> Attack(int player, int targetSlot)
        {
            PvpCardState attacker = RequireActiveCard(player);
            int enemy = 1 - player;
            PvpCardState defender = BoardCard(enemy, targetSlot);
            if (!defender.IsActive)
                throw new PvpActionException("Il bersaglio è già eliminato.");

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
            RequireActiveCard(player);
            var events = new List<PvpEvent>();
            EndTurn(events);
            return events;
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
            foreach (PlayerState state in players)
            {
                state.Board.Clear();
                state.Aura = PvpAuraType.None;
                state.NecromancerSpiritUsed = false;
                state.FormationAuraUsed = false;
                state.MightAuraUsedThisCycle = false;
                state.DecisiveChoice = null;
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
                Phase = PvpMatchPhase.DecisiveSelection;
                events.Add(new DecisiveSelectionStartedEvent(rules.DecisiveHandSize));
            }
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
                events.Add(new TurnStartedEvent(card.Owner, card.Slot, cycle));
                return;
            }
        }

        private void EndTurn(List<PvpEvent> events)
        {
            PvpCardState card = turnOrder[turnIndex];
            ExpireSpiritIfNeeded(card, events);
            if (CheckRoundEnd(events))
                return;
            BeginTurn(events, advanceFirst: true);
        }

        private void ExpireSpiritIfNeeded(PvpCardState card, List<PvpEvent> events)
        {
            if (!card.IsSpirit)
                return;
            card.IsSpirit = false;
            card.Eliminated = true;
            events.Add(new SpiritExpiredEvent(card.Owner, card.Slot));
        }

        private void AdvanceTurnIndex()
        {
            turnIndex++;
            if (turnIndex < turnOrder.Count)
                return;
            turnIndex = 0;
            cycle++;
            players[0].MightAuraUsedThisCycle = false;
            players[1].MightAuraUsedThisCycle = false;
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
                    attackerFlatBonus: counterFlatBonus)
                : BuildAttackModifiers(attacker, defender, defenderAdvantage);

            CombatCertainty certainty = CombatCertaintyCalculator.Evaluate(
                attacker.Card, defender.Card, attackerDie, defenderDie, modifiers);

            bool defenderLostLife = false;
            var emptyRoll = default(VigorRollResult);
            VigorRollResult attackerRoll = emptyRoll;
            VigorRollResult defenderRoll = emptyRoll;
            int attackerTotal = 0;
            int defenderTotal = 0;

            if (certainty == CombatCertainty.Guaranteed)
            {
                defenderLostLife = true;
                ConsumeWarriorArm(attacker, modifiers);
            }
            else if (certainty == CombatCertainty.RollRequired)
            {
                CombatResult result = resolver.ResolveAttack(
                    attacker.Card, defender.Card, attackerDie, defenderDie, modifiers);
                ConsumeWarriorArm(attacker, modifiers);
                attackerRoll = result.AttackerRoll;
                defenderRoll = result.DefenderRoll;
                attackerTotal = result.AttackerTotal;
                defenderTotal = result.DefenderTotal;
                defenderLostLife = result.DefenderIsDefeated;
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
                    defender.Lives--;
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
                ApplyPostAttackState(attacker, defenderLostLife, events);

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
                isCounter));
        }

        private CombatModifiers BuildAttackModifiers(
            PvpCardState attacker, PvpCardState defender, bool defenderAdvantage)
        {
            PlayerState attackerTeam = players[attacker.Owner];
            int attackerFlat = attacker.PendingAttackBonus + attacker.PermanentCombatBonus;
            if (attackerTeam.Aura == PvpAuraType.Warrior
                && attacker.Card.HeroClass == HeroClass.Warrior
                && attacker.AbilityArmed)
                attackerFlat++;
            attackerFlat += MarkBonusForTarget(defender);

            int defenderFlat = defender.PermanentCombatBonus + defender.PendingDefenseBonus;

            bool forceAdvantage = attackerTeam.Aura == PvpAuraType.Cunning
                && HeroClassFamily.Of(attacker.Card.HeroClass) == ClassFamily.Cunning
                && IsMarkedOrInhibited(defender);

            bool neutralize = false;
            if (attackerTeam.Aura == PvpAuraType.Formation
                && !attackerTeam.FormationAuraUsed
                && ClassMatchup.Compare(attacker.Card.HeroClass, defender.Card.HeroClass) == MatchupResult.Disadvantage)
            {
                neutralize = true;
                attackerTeam.FormationAuraUsed = true;
            }

            return new CombatModifiers(
                sumAttackerVigor: attacker.AbilityArmed && attacker.Card.HeroClass == HeroClass.Warrior,
                defenderAdvantage: defenderAdvantage,
                rerollAttackerOnes: rules.RogueRerollsOnes && attacker.Card.HeroClass == HeroClass.Rogue,
                rerollAttackerTwos: attackerTeam.Aura == PvpAuraType.Rogue && attacker.Card.HeroClass == HeroClass.Rogue,
                attackerFlatBonus: attackerFlat,
                defenderFlatBonus: defenderFlat,
                neutralizeAttackerMatchup: neutralize,
                forceAttackerAdvantage: forceAdvantage);
        }

        private void ApplyPostAttackState(PvpCardState attacker, bool defeatedTarget, List<PvpEvent> events)
        {
            attacker.PendingAttackBonus = 0;
            attacker.PendingBonusKind = PvpPendingBonusKind.None;
            PlayerState team = players[attacker.Owner];
            if (team.Aura == PvpAuraType.Might
                && !team.MightAuraUsedThisCycle
                && HeroClassFamily.Of(attacker.Card.HeroClass) == ClassFamily.Might
                && !defeatedTarget)
            {
                team.MightAuraUsedThisCycle = true;
                attacker.PermanentCombatBonus++;
                events.Add(new MightAuraBonusEvent(attacker.Owner, attacker.Slot));
            }
            else if (attacker.Card.HeroClass == HeroClass.Barbarian && !defeatedTarget)
            {
                int fury = team.Aura == PvpAuraType.Barbarian
                    ? rules.BarbarianRageBonus + 1
                    : rules.BarbarianRageBonus;
                attacker.PendingAttackBonus = fury;
                attacker.PendingBonusKind = PvpPendingBonusKind.Fury;
                events.Add(new FuryGainedEvent(attacker.Owner, attacker.Slot, fury));
            }
        }

        private void ConsumeWarriorArm(PvpCardState attacker, CombatModifiers modifiers)
        {
            if (!modifiers.SumAttackerVigor)
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

        private bool IsMarkedOrInhibited(PvpCardState target) =>
            target.WasInhibited || target.InhibitedTurns > 0 || IsMarked(target);

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
