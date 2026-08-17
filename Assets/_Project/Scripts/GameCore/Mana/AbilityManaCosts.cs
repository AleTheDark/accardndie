namespace AccardND.GameCore.Mana
{
    /// <summary>Seconda abilita' di classe ("suprema"). Vedi Docs/mana-design.md.</summary>
    public enum SupremeAbilityType
    {
        /// <summary>Guerriero: +2 alla Potenza, +4 se e' l'unica pedina rimasta.</summary>
        Empower,

        /// <summary>Ladro: ruba un buff e 2 mana; se non ci sono buff, ruba 1 Potenza.</summary>
        StealBuffs,

        /// <summary>Mago: Palla di fuoco, colpisce tutti i nemici con un dado di vigore in meno.</summary>
        Fireball,

        /// <summary>Cacciatore: Volley, colpisce tutti i nemici con un dado di vigore in meno.</summary>
        Volley,

        /// <summary>Barbaro: Cornamusa, buffa tutto il party per il round.</summary>
        WarHorn,

        /// <summary>Paladino: Riserva, porta il mana del giocatore alla soglia se e' sotto.</summary>
        ManaReserve,

        /// <summary>Sacerdote: toglie i malus agli alleati e i buff ai nemici. Non tocca le aure.</summary>
        Dispel,

        /// <summary>Assassino: invisibilita', non targettabile finche' non resta l'ultimo.</summary>
        Vanish,

        /// <summary>Necromante: evoca sgherri. Ancora da definire, non implementata.</summary>
        RaiseMinions
    }

    /// <summary>
    /// Costi in mana delle abilita'. I numeri vengono da Docs/mana-design.md e sono
    /// la fonte unica per motore, validazione server e testi di ispezione carta.
    /// </summary>
    public static class AbilityManaCosts
    {
        /// <summary>
        /// Costo della prima abilita' di classe (quella gia' esistente).
        /// Barbaro e Ladro sono a 0: le loro abilita' sono passive, non si attivano,
        /// e una passiva non consuma mana.
        /// </summary>
        public static int Primary(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Rogue => 0,        // RerollOne, passiva
                HeroClass.Barbarian => 0,    // GainRage, passiva
                HeroClass.Hunter => 2,       // MarkTarget
                HeroClass.Priest => 2,       // BlessAlly
                HeroClass.Assassin => 3,     // InhibitEnemy
                HeroClass.Mage => 2,         // WeakenEnemyVigor
                HeroClass.Paladin => 2,      // ProtectAlly
                HeroClass.Warrior => 5,      // DoubleVigorSum
                HeroClass.Necromancer => 4,  // RaiseDefeated
                _ => 2
            };
        }

        /// <summary>Costo base della suprema, prima dei sovrapprezzi di ripetizione e catena.</summary>
        public static int Supreme(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Paladin => 2,      // Riserva: costo iniziale speciale, poi cresce di +1 a ogni riuso nel round
                HeroClass.Warrior => 6,      // Empower
                HeroClass.Rogue => 3,        // StealBuffs
                HeroClass.Mage => 4,         // Fireball
                HeroClass.Hunter => 4,       // Volley
                HeroClass.Barbarian => 4,    // WarHorn
                HeroClass.Priest => 4,       // Dispel
                HeroClass.Assassin => 5,     // Vanish
                HeroClass.Necromancer => 8,  // RaiseMinions
                _ => 4
            };
        }

        public static SupremeAbilityType SupremeOf(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Warrior => SupremeAbilityType.Empower,
                HeroClass.Rogue => SupremeAbilityType.StealBuffs,
                HeroClass.Mage => SupremeAbilityType.Fireball,
                HeroClass.Hunter => SupremeAbilityType.Volley,
                HeroClass.Barbarian => SupremeAbilityType.WarHorn,
                HeroClass.Paladin => SupremeAbilityType.ManaReserve,
                HeroClass.Priest => SupremeAbilityType.Dispel,
                HeroClass.Assassin => SupremeAbilityType.Vanish,
                HeroClass.Necromancer => SupremeAbilityType.RaiseMinions,
                _ => SupremeAbilityType.Empower
            };
        }

        /// <summary>
        /// Tutte le supreme hanno una regola completa nel motore.
        /// </summary>
        public static bool IsSupremeImplemented(HeroClass heroClass) =>
            true;

        /// <summary>
        /// Una suprema che non produce effetti in combattimento non puo' essere
        /// incatenata a un attacco come se fosse un buff offensivo, ma non consuma
        /// nemmeno l'azione d'attacco della pedina.
        /// </summary>
        public static bool IsAttackSupreme(HeroClass heroClass) =>
            heroClass is HeroClass.Mage or HeroClass.Hunter;
    }

    /// <summary>
    /// Regole mana indipendenti dalla modalita'. Campagna, PvP client e server
    /// devono interrogare questa classe invece di ricostruire le stesse decisioni.
    /// </summary>
    public static class ManaActionPolicy
    {
        public static bool HasActivatablePrimary(HeroClass heroClass) =>
            heroClass is not HeroClass.Barbarian and not HeroClass.Rogue;

        public static int ActivationReward(
            ManaRules rules,
            bool skipped,
            bool usedAbilityBeforeSkip)
        {
            if (rules == null)
                return 0;
            if (skipped && usedAbilityBeforeSkip)
                return 0;
            return skipped ? rules.GainOnSkip : rules.GainOnActivation;
        }

        public static int PrimaryCost(HeroClass heroClass) =>
            AbilityManaCosts.Primary(heroClass);

        public static int AttackCost(ManaRules rules) =>
            rules?.AttackCost ?? 0;
    }
}
