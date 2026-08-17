using AccardND.GameCore.Mana;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class ManaPoolTests
    {
        [Test]
        public void StartRun_SetsInitialReserve()
        {
            var pool = new ManaPool();
            Assert.That(pool.Current, Is.EqualTo(3));
        }

        [Test]
        public void Gain_NeverExceedsMaximum()
        {
            var pool = new ManaPool();
            pool.Gain(20);
            Assert.That(pool.Current, Is.EqualTo(10));
        }

        [Test]
        public void StartRound_RaisesToFloor_WhenBelow()
        {
            var pool = new ManaPool();
            pool.Spend(3);
            Assert.That(pool.Current, Is.EqualTo(0));

            pool.StartRound();
            Assert.That(pool.Current, Is.EqualTo(2));
        }

        [Test]
        public void StartRound_KeepsReserve_WhenAboveFloor()
        {
            var pool = new ManaPool();
            pool.Gain(5);
            pool.StartRound();
            Assert.That(pool.Current, Is.EqualTo(8), "il mana persiste fra stanze e round");
        }

        // --- Le abilita' base hanno un costo fisso ---

        [Test]
        public void PrimaryCost_IsTheListPrice()
        {
            var pool = new ManaPool();
            Assert.That(pool.CostOfPrimary(HeroClass.Mage), Is.EqualTo(2));
        }

        [Test]
        public void WarriorPrimary_CostsFiveMana()
        {
            var pool = new ManaPool();
            Assert.That(pool.CostOfPrimary(HeroClass.Warrior), Is.EqualTo(5));
        }

        [Test]
        public void SpendingMana_NeverRaisesTheCostOfAnotherPrimary()
        {
            // Il bug storico: una spesa qualsiasi caricava un sovrapprezzo su tutte le
            // abilita' base successive, comprese quelle delle altre pedine della squadra.
            var pool = new ManaPool();
            pool.Gain(10);

            pool.Spend(pool.CostOfPrimary(HeroClass.Mage));

            Assert.That(pool.CostOfPrimary(HeroClass.Mage), Is.EqualTo(2));
            Assert.That(pool.CostOfPrimary(HeroClass.Hunter), Is.EqualTo(2));
            Assert.That(pool.CostOfPrimary(HeroClass.Warrior), Is.EqualTo(5));
        }

        [Test]
        public void PrimaryAbility_DoesNotRaiseTheSupremeCost()
        {
            var pool = new ManaPool();
            pool.Gain(10);

            pool.Spend(pool.CostOfPrimary(HeroClass.Mage));

            Assert.That(pool.CostOfSupreme(HeroClass.Mage), Is.EqualTo(4), "solo le supreme si incrementano");
        }

        [Test]
        public void SupremeSpend_DoesNotIncreasePrimaryCost()
        {
            var pool = new ManaPool();
            pool.Gain(10);

            pool.Spend(pool.CostOfSupreme(HeroClass.Mage));
            pool.RegisterSupremeUse(HeroClass.Mage);

            Assert.That(pool.CostOfPrimary(HeroClass.Mage), Is.EqualTo(2));
        }

        // --- Sovrapprezzo di ripetizione di classe ---

        [Test]
        public void SupremeRepeat_EscalatesCumulatively()
        {
            var pool = new ManaPool();
            Assert.That(pool.CostOfSupreme(HeroClass.Mage), Is.EqualTo(4));

            pool.RegisterSupremeUse(HeroClass.Mage);
            Assert.That(pool.CostOfSupreme(HeroClass.Mage), Is.EqualTo(5));

            pool.RegisterSupremeUse(HeroClass.Mage);
            Assert.That(pool.CostOfSupreme(HeroClass.Mage), Is.EqualTo(6));
        }

        [Test]
        public void SupremeRepeat_IsPerClass_NotShared()
        {
            var pool = new ManaPool();
            pool.RegisterSupremeUse(HeroClass.Mage);

            Assert.That(pool.CostOfSupreme(HeroClass.Mage), Is.EqualTo(5));
            Assert.That(pool.CostOfSupreme(HeroClass.Hunter), Is.EqualTo(4));
        }

        [Test]
        public void StartRound_PreservesSupremeEscalation()
        {
            var pool = new ManaPool();
            pool.RegisterSupremeUse(HeroClass.Mage);
            pool.StartRound();

            Assert.That(pool.CostOfSupreme(HeroClass.Mage), Is.EqualTo(5));
        }

        // --- Riserva del Paladino ---

        [TestCase(2, 4)]
        [TestCase(3, 3)]
        [TestCase(4, 2)]
        [TestCase(5, 1)]
        public void PaladinReserve_NetGain_MatchesDesignTable(int before, int expectedNet)
        {
            var pool = new ManaPool();
            pool.Restore(before);
            int cost = pool.CostOfSupreme(HeroClass.Paladin);

            pool.Spend(cost);
            pool.RaiseTo(pool.Rules.PaladinReserveThreshold);

            Assert.That(pool.Current - before, Is.EqualTo(expectedNet));
        }

        [Test]
        public void PaladinReserve_IsALoss_AboveThreshold()
        {
            var pool = new ManaPool();
            pool.Restore(8);

            pool.Spend(pool.CostOfSupreme(HeroClass.Paladin));
            pool.RaiseTo(pool.Rules.PaladinReserveThreshold);

            Assert.That(pool.Current, Is.EqualTo(6), "sopra la soglia e' una perdita secca");
        }

        [Test]
        public void PaladinReserve_ConvergesToZeroGain_ThroughRepeatSurcharge()
        {
            // Il documento di design sostiene che la Riserva non ha bisogno di un limite
            // d'uso perche' il +1 cumulativo la spegne da sola. Questo lo verifica.
            var pool = new ManaPool();
            int threshold = pool.Rules.PaladinReserveThreshold;

            int[] expectedNet = { 4, 3, 2, 1, 0 };
            for (int use = 0; use < expectedNet.Length; use++)
            {
                int cost = pool.CostOfSupreme(HeroClass.Paladin);
                // Il caso migliore per il Paladino: lanciarla con esattamente il costo in cassa.
                pool.Restore(cost);
                int before = pool.Current;

                pool.Spend(cost);
                pool.RaiseTo(threshold);
                pool.RegisterSupremeUse(HeroClass.Paladin);

                Assert.That(
                    pool.Current - before,
                    Is.EqualTo(expectedNet[use]),
                    $"guadagno netto al lancio numero {use + 1}");
            }
        }

        // --- Tabella dei costi ---

        [Test]
        public void PaladinSupreme_IsTheOnlyOneBelowTheBand()
        {
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
            {
                int cost = AbilityManaCosts.Supreme(heroClass);
                if (heroClass == HeroClass.Paladin)
                    Assert.That(cost, Is.EqualTo(2));
                else
                    Assert.That(cost, Is.InRange(2, 8), $"suprema di {heroClass} fuori dalla fascia 2-8");
            }
        }

        [Test]
        public void WarriorSupreme_CostsSixMana()
        {
            Assert.That(AbilityManaCosts.Supreme(HeroClass.Warrior), Is.EqualTo(6));
        }

        [Test]
        public void PrimaryAbilities_StayInBand()
        {
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
                Assert.That(
                    AbilityManaCosts.Primary(heroClass),
                    Is.InRange(0, 4),
                    $"prima abilita' di {heroClass} fuori fascia");
        }

        [Test]
        public void PassiveClasses_CostNothing()
        {
            // Barbaro e Ladro non hanno un'attivazione: le loro abilita' sono passive.
            Assert.That(AbilityManaCosts.Primary(HeroClass.Barbarian), Is.EqualTo(0));
            Assert.That(AbilityManaCosts.Primary(HeroClass.Rogue), Is.EqualTo(0));
        }

        [Test]
        public void AssassinInhibit_CostsThreeMana()
        {
            Assert.That(AbilityManaCosts.Primary(HeroClass.Assassin), Is.EqualTo(3));
        }

		[Test]
		public void SharedActionPolicy_DefinesActivatableClassesAndActivationRewards()
		{
			ManaRules rules = ManaRules.CreateDefault();
			foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
				Assert.That(
					ManaActionPolicy.HasActivatablePrimary(heroClass),
					Is.EqualTo(AbilityManaCosts.Primary(heroClass) > 0),
					$"attivabilita' incoerente per {heroClass}");

			Assert.That(ManaActionPolicy.ActivationReward(rules, false, false), Is.EqualTo(rules.GainOnActivation));
			Assert.That(ManaActionPolicy.ActivationReward(rules, true, false), Is.EqualTo(rules.GainOnSkip));
			Assert.That(ManaActionPolicy.ActivationReward(rules, true, true), Is.Zero);
			Assert.That(ManaActionPolicy.AttackCost(rules), Is.EqualTo(rules.AttackCost));
		}

        [Test]
        public void NecromancerSupreme_IsImplementedAtEightMana()
        {
            Assert.That(AbilityManaCosts.IsSupremeImplemented(HeroClass.Necromancer), Is.True);
            Assert.That(AbilityManaCosts.Supreme(HeroClass.Necromancer), Is.EqualTo(8));
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
                Assert.That(AbilityManaCosts.IsSupremeImplemented(heroClass), Is.True);
        }

        [Test]
        public void Spend_Throws_WhenNotAffordable()
        {
            var pool = new ManaPool();
            Assert.That(pool.CanAfford(4), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => pool.Spend(4));
        }
    }
}
