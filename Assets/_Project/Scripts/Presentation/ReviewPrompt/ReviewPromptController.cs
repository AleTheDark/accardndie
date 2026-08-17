using System;
using UnityEngine;

namespace AccardND.Presentation.ReviewPrompt
{
    /// <summary>
    /// Punto d'ingresso unico del popup di recensione: decide se mostrarlo, lo costruisce
    /// e ne registra l'esito.
    ///
    /// Il gioco lo usa da un posto solo — la chiusura del popup di fine campagna, in
    /// BattleBoardController.CampaignProgress.cs — e non deve sapere niente ne' delle
    /// stelle ne' del Play Store.
    /// </summary>
    public static class ReviewPromptController
    {
        /// <summary>
        /// La modalita' in produzione: <see cref="ReviewPromptMode.DirectAsk"/>, scelta
        /// il 2026-08-16.
        ///
        /// Il popup chiede la recensione e chi accetta va allo store, qualunque cosa
        /// pensi del gioco. <see cref="ReviewPromptMode.StarGate"/> resta implementata e
        /// provabile dal banco di debug, ma <b>non va messa in produzione</b>: filtrare
        /// per voto e' review gating e Google Play lo vieta.
        /// </summary>
        public static ReviewPromptMode Mode { get; set; } = ReviewPromptMode.DirectAsk;

        /// <summary>
        /// Forza il ramo Android quando si prova dall'editor. La scena di debug lo
        /// accende; in una build vera resta false e comanda la piattaforma reale.
        /// </summary>
        public static bool SimulateAndroidForDebug { get; set; }

        /// <summary>
        /// Ordine di disegno del popup. In gioco vale il valore di produzione; la scena
        /// di debug lo alza, perche' li' il banco di prova disegna sopra il gioco (che si
        /// auto-istanzia in ogni scena) e il popup deve restare sopra a entrambi.
        /// </summary>
        public static int SortingOrder { get; set; } = ReviewPromptView.DefaultSortingOrder;

        private static ReviewPromptView active;

        public static bool IsShowing => active != null;

        /// <summary>
        /// Prova a mostrare il popup dopo una run. Restituisce true se il popup e'
        /// comparso: in quel caso chi chiama deve aspettare <paramref name="onClosed"/>
        /// prima di proseguire, altrimenti il ritorno al menu passa sotto al popup.
        /// </summary>
        /// <param name="canvasRoot">
        /// La radice del Canvas, non il rect della Safe Area: i due non coincidono in
        /// battaglia e un modale centrato nel secondo finisce fuori asse.
        /// </param>
        /// <param name="chapterId">Il capitolo della run appena finita.</param>
        /// <param name="runCompleted">True se la run e' stata vinta.</param>
        /// <param name="onClosed">Chiamata alla chiusura del popup, sempre.</param>
        public static bool TryShow(
            Transform canvasRoot,
            string chapterId,
            bool runCompleted,
            Action onClosed = null)
        {
            if (active != null)
                return false;

            var request = new ReviewPromptPolicy.Request(
                chapterId,
                runCompleted,
                IsAndroid,
                ReviewPromptState.AlreadyPrompted,
                ReviewPromptState.AlreadyRated);

            if (!ReviewPromptPolicy.ShouldPrompt(request))
                return false;

            if (canvasRoot == null)
            {
                Debug.LogWarning("[Recensione] Nessun canvas a cui appendere il popup: salto.");
                return false;
            }

            Show(canvasRoot, onClosed);
            return true;
        }

        /// <summary>
        /// Mostra il popup saltando ogni controllo. Serve alla scena di debug: nel gioco
        /// si passa sempre da <see cref="TryShow"/>.
        /// </summary>
        public static void Show(Transform canvasRoot, Action onClosed = null)
        {
            if (active != null)
                return;

            // Si segna prima di mostrare, non dopo: se il giocatore chiude l'app con il
            // popup aperto, la domanda e' comunque gia' stata fatta e non deve tornare.
            ReviewPromptState.AlreadyPrompted = true;

            var view = new ReviewPromptView(canvasRoot, Mode, SortingOrder);
            active = view;
            view.Completed += (stars, openedStore) =>
            {
                ReviewPromptState.LastStars = stars;
                if (openedStore)
                    ReviewPromptState.AlreadyRated = true;

                Debug.Log($"[Recensione] voto {stars}/5, store {(openedStore ? "aperto" : "non aperto")}.");

                view.Destroy();
                if (ReferenceEquals(active, view))
                    active = null;

                onClosed?.Invoke();
            };
        }

        /// <summary>Chiude il popup senza registrare nulla (cambio scena, uscita).</summary>
        public static void Dismiss()
        {
            active?.Destroy();
            active = null;
        }

        private static bool IsAndroid =>
            SimulateAndroidForDebug || Application.platform == RuntimePlatform.Android;
    }
}
