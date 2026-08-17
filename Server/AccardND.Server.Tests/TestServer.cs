using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using AccardND.Server.Match;
using AccardND.Server.Progression;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccardND.Server.Tests;

/// <summary>
/// Un server vero su un database usa e getta: stessi servizi, stesse query, solo
/// senza rete. Serve ai test che devono verificare cosa finisce davvero su disco
/// (l'esito di un match, il ricordo di una richiesta), non cosa un finto servizio
/// dice di aver fatto.
/// </summary>
public sealed class TestServer : IDisposable
{
    private readonly string databasePath;

    public TestServer()
    {
        databasePath = Path.Combine(
            Path.GetTempPath(), $"accardnd-test-{Guid.NewGuid():N}.db");
        // Senza pool ogni connessione si chiude davvero quando viene rilasciata, quindi a
        // fine test il file si cancella senza dover svuotare il pool globale del processo:
        // xUnit fa girare le classi in parallelo, e quella chiamata globale strappava le
        // connessioni anche ai database degli altri test.
        Config = new ServerConfig { DatabaseFilePath = databasePath, DatabasePooling = false };
        Database = new AccardDatabase(Config);
        Accounts = new AccountService(Database, Config, NullLogger<AccountService>.Instance);

        var ranked = new RankedService(Database, Config);
        var unlocks = new UnlockService(Database, Config);
        var seasons = new SeasonService(Database, Config, ranked, unlocks);
        var achievements = new AchievementService(Database, ranked, unlocks);
        ResultRecorder = new MatchResultRecorder(
            Database, seasons, ranked, unlocks, achievements,
            NullLogger<MatchResultRecorder>.Instance);
    }

    public ServerConfig Config { get; }
    public AccardDatabase Database { get; }
    public AccountService Accounts { get; }
    public MatchResultRecorder ResultRecorder { get; }

    public RequestDedupStore CreateDedupStore() =>
        new(Database, NullLogger<RequestDedupStore>.Instance);

    /// <summary>
    /// Riapre il database come farebbe un processo appena avviato: stesso file,
    /// istanze nuove. È il modo per provare cosa sopravvive a un deploy.
    /// </summary>
    public RequestDedupStore RestartAndCreateDedupStore() =>
        new(new AccardDatabase(Config), NullLogger<RequestDedupStore>.Instance);

    public SessionTokenRegistry CreateSessionTokens() =>
        new(Database, NullLogger<SessionTokenRegistry>.Instance);

    /// <summary>Il registro come lo troverebbe un server appena riavviato: DB riaperto.</summary>
    public SessionTokenRegistry RestartAndCreateSessionTokens() =>
        new(new AccardDatabase(Config), NullLogger<SessionTokenRegistry>.Instance);

    public ClientVersionGate CreateClientVersionGate() =>
        new(Database, Config, NullLogger<ClientVersionGate>.Instance);

    /// <summary>Il gate come lo troverebbe un server appena riavviato: DB riaperto.</summary>
    public ClientVersionGate RestartAndCreateClientVersionGate() =>
        new(new AccardDatabase(Config), Config, NullLogger<ClientVersionGate>.Instance);

    public MaintenanceGate CreateMaintenanceGate() =>
        new(Database, NullLogger<MaintenanceGate>.Instance);

    /// <summary>Come sopra: un'istanza nuova su un database nuovo simula il riavvio.</summary>
    public MaintenanceGate RestartAndCreateMaintenanceGate() =>
        new(new AccardDatabase(Config), NullLogger<MaintenanceGate>.Instance);

    public AccountIdentity RegisterAccount(string username)
    {
        (AccountIdentity identity, _, string error) = Accounts.Register(username, "password-di-prova");
        if (identity == null)
            throw new InvalidOperationException($"Account di prova non creato: {error}");
        return identity;
    }

    public T QueryScalar<T>(string sql)
    {
        using SqliteConnection connection = Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object value = command.ExecuteScalar();
        return value is DBNull or null ? default : (T)Convert.ChangeType(value, typeof(T));
    }

    public void Execute(string sql)
    {
        using SqliteConnection connection = Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        try
        {
            File.Delete(databasePath);
        }
        catch (IOException)
        {
            // Un file di prova rimasto nella temp non è un fallimento del test.
        }
    }

    /// <summary>Loadout minimo valido: serve solo a far partire il motore.</summary>
    public static PvpLoadout BuildLoadout()
    {
        var cards = new List<PvpLoadoutCard>();
        for (int index = 0; index < 9; index++)
            cards.Add(new PvpLoadoutCard($"card-{index}", 1 + index % 6));
        return new PvpLoadout(cards, baseDieSides: 6);
    }
}

/// <summary>
/// WebSocket finto che non parla con nessuno e si limita a conservare quello che
/// il server gli ha spedito: basta per verificare *cosa* riceve un client.
/// </summary>
public sealed class FakeWebSocket : WebSocket
{
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    public List<Envelope> Sent { get; } = new();

    public override WebSocketCloseStatus? CloseStatus => null;
    public override string CloseStatusDescription => null;
    public override WebSocketState State { get; } = WebSocketState.Open;
    public override string SubProtocol => null;

    public IReadOnlyList<Envelope> SentOfType(string type) =>
        Sent.Where(envelope => envelope.type == type).ToList();

    public override void Abort()
    {
    }

    public override Task CloseAsync(
        WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override void Dispose()
    {
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(
        ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
        // Nessun test riceve da qui: resta appeso finché il test non finisce.
        Task.Delay(Timeout.Infinite, cancellationToken)
            .ContinueWith(_ => new WebSocketReceiveResult(0, WebSocketMessageType.Close, true), cancellationToken);

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        string json = Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count);
        Envelope envelope = JsonSerializer.Deserialize<Envelope>(json, JsonOptions);
        lock (Sent)
        {
            Sent.Add(envelope);
        }
        return Task.CompletedTask;
    }
}
