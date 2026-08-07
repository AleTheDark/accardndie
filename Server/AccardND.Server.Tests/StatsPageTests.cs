using AccardND.Server.Accounts;
using AccardND.Server.Web;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La pagina pubblica delle statistiche (/statistiche). Non c'e' niente da
/// scrivere, quindi l'unica cosa che puo' andare male e' anche l'unica che conta:
/// far vedere a qualcuno i dati di qualcun altro.
/// </summary>
public sealed class StatsPageTests
{
    private const string Email = "giocatore@example.com";

    [Fact]
    public void An_email_only_reaches_the_accounts_linked_to_it()
    {
        using var server = new TestServer();

        AccountIdentity mine = server.RegisterAccount("miaccount");
        AccountIdentity alsoMine = server.RegisterAccount("secondoprofilo");
        AccountIdentity stranger = server.RegisterAccount("estraneo");
        LinkGoogleIdentity(server, mine.PlayerId, Email);
        LinkGoogleIdentity(server, alsoMine.PlayerId, Email);
        LinkGoogleIdentity(server, stranger.PlayerId, "altro@example.com");

        IReadOnlyList<LinkedAccount> found =
            LinkedAccounts.FindByVerifiedEmail(server.Database, Email);

        Assert.Equal(
            new[] { mine.PlayerId, alsoMine.PlayerId }.OrderBy(id => id),
            found.Select(account => account.PlayerId).OrderBy(id => id));
    }

    [Fact]
    public void A_session_can_only_read_its_own_accounts()
    {
        var store = new WebSessionStore();
        string token = store.Create(Email, new[] { "player-mio", "player-anche-mio" });

        WebSession session = store.TryGet(token);

        Assert.NotNull(session);
        Assert.True(session.CanRead("player-mio"));
        Assert.True(session.CanRead("player-anche-mio"));

        // E' il caso che protegge la pagina: /statistiche?account=<player_id altrui>.
        Assert.False(session.CanRead("player-di-un-altro"));
        Assert.False(session.CanRead(null));
        Assert.False(session.CanRead(""));
    }

    [Fact]
    public void An_unknown_or_closed_session_gives_nothing()
    {
        var store = new WebSessionStore();
        string token = store.Create(Email, new[] { "player-mio" });

        Assert.Null(store.TryGet("token-inventato"));
        Assert.Null(store.TryGet(null));

        store.Remove(token);
        Assert.Null(store.TryGet(token));
    }

    /// <summary>Simula il login Google: e' la riga che lega un'email a un player_id.</summary>
    private static void LinkGoogleIdentity(TestServer server, string playerId, string email)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO external_identities
                (provider, external_id, player_id, auth_method, email, created_at, last_login_at)
            VALUES ('ugs', $external, $player, 'google', $email, $now, $now)";
        command.Parameters.AddWithValue("$external", "ugs-" + playerId);
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$email", email);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
