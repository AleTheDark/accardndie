using AccardND.Server.Accounts;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// I token di sessione, dal punto di vista del rimpallo fra due dispositivi.
/// Revocare un token non basta: il client sloggato può non aver mai letto l'avviso
/// (app in pausa, scheda in secondo piano) e al risveglio riprova a riagganciarsi.
/// Se quel tentativo si sentisse dire solo "token sconosciuto" ripiegherebbe sul
/// login completo, rientrerebbe come sessione nuova e sbatterebbe fuori chi sta
/// giocando adesso. Per questo la revoca lascia un ricordo.
/// </summary>
public sealed class SessionTokenRegistryTests
{
    private static AccountIdentity Identity(string username) =>
        new($"player-{username}", username);

    [Fact]
    public void AnIssuedToken_ResolvesToItsAccount()
    {
        var registry = new SessionTokenRegistry();
        AccountIdentity account = Identity("apettona");

        string token = registry.Issue(account);

        Assert.Equal(account.PlayerId, registry.Resolve(token)?.PlayerId);
        Assert.False(registry.WasSuperseded(token));
    }

    [Fact]
    public void ARevokedToken_IsRememberedAsSuperseded_NotJustForgotten()
    {
        var registry = new SessionTokenRegistry();
        string token = registry.Issue(Identity("apettona"));

        registry.Revoke(token);

        Assert.Null(registry.Resolve(token));
        Assert.True(registry.WasSuperseded(token));
    }

    [Fact]
    public void ATokenNobodyEverIssued_IsNotSuperseded_ItIsJustExpired()
    {
        var registry = new SessionTokenRegistry();

        // Distinzione che conta: qui la risposta giusta è "rifai l'accesso", non
        // "sei stato sostituito", altrimenti mostreremmo la sloggatura a chi rientra
        // dopo giorni con un token vecchio.
        Assert.False(registry.WasSuperseded("token-che-non-e-mai-esistito"));
        Assert.False(registry.WasSuperseded(null));
        Assert.False(registry.WasSuperseded(string.Empty));
    }

    [Fact]
    public void RevokingOneSession_LeavesTheOtherTokensInPeace()
    {
        var registry = new SessionTokenRegistry();
        string kicked = registry.Issue(Identity("apettona"));
        string playing = registry.Issue(Identity("apettona"));

        registry.Revoke(kicked);

        Assert.NotNull(registry.Resolve(playing));
        Assert.False(registry.WasSuperseded(playing));
    }
}
