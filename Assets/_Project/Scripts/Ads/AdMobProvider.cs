using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoogleMobileAds.Api;

namespace AccardND.Ads
{
    /// <summary>
    /// L'adattatore verso AdMob. E' l'unico file del progetto che nomina l'SDK di Google:
    /// tutto quello che sta sopra parla di <see cref="AdPlacement"/> e non sa da chi arrivi
    /// l'annuncio.
    ///
    /// Due comportamenti che meritano di essere detti prima di leggere il codice:
    ///
    /// - un annuncio caricato si mostra una volta sola. Dopo lo show l'istanza va distrutta e
    ///   ne va caricata un'altra, altrimenti il secondo tentativo fallisce in silenzio;
    /// - ogni attesa ha una scadenza. L'SDK, se qualcosa va storto in rete o nel bridge
    ///   Android, puo' semplicemente non richiamare mai il callback: senza timeout il
    ///   giocatore resterebbe davanti a un bottone premuto che non fa niente per sempre;
    /// - le richieste partono solo da <see cref="Warm"/>, cioe' da un punto del gioco che sta
    ///   per averne bisogno. Prima si caricavano tutti i placement all'avvio: cinque richieste
    ///   a sessione per tre annunci al giorno, un rapporto che AdMob legge come inventario
    ///   chiesto e sprecato.
    /// </summary>
    public sealed class AdMobProvider : IAdProvider
    {
        private const float InitializeTimeoutSeconds = 10f;
        private const float LoadTimeoutSeconds = 8f;

        /// <summary>
        /// Dopo quanto una richiesta senza risposta si considera persa. Non e' il timeout
        /// dell'attesa del giocatore (<see cref="LoadTimeoutSeconds"/>, otto secondi): e' la
        /// soglia oltre la quale si accetta di rifare una richiesta che l'SDK non ha mai
        /// chiuso, ne' con un annuncio ne' con un errore. Senza, un callback perso una volta
        /// spegnerebbe quel placement per tutta la sessione.
        /// </summary>
        private const float RequestGiveUpSeconds = 60f;

        /// <summary>
        /// Quanto si considera valido un annuncio caricato. Sta sotto l'ora scarsa dopo cui
        /// AdMob li fa scadere, con margine: uno show tentato su un annuncio scaduto e' un
        /// fallimento a schermo, mentre dichiararlo vecchio in anticipo costa solo una
        /// ricarica al prossimo Warm.
        /// </summary>
        private const float AdFreshnessSeconds = 50f * 60f;

        private readonly Dictionary<AdPlacement, InterstitialAd> interstitials =
            new Dictionary<AdPlacement, InterstitialAd>();

        private readonly Dictionary<AdPlacement, RewardedAd> rewardeds =
            new Dictionary<AdPlacement, RewardedAd>();

        // Caricamenti in volo, uno per placement. Serve a non far partire due richieste per
        // lo stesso annuncio quando il caricamento chiesto da Warm e' ancora in corso e il
        // giocatore preme il bottone: la seconda aspetta la prima invece di sprecarla.
        private readonly Dictionary<AdPlacement, Task<bool>> loading =
            new Dictionary<AdPlacement, Task<bool>>();

        // Quando e' partita la richiesta all'SDK, per i placement che non hanno ancora avuto
        // risposta. E' cosa diversa da loading: li' l'attesa scade dopo otto secondi perche'
        // il giocatore non puo' restare appeso, ma la richiesta ad AdMob resta in volo, e
        // farne partire un'altra sarebbe pagare due volte lo stesso annuncio.
        private readonly Dictionary<AdPlacement, float> requestedAt =
            new Dictionary<AdPlacement, float>();

        // I placement per cui il gioco ha detto che serve un annuncio e non ha ancora detto
        // il contrario. Decide se, dopo uno show, si ricarica: fuori da qui una ricarica e'
        // una richiesta che non diventera' mai un'impressione.
        private readonly HashSet<AdPlacement> armed = new HashSet<AdPlacement>();

        // Quando e' arrivato l'annuncio che sta in cassa, per sapere quando e' troppo vecchio.
        private readonly Dictionary<AdPlacement, float> loadedAt =
            new Dictionary<AdPlacement, float>();

        public string ProviderId => "admob";

        public async Task<bool> InitializeAsync()
        {
            // Gli eventi dell'SDK nascono sul thread di Android. Senza questa riga le
            // continuazioni delle nostre Task ripartirebbero fuori dal thread principale, e
            // il primo tocco all'API di Unity da li' e' un crash che nei log sembra tutt'altro.
            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            // Il consenso viene prima di tutto, e non e' un formalismo: senza una CMP
            // certificata Google non serve annunci al traffico europeo, quindi inizializzare
            // l'SDK prima di aver chiesto significa chiedere annunci che non arriveranno mai.
            if (!await AdConsent.RequestAsync())
                return false;

            if (AdUnits.TestDeviceIds.Length > 0)
            {
                MobileAds.SetRequestConfiguration(new RequestConfiguration
                {
                    TestDeviceIds = new List<string>(AdUnits.TestDeviceIds)
                });
            }

            var completion = new TaskCompletionSource<bool>();
            AdRunner.Instance.Run(AfterSeconds(InitializeTimeoutSeconds,
                () => completion.TrySetResult(false)));
            MobileAds.Initialize(status => completion.TrySetResult(status != null));

            // Nessun caricamento qui: inizializzare vuol dire avere l'SDK pronto a chiedere,
            // non avere gia' chiesto. Il primo annuncio parte al primo Warm.
            return await completion.Task;
        }

        public void Warm(AdPlacement placement)
        {
            armed.Add(placement);
            // Un annuncio gia' in cassa e ancora fresco non si ricarica: Warm si chiama a ogni
            // apertura della taverna, e senza questo controllo sarebbe di nuovo una richiesta
            // per apertura.
            if (!IsReady(placement))
                _ = EnsureLoadedAsync(placement);
        }

        public void Cool(AdPlacement placement) => armed.Remove(placement);

        public bool IsReady(AdPlacement placement)
        {
            if (IsStale(placement))
                return false;

            if (AdPlacements.FormatOf(placement) == AdFormat.Rewarded)
                return rewardeds.TryGetValue(placement, out RewardedAd rewarded)
                       && rewarded != null && rewarded.CanShowAd();

            return interstitials.TryGetValue(placement, out InterstitialAd interstitial)
                   && interstitial != null && interstitial.CanShowAd();
        }

        /// <summary>
        /// L'annuncio caricato e' troppo vecchio per fidarsene. AdMob li fa scadere dopo circa
        /// un'ora e non lo dice: CanShowAd continua a rispondere di si' e lo show fallisce sul
        /// momento, davanti al giocatore. Meglio dichiararlo non pronto un po' prima e lasciare
        /// che il prossimo Warm ne chieda uno nuovo.
        /// </summary>
        private bool IsStale(AdPlacement placement) =>
            loadedAt.TryGetValue(placement, out float when)
            && UnityEngine.Time.realtimeSinceStartup - when > AdFreshnessSeconds;

        public async Task<AdResult> ShowAsync(AdPlacement placement, AdRewardContext context)
        {
            // Il caricamento chiesto da Warm puo' non essere finito, o l'annuncio puo' essere
            // invecchiato aspettando. In pratica qui non ci si arriva quasi mai, perche'
            // AdService non chiama uno show senza un IsReady positivo: e' la rete di sicurezza
            // per chi usasse il provider direttamente, con la stessa scadenza sull'attesa.
            if (!IsReady(placement) && !await EnsureLoadedAsync(placement))
                return AdResult.Of(AdOutcome.NoFill);

            return AdPlacements.FormatOf(placement) == AdFormat.Rewarded
                ? await ShowRewardedAsync(placement, context)
                : await ShowInterstitialAsync(placement);
        }

        private Task<AdResult> ShowRewardedAsync(AdPlacement placement, AdRewardContext context)
        {
            RewardedAd ad = rewardeds[placement];
            // Fuori dalla cache subito: da qui in poi questa istanza e' bruciata, e lasciarla
            // dove sta farebbe credere a IsReady che ci sia ancora qualcosa da mostrare.
            rewardeds.Remove(placement);

            if (!context.IsEmpty)
            {
                ad.SetServerSideVerificationOptions(new ServerSideVerificationOptions
                {
                    UserId = context.UserId,
                    CustomData = context.CustomData
                });
            }

            // L'id si legge prima dello show: dopo la chiusura l'istanza e' da distruggere.
            string impressionId = ImpressionIdOf(ad.GetResponseInfo());
            var completion = new TaskCompletionSource<AdResult>();
            bool earned = false;

            ad.OnAdFullScreenContentClosed += () =>
            {
                Retire(ad, placement);
                // Chiuso senza aver maturato il premio vuol dire che il giocatore ha
                // interrotto: e' una sua scelta, non un guasto, e non paga.
                completion.TrySetResult(earned
                    ? new AdResult(AdOutcome.Watched, impressionId)
                    : AdResult.Of(AdOutcome.Skipped));
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Retire(ad, placement);
                completion.TrySetResult(AdResult.Of(AdOutcome.Failed));
            };

            ad.Show(reward => earned = true);
            return completion.Task;
        }

        private Task<AdResult> ShowInterstitialAsync(AdPlacement placement)
        {
            InterstitialAd ad = interstitials[placement];
            interstitials.Remove(placement);

            string impressionId = ImpressionIdOf(ad.GetResponseInfo());
            var completion = new TaskCompletionSource<AdResult>();

            // Un interstitial non ha un premio da maturare: mostrato e chiuso e' tutto quello
            // che puo' succedere di buono.
            ad.OnAdFullScreenContentClosed += () =>
            {
                Retire(ad, placement);
                completion.TrySetResult(new AdResult(AdOutcome.Watched, impressionId));
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Retire(ad, placement);
                completion.TrySetResult(AdResult.Of(AdOutcome.Failed));
            };

            ad.Show();
            return completion.Task;
        }

        private void Retire(RewardedAd ad, AdPlacement placement)
        {
            ad.Destroy();
            loadedAt.Remove(placement);
            Restock(placement);
        }

        private void Retire(InterstitialAd ad, AdPlacement placement)
        {
            ad.Destroy();
            loadedAt.Remove(placement);
            Restock(placement);
        }

        /// <summary>
        /// Rimette una scorta dopo uno show, ma solo dove ne servira' davvero un'altra: il
        /// giocatore e' ancora nel posto che l'ha chiesta (<see cref="armed"/>) e quel posto
        /// puo' produrre piu' di un annuncio (<see cref="AdPlacements.MayRepeat"/>). Il
        /// triplicatore di fine run non rientra in nessuno dei due casi: ricaricarlo appena
        /// dopo averlo mostrato e' la richiesta piu' sprecata di tutte, perche' la run e'
        /// finita proprio in quel momento.
        /// </summary>
        private void Restock(AdPlacement placement)
        {
            if (armed.Contains(placement) && AdPlacements.MayRepeat(placement))
                _ = EnsureLoadedAsync(placement);
        }

        private Task<bool> EnsureLoadedAsync(AdPlacement placement)
        {
            if (loading.TryGetValue(placement, out Task<bool> inFlight) && !inFlight.IsCompleted)
                return inFlight;

            // Richiesta gia' partita e mai chiusa dall'SDK: l'attesa e' scaduta per il
            // giocatore, la richiesta no. Rifarla adesso significherebbe due annunci chiesti
            // e uno solo mostrabile.
            if (requestedAt.TryGetValue(placement, out float since)
                && UnityEngine.Time.realtimeSinceStartup - since < RequestGiveUpSeconds)
                return Task.FromResult(false);

            string adUnitId = AdUnits.For(placement);
            if (string.IsNullOrEmpty(adUnitId))
                return Task.FromResult(false);

            var completion = new TaskCompletionSource<bool>();
            loading[placement] = completion.Task;
            requestedAt[placement] = UnityEngine.Time.realtimeSinceStartup;
            AdRunner.Instance.Run(AfterSeconds(LoadTimeoutSeconds, () => completion.TrySetResult(false)));

            var request = new AdRequest();
            if (AdPlacements.FormatOf(placement) == AdFormat.Rewarded)
            {
                RewardedAd.Load(adUnitId, request, (ad, error) =>
                {
                    requestedAt.Remove(placement);
                    if (error != null || ad == null)
                    {
                        completion.TrySetResult(false);
                        return;
                    }
                    rewardeds[placement] = ad;
                    loadedAt[placement] = UnityEngine.Time.realtimeSinceStartup;
                    completion.TrySetResult(true);
                });
            }
            else
            {
                InterstitialAd.Load(adUnitId, request, (ad, error) =>
                {
                    requestedAt.Remove(placement);
                    if (error != null || ad == null)
                    {
                        completion.TrySetResult(false);
                        return;
                    }
                    interstitials[placement] = ad;
                    loadedAt[placement] = UnityEngine.Time.realtimeSinceStartup;
                    completion.TrySetResult(true);
                });
            }

            return completion.Task;
        }

        /// <summary>
        /// L'identificativo con cui il server riconosce questa impression e non la paga due
        /// volte. Se AdMob non lo fornisce si ripiega su un id nostro: la richiesta resta
        /// idempotente, si perde solo la corrispondenza con i report della rete.
        /// </summary>
        private static string ImpressionIdOf(ResponseInfo info)
        {
            string responseId = info?.GetResponseId();
            return string.IsNullOrEmpty(responseId)
                ? "admob-" + Guid.NewGuid().ToString("N")
                : responseId;
        }

        private static IEnumerator AfterSeconds(float seconds, Action action)
        {
            // Tempo reale: durante un annuncio la scala del tempo e' a zero, e una scadenza
            // legata al tempo di gioco non scatterebbe mai.
            yield return new UnityEngine.WaitForSecondsRealtime(seconds);
            action();
        }
    }
}
