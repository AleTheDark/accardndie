using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Presentation;
using UnityEngine;

namespace AccardND.Battlefield
{
    public static class BattlePresentationViewStateMapper
    {
        public static BattlefieldViewState ToBattlefieldViewState(
            BattlePresentationState state,
            CardDatabase database,
            System.Func<int, bool> isTileClickable,
            System.Func<int, bool> isTargetModeSelection,
            System.Func<string, bool> shouldPlayEnterAnimation,
            System.Func<string, bool> hasPendingDeploymentPose)
        {
            if (state == null)
                state = new BattlePresentationState();

            return new BattlefieldViewState
            {
                FormationSize = 3,
                TopCards = BuildCards(
                    state,
                    1 - state.LocalPlayerIndex,
                    BattlefieldSide.Top,
                    database,
                    isTileClickable,
                    isTargetModeSelection,
                    shouldPlayEnterAnimation,
                    hasPendingDeploymentPose),
                BottomCards = BuildCards(
                    state,
                    state.LocalPlayerIndex,
                    BattlefieldSide.Bottom,
                    database,
                    isTileClickable,
                    isTargetModeSelection,
                    shouldPlayEnterAnimation,
                    hasPendingDeploymentPose)
            };
        }

        public static BattlePresentationCard FindBySlot(List<BattlePresentationCard> board, int slot)
        {
            if (board == null)
                return null;

            foreach (BattlePresentationCard card in board)
            {
                if (card.Slot == slot)
                    return card;
            }
            return null;
        }

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
                flags.Add(new PrototypeCardView.StatusToken($"BONUS +{card.PendingBonus}", new Color(0.2f, 1f, 0.45f)));
            return flags;
        }

        public static bool AbilityTargetsEnemy(BattlePresentationCard card) =>
            card != null && card.HeroClass is HeroClass.Assassin or HeroClass.Mage or HeroClass.Hunter;

        public static bool HasActivatableAbility(HeroClass heroClass) =>
            heroClass is not (HeroClass.Rogue or HeroClass.Barbarian);

        public static int PlayerForSide(BattlePresentationState state, BattlefieldSide side) =>
            side == BattlefieldSide.Bottom ? state.LocalPlayerIndex : 1 - state.LocalPlayerIndex;

        private static List<BattlefieldCardViewState> BuildCards(
            BattlePresentationState state,
            int player,
            BattlefieldSide side,
            CardDatabase database,
            System.Func<int, bool> isTileClickable,
            System.Func<int, bool> isTargetModeSelection,
            System.Func<string, bool> shouldPlayEnterAnimation,
            System.Func<string, bool> hasPendingDeploymentPose)
        {
            var cards = new List<BattlefieldCardViewState>();
            if (player < 0 || player >= state.Boards.Length)
                return cards;

            List<BattlePresentationCard> board = state.Boards[player];
            for (int slot = 0; slot < 3; slot++)
            {
                BattlePresentationCard card = FindBySlot(board, slot);
                if (card == null)
                    continue;

                string revealKey = $"{state.MatchRound}:{player}:{card.Slot}:{card.CardId}";
                bool playEnterAnimation = !(hasPendingDeploymentPose?.Invoke(card.CardId) ?? false)
                    && (shouldPlayEnterAnimation?.Invoke(revealKey) ?? false);

                cards.Add(new BattlefieldCardViewState
                {
                    Key = new BattlefieldCardKey(side, slot),
                    Definition = FindCardDefinition(database, card.CardId),
                    Strength = card.Strength + card.PermanentBonus + card.PendingBonus,
                    Lives = Mathf.Max(0, card.Lives),
                    MaximumLives = card.MaximumLives,
                    Eliminated = card.Eliminated,
                    ActiveTurn = state.Phase == BattlePresentationPhase.Battle
                        && state.ActivePlayer == player
                        && state.ActiveSlot == card.Slot,
                    Clickable = !card.Eliminated && (isTileClickable?.Invoke(player) ?? false),
                    Inspectable = true,
                    Selected = isTargetModeSelection?.Invoke(player) ?? false,
                    PlayerOwned = player == state.LocalPlayerIndex,
                    PlayEnterAnimation = playEnterAnimation,
                    EmptyLabel = ShortCardName(card.CardId),
                    Statuses = CardStatuses(card)
                });
            }
            return cards;
        }

        private static CardDefinition FindCardDefinition(CardDatabase database, string definitionId)
        {
            if (database == null || string.IsNullOrWhiteSpace(definitionId))
                return null;

            CardDefinition definition = database.FindById(definitionId);
            return definition != null ? definition : database.FindById(definitionId.Replace('_', '-'));
        }

        private static string ShortCardName(string definitionId) =>
            string.IsNullOrEmpty(definitionId) ? "?" : definitionId.Replace('-', '\n');
    }
}
