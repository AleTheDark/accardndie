namespace AccardND.GameCore
{
    public enum ClassAbilityType
    {
        InhibitEnemy,
        DoubleVigorSum,
        WeakenEnemyVigor,
        ProtectAlly,
        RerollOne,
        MarkTarget,
        GainRage,
        RaiseDefeated,
        BlessAlly
    }

    public static class ClassAbilityRules
    {
        public static ClassAbilityType For(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => ClassAbilityType.InhibitEnemy,
                HeroClass.Warrior => ClassAbilityType.DoubleVigorSum,
                HeroClass.Mage => ClassAbilityType.WeakenEnemyVigor,
                HeroClass.Paladin => ClassAbilityType.ProtectAlly,
                HeroClass.Rogue => ClassAbilityType.RerollOne,
                HeroClass.Hunter => ClassAbilityType.MarkTarget,
                HeroClass.Barbarian => ClassAbilityType.GainRage,
                HeroClass.Necromancer => ClassAbilityType.RaiseDefeated,
                HeroClass.Priest => ClassAbilityType.BlessAlly,
                _ => ClassAbilityType.DoubleVigorSum
            };
        }
    }
}
