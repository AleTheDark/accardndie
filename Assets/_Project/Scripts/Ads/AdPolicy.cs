using UnityEngine;

namespace AccardND.Ads
{
    /// <summary>
    /// Le regole di frequenza degli interstitial, cioe' l'unica difesa del giocatore contro
    /// la pubblicita' che non ha chiesto. Stanno qui e non sparse nei punti di chiamata:
    /// i placement sono cinque oggi e saranno di piu' domani, e nessuno di loro puo' sapere
    /// quanta pubblicita' hanno gia' visto gli altri.
    ///
    /// I rewarded non passano di qui: sono richiesti dal giocatore in cambio di qualcosa,
    /// e mettergli un tetto significherebbe togliergli una ricompensa che voleva. Per lo
    /// stesso motivo non ci passano gli interstitial usati come cancello davanti a una
    /// riscossione (le quest della taverna): li' la pubblicita' e' il prezzo di qualcosa che
    /// il giocatore ha chiesto, e un tetto raggiunto altrove gli chiuderebbe l'unica fonte di
    /// miele del gioco. Continuano pero' a contare per il tetto una volta mostrati, cosi' chi
    /// riscuote molto si vede meno pubblicita' non richiesta nel resto della sessione.
    ///
    /// I numeri sono tarabili: alzarli fa vedere meno pubblicita' e guadagnare meno, ma le
    /// reti pagano a impression viste, non a impression rifiutate, e un giocatore che
    /// disinstalla al secondo giorno non ne genera nessuna.
    ///
    /// La distanza minima fra due interstitial e' stata tolta: ogni aggancio che chiede un
    /// interstitial ne mostra uno, senza guardare quanto tempo e' passato dal precedente.
    /// Vuol dire che una sequenza rapida di azioni - riscuotere otto quest in taverna, usare
    /// tre oggetti di fila - produce un annuncio per azione. L'unico freno rimasto e'
    /// <see cref="MaxInterstitialsPerSession"/>.
    /// Rimetterla e' una costante e un confronto: si tenevano gli ultimi istanti in cui un
    /// interstitial e' andato a segno e si rifiutava chi arrivava troppo presto.
    /// </summary>
    public static class AdPolicy
    {
        /// <summary>
        /// Quanto dura l'esenzione a inizio sessione. E' il momento in cui il giocatore
        /// decide se il gioco vale la pena, quindi un interstitial qui costa piu' di quanto
        /// renda: la soglia dice quanto siamo disposti a rischiare su quel primo minuto.
        /// </summary>
        public const float SessionWarmupSeconds = 0f;

        /// <summary>Tetto di interstitial per sessione, contro le sessioni lunghe.</summary>
        public const int MaxInterstitialsPerSession = 40;

        private static int interstitialsThisSession;

        /// <summary>
        /// Inizio della sessione. Zero e non "il primo annuncio richiesto": l'orologio del
        /// riscaldamento deve partire da quando il giocatore ha aperto il gioco, altrimenti
        /// chi arriva al primo interstitial dopo mezz'ora se lo vedrebbe rifiutare come se
        /// avesse appena aperto l'app.
        /// </summary>
        private static float sessionStartTime;

        /// <summary>
        /// L'annuncio puo' partire. <paramref name="reason"/> spiega il no, e va nel log:
        /// senza, "la pubblicita' non parte" e' indistinguibile da un guasto del provider.
        /// </summary>
        public static bool Allows(AdFormat format, out string reason)
        {
            reason = null;
            if (format == AdFormat.Rewarded)
                return true;

            float now = Now;
            if (now - sessionStartTime < SessionWarmupSeconds)
            {
                reason = "sessione appena iniziata";
                return false;
            }
            if (interstitialsThisSession >= MaxInterstitialsPerSession)
            {
                reason = $"tetto di {MaxInterstitialsPerSession} interstitial per sessione raggiunto";
                return false;
            }
            return true;
        }

        /// <summary>Registra un annuncio davvero mostrato. Solo quelli mostrati contano per i tetti.</summary>
        public static void RecordShown(AdFormat format)
        {
            if (format != AdFormat.Interstitial)
                return;
            interstitialsThisSession++;
        }

        /// <summary>Riparte da zero. Serve alle prove: in partita la sessione e' una sola.</summary>
        public static void ResetSession()
        {
            interstitialsThisSession = 0;
            sessionStartTime = Now;
        }

        /// <summary>
        /// Tempo reale: la scala del tempo si azzera durante una pausa o un annuncio, e un
        /// orologio fermo farebbe sembrare vicinissimi due interstitial lontani minuti.
        /// </summary>
        private static float Now => Time.realtimeSinceStartup;
    }
}
