using System;
using System.Collections.Generic;

namespace AccardND.GameData
{
    public static class CardPurchaseUniqueness
    {
        public static bool AreEquivalent(CardDefinition left, CardDefinition right)
        {
            if (left == null || right == null)
                return false;
            if (ReferenceEquals(left, right))
                return true;
            if (!string.IsNullOrWhiteSpace(left.Id)
                && string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase))
                return true;
            if (left.Artwork != null && right.Artwork != null && left.Artwork == right.Artwork)
                return true;

            return left.Category == right.Category
                && left.Strength == right.Strength
                && left.HeroClass == right.HeroClass
                && string.Equals(
                    left.DisplayName?.Trim(),
                    right.DisplayName?.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsEquivalent(
            CardDefinition candidate,
            IReadOnlyList<CardDefinition> ownedCards)
        {
            if (candidate == null || ownedCards == null)
                return false;

            foreach (CardDefinition ownedCard in ownedCards)
            {
                if (AreEquivalent(ownedCard, candidate))
                    return true;
            }
            return false;
        }
    }
}
