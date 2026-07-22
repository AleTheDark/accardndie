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
            // P0: Priest, Priest, Warrior -> famiglie Magic+Might: nessuna aura.
            var loadout0 = UniformLoadout("p0", HeroClass.Priest, 5);
            loadout0[2] = Card(HeroClass.Warrior, 5, "p0-war");
            var engine = BattleReadyEngine(loadout0, UniformLoadout("p1", HeroClass.Warrior, 5), out _);

            // Turno del primo Priest (iniziativa 20): benedice il Warrior (slot 2) e passa.
            var bless = engine.UseAbility(0, 0, 2).OfType<AbilityUsedEvent>().Single();
            Assert.That(bless.Magnitude, Is.EqualTo(2));
            engine.Pass(0);
            engine.Pass(0); // secondo Priest

            // Warrior benedetto attacca: matchup neutro, un dado a testa (3 e 3 dalla riserva).
            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().First(e => !e.IsCounter);
            Assert.That(attack.AttackerTotal, Is.EqualTo(5 + 3 + 2), "forza + dado + benedizione");
            Assert.That(attack.DefenderTotal, Is.EqualTo(5 + 3));
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

            engine.Pass(0);
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
            // P0: Rogue, Assassin, Hunter -> aura famiglia Astuzia.
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
        public void RogueAura_RerollsFirstDefenderTwoOncePerExchange()
        {
            var random = QueueFor(
                IdentityShuffles(),
                DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }),
                new[] { 3, 2, 6 },
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
            Assert.That(attack.DefenderRoll.FirstRoll, Is.EqualTo(6));
            Assert.That(attack.DefenderRoll.FirstRollBeforeReroll, Is.EqualTo(2));
            Assert.That(attack.DefenderRoll.HasSecondRoll, Is.False);
            Assert.That(attack.DefenderLostLife, Is.False);
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
            engine.Pass(0);
            engine.Pass(0);

            // Il Warrior P0 (slot 2) attacca il bersaglio indebolito: D4 -> D3 in difesa.
            // Mage(P0) vs Warrior: Magic batte Might = vantaggio, quindi passiamo al turno del Warrior...
            // slot 2 è Warrior: matchup neutro.
            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().First(e => !e.IsCounter);
            Assert.That(attack.DefenderDieSides, Is.EqualTo(3));
            Assert.That(attack.AttackerDieSides, Is.EqualTo(4));
            // La penalità si consuma con lo scambio.
            Assert.That(engine.BoardOf(1)[0].PendingVigorStepPenalty, Is.EqualTo(0));
        }

        [Test]
        public void BarbarianFury_TriggersOnFailedKillAndBoostsDefense()
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

            var events = engine.Attack(0, 0);
            var fury = events.OfType<FuryGainedEvent>().Single();
            Assert.That(fury.Amount, Is.EqualTo(2));
            Assert.That(engine.BoardOf(0)[0].PendingAttackBonus, Is.EqualTo(2));
            Assert.That(engine.BoardOf(0)[0].PendingDefenseBonus, Is.EqualTo(2), "la Furia vale anche in difesa");
        }

        [Test]
        public void BarbarianFury_TriggersOnSuccessfulDefense()
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

            var events = engine.Attack(0, 0);
            var fury = events.OfType<FuryGainedEvent>().Single();
            Assert.That(fury.Player, Is.EqualTo(1));
            Assert.That(fury.Slot, Is.EqualTo(0));
            Assert.That(fury.Amount, Is.EqualTo(2));
            Assert.That(engine.BoardOf(1)[0].PendingAttackBonus, Is.EqualTo(2));
            Assert.That(engine.BoardOf(1)[0].PendingDefenseBonus, Is.EqualTo(2));
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

            engine.UseAbility(0, 0, 0); // arma la somma dadi
            var attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();
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

            // Turno del Paladin (iniziativa 20): protegge l'alleato slot 1 e passa.
            engine.UseAbility(1, 1, 1);
            engine.Pass(1);
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
