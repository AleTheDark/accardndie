using System;
using System.Collections;
using System.Threading.Tasks;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace AccardND.Ads
{
    /// <summary>
    /// Il consenso alla pubblicita' secondo il GDPR, gestito con la User Messaging Platform
    /// di Google.
    ///
    /// Non e' un adempimento da mettere in fondo alla lista: per il traffico europeo Google
    /// pretende che il consenso sia raccolto da una CMP certificata, e finche' non lo e'
    /// **non serve nessun annuncio**. Un gioco italiano senza questo pezzo non vede
    /// pubblicita' e sembra rotto.
    ///
    /// Va eseguito prima di <c>MobileAds.Initialize</c>. La domanda a cui risponde questa
    /// classe e' una sola: possiamo chiedere annunci adesso?
    /// </summary>
    public static class AdConsent
    {
        private const float TimeoutSeconds = 12f;

        /// <summary>
        /// Il giocatore deve poter tornare sulle proprie scelte: quando questo e' vero
        /// l'app e' tenuta a offrire un punto d'accesso alle opzioni privacy, ed e' una
        /// richiesta di Google, non una cortesia.
        /// </summary>
        public static bool IsPrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        /// <summary>
        /// Aggiorna lo stato del consenso e, se dovuto, mostra il modulo. Restituisce se si
        /// possono chiedere annunci.
        ///
        /// Un errore non e' fatale: <c>CanRequestAds</c> puo' essere gia' vero per un consenso
        /// raccolto in una sessione precedente, che UMP tiene da parte. Per questo si guarda
        /// sempre quello alla fine, e non l'esito della chiamata.
        /// </summary>
        public static async Task<bool> RequestAsync()
        {
            try
            {
                await UpdateAsync();
                await ShowFormIfRequiredAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Ads] Consenso non aggiornato: " + exception.Message);
            }

            bool canRequest = ConsentInformation.CanRequestAds();
            Debug.Log($"[Ads] Consenso: stato={ConsentInformation.ConsentStatus}, " +
                      $"annunci richiedibili={canRequest}.");
            return canRequest;
        }

        /// <summary>
        /// Riapre il modulo delle opzioni privacy su richiesta del giocatore. E' quello che
        /// sta dietro al bottone "PRIVACY" nelle opzioni.
        /// </summary>
        public static Task ShowPrivacyOptionsAsync()
        {
            var completion = new TaskCompletionSource<bool>();
            AdRunner.Instance.Run(AfterSeconds(TimeoutSeconds, () => completion.TrySetResult(false)));
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null)
                    Debug.LogWarning("[Ads] Opzioni privacy non mostrate: " + error.Message);
                completion.TrySetResult(true);
            });
            return completion.Task;
        }

        /// <summary>
        /// Cancella il consenso raccolto. Serve solo alle prove: dopo questa chiamata il
        /// modulo torna a comparire al primo avvio, che altrimenti si vedrebbe una volta sola
        /// per installazione.
        /// </summary>
        public static void Reset() => ConsentInformation.Reset();

        private static Task UpdateAsync()
        {
            var completion = new TaskCompletionSource<bool>();
            // Se il callback non arriva - rete assente, servizi Google non disponibili - non
            // si puo' restare fermi qui: il gioco andrebbe avanti senza pubblicita', ma
            // deve andare avanti.
            AdRunner.Instance.Run(AfterSeconds(TimeoutSeconds, () => completion.TrySetResult(false)));

            var parameters = new ConsentRequestParameters
            {
                // Il gioco non e' rivolto ai bambini: dichiararlo diversamente cambierebbe
                // gli annunci ammessi e va deciso con la classificazione del Play Store, non qui.
                TagForUnderAgeOfConsent = false
            };
            ConsentInformation.Update(parameters, error =>
            {
                if (error != null)
                    Debug.LogWarning("[Ads] Aggiornamento consenso fallito: " + error.Message);
                completion.TrySetResult(error == null);
            });
            return completion.Task;
        }

        private static Task ShowFormIfRequiredAsync()
        {
            var completion = new TaskCompletionSource<bool>();
            AdRunner.Instance.Run(AfterSeconds(TimeoutSeconds, () => completion.TrySetResult(false)));
            // Decide UMP se il modulo va mostrato: se il consenso non serve, o e' gia' stato
            // raccolto, questa chiamata non fa vedere niente e torna subito.
            ConsentForm.LoadAndShowConsentFormIfRequired(error =>
            {
                if (error != null)
                    Debug.LogWarning("[Ads] Modulo di consenso non mostrato: " + error.Message);
                completion.TrySetResult(error == null);
            });
            return completion.Task;
        }

        private static IEnumerator AfterSeconds(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }
    }
}
