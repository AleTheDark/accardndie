using System.Security.Cryptography;
using System.Text.Json;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Accounts;

public sealed record AccountIdentity(string PlayerId, string Username);
public sealed record ExternalAccountResult(
    AccountIdentity Identity, bool IsNewAccount, bool RequiresNickname, string Error);
public sealed record NicknameChangeResult(AccountIdentity Identity, string Error);

public sealed class AccountService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Pbkdf2Iterations = 100_000;
    private const int SqliteConstraintViolation = 19;

    private readonly AccardDatabase database;
    private readonly object tokenGate = new();
    private readonly Dictionary<string, AccountIdentity> identitiesByToken = new();

    public AccountService(AccardDatabase database, ServerConfig config, ILogger<AccountService> logger)
    {
        this.database = database;
        MigrateLegacyJson(config, logger);
    }

    public (AccountIdentity Identity, string Token, string Error) Register(string username, string password)
    {
        username = username?.Trim() ?? string.Empty;
        if (username.Length is < 3 or > 20)
            return (null, null, "Il nome utente deve avere 3-20 caratteri.");
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            return (null, null, "La password deve avere almeno 6 caratteri.");

        string usernameCi = username.ToLowerInvariant();
        using SqliteConnection connection = database.Open();

        if (UsernameExists(connection, usernameCi))
            return (null, null, "Nome utente già in uso.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = HashPassword(password, salt);
        string playerId = Guid.NewGuid().ToString("N");
        string now = DateTime.UtcNow.ToString("O");

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText = @"
            INSERT INTO accounts
                (player_id, source, username, username_ci, password_salt, password_hash, created_at, last_login_at)
            VALUES ($id, 'password', $username, $ci, $salt, $hash, $now, $now)";
        insert.Parameters.AddWithValue("$id", playerId);
        insert.Parameters.AddWithValue("$username", username);
        insert.Parameters.AddWithValue("$ci", usernameCi);
        insert.Parameters.AddWithValue("$salt", Convert.ToBase64String(salt));
        insert.Parameters.AddWithValue("$hash", Convert.ToBase64String(hash));
        insert.Parameters.AddWithValue("$now", now);

        try
        {
            insert.ExecuteNonQuery();
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteConstraintViolation)
        {
            // Corsa tra due registrazioni con lo stesso nome.
            return (null, null, "Nome utente già in uso.");
        }

        SetNickname(new AccountIdentity(playerId, username), username);
        return IssueSession(new AccountIdentity(playerId, username));
    }

    public (AccountIdentity Identity, string Token, string Error) Login(string username, string password)
    {
        username = username?.Trim() ?? string.Empty;
        string usernameCi = username.ToLowerInvariant();
        using SqliteConnection connection = database.Open();

        string playerId;
        string storedUsername;
        string salt;
        string hash;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText = @"
                SELECT player_id, username, password_salt, password_hash
                FROM accounts
                WHERE source='password' AND username_ci=$ci
                LIMIT 1";
            query.Parameters.AddWithValue("$ci", usernameCi);
            using SqliteDataReader reader = query.ExecuteReader();
            if (!reader.Read())
                return (null, null, "Credenziali non valide.");
            playerId = reader.GetString(0);
            storedUsername = reader.GetString(1);
            salt = reader.GetString(2);
            hash = reader.GetString(3);
        }

        byte[] expected = Convert.FromBase64String(hash);
        byte[] actual = HashPassword(password ?? string.Empty, Convert.FromBase64String(salt));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            return (null, null, "Credenziali non valide.");

        TouchLastLogin(connection, playerId);
        return IssueSession(new AccountIdentity(playerId, storedUsername));
    }

    /// <summary>
    /// Crea o aggiorna la riga di un account esterno (UGS) così che profili,
    /// statistiche e rank possano agganciarsi al suo player_id.
    /// </summary>
    public ExternalAccountResult ResolveExternalAccount(VerifiedExternalIdentity external)
    {
        if (external == null || string.IsNullOrWhiteSpace(external.Provider)
            || string.IsNullOrWhiteSpace(external.ExternalId))
            return new ExternalAccountResult(null, false, false, "Identita esterna non valida.");

        string now = DateTime.UtcNow.ToString("O");
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        string playerId = FindExternalAccount(
            connection, transaction, external.Provider, external.ExternalId);
        bool isNew = false;
        if (playerId == null)
        {
            string legacyId = external.Provider == "ugs" ? $"ugs:{external.ExternalId}" : null;
            playerId = AccountExists(connection, transaction, legacyId)
                ? legacyId
                : Guid.NewGuid().ToString("N");

            if (playerId != legacyId)
            {
                using SqliteCommand create = connection.CreateCommand();
                create.Transaction = transaction;
                create.CommandText = @"
                    INSERT INTO accounts
                        (player_id, source, username, username_ci, created_at, last_login_at)
                    VALUES ($id, 'external', $username, $ci, $now, $now)";
                create.Parameters.AddWithValue("$id", playerId);
                create.Parameters.AddWithValue("$username", external.DisplayName);
                create.Parameters.AddWithValue("$ci", external.DisplayName.ToLowerInvariant());
                create.Parameters.AddWithValue("$now", now);
                create.ExecuteNonQuery();
                isNew = true;
            }

            using SqliteCommand link = connection.CreateCommand();
            link.Transaction = transaction;
            link.CommandText = @"
                INSERT INTO external_identities
                    (provider, external_id, player_id, created_at, last_login_at)
                VALUES ($provider, $external, $player, $now, $now)";
            link.Parameters.AddWithValue("$provider", external.Provider);
            link.Parameters.AddWithValue("$external", external.ExternalId);
            link.Parameters.AddWithValue("$player", playerId);
            link.Parameters.AddWithValue("$now", now);
            link.ExecuteNonQuery();
        }

        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE accounts SET last_login_at=$now WHERE player_id=$id;
                UPDATE external_identities SET last_login_at=$now
                WHERE provider=$provider AND external_id=$external;";
            update.Parameters.AddWithValue("$now", now);
            update.Parameters.AddWithValue("$id", playerId);
            update.Parameters.AddWithValue("$provider", external.Provider);
            update.Parameters.AddWithValue("$external", external.ExternalId);
            update.ExecuteNonQuery();
        }

        string username;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = "SELECT username FROM accounts WHERE player_id=$id";
            query.Parameters.AddWithValue("$id", playerId);
            username = Convert.ToString(query.ExecuteScalar());
        }

        transaction.Commit();
        bool requiresNickname = !HasNickname(connection, playerId);
        return new ExternalAccountResult(
            new AccountIdentity(playerId, username), isNew, requiresNickname, null);
    }

    public NicknameChangeResult SetNickname(AccountIdentity current, string nickname)
    {
        if (current == null)
            return new NicknameChangeResult(null, "Account non autenticato.");

        nickname = nickname?.Trim() ?? string.Empty;
        if (nickname.Length is < 3 or > 18)
            return new NicknameChangeResult(null, "Il nickname deve avere 3-18 caratteri.");
        if (nickname.Any(character =>
                !char.IsLetterOrDigit(character) && character != '_' && character != '-'))
            return new NicknameChangeResult(
                null, "Usa solo lettere, numeri, trattino e underscore.");

        string nicknameCi = nickname.ToLowerInvariant();
        if (nicknameCi is "admin" or "administrator" or "moderator" or "support" or "system")
            return new NicknameChangeResult(null, "Questo nickname e riservato.");

        string now = DateTime.UtcNow.ToString("O");
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        try
        {
            using (SqliteCommand upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = @"
                    INSERT INTO account_nicknames
                        (player_id, nickname, nickname_ci, updated_at)
                    VALUES ($id, $nickname, $ci, $now)
                    ON CONFLICT(player_id) DO UPDATE SET
                        nickname=excluded.nickname,
                        nickname_ci=excluded.nickname_ci,
                        updated_at=excluded.updated_at";
                upsert.Parameters.AddWithValue("$id", current.PlayerId);
                upsert.Parameters.AddWithValue("$nickname", nickname);
                upsert.Parameters.AddWithValue("$ci", nicknameCi);
                upsert.Parameters.AddWithValue("$now", now);
                upsert.ExecuteNonQuery();
            }

            using (SqliteCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = @"
                    UPDATE accounts SET username=$nickname, username_ci=$ci
                    WHERE player_id=$id";
                update.Parameters.AddWithValue("$id", current.PlayerId);
                update.Parameters.AddWithValue("$nickname", nickname);
                update.Parameters.AddWithValue("$ci", nicknameCi);
                update.ExecuteNonQuery();
            }

            transaction.Commit();
            return new NicknameChangeResult(new AccountIdentity(current.PlayerId, nickname), null);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == SqliteConstraintViolation)
        {
            return new NicknameChangeResult(null, "Nickname gia in uso.");
        }
    }

    private static bool HasNickname(SqliteConnection connection, string playerId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT 1 FROM account_nicknames WHERE player_id=$id LIMIT 1";
        query.Parameters.AddWithValue("$id", playerId);
        return query.ExecuteScalar() != null;
    }

    private static string FindExternalAccount(
        SqliteConnection connection, SqliteTransaction transaction, string provider, string externalId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = @"
            SELECT player_id FROM external_identities
            WHERE provider=$provider AND external_id=$external LIMIT 1";
        query.Parameters.AddWithValue("$provider", provider);
        query.Parameters.AddWithValue("$external", externalId);
        return query.ExecuteScalar() as string;
    }

    private static bool AccountExists(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return false;
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT 1 FROM accounts WHERE player_id=$id LIMIT 1";
        query.Parameters.AddWithValue("$id", playerId);
        return query.ExecuteScalar() != null;
    }

    public AccountIdentity ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        lock (tokenGate)
            return identitiesByToken.GetValueOrDefault(token);
    }

    private static bool UsernameExists(SqliteConnection connection, string usernameCi)
    {
        using SqliteCommand check = connection.CreateCommand();
        check.CommandText =
            "SELECT 1 FROM accounts WHERE source='password' AND username_ci=$ci LIMIT 1";
        check.Parameters.AddWithValue("$ci", usernameCi);
        return check.ExecuteScalar() != null;
    }

    private static void TouchLastLogin(SqliteConnection connection, string playerId)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE accounts SET last_login_at=$now WHERE player_id=$id";
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$id", playerId);
        update.ExecuteNonQuery();
    }

    private (AccountIdentity, string, string) IssueSession(AccountIdentity identity)
    {
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        lock (tokenGate)
            identitiesByToken[token] = identity;
        return (identity, token, null);
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);

    /// <summary>
    /// Importa gli account dal vecchio accounts.json la prima volta (tabella vuota).
    /// Idempotente: se esistono già account password non fa nulla.
    /// </summary>
    private void MigrateLegacyJson(ServerConfig config, ILogger logger)
    {
        string path = config.AccountsFilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return;

        using SqliteConnection connection = database.Open();
        using (SqliteCommand count = connection.CreateCommand())
        {
            count.CommandText = "SELECT COUNT(*) FROM accounts WHERE source='password'";
            if (Convert.ToInt64(count.ExecuteScalar()) > 0)
                return;
        }

        List<LegacyAccount> stored;
        try
        {
            stored = JsonSerializer.Deserialize<List<LegacyAccount>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "accounts.json illeggibile, migrazione saltata.");
            return;
        }

        if (stored == null || stored.Count == 0)
            return;

        string now = DateTime.UtcNow.ToString("O");
        using SqliteTransaction transaction = connection.BeginTransaction();
        int imported = 0;
        foreach (LegacyAccount account in stored)
        {
            if (string.IsNullOrEmpty(account.PlayerId) || string.IsNullOrEmpty(account.Username))
                continue;
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR IGNORE INTO accounts
                    (player_id, source, username, username_ci, password_salt, password_hash, created_at, last_login_at)
                VALUES ($id, 'password', $username, $ci, $salt, $hash, $now, NULL)";
            insert.Parameters.AddWithValue("$id", account.PlayerId);
            insert.Parameters.AddWithValue("$username", account.Username);
            insert.Parameters.AddWithValue("$ci", account.Username.ToLowerInvariant());
            insert.Parameters.AddWithValue("$salt", account.Salt ?? string.Empty);
            insert.Parameters.AddWithValue("$hash", account.Hash ?? string.Empty);
            insert.Parameters.AddWithValue("$now", now);
            imported += insert.ExecuteNonQuery();
        }
        transaction.Commit();
        logger.LogInformation("Migrati {Count} account da {Path} a SQLite.", imported, path);
    }

    private sealed record LegacyAccount(string PlayerId, string Username, string Salt, string Hash);
}
