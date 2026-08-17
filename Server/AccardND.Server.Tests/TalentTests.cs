using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// L'albero dei talenti: cancelli di tier, spesa dei punti e pacchetto di modificatori.
/// Difende anche la regola che tiene in piedi il bilanciamento dell'intero sistema - nessun
/// talento aumenta l'esperienza guadagnata in una run - perche' e' una regola che si viola
/// aggiungendo una riga al catalogo e senza accorgersene.
/// </summary>
public sealed class TalentTests
{
    [Fact]
    public void A_fresh_account_sees_the_whole_tree_locked_by_price()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "albero-nuovo");

        TalentData data = talents.GetTalents(player);

        Assert.Equal(0, data.talentPoints);
        Assert.Equal(4, data.branches.Length);
        Assert.Equal(TalentCatalog.All.Count, data.talents.Length);
        Assert.All(data.talents, entry => Assert.False(entry.purchasable));
        Assert.All(data.talents, entry => Assert.Equal(0, entry.rank));
    }

    [Fact]
    public void Buying_a_rank_spends_the_points_and_raises_the_value()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "primo-acquisto", points: 5);

        (TalentData data, string code, string error) = talents.BuyTalent(
            player, new TalentBuyRequest { talentId = "purse-travel-fund" });

        Assert.Null(code);
        Assert.Null(error);
        Assert.Equal(4, data.talentPoints);
        TalentEntryData node = Entry(data, "purse-travel-fund");
        Assert.Equal(1, node.rank);
        Assert.Equal(2, node.currentValue);
        Assert.Equal(4, node.nextValue);
        Assert.Equal(1, Branch(data, TalentCatalog.BranchPurse).pointsSpent);
    }

    [Fact]
    public void A_tier_stays_shut_until_the_branch_has_been_paid()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "cancello", points: 50);

        // "Tempra del fabbro" e' tier 3: chiede 7 propoli gia' spesi nella Borsa.
        (_, string code, string error) = talents.BuyTalent(
            player, new TalentBuyRequest { talentId = "purse-smith-temper" });

        Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
        Assert.Contains("7 propoli spesi", error);
        // I punti restano in mano: il rifiuto non costa niente.
        Assert.Equal(50, talents.GetTalents(player).talentPoints);
    }

    [Fact]
    public void Spending_in_the_branch_opens_the_next_tier()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "salita-tier", points: 50);

        // Cinque ranghi di Fondo di viaggio (1 l'uno) aprono il tier 2 della Borsa, che ne
        // chiede 2, ma non il tier 3, che ne chiede 7.
        for (int rank = 0; rank < 5; rank++)
            talents.BuyTalent(player, new TalentBuyRequest { talentId = "purse-travel-fund" });

        TalentData data = talents.GetTalents(player);
        Assert.True(Entry(data, "purse-kind-merchant").tierUnlocked);
        Assert.True(Entry(data, "purse-kind-merchant").purchasable);
        Assert.False(Entry(data, "purse-smith-temper").tierUnlocked);
    }

    [Fact]
    public void A_maxed_node_cannot_be_bought_again()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "rango-massimo", points: 50);

        for (int rank = 0; rank < 5; rank++)
            talents.BuyTalent(player, new TalentBuyRequest { talentId = "purse-travel-fund" });

        (_, string code, string error) = talents.BuyTalent(
            player, new TalentBuyRequest { talentId = "purse-travel-fund" });

        Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
        Assert.Contains("rango massimo", error);
        Assert.Equal(10, Entry(talents.GetTalents(player), "purse-travel-fund").currentValue);
    }

    [Fact]
    public void Points_you_do_not_have_buy_nothing()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "al-verde");

        (_, string code, string error) = talents.BuyTalent(
            player, new TalentBuyRequest { talentId = "purse-travel-fund" });

        Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
        Assert.Contains("propoli", error);
    }

    [Fact]
    public void An_unknown_talent_is_refused()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "id-inventato", points: 50);

        (_, string code, _) = talents.BuyTalent(
            player, new TalentBuyRequest { talentId = "purse-non-esiste" });

        Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
    }

    [Fact]
    public void The_loadout_carries_the_ranks_into_the_run()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "pacchetto", points: 50);

        talents.BuyTalent(player, new TalentBuyRequest { talentId = "purse-travel-fund" });
        talents.BuyTalent(player, new TalentBuyRequest { talentId = "purse-travel-fund" });
        talents.BuyTalent(player, new TalentBuyRequest { talentId = "initiative-vanguard" });

        TalentLoadoutData loadout = talents.GetLoadout(player);

        Assert.Equal(4, loadout.startingGold);
        Assert.Equal(3, loadout.initiativeBonusBySlot.Length);
        Assert.Equal(1, loadout.initiativeBonusBySlot[0]);
        Assert.Equal(0, loadout.initiativeBonusBySlot[1]);
        Assert.False(loadout.opensEveryFight);
    }

    [Fact]
    public void An_empty_loadout_is_all_zeroes_and_never_null()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "pacchetto-vuoto");

        TalentLoadoutData loadout = talents.GetLoadout(player);

        // Il client indicizza l'array senza controllarne la lunghezza: se arrivasse null
        // esploderebbe al primo schieramento invece che qui.
        Assert.Equal(3, loadout.initiativeBonusBySlot.Length);
        Assert.Equal(0, loadout.startingGold);
        Assert.Equal(0, loadout.startingEssence);
    }

    [Fact]
    public void The_loadout_travels_with_the_progress_snapshot()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "pacchetto-snap", points: 50);
        var progress = new SinglePlayerProgressService(server.Database);
        talents.BuyTalent(player, new TalentBuyRequest { talentId = "purse-travel-fund" });

        SinglePlayerProgressData data = progress.GetProgress(player);

        Assert.NotNull(data.talentLoadout);
        Assert.Equal(2, data.talentLoadout.startingGold);
    }

    [Fact]
    public void No_talent_may_grant_run_experience()
    {
        // La regola che tiene chiuso l'anello exp -> livelli -> punti -> exp. Il ramo
        // Maestria agisce sulle soglie di livello, mai sull'esperienza incassata: se un
        // giorno qualcuno aggiungesse un nodo che regala exp, va aggiunto insieme al
        // contatore separato in RunProgressState, e questo test va cambiato apposta.
        foreach (System.Reflection.FieldInfo field in typeof(TalentLoadoutData).GetFields())
        {
            Assert.DoesNotContain("experienceGain", field.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bonusExperience", field.Name, StringComparison.OrdinalIgnoreCase);
        }

        // I quattro nodi che la Maestria puo' avere, per id. Prima qui si controllava che la
        // descrizione contenesse la parola "soglia", ma quella e' la prosa mostrata al
        // giocatore e va riscritta quando non si capisce: un test che la ingessa costringe a
        // scegliere fra un testo leggibile e una regola di bilanciamento. Gli id no, e un
        // nodo nuovo nel ramo fa fallire questa riga - che e' esattamente il momento in cui
        // qualcuno deve fermarsi a pensare a cosa sta agganciando.
        string[] masteryNodes =
        {
            "mastery-apprentice", "mastery-focus", "mastery-reserve", "mastery-trance"
        };

        foreach (TalentCatalog.Talent talent in TalentCatalog.All)
        {
            if (!string.Equals(talent.Branch, TalentCatalog.BranchMastery, StringComparison.Ordinal))
                continue;
            Assert.Contains(talent.Id, masteryNodes);
            Assert.DoesNotContain("esperienza in piu'", talent.Description);
            Assert.DoesNotContain("esperienza in piu'", talent.ValueFormat ?? string.Empty);
        }
    }

    [Fact]
    public void Only_one_node_in_the_whole_tree_touches_the_level_thresholds()
    {
        // Quattro nodi che spingevano tutti sulle soglie accorciavano la run invece di
        // cambiarla: si arrivava al dado grande troppo presto e la campagna diventava una
        // discesa. Ne resta uno, il piu' blando. Se un giorno se ne aggiunge un altro deve
        // essere una decisione, non il quinto sconto scritto per abitudine.
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "una-sola-leva", points: 100_000);

        for (int pass = 0; pass < TalentCatalog.All.Count; pass++)
        {
            foreach (TalentCatalog.Talent talent in TalentCatalog.All)
                talents.BuyTalent(player, new TalentBuyRequest { talentId = talent.Id });
        }

        TalentLoadoutData loadout = talents.GetLoadout(player);

        // Albero completo: lo sconto totale sulle soglie e' quello del solo Apprendista.
        Assert.Equal(10, loadout.masteryThresholdPercent);
    }

    [Fact]
    public void Every_ranked_node_says_what_its_number_means()
    {
        // Un valore senza unita' di misura non e' un'informazione: "ora 10" vale dieci monete
        // su un nodo e dieci per cento su quello accanto. Ogni nodo con piu' di un rango deve
        // percio' portarsi dietro il proprio formato, e il formato deve consumare il numero.
        foreach (TalentCatalog.Talent talent in TalentCatalog.All)
        {
            if (TalentCatalog.MaxRankOf(talent) <= 1)
                continue;

            Assert.False(string.IsNullOrEmpty(talent.ValueFormat), talent.Id);
            Assert.Contains("{0}", talent.ValueFormat);

            string first = TalentCatalog.FormatValueAtRank(talent, 1);
            string last = TalentCatalog.FormatValueAtRank(talent, TalentCatalog.MaxRankOf(talent));
            Assert.NotNull(first);
            Assert.NotEqual(first, last);
            Assert.DoesNotContain("{0}", first);
        }
    }

    [Fact]
    public void A_fresh_node_shows_what_the_first_rank_would_give()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "prima-di-spendere", points: 3);

        TalentEntryData node = Entry(talents.GetTalents(player), "purse-travel-fund");

        // Il numero deve essere leggibile prima dell'acquisto: e' li' che serve a decidere.
        Assert.Null(node.currentValueText);
        Assert.Equal("+2 oro alla partenza", node.nextValueText);

        talents.BuyTalent(player, new TalentBuyRequest { talentId = "purse-travel-fund" });
        node = Entry(talents.GetTalents(player), "purse-travel-fund");

        Assert.Equal("+2 oro alla partenza", node.currentValueText);
        Assert.Equal("+4 oro alla partenza", node.nextValueText);
    }

    [Fact]
    public void A_maxed_node_has_nothing_left_to_show()
    {
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "testo-al-massimo", points: 50);

        for (int rank = 0; rank < 5; rank++)
            talents.BuyTalent(player, new TalentBuyRequest { talentId = "purse-travel-fund" });

        TalentEntryData node = Entry(talents.GetTalents(player), "purse-travel-fund");

        Assert.Equal("+10 oro alla partenza", node.currentValueText);
        Assert.Null(node.nextValueText);
    }

    [Fact]
    public void The_singular_form_is_used_when_the_value_is_one()
    {
        TalentCatalog.TryGet("occasion-seeker", out TalentCatalog.Talent seeker);

        Assert.Equal("+1 consumabile a bottino", TalentCatalog.FormatValueAtRank(seeker, 1));
        Assert.Equal("+2 consumabili a bottino", TalentCatalog.FormatValueAtRank(seeker, 2));
    }

    [Fact]
    public void Every_branch_can_be_finished()
    {
        // Il costo totale dell'albero non dice niente su quanto se ne puo' comprare: i
        // cancelli di tier si misurano sui punti spesi *in quel ramo*, e un ramo che non ne
        // assorbe abbastanza si mura da solo. E' quello che succedeva con le soglie
        // 5/12/20: le Occasioni si fermavano al primo nodo, perche' il loro unico nodo di
        // tier 1 vale 4 punti contro un cancello da 5, per sempre e con propoli infiniti.
        //
        // Il test compra tutto il comprabile a ripetizione, come farebbe un giocatore che ha
        // deciso di finire un ramo, e pretende che non resti niente indietro.
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "ramo-completo", points: 100_000);

        for (int pass = 0; pass < TalentCatalog.All.Count; pass++)
        {
            foreach (TalentCatalog.Talent talent in TalentCatalog.All)
                talents.BuyTalent(player, new TalentBuyRequest { talentId = talent.Id });
        }

        TalentData data = talents.GetTalents(player);
        foreach (TalentEntryData entry in data.talents)
        {
            Assert.True(
                entry.rank >= entry.maxRank,
                $"{entry.name} ({entry.branch} t{entry.tier}) si ferma a {entry.rank}/{entry.maxRank}: " +
                $"{entry.lockedReason}");
        }
    }

    [Fact]
    public void The_last_tier_still_asks_for_a_committed_branch()
    {
        // Il rovescio del test qui sopra: i cancelli devono restare cancelli. Un capstone
        // comprabile con due spiccioli toglierebbe al ramo l'unica cosa che lo rende una
        // scelta.
        using var server = new TestServer();
        (TalentService talents, AccountIdentity player) = Setup(server, "capstone-caro", points: 100_000);

        foreach (TalentCatalog.Talent talent in TalentCatalog.All)
        {
            if (talent.Tier < 4)
                continue;

            (_, string code, _) = talents.BuyTalent(player, new TalentBuyRequest { talentId = talent.Id });
            Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
        }
    }

    [Fact]
    public void The_whole_tree_costs_what_the_design_says()
    {
        // 104 punti in tutto: si finisce intorno al livello 60-65, cioe' mai per un pezzo
        // lunghissimo. Se questo numero si muove, si e' mosso il ritmo di tutta la
        // progressione, e deve essere una decisione e non un effetto collaterale.
        //
        // Erano 114 fino al ritiro di "Forgia generosa" (5 ranghi da 2 = 10 punti), tolto
        // perche' dava essenze per una forgia che non ha piu' una valuta. L'albero costa
        // meno, non di meno: e' un nodo in meno da comprare, non uno sconto sugli altri.
        int total = 0;
        foreach (TalentCatalog.Talent talent in TalentCatalog.All)
            total += TalentCatalog.FullCostOf(talent);

        Assert.Equal(104, total);
    }

    private static (TalentService Talents, AccountIdentity Player) Setup(
        TestServer server, string username, int points = 0)
    {
        AccountIdentity player = server.RegisterAccount(username);
        // Fa nascere la riga di progressione: e' quello che succede alla connessione vera.
        new SinglePlayerProgressService(server.Database).GetProgress(player);
        if (points > 0)
        {
            server.Execute(
                $"UPDATE single_player_progress SET talent_points = {points}, " +
                $"talent_points_earned = {points} WHERE player_id = '{player.PlayerId}'");
        }
        return (new TalentService(server.Database), player);
    }

    private static TalentEntryData Entry(TalentData data, string id) =>
        Array.Find(data.talents, entry => entry.id == id);

    private static TalentBranchData Branch(TalentData data, string id) =>
        Array.Find(data.branches, branch => branch.id == id);
}
