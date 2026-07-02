using System;
using System.Collections.Generic;

namespace AccardND.GameCore.Pvp
{
    public readonly struct PvpLoadoutCard
    {
        public PvpLoadoutCard(string definitionId, int value)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
                throw new ArgumentException("Una carta del loadout deve avere un id.", nameof(definitionId));

            DefinitionId = definitionId;
            Value = value;
        }

        public string DefinitionId { get; }
        public int Value { get; }
    }

    public sealed class PvpLoadout
    {
        private readonly List<PvpLoadoutCard> cards;
        private readonly List<int> bagDiceSides;

        public PvpLoadout(
            IReadOnlyList<PvpLoadoutCard> cards,
            int baseDieSides,
            IReadOnlyList<int> bagDiceSides = null)
        {
            this.cards = cards != null ? new List<PvpLoadoutCard>(cards) : new List<PvpLoadoutCard>();
            BaseDieSides = baseDieSides;
            this.bagDiceSides = bagDiceSides != null ? new List<int>(bagDiceSides) : new List<int>();
        }

        public IReadOnlyList<PvpLoadoutCard> Cards => cards;
        public int BaseDieSides { get; }
        public IReadOnlyList<int> BagDiceSides => bagDiceSides;
    }
}
