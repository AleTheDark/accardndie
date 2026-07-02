using System;
using System.Collections.Generic;

namespace AccardND.GameCore.Pvp
{
    public readonly struct PvpFirstDeal
    {
        public PvpFirstDeal(IReadOnlyList<int> handIndices, IReadOnlyList<int> unseenIndices)
        {
            HandIndices = handIndices;
            UnseenIndices = unseenIndices;
        }

        public IReadOnlyList<int> HandIndices { get; }
        public IReadOnlyList<int> UnseenIndices { get; }
    }

    /// <summary>
    /// Distribuisce le mani PvP lavorando su indici di istanza (0..deckSize-1),
    /// perché il loadout può contenere più copie della stessa definizione.
    /// </summary>
    public static class PvpHandDealer
    {
        public static PvpFirstDeal DealFirstHand(IRandomSource random, int deckSize, int handSize)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (deckSize < 1)
                throw new ArgumentOutOfRangeException(nameof(deckSize));
            if (handSize < 1 || handSize > deckSize)
                throw new ArgumentOutOfRangeException(nameof(handSize));

            var indices = new List<int>(deckSize);
            for (int index = 0; index < deckSize; index++)
                indices.Add(index);
            Shuffle(indices, random);

            var hand = indices.GetRange(0, handSize);
            var unseen = indices.GetRange(handSize, deckSize - handSize);
            hand.Sort();
            unseen.Sort();
            return new PvpFirstDeal(hand, unseen);
        }

        public static List<int> BuildSecondHand(
            IReadOnlyList<int> unseenIndices,
            IReadOnlyList<int> firstHandIndices,
            IReadOnlyList<int> deployedIndices)
        {
            if (unseenIndices == null)
                throw new ArgumentNullException(nameof(unseenIndices));
            if (firstHandIndices == null)
                throw new ArgumentNullException(nameof(firstHandIndices));
            if (deployedIndices == null)
                throw new ArgumentNullException(nameof(deployedIndices));

            var firstHand = new HashSet<int>(firstHandIndices);
            var deployed = new HashSet<int>(deployedIndices);
            if (deployed.Count != deployedIndices.Count)
                throw new ArgumentException("Indici schierati duplicati.", nameof(deployedIndices));
            foreach (int index in deployed)
            {
                if (!firstHand.Contains(index))
                    throw new ArgumentException(
                        "Le carte schierate nel round 1 devono provenire dalla prima mano.",
                        nameof(deployedIndices));
            }

            // Round 2: le carte mai viste + quelle della prima mano non schierate.
            var secondHand = new List<int>(unseenIndices);
            foreach (int index in firstHandIndices)
            {
                if (!deployed.Contains(index))
                    secondHand.Add(index);
            }
            secondHand.Sort();
            return secondHand;
        }

        public static bool TryValidateDecisiveSelection(
            int deckSize, IReadOnlyList<int> chosenIndices, int requiredCount, out string error)
        {
            if (chosenIndices == null || chosenIndices.Count != requiredCount)
            {
                error = $"Il round decisivo richiede esattamente {requiredCount} carte.";
                return false;
            }

            var seen = new HashSet<int>();
            foreach (int index in chosenIndices)
            {
                if (index < 0 || index >= deckSize)
                {
                    error = $"Indice carta {index} fuori dal loadout.";
                    return false;
                }
                if (!seen.Add(index))
                {
                    error = "La selezione contiene carte duplicate.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static void Shuffle(List<int> values, IRandomSource random)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int other = random.NextInclusive(0, index);
                (values[index], values[other]) = (values[other], values[index]);
            }
        }
    }
}
