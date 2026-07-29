using System.Text.Json;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;

namespace AccardND.Server;

/// <summary>
/// Configurazione autoritativa del server. Il client riceve questi valori
/// via rules.data e li usa solo per la UI.
/// </summary>
public sealed class ServerConfig
{
    public string Urls { get; set; } = "http://localhost:5017";
    public string AccountsFilePath { get; set; } = "accounts.json";

    /// <summary>File SQLite (relativo alla cartella del binario se non assoluto).</summary>
    public string DatabaseFilePath { get; set; } = "accardnd.db";

    /// <summary>Project ID di Unity (dashboard UGS). Vuoto = auth UGS disattivata.</summary>
    public string UgsProjectId { get; set; } = string.Empty;
    public string UgsIssuer { get; set; } = "https://player-auth.services.api.unity.com";
    public string UgsJwksSource { get; set; } = "https://player-auth.services.api.unity.com/.well-known/jwks.json";

    /// <summary>Login username/password legacy (dev). Da spegnere in produzione.</summary>
    public bool AllowPasswordAuth { get; set; } = true;

    /// <summary>Broker OAuth Google per i client senza browser integrato (Android nativo).</summary>
    public GoogleOAuthConfig GoogleOAuth { get; set; } = new();

    public int LoadoutBudget { get; set; } = 60;
    public int LoadoutCardCount { get; set; } = 9;
    public int[] CardCostByValue { get; set; } = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
    public Dictionary<int, int> CardValueLimits { get; set; } = new()
    {
        [10] = 1,
        [9] = 2,
        [8] = 3,
        [7] = 4
    };
    public Dictionary<int, int> BaseDieCosts { get; set; } = new()
    {
        [3] = 0,
        [4] = 4,
        [6] = 8,
        [8] = 13
    };
    public Dictionary<int, int> BagDieCosts { get; set; } = new()
    {
        [6] = 2,
        [8] = 4,
        [10] = 7,
        [12] = 10,
        [20] = 18
    };

    public int RoundsToWinMatch { get; set; } = 2;
    public int DeployedCardLives { get; set; } = 2;
    public int HandSize { get; set; } = 6;
    public int FormationSize { get; set; } = 3;
    public int DecisiveHandSize { get; set; } = 3;
    public int InitiativeDieSides { get; set; } = 20;
    public int[] VigorDieByRound { get; set; } = { 4, 6, 8 };
    public bool RogueRerollsOnes { get; set; } = true;
    public int BarbarianRageBonus { get; set; } = 2;
    public int HunterMarkBonus { get; set; } = 2;
    public int PriestBlessingBonus { get; set; } = 2;
    public int TurnTimerSeconds { get; set; } = 60;
    public int ForfeitAfterConsecutiveTimeouts { get; set; } = 3;
    public int DisconnectTimeoutSeconds { get; set; } = 120;

    /// <summary>Vite di una carta schierata nelle stanze hardcore: una sola, nessun secondo scontro.</summary>
    public int HardcoreDeployedCardLives { get; set; } = 1;

    /// <summary>Timer di turno più stretto nelle stanze hardcore.</summary>
    public int HardcoreTurnTimerSeconds { get; set; } = 30;

    public string CardCatalogPath { get; set; } = "cardcatalog.json";

    /// <summary>Parametri del sistema ranked (MMR nascosto + tier a leghe).</summary>
    public RankedConfig Ranked { get; set; } = new();

    /// <summary>Parametri delle stagioni (durata, soft reset).</summary>
    public SeasonConfig Season { get; set; } = new();

    /// <summary>Pannello admin web (dashboard + gestione DB). Vedi AdminConfig.</summary>
    public AdminConfig Admin { get; set; } = new();

    /// <summary>Famiglie di mostri della campagna: sconfiggerle sblocca l'icona corrispondente.</summary>
    public string[] CampaignMonsters { get; set; } =
        { "goblin", "skeleton", "animal", "darkelf", "chimera", "alien", "whitealien", "spirit", "faceless", "champion" };

    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path))
            return new ServerConfig();
        var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
        return JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(path), options)
            ?? new ServerConfig();
    }

    public PvpLoadoutRules ToLoadoutRules() => new(
        LoadoutBudget,
        LoadoutCardCount,
        CardCostByValue,
        CardValueLimits,
        BaseDieCosts,
        BagDieCosts);

    /// <summary>Regole del match; le stanze hardcore ricevono la variante più severa.</summary>
    public PvpMatchRules ToMatchRules(string roomMode = null) => new(
        HandSize,
        FormationSize,
        DecisiveHandSize,
        RoundsToWinMatch,
        RoomModes.IsHardcore(roomMode) ? HardcoreDeployedCardLives : DeployedCardLives,
        VigorDieByRound,
        InitiativeDieSides,
        RogueRerollsOnes,
        BarbarianRageBonus,
        HunterMarkBonus,
        PriestBlessingBonus);

    /// <summary>Timer di turno effettivo per la modalità della stanza.</summary>
    public int ResolveTurnTimerSeconds(string roomMode) =>
        RoomModes.IsHardcore(roomMode) ? HardcoreTurnTimerSeconds : TurnTimerSeconds;

    public RulesData ToRulesData()
    {
        return new RulesData
        {
            budget = LoadoutBudget,
            requiredCardCount = LoadoutCardCount,
            cardCostByValue = CardCostByValue,
            cardValueLimits = CardValueLimits
                .Select(entry => new CardValueLimitDto { value = entry.Key, maximumCopies = entry.Value })
                .ToArray(),
            baseDieCosts = ToDieCosts(BaseDieCosts),
            bagDieCosts = ToDieCosts(BagDieCosts),
            roundsToWinMatch = RoundsToWinMatch,
            deployedCardLives = DeployedCardLives,
            turnTimerSeconds = TurnTimerSeconds,
            disconnectTimeoutSeconds = DisconnectTimeoutSeconds,
            initiativeDieSides = InitiativeDieSides,
            hardcoreDeployedCardLives = HardcoreDeployedCardLives,
            hardcoreTurnTimerSeconds = HardcoreTurnTimerSeconds
        };
    }

    private static DieCostDto[] ToDieCosts(Dictionary<int, int> costs) => costs
        .OrderBy(entry => entry.Key)
        .Select(entry => new DieCostDto { sides = entry.Key, cost = entry.Value })
        .ToArray();
}

/// <summary>
/// Broker OAuth Google. Il client ID e' quello Web gia' usato dal login del
/// browser: e' pubblico (sta nell'index.html della build WebGL) e va tenuto
/// identico, perche' e' quello che il provider Google di UGS accetta ed e' cio'
/// che fa coincidere il PlayerId tra APK e PWA. Il secret invece non si committa:
/// va passato via env var ACCARDND_GOOGLE_CLIENT_SECRET.
/// </summary>
public sealed class GoogleOAuthConfig
{
    public string ClientId { get; set; } =
        "866249556431-mgdm97uvov7mjvect4bp453dpp2oe48u.apps.googleusercontent.com";

    /// <summary>Consigliato lasciarlo vuoto qui e usare l'env var.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Deve stare tra gli "URI di reindirizzamento autorizzati" del client OAuth.</summary>
    public string RedirectUri { get; set; } = "https://accardndie.com/auth/google/callback";

    /// <summary>Quanto resta valida una richiesta di login in attesa del browser.</summary>
    public int RequestTtlMinutes { get; set; } = 10;

    public string ResolveClientSecret()
    {
        string fromEnv = Environment.GetEnvironmentVariable("ACCARDND_GOOGLE_CLIENT_SECRET");
        return string.IsNullOrEmpty(fromEnv) ? ClientSecret : fromEnv;
    }

    /// <summary>Senza secret il broker resta spento e Android ripiega sull'account locale.</summary>
    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ResolveClientSecret());
}

/// <summary>
/// Configurazione del pannello admin web. Nessun segreto viene committato:
/// la password si fornisce via variabile d'ambiente (Admin__Password oppure
/// ACCARDND_ADMIN_PASSWORD). Se nessuna password e disponibile il pannello
/// resta disattivato e gli endpoint /admin rispondono 404.
/// </summary>
public sealed class AdminConfig
{
    /// <summary>Username richiesto al login del pannello.</summary>
    public string Username { get; set; } = "admin";

    /// <summary>
    /// Password in chiaro. Consigliato lasciarla vuota qui (file versionato) e
    /// passarla via env var ACCARDND_ADMIN_PASSWORD sul server.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Durata di una sessione admin (token bearer) in ore.</summary>
    public int SessionTtlHours { get; set; } = 12;

    /// <summary>Password effettiva: env var se presente, altrimenti quella in config.</summary>
    public string ResolvePassword()
    {
        string fromEnv = Environment.GetEnvironmentVariable("ACCARDND_ADMIN_PASSWORD");
        return string.IsNullOrEmpty(fromEnv) ? Password : fromEnv;
    }

    /// <summary>Il pannello e attivo solo se e stata configurata una password.</summary>
    public bool IsEnabled => !string.IsNullOrEmpty(ResolvePassword());
}
