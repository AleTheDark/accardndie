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
                ? GameText.Get(GameTextKeys.Rules.ManaCostFree)
                : GameText.Format(GameTextKeys.Rules.ManaCost, cost);
        }

        public static string PrimaryAbilityCostLabel(HeroClass heroClass) =>
            ManaCostLabel(AbilityManaCosts.Primary(heroClass));

        public static string SupremeCostLabel(HeroClass heroClass) =>
            ManaCostLabel(AbilityManaCosts.Supreme(heroClass));

        // --- Abilita' suprema ---

        public static string SupremeTitle(HeroClass heroClass) =>
            GameText.Format(GameTextKeys.Rules.SupremeTitle, HeroClassName(heroClass));

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
            GameText.Get(GameTextKeys.Rules.SupremeLocked);

        // I testi veri stanno in GameCore: da li' li legge anche il server per la voce del
        // Santuario, cosi' l'effetto promesso all'altare e quello scritto sulla carta coincidono.
        private static string DefaultSupremeName(HeroClass heroClass) =>
            LocalizedSupremeName(heroClass);

        private static string DefaultSupremeDescription(HeroClass heroClass) =>
            LocalizedSupremeDescription(heroClass);

        // Le chiavi rules.class.*.supreme vengono aggiunte alle String Table dal catalogo.
        // Questi fallback completi evitano però qualsiasi ricaduta in italiano nelle build
        // che hanno già il codice ma non hanno ancora importato la nuova tabella.
        private static string LocalizedSupremeName(HeroClass heroClass)
        {
            string locale = GameText.CurrentLocaleCode;
            return locale switch
            {
                "en" => heroClass switch { HeroClass.Warrior => "Enhancement", HeroClass.Rogue => "Pilfer", HeroClass.Mage => "Fireball", HeroClass.Hunter => "Volley", HeroClass.Barbarian => "Bagpipes", HeroClass.Paladin => "Reserve", HeroClass.Priest => "Purification", HeroClass.Assassin => "Invisibility", HeroClass.Necromancer => "Summon Minions", _ => "Supreme" },
                "de" => heroClass switch { HeroClass.Warrior => "Verstärkung", HeroClass.Rogue => "Raub", HeroClass.Mage => "Feuerball", HeroClass.Hunter => "Salve", HeroClass.Barbarian => "Dudelsack", HeroClass.Paladin => "Reserve", HeroClass.Priest => "Läuterung", HeroClass.Assassin => "Unsichtbarkeit", HeroClass.Necromancer => "Schergen beschwören", _ => "Ultimative" },
                "es" => heroClass switch { HeroClass.Warrior => "Potenciación", HeroClass.Rogue => "Hurto", HeroClass.Mage => "Bola de fuego", HeroClass.Hunter => "Ráfaga", HeroClass.Barbarian => "Gaita", HeroClass.Paladin => "Reserva", HeroClass.Priest => "Purificación", HeroClass.Assassin => "Invisibilidad", HeroClass.Necromancer => "Invocar esbirros", _ => "Suprema" },
                "fr" => heroClass switch { HeroClass.Warrior => "Renforcement", HeroClass.Rogue => "Vol", HeroClass.Mage => "Boule de feu", HeroClass.Hunter => "Rafale", HeroClass.Barbarian => "Cornemuse", HeroClass.Paladin => "Réserve", HeroClass.Priest => "Purification", HeroClass.Assassin => "Invisibilité", HeroClass.Necromancer => "Invoquer des sbires", _ => "Suprême" },
                _ => SupremeAbilityText.Name(heroClass)
            };
        }

        private static string LocalizedSupremeDescription(HeroClass heroClass)
        {
            string italian = SupremeAbilityText.Description(heroClass);
            return GameText.CurrentLocaleCode switch
            {
                "en" => heroClass switch
                {
                    HeroClass.Warrior => "Cost 6 mana: Gain +2 Power until the room ends. If you are the only token left, the bonus becomes +4.",
                    HeroClass.Rogue => "Cost 3 mana: Steal one enhancement and 2 mana from the target. If it has no enhancements, steal 1 Power until the room ends instead.",
                    HeroClass.Mage or HeroClass.Hunter => "Cost 4 mana: Hit all opposing tokens with a Vigor die reduced by one step.",
                    HeroClass.Barbarian => "Cost 4 mana: Play the bagpipes: your whole team builds Fury, keeps it through defeats, and spends it on its first victory.",
                    HeroClass.Paladin => "Cost 2 mana: Draw from the reserve: if your mana is below 6, restore it to 6.",
                    HeroClass.Priest => "Cost 4 mana: Remove all penalties from allies and all enhancements from opponents. Auras are unaffected.",
                    HeroClass.Assassin => "Cost 5 mana: Become untargetable. When you are the only token left, become targetable again but defend with advantage.",
                    HeroClass.Necromancer => "Cost 8 mana: Summon 2 Power-2 minions that intercept direct and area attacks. They have no turn and cannot attack. When one dies, all your tokens gain +1 Power; Purification dissolves them without triggering the bonus.",
                    _ => italian
                },
                "de" => heroClass switch
                {
                    HeroClass.Warrior => "Kosten: 6 Mana: Erhalte +2 Stärke bis zum Ende des Raums. Bist du die letzte verbleibende Figur, wird der Bonus zu +4.",
                    HeroClass.Rogue => "Kosten: 3 Mana: Stiehl dem Ziel eine Verstärkung und 2 Mana. Hat es keine Verstärkungen, stiehl stattdessen 1 Stärke bis zum Ende des Raums.",
                    HeroClass.Mage or HeroClass.Hunter => "Kosten: 4 Mana: Triff alle gegnerischen Figuren mit einem um eine Stufe verringerten Vigor-Würfel.",
                    HeroClass.Barbarian => "Kosten: 4 Mana: Spiele den Dudelsack: Dein ganzes Team sammelt Wut, behält sie bei Niederlagen und verbraucht sie beim ersten Sieg.",
                    HeroClass.Paladin => "Kosten: 2 Mana: Greife auf die Reserve zu: Liegt dein Mana unter 6, wird es auf 6 aufgefüllt.",
                    HeroClass.Priest => "Kosten: 4 Mana: Entferne alle Mali von Verbündeten und alle Verstärkungen von Gegnern. Auren bleiben unberührt.",
                    HeroClass.Assassin => "Kosten: 5 Mana: Werde nicht anvisierbar. Bist du die letzte Figur, wirst du wieder anvisierbar, verteidigst aber mit Vorteil.",
                    HeroClass.Necromancer => "Kosten: 8 Mana: Beschwöre 2 Schergen mit Stärke 2, die direkte und Flächenangriffe abfangen. Sie haben keinen Zug und können nicht angreifen. Stirbt einer, erhalten alle deine Figuren +1 Stärke; Läuterung löst sie ohne Bonus aus.",
                    _ => italian
                },
                "es" => heroClass switch
                {
                    HeroClass.Warrior => "Coste 6 de maná: Obtienes +2 de Poder hasta el final de la sala. Si eres la única ficha restante, el bono pasa a ser +4.",
                    HeroClass.Rogue => "Coste 3 de maná: Robas una mejora y 2 de maná al objetivo. Si no tiene mejoras, robas 1 de Poder hasta el final de la sala.",
                    HeroClass.Mage or HeroClass.Hunter => "Coste 4 de maná: Golpea a todas las fichas rivales con un dado de Vigor reducido un nivel.",
                    HeroClass.Barbarian => "Coste 4 de maná: Toca la gaita: todo tu equipo acumula Furia, la conserva tras las derrotas y la descarga en su primera victoria.",
                    HeroClass.Paladin => "Coste 2 de maná: Usa la reserva: si tu maná está por debajo de 6, vuelve a 6.",
                    HeroClass.Priest => "Coste 4 de maná: Elimina todas las penalizaciones de los aliados y todas las mejoras de los rivales. Las auras no cambian.",
                    HeroClass.Assassin => "Coste 5 de maná: Te vuelves imposible de seleccionar. Cuando seas la única ficha restante, vuelves a ser seleccionable pero te defiendes con ventaja.",
                    HeroClass.Necromancer => "Coste 8 de maná: Invoca 2 esbirros de Poder 2 que interceptan ataques directos y de área. No tienen turno ni pueden atacar. Cuando uno muere, todas tus fichas ganan +1 de Poder; Purificación los disuelve sin activar el bono.",
                    _ => italian
                },
                "fr" => heroClass switch
                {
                    HeroClass.Warrior => "Coût : 6 mana : Gagnez +2 Puissance jusqu'à la fin de la salle. Si vous êtes le dernier pion restant, le bonus passe à +4.",
                    HeroClass.Rogue => "Coût : 3 mana : Volez une amélioration et 2 mana à la cible. Si elle n'a aucune amélioration, volez plutôt 1 Puissance jusqu'à la fin de la salle.",
                    HeroClass.Mage or HeroClass.Hunter => "Coût : 4 mana : Frappez tous les pions adverses avec un dé de Vigueur réduit d'un niveau.",
                    HeroClass.Barbarian => "Coût : 4 mana : Jouez de la cornemuse : toute votre équipe accumule de la Fureur, la conserve après les défaites et la dépense lors de sa première victoire.",
                    HeroClass.Paladin => "Coût : 2 mana : Puisez dans la réserve : si votre mana est inférieur à 6, remontez-le à 6.",
                    HeroClass.Priest => "Coût : 4 mana : Retirez tous les malus des alliés et toutes les améliorations des adversaires. Les auras ne sont pas affectées.",
                    HeroClass.Assassin => "Coût : 5 mana : Devenez impossible à cibler. Lorsque vous êtes le dernier pion restant, redevenez ciblable mais défendez avec avantage.",
                    HeroClass.Necromancer => "Coût : 8 mana : Invoquez 2 sbires de Puissance 2 qui interceptent les attaques directes et de zone. Ils n'ont pas de tour et ne peuvent pas attaquer. Quand l'un meurt, tous vos pions gagnent +1 Puissance ; Purification les dissout sans déclencher le bonus.",
                    _ => italian
                },
                _ => italian
            };
        }

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
