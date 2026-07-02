using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore.Pvp;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    public sealed class PvpHandDealerTests
    {
        [Test]
        public void DealFirstHand_SplitsDeckIntoHandAndUnseen()
        {
            var random = new SeededRandomSource(1234);

            PvpFirstDeal deal = PvpHandDealer.DealFirstHand(random, deckSize: 9, handSize: 6);

            Assert.That(deal.HandIndices, Has.Count.EqualTo(6));
            Assert.That(deal.UnseenIndices, Has.Count.EqualTo(3));
            var all = deal.HandIndices.Concat(deal.UnseenIndices).ToList();
            Assert.That(all, Is.Unique);
            Assert.That(all, Is.All.InRange(0, 8));
        }

        [Test]
        public void BuildSecondHand_CombinesUnseenAndUndeployedCards()
        {
            // Mano 1: 0,1,2,3,4,5 — mai viste: 6,7,8 — schierate: 0,2,4.
            List<int> secondHand = PvpHandDealer.BuildSecondHand(
                new[] { 6, 7, 8 },
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { 0, 2, 4 });

            Assert.That(secondHand, Is.EquivalentTo(new[] { 1, 3, 5, 6, 7, 8 }));
        }

        [Test]
        public void BuildSecondHand_RejectsDeployedCardsOutsideFirstHand()
        {
            Assert.Throws<System.ArgumentException>(() => PvpHandDealer.BuildSecondHand(
                new[] { 6, 7, 8 },
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { 0, 2, 7 }));
        }

        [Test]
        public void TryValidateDecisiveSelection_AcceptsThreeDistinctCardsFromDeck()
        {
            bool valid = PvpHandDealer.TryValidateDecisiveSelection(
                deckSize: 9, chosenIndices: new[] { 0, 4, 8 }, requiredCount: 3, out string error);

            Assert.That(valid, Is.True);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void TryValidateDecisiveSelection_RejectsDuplicatesWrongCountAndOutOfRange()
        {
            Assert.That(PvpHandDealer.TryValidateDecisiveSelection(9, new[] { 0, 0, 1 }, 3, out _), Is.False);
            Assert.That(PvpHandDealer.TryValidateDecisiveSelection(9, new[] { 0, 1 }, 3, out _), Is.False);
            Assert.That(PvpHandDealer.TryValidateDecisiveSelection(9, new[] { 0, 1, 9 }, 3, out _), Is.False);
        }

        [Test]
        public void RollOff_RerollsTiesUntilWinnerEmerges()
        {
            // 4-4 è pareggio: deve ritirare e produrre 2 vs 6.
            var random = new FixedRandomSource(4, 4, 2, 6);

            PvpInitiativeResult result = PvpInitiative.RollOff(random, 20);

            Assert.That(result.FirstPlayerRoll, Is.EqualTo(2));
            Assert.That(result.SecondPlayerRoll, Is.EqualTo(6));
            Assert.That(result.FirstPlayerStarts, Is.False);
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
                return values.Dequeue();
            }
        }
    }
}
