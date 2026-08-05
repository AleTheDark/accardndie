using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace AccardND.Ads
{
    /// <summary>
    /// L'adattatore verso gli H5 Games Ads di AdSense, cioe' la pubblicita' sul web e nella
    /// PWA. Parla con <c>Assets/Plugins/WebGL/AccardNdAds.jslib</c> e non conosce nient'altro:
    /// sopra c'e' <see cref="AdService"/>, che non sa da chi arrivi l'annuncio.
    ///
    /// Tre differenze rispetto ad AdMob, che non sono dettagli implementativi ma cambiano
    /// cosa si puo' promettere al giocatore:
    ///
    /// - <b>non esiste il caricamento per placement.</b> L'SDK si tiene pronto un annuncio da
    ///   solo (<c>preloadAdBreaks</c>), e non lo dice a nessuno. Quindi <see cref="Warm"/> qui
    ///   non chiede niente e <see cref="IsReady"/> non puo' rispondere davvero: risponde di si'
    ///   e impara dagli errori (vedi sotto);
    /// - <b>non esiste la verifica lato server.</b> Gli H5 Games Ads non chiamano un nostro
    ///   endpoint a video finito, quindi il x3 sul web resta parola del client, protetto solo
    ///   dall'idempotenza e dai tetti gia' in vigore lato server;
    /// - <b>il consenso non passa di qui.</b> L'equivalente web di UMP e' la CMP che Google
    ///   serve da sola dallo script di AdSense, configurata nella console (Privacy e
    ///   messaggistica). Se non e' pubblicata, al traffico europeo non arriva pubblicita' ed
    ///   e' la prima cosa da controllare quando gli annunci non compaiono.
    ///
    /// Un quarto punto vale per il prodotto piu' che per il codice: sul web un blocco
    /// pubblicita' e' normale, e in quel caso <see cref="InitializeAsync"/> risponde no. Ogni
    /// cancello costruito su un annuncio si chiude per quei giocatori.
    /// </summary>
    public sealed class H5GamesAdProvider : IAdProvider
    {
        /// <summary>
        /// Quanto si aspetta che lo script di AdSense si presenti. Generoso: la prima visita
        /// carica anche i megabyte della build, e dichiarare la rete morta perche' era in coda
        /// dietro al .wasm toglierebbe la pubblicita' per tutta la sessione.
        /// </summary>
        private const float InitializeTimeoutSeconds = 15f;

        /// <summary>
        /// Il tetto oltre cui si smette di aspettare un annuncio gia' partito. Non e' il tempo
        /// del video - quello lo decide il giocatore, e un rewarded lungo e' normale - ma la
        /// soglia oltre cui si accetta che il callback di chiusura non arrivera' mai. Senza,
        /// una pagina che perde <c>adBreakDone</c> lascerebbe il gioco in pausa per sempre.
        /// </summary>
        private const float ShowTimeoutSeconds = 300f;

        /// <summary>
        /// Per quanto un placement resta dichiarato non pronto dopo un buco. E' il surrogato
        /// del <c>IsReady</c> che l'SDK non offre: si parte ottimisti, e appena la rete dice
        /// che non ha niente si smette di proporre quel bottone per un po'. Un minuto e'
        /// abbastanza da non far ripremere a vuoto e poco da non spegnere il placement per
        /// il resto della sessione.
        /// </summary>
        private const float NoFillBackoffSeconds = 60f;

        // Gli esiti che arrivano dal bridge. Devono restare allineati alla tabella in cima a
        // AccardNdAds.jslib: sono l'unico punto di contatto fra i due file.
        private const int OutcomeWatched = 0;
        private const int OutcomeSkipped = 1;
        private const int OutcomeNoFill = 2;

        private readonly Dictionary<AdPlacement, float> blockedUntil =
            new Dictionary<AdPlacement, float>();

        private bool ready;

        public string ProviderId => "h5games";

        public async Task<bool> InitializeAsync()
        {
            AdsInit();

            float deadline = Time.realtimeSinceStartup + InitializeTimeoutSeconds;
            var completion = new TaskCompletionSource<bool>();
            AdRunner.Instance.Run(WaitForSdk(deadline, completion));

            ready = await completion.Task;
            return ready;
        }

        private IEnumerator WaitForSdk(float deadline, TaskCompletionSource<bool> completion)
        {
            // Uno stato che resta a zero vuol dire che lo script non ha ancora risposto: puo'
            // essere la rete lenta o un blocco pubblicita' che lo ha fatto sparire in silenzio.
            // Da qui i due casi non si distinguono, e la scadenza li tratta allo stesso modo.
            while (AdsState() == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            completion.TrySetResult(AdsState() == 1);
        }

        /// <summary>
        /// Sul web non c'e' niente da preparare: il preload lo fa l'SDK per conto suo e non
        /// esiste un modo di chiedergli un annuncio per un placement preciso. Resta utile
        /// come segnale che il posto e' aperto: azzera il blocco lasciato da un buco
        /// precedente, cosi' chi rientra in taverna dopo un no si ritrova il bottone.
        /// </summary>
        public void Warm(AdPlacement placement) => blockedUntil.Remove(placement);

        public void Cool(AdPlacement placement) => blockedUntil.Remove(placement);

        /// <summary>
        /// Ottimista per forza: l'SDK non espone nessun "ho un annuncio pronto". Si risponde
        /// di si' finche' la rete non ha detto di no da poco. Il prezzo e' che ogni tanto un
        /// bottone premuto non produce niente; l'alternativa - non mostrare mai il bottone -
        /// costerebbe tutte le impression.
        /// </summary>
        public bool IsReady(AdPlacement placement)
        {
            if (!ready)
                return false;
            return !blockedUntil.TryGetValue(placement, out float until)
                   || Time.realtimeSinceStartup >= until;
        }

        /// <summary>
        /// <paramref name="context"/> resta inutilizzato, e non e' una dimenticanza: senza
        /// SSV non c'e' nessuna callback di Google a cui allegarlo. Il server continua a
        /// vedere l'<c>ImpressionId</c> per non pagare due volte, ma non ha modo di sapere
        /// da chi arriva.
        /// </summary>
        public Task<AdResult> ShowAsync(AdPlacement placement, AdRewardContext context)
        {
            bool rewarded = AdPlacements.FormatOf(placement) == AdFormat.Rewarded;
            int id = AdsShow(AdPlacements.KeyOf(placement), rewarded);

            var completion = new TaskCompletionSource<AdResult>();
            AdRunner.Instance.Run(WaitForOutcome(id, placement, completion));
            return completion.Task;
        }

        private IEnumerator WaitForOutcome(
            int id, AdPlacement placement, TaskCompletionSource<AdResult> completion)
        {
            // Tempo reale: durante l'annuncio il gioco e' in pausa con timeScale a zero, e una
            // scadenza legata al tempo di gioco non scatterebbe mai.
            float deadline = Time.realtimeSinceStartup + ShowTimeoutSeconds;
            while (AdsRequestState(id) == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            AdResult result = AdsRequestState(id) == 0
                ? AdResult.Of(AdOutcome.Failed)
                : Interpret(id);

            AdsRelease(id);

            if (result.Outcome == AdOutcome.NoFill)
                blockedUntil[placement] = Time.realtimeSinceStartup + NoFillBackoffSeconds;

            completion.TrySetResult(result);
        }

        private static AdResult Interpret(int id)
        {
            switch (AdsRequestOutcome(id))
            {
                case OutcomeWatched:
                    return new AdResult(AdOutcome.Watched, AdsRequestImpression(id));
                case OutcomeSkipped:
                    return AdResult.Of(AdOutcome.Skipped);
                case OutcomeNoFill:
                    return AdResult.Of(AdOutcome.NoFill);
                default:
                    return AdResult.Of(AdOutcome.Failed);
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void AccardNdAdsInit();
        [DllImport("__Internal")] private static extern int AccardNdAdsState();
        [DllImport("__Internal")] private static extern int AccardNdAdsShow(string name, bool rewarded);
        [DllImport("__Internal")] private static extern int AccardNdAdsRequestState(int id);
        [DllImport("__Internal")] private static extern int AccardNdAdsRequestOutcome(int id);
        [DllImport("__Internal")] private static extern IntPtr AccardNdAdsRequestImpression(int id);
        [DllImport("__Internal")] private static extern void AccardNdAdsRelease(int id);
#endif

        // Il guscio attorno al bridge. Fuori da una build WebGL la .jslib non esiste, e queste
        // rispondono come una rete che non ha mai niente: questo file sta in un assembly che
        // viene compilato per tutte le piattaforme e deve reggere ovunque. In pratica non ci
        // si arriva mai, perche' CreateDefaultProvider sceglie questo provider solo su WebGL.
        //
        // Il ramo sta dentro ogni metodo e non attorno a due blocchi gemelli perche' cosi' la
        // firma resta una sola: il validatore di script di Unity non legge le direttive del
        // preprocessore e leggerebbe le due versioni come metodi duplicati.

        private static void AdsInit()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AccardNdAdsInit();
#endif
        }

        private static int AdsState()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return AccardNdAdsState();
#else
            return 2;
#endif
        }

        private static int AdsShow(string name, bool rewarded)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return AccardNdAdsShow(name, rewarded);
#else
            return 0;
#endif
        }

        private static int AdsRequestState(int id)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return AccardNdAdsRequestState(id);
#else
            return 1;
#endif
        }

        private static int AdsRequestOutcome(int id)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return AccardNdAdsRequestOutcome(id);
#else
            return OutcomeNoFill;
#endif
        }

        private static string AdsRequestImpression(int id)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr pointer = AccardNdAdsRequestImpression(id);
            return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer);
#else
            return string.Empty;
#endif
        }

        private static void AdsRelease(int id)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AccardNdAdsRelease(id);
#endif
        }
    }
}
