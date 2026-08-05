using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.Localization;

namespace AccardND.GameData
{
    public static class CardRulesGlossary
    {
        // --- Costi in mana ---

        /// <summary>Etichetta del costo in rune, da mostrare accanto al nome dell'abilita'.</summary>
        public static string ManaCostLabel(int cost)
        {
            return cost <= 0
                ? GameText.GetOrFallback(GameTextKeys.Rules.ManaCostFree, "Gratuita")
                : GameText.GetOrFallback(GameTextKeys.Rules.ManaCost, "{0} mana", cost);
        }

        public static string PrimaryAbilityCostLabel(HeroClass heroClass) =>
            ManaCostLabel(AbilityManaCosts.Primary(heroClass));

        public static string SupremeCostLabel(HeroClass heroClass) =>
            ManaCostLabel(AbilityManaCosts.Supreme(heroClass));

        // --- Abilita' suprema ---

        public static string SupremeTitle(HeroClass heroClass) =>
            GameText.GetOrFallback(
                GameTextKeys.Rules.SupremeTitle,
                "Suprema di {0}",
                HeroClassName(heroClass));

        /// <summary>Nome proprio della suprema, quello che compare sul bottone.</summary>
        public static string SupremeName(HeroClass heroClass)
        {
            string key = GameTextKeys.Rules.SupremeName(heroClass.ToString().ToLowerInvariant());
            return GameText.GetOrFallback(key, DefaultSupremeName(heroClass));
        }

        public static string SupremeDescription(HeroClass heroClass)
        {
            string key = GameTextKeys.Rules.SupremeDescription(heroClass.ToString().ToLowerInvariant());
            return GameText.GetOrFallback(key, DefaultSupremeDescription(heroClass));
        }

        /// <summary>La tecnica non e' ancora stata sbloccata al Santuario.</summary>
        public static string SupremeLockedText() =>
            GameText.GetOrFallback(
                GameTextKeys.Rules.SupremeLocked,
                "Tecnica non ancora appresa. Si sblocca al Santuario.");

        // I testi veri stanno in GameCore: da li' li legge anche il server per la voce del
        // Santuario, cosi' l'effetto promesso all'altare e quello scritto sulla carta coincidono.
        private static string DefaultSupremeName(HeroClass heroClass) =>
            SupremeAbilityText.Name(heroClass);

        private static string DefaultSupremeDescription(HeroClass heroClass) =>
            SupremeAbilityText.Description(heroClass);

        public static bool HasSupreme(HeroClass heroClass) =>
            AbilityManaCosts.IsSupremeImplemented(heroClass);

        public static string HeroClassName(HeroClass heroClass)
        {
            string fallback = heroClass.ToString();
            return GameText.GetOrFallback(
                GameTextKeys.Rules.HeroClassName(fallback.ToLowerInvariant()),
                fallback);
        }

        public static string HeroClassNameUpper(HeroClass heroClass) =>
            HeroClassName(heroClass).ToUpperInvariant();

        public static string ClassFamilyName(ClassFamily family)
        {
            string fallback = family.ToString();
            return GameText.GetOrFallback(
                GameTextKeys.Rules.FamilyName(fallback.ToLowerInvariant()),
                fallback);
        }

        public static string ShortAbilityText(HeroClass heroClass, ClassBalanceConfiguration balance = null)
        {
            int rage = balance?.BarbarianRageBonus ?? 2;
            int mark = balance?.HunterStrongTargetBonus ?? 2;
            int blessing = balance?.PriestBlessingBonus ?? 2;

            string key = GameTextKeys.Rules.ShortAbility(heroClass.ToString().ToLowerInvariant());
            return heroClass switch
            {
                HeroClass.Hunter => GameText.Format(key, mark),
                HeroClass.Barbarian => GameText.Format(key, rage),
                HeroClass.Priest => GameText.Format(key, blessing),
                HeroClass.Rogue or HeroClass.Necromancer or HeroClass.Assassin or
                    HeroClass.Warrior or HeroClass.Mage or HeroClass.Paladin => GameText.Get(key),
                _ => string.Empty
            };
        }

        public static string AbilityDescription(HeroClass heroClass, ClassBalanceConfiguration balance = null)
        {
            int rage = balance?.BarbarianRageBonus ?? 2;
            int mark = balance?.HunterStrongTargetBonus ?? 2;
            int blessing = balance?.PriestBlessingBonus ?? 2;

            string key = GameTextKeys.Rules.AbilityDescription(heroClass.ToString().ToLowerInvariant());
            return heroClass switch
            {
                HeroClass.Barbarian => GameText.Format(key, rage),
                HeroClass.Hunter => GameText.Format(key, mark),
                HeroClass.Priest => GameText.Format(key, blessing),
                HeroClass.Warrior or HeroClass.Paladin or HeroClass.Rogue or HeroClass.Assassin or
                    HeroClass.Mage or HeroClass.Necromancer => GameText.Get(key),
                _ => GameText.Get(GameTextKeys.Rules.NoCombatAbility)
            };
        }

        public static string AbilityTitle(HeroClass heroClass) =>
            GameText.Format(GameTextKeys.Rules.AbilityTitle, HeroClassName(heroClass));

        public static string FamilyAuraDescription(ClassFamily family)
        {
            return family is ClassFamily.Might or ClassFamily.Cunning or ClassFamily.Magic
                ? GameText.Get(GameTextKeys.Rules.FamilyAura(family.ToString().ToLowerInvariant()))
                : GameText.Get(GameTextKeys.Rules.NoAura);
        }

        public static string ClassAuraDescription(HeroClass heroClass, ClassBalanceConfiguration balance = null)
        {
            int mark = balance?.HunterStrongTargetBonus ?? 2;
            string key = GameTextKeys.Rules.ClassAura(heroClass.ToString().ToLowerInvariant());
            return heroClass switch
            {
                HeroClass.Hunter => GameText.Format(key, mark * 2, mark),
                HeroClass.Warrior or HeroClass.Barbarian or HeroClass.Paladin or HeroClass.Rogue or
                    HeroClass.Assassin or HeroClass.Mage or HeroClass.Necromancer or HeroClass.Priest => GameText.Get(key),
                _ => GameText.Get(GameTextKeys.Rules.NoAura)
            };
        }

        public static string FormationAuraDescription() =>
            GameText.Get(GameTextKeys.Rules.FormationAura);
    }
}
