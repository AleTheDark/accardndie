namespace AccardND.Server.Progression;

/// <summary>
/// L'albero dei talenti: quali nodi esistono, quanto costano e quanto valgono. Vive sul
/// server ed e' la sorgente unica, come il <see cref="SanctuaryCatalog"/>: il client riceve
/// voci gia' valutate e le disegna, e i valori degli effetti scendono nel pacchetto di
/// inizio run. Duplicarli sul client li farebbe divergere al primo ritocco di bilanciamento,
/// che e' l'errore gia' commesso e corretto sui costi del Santuario.
///
/// Due regole che governano tutta la tabella:
///
/// <list type="number">
/// <item>
/// <b>Nessun nodo esclusivo.</b> Non c'e' respec, quindi chi spende male resterebbe fregato
/// per sempre. Ogni nodo e' un miglioramento secco, tutto e' comprabile prima o poi, e
/// l'unica scelta reale e' l'ordine.
/// </item>
/// <item>
/// <b>Nessun talento aumenta l'esperienza guadagnata in una run.</b> L'exp account e' un
/// decimo di quella di run: un bonus "+% exp" chiuderebbe un anello aritmetico (piu' exp,
/// piu' livelli, piu' punti, piu' exp) che renderebbe il ramo Maestria obbligatorio e gli
/// altri tre decorativi. Per questo Maestria agisce sulle <em>soglie</em> di livello e mai
/// sull'exp incassata. Vedi Docs/talenti-design.md.
/// </item>
/// </list>
/// </summary>
public static class TalentCatalog
{
    public const string BranchPurse = "purse";
    public const string BranchInitiative = "initiative";
    public const string BranchMastery = "mastery";
    public const string BranchOccasion = "occasion";

    /// <summary>
    /// Un nodo dell'albero.
    /// </summary>
    /// <param name="Values">
    /// Il valore dell'effetto rango per rango: <c>Values[0]</c> e' il rango 1. E' un array e
    /// non una formula perche' i valori non sono lineari su tutti i nodi, e una tabella si
    /// ritocca senza toccare il codice che la legge.
    /// </param>
    /// <param name="ValueFormat">
    /// Come si scrive il valore per il giocatore: <c>{0}</c> e' il numero, il resto e'
    /// l'unita' di misura. Senza questo campo la scheda mostrerebbe un numero nudo, e "ora
    /// 10" vuol dire dieci monete su un nodo, dieci per cento su quello accanto: la stessa
    /// scritta per due cose diverse.
    ///
    /// Un formato con la barra verticale ha due varianti, singolare e plurale in quest'ordine
    /// (<c>"{0} carta|{0} carte"</c>). Null sui nodi che non hanno un numero da mostrare -
    /// quelli a rango unico, dove la descrizione dice gia' tutto.
    /// </param>
    public sealed record Talent(
        string Id,
        string Branch,
        int Tier,
        string Name,
        string Description,
        int CostPerRank,
        int[] Values,
        string ValueFormat = null);

    /// <summary>
    /// Punti da spendere nel ramo per aprire il tier: l'indice e' il tier, il valore la
    /// soglia. Il livello account distribuisce i punti, il ramo decide dove possono andare,
    /// quindi la specializzazione emerge da sola senza un secondo cancello sul livello.
    ///
    /// Le soglie vanno tarate contro quanto un ramo puo' davvero assorbire, non scelte a
    /// tavolino: erano <c>{0, 0, 5, 12, 20}</c> e tre rami su quattro si murava da soli. Le
    /// Occasioni sono il caso limite - l'unico nodo di tier 1 vale 4 punti in tutto, contro
    /// un cancello da 5, e il ramo finiva li' anche con propoli infiniti. Il tetto vero, ramo
    /// per ramo, e' 3/9/17 punti cumulativi (Iniziativa e Maestria sono le piu' magre):
    /// questi numeri lasciano un rango di margine ovunque.
    /// Difeso da <c>TalentTests.Every_branch_can_be_finished</c>.
    /// </summary>
    private static readonly int[] TierGates = { 0, 0, 2, 7, 14 };

    private static readonly Talent[] Talents =
    {
        // ---- Borsa: l'economia della run ------------------------------------------------
        // "+1 oro" non si vedrebbe: MerchantEconomy paga 6-20 a stanza e una carta costa
        // 12-36, quindi lo scalino minimo percepibile e' 2.
        new("purse-travel-fund", BranchPurse, 1, "Fondo di viaggio",
            "Inizi ogni run con dell'oro gia' in tasca.",
            1, new[] { 2, 4, 6, 8, 10 }, "+{0} oro alla partenza"),

        // Qui c'era "Forgia generosa", che dava essenze da spendere nella forgia. La forgia
        // non ha piu' una valuta: il mazzo di partenza si compone scegliendo campione e vice,
        // e le altre sette carte escono a caso. Il nodo era diventato un acquisto inerte -
        // esattamente il caso contro cui mette in guardia Docs/talenti-design.md - quindi e'
        // ritirato, e i propoli spesi tornano indietro con RemovedTalentRefundMigration.
        //
        // Se un domani la composizione del mazzo torna ad avere una leva su cui spendere, il
        // posto per rimetterlo e' questo, ma con un effetto che parli di quella leva.
        new("purse-kind-merchant", BranchPurse, 2, "Mercante compiacente",
            "Al mercato le carte e i potenziamenti ti costano meno oro.",
            3, new[] { 10, 20 }, "-{0}% sui prezzi del mercante"),

        // Sicuro perche' maximumCopiesPerCard e' 1: non puo' impilarsi sulla stessa carta.
        new("purse-smith-temper", BranchPurse, 3, "Tempra del fabbro",
            "Appena forgiato il mazzo, alcune carte a caso ottengono +1 Forza permanente.",
            5, new[] { 1, 2 }, "{0} carta temprata|{0} carte temprate"),

        new("purse-first-deal", BranchPurse, 4, "Primo affare",
            "Il primo potenziamento che compri dal mercante e' gratis, una volta per run.",
            6, new[] { 1 }),

        // ---- Iniziativa: i tre dadi di inizio scontro -----------------------------------
        // Il dado e' un d20 e i dadi tirati sono tre: +3 vale +15%, che e' il pollice sulla
        // bilancia voluto e non un'iniziativa decisa a tavolino.
        //
        // L'avviso sul numero a schermo sta solo sul primo nodo: e' la stessa regola per
        // tutti e tre, e ripeterla tre volte la farebbe smettere di essere letta.
        new("initiative-vanguard", BranchInitiative, 1, "Avanguardia",
            "Il tuo primo dado d'iniziativa vale di piu' di quello che mostra.",
            1, new[] { 1, 2, 3 }, "+{0} al primo dado d'iniziativa"),

        new("initiative-flanker", BranchInitiative, 2, "Fiancheggiatore",
            "Anche il tuo secondo dado d'iniziativa conta di piu'.",
            2, new[] { 1, 2, 3 }, "+{0} al secondo dado d'iniziativa"),

        new("initiative-rearguard", BranchInitiative, 3, "Retroguardia",
            "Anche il tuo terzo dado d'iniziativa conta di piu'.",
            3, new[] { 1, 2, 3 }, "+{0} al terzo dado d'iniziativa"),

        // Qui prima c'era "Colpo d'anticipo", che vinceva le parita' di iniziativa: le
        // parita' non esistono, perche' i tiri sono estratti unici fra tutti i combattenti.
        // Era un talento comprabile e inerte, quello contro cui mette in guardia il
        // documento di design. Adesso il nodo non aspetta piu' un caso che non arriva: si
        // prende il primo turno e basta.
        new("initiative-opening", BranchInitiative, 4, "Apertura",
            "Sei sempre tu ad aprire lo scontro: il tuo primo dado d'iniziativa batte qualunque numero in campo.",
            8, new[] { 1 }),

        // ---- Maestria: il mana per le abilita' ------------------------------------------
        // Il ramo era fatto di quattro sconti sulle soglie di livello: quattro nodi che
        // spingono tutti sulla stessa leva accorciano la run invece di cambiarla, e si
        // arrivava al d20 troppo presto. Ne resta uno solo, il piu' blando.
        //
        // Gli altri tre stanno sul mana e non sui totali di combattimento: un talento che
        // somma Potenza sposta ogni singolo scontro di un sistema che confronta numeri
        // piccoli, mentre il mana cambia quanto spesso puoi giocare le abilita' senza
        // toccare chi vince il confronto. Ognuno scatta in un momento diverso - cambio
        // stanza, eliminazione, prima abilita' - cosi' il ramo non e' lo stesso nodo tre
        // volte con la cifra piu' grossa.
        //
        // Apprendista abbassa il traguardo, non aumenta il passo: l'esperienza incassata
        // resta identica a quella di chi non ha talenti. E' la regola che tiene chiuso
        // l'anello exp -> livelli -> punti.
        new("mastery-apprentice", BranchMastery, 1, "Apprendista",
            "Ogni livello della run arriva prima: serve meno esperienza per ogni soglia.",
            1, new[] { 2, 4, 6, 8, 10 }, "-{0}% a tutte le soglie"),

        new("mastery-focus", BranchMastery, 2, "Concentrazione",
            "Recuperi mana ogni volta che entri in una stanza nuova.",
            2, new[] { 1, 2 }, "+{0} mana a ogni cambio stanza"),

        new("mastery-reserve", BranchMastery, 3, "Riserva",
            "La tua riserva di mana tiene di piu': il tetto si alza da 10 fino a 12.",
            4, new[] { 1, 2 }, "+{0} al massimo di mana"),

        // "Abilita' base" e non "abilita' di classe": anche le supreme sono abilita' di
        // classe, e questo nodo non le tocca - si pagano in CampaignSupreme.cs, che e' un
        // percorso separato da TrySpendCampaignPrimaryMana.
        new("mastery-trance", BranchMastery, 4, "Trance",
            "La prima abilita' base che usi in ogni stanza non costa mana. Le supreme si pagano comunque.",
            8, new[] { 1 }),

        // ---- Occasioni: i condizionali --------------------------------------------------
        // "Recupero" e "Secondo fiato" toccano lo stesso gancio - il bottone RECUPERA del
        // mercato - e devono chiamarlo con le stesse parole, altrimenti sembrano due cose.
        new("occasion-recovery", BranchOccasion, 1, "Recupero",
            "Al mercato, riportare una carta dal cimitero nel mazzo costa meno oro.",
            2, new[] { 10, 20 }, "-{0}% sul costo di recupero"),

        new("occasion-challenger", BranchOccasion, 2, "Sfidante",
            "Il tuo primo attacco in ogni scontro contro un boss o un miniboss colpisce piu' forte.",
            3, new[] { 1, 2 }, "+{0} Potenza al primo attacco"),

        new("occasion-seeker", BranchOccasion, 3, "Cercatore",
            "Ogni stanza bottino ti consegna consumabili in piu'.",
            4, new[] { 1, 2 }, "+{0} consumabile a bottino|+{0} consumabili a bottino"),

        // Prima questo nodo regalava il primo recupero al mercato: il capstone del ramo era
        // una versione piu' piccola del suo nodo d'apertura - Recupero costa 4 propoli in
        // tutto e sconta ogni recupero della run, questo ne costava 8 e ne copriva uno solo.
        // Adesso la carta non passa nemmeno dal cimitero, e i due nodi smettono di
        // contendersi la stessa leva.
        new("occasion-second-wind", BranchOccasion, 4, "Secondo fiato",
            "La prima pedina che perdi in ogni run non va al cimitero: torna subito nel mazzo.",
            8, new[] { 1 })
    };

    public static IReadOnlyList<Talent> All => Talents;

    public static bool TryGet(string talentId, out Talent talent)
    {
        foreach (Talent candidate in Talents)
        {
            if (string.Equals(candidate.Id, talentId, StringComparison.OrdinalIgnoreCase))
            {
                talent = candidate;
                return true;
            }
        }
        talent = null;
        return false;
    }

    /// <summary>Punti da spendere nel ramo per aprire il tier di <paramref name="talent"/>.</summary>
    public static int TierGateOf(Talent talent)
    {
        int tier = Math.Clamp(talent.Tier, 0, TierGates.Length - 1);
        return TierGates[tier];
    }

    /// <summary>
    /// Il valore dell'effetto al rango posseduto. Rango 0 vuol dire nodo non comprato, e
    /// vale zero: i chiamanti possono sommare senza controllare il possesso.
    /// </summary>
    public static int ValueAtRank(Talent talent, int rank)
    {
        if (rank <= 0 || talent.Values.Length == 0)
            return 0;
        int index = Math.Min(rank, talent.Values.Length) - 1;
        return talent.Values[index];
    }

    public static int MaxRankOf(Talent talent) => talent.Values.Length;

    /// <summary>
    /// L'effetto scritto per il giocatore: il numero con la sua unita' di misura, gia'
    /// pronto da stampare. Null quando non c'e' niente da mostrare - nodo a rango unico,
    /// oppure rango zero, dove l'effetto attuale non esiste ancora.
    ///
    /// Vive qui e non sul client perche' il formato appartiene al nodo quanto il suo valore:
    /// tenerli in due file diversi vuol dire aggiungere un talento in uno e ricordarsi del
    /// secondo, che e' il modo in cui una scritta resta indietro di un ritocco.
    /// </summary>
    public static string FormatValue(Talent talent, int value)
    {
        if (value <= 0 || string.IsNullOrEmpty(talent.ValueFormat))
            return null;

        // Un formato con la barra ha singolare e plurale: "1 carte temprate" e' il genere di
        // sciatteria che fa sembrare provvisorio tutto il resto della schermata.
        string format = talent.ValueFormat;
        int separator = format.IndexOf('|');
        if (separator >= 0)
        {
            format = value == 1
                ? format[..separator]
                : format[(separator + 1)..];
        }

        return string.Format(System.Globalization.CultureInfo.InvariantCulture, format, value);
    }

    /// <summary>L'effetto al rango posseduto, gia' scritto. Null al rango zero.</summary>
    public static string FormatValueAtRank(Talent talent, int rank) =>
        rank <= 0 ? null : FormatValue(talent, ValueAtRank(talent, rank));

    /// <summary>Costo totale per portare un nodo dal rango 0 al massimo.</summary>
    public static int FullCostOf(Talent talent) => talent.CostPerRank * MaxRankOf(talent);
}
