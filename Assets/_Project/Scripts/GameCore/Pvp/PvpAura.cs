using System.Collections.Generic;

namespace AccardND.GameCore.Pvp
{
    public enum PvpAuraType
    {
        None,
        Formation,
        Might,
        Cunning,
        Magic,
        Warrior,
        Barbarian,
        Paladin,
        Rogue,
        Assassin,
        Hunter,
        Mage,
        Necromancer,
        Priest
    }

    public static class PvpAura
    {
        /// <summary>Stesse priorità della campagna: classe > famiglia > formazione.</summary>
        public static PvpAuraType Determine(IReadOnlyList<CombatCard> formation)
        {
            if (formation == null || formation.Count != 3)
                return PvpAuraType.None;

            HeroClass firstClass = formation[0].HeroClass;
            bool sameClass = true;
            foreach (CombatCard card in formation)
            {
                if (card.HeroClass != firstClass)
                {
                    sameClass = false;
                    break;
                }
            }
            if (sameClass)
                return firstClass switch
                {
                    HeroClass.Warrior => PvpAuraType.Warrior,
                    HeroClass.Barbarian => PvpAuraType.Barbarian,
                    HeroClass.Paladin => PvpAuraType.Paladin,
                    HeroClass.Rogue => PvpAuraType.Rogue,
                    HeroClass.Assassin => PvpAuraType.Assassin,
                    HeroClass.Hunter => PvpAuraType.Hunter,
                    HeroClass.Mage => PvpAuraType.Mage,
                    HeroClass.Necromancer => PvpAuraType.Necromancer,
                    HeroClass.Priest => PvpAuraType.Priest,
                    _ => PvpAuraType.None
                };

            var families = new List<ClassFamily>(3);
            foreach (CombatCard card in formation)
                families.Add(HeroClassFamily.Of(card.HeroClass));

            if (families.Contains(ClassFamily.Might)
                && families.Contains(ClassFamily.Cunning)
                && families.Contains(ClassFamily.Magic))
                return PvpAuraType.Formation;
            if (families.TrueForAll(family => family == ClassFamily.Might))
                return PvpAuraType.Might;
            if (families.TrueForAll(family => family == ClassFamily.Cunning))
                return PvpAuraType.Cunning;
            if (families.TrueForAll(family => family == ClassFamily.Magic))
                return PvpAuraType.Magic;
            return PvpAuraType.None;
        }
    }

    /// <summary>Scala dei dadi vigore usata da Mage (abbassa) e aura Magic (alza):
    /// D3-D4-D6-D8-D10-D12-D20, identica alla campagna.</summary>
    public static class PvpVigorScale
    {
        public static int Lower(int dieSides)
        {
            if (dieSides <= 3)
                return 3;
            return dieSides switch
            {
                4 => 3,
                6 => 4,
                8 => 6,
                10 => 8,
                12 => 10,
                20 => 12,
                _ => dieSides <= 6 ? 4 : dieSides <= 8 ? 6 : dieSides <= 10 ? 8 : dieSides <= 12 ? 10 : 12
            };
        }

        public static int Raise(int dieSides)
        {
            if (dieSides <= 3)
                return 4;
            return dieSides switch
            {
                4 => 6,
                6 => 8,
                8 => 10,
                10 => 12,
                12 => 20,
                _ => dieSides < 6 ? 6 : dieSides < 8 ? 8 : dieSides < 10 ? 10 : dieSides < 12 ? 12 : 20
            };
        }

        public static int LowerBySteps(int dieSides, int steps)
        {
            int result = dieSides;
            for (int step = 0; step < steps; step++)
                result = Lower(result);
            return result;
        }
    }
}
