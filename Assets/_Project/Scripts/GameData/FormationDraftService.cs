using System;
using System.Collections.Generic;
using AccardND.GameCore;

namespace AccardND.GameData
{
    public sealed class FormationDraftService
    {
        private readonly IRandomSource random;

        public FormationDraftService(IRandomSource random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public List<CardDefinition> DrawCandidates(
            IReadOnlyList<CardDefinition> allCards,
            int count)
        {
            return DrawCandidates(allCards, count, CardCategory.Monster);
        }

        public List<CardDefinition> DrawBossCandidates(
            IReadOnlyList<CardDefinition> allCards,
            int count)
        {
            if (allCards == null)
                throw new ArgumentNullException(nameof(allCards));
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count));

            List<CardDefinition> bosses = BuildPool(allCards, CardCategory.Boss);
            List<CardDefinition> monsters = BuildPool(allCards, CardCategory.Monster);
            Shuffle(bosses);
            Shuffle(monsters);

            var result = new List<CardDefinition>(count);
            for (int index = 0; index < bosses.Count && result.Count < count; index++)
                result.Add(bosses[index]);
            for (int index = 0; index < monsters.Count && result.Count < count; index++)
                result.Add(monsters[index]);

            if (result.Count == 0)
                throw new InvalidOperationException("Il database non contiene Boss o Mostri utilizzabili in combattimento.");
            return result;
        }

        private List<CardDefinition> DrawCandidates(
            IReadOnlyList<CardDefinition> allCards,
            int count,
            CardCategory category)
        {
            if (allCards == null)
                throw new ArgumentNullException(nameof(allCards));

            List<CardDefinition> pool = BuildPool(allCards, category);

            if (count < 1 || count > pool.Count)
                throw new ArgumentOutOfRangeException(nameof(count));

            Shuffle(pool);

            return pool.GetRange(0, count);
        }

        private static List<CardDefinition> BuildPool(
            IReadOnlyList<CardDefinition> allCards,
            CardCategory category)
        {
            var pool = new List<CardDefinition>();
            foreach (CardDefinition card in allCards)
            {
                if (card == null || card.Category != category || !card.CanEnterCombat)
                    continue;

                if (CardPurchaseUniqueness.ContainsEquivalent(card, pool))
                    continue;

                pool.Add(card);
            }
            return pool;
        }

        private void Shuffle(List<CardDefinition> pool)
        {
            for (int index = pool.Count - 1; index > 0; index--)
            {
                int otherIndex = random.NextInclusive(0, index);
                (pool[index], pool[otherIndex]) = (pool[otherIndex], pool[index]);
            }
        }
    }
}
