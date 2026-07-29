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
                HeroClass.Hunter => $"MARCA UN NEMICO: +{mark} AL PROSSIMO ATTACCO",
                HeroClass.Barbarian => $"FURIA +{rage} SE NON ELIMINA",
                HeroClass.Necromancer => "RIALZA UN ALLEATO ELIMINATO",
                HeroClass.Priest => $"BENEDIZIONE +{blessing} A UN ALLEATO",
                HeroClass.Assassin => "SCEGLIE UN NEMICO: SALTA IL TURNO",
                HeroClass.Warrior => "PROSSIMO ATTACCO: SOMMA DADO VIGORE + DADO STEP -1",
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
                HeroClass.Warrior => "Attiva l'abilita: al prossimo attacco tira il dado Vigore e un dado di uno step inferiore, poi somma i risultati.",
                HeroClass.Barbarian => $"Se attacca ma non elimina il bersaglio, prepara Furia: +{rage} al prossimo attacco e alla prossima difesa.",
                HeroClass.Paladin => "Il Paladino puo rafforzarsi o proteggere un alleato deviando un attacco su di se, si difendera con vantaggio.",
                HeroClass.Rogue => "Abilita passiva: ogni dado Vigore ritira una volta se esce 1, in attacco e in difesa.",
                HeroClass.Assassin => "Scegli un avversario: salta il suo prossimo turno.",
                HeroClass.Hunter => $"Marca un nemico. Il prossimo attacco contro quel bersaglio riceve +{mark}, poi tutti i marchi sul bersaglio vengono consumati. Piu marchi non si sommano.",
                HeroClass.Mage => "Scegli un nemico: nel prossimo confronto il suo dado Vigore scende di una taglia.",
                HeroClass.Necromancer => "Riporta in vita un alleato eliminato che agisce subito dopo di te.",
                HeroClass.Priest => $"Potenzia un alleato di +{blessing} al suo prossimo attacco.",
                _ => "Nessuna abilita di combattimento."
            };
        }

        public static string AbilityTitle(HeroClass heroClass) =>
            "Abilita " + HeroClassName(heroClass);

        public static string FamilyAuraDescription(ClassFamily family)
        {
            return family switch
            {
                ClassFamily.Might => "Quando muore una pedina qualsiasi, ogni carta Fortuza con l'aura attiva acquisisce +1 permanente.",
                ClassFamily.Cunning => "Le carte Astute attaccano con vantaggio i nemici che hanno bonus o malus.",
                ClassFamily.Magic => "Le carte Magiche si difendono con un dado Vigore di una taglia superiore.",
                _ => "Nessun effetto."
            };
        }

        public static string ClassAuraDescription(HeroClass heroClass, ClassBalanceConfiguration balance = null)
        {
            int mark = balance?.HunterStrongTargetBonus ?? 2;
            return heroClass switch
            {
                HeroClass.Warrior => "Durante un confronto, se la Potenza del Guerriero e inferiore a quella dell'avversario, il Guerriero riceve +2 al totale.",
                HeroClass.Barbarian => "Furia vale +3 invece di +2, sia in attacco sia in difesa.",
                HeroClass.Paladin => "Quando un Paladino sopravvive a una difesa, contrattacca con +1.",
                HeroClass.Rogue => "I Ladri ritirano una volta ogni dado che mostra 1 o 2, in attacco e in difesa.",
                HeroClass.Assassin => "Quando un Assassino inibisce un nemico, quel nemico subisce anche -1 permanente.",
                HeroClass.Hunter => $"Il prossimo attacco contro un bersaglio marcato riceve +{mark * 2} invece di +{mark}; poi tutti i marchi sul bersaglio vengono consumati. I marchi non si sommano.",
                HeroClass.Mage => "Quando un Mago con questa aura muore per un attacco, l'attaccante che lo ha eliminato subisce -2 permanente.",
                HeroClass.Necromancer => "La prima volta che un alleato viene eliminato, resta in campo come Spirito per un ultimo turno.",
                HeroClass.Priest => "Benedizione vale +3 invece di +2.",
                _ => "Nessun effetto."
            };
        }

        public static string FormationAuraDescription() =>
            "Una volta per combattimento, quando una carta avrebbe svantaggio di famiglia in attacco, lo svantaggio diventa neutro.";
    }
}
