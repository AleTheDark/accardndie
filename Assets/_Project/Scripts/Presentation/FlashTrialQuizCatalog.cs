using AccardND.Localization;

namespace AccardND.Presentation
{
    /// <summary>
    /// Fonte unica dei contenuti del quiz. Per aggiungere una domanda inserire una voce
    /// in Questions e aggiungere le relative chiavi alla String Table Unity "Game".
    /// I testi italiani qui presenti sono fallback utilizzabili anche senza traduzione.
    ///
    /// La sessione ne estrae tre a caso (<see cref="AccardND.GameCore.FlashTrialQuizSession"/>),
    /// quindi il catalogo deve restare abbastanza largo da non ripetersi da una prova
    /// all'altra. Le risposte non vengono mescolate a schermo: la posizione di quella
    /// giusta e' quella scritta qui, e va tenuta distribuita fra 0, 1 e 2 - se tutte le
    /// domande avessero la soluzione nello stesso slot, il quiz si vincerebbe senza
    /// leggerlo.
    ///
    /// Le domande devono restare rispondibili da chi gioca: regole mostrate in partita,
    /// classi, aure, mana e struttura della campagna. Niente numeri di bilanciamento che
    /// il giocatore non vede mai a schermo.
    /// </summary>
    public static class FlashTrialQuizCatalog
    {
        public static readonly FlashTrialQuizQuestion[] Questions =
        {
            // --- Fazioni e triangolo ---------------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.magic_faction",
                "Quale classe appartiene alla fazione Magica?",
                new[] { "Guerriero", "Necromante", "Cacciatore" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.might_counter",
                "Quale fazione batte la Forzuta?",
                new[] { "Magica", "Astuta", "Nessuna" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.cunning_counter",
                "Quale fazione batte l'Astuta?",
                new[] { "Magica", "Astuta", "Forzuta" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.magic_counter",
                "Quale fazione batte la Magica?",
                new[] { "Magica", "Astuta", "Forzuta" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.paladin_faction",
                "A quale fazione appartiene il Paladino?",
                new[] { "Astuta", "Magica", "Forzuta" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.assassin_faction",
                "A quale fazione appartiene l'Assassino?",
                new[] { "Forzuta", "Astuta", "Magica" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.priest_faction",
                "A quale fazione appartiene il Sacerdote?",
                new[] { "Forzuta", "Astuta", "Magica" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.barbarian_faction",
                "A quale fazione appartiene il Barbaro?",
                new[] { "Magica", "Astuta", "Forzuta" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.advantage_roll",
                "Con il vantaggio di fazione, come si tira?",
                new[] { "Un dado solo", "Due dadi, tieni il migliore", "Due dadi, tieni il peggiore" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.disadvantage_roll",
                "Con lo svantaggio di fazione, come si tira?",
                new[] { "Due dadi, tieni il migliore", "Un dado solo", "Due dadi, tieni il peggiore" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.neutral_roll",
                "In un confronto neutro quanti dadi si tirano?",
                new[] { "Uno", "Due", "Tre" },
                correctAnswerIndex: 0),

            // --- Mazzo, mano e schieramento --------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.deck_size",
                "Da quante carte e' composto il mazzo iniziale?",
                new[] { "9 carte", "6 carte", "12 carte" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.hand_size",
                "Quante carte compongono la mano prima di un combattimento?",
                new[] { "9 carte", "6 carte", "3 carte" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.formation_size",
                "Quante carte si schierano in combattimento?",
                new[] { "Due", "Tre", "Quattro" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.card_max_value",
                "Qual e' il valore piu' alto che puo' avere una carta?",
                new[] { "12", "20", "10" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.card_min_value",
                "Qual e' il valore piu' basso che puo' avere una carta?",
                new[] { "1", "2", "3" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.graveyard",
                "Le carte finite nel cimitero...",
                new[] { "Tornano in mano dopo un turno", "Non sono piu' disponibili", "Rientrano nel mazzo" },
                correctAnswerIndex: 1),

            // --- Il Vigore --------------------------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.vigor_lowest",
                "Qual e' il dado piu' basso della scala del Vigore?",
                new[] { "D4", "D6", "D2" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.vigor_highest",
                "Qual e' il dado piu' alto della scala del Vigore?",
                new[] { "D20", "D12", "D100" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.vigor_step_down_d20",
                "Abbassato di uno scalino, il D20 diventa...",
                new[] { "D10", "D12", "D8" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.vigor_step_down_d6",
                "Abbassato di uno scalino, il D6 diventa...",
                new[] { "D2", "D8", "D4" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.vigor_floor",
                "Chi e' gia' al D2 e subisce un altro malus al Vigore...",
                new[] { "Scende sotto il D2", "Resta al D2", "Non tira piu' i dadi" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.vigor_campaign_start",
                "In campagna, da quale dado parte il Vigore?",
                new[] { "D4", "D6", "D8" },
                correctAnswerIndex: 0),

            // --- Mana --------------------------------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_attack_cost",
                "Quanto mana costa un attacco normale?",
                new[] { "Niente", "1", "2" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_cap",
                "Qual e' il tetto base della riserva di mana?",
                new[] { "6", "20", "10" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_skip",
                "Quanto mana recupera una pedina che salta il turno?",
                new[] { "+1", "+2", "+3" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_kill",
                "Quanto mana si guadagna eliminando una pedina nemica?",
                new[] { "+1", "+2", "Niente" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_parry",
                "Oltre alla fine del turno, cosa fa guadagnare mana?",
                new[] { "Perdere una pedina", "Cambiare schieramento", "Parare un colpo" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_free_classes",
                "Quali classi non pagano mana per la loro abilita'?",
                new[] { "Ladro e Barbaro", "Mago e Sacerdote", "Paladino e Cacciatore" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_expensive_primary",
                "Quale abilita' di classe costa piu' mana?",
                new[] { "Necromante", "Guerriero", "Assassino" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_persists",
                "Cosa passa da una stanza all'altra?",
                new[] { "I potenziamenti", "I malus", "Il mana" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.mana_room_floor",
                "A inizio stanza, chi ha meno di 2 mana...",
                new[] { "Risale a 2", "Resta come sta", "Salta il primo turno" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_per_turn",
                "Esistono degli oggetti per recuperare mana?",
                new[] { "Si", "Si, due", "No" },
                correctAnswerIndex: 1),

            // --- Abilita' di classe -------------------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_hunter",
                "Chi marca un nemico per potenziare il prossimo attacco?",
                new[] { "Assassino", "Mago", "Cacciatore" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_assassin",
                "Chi fa saltare il turno a un nemico?",
                new[] { "Assassino", "Sacerdote", "Ladro" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_mage",
                "Chi abbassa di una taglia il dado Vigore nemico?",
                new[] { "Ladro", "Barbaro", "Mago" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_necromancer",
                "Chi riporta in campo un alleato eliminato?",
                new[] { "Necromante", "Sacerdote", "Paladino" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_barbarian",
                "Chi accumula Furia quando perde gli scambi?",
                new[] { "Guerriero", "Barbaro", "Assassino" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_paladin",
                "Chi devia su di se' l'attacco diretto a un alleato?",
                new[] { "Guerriero", "Ladro", "Paladino" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_priest",
                "Chi purifica i malus di un alleato e lo benedice?",
                new[] { "Mago", "Necromante", "Sacerdote" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_rogue",
                "Il Ladro, se il totale non basta a vincere...",
                new[] { "Ritira i dadi piu' bassi", "Ruba mana al nemico", "Raddoppia il suo valore" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.ability_warrior",
                "L'abilita' del Guerriero somma...",
                new[] { "Due dadi Vigore uguali", "Il Vigore e un dado piu' piccolo", "Il valore di due carte" },
                correctAnswerIndex: 1),

            // --- Supreme -------------------------------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.supreme_unlock",
                "Dove si sbloccano le abilita' supreme?",
                new[] { "Dal Mercante", "Al Santuario", "In Taverna" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.supreme_duration",
                "Gli effetti di una suprema durano...",
                new[] { "Tutta la run", "Fino a fine stanza", "Per sempre" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.supreme_mage",
                "Come si chiama la suprema del Mago?",
                new[] { "Palla di Fuoco", "Raffica", "Cornamusa" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.supreme_barbarian",
                "Quale suprema da' Furia a tutta la squadra?",
                new[] { "Cornamusa", "Raffica", "Riserva" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.supreme_paladin",
                "A quanto porta il mana la Riserva del Paladino?",
                new[] { "6", "4", "10" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.supreme_assassin",
                "Quale suprema rende una pedina non bersagliabile?",
                new[] { "Purificazione", "Scippo", "Invisibilita'" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.supreme_repeat",
                "La seconda suprema della stessa classe, nella stessa stanza...",
                new[] { "Costa 1 mana in piu'", "Costa uguale", "Non si puo' usare" },
                correctAnswerIndex: 0),

            // --- Aure ----------------------------------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_limit",
                "Quante aure possono essere attive insieme?",
                new[] { "Due", "Tre", "Una sola" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_class_condition",
                "Cosa attiva l'Aura di Classe?",
                new[] { "Tre carte della stessa fazione", "Tre carte della stessa classe", "Una carta per fazione" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_formation_condition",
                "Cosa attiva l'Aura di Formazione?",
                new[] { "Una carta per fazione", "Tre carte uguali", "Tre carte Magiche" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_formation_effect",
                "L'Aura di Formazione cosa annulla?",
                new[] { "I malus subiti", "Il costo del mana", "Lo svantaggio in attacco" },
                correctAnswerIndex: 2),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_magic",
                "Cosa fa l'Aura Magica?",
                new[] { "Difende con un dado piu' alto", "Attacca sempre con vantaggio", "Raddoppia il mana" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_might",
                "Con l'Aura Forzuta, ogni volta che muore una pedina...",
                new[] { "Recuperi 2 mana", "Le tue Forzute prendono +1", "Non succede niente" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_cunning",
                "L'Aura Astuta da' vantaggio contro i nemici...",
                new[] { "Che hanno bonus o malus", "Ancora a piena vita", "Della fazione Magica" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.aura_priority",
                "Tre carte della stessa classe attivano...",
                new[] { "Solo l'Aura di Classe", "Classe e Fazione insieme", "Solo l'Aura di Fazione" },
                correctAnswerIndex: 0),

            // --- Campagna e Sfida Veloce ----------------------------------------------------
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.answer_count",
                "Quante risposte sono presenti in questa Prova Lampo?",
                new[] { "Due", "Tre", "Quattro" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.combat_tie",
                "In caso di parita' nel combattimento, chi vince?",
                new[] { "Attaccante", "Difensore", "Entrambi" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.forfeit_penalty",
                "Se rinunci alla Sfida Veloce, la prossima stanza Mostro...",
                new[] { "Da' meta' EXP e oro", "Non cambia", "Viene saltata" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.tavern_quests",
                "Le missioni giornaliere della Taverna pagano in...",
                new[] { "Oro", "Miele", "Propoli" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.sanctuary_offer",
                "Cosa si sblocca al Santuario?",
                new[] { "Classi, tecniche e oggetti", "Soltanto carte nuove", "Soltanto oro" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.chapter_one_boss",
                "Chi e' il boss del primo capitolo?",
                new[] { "Bragus", "Trentor", "Palatir" },
                correctAnswerIndex: 1),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.chapter_two_boss",
                "Chi regna sullo scenario della Nebbia?",
                new[] { "Bragus", "Medusa", "Seraphel" },
                correctAnswerIndex: 0),
            new FlashTrialQuizQuestion(
                "quick_challenge.quiz.chapter_one_reward",
                "Quale classe si ottiene completando il primo capitolo?",
                new[] { "Barbaro", "Paladino", "Cacciatore" },
                correctAnswerIndex: 2)
        };
    }

    public sealed class FlashTrialQuizQuestion
    {
        private readonly string fallbackQuestion;
        private readonly string[] fallbackAnswers;

        public FlashTrialQuizQuestion(
            string localizationId,
            string fallbackQuestion,
            string[] fallbackAnswers,
            int correctAnswerIndex)
        {
            LocalizationId = localizationId;
            this.fallbackQuestion = fallbackQuestion;
            this.fallbackAnswers = fallbackAnswers;
            CorrectAnswerIndex = correctAnswerIndex;
        }

        public string LocalizationId { get; }
        public int CorrectAnswerIndex { get; }

        /// <summary>
        /// Quante risposte ha la domanda. La schermata ne disegna esattamente tre, quindi
        /// serve a far fallire una voce malformata nei test invece che a runtime.
        /// </summary>
        public int AnswerCount => fallbackAnswers.Length;

        public string LocalizedQuestion => GameText.GetOrFallbackSilent(
            LocalizationId + ".question", fallbackQuestion);

        public string LocalizedAnswer(int index) => GameText.GetOrFallbackSilent(
            LocalizationId + ".answer_" + index, fallbackAnswers[index]);
    }
}
