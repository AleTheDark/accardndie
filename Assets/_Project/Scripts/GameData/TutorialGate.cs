using System.Collections.Generic;

namespace AccardND.GameData
{
    /// <summary>Le zone del gioco che l'onboarding puo' aprire, chiudere o indicare.</summary>
    public enum TutorialSurface
    {
        HubCampaign,
        HubSanctuary,
        HubShop,
        HubTavern,
        HubLibrary,
        HubProfile,
        HubLeaderboard,
        HubArena,

        /// <summary>Il tasto AVVENTURA dentro Campagna.</summary>
        CampaignAdventure,

        /// <summary>Il tasto HARDCORE dentro Campagna.</summary>
        CampaignHardcore,

        /// <summary>La riga TUTORIAL nella lista dei capitoli: la porta dei moduli.</summary>
        AdventureTutorialRow,

        /// <summary>Le righe dei capitoli veri.</summary>
        AdventureChapters,

        SanctuaryAltarClasses,
        SanctuaryAltarTechniques,
        SanctuaryAltarRelics,

        ShopOffers,
        ShopCatalog,

        /// <summary>Sezione acquisti reali. Chiusa per tutto l'onboarding.</summary>
        ShopPremium
    }

    public enum TutorialGateState
    {
        /// <summary>Si usa normalmente.</summary>
        Open,

        /// <summary>Visibile ma non cliccabile: il percorso non ci e' ancora arrivato.</summary>
        Closed,

        /// <summary>L'unica cosa da toccare adesso.</summary>
        Highlighted
    }

    /// <summary>
    /// Lo stato del percorso ridotto a quello che serve per decidere i cancelli. Tre delle
    /// cinque voci si leggono dalla progressione autoritativa; solo i due tour informativi
    /// (Santuario e Negozio) hanno bisogno di un flag locale, perche' "l'ho gia' visto" non
    /// e' progressione e non vale la pena farlo viaggiare fino al server.
    /// </summary>
    public readonly struct TutorialFlowState
    {
        public TutorialFlowState(
            int completedModules,
            bool ownsMage,
            bool ownsRogue,
            bool sanctuaryTourSeen,
            bool shopTourSeen)
        {
            CompletedModules = completedModules;
            OwnsMage = ownsMage;
            OwnsRogue = ownsRogue;
            SanctuaryTourSeen = sanctuaryTourSeen;
            ShopTourSeen = shopTourSeen;
        }

        /// <summary>Moduli chiusi in fila dall'inizio del percorso.</summary>
        public int CompletedModules { get; }

        public bool OwnsMage { get; }
        public bool OwnsRogue { get; }
        public bool SanctuaryTourSeen { get; }
        public bool ShopTourSeen { get; }

        /// <summary>Il percorso e' finito: da qui in poi i cancelli non decidono piu' niente.</summary>
        public bool IsComplete => CompletedModules >= TutorialModuleCatalog.Count;

        public static TutorialFlowState Read(
            IReadOnlyList<string> completedModules,
            bool ownsMage,
            bool ownsRogue,
            bool sanctuaryTourSeen,
            bool shopTourSeen) => new TutorialFlowState(
                TutorialModuleCatalog.CompletedInOrder(completedModules),
                ownsMage,
                ownsRogue,
                sanctuaryTourSeen,
                shopTourSeen);
    }

    /// <summary>
    /// Chi apre cosa durante l'onboarding. E' l'unico posto che lo decide: la tabella dei
    /// cancelli sta in Docs/tutorial-progressivo-design.md §5, e averla sparsa su otto
    /// schermate vorrebbe dire non poterla piu' verificare.
    ///
    /// Regola generale: durante il percorso e' toccabile solo cio' che il modulo corrente
    /// sta insegnando. Fuori dal percorso questa classe risponde sempre <see
    /// cref="TutorialGateState.Open"/> e il gioco si comporta come se non esistesse.
    ///
    /// Fuori dalla tabella per scelta: Impostazioni e tasto Home. Le prime contengono lingua,
    /// logout e cancellazione account; il secondo e' il recupero della navigazione se il
    /// tutorial si incastra. Chiuderli sarebbe un problema, non un cancello.
    /// </summary>
    public static class TutorialGate
    {
        public static TutorialGateState Evaluate(TutorialSurface surface, TutorialFlowState flow)
        {
            if (flow.IsComplete)
                return TutorialGateState.Open;

            return surface switch
            {
                TutorialSurface.HubCampaign => HubCampaign(flow),
                TutorialSurface.HubSanctuary => HubSanctuary(flow),
                TutorialSurface.HubShop => HubShop(flow),

                // Il resto dell'hub si apre tutto insieme alla fine del percorso.
                TutorialSurface.HubTavern
                    or TutorialSurface.HubLibrary
                    or TutorialSurface.HubProfile
                    or TutorialSurface.HubLeaderboard
                    or TutorialSurface.HubArena => TutorialGateState.Closed,

                // Campagna esiste durante l'onboarding per una ragione sola: arrivare al
                // tutorial. L'Hardcore e' un acquisto, e non si compra niente qui.
                TutorialSurface.CampaignAdventure => TutorialGateState.Highlighted,
                TutorialSurface.CampaignHardcore => TutorialGateState.Closed,

                TutorialSurface.AdventureTutorialRow => TutorialGateState.Highlighted,
                TutorialSurface.AdventureChapters => TutorialGateState.Closed,

                // Dentro il Santuario si guarda; l'unica cosa da toccare e' l'altare delle
                // classi, e solo quando c'e' un acquisto guidato in sospeso.
                TutorialSurface.SanctuaryAltarClasses => PendingPurchase(flow)
                    ? TutorialGateState.Highlighted
                    : TutorialGateState.Closed,
                TutorialSurface.SanctuaryAltarTechniques
                    or TutorialSurface.SanctuaryAltarRelics => TutorialGateState.Closed,

                TutorialSurface.ShopOffers
                    or TutorialSurface.ShopCatalog
                    or TutorialSurface.ShopPremium => TutorialGateState.Closed,

                _ => TutorialGateState.Closed
            };
        }

        /// <summary>
        /// La classe che il percorso sta facendo comprare adesso, o null se non c'e' nessun
        /// acquisto guidato in sospeso. Non serve uno stato apposta: un acquisto e' in
        /// sospeso finche' la classe che il modulo appena finito ha pagato non risulta
        /// posseduta, e il possesso e' progressione autoritativa.
        /// </summary>
        public static string PendingPurchaseClassId(TutorialFlowState flow)
        {
            if (flow.IsComplete)
                return null;
            // Finito il modulo del Guerriero (il primo) si compra il Mago; finito quello del
            // Mago si compra il Ladro. I numeri sono posizioni nel percorso: se l'ordine dei
            // moduli cambia, cambiano anche loro.
            if (flow.CompletedModules == 1 && !flow.OwnsMage)
                return "mage";
            if (flow.CompletedModules == 2 && !flow.OwnsRogue)
                return "rogue";
            return null;
        }

        /// <summary>
        /// Il tour informativo dovuto adesso, o null. Sono i due momenti in cui una zona si
        /// apre e va spiegata prima di lasciarla al giocatore.
        /// </summary>
        public static TutorialSurface? PendingTourSurface(TutorialFlowState flow)
        {
            if (flow.IsComplete)
                return null;
            // Il Santuario si apre col primo modulo, il Negozio col terzo (il Ladro). Il tour
            // del Santuario e l'acquisto del Mago cadono nello stesso momento: prima si
            // spiega la zona, poi si compra, e le due cose si susseguono da sole perche' il
            // tour smette di essere dovuto appena e' stato visto.
            // Se il Mago e' gia' posseduto, l'acquisto guidato e' concluso: al login non
            // si torna indietro nel Santuario solo perche' il flag locale del tour manca.
            if (flow.CompletedModules == 1 && !flow.OwnsMage && !flow.SanctuaryTourSeen)
                return TutorialSurface.HubSanctuary;
            if (flow.CompletedModules == 3 && !flow.ShopTourSeen)
                return TutorialSurface.HubShop;
            return null;
        }

        private static bool PendingPurchase(TutorialFlowState flow) =>
            PendingPurchaseClassId(flow) != null;

        /// <summary>
        /// Campagna e' la porta del tutorial: si accende quando non c'e' nient'altro di piu'
        /// urgente da fare, cioe' quando nessun tour e nessun acquisto guidato aspettano.
        /// </summary>
        private static TutorialGateState HubCampaign(TutorialFlowState flow) =>
            PendingTourSurface(flow) == null && !PendingPurchase(flow)
                ? TutorialGateState.Highlighted
                : TutorialGateState.Closed;

        private static TutorialGateState HubSanctuary(TutorialFlowState flow)
        {
            // Chiuso finche' il primo modulo non lo consegna.
            if (flow.CompletedModules < 1)
                return TutorialGateState.Closed;

            // Appena aperto va visitato; poi torna a indicarlo solo quando c'e' una classe
            // da comprare. Fra un momento e l'altro resta aperto: e' gia' suo.
            if (PendingTourSurface(flow) == TutorialSurface.HubSanctuary || PendingPurchase(flow))
                return TutorialGateState.Highlighted;
            return TutorialGateState.Open;
        }

        private static TutorialGateState HubShop(TutorialFlowState flow)
        {
            // Lo consegna il modulo del Ladro, che e' il terzo.
            if (flow.CompletedModules < 3)
                return TutorialGateState.Closed;
            return PendingTourSurface(flow) == TutorialSurface.HubShop
                ? TutorialGateState.Highlighted
                : TutorialGateState.Open;
        }
    }
}
