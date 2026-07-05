using System;
using System.Collections.Generic;

namespace AccardND.GameCore.Pvp
{
    public enum PvpLoadoutErrorCode
    {
        WrongCardCount,
        DuplicateCard,
        CardValueOutOfRange,
        TooManyCardsOfValue,
        InvalidBaseDie,
        InvalidBagDie,
        BudgetExceeded
    }

    public readonly struct PvpLoadoutError
    {
        public PvpLoadoutError(PvpLoadoutErrorCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public PvpLoadoutErrorCode Code { get; }
        public string Message { get; }
    }

    public sealed class PvpLoadoutValidationResult
    {
        private readonly List<PvpLoadoutError> errors;

        internal PvpLoadoutValidationResult(
            int cardsCost, int baseDieCost, int bagCost, List<PvpLoadoutError> errors)
        {
            CardsCost = cardsCost;
            BaseDieCost = baseDieCost;
            BagCost = bagCost;
            this.errors = errors ?? new List<PvpLoadoutError>();
        }

        public bool IsValid => errors.Count == 0;
        public int CardsCost { get; }
        public int BaseDieCost { get; }
        public int BagCost { get; }
        public int TotalCost => CardsCost + BaseDieCost + BagCost;
        public IReadOnlyList<PvpLoadoutError> Errors => errors;

        public bool HasError(PvpLoadoutErrorCode code)
        {
            foreach (PvpLoadoutError error in errors)
            {
                if (error.Code == code)
                    return true;
            }
            return false;
        }
    }

    public static class PvpLoadoutValidator
    {
        public static PvpLoadoutValidationResult Validate(PvpLoadout loadout, PvpLoadoutRules rules)
        {
            if (loadout == null)
                throw new ArgumentNullException(nameof(loadout));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            var errors = new List<PvpLoadoutError>();

            int cardsCost = ValidateCards(loadout, rules, errors);
            int baseDieCost = ValidateBaseDie(loadout, rules, errors);
            int bagCost = ValidateBag(loadout, rules, errors);

            int totalCost = cardsCost + baseDieCost + bagCost;
            if (totalCost > rules.Budget)
                errors.Add(new PvpLoadoutError(
                    PvpLoadoutErrorCode.BudgetExceeded,
                    $"Il loadout costa {totalCost} punti ma il budget è {rules.Budget}."));

            return new PvpLoadoutValidationResult(cardsCost, baseDieCost, bagCost, errors);
        }

        private static int ValidateCards(PvpLoadout loadout, PvpLoadoutRules rules, List<PvpLoadoutError> errors)
        {
            if (loadout.Cards.Count != rules.RequiredCardCount)
                errors.Add(new PvpLoadoutError(
                    PvpLoadoutErrorCode.WrongCardCount,
                    $"Servono esattamente {rules.RequiredCardCount} carte, trovate {loadout.Cards.Count}."));

            int cardsCost = 0;
            var countsByValue = new Dictionary<int, int>();
            var countsByDefinitionId = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (PvpLoadoutCard card in loadout.Cards)
            {
                countsByDefinitionId.TryGetValue(card.DefinitionId, out int definitionCount);
                countsByDefinitionId[card.DefinitionId] = definitionCount + 1;

                if (!rules.TryGetCardCost(card.Value, out int cost))
                {
                    errors.Add(new PvpLoadoutError(
                        PvpLoadoutErrorCode.CardValueOutOfRange,
                        $"La carta '{card.DefinitionId}' ha valore {card.Value}, fuori dall'intervallo 1-{rules.MaximumCardValue}."));
                    continue;
                }
                cardsCost += cost;
                countsByValue.TryGetValue(card.Value, out int count);
                countsByValue[card.Value] = count + 1;
            }

            foreach (KeyValuePair<string, int> entry in countsByDefinitionId)
            {
                if (entry.Value > 1)
                    errors.Add(new PvpLoadoutError(
                        PvpLoadoutErrorCode.DuplicateCard,
                        $"La carta '{entry.Key}' Ã¨ presente {entry.Value} volte: ogni carta puÃ² essere selezionata una sola volta."));
            }

            foreach (KeyValuePair<int, int> entry in countsByValue)
            {
                if (rules.TryGetCardCountLimit(entry.Key, out int limit) && entry.Value > limit)
                    errors.Add(new PvpLoadoutError(
                        PvpLoadoutErrorCode.TooManyCardsOfValue,
                        $"Massimo {limit} carte di valore {entry.Key}, trovate {entry.Value}."));
            }

            return cardsCost;
        }

        private static int ValidateBaseDie(PvpLoadout loadout, PvpLoadoutRules rules, List<PvpLoadoutError> errors)
        {
            if (rules.TryGetBaseDieCost(loadout.BaseDieSides, out int cost))
                return cost;

            errors.Add(new PvpLoadoutError(
                PvpLoadoutErrorCode.InvalidBaseDie,
                $"D{loadout.BaseDieSides} non è un dado base valido."));
            return 0;
        }

        private static int ValidateBag(PvpLoadout loadout, PvpLoadoutRules rules, List<PvpLoadoutError> errors)
        {
            int bagCost = 0;
            foreach (int sides in loadout.BagDiceSides)
            {
                if (!rules.TryGetBagDieCost(sides, out int cost))
                {
                    errors.Add(new PvpLoadoutError(
                        PvpLoadoutErrorCode.InvalidBagDie,
                        $"D{sides} non è un dado acquistabile per la bag."));
                    continue;
                }
                bagCost += cost;
            }
            return bagCost;
        }
    }
}
