using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace AccardND.Server.Accounts;

public sealed record VerifiedExternalIdentity(string Provider, string ExternalId, string DisplayName);

/// <summary>
/// Valida gli access token di Unity Authentication contro le chiavi pubbliche
/// (JWKS) di Unity. Per i test la sorgente JWKS può essere un file locale.
/// </summary>
public sealed class UgsAuthService
{
    private readonly ServerConfig config;
    private readonly ILogger<UgsAuthService> logger;
    private readonly HttpClient httpClient = new();
    private readonly SemaphoreSlim keysLock = new(1, 1);
    private IList<SecurityKey> cachedKeys;
    private DateTime keysExpireAt = DateTime.MinValue;

    public UgsAuthService(ServerConfig config, ILogger<UgsAuthService> logger)
    {
        this.config = config;
        this.logger = logger;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(config.UgsProjectId);

    public async Task<(VerifiedExternalIdentity Identity, string Error)> ValidateAsync(
        string accessToken, string displayName)
    {
        if (!IsEnabled)
            return (null, "Unity Authentication non configurato sul server.");
        if (string.IsNullOrWhiteSpace(accessToken))
            return (null, "Token mancante.");

        IList<SecurityKey> keys;
        try
        {
            keys = await GetSigningKeysAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Impossibile caricare il JWKS da {Source}", config.UgsJwksSource);
            return (null, "Chiavi di firma non disponibili, riprova.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = config.UgsIssuer,
            ValidateIssuer = true,
            IssuerSigningKeys = keys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateAudience = true,
            // Unity mette il project id tra le audience: basta che una lo contenga.
            AudienceValidator = (audiences, _, _) =>
                audiences != null && audiences.Any(a =>
                    a != null && a.Contains(config.UgsProjectId, StringComparison.OrdinalIgnoreCase))
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            System.Security.Claims.ClaimsPrincipal principal =
                handler.ValidateToken(accessToken, parameters, out _);
            string playerId = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(playerId))
                return (null, "Token senza identità giocatore.");

            string name = SanitizeName(displayName, playerId);
            return (new VerifiedExternalIdentity("ugs", playerId, name), null);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            logger.LogInformation("Token UGS rifiutato: {Reason}", exception.Message);
            return (null, "Token non valido o scaduto.");
        }
    }

    private static string SanitizeName(string displayName, string playerId)
    {
        string name = (displayName ?? string.Empty).Trim();
        if (name.Length > 20)
            name = name[..20];
        if (name.Length < 3)
            name = $"player-{playerId[..Math.Min(6, playerId.Length)]}";
        return name;
    }

    private async Task<IList<SecurityKey>> GetSigningKeysAsync()
    {
        if (cachedKeys != null && DateTime.UtcNow < keysExpireAt)
            return cachedKeys;

        await keysLock.WaitAsync();
        try
        {
            if (cachedKeys != null && DateTime.UtcNow < keysExpireAt)
                return cachedKeys;

            string json = config.UgsJwksSource.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? await httpClient.GetStringAsync(config.UgsJwksSource)
                : await File.ReadAllTextAsync(config.UgsJwksSource);
            cachedKeys = new JsonWebKeySet(json).GetSigningKeys();
            keysExpireAt = DateTime.UtcNow.AddHours(6);
            logger.LogInformation("JWKS caricato: {Count} chiavi di firma.", cachedKeys.Count);
            return cachedKeys;
        }
        finally
        {
            keysLock.Release();
        }
    }
}
