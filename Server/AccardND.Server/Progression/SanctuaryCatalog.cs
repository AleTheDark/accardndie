using AccardND.NetProtocol;

namespace AccardND.Server.Progression;

/// <summary>
/// Catalogo del Santuario: cosa si puo' sbloccare, quanto costa e quali prove servono.
/// Vive sul server ed e' la sorgente unica: il client riceve voci gia' valutate e le disegna.
/// Prima i costi erano duplicati a mano tra client e server ("allineato alle costanti
/// client"), con l'ovvio rischio di divergenza a ogni ritocco di bilanciamento.
///
/// Principio di design: il miele non compra lo sblocco, lo conferma. Ogni classe avanzata
/// chiede una prova guadagnata giocando, e la difficolta' della prova sale insieme al costo.
/// </summary>
public static class SanctuaryCatalog
{
    public const string TypeClass = "class";
    public const string TypeSecondAbility = "secondAbility";
    public const string TypeItem = "item";
    public const string TypeSlot = "slot";

    /// <summary>Slot bisaccia disponibili senza acquisti.</summary>
    public const int BaseBagSlots = 2;

    /// <summary>Tetto assoluto: oltre questo la run smette di essere una serie di scelte.</summary>
    public const int MaxBagSlots = 4;

    public const string KindCounter = "counter";
    public const string KindAccountLevel = "accountLevel";
    public const string KindClassOwned = "classOwned";

    public sealed record Requirement(string Kind, string Key, int Threshold, string Description);

    public sealed record Entry(
        string Type,
        string Id,
        string Name,
        string Description,
        int HoneyCost,
        bool Available,
        Requirement[] Requirements);

    private const int SecondAbilityCost = 80;

    private static readonly Entry[] Entries = BuildEntries();

    public static IReadOnlyList<Entry> All => Entries;

    public static bool TryGetEntry(string type, string id, out Entry entry)
    {
        foreach (Entry candidate in Entries)
        {
            if (string.Equals(candidate.Type, type, StringComparison.Ordinal) &&
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                entry = candidate;
                return true;
            }
        }
        entry = null;
        return false;
    }

    private static Entry[] BuildEntries()
    {
        var entries = new List<Entry>
        {
            // Classi starter: concesse dal tutorial, mostrate nell'altare ma non acquistabili.
            // Un altare con 6 slot su 9 racconterebbe male la storia.
            StarterClass("mage", "Mago"),
            StarterClass("warrior", "Guerriero"),
            StarterClass("rogue", "Ladro"),

            AdvancedClass("assassin", "Assassino", 40,
                Counter("boss_bragus", 2, "Sconfiggi Bragus 2 volte")),
            AdvancedClass("hunter", "Cacciatore", 40,
                Counter("boss_trentor", 2, "Sconfiggi Trentor 2 volte")),
            AdvancedClass("paladin", "Paladino", 40,
                Counter("miniboss_golem", 3, "Sconfiggi il Golem 3 volte")),
            AdvancedClass("barbarian", "Barbaro", 60,
                Counter("boss_medusa", 2, "Sconfiggi Medusa 2 volte")),
            AdvancedClass("necromancer", "Negromante", 60,
                AccountLevel(5, "Raggiungi il livello account 5"),
                Counter("enemies_defeated", 300, "Sconfiggi 300 nemici")),
            AdvancedClass("priest", "Sacerdote", 60,
                Counter("boss_palatir", 2, "Sconfiggi Palatir 2 volte"),
                Counter("daily_completed", 5, "Completa 5 giornate di quest in taverna"))
        };

        // Tecniche: gli effetti non sono ancora definiti, quindi restano visibili col prezzo
        // ma bloccate. Gli id sono definitivi da subito perche' finiscono nel DB degli unlock
        // e non si rinominano piu'.
        foreach (Entry classEntry in entries.Where(entry => entry.Type == TypeClass).ToArray())
            entries.Add(SecondAbility(classEntry.Id, classEntry.Name));

        // Reliquie. Al Santuario si sblocca il diritto di comprare un oggetto: l'acquisto
        // delle copie avviene poi al negozio. Quindi questi prezzi pagano un permesso
        // permanente, non un pezzo, e stanno percio' ben sopra al costo di una singola copia.
        // La gerarchia di forza resta quella del mercante in run:
        // Detector < Defrost < DoppiaEXP < Empower < SigilloRubino < SecondaChance.
        entries.Add(Item("detector", "Detector", 20,
            "Sblocca il Detector al negozio: rivela il destino delle tre porte."));
        entries.Add(Item("defrost", "Defrost", 25,
            "Sblocca il Defrost al negozio: scongela le carte in cooldown."));
        entries.Add(Item("double-exp", "Doppia EXP", 30,
            "Sblocca la Doppia EXP al negozio: raddoppia l'esperienza di una stanza."));
        entries.Add(Item("empower", "Empower", 40,
            "Sblocca l'Empower al negozio: alza di uno step il dado Vigore in attacco."));
        entries.Add(Item("sigillo-rubino", "Sigillo Rubino", 50,
            "Sblocca il Sigillo Rubino al negozio: incide +2 permanente su una carta, una sola volta per carta."));
        entries.Add(Item("second-chance", "Seconda Chance", 70,
            "Sblocca la Seconda Chance al negozio: riporta nel mazzo le carte dal cimitero."));

        entries.Add(Slot("bag-slot-3", "Terzo slot", 60));
        entries.Add(Slot("bag-slot-4", "Quarto slot", 150));

        return entries.ToArray();
    }

    private static Entry StarterClass(string id, string name) => new(
        TypeClass, id, name, "Ottenuta completando il tutorial.", 0, false, Array.Empty<Requirement>());

    private static Entry AdvancedClass(string id, string name, int cost, params Requirement[] requirements) => new(
        TypeClass, id, name, "Classe avanzata.", cost, true, requirements);

    private static Entry SecondAbility(string classId, string className) => new(
        TypeSecondAbility,
        $"ability-{classId}-2",
        $"Tecnica di {className}",
        "In preparazione: sara' acquistabile quando l'effetto sara' definito.",
        SecondAbilityCost,
        false,
        new[] { new Requirement(KindClassOwned, classId, 1, $"Possiedi la classe {className}") });

    private static Entry Item(string id, string name, int cost, string description) => new(
        TypeItem, id, name, description, cost, true, Array.Empty<Requirement>());

    /// <summary>
    /// Prezzo di una singola copia al negozio, derivato dal costo di sblocco. Provvisorio:
    /// il listino vero arrivera' con la pagina del negozio. Tenerlo derivato evita per ora
    /// una seconda tabella di numeri da mantenere allineata.
    /// </summary>
    public static int CopyCostOf(Entry entry) => Math.Max(1, entry.HoneyCost / 4);

    private static Entry Slot(string id, string name, int cost) => new(
        TypeSlot, id, name, "Uno slot in piu' nella bisaccia.", cost, true, Array.Empty<Requirement>());

    private static Requirement Counter(string key, int threshold, string description) =>
        new(KindCounter, key, threshold, description);

    private static Requirement AccountLevel(int threshold, string description) =>
        new(KindAccountLevel, string.Empty, threshold, description);
}

/// <summary>
/// Stato del giocatore contro cui si valutano le prove. Costruito una volta per richiesta
/// dallo stesso snapshot autoritativo che viene rimandato al client, cosi' quello che il
/// giocatore vede e quello che il server valida non possono divergere.
/// </summary>
public sealed class SanctuaryRequirementContext
{
    private readonly Dictionary<string, int> counters = new(StringComparer.Ordinal);
    private readonly HashSet<string> unlockedClasses = new(StringComparer.OrdinalIgnoreCase);
    private readonly int accountLevel;

    public SanctuaryRequirementContext(SinglePlayerProgressData progress)
    {
        accountLevel = Math.Max(1, progress?.accountLevel ?? 1);

        foreach (PlayerCounterData counter in progress?.counters ?? Array.Empty<PlayerCounterData>())
        {
            if (counter != null && !string.IsNullOrWhiteSpace(counter.key))
                counters[counter.key] = counter.value;
        }

        foreach (string classId in progress?.unlockedClasses ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(classId))
                unlockedClasses.Add(classId);
        }
    }

    /// <summary>Valore attuale del giocatore per la prova indicata.</summary>
    public int CurrentValue(SanctuaryCatalog.Requirement requirement) => requirement.Kind switch
    {
        SanctuaryCatalog.KindCounter => counters.TryGetValue(requirement.Key, out int value) ? value : 0,
        SanctuaryCatalog.KindAccountLevel => accountLevel,
        SanctuaryCatalog.KindClassOwned => unlockedClasses.Contains(requirement.Key) ? 1 : 0,
        // Una prova di tipo sconosciuto non deve mai risultare superata per sbaglio.
        _ => 0
    };

    public bool IsMet(SanctuaryCatalog.Requirement requirement) =>
        CurrentValue(requirement) >= requirement.Threshold;

    public bool AreAllMet(IEnumerable<SanctuaryCatalog.Requirement> requirements) =>
        requirements == null || requirements.All(IsMet);

    public bool OwnsClass(string classId) => unlockedClasses.Contains(classId ?? string.Empty);
}
