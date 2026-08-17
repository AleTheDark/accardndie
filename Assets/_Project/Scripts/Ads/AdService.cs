using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace AccardND.Ads
{
    /// <summary>
    /// Il punto da cui il gioco chiede una pubblicita'. Sopra c'e' il gameplay, che conosce
    /// solo i <see cref="AdPlacement"/>; sotto c'e' un <see cref="IAdProvider"/>, che conosce
    /// un SDK. In mezzo stanno le tre cose che nessuno dei due deve rifare: le regole di
    /// frequenza (<see cref="AdPolicy"/>), la pausa del gioco mentre l'annuncio e' a schermo,
    /// e la garanzia che non partano due annunci insieme.
    ///
    /// Nessuna chiamata qui dentro puo' lanciare verso il gameplay: se la rete pubblicitaria
    /// va giu', il gioco continua senza pubblicita'.
    /// </summary>
    public static class AdService
    {
        private static IAdProvider provider;
        private static Task<bool> initialization;
        private static bool showing;

        /// <summary>
        /// Aggancio per il log del gioco (AppendLog e simili). Facoltativo: se nessuno lo
        /// riempie, le righe finiscono solo nella console di Unity.
        /// </summary>
        public static Action<string> Log;

        /// <summary>
        /// Un annuncio e' a schermo adesso. Chi disegna la UI puo' usarlo per non far partire
        /// animazioni o suoni sotto la pubblicita'.
        /// </summary>
        public static bool IsShowingAd => showing;

        /// <summary>
        /// Le ultime righe di diario della pubblicita'. Esistono perche' su un telefono con
        /// un apk installato a mano non c'e' logcat: se non e' il gioco a dire cosa e'
        /// successo, non lo dice nessuno.
        /// </summary>
        private static readonly System.Collections.Generic.List<string> recentLog =
            new System.Collections.Generic.List<string>();

        private const int RecentLogCapacity = 25;

        public static System.Collections.Generic.IReadOnlyList<string> RecentLog => recentLog;

        /// <summary>Chi sta servendo gli annunci, per la schermata di diagnostica.</summary>
        public static string ActiveProviderId => provider?.ProviderId ?? "nessuno";

        /// <summary>
        /// Su questo canale una ricompensa dietro un annuncio si concede anche quando
        /// l'annuncio non c'e'. E' vero solo sul web, ed e' una misura temporanea: finche'
        /// AdSense non ha approvato il sito non arriva nessun annuncio, e un cancello
        /// pubblicitario diventa una porta chiusa e basta - il miele delle quest, che e'
        /// l'unica fonte del gioco, sarebbe irraggiungibile per tutti.
        ///
        /// Resta comunque vero anche dopo l'approvazione che sul web un blocco pubblicita'
        /// e' normale, quindi questa non e' una riga che si toglie a cuor leggero: toglierla
        /// significa accettare di perdere i giocatori che ne hanno uno.
        ///
        /// Quando si decide di rimuoverla si tocca solo questo punto: nessun altro file
        /// nomina la piattaforma, e i punti di chiamata continuano a chiedere
        /// <see cref="AdResult.Grants"/> senza sapere perche' la risposta e' si'.
        ///
        /// ACCARDND_WAIVE_ADS lo forza ovunque: serve a provare il condono in editor, dove
        /// altrimenti risponderebbe la pubblicita' finta e non si vedrebbe mai questo ramo.
        /// </summary>
        public static bool RewardsWaivedWithoutAds =>
            AdsRemoved ||
#if ACCARDND_WAIVE_ADS || (UNITY_WEBGL && !UNITY_EDITOR)
            true;
#else
            false;
#endif

        /// <summary>
        /// L'account ha comprato la rimozione della pubblicita'. Lo dice il server dopo aver
        /// verificato la ricevuta: qui si tiene solo l'ultima risposta.
        ///
        /// Toglie gli annunci ma non le ricompense. I cancelli pubblicitari del gioco - il
        /// miele delle quest, il premio di giornata, l'EXP tripla - sono il modo con cui si
        /// guadagna, non un pedaggio: chi paga per non vedere la pubblicita' compra il tempo
        /// che avrebbe speso a guardarla, non una progressione piu' lenta. Per questo entra
        /// in <see cref="RewardsWaivedWithoutAds"/>, che e' la stessa strada gia' battuta sul
        /// web quando un annuncio non arriva.
        /// </summary>
        public static bool AdsRemoved { get; set; }

        /// <summary>
        /// Il provider e' stato preparato con successo. Falso puo' voler dire "non ancora"
        /// oppure "non ci riesce": la differenza sta nelle righe di <see cref="RecentLog"/>.
        /// </summary>
        public static bool IsProviderReady =>
            initialization != null && initialization.IsCompleted && initialization.Result;

        /// <summary>
        /// Sostituisce il provider. E' cosi' che AdMob (o l'adattatore web) si presenta senza
        /// che questa classe debba conoscerlo: va chiamata prima del primo annuncio.
        /// </summary>
        public static void SetProvider(IAdProvider newProvider)
        {
            provider = newProvider;
            initialization = null;
            Write($"provider impostato: {newProvider?.ProviderId ?? "nessuno"}.");
        }

        /// <summary>
        /// C'e' qualcosa da mostrare per questo placement. Serve a decidere se disegnare un
        /// bottone "guarda la pubblicita'": offrirlo e poi non avere niente e' peggio che
        /// non offrirlo. Non prepara il provider, quindi prima del primo annuncio risponde no.
        /// </summary>
        public static bool IsReady(AdPlacement placement)
        {
            // Dove la ricompensa si concede comunque, il bottone deve comparire comunque:
            // e' costruito su questa risposta, e senza di essa non ci sarebbe niente da
            // premere e nessun modo di riscuotere.
            if (RewardsWaivedWithoutAds)
                return true;

            if (provider == null || initialization == null || !initialization.IsCompleted)
                return false;
            if (!AdPolicy.Allows(AdPlacements.FormatOf(placement), out _))
                return false;
            try
            {
                return provider.IsReady(placement);
            }
            catch (Exception exception)
            {
                Write($"IsReady({AdPlacements.KeyOf(placement)}) fallita: {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Avvisa che fra poco potrebbe servire un annuncio per questo placement: si apre la
        /// taverna, comincia una run, parte una partita d'arena. E' il solo modo di far partire
        /// una richiesta alla rete, e va chiamata con qualche secondo di anticipo, perche' fino
        /// a quando l'annuncio non e' arrivato <see cref="IsReady"/> risponde no e i bottoni
        /// costruiti su quella risposta non compaiono.
        ///
        /// Non aspetta e non lancia: se la rete non c'e', il posto si apre lo stesso e la
        /// pubblicita' semplicemente non ci sara'.
        /// </summary>
        public static void Warm(AdPlacement placement)
        {
            // Chi ha comprato la rimozione non vedra' nessun annuncio: caricarlo sarebbe una
            // richiesta ad AdMob che nessuno guardera' mai.
            if (AdsRemoved)
                return;

            // Un interstitial oltre il tetto di sessione non si mostrera' comunque: chiederlo
            // sarebbe inventario buttato, ed e' esattamente il rapporto richieste/impressioni
            // che si sta cercando di tenere sano.
            if (!AdPolicy.Allows(AdPlacements.FormatOf(placement), out string reason))
            {
                Write($"{AdPlacements.KeyOf(placement)}: non si prepara niente ({reason}).");
                return;
            }
            _ = WarmAsync(placement);
        }

        /// <summary>
        /// Il giocatore ha lasciato il posto che aveva chiesto l'annuncio. Quello gia' caricato
        /// resta buono - se torna indietro fra un minuto lo trova pronto - ma nessuno ne
        /// carichera' un altro dopo il prossimo show.
        /// </summary>
        public static void Cool(AdPlacement placement)
        {
            if (provider == null)
                return;
            try
            {
                provider.Cool(placement);
            }
            catch (Exception exception)
            {
                Write($"Cool({AdPlacements.KeyOf(placement)}) fallita: {exception.Message}");
            }
        }

        private static async Task WarmAsync(AdPlacement placement)
        {
            if (!await EnsureProviderAsync())
                return;
            try
            {
                provider.Warm(placement);
                Write($"{AdPlacements.KeyOf(placement)}: annuncio in preparazione.");
            }
            catch (Exception exception)
            {
                Write($"{AdPlacements.KeyOf(placement)}: preparazione fallita ({exception.Message}).");
            }
        }

        /// <summary>
        /// Mostra un annuncio e restituisce com'e' andata. Non lancia mai: al punto di
        /// chiamata si legge <see cref="AdResult.Grants"/> per decidere se pagare, e
        /// <see cref="AdResult.Unavailable"/> per distinguere "la rete non ha annunci" da
        /// "l'ha chiuso a meta'", che al giocatore vanno spiegati in modo diverso.
        ///
        /// La domanda giusta e' <see cref="AdResult.Grants"/> e non
        /// <see cref="AdResult.Watched"/>: dove vale <see cref="RewardsWaivedWithoutAds"/> le
        /// due risposte si separano, perche' la ricompensa spetta anche senza annuncio.
        /// <see cref="AdResult.Watched"/> resta la domanda su quante pubblicita' sono state
        /// davvero guardate, che e' un'altra cosa e serve ai tetti di frequenza.
        ///
        /// <paramref name="asGate"/> dice che l'annuncio sta davanti a una ricompensa e non
        /// la accompagna. Cambia due cose: si aspetta il caricamento invece di rinunciare
        /// subito (il giocatore ha premuto sapendo che arriva una pubblicita', e dirgli di no
        /// mentre l'annuncio sta arrivando gli costerebbe la ricompensa), e non si applicano
        /// le regole di frequenza, che esistono per difenderlo dagli annunci che non ha
        /// chiesto. Sopra, chi ha aperto il cancello paga solo su <see cref="AdResult.Grants"/>:
        /// dove non c'e' condono, niente pubblicita' vuol dire niente riscossione e la
        /// ricompensa resta li'.
        ///
        /// Su un cancello fatto con un interstitial "guardato" resta pero' una parola del
        /// client: gli interstitial non hanno SSV, quindi il server non puo' verificare
        /// niente. Solo un rewarded permette di pretendere l'impression verificata prima di
        /// accreditare (vedi Docs/ads-design.md).
        /// </summary>
        public static async Task<AdResult> ShowAsync(
            AdPlacement placement, AdRewardContext context = default, bool asGate = false)
        {
            AdFormat format = AdPlacements.FormatOf(placement);
            string key = AdPlacements.KeyOf(placement);

            // Rimozione comprata: nessun annuncio parte. Davanti a un cancello si passa
            // comunque per il condono, che concede la ricompensa senza mostrare niente.
            if (AdsRemoved)
            {
                Write($"{key}: pubblicita' rimossa dall'acquisto.");
                return asGate ? Unavailable(AdOutcome.NoFill, key) : AdResult.Of(AdOutcome.Suppressed);
            }

            // Due annunci insieme non sono un caso di scuola: un doppio tocco sul bottone di
            // riscossione basta a produrli.
            if (showing)
            {
                Write($"{key}: annuncio saltato, ce n'e' gia' uno a schermo.");
                return AdResult.Of(AdOutcome.Suppressed);
            }

            // Le regole di frequenza difendono il giocatore dalla pubblicita' che non ha
            // chiesto, e un cancello e' il caso opposto: l'ha chiesta lui, in cambio di
            // qualcosa. Applicarle qui vorrebbe dire togliergli il miele delle quest perche'
            // ha gia' visto troppi interstitial altrove.
            if (!asGate && !AdPolicy.Allows(format, out string reason))
            {
                Write($"{key}: interstitial saltato ({reason}).");
                return AdResult.Of(AdOutcome.Suppressed);
            }

            if (!await EnsureProviderAsync())
                return Unavailable(AdOutcome.NoFill, key);

            // Davanti a un cancello si aspetta il caricamento (ci pensa il provider, con la sua
            // scadenza): il giocatore ha premuto sapendo che arriva una pubblicita', e
            // rispondergli di no mentre l'annuncio sta arrivando gli costerebbe la ricompensa.
            // Fuori dai cancelli no: l'annuncio e' un di piu' e non vale un'attesa.
            if (!asGate && !provider.IsReady(placement))
            {
                // Questa occasione e' persa: si prepara la prossima, che e' anche il recupero
                // automatico se un Warm e' stato dimenticato o se l'annuncio e' scaduto.
                Write($"{key}: nessun annuncio pronto da {provider.ProviderId}.");
                Warm(placement);
                return Unavailable(AdOutcome.NoFill, key);
            }

            showing = true;
            AdPause.Begin();
            AdResult result;
            try
            {
                result = await provider.ShowAsync(placement, context);
            }
            catch (Exception exception)
            {
                Write($"{key}: annuncio fallito ({exception.Message}).");
                result = AdResult.Of(AdOutcome.Failed);
            }
            finally
            {
                AdPause.End();
                showing = false;
            }

            if (result.Watched)
                AdPolicy.RecordShown(format);
            Write($"{key}: {result.Outcome}.");

            // Un guasto della rete, o niente da mostrare: dove le ricompense sono condonate
            // il giocatore non deve pagarne il prezzo. Un annuncio chiuso a meta' invece
            // resta un no, perche' li' una pubblicita' c'era davvero ed e' stata rifiutata.
            return result.Unavailable ? Unavailable(result.Outcome, key) : result;
        }

        /// <summary>
        /// Come si risponde quando l'annuncio non c'e'. Di norma si dice com'e' andata e chi
        /// ha chiesto non incassa; dove vale <see cref="RewardsWaivedWithoutAds"/> si condona
        /// e la ricompensa spetta lo stesso.
        ///
        /// L'identificativo del condono e' marcato: il server rifiuta una richiesta senza
        /// identificativo, quindi qualcosa va mandato comunque, e mandare un id che sembri
        /// un'impressione vera sporcherebbe gli unici dati che dicono quanta pubblicita' e'
        /// stata davvero guardata. E' anche il modo per contarli, i condoni.
        /// </summary>
        private static AdResult Unavailable(AdOutcome outcome, string key)
        {
            if (!RewardsWaivedWithoutAds)
                return AdResult.Of(outcome);

            Write($"{key}: nessun annuncio ({outcome}), ricompensa concessa lo stesso.");
            return new AdResult(AdOutcome.Waived, "waived-" + Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// Prepara l'SDK all'avvio del gioco: consenso, inizializzazione, niente altro. Non
        /// carica nessun annuncio - a quello pensa <see cref="Warm"/>, dal posto che ne ha
        /// bisogno - ma toglie di mezzo in anticipo la parte lenta, cosi' il primo Warm trova
        /// la rete gia' in piedi e ha solo da chiedere. Da chiamare una volta, presto.
        /// </summary>
        public static Task<bool> PrepareAsync() => EnsureProviderAsync();

        /// <summary>
        /// Interstitial "e vai avanti comunque": non c'e' niente da aspettare, l'esito non
        /// cambia nulla per il giocatore. Chi la chiama non deve nemmeno essere async.
        ///
        /// Parte un frame dopo: chi chiama sta finendo di applicare l'effetto che ha appena
        /// prodotto (un oggetto usato, una riscossione), e coprirlo nello stesso istante in
        /// cui succede fa sembrare che la pubblicita' abbia interrotto l'azione invece di
        /// seguirla.
        /// </summary>
        public static void ShowInterstitial(AdPlacement placement)
        {
            AdRunner.Instance.Run(ShowAfterFrame(placement));
        }

        private static IEnumerator ShowAfterFrame(AdPlacement placement)
        {
            yield return null;
            _ = ShowAsync(placement);
        }

        private static async Task<bool> EnsureProviderAsync()
        {
            if (provider == null)
                SetProvider(CreateDefaultProvider());

            initialization ??= InitializeAsync(provider);
            try
            {
                return await initialization;
            }
            catch (Exception exception)
            {
                Write($"inizializzazione di {provider.ProviderId} fallita: {exception.Message}");
                return false;
            }
        }

        private static async Task<bool> InitializeAsync(IAdProvider target)
        {
            bool ready = await target.InitializeAsync();
            Write(ready
                ? $"{target.ProviderId} pronto."
                : $"{target.ProviderId} non disponibile: si gioca senza pubblicita'.");
            return ready;
        }

        /// <summary>
        /// Chi serve gli annunci quando nessuno ha chiamato <see cref="SetProvider"/>.
        ///
        /// Su Android c'e' AdMob e sul web gli H5 Games Ads di AdSense, anche nelle build di
        /// sviluppo: collaudare l'integrazione vera e' il punto, e a non generare traffico non
        /// valido pensano <see cref="AdUnits.For"/> (unita' di prova in sviluppo) e, sul web,
        /// il flag <c>ACCARDND_ADSENSE_TEST</c> del template.
        /// In editor la pubblicita' e' finta, perche' nessuno dei due SDK su desktop ha
        /// qualcosa da mostrare. Altrove non arriva nulla, che e' meglio di una pubblicita'
        /// finta addosso a un giocatore vero.
        ///
        /// ACCARDND_FAKE_ADS forza il pannello finto ovunque, per provare i punti di aggancio
        /// in una build senza SDK.
        /// </summary>
        private static IAdProvider CreateDefaultProvider()
        {
#if ACCARDND_FAKE_ADS
            return new FakeAdProvider();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new AdMobProvider();
#elif UNITY_WEBGL && !UNITY_EDITOR
            return new H5GamesAdProvider();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return new FakeAdProvider();
#else
            return new NoAdsProvider();
#endif
        }

        private static void Write(string message)
        {
            string line = "ADV - " + message;
            recentLog.Add(line);
            if (recentLog.Count > RecentLogCapacity)
                recentLog.RemoveRange(0, recentLog.Count - RecentLogCapacity);
            Log?.Invoke(line);
            Debug.Log("[Ads] " + message);
        }
    }
}
