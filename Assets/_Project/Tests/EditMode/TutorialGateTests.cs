using System.Collections.Generic;
using AccardND.GameData;
using NUnit.Framework;

namespace AccardND.GameCore.Tests
{
    /// <summary>
    /// La tabella dei cancelli di Docs/tutorial-progressivo-design.md §5, riscritta come
    /// test. E' l'unico modo di tenerla verificabile: a mano vorrebbe dire rigiocare
    /// l'onboarding per intero a ogni modifica, sei volte, su otto schermate.
    /// </summary>
    public sealed class TutorialGateTests
    {
        [Test]
        public void At_the_very_first_launch_only_the_campaign_is_reachable()
        {
            TutorialFlowState flow = Flow(0);

            Assert.That(State(TutorialSurface.HubCampaign, flow), Is.EqualTo(TutorialGateState.Highlighted));
            Assert.That(State(TutorialSurface.HubSanctuary, flow), Is.EqualTo(TutorialGateState.Closed));
            Assert.That(State(TutorialSurface.HubShop, flow), Is.EqualTo(TutorialGateState.Closed));
            Assert.That(State(TutorialSurface.HubTavern, flow), Is.EqualTo(TutorialGateState.Closed));
            Assert.That(State(TutorialSurface.HubLibrary, flow), Is.EqualTo(TutorialGateState.Closed));
            Assert.That(State(TutorialSurface.HubProfile, flow), Is.EqualTo(TutorialGateState.Closed));
            Assert.That(State(TutorialSurface.HubLeaderboard, flow), Is.EqualTo(TutorialGateState.Closed));
            Assert.That(State(TutorialSurface.HubArena, flow), Is.EqualTo(TutorialGateState.Closed));
        }

        [Test]
        public void The_warrior_module_opens_the_sanctuary_and_points_at_it()
        {
            TutorialFlowState flow = Flow(1);

            Assert.That(State(TutorialSurface.HubSanctuary, flow), Is.EqualTo(TutorialGateState.Highlighted));
            // Finche' il tour del Santuario non e' visto, tornare in campagna non ha senso.
            Assert.That(State(TutorialSurface.HubCampaign, flow), Is.EqualTo(TutorialGateState.Closed));
        }

        /// <summary>
        /// Tour e acquisto guidato cadono nello stesso punto del percorso: prima si spiega la
        /// zona, poi si compra. Il passaggio dall'uno all'altro deve avvenire da solo, senza
        /// che il giocatore torni all'hub in mezzo.
        /// </summary>
        [Test]
        public void The_sanctuary_tour_hands_over_to_the_guided_purchase()
        {
            TutorialFlowState beforeTour = Flow(1);

            Assert.That(TutorialGate.PendingTourSurface(beforeTour),
                Is.EqualTo(TutorialSurface.HubSanctuary));

            TutorialFlowState afterTour = Flow(1, sanctuaryTourSeen: true);

            Assert.That(TutorialGate.PendingTourSurface(afterTour), Is.Null);
            Assert.That(TutorialGate.PendingPurchaseClassId(afterTour), Is.EqualTo("mage"));
            // Il Santuario resta indicato: il tour e' finito, ma c'e' ancora da comprare.
            Assert.That(State(TutorialSurface.HubSanctuary, afterTour), Is.EqualTo(TutorialGateState.Highlighted));
        }

        /// <summary>
        /// Il cuore del ritmo "impara, guadagna, compra": finito il modulo del Guerriero il
        /// percorso non prosegue finche' il Mago non e' stato comprato davvero.
        /// </summary>
        [Test]
        public void A_pending_guided_purchase_holds_the_path_at_the_sanctuary()
        {
            TutorialFlowState waiting = Flow(1, sanctuaryTourSeen: true);

            Assert.That(TutorialGate.PendingPurchaseClassId(waiting), Is.EqualTo("mage"));
            Assert.That(State(TutorialSurface.HubSanctuary, waiting), Is.EqualTo(TutorialGateState.Highlighted));
            Assert.That(State(TutorialSurface.SanctuaryAltarClasses, waiting), Is.EqualTo(TutorialGateState.Highlighted));
            Assert.That(State(TutorialSurface.HubCampaign, waiting), Is.EqualTo(TutorialGateState.Closed));

            TutorialFlowState bought = Flow(1, ownsMage: true, sanctuaryTourSeen: true);

            Assert.That(TutorialGate.PendingPurchaseClassId(bought), Is.Null);
            Assert.That(State(TutorialSurface.HubCampaign, bought), Is.EqualTo(TutorialGateState.Highlighted));
            // Comprato il Mago l'altare torna semplicemente disponibile, non indicato.
            Assert.That(State(TutorialSurface.SanctuaryAltarClasses, bought), Is.EqualTo(TutorialGateState.Closed));
        }

        [Test]
        public void The_mage_module_holds_the_path_until_the_rogue_is_bought()
        {
            TutorialFlowState waiting = Flow(2, ownsMage: true, sanctuaryTourSeen: true);

            Assert.That(TutorialGate.PendingPurchaseClassId(waiting), Is.EqualTo("rogue"));
            Assert.That(State(TutorialSurface.HubSanctuary, waiting), Is.EqualTo(TutorialGateState.Highlighted));

            TutorialFlowState bought = Flow(2, ownsMage: true, ownsRogue: true, sanctuaryTourSeen: true);

            Assert.That(State(TutorialSurface.HubCampaign, bought), Is.EqualTo(TutorialGateState.Highlighted));
        }

        [Test]
        public void The_rogue_module_opens_the_shop_and_asks_for_its_tour()
        {
            TutorialFlowState flow = Flow(3, ownsMage: true, ownsRogue: true, sanctuaryTourSeen: true);

            Assert.That(State(TutorialSurface.HubShop, flow), Is.EqualTo(TutorialGateState.Highlighted));
            Assert.That(State(TutorialSurface.HubCampaign, flow), Is.EqualTo(TutorialGateState.Closed));

            TutorialFlowState toured = Flow(
                3, ownsMage: true, ownsRogue: true, sanctuaryTourSeen: true, shopTourSeen: true);

            Assert.That(State(TutorialSurface.HubShop, toured), Is.EqualTo(TutorialGateState.Open));
            Assert.That(State(TutorialSurface.HubCampaign, toured), Is.EqualTo(TutorialGateState.Highlighted));
        }

        [Test]
        public void The_tavern_stays_closed_for_the_whole_path()
        {
            for (int completed = 0; completed < TutorialModuleCatalog.Count; completed++)
            {
                TutorialFlowState flow = Flow(
                    completed, ownsMage: true, ownsRogue: true,
                    sanctuaryTourSeen: true, shopTourSeen: true);

                Assert.That(State(TutorialSurface.HubTavern, flow), Is.EqualTo(TutorialGateState.Closed),
                    $"La taverna deve restare chiusa con {completed} moduli fatti.");
            }
        }

        [Test]
        public void The_last_module_opens_everything()
        {
            TutorialFlowState flow = Flow(TutorialModuleCatalog.Count);

            foreach (TutorialSurface surface in System.Enum.GetValues(typeof(TutorialSurface)))
            {
                Assert.That(State(surface, flow), Is.EqualTo(TutorialGateState.Open),
                    $"A percorso finito {surface} deve essere aperta.");
            }
            Assert.That(TutorialGate.PendingTourSurface(flow), Is.Null);
            Assert.That(TutorialGate.PendingPurchaseClassId(flow), Is.Null);
        }

        /// <summary>
        /// Gli acquisti reali non si toccano durante l'onboarding: un tour che finisce per
        /// sbaglio su un acquisto vero e' l'unico errore di questo sistema che costa soldi.
        /// </summary>
        [Test]
        public void The_premium_shop_is_never_reachable_during_the_path()
        {
            for (int completed = 0; completed < TutorialModuleCatalog.Count; completed++)
            {
                TutorialFlowState flow = Flow(
                    completed, ownsMage: true, ownsRogue: true,
                    sanctuaryTourSeen: true, shopTourSeen: true);

                Assert.That(State(TutorialSurface.ShopPremium, flow), Is.EqualTo(TutorialGateState.Closed));
            }
        }

        /// <summary>
        /// I capitoli restano chiusi finche' il percorso non finisce, anche se l'account li
        /// possiede gia' (concessi a mano dall'admin su un account di prova). Il cancello
        /// vince sul possesso: e' una regola, non un caso non gestito.
        /// </summary>
        [Test]
        public void Chapters_stay_closed_until_the_path_ends()
        {
            TutorialFlowState almost = Flow(
                TutorialModuleCatalog.Count - 1, ownsMage: true, ownsRogue: true,
                sanctuaryTourSeen: true, shopTourSeen: true);

            Assert.That(State(TutorialSurface.AdventureChapters, almost), Is.EqualTo(TutorialGateState.Closed));
            Assert.That(State(TutorialSurface.AdventureTutorialRow, almost), Is.EqualTo(TutorialGateState.Highlighted));
        }

        /// <summary>
        /// Un modulo segnato fuori ordine (admin su un account di prova) non deve aprire i
        /// cancelli di quelli prima: il conteggio si ferma al primo buco.
        /// </summary>
        [Test]
        public void A_module_marked_out_of_order_opens_nothing()
        {
            var completed = new List<string> { TutorialModuleCatalog.Basics };
            TutorialFlowState flow = TutorialFlowState.Read(completed, false, false, false, false);

            Assert.That(flow.CompletedModules, Is.Zero);
            Assert.That(flow.IsComplete, Is.False);
            Assert.That(State(TutorialSurface.HubSanctuary, flow), Is.EqualTo(TutorialGateState.Closed));
        }

        private static TutorialGateState State(TutorialSurface surface, TutorialFlowState flow) =>
            TutorialGate.Evaluate(surface, flow);

        private static TutorialFlowState Flow(
            int completedModules,
            bool ownsMage = false,
            bool ownsRogue = false,
            bool sanctuaryTourSeen = false,
            bool shopTourSeen = false) =>
            new TutorialFlowState(completedModules, ownsMage, ownsRogue, sanctuaryTourSeen, shopTourSeen);
    }
}
