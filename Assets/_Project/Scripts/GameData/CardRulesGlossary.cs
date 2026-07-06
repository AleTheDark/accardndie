using AccardND.GameCore;

namespace AccardND.GameData
{
    public static class CardRulesGlossary
    {
        public static string HeroClassName(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => "Assassino",
                HeroClass.Warrior => "Guerriero",
                HeroClass.Mage => "Mago",
                HeroClass.Paladin => "Paladino",
                HeroClass.Rogue => "Ladro",
                HeroClass.Hunter => "Cacciatore",
                HeroClass.Barbarian => "Barbaro",
                HeroClass.Necromancer => "Necromante",
                HeroClass.Priest => "Sacerdote",
                _ => heroClass.ToString()
            };
        }

        public static string HeroClassNameUpper(HeroClass heroClass) =>
            HeroClassName(heroClass).ToUpperInvariant();

        public static string ClassFamilyName(ClassFamily family)
        {
            return family switch
            {
                ClassFamily.Might => "Forza",
                ClassFamily.Cunning => "Astuzia",
                ClassFamily.Magic => "Magia",
                _ => family.ToString()
            };
        }

        public static string ShortAbilityText(HeroClass heroClass, ClassBalanceConfiguration balance = null)
        {
            int rage = balance?.BarbarianRageBonus ?? 2;
            int mark = balance?.HunterStrongTargetBonus ?? 2;
            int blessing = balance?.PriestBlessingBonus ?? 2;

            return heroClass switch
            {
                HeroClass.Rogue => "RITIRA GLI 1 QUANDO ATTACCA",
                HeroClass.Hunter => $"MARCA UN NEMICO: +{mark} A CHI LO ATTACCA",
                HeroClass.Barbarian => $"FURIA +{rage} SE NON ELIMINA",
                HeroClass.Necromancer => "RIALZA UN ALLEATO ELIMINATO",
                HeroClass.Priest => $"BENEDIZIONE +{blessing} A UN ALLEATO",
                HeroClass.Assassin => "INIBISCE UN NEMICO PER 1 TURNO",
                HeroClass.Warrior => "PROSSIMO ATTACCO: SOMMA 2 DADI VIGORE",
                HeroClass.Mage => "RIDUCE IL DADO VIGORE NEMICO",
                HeroClass.Paladin => "PROTEGGE UN ALLEATO O SE STESSO",
                _ => string.Empty
            };
        }

        public static string AbilityDescription(HeroClass heroClass, ClassBalanceConfiguration balance = null)
        {
            int rage = balance?.BarbarianRageBonus ?? 2;
            int mark = balance?.HunterStrongTargetBonus ?? 2;
            int blessing = balance?.PriestBlessingBonus ?? 2;

            return heroClass switch
            {
                HeroClass.Warrior => "Attiva l'abilita: al prossimo attacco tira due dadi Vigore e somma i risultati.",
                HeroClass.Barbarian => $"Se attacca ma non elimina il bersaglio, prepara Furia: +{rage} al prossimo attacco e alla prossima difesa.",
                HeroClass.Paladin => "Scegli un alleato da proteggere. Il Paladino puo deviare l'attacco su di se; se protegge se stesso, difende con vantaggio.",
                HeroClass.Rogue => "Quando attacca, se il dado Vigore fa 1 lo ritira una volta e usa il nuovo risultato.",
                HeroClass.Assassin => "Scegli un nemico: diventa Inibito per 1 turno e agisce con limitazioni.",
                HeroClass.Hunter => $"Scegli un nemico come Bersaglio marcato. Chi attacca quel bersaglio riceve +{mark}. Piu marchi sullo stesso bersaglio non si sommano.",
                HeroClass.Mage => "Scegli un nemico: nel prossimo confronto il suo dado Vigore scende di una taglia.",
                HeroClass.Necromancer => "Scegli un alleato eliminato: torna in campo come Spirito con 1 vita e puo agire ancora.",
                HeroClass.Priest => $"Scegli un alleato: riceve Benedizione, +{blessing} al suo prossimo attacco.",
                _ => "Nessuna abilita di combattimento."
            };
        }

        public static string AbilityTitle(HeroClass heroClass) =>
            "Abilita " + HeroClassName(heroClass);
    }
}
