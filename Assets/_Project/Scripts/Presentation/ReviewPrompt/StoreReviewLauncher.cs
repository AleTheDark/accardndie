using UnityEngine;

namespace AccardND.Presentation.ReviewPrompt
{
    /// <summary>
    /// Apre la scheda del gioco sul Play Store.
    ///
    /// Non esiste un link che porti direttamente alla finestra "scrivi una recensione":
    /// l'unico modo di far comparire quella finestra dentro il gioco e' la In-App Review
    /// API di Google, che richiede il pacchetto <c>com.google.play.review</c> (non
    /// presente nel manifest di questo progetto). Finche' non c'e', si apre la scheda
    /// dello store e il giocatore scorre fino alle stelle.
    /// </summary>
    public static class StoreReviewLauncher
    {
        /// <summary>
        /// Il package name vero della build. Si legge da <see cref="Application.identifier"/>
        /// invece di scriverlo a mano: e' gia' scritto nei ProjectSettings e una costante
        /// duplicata qui sarebbe l'ennesimo posto da ricordare quando cambia.
        /// </summary>
        private static string PackageName =>
            string.IsNullOrWhiteSpace(Application.identifier)
                ? "com.apesolution.accardndie"
                : Application.identifier;

        /// <summary>
        /// Schema nativo: apre l'app Play Store se e' installata, senza passare dal
        /// browser. E' quello che si vuole su un telefono vero.
        /// </summary>
        public static string MarketUrl => $"market://details?id={PackageName}";

        /// <summary>
        /// Ripiego web, per i dispositivi Android senza i servizi Google (e per poter
        /// aprire il link anche dall'editor durante le prove).
        /// </summary>
        public static string WebUrl => $"https://play.google.com/store/apps/details?id={PackageName}";

        /// <summary>
        /// Manda il giocatore allo store. Su Android prova prima lo schema nativo; se
        /// l'intent non trova nessuno che lo gestisca Unity non lancia un'eccezione ma
        /// non succede niente, quindi il ripiego web parte comunque.
        /// </summary>
        public static void OpenStorePage()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                Application.OpenURL(MarketUrl);
                return;
            }

            // Editor, WebGL, standalone: lo schema market:// non significa niente.
            Application.OpenURL(WebUrl);
        }
    }
}
