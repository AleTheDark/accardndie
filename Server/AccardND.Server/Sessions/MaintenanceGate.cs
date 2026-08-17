using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Sessions;

/// <summary>
/// Chiude il portone senza spegnere il server. Acceso il blocco, nessun nuovo
/// accesso passa: chi bussa resta sulla schermata di login con l'avviso di
/// manutenzione. Chi è già dentro non viene toccato — è un *drain*, non uno
/// sfratto: una partita PvP vive solo in memoria e un riavvio la annulla
/// comunque (vedi <see cref="Rooms.MatchDrainService"/>), quindi il modo civile
/// di riavviare è smettere di far entrare gente e aspettare che il campo si
/// svuoti.
///
/// Lo stato vive nella tabella <c>server_settings</c> come per
/// <see cref="ClientVersionGate"/>, e per la stessa ragione: la manutenzione si
/// accende *per* riavviare, quindi deve sopravvivere al riavvio. Scritta in
/// <c>serverconfig.json</c> se ne andrebbe al primo deploy del binario.
/// </summary>
public sealed class MaintenanceGate
{
    private const string EnabledKey = "maintenance.enabled";
    private const string MessageKey = "maintenance.message";
    private const string SinceKey = "maintenance.since";

    /// <summary>
    /// Tetto del messaggio: ci sta un avviso ("torniamo alle 18:00"), non un
    /// comunicato. Il popup del client è un riquadro di larghezza fissa.
    /// </summary>
    public const int MaxMessageLength = 240;

    private readonly AccardDatabase database;
    private readonly ILogger<MaintenanceGate> logger;

    /// <summary>
    /// Stato effettivo, sostituito in blocco a ogni modifica: un accesso in corso
    /// non deve poter leggere l'interruttore nuovo col messaggio vecchio.
    /// </summary>
    private volatile Snapshot current;

    public MaintenanceGate(AccardDatabase database, ILogger<MaintenanceGate> logger)
    {
        this.database = database;
        this.logger = logger;
        current = LoadFromDatabase();
        if (current.Active)
        {
            logger.LogWarning(
                "Avvio in MANUTENZIONE: nessun accesso sarà accettato (attiva dal {Since}).",
                current.SinceUtc?.ToString("O") ?? "(sconosciuto)");
        }
    }

    /// <summary>true se il blocco è acceso: gli accessi vanno rifiutati.</summary>
    public bool IsActive => current.Active;

    /// <summary>Avviso scritto dall'admin; vuoto se non ne ha messo uno.</summary>
    public string Message => current.Message;

    /// <summary>Da quando è accesa, per mostrarlo nel pannello.</summary>
    public DateTime? SinceUtc => current.SinceUtc;

    /// <summary>
    /// Accende o spegne il blocco e lo scrive sul DB. Vale dagli accessi
    /// successivi: chi è già collegato resta dov'è.
    /// </summary>
    public (bool ok, string error) Update(bool enabled, string message)
    {
        message = message?.Trim() ?? string.Empty;
        if (message.Length > MaxMessageLength)
            return (false, $"Messaggio troppo lungo (max {MaxMessageLength} caratteri).");

        DateTime now = DateTime.UtcNow;
        // Riaccendere una manutenzione già accesa non ne azzera l'inizio: il "da
        // quanto siamo giù" è l'unica cosa che quel timestamp deve raccontare, e
        // un salvataggio per correggere il messaggio lo falsificherebbe.
        DateTime? since = enabled ? (current.Active ? current.SinceUtc ?? now : now) : null;

        using SqliteConnection connection = database.Open();
        string stamp = now.ToString("O");
        Write(connection, EnabledKey, enabled ? "1" : "0", stamp);
        Write(connection, MessageKey, message, stamp);
        Write(connection, SinceKey, since?.ToString("O") ?? string.Empty, stamp);

        current = new Snapshot(enabled, message, since);
        logger.LogWarning(
            "Manutenzione {State} dal pannello admin{Message}.",
            enabled ? "ATTIVATA" : "disattivata",
            message.Length > 0 ? $": {message}" : string.Empty);
        return (true, null);
    }

    private Snapshot LoadFromDatabase()
    {
        using SqliteConnection connection = database.Open();
        if (Read(connection, EnabledKey) != "1")
            return Snapshot.Off;

        string since = Read(connection, SinceKey);
        return new Snapshot(
            true,
            Read(connection, MessageKey) ?? string.Empty,
            DateTime.TryParse(
                since,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                ? parsed
                : null);
    }

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

    private sealed record Snapshot(bool Active, string Message, DateTime? SinceUtc)
    {
        public static readonly Snapshot Off = new(false, string.Empty, null);
    }
}
