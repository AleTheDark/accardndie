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
                ClassFamily.Might => "Fortuza",
                ClassFamily.Cunning => "Astuta",
                ClassFamily.Magic => "Magica",
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
                HeroClass.Rogue => "PROSSIMO ATTACCO: RITIRA IL DADO SE ESCE 1",
                HeroClass.Hunter => $"MARCA UN NEMICO: +{mark} A CHI LO ATTACCA",
                HeroClass.Barbarian => $"FURIA +{rage} SE NON ELIMINA",
                HeroClass.Necromancer => "RIALZA UN ALLEATO ELIMINATO",
                HeroClass.Priest => $"BENEDIZIONE +{blessing} A UN ALLEATO",
                HeroClass.Assassin => "SCEGLIE UN NEMICO: SALTA IL TURNO",
                HeroClass.Warrior => "PROSSIMO ATTACCO: SOMMA 2 DADI VIGORE",
                HeroClass.Mage => "RIDUCE IL DADO VIGORE NEMICO",
                HeroClass.Paladin => "SI RAFFORZA O PROTEGGE UN ALLEATO",
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
                HeroClass.Paladin => "Il Paladino puo rafforzarsi o proteggere un alleato deviando un attacco su di se, si difendera con vantaggio.",
                HeroClass.Rogue => "Abilita passiva: ogni dado Vigore ritira una volta se esce 1, in attacco e in difesa.",
                HeroClass.Assassin => "Scegli un avversario: salta il suo prossimo turno.",
                HeroClass.Hunter => $"Scegli un nemico come Bersaglio marcato. Chi attacca quel bersaglio riceve +{mark}. Piu marchi sullo stesso bersaglio non si sommano.",
                HeroClass.Mage => "Scegli un nemico: nel prossimo confronto il suo dado Vigore scende di una taglia.",
                HeroClass.Necromancer => "Riporta in vita un alleato eliminato che agisce subito dopo di te.",
                HeroClass.Priest => $"Potenzia un alleato di +{blessing} al suo prossimo attacco.",
                _ => "Nessuna abilita di combattimento."
            };
        }

        public static string AbilityTitle(HeroClass heroClass) =>
            "Abilita " + HeroClassName(heroClass);
    }
}
