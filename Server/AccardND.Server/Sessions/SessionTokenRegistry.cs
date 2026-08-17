using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Sessions;

/// <summary>
/// Token di sessione emessi al login e rigiocabili su auth.session per riagganciare
/// una connessione caduta. Serve a non ripassare da Google/UGS a ogni blip di rete:
/// quel giro è lento e fallisce proprio quando la rete è instabile.
///
/// Vivono in memoria e su disco insieme: la memoria è la strada veloce, il database
/// è quello che sopravvive a un riavvio. Tenerli solo nel processo significava che
/// ogni deploy sbatteva fuori tutti i collegati, e chi in quel momento aveva l'app in
/// secondo piano non poteva nemmeno rientrare da solo (il rientro automatico con
/// login Google è disattivato in pausa, di proposito): trovava il badge "sessione
/// scaduta" e doveva rifare l'accesso a mano.
///
/// Il token è un bearer, quindi è casuale a 256 bit, scade, e su disco ne finisce
/// solo l'impronta: un database che finisse nelle mani sbagliate non deve contenere
/// niente di rigiocabile.
/// </summary>
public sealed class SessionTokenRegistry
{
    private sealed record Entry(AccountIdentity Identity, DateTime ExpiresAtUtc);

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);

    /// <summary>
    /// Pietre tombali dei token revocati perché l'account è entrato altrove.
    /// Togliere il token non basta: il client sloggato può non aver mai letto
    /// l'avviso (app Android in pausa, scheda del browser in secondo piano: lì il
    /// loop di gioco è fermo e i messaggi restano in coda) e al risveglio prova a
    /// riagganciarsi. Trovando un token "sconosciuto" ripiegherebbe sul login
    /// Google, rientrerebbe come sessione nuova e sbatterebbe fuori il dispositivo
    /// che sta giocando adesso — che a sua volta rifarebbe lo stesso, all'infinito.
    /// Ricordandoci la revoca possiamo invece rispondergli "sei stato sostituito",
    /// e quello è un rifiuto su cui si ferma.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> superseded = new(StringComparer.Ordinal);

    private readonly AccardDatabase database;
    private readonly ILogger<SessionTokenRegistry> logger;

    /// <summary>
    /// Senza database resta un registro di sola memoria: è il comportamento che
    /// serve ai test che non hanno niente da far sopravvivere a un riavvio.
    /// </summary>
    public SessionTokenRegistry(AccardDatabase database = null, ILogger<SessionTokenRegistry> logger = null)
    {
        this.database = database;
        this.logger = logger;
    }

    /// <summary>Emette un token per l'identità appena autenticata.</summary>
    public string Issue(AccountIdentity identity)
    {
        if (identity == null)
            return null;

        Prune();
        string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        DateTime expiresAt = DateTime.UtcNow.Add(Lifetime);
        entries[token] = new Entry(identity, expiresAt);
        Persist(token, identity, expiresAt);
        return token;
    }

    /// <summary>
    /// Risolve un token. Il rinnovo è scorrevole: finché il giocatore si riconnette
    /// il token resta valido, così una sessione lunga non decade a metà partita.
    /// </summary>
    public AccountIdentity Resolve(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        // In memoria non c'è: o è di un altro processo (deploy appena fatto) o non
        // esiste. Il disco ha la risposta, e se ce l'ha la si riporta in memoria.
        if (!entries.TryGetValue(token, out Entry entry))
            entry = LoadFromDisk(token);
        if (entry == null)
            return null;

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            entries.TryRemove(token, out _);
            Forget(token);
            return null;
        }

        DateTime renewed = DateTime.UtcNow.Add(Lifetime);
        entries[token] = entry with { ExpiresAtUtc = renewed };
        Renew(token, renewed);
        return entry.Identity;
    }

    /// <summary>
    /// Butta via un token: serve quando una sessione viene chiusa perché l'account
    /// è entrato altrove. Senza, il client sloggato potrebbe rientrare da solo con
    /// la riconnessione automatica e sbattere fuori a sua volta il dispositivo
    /// nuovo, in un rimpallo senza fine.
    /// </summary>
    public void Revoke(string token)
    {
        if (string.IsNullOrEmpty(token))
            return;
        entries.TryRemove(token, out _);
        // Il ricordo dura quanto sarebbe durato il token: oltre, un riaggancio
        // sarebbe comunque scaduto e "rifai l'accesso" è la risposta giusta.
        DateTime rememberUntil = DateTime.UtcNow.Add(Lifetime);
        superseded[token] = rememberUntil;
        MarkSupersededOnDisk(token, rememberUntil);
    }

    /// <summary>
    /// true se questo token era valido ed è stato revocato perché l'account è
    /// entrato da un altro dispositivo. Un token mai emesso o semplicemente
    /// scaduto non conta: quello merita un login nuovo, non l'avviso di
    /// sloggatura.
    /// </summary>
    public bool WasSuperseded(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        if (!superseded.TryGetValue(token, out DateTime rememberUntil))
        {
            // Anche la sostituzione deve sopravvivere al riavvio: è proprio dopo un
            // deploy che il client rimasto indietro riprova col token vecchio.
            rememberUntil = LoadSupersededFromDisk(token);
            if (rememberUntil == default)
                return false;
            superseded[token] = rememberUntil;
        }

        if (rememberUntil > DateTime.UtcNow)
            return true;

        superseded.TryRemove(token, out _);
        Forget(token);
        return false;
    }

    private void Prune()
    {
        DateTime now = DateTime.UtcNow;
        foreach (KeyValuePair<string, Entry> pair in entries)
        {
            if (pair.Value.ExpiresAtUtc <= now)
                entries.TryRemove(pair.Key, out _);
        }

        foreach (KeyValuePair<string, DateTime> pair in superseded)
        {
            if (pair.Value <= now)
                superseded.TryRemove(pair.Key, out _);
        }

        Execute(
            "DELETE FROM session_tokens WHERE expires_at <= $now",
            command => command.Parameters.AddWithValue("$now", Timestamp(now)));
    }

    // --- Disco ---
    //
    // Nessuna di queste operazioni può far fallire un login: se il database non
    // risponde si perde la sopravvivenza al riavvio, non la sessione in corso.

    /// <summary>
    /// L'impronta con cui il token viaggia su disco. SHA-256 basta: il token è già
    /// 256 bit di casualità, non c'è niente da indovinare e niente da salare.
    /// </summary>
    private static string Fingerprint(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Timestamp(DateTime moment) => moment.ToString("O");

    /// <summary>Rilegge un istante scritto in "O" restituendolo sempre in UTC.</summary>
    private static DateTime ReadTimestamp(string stored) =>
        DateTime.Parse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ToUniversalTime();

    private void Persist(string token, AccountIdentity identity, DateTime expiresAt) =>
        Execute(
            @"INSERT INTO session_tokens (token_hash, player_id, username, expires_at, superseded_at)
              VALUES ($hash, $player, $username, $expires, NULL)
              ON CONFLICT (token_hash) DO UPDATE SET
                  player_id = excluded.player_id,
                  username = excluded.username,
                  expires_at = excluded.expires_at,
                  superseded_at = NULL",
            command =>
            {
                command.Parameters.AddWithValue("$hash", Fingerprint(token));
                command.Parameters.AddWithValue("$player", identity.PlayerId);
                command.Parameters.AddWithValue("$username", identity.Username ?? string.Empty);
                command.Parameters.AddWithValue("$expires", Timestamp(expiresAt));
            });

    private void Renew(string token, DateTime expiresAt) =>
        Execute(
            "UPDATE session_tokens SET expires_at = $expires WHERE token_hash = $hash",
            command =>
            {
                command.Parameters.AddWithValue("$expires", Timestamp(expiresAt));
                command.Parameters.AddWithValue("$hash", Fingerprint(token));
            });

    private void MarkSupersededOnDisk(string token, DateTime rememberUntil) =>
        Execute(
            @"UPDATE session_tokens
              SET superseded_at = $until, expires_at = $until
              WHERE token_hash = $hash",
            command =>
            {
                command.Parameters.AddWithValue("$until", Timestamp(rememberUntil));
                command.Parameters.AddWithValue("$hash", Fingerprint(token));
            });

    private void Forget(string token) =>
        Execute(
            "DELETE FROM session_tokens WHERE token_hash = $hash",
            command => command.Parameters.AddWithValue("$hash", Fingerprint(token)));

    /// <summary>
    /// Rilegge dal disco un token che questo processo non ha mai emesso. Un token
    /// già revocato non torna vivo: quella riga la legge solo <see cref="WasSuperseded"/>.
    /// </summary>
    private Entry LoadFromDisk(string token)
    {
        if (database == null)
            return null;

        try
        {
            using SqliteConnection connection = database.Open();
            using SqliteCommand select = connection.CreateCommand();
            select.CommandText = @"
                SELECT player_id, username, expires_at FROM session_tokens
                WHERE token_hash = $hash AND superseded_at IS NULL";
            select.Parameters.AddWithValue("$hash", Fingerprint(token));

            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read())
                return null;

            var identity = new AccountIdentity(reader.GetString(0), reader.GetString(1));
            return new Entry(identity, ReadTimestamp(reader.GetString(2)));
        }
        catch (Exception exception) when (exception is SqliteException or FormatException)
        {
            logger?.LogError(exception, "Lettura del token di sessione fallita.");
            return null;
        }
    }

    /// <summary>Quando scade il ricordo della sostituzione, o default se non c'è.</summary>
    private DateTime LoadSupersededFromDisk(string token)
    {
        if (database == null)
            return default;

        try
        {
            using SqliteConnection connection = database.Open();
            using SqliteCommand select = connection.CreateCommand();
            select.CommandText =
                "SELECT superseded_at FROM session_tokens WHERE token_hash = $hash AND superseded_at IS NOT NULL";
            select.Parameters.AddWithValue("$hash", Fingerprint(token));

            object value = select.ExecuteScalar();
            return value is string moment ? ReadTimestamp(moment) : default;
        }
        catch (Exception exception) when (exception is SqliteException or FormatException)
        {
            logger?.LogError(exception, "Lettura della sostituzione di sessione fallita.");
            return default;
        }
    }

    private void Execute(string sql, Action<SqliteCommand> bind)
    {
        if (database == null)
            return;

        try
        {
            using SqliteConnection connection = database.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            bind(command);
            command.ExecuteNonQuery();
        }
        catch (SqliteException exception)
        {
            logger?.LogError(exception, "Scrittura del token di sessione fallita.");
        }
    }
}
