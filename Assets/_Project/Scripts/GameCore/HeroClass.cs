namespace AccardND.GameCore
{
    public enum HeroClass
    {
        Assassin = 0,
        Warrior = 1,
        Mage = 2,
        Paladin = 3,
        Rogue = 4,
        Hunter = 5,
        Barbarian = 6,
        Necromancer = 7,
        Priest = 8
    }

    public enum ClassFamily
    {
        Might,
        Cunning,
        Magic
    }

    public static class HeroClassFamily
    {
        public static ClassFamily Of(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior or HeroClass.Barbarian or HeroClass.Paladin => ClassFamily.Might,
                HeroClass.Rogue or HeroClass.Assassin or HeroClass.Hunter => ClassFamily.Cunning,
                HeroClass.Mage or HeroClass.Necromancer or HeroClass.Priest => ClassFamily.Magic,
                _ => ClassFamily.Might
            };
        }
    }
}
