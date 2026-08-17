namespace AccardND.Presentation.ReviewPrompt
{
    /// <summary>
    /// Come si comporta il popup una volta che il giocatore ha scelto le stelle.
    /// </summary>
    public enum ReviewPromptMode
    {
        /// <summary>
        /// Le stelle decidono: 5 stelle aprono il Play Store, meno di 5 no.
        ///
        /// <b>NON USARE IN PRODUZIONE.</b> Questo e' "review gating" e le linee guida di
        /// Google Play lo vietano: filtrare chi arriva allo store in base al giudizio
        /// espresso e' considerato manipolazione delle recensioni, e la sanzione va dal
        /// rifiuto dell'aggiornamento alla rimozione dell'app. Vedi
        /// https://support.google.com/googleplay/android-developer/answer/9898684
        ///
        /// Resta nel codice solo come termine di paragone nel banco di debug: la
        /// modalita' di produzione e' <see cref="DirectAsk"/> (scelta il 2026-08-16).
        /// </summary>
        StarGate,

        /// <summary>
        /// Nessun filtro: il popup chiede se si vuole recensire e chi accetta va allo
        /// store, qualunque cosa pensi del gioco. E' la modalita' conforme.
        /// </summary>
        DirectAsk
    }

    /// <summary>
    /// Decide se il popup di recensione va mostrato. E' volutamente logica pura, senza
    /// UnityEngine e senza PlayerPrefs: cosi' e' verificabile dai test EditMode e la
    /// regola resta leggibile in un punto solo.
    /// </summary>
    public static class ReviewPromptPolicy
    {
        /// <summary>Il capitolo dopo il quale si chiede la recensione.</summary>
        public const string TriggerChapterId = "chapter-1";

        /// <summary>
        /// Lo stato persistente del popup. Vive in PlayerPrefs
        /// (<see cref="ReviewPromptState"/>), ma qui arriva come dato per poter essere
        /// costruito a mano nei test.
        /// </summary>
        public readonly struct Request
        {
            public Request(
                string chapterId,
                bool runCompleted,
                bool isAndroid,
                bool alreadyPrompted,
                bool alreadyRated)
            {
                ChapterId = chapterId;
                RunCompleted = runCompleted;
                IsAndroid = isAndroid;
                AlreadyPrompted = alreadyPrompted;
                AlreadyRated = alreadyRated;
            }

            /// <summary>Il capitolo della run appena finita ("chapter-1", "free-run"...).</summary>
            public string ChapterId { get; }

            /// <summary>True se la run e' stata vinta, false se il giocatore e' morto.</summary>
            public bool RunCompleted { get; }

            public bool IsAndroid { get; }

            /// <summary>True se il popup e' gia' stato mostrato una volta.</summary>
            public bool AlreadyPrompted { get; }

            /// <summary>True se il giocatore e' gia' stato mandato allo store.</summary>
            public bool AlreadyRated { get; }
        }

        /// <summary>
        /// Il popup si mostra una sola volta, su Android, alla prima run di capitolo 1
        /// portata a termine.
        ///
        /// Perche' solo dopo una vittoria: chiedere una recensione a chi ha appena perso
        /// e' il momento peggiore possibile, e le poche stelle che raccoglie restano
        /// sullo store per sempre. Perche' solo una volta: un popup che ritorna dopo
        /// essere stato chiuso smette di essere una domanda e diventa un fastidio.
        /// </summary>
        public static bool ShouldPrompt(in Request request)
        {
            if (!request.IsAndroid)
                return false;

            if (request.AlreadyPrompted || request.AlreadyRated)
                return false;

            if (!request.RunCompleted)
                return false;

            return string.Equals(
                (request.ChapterId ?? string.Empty).Trim(),
                TriggerChapterId,
                System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Dato il voto scelto e la modalita', si apre il Play Store?
        ///
        /// In <see cref="ReviewPromptMode.StarGate"/> solo il punteggio pieno passa; in
        /// <see cref="ReviewPromptMode.DirectAsk"/> il voto non viene nemmeno chiesto e
        /// qualunque conferma porta allo store.
        /// </summary>
        public static bool ShouldOpenStore(ReviewPromptMode mode, int stars) => mode switch
        {
            ReviewPromptMode.StarGate => stars >= MaxStars,
            ReviewPromptMode.DirectAsk => true,
            _ => false
        };

        public const int MaxStars = 5;
    }
}
