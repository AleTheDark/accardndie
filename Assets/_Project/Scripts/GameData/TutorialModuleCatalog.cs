using System;
using System.Collections.Generic;

namespace AccardND.GameData
{
    /// <summary>
    /// I moduli del tutorial progressivo, in ordine di percorso. Qui stanno solo identita' e
    /// ordine: le ricompense le decide il catalogo gemello sul server, e il client non deve
    /// nemmeno conoscerle - manda l'id del modulo finito e riceve lo stato aggiornato.
    ///
    /// Gli id devono restare identici a quelli di
    /// Server/AccardND.Server/Progression/TutorialModuleCatalog.cs: finiscono nel database
    /// degli sblocchi e non si rinominano piu'.
    ///
    /// Vedi Docs/tutorial-progressivo-design.md.
    /// </summary>
    public static class TutorialModuleCatalog
    {
        public const string Basics = "m0-basics";
        public const string Warrior = "m1-warrior";
        public const string Mage = "m2-mage";
        public const string Rogue = "m3-rogue";
        public const string ItemsAndBag = "m4-items-bag";
        public const string ChapterRun = "m5-chapter-run";

        /// <summary>
        /// L'ordine del percorso. Le lezioni spiegate vengono prima, la prova pratica per
        /// ultima: <see cref="Basics"/> e' la run guidata completa, ed e' li' che il giocatore
        /// mette in pratica tutto quello che ha imparato, non il primo passo.
        /// </summary>
        private static readonly string[] Ordered =
        {
            Warrior,
            Mage,
            Rogue,
            ItemsAndBag,
            ChapterRun,
            Basics
        };

        public static IReadOnlyList<string> All => Ordered;

        public static int Count => Ordered.Length;

        /// <summary>
        /// Titolo e riga di presentazione del modulo. Sono i testi di ripiego: la versione
        /// localizzata passa da GameTextKeys, e questi restano per quando una chiave manca.
        /// </summary>
        public static (string Title, string Subtitle) DisplayText(string moduleId) => moduleId switch
        {
            Basics => ("PRIMI PASSI", "La prova sul campo"),
            Warrior => ("IL GUERRIERO", "Abilita', tecnica e aura"),
            Mage => ("IL MAGO", "Abilita', tecnica e aura"),
            Rogue => ("IL LADRO", "Abilita', tecnica e aura"),
            ItemsAndBag => ("OGGETTI", "Consumabili e bisaccia"),
            ChapterRun => ("UN CAPITOLO", "Stanze, miniboss e boss"),
            _ => ("TUTORIAL", string.Empty)
        };

        public static string Title(string moduleId) => DisplayText(moduleId).Title;

        public static bool Exists(string moduleId) => IndexOf(moduleId) >= 0;

        /// <summary>Posizione nel percorso, -1 se l'id non e' del catalogo.</summary>
        public static int IndexOf(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return -1;

            for (int index = 0; index < Ordered.Length; index++)
            {
                if (string.Equals(Ordered[index], moduleId, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }

        /// <summary>
        /// Il primo modulo non ancora completato, o null se il percorso e' finito. E' il
        /// numero da cui discende tutto il resto: quale lezione proporre e, da li', quali
        /// cancelli aprire.
        /// </summary>
        public static string NextModule(IReadOnlyList<string> completedModules)
        {
            foreach (string moduleId in Ordered)
            {
                if (!Contains(completedModules, moduleId))
                    return moduleId;
            }
            return null;
        }

        /// <summary>
        /// Quanti moduli del percorso sono chiusi. Si ferma al primo buco invece di contare
        /// le voci: un modulo segnato fuori ordine (admin su un account di prova) non deve
        /// far sembrare aperto un pezzo di gioco che il giocatore non ha ancora visto.
        /// </summary>
        public static int CompletedInOrder(IReadOnlyList<string> completedModules)
        {
            int completed = 0;
            foreach (string moduleId in Ordered)
            {
                if (!Contains(completedModules, moduleId))
                    break;
                completed++;
            }
            return completed;
        }

        public static bool IsCompleted(IReadOnlyList<string> completedModules, string moduleId) =>
            Contains(completedModules, moduleId);

        private static bool Contains(IReadOnlyList<string> modules, string moduleId)
        {
            if (modules == null || string.IsNullOrWhiteSpace(moduleId))
                return false;

            for (int index = 0; index < modules.Count; index++)
            {
                if (string.Equals(modules[index], moduleId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
