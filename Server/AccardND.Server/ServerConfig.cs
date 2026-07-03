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

    /// <summary>Project ID di Unity (dashboard UGS). Vuoto = auth UGS disattivata.</summary>
    public string UgsProjectId { get; set; } = string.Empty;
    public string UgsIssuer { get; set; } = "https://player-auth.services.api.unity.com";
    public string UgsJwksSource { get; set; } = "https://player-auth.services.api.unity.com/.well-known/jwks.json";

    /// <summary>Login username/password legacy (dev). Da spegnere in produzione.</summary>
    public bool AllowPasswordAuth { get; set; } = true;

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
    public string CardCatalogPath { get; set; } = "cardcatalog.json";

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

    public PvpMatchRules ToMatchRules() => new(
        HandSize,
        FormationSize,
        DecisiveHandSize,
        RoundsToWinMatch,
        DeployedCardLives,
        VigorDieByRound,
        InitiativeDieSides,
        RogueRerollsOnes,
        BarbarianRageBonus,
        HunterMarkBonus,
        PriestBlessingBonus);

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
            initiativeDieSides = InitiativeDieSides
        };
    }

    private static DieCostDto[] ToDieCosts(Dictionary<int, int> costs) => costs
        .OrderBy(entry => entry.Key)
        .Select(entry => new DieCostDto { sides = entry.Key, cost = entry.Value })
        .ToArray();
}
