using AccardND.NetProtocol;

namespace AccardND.Localization
{
    /// <summary>
    /// Localizzazione del favo. Il server resta autoritativo su ranghi, costi e valori;
    /// questa classe associa esclusivamente i suoi id stabili alle chiavi visuali.
    /// </summary>
    public static class TalentUiText
    {
        public static string BranchName(string id) => id switch
        {
            "purse" => Local(GameTextKeys.Talents.BranchName(id), "Borsa", "Purse", "Beutel", "Bolsa", "Bourse"),
            "initiative" => Local(GameTextKeys.Talents.BranchName(id), "Iniziativa", "Initiative", "Initiative", "Iniciativa", "Initiative"),
            "mastery" => Local(GameTextKeys.Talents.BranchName(id), "Maestria", "Mastery", "Meisterschaft", "Maestría", "Maîtrise"),
            "occasion" => Local(GameTextKeys.Talents.BranchName(id), "Occasioni", "Opportunities", "Gelegenheiten", "Oportunidades", "Occasions"),
            _ => GameText.GetOrFallbackSilent(GameTextKeys.Talents.BranchName(id), id)
        };

        public static string Name(TalentEntryData node) => node == null ? string.Empty : Node(node.id, 0, node.name);
        public static string Description(TalentEntryData node) => node == null ? string.Empty : Node(node.id, 1, node.description);

        public static string Value(TalentEntryData node, int value)
        {
            if (node == null || value <= 0)
                return string.Empty;
            return node.id switch
            {
                "purse-travel-fund" => Local(GameTextKeys.Talents.TalentValue(node.id), "+{0} oro alla partenza", "+{0} starting gold", "+{0} Startgold", "+{0} de oro inicial", "+{0} or de départ", value),
                "purse-kind-merchant" => Local(GameTextKeys.Talents.TalentValue(node.id), "-{0}% sui prezzi del mercante", "-{0}% merchant prices", "-{0}% Händlerpreise", "-{0}% en precios del mercader", "-{0}% sur les prix du marchand", value),
                "purse-smith-temper" => Local(GameTextKeys.Talents.TalentValue(node.id), value == 1 ? "{0} carta temprata" : "{0} carte temprate", value == 1 ? "{0} tempered card" : "{0} tempered cards", value == 1 ? "{0} gehärtete Karte" : "{0} gehärtete Karten", value == 1 ? "{0} carta templada" : "{0} cartas templadas", value == 1 ? "{0} carte trempée" : "{0} cartes trempées", value),
                "initiative-vanguard" => Local(GameTextKeys.Talents.TalentValue(node.id), "+{0} al primo dado d'iniziativa", "+{0} to the first initiative die", "+{0} auf den ersten Initiativwürfel", "+{0} al primer dado de iniciativa", "+{0} au premier dé d'initiative", value),
                "initiative-flanker" => Local(GameTextKeys.Talents.TalentValue(node.id), "+{0} al secondo dado d'iniziativa", "+{0} to the second initiative die", "+{0} auf den zweiten Initiativwürfel", "+{0} al segundo dado de iniciativa", "+{0} au deuxième dé d'initiative", value),
                "initiative-rearguard" => Local(GameTextKeys.Talents.TalentValue(node.id), "+{0} al terzo dado d'iniziativa", "+{0} to the third initiative die", "+{0} auf den dritten Initiativwürfel", "+{0} al tercer dado de iniciativa", "+{0} au troisième dé d'initiative", value),
                "mastery-apprentice" => Local(GameTextKeys.Talents.TalentValue(node.id), "-{0}% a tutte le soglie", "-{0}% to all thresholds", "-{0}% auf alle Schwellen", "-{0}% a todos los umbrales", "-{0}% sur tous les seuils", value),
                "mastery-focus" => Local(GameTextKeys.Talents.TalentValue(node.id), "+{0} mana a ogni cambio stanza", "+{0} mana on each room change", "+{0} Mana bei jedem Raumwechsel", "+{0} de maná al cambiar de sala", "+{0} mana à chaque changement de salle", value),
                "mastery-reserve" => Local(GameTextKeys.Talents.TalentValue(node.id), "+{0} al massimo di mana", "+{0} maximum mana", "+{0} maximales Mana", "+{0} de maná máximo", "+{0} mana maximum", value),
                "occasion-recovery" => Local(GameTextKeys.Talents.TalentValue(node.id), "-{0}% sul costo di recupero", "-{0}% recovery cost", "-{0}% Wiederherstellungskosten", "-{0}% de coste de recuperación", "-{0}% sur le coût de récupération", value),
                "occasion-challenger" => Local(GameTextKeys.Talents.TalentValue(node.id), "+{0} Potenza al primo attacco", "+{0} Power on the first attack", "+{0} Stärke beim ersten Angriff", "+{0} de Poder en el primer ataque", "+{0} Puissance lors de la première attaque", value),
                "occasion-seeker" => Local(GameTextKeys.Talents.TalentValue(node.id), value == 1 ? "+{0} consumabile a bottino" : "+{0} consumabili a bottino", value == 1 ? "+{0} consumable per loot room" : "+{0} consumables per loot room", value == 1 ? "+{0} Verbrauchsgegenstand pro Beuteraum" : "+{0} Verbrauchsgegenstände pro Beuteraum", value == 1 ? "+{0} consumible por sala de botín" : "+{0} consumibles por sala de botín", value == 1 ? "+{0} consommable par salle de butin" : "+{0} consommables par salle de butin", value),
                _ => string.Empty
            };
        }

        public static string DetailEffect(string current, string next)
        {
            if (string.IsNullOrEmpty(current) && string.IsNullOrEmpty(next)) return null;
            if (string.IsNullOrEmpty(current)) return Local(GameTextKeys.Talents.FirstRank, "Primo rango: {0}", "First rank: {0}", "Erster Rang: {0}", "Primer rango: {0}", "Premier rang : {0}", next);
            if (string.IsNullOrEmpty(next)) return Local(GameTextKeys.Talents.Now, "Ora {0}", "Now {0}", "Jetzt {0}", "Ahora {0}", "Maintenant {0}", current);
            return Local(GameTextKeys.Talents.Now, "Ora {0}  →  {1}", "Now {0}  →  {1}", "Jetzt {0}  →  {1}", "Ahora {0}  →  {1}", "Maintenant {0}  →  {1}", current, next);
        }

		public static string LockedReason(TalentEntryData node, int branchPointsSpent)
        {
            if (node == null) return string.Empty;
            if (!node.tierUnlocked)
				return Local(GameTextKeys.Talents.LockedTier, "Spendi ancora {0} Propoli in questo ramo per sbloccare il rango {1}.", "Spend {0} more Propolis in this branch to unlock tier {1}.", "Gib noch {0} Propolis in diesem Zweig aus, um Rang {1} freizuschalten.", "Gasta {0} de Propóleo más en esta rama para desbloquear el rango {1}.", "Dépensez encore {0} Propolis dans cette branche pour débloquer le rang {1}.", System.Math.Max(0, node.tierGate - branchPointsSpent), node.tier);
            return Local(GameTextKeys.Talents.LockedPoints, "Servono {0} Propoli.", "{0} Propolis required.", "{0} Propolis benötigt.", "Se requieren {0} de Propóleo.", "{0} Propolis requis.", node.nextCost);
        }

        public static string BuyLabel(bool upgrade, int cost) => Local(upgrade ? GameTextKeys.Talents.Upgrade : GameTextKeys.Talents.Unlock,
            upgrade ? "MIGLIORA · {0} PROPOLI" : "SBLOCCA · {0} PROPOLI",
            upgrade ? "UPGRADE · {0} PROPOLIS" : "UNLOCK · {0} PROPOLIS",
            upgrade ? "VERBESSERN · {0} PROPOLIS" : "FREISCHALTEN · {0} PROPOLIS",
            upgrade ? "MEJORAR · {0} PROPÓLEO" : "DESBLOQUEAR · {0} PROPÓLEO",
            upgrade ? "AMÉLIORER · {0} PROPOLIS" : "DÉBLOQUER · {0} PROPOLIS", cost);

        private static string Node(string id, int field, string fallback) => (id, field) switch
        {
            ("purse-travel-fund", 0) => Local(GameTextKeys.Talents.TalentName(id), "Fondo di viaggio", "Travel Fund", "Reisefonds", "Fondo de viaje", "Fonds de voyage"),
            ("purse-travel-fund", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Inizi ogni run con dell'oro già in tasca.", "Start each run with gold already in your purse.", "Beginne jeden Durchlauf mit Gold in deinem Beutel.", "Comienzas cada partida con oro ya en tu bolsa.", "Commencez chaque expédition avec de l'or dans votre bourse."),
            ("purse-kind-merchant", 0) => Local(GameTextKeys.Talents.TalentName(id), "Mercante compiacente", "Kind Merchant", "Gefälliger Händler", "Mercader complaciente", "Marchand complaisant"),
            ("purse-kind-merchant", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Al mercato le carte e i potenziamenti ti costano meno oro.", "Cards and upgrades cost less gold at the market.", "Karten und Verbesserungen kosten auf dem Markt weniger Gold.", "Las cartas y mejoras cuestan menos oro en el mercado.", "Les cartes et améliorations coûtent moins d'or au marché."),
            ("purse-smith-temper", 0) => Local(GameTextKeys.Talents.TalentName(id), "Tempra del fabbro", "Smith's Temper", "Härte des Schmieds", "Temple del herrero", "Trempe du forgeron"),
            ("purse-smith-temper", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Appena forgiato il mazzo, alcune carte a caso ottengono +1 Forza permanente.", "When the deck is forged, random cards gain +1 permanent Strength.", "Beim Schmieden des Decks erhalten zufällige Karten +1 permanente Stärke.", "Al forjar el mazo, algunas cartas al azar obtienen +1 de Fuerza permanente.", "Une fois le deck forgé, des cartes aléatoires gagnent +1 Force permanente."),
            ("purse-first-deal", 0) => Local(GameTextKeys.Talents.TalentName(id), "Primo affare", "First Deal", "Erstes Geschäft", "Primer trato", "Première affaire"),
            ("purse-first-deal", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Il primo potenziamento che compri dal mercante è gratis, una volta per run.", "The first merchant upgrade you buy is free, once per run.", "Die erste Händlerverbesserung ist einmal pro Durchlauf kostenlos.", "La primera mejora que compres al mercader es gratis, una vez por partida.", "La première amélioration achetée au marchand est gratuite, une fois par expédition."),
            ("initiative-vanguard", 0) => Local(GameTextKeys.Talents.TalentName(id), "Avanguardia", "Vanguard", "Vorhut", "Vanguardia", "Avant-garde"),
            ("initiative-vanguard", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Il tuo primo dado d'iniziativa vale di più di quello che mostra.", "Your first initiative die counts for more than it shows.", "Dein erster Initiativwürfel zählt mehr, als er zeigt.", "Tu primer dado de iniciativa vale más de lo que muestra.", "Votre premier dé d'initiative vaut plus que ce qu'il affiche."),
            ("initiative-flanker", 0) => Local(GameTextKeys.Talents.TalentName(id), "Fiancheggiatore", "Flanker", "Flankierer", "Flanqueador", "Flanqueur"),
            ("initiative-flanker", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Anche il tuo secondo dado d'iniziativa conta di più.", "Your second initiative die also counts for more.", "Auch dein zweiter Initiativwürfel zählt mehr.", "Tu segundo dado de iniciativa también cuenta más.", "Votre deuxième dé d'initiative compte aussi davantage."),
            ("initiative-rearguard", 0) => Local(GameTextKeys.Talents.TalentName(id), "Retroguardia", "Rearguard", "Nachhut", "Retaguardia", "Arrière-garde"),
            ("initiative-rearguard", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Anche il tuo terzo dado d'iniziativa conta di più.", "Your third initiative die also counts for more.", "Auch dein dritter Initiativwürfel zählt mehr.", "Tu tercer dado de iniciativa también cuenta más.", "Votre troisième dé d'initiative compte aussi davantage."),
            ("initiative-opening", 0) => Local(GameTextKeys.Talents.TalentName(id), "Apertura", "Opening", "Eröffnung", "Apertura", "Ouverture"),
            ("initiative-opening", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Sei sempre tu ad aprire lo scontro: il tuo primo dado d'iniziativa batte qualunque numero in campo.", "You always open the fight: your first initiative die beats every number on the field.", "Du eröffnest immer den Kampf: Dein erster Initiativwürfel schlägt jede Zahl auf dem Feld.", "Siempre abres el combate: tu primer dado de iniciativa supera cualquier número en el campo.", "Vous ouvrez toujours le combat : votre premier dé d'initiative bat tous les nombres sur le terrain."),
            ("mastery-apprentice", 0) => Local(GameTextKeys.Talents.TalentName(id), "Apprendista", "Apprentice", "Lehrling", "Aprendiz", "Apprenti"),
            ("mastery-apprentice", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Ogni livello della run arriva prima: serve meno esperienza per ogni soglia.", "Each run level comes sooner: every threshold needs less experience.", "Jede Durchlaufstufe kommt früher: Jede Schwelle benötigt weniger Erfahrung.", "Cada nivel de la partida llega antes: cada umbral requiere menos experiencia.", "Chaque niveau d'expédition arrive plus tôt : chaque seuil demande moins d'expérience."),
            ("mastery-focus", 0) => Local(GameTextKeys.Talents.TalentName(id), "Concentrazione", "Focus", "Konzentration", "Concentración", "Concentration"),
            ("mastery-focus", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Recuperi mana ogni volta che entri in una stanza nuova.", "Recover mana whenever you enter a new room.", "Erhalte Mana zurück, wann immer du einen neuen Raum betrittst.", "Recuperas maná cada vez que entras en una nueva sala.", "Récupérez du mana chaque fois que vous entrez dans une nouvelle salle."),
            ("mastery-reserve", 0) => Local(GameTextKeys.Talents.TalentName(id), "Riserva", "Reserve", "Reserve", "Reserva", "Réserve"),
            ("mastery-reserve", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "La tua riserva di mana tiene di più: il tetto si alza da 10 fino a 12.", "Your mana reserve holds more: its cap rises from 10 to 12.", "Deine Manreserve fasst mehr: Ihr Limit steigt von 10 auf 12.", "Tu reserva de maná aguanta más: su límite sube de 10 a 12.", "Votre réserve de mana contient plus : son plafond passe de 10 à 12."),
            ("mastery-trance", 0) => Local(GameTextKeys.Talents.TalentName(id), "Trance", "Trance", "Trance", "Trance", "Transe"),
            ("mastery-trance", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "La prima abilità base che usi in ogni stanza non costa mana. Le supreme si pagano comunque.", "The first basic ability used in each room costs no mana. Supremes still cost mana.", "Die erste Grundfähigkeit in jedem Raum kostet kein Mana. Ultimative Fähigkeiten kosten weiterhin Mana.", "La primera habilidad básica que uses en cada sala no cuesta maná. Las supremas siguen costando maná.", "La première capacité de base utilisée dans chaque salle ne coûte pas de mana. Les ultimes en coûtent toujours."),
            ("occasion-recovery", 0) => Local(GameTextKeys.Talents.TalentName(id), "Recupero", "Recovery", "Wiederherstellung", "Recuperación", "Récupération"),
            ("occasion-recovery", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Al mercato, riportare una carta dal cimitero nel mazzo costa meno oro.", "At the market, returning a card from the graveyard to the deck costs less gold.", "Auf dem Markt kostet es weniger Gold, eine Karte vom Friedhof ins Deck zurückzuholen.", "En el mercado, devolver una carta del cementerio al mazo cuesta menos oro.", "Au marché, ramener une carte du cimetière dans le deck coûte moins d'or."),
            ("occasion-challenger", 0) => Local(GameTextKeys.Talents.TalentName(id), "Sfidante", "Challenger", "Herausforderer", "Retador", "Challenger"),
            ("occasion-challenger", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Il tuo primo attacco in ogni scontro contro un boss o un miniboss colpisce più forte.", "Your first attack in every fight against a boss or miniboss hits harder.", "Dein erster Angriff in jedem Kampf gegen einen Boss oder Miniboss trifft härter.", "Tu primer ataque en cada combate contra un jefe o minijefe golpea más fuerte.", "Votre première attaque de chaque combat contre un boss ou mini-boss frappe plus fort."),
            ("occasion-seeker", 0) => Local(GameTextKeys.Talents.TalentName(id), "Cercatore", "Seeker", "Sucher", "Buscador", "Chercheur"),
            ("occasion-seeker", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "Ogni stanza bottino ti consegna consumabili in più.", "Every loot room grants extra consumables.", "Jeder Beuteraum gewährt zusätzliche Verbrauchsgegenstände.", "Cada sala de botín concede consumibles adicionales.", "Chaque salle de butin accorde des consommables supplémentaires."),
            ("occasion-second-wind", 0) => Local(GameTextKeys.Talents.TalentName(id), "Secondo fiato", "Second Wind", "Zweiter Atem", "Segundo aliento", "Second souffle"),
            ("occasion-second-wind", 1) => Local(GameTextKeys.Talents.TalentDescription(id), "La prima pedina che perdi in ogni run non va al cimitero: torna subito nel mazzo.", "The first pawn you lose each run does not go to the graveyard: it returns to the deck immediately.", "Die erste Figur, die du pro Durchlauf verlierst, geht nicht auf den Friedhof: Sie kehrt sofort ins Deck zurück.", "La primera ficha que pierdes en cada partida no va al cementerio: vuelve al mazo de inmediato.", "Le premier pion perdu à chaque expédition ne va pas au cimetière : il retourne immédiatement dans le deck."),
            _ => GameText.GetOrFallbackSilent(field == 0 ? GameTextKeys.Talents.TalentName(id) : GameTextKeys.Talents.TalentDescription(id), fallback)
        };

        private static string Local(string key, string it, string en, string de, string es, string fr, params object[] args) =>
            GameText.GetLocalizedFallback(key, it, en, de, es, fr, args);
    }
}
