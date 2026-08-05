using AccardND.Server.Accounts;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La cancellazione account richiesta dall'utente su /account/delete. Quello che
/// conta davvero e' che dopo la conferma non resti nessuna riga con quel
/// player_id: e' la promessa fatta a Google Play e all'utente.
/// </summary>
public sealed class AccountDeletionTests
{
    private const string Email = "giocatore@example.com";

    [Fact]
    public void Confirmed_deletion_removes_the_account_and_every_trace()
    {
        using var server = new TestServer();
        AccountDeletionService deletion = CreateService(server);

        AccountIdentity player = server.RegisterAccount("dacancellare");
        LinkGoogleIdentity(server, player.PlayerId, Email);
        GiveProgress(server, player.PlayerId);

        PreparedDeletion prepared = deletion.PrepareForVerifiedEmail(Email);
        Assert.Null(prepared.Error);
        DeletableAccount found = Assert.Single(prepared.Accounts);
        Assert.Equal(player.PlayerId, found.PlayerId);

        (bool ok, string error, int deleted) = deletion.Confirm(prepared.Ticket);
        Assert.True(ok, error);
        Assert.Equal(1, deleted);

        Assert.Equal(0, CountRows(server, "accounts", player.PlayerId));
        Assert.Equal(0, CountRows(server, "external_identities", player.PlayerId));
        Assert.Equal(0, CountRows(server, "account_nicknames", player.PlayerId));
        Assert.Equal(0, CountRows(server, "single_player_progress", player.PlayerId));
        Assert.Equal(0, CountRows(server, "login_events", player.PlayerId));
    }

    [Fact]
    public void A_ticket_works_once_and_only_for_its_own_account()
    {
        using var server = new TestServer();
        AccountDeletionService deletion = CreateService(server);

        AccountIdentity mine = server.RegisterAccount("miaccount");
        AccountIdentity other = server.RegisterAccount("altrui");
        LinkGoogleIdentity(server, mine.PlayerId, Email);
        LinkGoogleIdentity(server, other.PlayerId, "estraneo@example.com");

        PreparedDeletion prepared = deletion.PrepareForVerifiedEmail(Email);
        Assert.Single(prepared.Accounts);

        Assert.True(deletion.Confirm(prepared.Ticket).ok);

        // Rigiocare lo stesso ticket non deve cancellare nient'altro.
        (bool replayed, _, _) = deletion.Confirm(prepared.Ticket);
        Assert.False(replayed);

        // L'account di un'altra email non e' stato toccato.
        Assert.Equal(1, CountRows(server, "accounts", other.PlayerId));
    }

    [Fact]
    public void An_email_without_accounts_is_reported_without_a_ticket()
    {
        using var server = new TestServer();
        AccountDeletionService deletion = CreateService(server);

        PreparedDeletion prepared = deletion.PrepareForVerifiedEmail("nessuno@example.com");

        Assert.Null(prepared.Error);
        Assert.Empty(prepared.Accounts);
        Assert.Null(prepared.Ticket);
    }

    [Fact]
    public void An_unknown_ticket_is_refused()
    {
        using var server = new TestServer();
        AccountDeletionService deletion = CreateService(server);

        (bool ok, string error, int deleted) = deletion.Confirm("ticket-inventato");

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(0, deleted);
    }

    // ---- Helper ---------------------------------------------------------------

    private static AccountDeletionService CreateService(TestServer server) =>
        new(server.Database,
            new GoogleIdTokenReader(server.Config, NullLogger<GoogleIdTokenReader>.Instance),
            new AccountEraser(server.Database),
            NullLogger<AccountDeletionService>.Instance);

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

    private static void GiveProgress(TestServer server, string playerId)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO single_player_progress
                (player_id, honey, account_level, account_experience, account_total_experience,
                 account_experience_to_next_level, tutorial_completed, hardcore_unlocked, updated_at)
            VALUES ($player, 120, 4, 30, 430, 100, 1, 0, $now)";
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static int CountRows(TestServer server, string table, string playerId)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE player_id = $player";
        command.Parameters.AddWithValue("$player", playerId);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
