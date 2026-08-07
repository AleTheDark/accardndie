using AccardND.Server.Progression;

namespace AccardND.Server.Admin;

/// <summary>
/// Cosa il pannello admin puo' concedere o togliere a mano su un account, per gli account di
/// prova: qui costi in miele e prove del Santuario non si applicano, ed e' il punto della
/// funzione.
///
/// E' una whitelist e non un passaggio libero di stringhe: l'id arriva dal browser e finirebbe
/// in <c>single_player_unlocks</c>, che il gioco legge come verita' assoluta, quindi una riga
/// inventata resterebbe li' per sempre senza che nulla la riconosca.
/// </summary>
public static class AdminUnlockCatalog
{
    /// <summary>Pseudo-tipo: il tutorial non e' una riga di unlock ma una colonna del progresso.</summary>
    public const string TypeTutorial = "tutorial";

    public const string TypeMode = "mode";
    public const string TypeChapter = "chapter";
    public const string TypeChapterCleared = "chapterCleared";

    /// <summary>Classi consegnate dal tutorial: tornano da sole finche' il tutorial risulta fatto.</summary>
    public static readonly string[] StarterClassIds = { "mage", "warrior", "rogue" };

    /// <summary>
    /// Voce concedibile. <paramref name="Note"/> dice cosa si sta scavalcando (prove, costo,
    /// "non ancora acquistabile"): senza, il pannello concederebbe alla cieca.
    /// </summary>
    public sealed record Entry(string Id, string Name, string Note);

    public sealed record Group(string Type, string Label, string Note, Entry[] Entries);

    private static readonly Group[] AllGroups = BuildGroups();

    public static IReadOnlyList<Group> Groups => AllGroups;

    /// <summary>
    /// Risolve la coppia tipo/id contro la whitelist e restituisce l'id nella forma esatta del
    /// catalogo, che e' quella che il gioco confronta.
    /// </summary>
    public static bool TryResolve(string type, string id, out string canonicalId)
    {
        foreach (Group group in AllGroups)
        {
            if (!string.Equals(group.Type, type, StringComparison.Ordinal))
                continue;
            foreach (Entry entry in group.Entries)
            {
                if (string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    canonicalId = entry.Id;
                    return true;
                }
            }
        }
        canonicalId = null;
        return false;
    }

    /// <summary>
    /// Tutte le voci che vivono come riga in <c>single_player_unlocks</c>. Tutorial e modalita'
    /// restano fuori: sono colonne di <c>single_player_progress</c> e vanno scritte a parte.
    /// </summary>
    public static IEnumerable<(string Type, string Id)> UnlockRows()
    {
        foreach (Group group in AllGroups)
        {
            if (group.Type is TypeTutorial or TypeMode)
                continue;
            foreach (Entry entry in group.Entries)
                yield return (group.Type, entry.Id);
        }
    }

    public static bool IsStarterClass(string type, string id) =>
        type == SanctuaryCatalog.TypeClass &&
        Array.Exists(StarterClassIds, starter => string.Equals(starter, id, StringComparison.OrdinalIgnoreCase));

    private static Group[] BuildGroups() => new[]
    {
        new Group(SanctuaryCatalog.TypeClass, "Classi",
            "Le tre base arrivano dal tutorial: finche' risulta completato tornano da sole.",
            SanctuaryEntries(SanctuaryCatalog.TypeClass)),

        new Group(SanctuaryCatalog.TypeSecondAbility, "Abilita' supreme",
            "Nel gioco non sono ancora acquistabili: qui si concedono lo stesso per provarle.",
            SanctuaryEntries(SanctuaryCatalog.TypeSecondAbility)),

        new Group(SanctuaryCatalog.TypeItem, "Oggetti",
            "Sbloccano il diritto di comprarli al negozio; le copie si prendono li' col miele.",
            SanctuaryEntries(SanctuaryCatalog.TypeItem)),

        new Group(SanctuaryCatalog.TypeSlot, "Slot bisaccia",
            "Si sommano ai due slot di partenza, fino a quattro.",
            SanctuaryEntries(SanctuaryCatalog.TypeSlot)),

        new Group(TypeChapter, "Capitoli",
            "Accesso al capitolo. In gioco arriva battendo il boss del capitolo prima, oppure comprandolo al Santuario.",
            SanctuaryEntries(TypeChapter)),

        new Group(TypeChapterCleared, "Capitoli completati",
            "Boss finale battuto. Non si compra: si guadagna vincendo, e consegna la classe premio del capitolo.",
            ChapterCatalog.All
                .Select(chapter => new Entry(
                    chapter.Id,
                    chapter.Name,
                    chapter.RewardClassId == null ? null : "premio: " + chapter.RewardClassId))
                .ToArray()),

        new Group(TypeMode, "Modalita'", null,
            new[] { new Entry("hardcore", "Hardcore", "Normalmente 50 miele.") }),

        new Group(TypeTutorial, "Tutorial",
            "Togliendolo il gioco lo riproporra' al prossimo avvio.",
            new[] { new Entry(TypeTutorial, "Tutorial completato", "Consegna classi base e primo capitolo.") })
    };

    private static Entry[] SanctuaryEntries(string type) => SanctuaryCatalog.All
        .Where(entry => entry.Type == type)
        .Select(entry => new Entry(entry.Id, entry.Name, DescribeCost(entry)))
        .ToArray();

    /// <summary>Cosa chiederebbe il Santuario per questa voce, in una riga.</summary>
    private static string DescribeCost(SanctuaryCatalog.Entry entry)
    {
        var parts = new List<string>();
        if (!entry.Available)
            parts.Add(entry.HoneyCost > 0 ? "non acquistabile nel gioco" : "dal tutorial");
        else if (entry.HoneyCost > 0)
            parts.Add(entry.HoneyCost + " miele");
        parts.AddRange(entry.Requirements.Select(requirement => requirement.Description));
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
