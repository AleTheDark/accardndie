using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.Presentation;
using UnityEngine;

namespace AccardND.Battlefield
{
    public static class BattlePresentationViewStateMapper
    {
        public static IReadOnlyList<PrototypeCardView.StatusToken> CardStatuses(BattlePresentationCard card)
        {
            var flags = new List<PrototypeCardView.StatusToken>();
            if (card == null)
                return flags;

            if (card.Initiative > 0)
                flags.Add(new PrototypeCardView.StatusToken($"INI {card.Initiative}", new Color(0.95f, 0.78f, 0.22f)));
            if (card.IsSpirit)
                flags.Add(new PrototypeCardView.StatusToken("SPIRITO", new Color(0.58f, 0.8f, 1f)));
            if (card.Inhibited)
                flags.Add(new PrototypeCardView.StatusToken("INIBITO", new Color(0.56f, 0.42f, 0.92f)));
            if (card.Marked)
                flags.Add(new PrototypeCardView.StatusToken("PREDA MARCATA", new Color(1f, 0.45f, 0.2f)));
            if (card.Protecting)
                flags.Add(new PrototypeCardView.StatusToken("PROTEZIONE", new Color(0.2f, 0.72f, 1f)));
            if (card.DiePenaltySteps > 0)
                flags.Add(new PrototypeCardView.StatusToken($"DADO -{card.DiePenaltySteps}", new Color(0.8f, 0.65f, 1f)));
            if (card.PendingBonus > 0)
                flags.Add(new PrototypeCardView.StatusToken(
                    PendingBonusLabel(card),
                    card.PendingBonusKind == PvpPendingBonusKind.Blessing
                        ? new Color(0.85f, 0.8f, 1f)
                        : new Color(1f, 0.75f, 0.25f)));
            return flags;
        }

        private static string PendingBonusLabel(BattlePresentationCard card) =>
            card.PendingBonusKind switch
            {
                PvpPendingBonusKind.Blessing => $"BENEDIZIONE +{card.PendingBonus}",
                PvpPendingBonusKind.Fury => $"FURIA +{card.PendingBonus}",
                _ => $"BONUS +{card.PendingBonus}"
            };

        public static bool HasActivatableAbility(HeroClass heroClass) =>
            heroClass is not (HeroClass.Rogue or HeroClass.Barbarian);
    }
}
