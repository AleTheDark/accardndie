using System;
using System.Collections.Generic;
using AccardND.GameCore.Pvp;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class LoadoutCardDto
    {
        public string definitionId;
        public int value;

        /// <summary>Classe eroe come intero di AccardND.GameCore.HeroClass.
        /// Il server per ora si fida del client: andrà verificato contro il catalogo carte.</summary>
        public int heroClass;
    }

    [Serializable]
    public sealed class PvpLoadoutDto
    {
        public LoadoutCardDto[] cards;
        public int baseDieSides;
        public int[] bagDiceSides;

        public PvpLoadout ToLoadout()
        {
            var loadoutCards = new List<PvpLoadoutCard>();
            if (cards != null)
            {
                foreach (LoadoutCardDto card in cards)
                {
                    if (card != null)
                        loadoutCards.Add(new PvpLoadoutCard(
                            card.definitionId, card.value, (AccardND.GameCore.HeroClass)card.heroClass));
                }
            }
            return new PvpLoadout(loadoutCards, baseDieSides, bagDiceSides);
        }

        public static PvpLoadoutDto FromLoadout(PvpLoadout loadout)
        {
            if (loadout == null)
                throw new ArgumentNullException(nameof(loadout));

            var dto = new PvpLoadoutDto
            {
                cards = new LoadoutCardDto[loadout.Cards.Count],
                baseDieSides = loadout.BaseDieSides,
                bagDiceSides = new int[loadout.BagDiceSides.Count]
            };
            for (int index = 0; index < loadout.Cards.Count; index++)
                dto.cards[index] = new LoadoutCardDto
                {
                    definitionId = loadout.Cards[index].DefinitionId,
                    value = loadout.Cards[index].Value,
                    heroClass = (int)loadout.Cards[index].HeroClass
                };
            for (int index = 0; index < loadout.BagDiceSides.Count; index++)
                dto.bagDiceSides[index] = loadout.BagDiceSides[index];
            return dto;
        }
    }
}
