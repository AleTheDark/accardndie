using System;
using UnityEngine;

namespace AccardND.TourKit
{
    /// <summary>
    /// Come si passa alla tappa dopo.
    /// </summary>
    public enum GuidedTourAdvance
    {
        /// <summary>Il giocatore preme CONTINUA.</summary>
        Continue,

        /// <summary>Il giocatore tocca il bersaglio illuminato.</summary>
        TapTarget,

        /// <summary>
        /// Aspetta che succeda una cosa nel gioco (una classe comprata, un oggetto usato).
        /// La tappa non ha un pulsante: si sblocca da sola quando l'evento arriva.
        /// </summary>
        GameEvent
    }

    /// <summary>
    /// Una tappa del tour. Il bersaglio e' una funzione e non un RectTransform gia' risolto:
    /// molte schermate costruiscono i loro pulsanti solo quando si aprono, quindi al momento
    /// in cui il tour viene scritto quel rect non esiste ancora.
    /// </summary>
    public sealed class GuidedTourStep
    {
        public string Title;
        public string Body;
        public Func<RectTransform> Target;
        public GuidedTourAdvance Advance = GuidedTourAdvance.Continue;
        public bool CenterPanel;
        public bool BottomPanel;
        public bool ClassicRectSpotlight;
        public bool ShowSpotlight = true;
        public bool ShowPanel = true;
        // I tour spesso vengono mostrati sopra griglie illustrate (Tutorial, Negozio,
        // Santuario): con il pannello troppo trasparente titoli e copertine sottostanti
        // interferiscono con il corpo del testo.
        public float PanelOpacity = 0.985f;

        /// <summary>Per <see cref="GuidedTourAdvance.GameEvent"/>: l'id dell'evento atteso.</summary>
        public string AwaitedEvent;

        /// <summary>Eseguita quando la tappa compare (per aprire un pannello, per esempio).</summary>
        public Action OnEnter;
    }

    /// <summary>
    /// La pelle del tour: pannello, spotlight, testo, blocco dell'input.
    /// Il motore non sa come sono fatti, li comanda soltanto — ed e' questo che rende
    /// <see cref="GuidedTourRunner"/> riutilizzabile in un altro gioco con un'altra grafica.
    /// </summary>
    public interface IGuidedTourView
    {
        /// <summary>Chiamata una volta all'avvio del tour, per costruire la vista se manca.</summary>
        void EnsureCreated();

        /// <summary>
        /// Mostra <paramref name="step"/>. Il bersaglio arriva gia' risolto, perche' il
        /// momento giusto per risolverlo lo conosce il motore, non la vista.
        /// </summary>
        void ShowStep(GuidedTourStep step, RectTransform target, int stepNumber, int stepCount);

        /// <summary>Chiude tutto: tour finito o interrotto.</summary>
        void Hide();
    }
}
