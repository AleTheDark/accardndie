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

    [Fact]
    public void ASessionSurvivesARestartOfTheServer()
    {
        using var server = new TestServer();
        AccountIdentity account = server.RegisterAccount("apettona");
        string token = server.CreateSessionTokens().Issue(account);

        // Un deploy: processo nuovo, stesso database. Prima di questo, ogni
        // pubblicazione sbatteva fuori chiunque fosse collegato.
        SessionTokenRegistry afterDeploy = server.RestartAndCreateSessionTokens();

        Assert.Equal(account.PlayerId, afterDeploy.Resolve(token)?.PlayerId);
        Assert.Equal(account.Username, afterDeploy.Resolve(token)?.Username);
    }

    [Fact]
    public void ARevokedSession_IsStillRememberedAfterARestart()
    {
        using var server = new TestServer();
        AccountIdentity account = server.RegisterAccount("apettona");
        SessionTokenRegistry before = server.CreateSessionTokens();
        string kicked = before.Issue(account);
        before.Revoke(kicked);

        // È proprio dopo un deploy che il client rimasto indietro riprova col token
        // vecchio: se il ricordo non sopravvivesse, si sentirebbe dire "sessione
        // scaduta", rifarebbe il login completo e sloggherebbe chi sta giocando.
        SessionTokenRegistry afterDeploy = server.RestartAndCreateSessionTokens();

        Assert.Null(afterDeploy.Resolve(kicked));
        Assert.True(afterDeploy.WasSuperseded(kicked));
    }

    [Fact]
    public void TheDatabaseNeverHoldsTheTokenItself_OnlyItsFingerprint()
    {
        using var server = new TestServer();
        string token = server.CreateSessionTokens().Issue(server.RegisterAccount("apettona"));

        // Il token è un bearer: chi legge il database non deve poterlo rigiocare.
        Assert.Equal(0, server.QueryScalar<int>(
            $"SELECT COUNT(*) FROM session_tokens WHERE token_hash = '{token.Replace("'", "''")}'"));
        Assert.Equal(1, server.QueryScalar<int>("SELECT COUNT(*) FROM session_tokens"));
    }
}
