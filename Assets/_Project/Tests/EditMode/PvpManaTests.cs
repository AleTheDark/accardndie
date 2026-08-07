using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore.Mana;
using AccardND.GameCore.Pvp;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// Economia del mana dentro il motore PvP: generazione, spesa e persistenza.
    /// Vedi Docs/mana-design.md.
    /// </summary>
    public sealed class PvpManaTests
    {
        private sealed class QueuedRandom : IRandomSource
        {
            private readonly Queue<int> values;

            public QueuedRandom(IEnumerable<int> values) => this.values = new Queue<int>(values);

            public int NextInclusive(int minimum, int maximum) =>
                values.Count > 0 ? values.Dequeue() : minimum;
        }

        private static CombatCard Card(HeroClass heroClass, int strength, string id) =>
            new(id, id, heroClass, strength);

        private static List<CombatCard> Loadout(string prefix, HeroClass heroClass, int strength)
        {
            var cards = new List<CombatCard>();
            for (int index = 0; index < 9; index++)
                cards.Add(Card(heroClass, strength, $"{prefix}-{index}"));
            return cards;
        }

        private static IEnumerable<int> IdentityShuffles()
        {
            for (int player = 0; player < 2; player++)
                for (int index = 8; index >= 1; index--)
                    yield return index;
        }

        private static IEnumerable<int> DeploymentAndInitiatives(int[] first, int[] second)
        {
            foreach (int initiative in first)
            {
                yield return initiative;
                yield return 1;
            }
            foreach (int initiative in second)
            {
                yield return initiative;
                yield return 1;
            }
        }

        /// <summary>Match pronto a inizio battaglia, con P0 che agisce per primo.</summary>
        private static PvpMatchEngine BattleReady(
            List<CombatCard> loadout0,
            List<CombatCard> loadout1,
            IEnumerable<int> combatRolls = null)
        {
            var random = new QueuedRandom(
                IdentityShuffles()
                    .Concat(DeploymentAndInitiatives(new[] { 20, 19, 18 }, new[] { 6, 5, 4 }))
                    .Concat(combatRolls ?? Enumerable.Repeat(3, 400)));
            var engine = new PvpMatchEngine(loadout0, loadout1, PvpMatchRules.CreateDefault(), random);
            engine.Start();
            int[] next = { 0, 0 };
            while (engine.Phase == PvpMatchPhase.Deployment)
            {
                int player = engine.ActivePlayer;
                engine.Deploy(player, 0);
                next[player]++;
            }
            return engine;
        }

        private static PvpMatchEngine WarriorMirror(IEnumerable<int> combatRolls = null) =>
            BattleReady(
                Loadout("p0", HeroClass.Warrior, 5),
                Loadout("p1", HeroClass.Warrior, 5),
                combatRolls);

        private static void BankMana(PvpMatchEngine engine, int player, int required)
        {
            for (int guard = 0; guard < 100; guard++)
            {
                if (engine.ManaOf(player) >= required && engine.ActivePlayer == player)
                    return;
                engine.Pass(engine.ActivePlayer);
            }
            Assert.Fail($"Non sono riuscito a portare il giocatore {player} a {required} mana.");
        }

        // --- Riserva iniziale e generazione ---

        [Test]
        public void BothPlayersStartWithTheRunReserve()
        {
            PvpMatchEngine engine = WarriorMirror();
            Assert.That(engine.ManaOf(0), Is.EqualTo(3));
            Assert.That(engine.ManaOf(1), Is.EqualTo(3));
        }

        [Test]
        public void Attacking_GainsOneManaAtEndOfActivation()
        {
            PvpMatchEngine engine = WarriorMirror();
            int before = engine.ManaOf(0);

            engine.Attack(0, 0);

            Assert.That(engine.ManaOf(0), Is.EqualTo(before + 1));
        }

        [Test]
        public void Skipping_GainsThreeMana()
        {
            PvpMatchEngine engine = WarriorMirror();
            int before = engine.ManaOf(0);

            engine.Pass(0);

            Assert.That(engine.ManaOf(0), Is.EqualTo(before + 3), "saltare recupera tre mana");
        }

        [Test]
        public void Skipping_EmitsManaChangedEventWithSkipReason()
        {
            PvpMatchEngine engine = WarriorMirror();

            ManaChangedEvent change = engine.Pass(0)
                .OfType<ManaChangedEvent>()
                .First(e => e.Player == 0);

            Assert.That(change.Reason, Is.EqualTo(ManaChangeReasons.Skip));
            Assert.That(change.Delta, Is.EqualTo(3));
            Assert.That(change.Current, Is.EqualTo(engine.ManaOf(0)), "Current e' il valore dopo la variazione");
        }

        [Test]
        public void Gain_NeverExceedsTheCap()
        {
            PvpMatchEngine engine = WarriorMirror();
            for (int turn = 0; turn < 40; turn++)
            {
                if (engine.Phase != PvpMatchPhase.Battle)
                    break;
                engine.Pass(engine.ActivePlayer);
            }

            Assert.That(engine.ManaOf(0), Is.LessThanOrEqualTo(10));
            Assert.That(engine.ManaOf(1), Is.LessThanOrEqualTo(10));
        }

        // --- Spesa ---

        [Test]
        public void UsingAnAbility_SpendsItsCost()
        {
            PvpMatchEngine engine = WarriorMirror();
            BankMana(engine, 0, 5);
            int before = engine.ManaOf(0);

            engine.UseAbility(0, 0, 0); // Guerriero: costo 5

            Assert.That(engine.ManaOf(0), Is.EqualTo(before - 5));
        }

        [Test]
        public void UsingAnAbility_EmitsSpendEvent()
        {
            PvpMatchEngine engine = WarriorMirror();
            BankMana(engine, 0, 5);

            ManaChangedEvent spend = engine.UseAbility(0, 0, 0)
                .OfType<ManaChangedEvent>()
                .Single();

            Assert.That(spend.Reason, Is.EqualTo(ManaChangeReasons.Spend));
            Assert.That(spend.Delta, Is.EqualTo(-5));
        }

        [Test]
        public void Ability_IsRejected_WhenManaIsInsufficient()
        {
            // Il Necromante costa 4, la riserva iniziale e' 3.
            PvpMatchEngine engine = BattleReady(
                Loadout("p0", HeroClass.Necromancer, 5),
                Loadout("p1", HeroClass.Warrior, 5));

            var error = Assert.Throws<PvpActionException>(() => engine.UseAbility(0, 0, 0));
            Assert.That(error.ErrorCode, Is.EqualTo(PvpActionErrorCodes.NotEnoughMana));
        }

        [Test]
        public void RejectedAbility_LeavesManaUntouched()
        {
            PvpMatchEngine engine = BattleReady(
                Loadout("p0", HeroClass.Necromancer, 5),
                Loadout("p1", HeroClass.Warrior, 5));
            int before = engine.ManaOf(0);

            Assert.Throws<PvpActionException>(() => engine.UseAbility(0, 0, 0));

            Assert.That(engine.ManaOf(0), Is.EqualTo(before));
        }

        [Test]
        public void PassiveAbilityClass_DoesNotSpendMana()
        {
            // Barbaro e Ladro non hanno un'attivazione nel motore PvP: l'azione
            // viene rifiutata e non deve costare nulla.
            PvpMatchEngine engine = BattleReady(
                Loadout("p0", HeroClass.Barbarian, 5),
                Loadout("p1", HeroClass.Warrior, 5));
            int before = engine.ManaOf(0);

            Assert.Throws<PvpActionException>(() => engine.UseAbility(0, 0, 0));

            Assert.That(engine.ManaOf(0), Is.EqualTo(before));
        }

        [Test]
        public void UsingAPrimaryAbility_DoesNotRaiseTheNextPrimaryCost()
        {
            PvpMatchEngine engine = WarriorMirror();
            BankMana(engine, 0, 5);
            Assert.That(engine.PrimaryCostFor(0), Is.EqualTo(5), "costo a listino");

            engine.UseAbility(0, 0, 0);

            Assert.That(engine.PrimaryCostFor(0), Is.EqualTo(5), "le abilita' base non si incrementano mai");
        }

        [Test]
        public void PrimaryCost_StaysFlatOnTheNextActivation()
        {
            PvpMatchEngine engine = WarriorMirror();
            BankMana(engine, 0, 5);
            engine.UseAbility(0, 0, 0);
            engine.Attack(0, 0); // chiude l'attivazione

            Assert.That(engine.PrimaryCostFor(engine.ActivePlayer), Is.EqualTo(5));
        }

        // --- Parata ed eliminazione ---

        /// <summary>
        /// Forze 5 contro 7 con dadi fissi a 3: il confronto e' RollRequired
        /// (5+4 > 7+1, quindi non Impossible) ma l'attaccante perde 8 a 10.
        /// E' la parata vera, non un attacco che non poteva riuscire.
        /// </summary>
        private static PvpMatchEngine ParryScenario() =>
            BattleReady(
                Loadout("p0", HeroClass.Warrior, 5),
                Loadout("p1", HeroClass.Warrior, 7));

        [Test]
        public void SurvivingAnAttack_GivesTheDefenderParryMana()
        {
            PvpMatchEngine engine = ParryScenario();
            int before = engine.ManaOf(1);

            AttackResolvedEvent attack = engine.Attack(0, 0).OfType<AttackResolvedEvent>().Single();

            Assert.That(attack.Certainty, Is.EqualTo(CombatCertainty.RollRequired));
            Assert.That(attack.DefenderLostLife, Is.False);
            Assert.That(engine.ManaOf(1), Is.EqualTo(before + 1), "chi para guadagna 1");
        }

        [Test]
        public void EverySuccessfulParry_GivesManaToTheDefender()
        {
            PvpMatchEngine engine = ParryScenario();

            engine.Attack(0, 0);
            int afterFirstParry = engine.ManaOf(1);

            // P0 ha iniziativa piu' alta su tutte e tre le pedine, quindi tocca
            // ancora a lui: colpisce lo stesso bersaglio una seconda volta.
            Assert.That(engine.ActivePlayer, Is.EqualTo(0));
            engine.Attack(0, 0);

            Assert.That(
                engine.ManaOf(1),
                Is.EqualTo(afterFirstParry + 1),
                "ogni parata riuscita genera 1 mana");
        }

        [Test]
        public void ImpossibleAttack_DoesNotGenerateParryMana()
        {
            // Attaccante talmente debole che il confronto e' Impossible: nessuno
            // scontro reale, nessun mana da parata.
            PvpMatchEngine engine = BattleReady(
                Loadout("p0", HeroClass.Warrior, 1),
                Loadout("p1", HeroClass.Warrior, 20));
            int before = engine.ManaOf(1);

            engine.Attack(0, 0);

            Assert.That(engine.ManaOf(1), Is.EqualTo(before));
        }

        [Test]
        public void ImpossibleAttack_AutomaticallyRewardsSkipMana()
        {
            PvpMatchEngine engine = BattleReady(
                Loadout("p0", HeroClass.Warrior, 1),
                Loadout("p1", HeroClass.Warrior, 20));
            int before = engine.ManaOf(0);

            IReadOnlyList<PvpEvent> events = engine.Attack(0, 0);

            Assert.That(engine.ManaOf(0), Is.EqualTo(before + 3));
            Assert.That(events.OfType<ManaChangedEvent>().Any(e =>
                e.Player == 0
                && e.Delta == 3
                && e.Reason == ManaChangeReasons.Skip), Is.True);
        }

        /// <summary>
        /// Uccidere non produce mana. L'economia sta su tre voci: +1 parata,
        /// +1 fine attivazione, -1 attacco base (piu' il +3 dello skip).
        /// </summary>
        [Test]
        public void Elimination_DoesNotPayAnyone()
        {
            // P0 schiaccia P1: con 2 vite servono due colpi andati a segno.
            PvpMatchEngine engine = BattleReady(
                Loadout("p0", HeroClass.Warrior, 20),
                Loadout("p1", HeroClass.Warrior, 1));

            int victimBefore = engine.ManaOf(1);
            bool eliminated = false;
            int victimGainsFromKills = 0;

            for (int guard = 0; guard < 12 && engine.Phase == PvpMatchPhase.Battle && !eliminated; guard++)
            {
                if (engine.ActivePlayer != 0)
                {
                    engine.Pass(engine.ActivePlayer);
                    continue;
                }
                IReadOnlyList<PvpEvent> events = engine.Attack(0, 0);
                eliminated = events.OfType<AttackResolvedEvent>().Any(e => e.DefenderEliminated);
                victimGainsFromKills += events
                    .OfType<ManaChangedEvent>()
                    .Count(e => e.Reason == ManaChangeReasons.Kill || e.Reason == ManaChangeReasons.Loss);
            }

            Assert.That(eliminated, Is.True, "il setup deve produrre un'eliminazione");
            Assert.That(victimGainsFromKills, Is.EqualTo(0), "nessun evento di mana da uccisione o perdita");
            Assert.That(engine.ManaOf(1), Is.EqualTo(victimBefore), "chi perde la pedina non guadagna nulla");
        }

        // --- Persistenza ---

        [Test]
        public void ManaPersistsAcrossRounds_AndRisesToTheFloor()
        {
            PvpMatchEngine engine = WarriorMirror();
            engine.UseAbility(0, 0, 0); // 3 -> 0

            Assert.That(engine.ManaOf(0), Is.EqualTo(0));

            engine.Attack(0, 0); // chiude l'attivazione: 0 -> 1
            Assert.That(engine.ManaOf(0), Is.EqualTo(1), "il mana non si azzera a fine attivazione");
        }

        [Test]
        public void SupremeCost_IsExposedForTheActiveCard()
        {
            PvpMatchEngine engine = WarriorMirror();
            Assert.That(engine.SupremeCostFor(0), Is.EqualTo(AbilityManaCosts.Supreme(HeroClass.Warrior)));
        }
    }
}
