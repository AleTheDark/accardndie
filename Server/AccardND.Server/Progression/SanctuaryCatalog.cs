using AccardND.GameCore;
using AccardND.GameCore.Mana;
using AccardND.NetProtocol;

namespace AccardND.Server.Progression;

/// <summary>
/// Catalogo del Santuario: cosa si puo' sbloccare, quanto costa e quali prove servono.
/// Vive sul server ed e' la sorgente unica: il client riceve voci gia' valutate e le disegna.
/// Prima i costi erano duplicati a mano tra client e server ("allineato alle costanti
/// client"), con l'ovvio rischio di divergenza a ogni ritocco di bilanciamento.
///
/// Le classi si comprano col solo miele: il prezzo e' l'unico cancello, e sale con la classe.
/// Le prove guadagnate giocando che chiedevano un tot di boss o un livello account sono state
/// tolte. Il motore dei requisiti resta in piedi - lo usa la tecnica, che chiede di possedere
/// la classe corrispondente - quindi rimetterne una e' aggiungere una riga qui sotto.
/// </summary>
public static class SanctuaryCatalog
{
    public const string TypeClass = "class";
    public const string TypeSecondAbility = "secondAbility";
    public const string TypeItem = "item";
    public const string TypeSlot = "slot";
    public const string TypeChapter = "chapter";

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

            AdvancedClass("assassin", "Assassino", 40),
            AdvancedClass("hunter", "Cacciatore", 40),
            AdvancedClass("paladin", "Paladino", 40),
            AdvancedClass("barbarian", "Barbaro", 60),
            AdvancedClass("necromancer", "Negromante", 60),
            AdvancedClass("priest", "Sacerdote", 60)
        };

        // Tecniche: una per classe, con l'effetto vero preso da GameCore. Sono acquistabili
        // quando la suprema e' implementata nel motore; quella del Negromante non lo e'
        // ancora, quindi resta visibile col prezzo ma bloccata. Gli id sono definitivi da
        // subito perche' finiscono nel DB degli unlock e non si rinominano piu'.
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

        // Capitoli. Il Santuario e' il solo banco dove si comprano: nella schermata
        // Avventura un capitolo chiuso non si apre piu' pagando, si guarda e basta. Restano
        // due strade per averlo, ed e' voluto che siano alternative: batti il boss del
        // capitolo prima, oppure paghi per non aspettare.
        foreach (ChapterCatalog.Chapter chapter in ChapterCatalog.All)
            entries.Add(ChapterEntry(chapter));

        return entries.ToArray();
    }

    private static Entry ChapterEntry(ChapterCatalog.Chapter chapter) => new(
        TypeChapter,
        chapter.Id,
        chapter.Name,
        chapter.Playable
            ? "Accesso al capitolo. Si ottiene anche battendo il boss del capitolo precedente."
            : "In arrivo: il capitolo esiste gia' nella campagna ma il suo boss non e' ancora pronto.",
        chapter.HoneyCost,
        ChapterCatalog.IsPurchasable(chapter),
        Array.Empty<Requirement>());

    private static Entry StarterClass(string id, string name) => new(
        TypeClass, id, name, "Ottenuta completando il tutorial.", 0, false, Array.Empty<Requirement>());

    private static Entry AdvancedClass(string id, string name, int cost) => new(
        TypeClass, id, name, "Classe avanzata.", cost, true, Array.Empty<Requirement>());

    private static Entry SecondAbility(string classId, string className)
    {
        // Gli id delle classi a catalogo sono i nomi dell'enum in minuscolo: se un domani
        // divergessero, meglio una tecnica bloccata che una venduta con l'effetto sbagliato.
        bool known = Enum.TryParse(classId, ignoreCase: true, out HeroClass heroClass);
        bool implemented = known && AbilityManaCosts.IsSupremeImplemented(heroClass);

        return new Entry(
            TypeSecondAbility,
            $"ability-{classId}-2",
            known ? $"{className}: {SupremeAbilityText.Name(heroClass)}" : $"Tecnica di {className}",
            implemented
                ? SupremeAbilityText.Description(heroClass)
                : "In preparazione: sara' acquistabile quando l'effetto sara' definito.",
            SecondAbilityCost,
            implemented,
            new[] { new Requirement(KindClassOwned, classId, 1, $"Possiedi la classe {className}") });
    }

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
