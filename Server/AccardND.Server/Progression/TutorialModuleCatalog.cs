namespace AccardND.Server.Progression;

/// <summary>
/// I moduli del tutorial progressivo e cosa consegna ognuno. Sorgente unica delle
/// ricompense: il client dice solo quale modulo ha finito, mai cosa gli spetta.
///
/// Il percorso alterna moduli di sistema e moduli di classe. I due doni da 40 vasetti
/// non sono un guadagno ma un buono vincolato: valgono esattamente il prezzo della
/// classe che il tutorial fa comprare subito dopo, e durante l'onboarding non esiste
/// nient'altro su cui spenderli. Il miele resta una cosa che si guadagna in taverna.
///
/// Vedi Docs/tutorial-progressivo-design.md.
/// </summary>
public static class TutorialModuleCatalog
{
    /// <summary>
    /// Il valore di <c>unlock_type</c> con cui i moduli finiti finiscono nel DB. Non e'
    /// "tutorial" di proposito: quel nome e' gia' il pseudo-tipo con cui il pannello admin
    /// tocca la colonna <c>tutorial_completed</c>, e due cose diverse con lo stesso nome
    /// finirebbero per scriversi addosso.
    /// </summary>
    public const string UnlockType = "tutorialModule";

    /// <summary>
    /// Un modulo. Le ricompense sono tutte facoltative: la maggior parte dei moduli non
    /// consegna niente e vale solo come tappa del percorso.
    /// </summary>
    /// <param name="Order">
    /// Posizione nel percorso. Serve alla migrazione e ai controlli di ordine: un modulo
    /// non si puo' riscuotere se quelli prima non sono chiusi.
    /// </param>
    /// <param name="PaysForClassId">
    /// La classe che il tour d'acquisto fa comprare subito dopo. Il dono in miele non e' un
    /// numero scritto qui: e' il prezzo di questa classe, letto dal Santuario. Cosi' il buono
    /// non puo' divergere dal listino - ne' in eccesso, che lascerebbe miele in tasca e
    /// romperebbe "il miele si guadagna in taverna", ne' in difetto, che bloccherebbe il tour
    /// davanti a un acquisto che il giocatore non puo' permettersi.
    /// </param>
    /// <param name="ClassIds">Classi concesse (id del catalogo Santuario).</param>
    /// <param name="ChapterIds">Capitoli concessi.</param>
    /// <param name="ItemIds">Consumabili versati nella scorta, una copia per id.</param>
    /// <param name="CompletesTutorial">
    /// Alza <c>tutorial_completed</c>. Lo fa solo l'ultimo modulo: quel flag e' la porta di
    /// tutto il resto del gioco, e va alzato quando il percorso e' finito davvero.
    /// </param>
    public sealed record Module(
        int Order,
        string Id,
        string Name,
        string PaysForClassId,
        string[] ClassIds,
        string[] ChapterIds,
        string[] ItemIds,
        bool CompletesTutorial);

    private static readonly string[] None = Array.Empty<string>();

    private static readonly Module[] Modules =
    {
        // Il Guerriero apre il percorso: mana, abilita', tecnica in prova e aura Might. E' il
        // primo perche' e' anche la dotazione - la classe la consegna lui - e perche' paga il
        // Mago, che e' il modulo dopo.
        new(0, "m1-warrior", "Il Guerriero",
            "mage", new[] { "warrior" }, None, None, false),

        // Mago: abilita', Palla di fuoco in prova, aura Magic. Paga il Ladro.
        new(1, "m2-mage", "Il Mago",
            "rogue", None, None, None, false),

        // Ladro: abilita' passiva, Ruba potenziamenti in prova, aura Cunning. Chiude il
        // triangolo delle fazioni e apre il Negozio.
		new(2, "m3-rogue", "Il Ladro",
			null, None, None, new[] { "empower" }, false),

        // Oggetti e bisaccia.
        new(3, "m4-items-bag", "Oggetti e bisaccia",
            null, None, None, None, false),

        // Com'e' fatto un capitolo: porte, miniboss, boss, stanze speciali. Spiegazione, non
        // ancora pratica.
        new(4, "m5-chapter-run", "Un capitolo intero",
            null, None, None, None, false),

        // La prova pratica, ultima: la run guidata dall'inizio alla fine. E' quella che
        // consegna il primo capitolo e la Seconda Chance, e chiude il tutorial.
        new(5, "m0-basics", "Primi passi",
            null, None, new[] { ChapterCatalog.TutorialChapterId }, new[] { "second-chance" }, true)
    };

    /// <summary>
    /// Il dono in miele del modulo: il prezzo esatto della classe che fa comprare, zero se
    /// non ne fa comprare nessuna. Finche' una classe resta gratuita a catalogo il dono e'
    /// zero, ed e' giusto cosi': non c'e' niente da pagare.
    /// </summary>
    public static int HoneyOf(Module module)
    {
        if (module?.PaysForClassId == null)
            return 0;
        return SanctuaryCatalog.TryGetEntry(SanctuaryCatalog.TypeClass, module.PaysForClassId, out var entry)
            ? Math.Max(0, entry.HoneyCost)
            : 0;
    }

    public static IReadOnlyList<Module> All => Modules;

    /// <summary>Quanti moduli compongono il percorso.</summary>
    public static int Count => Modules.Length;

    /// <summary>Gli id di tutti i moduli, in ordine di percorso.</summary>
    public static string[] AllIds => Modules.Select(module => module.Id).ToArray();

    public static bool TryGet(string moduleId, out Module module)
    {
        foreach (Module candidate in Modules)
        {
            if (string.Equals(candidate.Id, moduleId, StringComparison.OrdinalIgnoreCase))
            {
                module = candidate;
                return true;
            }
        }
        module = null;
        return false;
    }

    /// <summary>
    /// I moduli che devono essere gia' chiusi prima di poter riscuotere questo. Il percorso
    /// e' una fila: senza questo controllo un client modificato potrebbe riscuotere l'ultimo
    /// modulo per primo e portarsi a casa capitolo e oggetto senza aver giocato niente.
    /// </summary>
    public static string[] RequiredBefore(Module module) =>
        Modules.Where(candidate => candidate.Order < module.Order)
            .Select(candidate => candidate.Id)
            .ToArray();
}
