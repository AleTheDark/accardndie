using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Il rimborso dei nodi ritirati. La proprieta' che conta e' che un giocatore non paghi mai
/// per una nostra decisione di cambiare l'albero: ne' perdendo i propoli spesi, ne'
/// ricevendoli due volte se la migrazione ripassa.
/// </summary>
public sealed class RemovedTalentRefundTests
{
    [Fact]
    public void Points_spent_on_a_retired_node_come_back()
    {
        using var server = new TestServer();
        AccountIdentity player = Setup(server, "rimborso", points: 10);

        // "Slancio" costava 4 propoli ed era l'unico nodo ritirato che si riuscisse davvero a
        // comprare con i vecchi cancelli: la riga simula chi l'aveva preso.
        GrantRank(server, player, "mastery-momentum", rank: 1);
        Spend(server, player, 4);

        int refunded = RemovedTalentRefundMigration.RunIfNeeded(server.Database);

        Assert.Equal(1, refunded);
        Assert.Equal(10, Points(server, player));
    }

    [Fact]
    public void A_multi_rank_node_gives_back_every_rank()
    {
        using var server = new TestServer();
        AccountIdentity player = Setup(server, "rimborso-ranghi", points: 0);

        // "Veterano": 2 ranghi da 4.
        GrantRank(server, player, "mastery-veteran", rank: 2);

        RemovedTalentRefundMigration.RunIfNeeded(server.Database);

        Assert.Equal(8, Points(server, player));
    }

    [Fact]
    public void The_refund_does_not_inflate_the_points_ever_earned()
    {
        using var server = new TestServer();
        AccountIdentity player = Setup(server, "totale-guadagnato", points: 0, earned: 20);
        GrantRank(server, player, "mastery-summit", rank: 1);

        RemovedTalentRefundMigration.RunIfNeeded(server.Database);

        // Gli 8 propoli tornano spendibili, ma erano gia' stati guadagnati: contarli di nuovo
        // nel totale li farebbe valere due volte in ogni statistica che lo legge.
        Assert.Equal(8, Points(server, player));
        Assert.Equal(20, Earned(server, player));
    }

    [Fact]
    public void Running_it_twice_pays_once()
    {
        using var server = new TestServer();
        AccountIdentity player = Setup(server, "niente-bis", points: 0);
        GrantRank(server, player, "initiative-first-strike", rank: 1);

        RemovedTalentRefundMigration.RunIfNeeded(server.Database);
        RemovedTalentRefundMigration.RunIfNeeded(server.Database);

        Assert.Equal(8, Points(server, player));
    }

    [Fact]
    public void Even_a_reset_key_cannot_pay_twice()
    {
        using var server = new TestServer();
        AccountIdentity player = Setup(server, "chiave-rimessa", points: 0);
        GrantRank(server, player, "mastery-momentum", rank: 1);

        RemovedTalentRefundMigration.RunIfNeeded(server.Database);
        // Qualcuno rimette la chiave a mano: le righe pero' non ci sono piu', quindi non c'e'
        // piu' niente da rimborsare. E' la seconda rete oltre a server_settings.
        server.Execute("DELETE FROM server_settings WHERE key = 'migration.talents.refund-removed-1'");
        RemovedTalentRefundMigration.RunIfNeeded(server.Database);

        Assert.Equal(4, Points(server, player));
    }

    [Fact]
    public void Nodes_still_in_the_catalogue_are_left_alone()
    {
        using var server = new TestServer();
        AccountIdentity player = Setup(server, "nodi-vivi", points: 0);
        GrantRank(server, player, "purse-travel-fund", rank: 3);

        RemovedTalentRefundMigration.RunIfNeeded(server.Database);

        Assert.Equal(0, Points(server, player));
        TalentData data = new TalentService(server.Database).GetTalents(player);
        Assert.Equal(3, Array.Find(data.talents, entry => entry.id == "purse-travel-fund").rank);
    }

    [Fact]
    public void Every_retired_node_is_really_gone_from_the_catalogue()
    {
        // La tabella dei ritirati e il catalogo non devono mai sovrapporsi: un id in
        // entrambi rimborserebbe un nodo che si puo' ancora comprare, cioe' propoli gratis a
        // ogni riavvio del server per chiunque lo possieda.
        foreach (string talentId in new[]
        {
            "mastery-momentum", "mastery-veteran", "mastery-summit", "initiative-first-strike"
        })
        {
            Assert.False(TalentCatalog.TryGet(talentId, out _), talentId);
        }
    }

    private static AccountIdentity Setup(
        TestServer server, string username, int points, int earned = 0)
    {
        AccountIdentity player = server.RegisterAccount(username);
        new SinglePlayerProgressService(server.Database).GetProgress(player);
        server.Execute(
            $"UPDATE single_player_progress SET talent_points = {points}, " +
            $"talent_points_earned = {earned} WHERE player_id = '{player.PlayerId}'");
        return player;
    }

    private static void GrantRank(TestServer server, AccountIdentity player, string talentId, int rank) =>
        server.Execute(
            "INSERT INTO player_talents (player_id, talent_id, rank, updated_at) " +
            $"VALUES ('{player.PlayerId}', '{talentId}', {rank}, '{DateTime.UtcNow:O}')");

    private static void Spend(TestServer server, AccountIdentity player, int points) =>
        server.Execute(
            $"UPDATE single_player_progress SET talent_points = talent_points - {points} " +
            $"WHERE player_id = '{player.PlayerId}'");

    private static int Points(TestServer server, AccountIdentity player) =>
        new TalentService(server.Database).GetTalents(player).talentPoints;

    private static int Earned(TestServer server, AccountIdentity player) =>
        new TalentService(server.Database).GetTalents(player).talentPointsEarned;
}
