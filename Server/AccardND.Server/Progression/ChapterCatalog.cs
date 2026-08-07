namespace AccardND.Server.Progression;

/// <summary>
/// L'ordine dei capitoli della campagna, i loro boss e cosa consegnano. Sorgente unica sul
/// server: la mappa boss-capitolo, la catena degli sblocchi e il listino del Santuario
/// nascono tutti da qui, cosi' riordinare la campagna e' cambiare questa tabella e nient'altro.
///
/// Un capitolo si ottiene in due modi (sono alternative, non passaggi): battendo il boss del
/// capitolo precedente, oppure comprandolo al Santuario col miele.
/// </summary>
public static class ChapterCatalog
{
    /// <summary>
    /// Un capitolo. <paramref name="Playable"/> distingue i capitoli con il boss davvero
    /// implementato da quelli gia' in tabella ma ancora senza avversario: la campagna e'
    /// scritta per intero fin da subito, cosi' gli id finiscono nel DB nella forma
    /// definitiva e non andranno piu' rinumerati quando i contenuti arriveranno.
    /// </summary>
    public sealed record Chapter(
        int Number,
        string Id,
        string Name,
        string ScenarioId,
        string BossId,
        string RewardClassId,
        int HoneyCost,
        bool Playable);

    /// <summary>Capitolo consegnato dal tutorial: e' la dotazione con cui si comincia.</summary>
    public const string TutorialChapterId = "chapter-1";

    private static readonly Chapter[] Chapters =
    {
        new(1, "chapter-1", "Capitolo 1 · I Rampicanti di Trentor",
            "climbing", "trentor", "hunter", 0, true),

        new(2, "chapter-2", "Capitolo 2 · La Nebbia di Bragus",
            "fog", "boss-bragus", "barbarian", 25, true),

        new(3, "chapter-3", "Capitolo 3 · L'Infestazione di Jurinashor",
            "infested", "boss-jurinashor", "necromancer", 50, false),

        // Il boss non ha ancora un nome deciso. "Seraphel" viene da Docs/ScenariBoss.md, che
        // assegna quel boss allo scenario Illuminata; l'asset lux.asset dice invece "zakhar",
        // ma lo stesso documento da' Zakhar allo scenario Spettrale, quindi l'asset e' il
        // dato che non torna. Cambiare questo id piu' avanti non costa nulla: i boss non
        // finiscono nel database, ci finiscono solo i capitoli.
        new(4, "chapter-4", "Capitolo 4 · La Luce di Seraphel",
            "lux", "boss-seraphel", "paladin", 80, false),

        new(5, "chapter-5", "Capitolo 5", null, null, "assassin", 120, false),

        // Medusa gira per ora sullo scenario di default, non sugli Specchi: e' una scelta
        // temporanea, e cambiarla e' cambiare questa riga (piu' la gemella sul client).
        new(6, "chapter-6", "Capitolo 6 · Medusa",
            "default", "boss-medusa", "priest", 170, true),

        // Chiude la campagna: non consegna una classe ma il cosmetico dei dadi, che vive
        // fuori da questo catalogo perche' non e' un unlock di progressione.
        new(7, "chapter-7", "Capitolo 7 · La Cosmica di Palatir",
            "cosmic", "boss-palatir", null, 230, true)
    };

    public static IReadOnlyList<Chapter> All => Chapters;

    public static bool TryGetById(string chapterId, out Chapter chapter)
    {
        foreach (Chapter candidate in Chapters)
        {
            if (string.Equals(candidate.Id, chapterId, StringComparison.OrdinalIgnoreCase))
            {
                chapter = candidate;
                return true;
            }
        }
        chapter = null;
        return false;
    }

    /// <summary>Il capitolo che si chiude battendo questo boss, se ce n'e' uno.</summary>
    public static bool TryGetByFinalBoss(string bossId, out Chapter chapter)
    {
        foreach (Chapter candidate in Chapters)
        {
            if (!string.IsNullOrEmpty(candidate.BossId) &&
                string.Equals(candidate.BossId, bossId, StringComparison.OrdinalIgnoreCase))
            {
                chapter = candidate;
                return true;
            }
        }
        chapter = null;
        return false;
    }

    /// <summary>
    /// Cosa si sblocca completando <paramref name="chapterId"/>: il capitolo successivo e,
    /// finche' i boss di mezzo non esistono, anche quelli che verrebbero saltati.
    ///
    /// Fermarsi al solo capitolo successivo toglierebbe contenuto a chi gioca: con i boss 3,
    /// 4 e 5 ancora da scrivere, chi finisce il capitolo 2 resterebbe davanti a un capitolo
    /// che non si puo' giocare, mentre prima del restyling la campagna proseguiva. Concedere
    /// fino al primo giocabile incluso non lascia buchi in mezzo, quindi il giorno in cui un
    /// boss mancante arriva il giocatore ha gia' il suo capitolo e lo trova semplicemente
    /// aperto. La regola si spegne da sola quando ogni capitolo e' Playable.
    /// </summary>
    public static IReadOnlyList<string> UnlocksAfterClearing(string chapterId)
    {
        if (!TryGetById(chapterId, out Chapter cleared))
            return Array.Empty<string>();

        var granted = new List<string>();
        foreach (Chapter candidate in Chapters)
        {
            if (candidate.Number <= cleared.Number)
                continue;

            granted.Add(candidate.Id);
            if (candidate.Playable)
                break;
        }
        return granted;
    }

    /// <summary>
    /// Un capitolo si compra al Santuario se ha un prezzo e se e' giocabile: far pagare
    /// l'accesso a un capitolo che non si puo' ancora giocare sarebbe vendere il vuoto.
    /// Il primo non ha prezzo perche' lo consegna il tutorial.
    /// </summary>
    public static bool IsPurchasable(Chapter chapter) => chapter.Playable && chapter.HoneyCost > 0;

    /// <summary>
    /// Il capitolo che va posseduto per poter comprarne un altro: gli acquisti restano in
    /// ordine, altrimenti si salta al settimo capitolo dal primo giorno. E' il precedente
    /// <em>giocabile</em>, non il precedente in assoluto: un capitolo ancora senza boss non
    /// si puo' comprare, quindi pretenderlo bloccherebbe per sempre tutti quelli dopo.
    /// Null sul primo, che arriva dal tutorial.
    /// </summary>
    public static string PurchasePrerequisiteOf(Chapter chapter)
    {
        Chapter previous = null;
        foreach (Chapter candidate in Chapters)
        {
            if (candidate.Number >= chapter.Number)
                break;
            if (candidate.Playable)
                previous = candidate;
        }
        return previous?.Id;
    }
}
