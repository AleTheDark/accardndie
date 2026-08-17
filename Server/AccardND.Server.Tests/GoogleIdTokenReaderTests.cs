using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AccardND.Server;
using AccardND.Server.Accounts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Lettura della mail dall'ID token Google. E' il cardine di tre cose che senza
/// non funzionano: l'accesso a /statistiche, la pagina di cancellazione account
/// pretesa da Google Play, e la mail registrata al login dal gioco.
///
/// Il token e' firmato qui con una chiave di prova, cosi' la verifica gira per
/// intero (firma, emittente, destinatario, scadenza) senza chiamare Google.
/// </summary>
public sealed class GoogleIdTokenReaderTests
{
    private const string Email = "giocatrice@example.com";

    [Fact]
    public void A_valid_token_gives_back_the_verified_email()
    {
        (GoogleIdTokenReader reader, RsaSecurityKey key, string audience) = Build();

        string token = Sign(key, audience, Email, emailVerified: "true");

        Assert.Equal(Email, reader.TryReadVerifiedEmail(token, new SecurityKey[] { key }));
    }

    /// <summary>
    /// Il caso che ha rotto l'accesso in produzione: JwtSecurityTokenHandler, con le
    /// impostazioni predefinite, rinomina il claim "email" nel nome SAML lungo, e
    /// cercarlo come "email" torna null. Il token restava validissimo e l'accesso
    /// falliva sempre, con un messaggio che dava la colpa a Google. Questo test
    /// fallisce se qualcuno toglie MapInboundClaims = false.
    /// </summary>
    [Fact]
    public void The_email_claim_is_not_renamed_by_the_handler()
    {
        (GoogleIdTokenReader reader, RsaSecurityKey key, string audience) = Build();
        string token = Sign(key, audience, Email, emailVerified: "true");

        // Come lo leggerebbe l'handler lasciato ai valori predefiniti: la prova che
        // il claim c'e' ma sotto un altro nome, non che manchi.
        ClaimsPrincipal mapped = new JwtSecurityTokenHandler().ValidateToken(
            token, Parameters(key, audience), out _);
        Assert.Null(mapped.FindFirst("email"));
        Assert.Equal(Email, mapped.FindFirst(ClaimTypes.Email)?.Value);

        Assert.Equal(Email, reader.TryReadVerifiedEmail(token, new SecurityKey[] { key }));
    }

    [Fact]
    public void An_unverified_email_is_worth_nothing()
    {
        (GoogleIdTokenReader reader, RsaSecurityKey key, string audience) = Build();

        string token = Sign(key, audience, Email, emailVerified: "false");

        Assert.Null(reader.TryReadVerifiedEmail(token, new SecurityKey[] { key }));
    }

    [Fact]
    public void A_token_for_someone_else_or_signed_by_someone_else_is_refused()
    {
        (GoogleIdTokenReader reader, RsaSecurityKey key, string audience) = Build();

        string wrongAudience = Sign(key, "un-altra-app.apps.googleusercontent.com", Email, "true");
        Assert.Null(reader.TryReadVerifiedEmail(wrongAudience, new SecurityKey[] { key }));

        var impostor = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "impostore" };
        string forged = Sign(impostor, audience, Email, "true");
        Assert.Null(reader.TryReadVerifiedEmail(forged, new SecurityKey[] { key }));
    }

    [Fact]
    public void An_expired_token_is_refused()
    {
        (GoogleIdTokenReader reader, RsaSecurityKey key, string audience) = Build();

        // Oltre i due minuti di tolleranza previsti per lo sfasamento degli orologi.
        string token = Sign(key, audience, Email, "true", expiresAt: DateTime.UtcNow.AddMinutes(-10));

        Assert.Null(reader.TryReadVerifiedEmail(token, new SecurityKey[] { key }));
    }

    // ---- Impalcatura ----------------------------------------------------------

    private static (GoogleIdTokenReader Reader, RsaSecurityKey Key, string Audience) Build()
    {
        var config = new ServerConfig();
        var key = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "chiave-di-prova" };
        var reader = new GoogleIdTokenReader(config, NullLogger<GoogleIdTokenReader>.Instance);
        return (reader, key, config.GoogleOAuth.ClientId);
    }

    private static TokenValidationParameters Parameters(SecurityKey key, string audience) => new()
    {
        ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
        IssuerSigningKeys = new[] { key },
        ValidAudience = audience,
        ClockSkew = TimeSpan.FromMinutes(2)
    };

    private static string Sign(
        SecurityKey key, string audience, string email, string emailVerified,
        DateTime? expiresAt = null)
    {
        // Un token scaduto e' comunque un token che a suo tempo era valido: il
        // notBefore va tenuto prima della scadenza, altrimenti non si costruisce.
        DateTime expires = expiresAt ?? DateTime.UtcNow.AddMinutes(30);

        var token = new JwtSecurityToken(
            issuer: "https://accounts.google.com",
            audience: audience,
            claims: new[]
            {
                new Claim("sub", "1234567890"),
                new Claim("email", email),
                new Claim("email_verified", emailVerified)
            },
            notBefore: expires.AddMinutes(-35),
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
