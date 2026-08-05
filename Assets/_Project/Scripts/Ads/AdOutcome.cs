namespace AccardND.Ads
{
    /// <summary>Com'e' finita la richiesta di un annuncio.</summary>
    public enum AdOutcome
    {
        /// <summary>Vista fino in fondo: e' l'unico esito che paga una ricompensa.</summary>
        Watched,

        /// <summary>Chiusa dal giocatore prima della fine.</summary>
        Skipped,

        /// <summary>Nessun annuncio da mostrare: niente riempimento, offline, o nessun provider.</summary>
        NoFill,

        /// <summary>Il provider ha risposto con un errore.</summary>
        Failed,

        /// <summary>Fermata dalle nostre regole di frequenza. Non e' un guasto.</summary>
        Suppressed
    }

    /// <summary>
    /// Esito di una richiesta ad <see cref="AdService"/>.
    ///
    /// Le due letture da fare al punto di chiamata sono diverse e non vanno confuse:
    /// <see cref="Watched"/> decide se si paga - ed e' l'unico esito che paga, sempre, anche
    /// davanti a una ricompensa gia' guadagnata - mentre <see cref="Unavailable"/> serve solo
    /// a spiegare il no: "la rete non ha annunci, riprova" e "l'hai chiusa a meta'" sono due
    /// messaggi diversi, e dare il secondo a chi ha subito il primo lo fa sentire imbrogliato.
    /// </summary>
    public readonly struct AdResult
    {
        public readonly AdOutcome Outcome;

        /// <summary>
        /// Identificativo dell'impression secondo il provider: e' il riferimento che il
        /// server usa per non pagare due volte la stessa pubblicita' e, quando arrivera' la
        /// verifica lato server (SSV), per riconoscere l'impression segnalata dalla rete.
        /// Vuoto se l'annuncio non e' stato visto.
        /// </summary>
        public readonly string ImpressionId;

        public AdResult(AdOutcome outcome, string impressionId = null)
        {
            Outcome = outcome;
            ImpressionId = impressionId ?? string.Empty;
        }

        /// <summary>Vista per intero: la ricompensa spetta.</summary>
        public bool Watched => Outcome == AdOutcome.Watched;

        /// <summary>
        /// Non c'e' stata pubblicita' per motivi che non dipendono dal giocatore. Non apre
        /// nessun cancello: serve a scegliere le parole del rifiuto, e a distinguere nei log
        /// un guasto della pila pubblicitaria da un giocatore che ha cambiato idea.
        /// </summary>
        public bool Unavailable =>
            Outcome == AdOutcome.NoFill || Outcome == AdOutcome.Failed || Outcome == AdOutcome.Suppressed;

        public static AdResult Of(AdOutcome outcome) => new AdResult(outcome);
    }
}

