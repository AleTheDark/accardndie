using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.NetProtocol;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Loadout di partenza legali finché non esiste il builder vero.
    /// </summary>
    public static class PvpQuickLoadout
    {
        /// <summary>
        /// Scala di valori 2-10, una classe diversa per valore: costa 54 punti
        /// col D3 gratuito. Se il CardDatabase è disponibile usa gli id reali
        /// del catalogo, altrimenti ripiega sui goblin da 2 (18 punti).
        /// </summary>
        public static PvpLoadoutDto Build(CardDatabase database)
        {
            var classes = new[]
            {
                HeroClass.Warrior, HeroClass.Rogue, HeroClass.Mage,
                HeroClass.Barbarian, HeroClass.Assassin, HeroClass.Priest,
                HeroClass.Paladin, HeroClass.Hunter, HeroClass.Necromancer
            };

            var cards = new List<LoadoutCardDto>();
            if (database != null)
            {
                for (int index = 0; index < classes.Length; index++)
                {
                    int value = 2 + index;
                    CardDefinition definition = FindCard(database, classes[index], value);
                    if (definition == null)
                        break;
                    cards.Add(new LoadoutCardDto
                    {
                        definitionId = definition.Id,
                        value = definition.Strength,
                        heroClass = (int)definition.HeroClass
                    });
                }
            }

            if (cards.Count != classes.Length)
            {
                // Fallback senza database: i goblin da 2 esistono per tutte le classi.
                cards.Clear();
                foreach (HeroClass heroClass in classes)
                    cards.Add(new LoadoutCardDto
                    {
                        definitionId = $"2-goblin-{heroClass.ToString().ToLowerInvariant()}",
                        value = 2,
                        heroClass = (int)heroClass
                    });
            }

            return new PvpLoadoutDto
            {
                cards = cards.ToArray(),
                baseDieSides = 3,
                bagDiceSides = new int[0]
            };
        }

        private static CardDefinition FindCard(CardDatabase database, HeroClass heroClass, int value)
        {
            foreach (CardDefinition card in database.Cards)
            {
                if (card != null
                    && card.Category == CardCategory.Monster
                    && card.CanEnterCombat
                    && card.HeroClass == heroClass
                    && card.Strength == value)
                    return card;
            }
            return null;
        }
    }
}
