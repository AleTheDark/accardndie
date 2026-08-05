using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Sessions;

/// <summary>
/// Decide quali build possono accedere. La versione richiesta si cambia a caldo
/// dal pannello admin e vive sul DB: <c>serverconfig.json</c> viene sovrascritto
/// a ogni deploy del binario, quindi non è un posto dove far vivere un valore che
/// l'amministratore modifica.
///
/// Precedenza: quello che il pannello ha scritto sul DB vince; se nessuno l'ha mai
/// toccato valgono la env var ACCARDND_CLIENT_VERSION e poi il file di config, che
/// restano la via per far partire un server nuovo già con la versione giusta.
/// </summary>
public sealed class ClientVersionGate
{
    private const string TargetKey = "client_version.target";
    private const string EnforceKey = "client_version.enforce";
    private const string UpdateUrlKey = "client_version.update_url";

    private readonly AccardDatabase database;
    private readonly ClientVersionConfig fallback;
    private readonly ILogger<ClientVersionGate> logger;

    /// <summary>
    /// Lo stato effettivo, sostituito in blocco a ogni modifica. Una singola
    /// reference immutabile evita che un accesso in corso legga un target nuovo
    /// con l'interruttore vecchio mentre l'admin sta salvando.
    /// </summary>
    private volatile Snapshot current;

    public ClientVersionGate(AccardDatabase database, ServerConfig config, ILogger<ClientVersionGate> logger)
    {
        this.database = database;
        this.logger = logger;
        fallback = config.ClientVersion;
        current = LoadFromDatabase();
        logger.LogInformation(
            "Versione client richiesta: {Target} (origine {Source}, blocco {State}).",
            string.IsNullOrEmpty(current.Target) ? "(nessuna)" : current.Target,
            current.Source,
            current.Enforce ? "attivo" : "spento");
    }

    public string Target => current.Target;
    public bool Enforce => current.Enforce;
    public string UpdateUrl => current.UpdateUrl;

    /// <summary>"database" se il valore arriva dal pannello, altrimenti "env"/"config".</summary>
    public string Source => current.Source;

    /// <summary>Il blocco morde solo se è acceso e c'è una versione da pretendere.</summary>
    public bool IsEnforced => current.Enforce && !string.IsNullOrWhiteSpace(current.Target);

    /// <summary>
    /// I client che non dichiarano affatto la versione sono build precedenti al
    /// controllo: quando il blocco è attivo vanno respinti come tutti gli altri.
    /// </summary>
    public bool IsAccepted(string clientVersion)
    {
        Snapshot snapshot = current;
        return !(snapshot.Enforce && !string.IsNullOrWhiteSpace(snapshot.Target))
            || string.Equals(clientVersion?.Trim(), snapshot.Target, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applica la scelta del pannello e la scrive sul DB. Il valore vale da subito
    /// per i login successivi: chi è già dentro non viene buttato fuori.
    /// </summary>
    public (bool ok, string error) Update(string target, bool enforce, string updateUrl)
    {
        target = target?.Trim() ?? string.Empty;
        if (enforce && string.IsNullOrEmpty(target))
            return (false, "Per attivare il blocco serve una versione target.");
        if (target.Length > 32)
            return (false, "Versione troppo lunga.");

        updateUrl = updateUrl?.Trim() ?? string.Empty;
        if (updateUrl.Length > 0 && !IsWebUrl(updateUrl))
            return (false, "Il link di aggiornamento deve essere un URL http/https.");

        using SqliteConnection connection = database.Open();
        string now = DateTime.UtcNow.ToString("O");
        Write(connection, TargetKey, target, now);
        Write(connection, EnforceKey, enforce ? "1" : "0", now);
        Write(connection, UpdateUrlKey, updateUrl, now);

        current = new Snapshot(target, enforce, updateUrl, "database");
        logger.LogWarning(
            "Versione client richiesta cambiata dal pannello admin: {Target} (blocco {State}).",
            string.IsNullOrEmpty(target) ? "(nessuna)" : target,
            enforce ? "attivo" : "spento");
        return (true, null);
    }

    /// <summary>
    /// Torna al valore di env var / file di config cancellando l'override. Via di
    /// fuga se il pannello scrive una versione sbagliata e chiude fuori tutti: si
    /// riparte da ciò con cui il server è stato avviato.
    /// </summary>
    public void ResetToConfiguration()
    {
        using (SqliteConnection connection = database.Open())
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "DELETE FROM server_settings WHERE key IN ($target, $enforce, $url);";
            command.Parameters.AddWithValue("$target", TargetKey);
            command.Parameters.AddWithValue("$enforce", EnforceKey);
            command.Parameters.AddWithValue("$url", UpdateUrlKey);
            command.ExecuteNonQuery();
        }

        current = FromConfiguration();
        logger.LogWarning(
            "Versione client riportata alla configurazione di avvio: {Target}.",
            string.IsNullOrEmpty(current.Target) ? "(nessuna)" : current.Target);
    }

    /// <summary>Il valore che tornerebbe attivo con un reset, per mostrarlo nel pannello.</summary>
    public string ConfiguredTarget => fallback.ResolveTarget();

    private Snapshot LoadFromDatabase()
    {
        using SqliteConnection connection = database.Open();
        string target = Read(connection, TargetKey);
        if (target == null)
            return FromConfiguration();

        return new Snapshot(
            target,
            Read(connection, EnforceKey) != "0",
            Read(connection, UpdateUrlKey) ?? fallback.UpdateUrl ?? string.Empty,
            "database");
    }

    private Snapshot FromConfiguration() => new(
        fallback.ResolveTarget(),
        fallback.Enforce,
        fallback.UpdateUrl ?? string.Empty,
        Environment.GetEnvironmentVariable("ACCARDND_CLIENT_VERSION") is { Length: > 0 } ? "env" : "config");

    private static string Read(SqliteConnection connection, string key)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM server_settings WHERE key=$key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void Write(SqliteConnection connection, string key, string value, string now)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO server_settings (key, value, updated_at) VALUES ($key, $value, $now)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value, updated_at=excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$now", now);
        command.ExecuteNonQuery();
    }

    private static bool IsWebUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private sealed record Snapshot(string Target, bool Enforce, string UpdateUrl, string Source);
}
