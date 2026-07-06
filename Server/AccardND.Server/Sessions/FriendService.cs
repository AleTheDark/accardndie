using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Sessions;

/// <summary>Esito di un'azione amico: eventuale errore e l'altro giocatore coinvolto (per il push).</summary>
public sealed record FriendActionResult(bool Ok, string Error, string OtherPlayerId)
{
    public static FriendActionResult Fail(string error) => new(false, error, null);
    public static FriendActionResult Success(string otherPlayerId) => new(true, null, otherPlayerId);
}

/// <summary>
/// Amicizie su modello a righe speculari (una riga per prospettiva). L'invio dei
/// messaggi resta al MessageRouter; qui vivono solo le operazioni sul database.
/// </summary>
public sealed class FriendService
{
    private readonly AccardDatabase database;
    private readonly PresenceRegistry presence;

    public FriendService(AccardDatabase database, PresenceRegistry presence)
    {
        this.database = database;
        this.presence = presence;
    }

    public FriendActionResult SendRequest(AccountIdentity from, string username)
    {
        username = username?.Trim() ?? string.Empty;
        if (username.Length == 0)
            return FriendActionResult.Fail("Nome utente mancante.");

        using SqliteConnection connection = database.Open();
        (string targetId, string error) = ResolveByUsername(connection, username);
        if (targetId == null)
            return FriendActionResult.Fail(error);
        if (targetId == from.PlayerId)
            return FriendActionResult.Fail("Non puoi aggiungere te stesso.");

        string existing = ReadStatus(connection, from.PlayerId, targetId);
        if (existing == "accepted")
            return FriendActionResult.Fail("Siete già amici.");
        if (existing == "requested")
            return FriendActionResult.Fail("Richiesta già inviata.");
        if (existing == "blocked")
            return FriendActionResult.Fail("Hai bloccato questo giocatore.");
        if (ReadStatus(connection, targetId, from.PlayerId) == "blocked")
            return FriendActionResult.Fail("Impossibile inviare la richiesta.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        // Se l'altro mi aveva già richiesto (incoming per me), la richiesta reciproca diventa amicizia.
        if (existing == "incoming")
        {
            SetStatus(connection, transaction, from.PlayerId, targetId, "accepted");
            SetStatus(connection, transaction, targetId, from.PlayerId, "accepted");
        }
        else
        {
            SetStatus(connection, transaction, from.PlayerId, targetId, "requested");
            SetStatus(connection, transaction, targetId, from.PlayerId, "incoming");
        }
        transaction.Commit();
        return FriendActionResult.Success(targetId);
    }

    public FriendActionResult Respond(AccountIdentity identity, string otherId, bool accept)
    {
        if (string.IsNullOrEmpty(otherId))
            return FriendActionResult.Fail("Giocatore non valido.");

        using SqliteConnection connection = database.Open();
        if (ReadStatus(connection, identity.PlayerId, otherId) != "incoming")
            return FriendActionResult.Fail("Nessuna richiesta da accettare.");

        using SqliteTransaction transaction = connection.BeginTransaction();
        if (accept)
        {
            SetStatus(connection, transaction, identity.PlayerId, otherId, "accepted");
            SetStatus(connection, transaction, otherId, identity.PlayerId, "accepted");
        }
        else
        {
            DeletePair(connection, transaction, identity.PlayerId, otherId);
        }
        transaction.Commit();
        return FriendActionResult.Success(otherId);
    }

    public FriendActionResult Remove(AccountIdentity identity, string otherId)
    {
        if (string.IsNullOrEmpty(otherId))
            return FriendActionResult.Fail("Giocatore non valido.");
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        DeletePair(connection, transaction, identity.PlayerId, otherId);
        transaction.Commit();
        return FriendActionResult.Success(otherId);
    }

    public FriendActionResult Block(AccountIdentity identity, string otherId)
    {
        if (string.IsNullOrEmpty(otherId) || otherId == identity.PlayerId)
            return FriendActionResult.Fail("Giocatore non valido.");
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        SetStatus(connection, transaction, identity.PlayerId, otherId, "blocked");
        // L'altro non mi vede più tra i suoi contatti.
        using (SqliteCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM friends WHERE owner_id=$other AND other_id=$me";
            delete.Parameters.AddWithValue("$other", otherId);
            delete.Parameters.AddWithValue("$me", identity.PlayerId);
            delete.ExecuteNonQuery();
        }
        transaction.Commit();
        return FriendActionResult.Success(otherId);
    }

    /// <summary>Lista amici del giocatore, con presenza calcolata al momento.</summary>
    public FriendsData GetFriends(AccountIdentity identity)
    {
        var friends = new List<FriendDto>();
        using SqliteConnection connection = database.Open();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT f.other_id, COALESCE(a.username, ''), f.status
            FROM friends f
            LEFT JOIN accounts a ON a.player_id = f.other_id
            WHERE f.owner_id=$me AND f.status <> 'blocked'
            ORDER BY f.status, a.username";
        query.Parameters.AddWithValue("$me", identity.PlayerId);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            string otherId = reader.GetString(0);
            friends.Add(new FriendDto
            {
                playerId = otherId,
                username = reader.GetString(1),
                status = reader.GetString(2),
                presence = presence.GetPresence(otherId)
            });
        }
        return new FriendsData { friends = friends.ToArray() };
    }

    /// <summary>Id degli amici confermati (per notificare la presenza).</summary>
    public IReadOnlyList<string> GetAcceptedFriendIds(string playerId)
    {
        var ids = new List<string>();
        using SqliteConnection connection = database.Open();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT other_id FROM friends WHERE owner_id=$me AND status='accepted'";
        query.Parameters.AddWithValue("$me", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetString(0));
        return ids;
    }

    public bool AreFriends(string a, string b)
    {
        using SqliteConnection connection = database.Open();
        return ReadStatus(connection, a, b) == "accepted";
    }

    private static (string PlayerId, string Error) ResolveByUsername(SqliteConnection connection, string username)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT player_id FROM accounts WHERE username_ci=$ci LIMIT 2";
        query.Parameters.AddWithValue("$ci", username.ToLowerInvariant());
        using SqliteDataReader reader = query.ExecuteReader();
        if (!reader.Read())
            return (null, "Giocatore non trovato.");
        string playerId = reader.GetString(0);
        if (reader.Read())
            return (null, "Nome ambiguo: più giocatori con questo nome.");
        return (playerId, null);
    }

    private static string ReadStatus(SqliteConnection connection, string ownerId, string otherId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT status FROM friends WHERE owner_id=$owner AND other_id=$other";
        query.Parameters.AddWithValue("$owner", ownerId);
        query.Parameters.AddWithValue("$other", otherId);
        return query.ExecuteScalar() as string;
    }

    private static void SetStatus(
        SqliteConnection connection, SqliteTransaction transaction,
        string ownerId, string otherId, string status)
    {
        using SqliteCommand upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = @"
            INSERT INTO friends (owner_id, other_id, status, created_at, updated_at)
            VALUES ($owner, $other, $status, $now, $now)
            ON CONFLICT(owner_id, other_id) DO UPDATE SET status=$status, updated_at=$now";
        upsert.Parameters.AddWithValue("$owner", ownerId);
        upsert.Parameters.AddWithValue("$other", otherId);
        upsert.Parameters.AddWithValue("$status", status);
        upsert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        upsert.ExecuteNonQuery();
    }

    private static void DeletePair(
        SqliteConnection connection, SqliteTransaction transaction, string a, string b)
    {
        using SqliteCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = @"
            DELETE FROM friends
            WHERE (owner_id=$a AND other_id=$b) OR (owner_id=$b AND other_id=$a)";
        delete.Parameters.AddWithValue("$a", a);
        delete.Parameters.AddWithValue("$b", b);
        delete.ExecuteNonQuery();
    }
}
