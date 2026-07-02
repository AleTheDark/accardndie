using System.Collections.Generic;
using AccardND.GameCore.Pvp;
using AccardND.GameData;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class PvpLoadoutValidatorTests
    {
        private static PvpLoadoutCard Card(int value, string suffix = "a") =>
            new($"card-{value}-{suffix}", value);

        private static List<PvpLoadoutCard> NineCardsOfValue(int value)
        {
            var cards = new List<PvpLoadoutCard>();
            for (int index = 0; index < 9; index++)
                cards.Add(Card(value, index.ToString()));
            return cards;
        }

        [Test]
        public void Validate_AcceptsLegalLoadoutAndComputesCosts()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var cards = new List<PvpLoadoutCard>
            {
                Card(10), Card(9), Card(7), Card(5), Card(4),
                Card(3), Card(3, "b"), Card(2), Card(2, "b")
            };
            // Carte = 45, D4 base = 4, bag D6+D8 = 6 → totale 55.
            var loadout = new PvpLoadout(cards, baseDieSides: 4, bagDiceSides: new[] { 6, 8 });

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.CardsCost, Is.EqualTo(45));
            Assert.That(result.BaseDieCost, Is.EqualTo(4));
            Assert.That(result.BagCost, Is.EqualTo(6));
            Assert.That(result.TotalCost, Is.EqualTo(55));
        }

        [Test]
        public void Validate_BaseD3IsFreeAndBagIsOptional()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var loadout = new PvpLoadout(NineCardsOfValue(2), baseDieSides: 3);

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.BaseDieCost, Is.EqualTo(0));
            Assert.That(result.BagCost, Is.EqualTo(0));
            Assert.That(result.TotalCost, Is.EqualTo(18));
        }

        [Test]
        public void Validate_RejectsBudgetExceeded()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var cards = new List<PvpLoadoutCard>
            {
                Card(10), Card(9), Card(9, "b"), Card(8), Card(8, "b"),
                Card(8, "c"), Card(7), Card(7, "b"), Card(7, "c")
            };
            // Carte = 73 > 60 anche con D3 gratis.
            var loadout = new PvpLoadout(cards, baseDieSides: 3);

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasError(PvpLoadoutErrorCode.BudgetExceeded), Is.True);
            Assert.That(result.TotalCost, Is.EqualTo(73));
        }

        [Test]
        public void Validate_RejectsWrongCardCount()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var loadout = new PvpLoadout(
                new List<PvpLoadoutCard> { Card(2), Card(3), Card(4) }, baseDieSides: 3);

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasError(PvpLoadoutErrorCode.WrongCardCount), Is.True);
        }

        [Test]
        public void Validate_RejectsTooManyHighValueCards()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var cards = new List<PvpLoadoutCard>
            {
                Card(9), Card(9, "b"), Card(9, "c"),
                Card(2), Card(2, "b"), Card(2, "c"),
                Card(2, "d"), Card(2, "e"), Card(2, "f")
            };
            var loadout = new PvpLoadout(cards, baseDieSides: 3);

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasError(PvpLoadoutErrorCode.TooManyCardsOfValue), Is.True);
        }

        [Test]
        public void Validate_LowValueCardsHaveNoCopyLimit()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var loadout = new PvpLoadout(NineCardsOfValue(6), baseDieSides: 3);

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.TotalCost, Is.EqualTo(54));
        }

        [Test]
        public void Validate_RejectsUnknownDice()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var loadout = new PvpLoadout(
                NineCardsOfValue(2), baseDieSides: 20, bagDiceSides: new[] { 7 });

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasError(PvpLoadoutErrorCode.InvalidBaseDie), Is.True);
            Assert.That(result.HasError(PvpLoadoutErrorCode.InvalidBagDie), Is.True);
        }

        [Test]
        public void Validate_RejectsCardValueOutOfRange()
        {
            var rules = PvpLoadoutRules.CreateDefault();
            var cards = NineCardsOfValue(2);
            cards[0] = new PvpLoadoutCard("card-broken", 11);
            var loadout = new PvpLoadout(cards, baseDieSides: 3);

            PvpLoadoutValidationResult result = PvpLoadoutValidator.Validate(loadout, rules);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.HasError(PvpLoadoutErrorCode.CardValueOutOfRange), Is.True);
        }

        [Test]
        public void PvpConfiguration_DefaultsMatchRuleDocument()
        {
            var configuration = new PvpConfiguration();

            PvpLoadoutRules rules = configuration.ToLoadoutRules();

            Assert.That(rules.Budget, Is.EqualTo(60));
            Assert.That(rules.RequiredCardCount, Is.EqualTo(9));
            Assert.That(rules.TryGetCardCountLimit(10, out int limitTen) && limitTen == 1, Is.True);
            Assert.That(rules.TryGetCardCountLimit(7, out int limitSeven) && limitSeven == 4, Is.True);
            Assert.That(rules.TryGetCardCountLimit(6, out _), Is.False);
            Assert.That(rules.TryGetBaseDieCost(3, out int d3Cost) && d3Cost == 0, Is.True);
            Assert.That(rules.TryGetBaseDieCost(8, out int d8Cost) && d8Cost == 13, Is.True);
            Assert.That(rules.TryGetBagDieCost(20, out int d20Cost) && d20Cost == 18, Is.True);
            Assert.That(rules.TryGetBagDieCost(4, out _), Is.False);
        }
    }
}
