using System;
using System.Collections.Generic;

namespace AccardND.GameData
{
    /// <summary>
    /// Un capitolo della campagna come lo disegna il client.
    /// </summary>
    public sealed class AdventureChapter
    {
        public AdventureChapter(
            int number,
            string id,
            string title,
            string scenarioId,
            string scenarioLabel,
            string bossId,
            string bossName,
            string rewardClassId,
            int honeyCost,
            bool playable)
        {
            Number = number;
            Id = id;
            Title = title;
            ScenarioId = scenarioId;
            ScenarioLabel = scenarioLabel;
            BossId = bossId;
            BossName = bossName;
            RewardClassId = rewardClassId;
            HoneyCost = honeyCost;
            Playable = playable;
        }

        public int Number { get; }
        public string Id { get; }
        public string Title { get; }
        public string ScenarioId { get; }
        public string ScenarioLabel { get; }
        public string BossId { get; }
        public string BossName { get; }

        /// <summary>Classe consegnata completando il capitolo. Null sull'ultimo, che da' un cosmetico.</summary>
        public string RewardClassId { get; }

        /// <summary>Prezzo al Santuario. Zero sul primo, che arriva dal tutorial.</summary>
        public int HoneyCost { get; }

        /// <summary>Se il boss del capitolo e' implementato, quindi se il capitolo si puo' giocare.</summary>
        public bool Playable { get; }
    }

    /// <summary>
    /// L'ordine dei capitoli lato client: serve a disegnare la schermata Avventura e a sapere
    /// quale scenario e quale boss caricare.
    ///
    /// E' il riflesso di <c>ChapterCatalog</c> sul server, che resta l'autorita': qui non si
    /// decide niente. Il client non concede capitoli ne' classi - li chiede e li rispecchia -
    /// quindi una divergenza si vede come una schermata sbagliata, non come una progressione
    /// sbagliata. Restano comunque due tabelle da tenere allineate a mano: se un giorno i
    /// capitoli diventano tanti, conviene farle arrivare dal server insieme al catalogo del
    /// Santuario.
    /// </summary>
    public static class AdventureChapterCatalog
    {
        /// <summary>Capitolo consegnato dal tutorial.</summary>
        public const string TutorialChapterId = "chapter-1";

        private static readonly AdventureChapter[] Chapters =
        {
            new AdventureChapter(1, "chapter-1", "Capitolo 1 - I Rampicanti di Trentor",
                "climbing", "Rampicanti", "trentor", "Trentor", "hunter", 0, true),

            new AdventureChapter(2, "chapter-2", "Capitolo 2 - La Nebbia di Bragus",
                "fog", "Nebbia", "boss-bragus", "Bragus", "barbarian", 25, true),

            new AdventureChapter(3, "chapter-3", "Capitolo 3 - L'Infestazione di Jurinashor",
                "infested", "Infestata", "boss-jurinashor", "Jurinashor", "necromancer", 50, false),

            // Nome del boss ancora da decidere: "Seraphel" segue Docs/ScenariBoss.md, non
            // lux.asset (che dice "zakhar", boss che lo stesso documento assegna pero' allo
            // scenario Spettrale). Vedi ChapterCatalog sul server, che tiene la stessa nota.
            new AdventureChapter(4, "chapter-4", "Capitolo 4 - La Luce di Seraphel",
                "lux", "Illuminata", "boss-seraphel", "Seraphel", "paladin", 80, false),

            new AdventureChapter(5, "chapter-5", "Capitolo 5",
                null, null, null, null, "assassin", 120, false),

            // Medusa gira per ora sullo scenario di default, non sugli Specchi.
            new AdventureChapter(6, "chapter-6", "Capitolo 6 - Medusa",
                "default", "Dungeon", "boss-medusa", "Medusa", "priest", 170, true),

            new AdventureChapter(7, "chapter-7", "Capitolo 7 - La Cosmica di Palatir",
                "cosmic", "Cosmica", "boss-palatir", "Palatir", null, 230, true)
        };

        public static IReadOnlyList<AdventureChapter> All => Chapters;

        public static AdventureChapter Find(string chapterId)
        {
            foreach (AdventureChapter chapter in Chapters)
            {
                if (string.Equals(chapter.Id, chapterId, StringComparison.OrdinalIgnoreCase))
                    return chapter;
            }
            return null;
        }

        public static AdventureChapter FindByBoss(string bossId)
        {
            if (string.IsNullOrWhiteSpace(bossId))
                return null;

            foreach (AdventureChapter chapter in Chapters)
            {
                if (!string.IsNullOrEmpty(chapter.BossId) &&
                    string.Equals(chapter.BossId, bossId, StringComparison.OrdinalIgnoreCase))
                    return chapter;
            }
            return null;
        }

        /// <summary>Nome del boss per la schermata di conferma; "Boss" se l'id non e' di capitolo.</summary>
        public static string BossDisplayName(string bossId) => FindByBoss(bossId)?.BossName ?? "Boss";
    }
}
