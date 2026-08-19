using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore.Pvp;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class PvpMatchEngineTests
    {
        private static CombatCard Card(HeroClass heroClass, int strength, string id) =>
            new(id, id, heroClass, strength);

        /// <summary>Loadout con carte forti (10) agli indici indicati e deboli (1) altrove.</summary>
        private static List<CombatCard> MixedLoadout(string prefix, params int[] strongIndices)
        {
            var strong = new HashSet<int>(strongIndices);
            var cards = new List<CombatCard>();
            for (int index = 0; index < 9; index++)
                cards.Add(Card(HeroClass.Warrior, strong.Contains(index) ? 10 : 1, $"{prefix}-{index}"));
            return cards;
        }

        private static List<CombatCard> UniformLoadout(string prefix, HeroClass heroClass, int strength)
        {
            var cards = new List<CombatCard>();
            for (int index = 0; index < 9; index++)
                cards.Add(Card(heroClass, strength, $"{prefix}-{index}"));
            return cards;
        }

        // Shuffle identità per entrambi i mazzi round 1 (Fisher-Yates che scambia ogni indice con sé stesso).
        private static IEnumerable<int> IdentityShuffles()
        {
            for (int player = 0; player < 2; player++)
                for (int index = 8; index >= 1; index--)
                    yield return index;
        }

        // Token di schieramento: ordine crescente per deploy, poi gli stessi
        // valori vengono usati in ordine decrescente per la battaglia.
        private static IEnumerable<int> DeploymentAndInitiatives(int[] player0Initiatives, int[] player1Initiatives)
        {
            foreach (int initiative in player0Initiatives)
            {
                yield return initiative;
                yield return 1;
            }
            foreach (int initiative in player1Initiatives)
            {
                yield return initiative;
                yield return 1;
            }
        }

        private static FixedRandomSource QueueFor(params IEnumerable<int>[] parts) =>
            new(parts.SelectMany(part => part).ToArray());

        private static List<PvpEvent> DeployAll(PvpMatchEngine engine, int[] player0HandPicks, int[] player1HandPicks)
        {
            var events = new List<PvpEvent>();
            int[] next = { 0, 0 };
            while (engine.Phase == PvpMatchPhase.Deployment)
            {
                int player = engine.ActivePlayer;
                int[] picks = player == 0 ? player0HandPicks : player1HandPicks;
                events.AddRange(engine.Deploy(player, picks[next[player]]));
                next[player]++;
            }
            return events;
        }

        /// <summary>Porta un match con carte forti P0 / deboli P1 a inizio battaglia round 1.</summary>
        private static PvpMatchEngine BattleReadyEngine(
            List<CombatCard> loadout0,
            List<CombatCard> loadout1,
            out List<PvpEvent> events,
            PvpMatchRules rules = null)
        {
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                Enumerable.Repeat(3, 400)); // riserva per i tiri di attacco dei test
            var engine = new PvpMatchEngine(loadout0, loadout1, rules ?? PvpMatchRules.CreateDefault(), random);
            events = new List<PvpEvent>(engine.Start());
            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));
            return engine;
        }

        [Test]
        public void FullMatch_DeterministicBestOfThree_ThirdRoundUsesSurvivors()
        {
            // P0: carte forti agli indici 0-2. P1: forti agli indici 6-8 (arrivano solo nella mano del round 2).
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                DeploymentAndInitiatives(new[] { 6, 5, 4 }, new[] { 20, 19, 18 }),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }));
            var engine = new PvpMatchEngine(
                MixedLoadout("p0", 0, 1, 2),
                MixedLoadout("p1", 6, 7, 8),
                PvpMatchRules.CreateDefault(),
                random);

            var events = new List<PvpEvent>(engine.Start());

            // Round 1: P0 schiera le forti (testa della mano), P1 le deboli. Tutti attacchi certi: nessun tiro.
            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));
            events.AddRange(DriveBattle(engine));
            Assert.That(engine.MatchRound, Is.EqualTo(2), "il round 1 deve chiudersi 1-0");
            Assert.That(engine.WinsOf(0), Is.EqualTo(1));

            // Round 2: la mano di P1 è 3 mai viste (6,7,8 forti) + 3 non schierate.
            Assert.That(engine.HandOf(1), Is.EquivalentTo(new[] { 3, 4, 5, 6, 7, 8 }));
            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 3, 3, 3 }));
            events.AddRange(DriveBattle(engine));
            Assert.That(engine.WinsOf(1), Is.EqualTo(1), "il round 2 va a P1");

            // Round 3: nessuna selezione manuale, si schiera come sempre. La mano è
            // composta dalle sole carte mai morte: P0 ha perso il round 2 con {3,4,5},
            // P1 il round 1 con {0,1,2}, quindi restano rispettivamente 6 carte a testa.
            Assert.That(engine.MatchRound, Is.EqualTo(3));
            Assert.That(engine.Phase, Is.EqualTo(PvpMatchPhase.Deployment));
            Assert.That(engine.HandOf(0), Is.EquivalentTo(new[] { 0, 1, 2, 6, 7, 8 }));
            Assert.That(engine.HandOf(1), Is.EquivalentTo(new[] { 3, 4, 5, 6, 7, 8 }));

            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));
            events.AddRange(DriveBattle(engine));

            Assert.That(engine.Phase, Is.EqualTo(PvpMatchPhase.Finished));
            Assert.That(engine.MatchWinner, Is.EqualTo(0));
            Assert.That(engine.WinsOf(0), Is.EqualTo(2));
            Assert.That(engine.WinsOf(1), Is.EqualTo(1));
            Assert.That(events.OfType<MatchEndedEvent>().Single().Winner, Is.EqualTo(0));
            Assert.That(events.OfType<RoundEndedEvent>().Count(), Is.EqualTo(3));
        }

        private static List<PvpEvent> DriveBattle(PvpMatchEngine engine)
        {
            var events = new List<PvpEvent>();
            int guard = 0;
            while (engine.Phase == PvpMatchPhase.Battle && guard++ < 500)
            {
                int player = engine.ActivePlayer;
                int enemy = 1 - player;
                int target = FirstActiveSlot(engine, enemy);
                events.AddRange(engine.Attack(player, target));
            }
            Assert.That(guard, Is.LessThan(500), "la battaglia non termina");
            return events;
        }

        private static int FirstActiveSlot(PvpMatchEngine engine, int player)
        {
            IReadOnlyList<PvpCardState> board = engine.BoardOf(player);
            for (int slot = 0; slot < board.Count; slot++)
            {
                if (board[slot].IsActive)
                    return slot;
            }
            return 0;
        }

        [Test]
        public void Attack_DeployedCardsHaveTwoLives()
        {
            // Forza 5 contro forza 5: l'attaccante vince lo scambio ma non
            // raddoppia il difensore (9 contro 6), quindi niente Overkill e la
            // carta da 2 vite ne perde una alla volta.
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                new[] { 4, 1, 4, 1 }, // due scambi: 5+4=9 vs 5+1=6 (9 < 12: nessun Overkill)
                Enumerable.Repeat(3, 50));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Warrior, 5),
                PvpMatchRules.CreateDefault(),
                random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });

            // Primo colpo: perde una vita ma resta in gioco.
            var first = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
            Assert.That(first.Overkill, Is.False);
            Assert.That(first.DefenderLostLife, Is.True);
            Assert.That(first.DefenderRemainingLives, Is.EqualTo(1));
            Assert.That(first.DefenderEliminated, Is.False);

            // Secondo colpo sulla stessa carta: eliminata.
            var second = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
            Assert.That(second.Overkill, Is.False);
            Assert.That(second.DefenderRemainingLives, Is.EqualTo(0));
            Assert.That(second.DefenderEliminated, Is.True);
            Assert.That(engine.BoardOf(1)[0].Eliminated, Is.True);
        }

        [Test]
        public void Attack_OverkillRemovesBothLivesInOneHit()
        {
            // Regola PvP: se l'attaccante totalizza almeno il doppio del
            // difensore, la carta perde entrambe le vite in un colpo solo.
            var engine = BattleReadyEngine(
                UniformLoadout("p0", HeroClass.Warrior, 10),
                UniformLoadout("p1", HeroClass.Warrior, 1),
                out _);

            // Forza 10 (+3 dado = 13) contro forza 1 (+3 dado = 4): 13 >= 2*4.
            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
            Assert.That(attack.Overkill, Is.True);
            Assert.That(attack.DefenderLostLife, Is.True);
            Assert.That(attack.DefenderRemainingLives, Is.EqualTo(0));
            Assert.That(attack.DefenderEliminated, Is.True);
            Assert.That(engine.BoardOf(1)[0].Eliminated, Is.True);
        }

        [Test]
        public void MightAura_WhenAnyPawnDiesBoostsAllActiveMightAuraCards()
        {
            var engine = BattleReadyEngine(
                UniformLoadout("p0", HeroClass.Warrior, 10),
                UniformLoadout("p1", HeroClass.Warrior, 1),
                out var setupEvents);
            Assert.That(setupEvents.OfType<BattleStartedEvent>().Single().AuraPlayer0,
                Is.EqualTo(PvpAuraType.Might));
            Assert.That(setupEvents.OfType<BattleStartedEvent>().Single().AuraPlayer1,
                Is.EqualTo(PvpAuraType.Might));

            var events = engine.Attack(0, 0).ToList();
            Assert.That(events.OfType<AttackResolvedEvent>().Single().DefenderEliminated, Is.True);

            CollectionAssert.AreEquivalent(
                new[] { (0, 0), (0, 1), (0, 2), (1, 1), (1, 2) },
                events.OfType<MightAuraBonusEvent>().Select(e => (e.Player, e.Slot)));
            Assert.That(engine.BoardOf(0)[0].PermanentCombatBonus, Is.EqualTo(1));
            Assert.That(engine.BoardOf(0)[1].PermanentCombatBonus, Is.EqualTo(1));
            Assert.That(engine.BoardOf(0)[2].PermanentCombatBonus, Is.EqualTo(1));
            Assert.That(engine.BoardOf(1)[0].PermanentCombatBonus, Is.EqualTo(0));
            Assert.That(engine.BoardOf(1)[1].PermanentCombatBonus, Is.EqualTo(1));
            Assert.That(engine.BoardOf(1)[2].PermanentCombatBonus, Is.EqualTo(1));
        }

        [Test]
        public void Battle_UsesDeploymentInitiativesInDescendingOrderWithoutReroll()
        {
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 10, 6 }, new[] { 19, 9, 5 }),
                Enumerable.Repeat(3, 40));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Warrior, 5),
                PvpMatchRules.CreateDefault(),
                random);

            var events = new List<PvpEvent>(engine.Start());
            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));

            Assert.That(events.OfType<CardInitiativeEvent>(), Is.Empty);
            CollectionAssert.AreEquivalent(new[] { 20, 10, 6 }, engine.BoardOf(0).Select(card => card.Initiative));
            CollectionAssert.AreEquivalent(new[] { 19, 9, 5 }, engine.BoardOf(1).Select(card => card.Initiative));
            Assert.That(engine.ActiveCard.Owner, Is.EqualTo(0));
            Assert.That(engine.ActiveCard.Initiative, Is.EqualTo(20));
        }

        [Test]
        public void PriestBlessing_AddsBonusToNextAttackOnly()
        {
            // P0: Priest, Priest, Warrior -> fazioni Magic+Might: nessuna aura.
            var loadout0 = UniformLoadout("p0", HeroClass.Priest, 5);
            loadout0[2] = Card(HeroClass.Warrior, 5, "p0-war");
            var engine = BattleReadyEngine(loadout0, UniformLoadout("p1", HeroClass.Warrior, 5), out _);

            // Turno del primo Priest (iniziativa 20): benedice il Warrior (slot 2),
            // non puo passare e deve concludere con un attacco.
            PvpCardState actingPriest = engine.ActiveCard;
            var bless = engine.UseAbility(0, 0, 2).OfType<AbilityUsedEvent>().Single();
            Assert.That(bless.Magnitude, Is.EqualTo(2));
            Assert.That(actingPriest.AbilityUsedThisTurn, Is.True);
            Assert.Throws<PvpActionException>(() => engine.Pass(0));
            engine.Attack(0, 1);
            Assert.That(actingPriest.AbilityUsedThisTurn, Is.False);
            engine.Pass(0); // secondo Priest

            // Warrior benedetto attacca: matchup neutro, un dado a testa (3 e 3 dalla riserva).
            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().First(e => !e.IsCounter);
            Assert.That(attack.AttackerTotal, Is.EqualTo(5 + 3 + 2), "forza + dado + benedizione");
            Assert.That(attack.DefenderTotal, Is.EqualTo(5 + 3));
        }

        [Test]
        public void PriestBlessing_CleansesEveryMalusFromBlessedAlly()
        {
            var loadout0 = UniformLoadout("p0", HeroClass.Priest, 5);
            loadout0[2] = Card(HeroClass.Warrior, 5, "p0-war");
            var engine = BattleReadyEngine(loadout0, UniformLoadout("p1", HeroClass.Hunter, 5), out _);
            PvpCardState ally = engine.BoardOf(0)[2];
            PvpCardState enemyHunter = engine.BoardOf(1)[0];
            ally.InhibitedTurns = 1;
            ally.WasInhibited = true;
            ally.PendingVigorStepPenalty = 2;
            ally.PermanentCombatBonus = -3;
            enemyHunter.MarkedTarget = ally;

            engine.UseAbility(0, 0, 2);

            Assert.That(ally.InhibitedTurns, Is.Zero);
            Assert.That(ally.WasInhibited, Is.False);
            Assert.That(ally.PendingVigorStepPenalty, Is.Zero);
            Assert.That(ally.PermanentCombatBonus, Is.Zero);
            Assert.That(enemyHunter.MarkedTarget, Is.Null);
            Assert.That(ally.PendingAttackBonus, Is.EqualTo(2));
            Assert.That(ally.PendingBonusKind, Is.EqualTo(PvpPendingBonusKind.Blessing));
        }

        [Test]
        public void MarkedChampion_AttackingDoesNotApplyOrConsumeTheMark()
        {
            var engine = BattleReadyEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Hunter, 5),
                out _);
            PvpCardState markedAttacker = engine.ActiveCard;
            PvpCardState enemyHunter = engine.BoardOf(1)[0];
            enemyHunter.MarkedTarget = markedAttacker;

            var attack = engine.Attack(markedAttacker.Owner, enemyHunter.Slot)
                .OfType<AttackResolvedEvent>()
                .First(e => !e.IsCounter);

            Assert.That(attack.AttackerTotal, Is.EqualTo(5 + 3),
                "il Marchio sull'attaccante non deve modificare il suo totale");
            Assert.That(attack.DefenderTotal, Is.EqualTo(5 + 3),
                "il difensore non deve ricevere il bonus del Marchio posto sull'attaccante");
            Assert.That(enemyHunter.MarkedTarget, Is.SameAs(markedAttacker),
                "il Marchio si consuma solo quando la pedina marchiata difende");
        }

        [Test]
        public void WarriorAura_ConsidersPriestBlessingInEffectiveStrength()
        {
            var engine = BattleReadyEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Warrior, 5),
                out _);
            PvpCardState attacker = engine.ActiveCard;
            attacker.PendingAttackBonus = 2;
            attacker.PendingBonusKind = PvpPendingBonusKind.Blessing;

            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().First(e => !e.IsCounter);

            Assert.That(attack.AttackerTotal, Is.EqualTo(5 + 3 + 2));
            Assert.That(attack.DefenderTotal, Is.EqualTo(5 + 3 + 2),
                "l'aura deve vedere il +2 della Benedizione che rende l'attaccante piu forte");
        }

        [Test]
        public void WarriorAura_DoesNotTriggerWhenEquipmentAlreadyMakesWarriorStronger()
        {
            var engine = BattleReadyEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Warrior, 6),
                out _);
            engine.ActiveCard.PermanentCombatBonus = 3;

            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().First(e => !e.IsCounter);

            Assert.That(attack.AttackerTotal, Is.EqualTo(5 + 3 + 3),
                "il Guerriero e' gia' piu forte grazie all'equipaggiamento e non deve ottenere l'aura");
            Assert.That(attack.DefenderTotal, Is.EqualTo(6 + 3));
        }

        [Test]
        public void AssassinInhibition_SkipsTargetTurnAndEnablesCunningSynergy()
        {
            // P0: Assassin, Assassin, Warrior (Cunning+Might: nessuna aura).
            var loadout0 = UniformLoadout("p0", HeroClass.Assassin, 5);
            loadout0[2] = Card(HeroClass.Warrior, 5, "p0-war");
            var engine = BattleReadyEngine(loadout0, UniformLoadout("p1", HeroClass.Warrior, 5), out _);

            engine.UseAbility(0, 1, 0); // inibisce la carta nemica slot 0 (iniziativa 6)
            Assert.That(engine.BoardOf(1)[0].InhibitedTurns, Is.EqualTo(1));
            Assert.That(engine.BoardOf(1)[0].WasInhibited, Is.True);

            Assert.Throws<PvpActionException>(() => engine.Pass(0));
            engine.Attack(0, 1);
            engine.Pass(0);
            var events = engine.Pass(0); // chiude il giro dei P0: tocca al nemico slot 0, che salta.
            var skipped = events.OfType<TurnSkippedEvent>().Single();
            Assert.That(skipped.Player, Is.EqualTo(1));
            Assert.That(skipped.Slot, Is.EqualTo(0));
            Assert.That(engine.BoardOf(1)[0].InhibitedTurns, Is.EqualTo(0));
        }

        [Test]
        public void CunningAura_AttacksWithAdvantageAgainstEnemiesWithBonusOrMalus()
        {
            // P0: Rogue, Assassin, Hunter -> aura fazione Astuzia.
            var loadout0 = UniformLoadout("p0", HeroClass.Rogue, 5);
            loadout0[1] = Card(HeroClass.Assassin, 5, "p0-assassin");
            loadout0[2] = Card(HeroClass.Hunter, 5, "p0-hunter");
            var loadout1 = UniformLoadout("p1", HeroClass.Warrior, 5);
            loadout1[0] = Card(HeroClass.Warrior, 2, "p1-attachment-source");
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 21, 5, 4 }),
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(loadout0, loadout1, PvpMatchRules.CreateDefault(), random);
            var events = new List<PvpEvent>(engine.Start());
            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));

            Assert.That(events.OfType<BattleStartedEvent>().Single().AuraPlayer0, Is.EqualTo(PvpAuraType.Cunning));
            engine.Attach(1, 1);
            Assert.That(engine.BoardOf(1)[1].PermanentCombatBonus, Is.EqualTo(3));

            var attack = engine.Attack(0, 1).OfType<AttackResolvedEvent>().First(e => !e.IsCounter);
            Assert.That(attack.AttackerRoll.Matchup, Is.EqualTo(MatchupResult.Advantage));
            Assert.That(attack.AttackerRoll.SelectionMode, Is.EqualTo(VigorSelectionMode.Highest));
            Assert.That(attack.AttackerRoll.HasSecondRoll, Is.True);
        }

        [Test]
        public void RogueAura_RerollsTheDefenderDieUpToTheRoundThreshold()
        {
            // Round 1: dado D4, quindi il Ladro ritira solo l'1.
            // Guerriero contro Ladro e' vantaggio: l'attaccante tira due dadi (3 e 2,
            // tiene 3 -> totale 8), il difensore tira 1 (totale 6) e lo ritira in 4.
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                new[] { 3, 2, 1, 4 },
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Rogue, 5),
                PvpMatchRules.CreateDefault(),
                random);
            var events = new List<PvpEvent>(engine.Start());
            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));

            Assert.That(events.OfType<BattleStartedEvent>().Single().AuraPlayer1, Is.EqualTo(PvpAuraType.Rogue));

            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
            Assert.That(attack.DefenderRoll.FirstRollBeforeReroll, Is.EqualTo(1));
            Assert.That(attack.DefenderRoll.FirstRoll, Is.EqualTo(4));
            Assert.That(attack.DefenderRoll.HasSecondRoll, Is.False);
            Assert.That(attack.DefenderLostLife, Is.False, "il reroll porta la difesa a 9 contro 8");
        }

        [Test]
        public void RogueAura_DoesNotRerollADefenderDieAboveTheRoundThreshold()
        {
            // Stessa scena, ma il difensore tira 2: al round 1 la soglia e' 1, quindi
            // il 2 resta e la carta incassa il colpo.
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                new[] { 3, 2, 2 },
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Rogue, 5),
                PvpMatchRules.CreateDefault(),
                random);
            var events = new List<PvpEvent>(engine.Start());
            events.AddRange(DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 }));

            Assert.That(events.OfType<BattleStartedEvent>().Single().AuraPlayer1, Is.EqualTo(PvpAuraType.Rogue));

            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
            Assert.That(attack.DefenderRoll.FirstRollBeforeReroll, Is.Zero);
            Assert.That(attack.DefenderRoll.FirstRoll, Is.EqualTo(2));
            Assert.That(attack.DefenderLostLife, Is.True);
        }

        [Test]
        public void RogueReroll_ThresholdFollowsTheVigorDieOfTheRound()
        {
            // In PvP non ci sono livelli: la soglia di rilancio del Ladro segue il dado
            // Vigore del round (D4 -> 1, D6 -> 2, D8 -> 3), cioe' il round fa da livello.
            // Duello 1 contro 1 con una vita sola: ogni round si chiude al primo colpo
            // andato a segno e la coda dei dadi resta leggibile.
            var rules = new PvpMatchRules(
                handSize: 6,
                formationSize: 1,
                decisiveHandSize: 1,
                roundsToWin: 2,
                cardLives: 1,
                vigorDieByRound: new[] { 4, 6, 8 },
                initiativeDieSides: 20,
                rogueRerollsOnes: true,
                barbarianRageBonus: 2,
                hunterMarkBonus: 2,
                priestBlessingBonus: 2);
            var random = QueueFor(
                IdentityShuffles(),
                new[] { 20, 1, 10, 1 }, // iniziative round 1: muove prima P0
                new[] { 2, 3 },         // R1 P0: il 2 e' sopra la soglia 1, nessun reroll
                new[] { 2, 4 },         // R1 P1: parata
                new[] { 1, 3, 4 },      // R1 P0: l'1 diventa 4 e uccide
                new[] { 5, 1, 15, 1 },  // iniziative round 2: muove prima P1
                new[] { 3, 4 },         // R2 P1: il 3 e' sopra la soglia 2, nessun reroll
                new[] { 1, 5, 1 },      // R2 P0: ritira ma non basta
                new[] { 2, 3, 6 },      // R2 P1: il 2 diventa 6 e uccide
                new[] { 15, 1, 5, 1 },  // iniziative round 3: muove prima P0
                new[] { 4, 5 },         // R3 P0: il 4 e' sopra la soglia 3, nessun reroll
                new[] { 4, 5 },         // R3 P1: parata
                new[] { 3, 5, 8 });     // R3 P0: il 3 diventa 8 e uccide
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Rogue, 5),
                UniformLoadout("p1", HeroClass.Rogue, 5),
                rules,
                random);
            engine.Start();

            // Round 1, D4: si ritira solo l'1.
            DeployAll(engine, new[] { 0 }, new[] { 0 });
            Assert.That(engine.MatchRound, Is.EqualTo(1));
            AssertReroll(NextAttack(engine), before: 0, after: 2, "round 1: il 2 non si ritira");
            NextAttack(engine);
            AssertReroll(NextAttack(engine), before: 1, after: 4, "round 1: l'1 si ritira");

            // Round 2, D6: la soglia sale a 2.
            Assert.That(engine.MatchRound, Is.EqualTo(2));
            DeployAll(engine, new[] { 0 }, new[] { 0 });
            AssertReroll(NextAttack(engine), before: 0, after: 3, "round 2: il 3 non si ritira");
            NextAttack(engine);
            AssertReroll(NextAttack(engine), before: 2, after: 6, "round 2: il 2 si ritira");

            // Round 3, D8: la soglia sale a 3.
            Assert.That(engine.MatchRound, Is.EqualTo(3));
            DeployAll(engine, new[] { 0 }, new[] { 0 });
            AssertReroll(NextAttack(engine), before: 0, after: 4, "round 3: il 4 non si ritira");
            NextAttack(engine);
            AssertReroll(NextAttack(engine), before: 3, after: 8, "round 3: il 3 si ritira");

            Assert.That(engine.Phase, Is.EqualTo(PvpMatchPhase.Finished));
            Assert.That(engine.MatchWinner, Is.EqualTo(0));
        }

        private static AttackResolvedEvent NextAttack(PvpMatchEngine engine) =>
            engine.Attack(engine.ActivePlayer, 0).OfType<AttackResolvedEvent>().Single();

        private static void AssertReroll(AttackResolvedEvent attack, int before, int after, string message)
        {
            Assert.That(attack.AttackerRoll.FirstRollBeforeReroll, Is.EqualTo(before), message);
            Assert.That(attack.AttackerRoll.FirstRoll, Is.EqualTo(after), message);
        }

        [Test]
        public void MageAbility_LowersEnemyVigorDieForOneExchange()
        {
            var loadout0 = UniformLoadout("p0", HeroClass.Mage, 5);
            loadout0[2] = Card(HeroClass.Warrior, 5, "p0-war");
            // P1 tutto Might per non avere aura Magic in difesa.
            var engine = BattleReadyEngine(loadout0, UniformLoadout("p1", HeroClass.Warrior, 5), out _);

            engine.UseAbility(0, 1, 0); // -1 step al nemico slot 0
            Assert.That(engine.BoardOf(1)[0].PendingVigorStepPenalty, Is.EqualTo(1));
            Assert.Throws<PvpActionException>(() => engine.Pass(0));
            engine.Attack(0, 1);
            engine.Pass(0);

            // Il Warrior P0 (slot 2) attacca il bersaglio indebolito: D4 -> D2 in difesa.
            // Mage(P0) vs Warrior: Magic batte Might = vantaggio, quindi passiamo al turno del Warrior...
            // slot 2 è Warrior: matchup neutro.
            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().First(e => !e.IsCounter);
            Assert.That(attack.DefenderDieSides, Is.EqualTo(2));
            Assert.That(attack.AttackerDieSides, Is.EqualTo(4));
            // La penalità si consuma con lo scambio.
            Assert.That(engine.BoardOf(1)[0].PendingVigorStepPenalty, Is.EqualTo(0));
        }

        [Test]
        public void MageAbility_PenaltySurvivesImpossibleAttack()
        {
            var engine = BattleReadyEngine(
                UniformLoadout("p0", HeroClass.Mage, 1),
                UniformLoadout("p1", HeroClass.Warrior, 20),
                out _);

            engine.UseAbility(0, 1, 0);
            Assert.That(engine.BoardOf(1)[0].PendingVigorStepPenalty, Is.EqualTo(1));

            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single(e => !e.IsCounter);

            Assert.That(attack.Certainty, Is.EqualTo(CombatCertainty.Impossible));
            Assert.That(engine.BoardOf(1)[0].PendingVigorStepPenalty, Is.EqualTo(1),
                "senza confronto il malus al dado Vigore deve restare attivo");
        }

        [Test]
        public void MageAbility_StackedPenaltyLowersD6ToD2AndStopsAtMinimum()
        {
            Assert.That(PvpVigorScale.StepsToMinimum(6), Is.EqualTo(2));
            Assert.That(PvpVigorScale.LowerBySteps(6, 2), Is.EqualTo(2));
            Assert.That(PvpVigorScale.LowerBySteps(6, 3), Is.EqualTo(2));
        }

        [Test]
        public void BarbarianFury_StacksOnDefeatAndBoostsDefense()
        {
            var loadout0 = UniformLoadout("p0", HeroClass.Barbarian, 5);
            loadout0[2] = Card(HeroClass.Priest, 5, "p0-priest"); // niente aura (Might+Magic)
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                new[] { 1, 4 },   // attacco fallito del Barbarian: 5+1 vs 5+4
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                loadout0, UniformLoadout("p1", HeroClass.Warrior, 5), PvpMatchRules.CreateDefault(), random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
            engine.BoardOf(0)[0].PendingAttackBonus = 2;
            engine.BoardOf(0)[0].PendingBonusKind = PvpPendingBonusKind.Fury;

            var events = engine.Attack(0, 0);
            var fury = events.OfType<FuryGainedEvent>().Single();
            Assert.That(fury.Amount, Is.EqualTo(2));
            Assert.That(engine.BoardOf(0)[0].PendingAttackBonus, Is.EqualTo(4));
            Assert.That(engine.BoardOf(0)[0].PendingDefenseBonus, Is.EqualTo(4), "la Furia cumulata vale anche in difesa");
        }

        /// <summary>
        /// La regia del client riproduce gli eventi nell'ordine in cui il motore li
        /// accoda: se la Furia precede l'AttackResolved, il Barbaro anima la passiva
        /// prima ancora dell'attacco che la fa scattare (in campagna arriva dopo).
        /// </summary>
        [Test]
        public void BarbarianFury_IsAnnouncedAfterTheAttackThatTriggersIt()
        {
            // Tutto Barbarian di qua cosi' chiunque sia l'attivo si infuria, difensori
            // piu' forti di la': con tutti i dadi a 3 fa 5+3 contro 7+3 e l'attacco fallisce.
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Barbarian, 5),
                UniformLoadout("p1", HeroClass.Priest, 7),
                PvpMatchRules.CreateDefault(),
                random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });

            List<PvpEvent> events = engine.Attack(0, 0).ToList();
            int attackIndex = events.FindIndex(e => e is AttackResolvedEvent);
            int furyIndex = events.FindIndex(e => e is FuryGainedEvent);
            Assert.That(attackIndex, Is.GreaterThanOrEqualTo(0), "manca l'evento del confronto");
            Assert.That(furyIndex, Is.GreaterThanOrEqualTo(0), "manca l'evento della Furia");
            Assert.That(furyIndex, Is.GreaterThan(attackIndex),
                "la Furia deve seguire l'attacco fallito, non precederlo");
        }

        [Test]
        public void BarbarianFury_IsDischargedOnSuccessfulDefense()
        {
            var loadout1 = UniformLoadout("p1", HeroClass.Barbarian, 5);
            loadout1[2] = Card(HeroClass.Priest, 5, "p1-priest"); // niente aura Barbarian
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                new[] { 1, 4 },   // attacco fallito: 5+1 vs Barbarian 5+4
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5), loadout1, PvpMatchRules.CreateDefault(), random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });
            engine.BoardOf(1)[0].PendingAttackBonus = 2;
            engine.BoardOf(1)[0].PendingBonusKind = PvpPendingBonusKind.Fury;

            var events = engine.Attack(0, 0);
            Assert.That(events.OfType<FuryGainedEvent>(), Is.Empty);
            Assert.That(engine.BoardOf(1)[0].PendingAttackBonus, Is.Zero);
            Assert.That(engine.BoardOf(1)[0].PendingDefenseBonus, Is.Zero);
            Assert.That(engine.BoardOf(1)[0].PendingBonusKind, Is.EqualTo(PvpPendingBonusKind.None));
        }

        [Test]
        public void Pass_AfterAbilityEndsTurnNormally()
        {
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5),
                UniformLoadout("p1", HeroClass.Warrior, 5),
                PvpMatchRules.CreateDefault(),
                random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });

            PvpCardState actor = engine.ActiveCard;
            int manaAfterAbility;
            engine.UseAbility(actor.Owner, actor.Owner, actor.Slot);
			manaAfterAbility = engine.ManaOf(actor.Owner);

            Assert.That(actor.AbilityUsedThisTurn, Is.True);
            Assert.DoesNotThrow(() => engine.Pass(actor.Owner));
            Assert.That(engine.ActiveCard, Is.Not.SameAs(actor));
            Assert.That(actor.AbilityUsedThisTurn, Is.False);
			Assert.That(engine.ManaOf(actor.Owner), Is.EqualTo(manaAfterAbility),
				"Saltare dopo avere usato un'abilita' conclude il turno senza generare mana.");
        }

		[Test]
		public void PrimaryAbility_IsReusableOnNextActivationWhenManaIsAvailable()
		{
			var random = QueueFor(
				IdentityShuffles(),
				DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
				Enumerable.Repeat(3, 100));
			var engine = new PvpMatchEngine(
				UniformLoadout("p0", HeroClass.Hunter, 5),
				UniformLoadout("p1", HeroClass.Warrior, 5),
				PvpMatchRules.CreateDefault(),
				random);
			IReadOnlyList<PvpEvent> startEvents = engine.Start();

			Assert.That(startEvents.OfType<ManaChangedEvent>().Select(e => e.Current),
				Is.EquivalentTo(new[] { 3, 3 }), "Il mana iniziale deve essere sincronizzato anche con delta zero.");
			DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });

			PvpCardState hunter = engine.ActiveCard;
			engine.UseAbility(hunter.Owner, 1 - hunter.Owner, 0);
			engine.Pass(hunter.Owner);
			for (int turn = 0; turn < 5; turn++)
				engine.Pass(engine.ActivePlayer);

			Assert.That(engine.ActiveCard, Is.SameAs(hunter));
			Assert.That(hunter.AbilityUsed, Is.False);
			Assert.DoesNotThrow(() => engine.UseAbility(hunter.Owner, 1 - hunter.Owner, 1));
			Assert.That(hunter.HunterMarkedTargets, Has.Count.EqualTo(2));
			Assert.That(hunter.HunterMarkedTargets, Does.Contain(engine.BoardOf(1 - hunter.Owner)[0]));
			Assert.That(hunter.HunterMarkedTargets, Does.Contain(engine.BoardOf(1 - hunter.Owner)[1]));
		}

        [Test]
        public void WarriorAbility_SumsTwoVigorDiceOnNextAttack()
        {
            var loadout0 = UniformLoadout("p0", HeroClass.Warrior, 5);
            loadout0[2] = Card(HeroClass.Priest, 5, "p0-priest");
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                new[] { 2, 3, 1 }, // due dadi attaccante (2+3) + difesa 1
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                loadout0, UniformLoadout("p1", HeroClass.Warrior, 5), PvpMatchRules.CreateDefault(), random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });

            PvpCardState actingWarrior = engine.ActiveCard;
            engine.UseAbility(0, 0, 0); // arma la somma dadi
            Assert.That(actingWarrior.AbilityUsedThisTurn, Is.True);
            Assert.Throws<PvpActionException>(() => engine.Pass(0));
            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
            Assert.That(actingWarrior.AbilityUsedThisTurn, Is.False);
            Assert.That(attack.AttackerRoll.SelectionMode, Is.EqualTo(VigorSelectionMode.Sum));
            Assert.That(attack.AttackerTotal, Is.EqualTo(5 + 2 + 3));
            Assert.That(engine.BoardOf(0)[0].AbilityUsed, Is.True, "abilità consumata dall'attacco");
        }

        [Test]
        public void PaladinProtection_RedirectsAttackAndDefendsWithAdvantage()
        {
            // P1: Paladin, Warrior, Warrior (tutti Might ma classi diverse -> aura Might, non Paladin).
            var loadout1 = UniformLoadout("p1", HeroClass.Warrior, 5);
            loadout1[0] = Card(HeroClass.Paladin, 5, "p1-pala");
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 6, 5, 4 }, new[] { 20, 19, 18 }), // P1 agisce prima
                new[] { 2, 3, 6 }, // attacco P0: dado 2; difesa paladino con vantaggio: 3 e 6
                Enumerable.Repeat(3, 100));
            var engine = new PvpMatchEngine(
                UniformLoadout("p0", HeroClass.Warrior, 5), loadout1, PvpMatchRules.CreateDefault(), random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });

            // Turno del Paladin (iniziativa 20): protegge l'alleato slot 1 e poi attacca.
            engine.UseAbility(1, 1, 1);
            Assert.Throws<PvpActionException>(() => engine.Pass(1));
            engine.Attack(1, 0);
            engine.Pass(1); // gli altri due P1 passano
            engine.Pass(1);

            // Ora attacca P0 contro il Warrior protetto (slot 1): il colpo devia sul Paladin.
            var events = engine.Attack(0, 1);
            var protection = events.OfType<ProtectionTriggeredEvent>().Single();
            Assert.That(protection.Redirected, Is.True);
            var attack = events.OfType<AttackResolvedEvent>().Single();
            Assert.That(attack.DefenderSlot, Is.EqualTo(0), "il Paladin prende il posto del bersaglio");
            Assert.That(attack.DefenderRoll.HasSecondRoll, Is.True, "difesa con vantaggio: due dadi");
            Assert.That(attack.DefenderRoll.SelectedRoll, Is.EqualTo(6));
            Assert.That(engine.BoardOf(1)[0].AbilityUsed, Is.True);
        }

        [Test]
        public void NecromancerAura_FirstDeathBecomesSpiritThenExpires()
        {
            // P1: 3 Necromancer -> aura di classe Necromancer.
            var engine = BattleReadyEngine(
                MixedLoadout("p0", 0, 1, 2),
                UniformLoadout("p1", HeroClass.Necromancer, 1),
                out var setupEvents);
            Assert.That(setupEvents.OfType<BattleStartedEvent>().Single().AuraPlayer1,
                Is.EqualTo(PvpAuraType.Necromancer));

            // Due eliminazioni certe sulla stessa carta: alla seconda diventa Spirito.
            engine.Attack(0, 0);
            var spirit = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
            Assert.That(spirit.BecameSpirit, Is.True);
            Assert.That(spirit.DefenderEliminated, Is.False);
            Assert.That(engine.BoardOf(1)[0].IsSpirit, Is.True);

            // P0 slot 2 passa; il turno arriva allo Spirito (iniziativa 6) che agisce e poi svanisce.
            engine.Pass(0);
            Assert.That(engine.ActiveCard.IsSpirit, Is.True);

            var supremeError = Assert.Throws<PvpActionException>(() => engine.UseSupreme(1, 0, 0));
            Assert.That(supremeError.ErrorCode, Is.EqualTo(PvpActionErrorCodes.SpiritActionForbidden));

            var attachmentError = Assert.Throws<PvpActionException>(() => engine.Attach(1, 1));
            Assert.That(attachmentError.ErrorCode, Is.EqualTo(PvpActionErrorCodes.SpiritActionForbidden));

            var expiry = engine.Pass(1);
            Assert.That(expiry.OfType<SpiritExpiredEvent>().Single().Slot, Is.EqualTo(0));
            Assert.That(engine.BoardOf(1)[0].Eliminated, Is.True);
        }

        [Test]
        public void Attachment_SacrificesLowCardToBoostAlly()
        {
            var loadout0 = UniformLoadout("p0", HeroClass.Warrior, 2);
            loadout0[2] = Card(HeroClass.Priest, 5, "p0-priest");
            var engine = BattleReadyEngine(loadout0, UniformLoadout("p1", HeroClass.Warrior, 5), out _);

            var events = engine.Attach(0, 2); // il Warrior da 2 si sacrifica per il Priest
            var attach = events.OfType<AttachmentAppliedEvent>().Single();
            Assert.That(attach.Bonus, Is.EqualTo(3));
            Assert.That(engine.BoardOf(0)[0].Eliminated, Is.True);
            Assert.That(engine.BoardOf(0)[0].IsAttachment, Is.True);
            Assert.That(engine.BoardOf(0)[2].PermanentCombatBonus, Is.EqualTo(3));
        }

        [Test]
        public void NecromancerAbility_RevivesAllyWithOneLife()
        {
            // P0: Necromancer forte + deboli (niente aura: c'è un Warrior); P1 ha un Warrior forte.
            var loadout0 = UniformLoadout("p0", HeroClass.Necromancer, 1);
            loadout0[0] = Card(HeroClass.Necromancer, 10, "p0-necro");
            loadout0[2] = Card(HeroClass.Warrior, 1, "p0-war");
            var loadout1 = UniformLoadout("p1", HeroClass.Warrior, 1);
            loadout1[0] = Card(HeroClass.Warrior, 10, "p1-war");
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 6, 5 }, new[] { 19, 4, 3 }),
                Enumerable.Repeat(3, 200));
            var engine = new PvpMatchEngine(loadout0, loadout1, PvpMatchRules.CreateDefault(), random);
            engine.Start();
            DeployAll(engine, new[] { 0, 0, 0 }, new[] { 0, 0, 0 });

            // Ordine turni: p0s0(20), p1s0(19), p0s1(6), p0s2(5), p1s1(4), p1s2(3).
            // Il Warrior forte P1 colpisce due volte (nei suoi due turni) il debole P0 slot 1.
            engine.Pass(0);      // Necromancer attende
            engine.Attack(1, 1); // prima vita
            engine.Pass(0);      // p0 slot 1 (ancora vivo)
            engine.Pass(0);      // p0 slot 2
            engine.Pass(1);      // p1 slot 1
            engine.Pass(1);      // p1 slot 2 - fine ciclo 1
            engine.Pass(0);      // ciclo 2: Necromancer attende ancora
            engine.Attack(1, 1); // seconda vita: eliminato
            Assert.That(engine.BoardOf(0)[1].Eliminated, Is.True);
            engine.Pass(0);      // p0 slot 2 (slot 1 saltato perché eliminato)
            engine.Pass(1);      // p1 slot 1
            engine.Pass(1);      // p1 slot 2 - fine ciclo 2

            // Turno del Necromancer: rialza l'alleato con 1 vita, che agisce subito dopo.
            var events = engine.UseAbility(0, 0, 1);
            var revived = events.OfType<CardRevivedEvent>().Single();
            Assert.That(revived.Lives, Is.EqualTo(1));
            Assert.That(engine.BoardOf(0)[1].Eliminated, Is.False);
            engine.Pass(0);
            Assert.That(engine.ActiveCard.Slot, Is.EqualTo(1), "la carta rialzata agisce dopo il Necromancer");
            Assert.That(engine.ActiveCard.Owner, Is.EqualTo(0));
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            private readonly Queue<int> values;

            public FixedRandomSource(params int[] values)
            {
                this.values = new Queue<int>(values);
            }

            public int NextInclusive(int minimum, int maximum)
            {
                if (values.Count == 0)
                    throw new System.InvalidOperationException("Coda dei tiri esaurita.");
                return values.Dequeue();
            }
        }
    }
}
