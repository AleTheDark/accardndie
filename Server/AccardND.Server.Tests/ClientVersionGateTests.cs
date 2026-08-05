using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Il cancello di versione decide chi entra: sbagliarlo per eccesso chiude fuori
/// tutti i giocatori, per difetto lascia parlare build vecchie con un protocollo
/// che non esiste più. Qui si fissano le due direzioni, e il fatto che una
/// versione decisa dal pannello sopravviva al deploy successivo.
/// </summary>
public sealed class ClientVersionGateTests : IDisposable
{
    private readonly TestServer server = new();

    public void Dispose() => server.Dispose();

    [Fact]
    public void SenzaVersioneTarget_ChiunquePuoEntrare()
    {
        server.Config.ClientVersion.Target = string.Empty;
        ClientVersionGate gate = server.CreateClientVersionGate();

        Assert.False(gate.IsEnforced);
        Assert.True(gate.IsAccepted("0.0.1"));
        Assert.True(gate.IsAccepted(null));
    }

    [Fact]
    public void ConVersioneTarget_PassaSoloQuellaEsatta()
    {
        server.Config.ClientVersion.Target = "0.9.2";
        ClientVersionGate gate = server.CreateClientVersionGate();

        Assert.True(gate.IsEnforced);
        Assert.True(gate.IsAccepted("0.9.2"));
        Assert.True(gate.IsAccepted("  0.9.2 "));
        Assert.False(gate.IsAccepted("0.9.1"));
        Assert.False(gate.IsAccepted("0.9.10"));
    }

    /// <summary>
    /// Le build precedenti a questo controllo non mandano nessuna versione: sono
    /// esattamente quelle da fermare, quindi il campo vuoto non vale un lasciapassare.
    /// </summary>
    [Fact]
    public void ChiNonDichiaraLaVersione_RestaFuori()
    {
        server.Config.ClientVersion.Target = "0.9.2";
        ClientVersionGate gate = server.CreateClientVersionGate();

        Assert.False(gate.IsAccepted(null));
        Assert.False(gate.IsAccepted(string.Empty));
    }

    [Fact]
    public void IlPannelloCambiaLaVersioneAllIstante()
    {
        server.Config.ClientVersion.Target = "0.9.2";
        ClientVersionGate gate = server.CreateClientVersionGate();

        (bool ok, string error) = gate.Update("0.9.3", enforce: true, "https://accardndie.com");

        Assert.True(ok);
        Assert.Null(error);
        Assert.False(gate.IsAccepted("0.9.2"));
        Assert.True(gate.IsAccepted("0.9.3"));
    }

    /// <summary>
    /// È il motivo per cui il valore sta sul DB e non in serverconfig.json: il file
    /// viene sovrascritto dal deploy, il DB no.
    /// </summary>
    [Fact]
    public void LaVersioneDelPannello_SopravviveAlRiavvio()
    {
        server.Config.ClientVersion.Target = "0.9.2";
        server.CreateClientVersionGate().Update("0.9.3", enforce: true, updateUrl: null);

        ClientVersionGate afterRestart = server.RestartAndCreateClientVersionGate();

        Assert.Equal("0.9.3", afterRestart.Target);
        Assert.Equal("database", afterRestart.Source);
        Assert.False(afterRestart.IsAccepted("0.9.2"));
    }

    [Fact]
    public void IlResetRiportaAllaConfigurazioneDiAvvio()
    {
        server.Config.ClientVersion.Target = "0.9.2";
        ClientVersionGate gate = server.CreateClientVersionGate();
        gate.Update("0.9.3", enforce: true, updateUrl: null);

        gate.ResetToConfiguration();

        Assert.Equal("0.9.2", gate.Target);
        Assert.True(gate.IsAccepted("0.9.2"));
        Assert.Equal("0.9.2", server.RestartAndCreateClientVersionGate().Target);
    }

    [Fact]
    public void SpegnereIlBlocco_NonCancellaLaVersione()
    {
        server.Config.ClientVersion.Target = "0.9.2";
        ClientVersionGate gate = server.CreateClientVersionGate();

        Assert.True(gate.Update("0.9.3", enforce: false, updateUrl: null).ok);

        Assert.Equal("0.9.3", gate.Target);
        Assert.False(gate.IsEnforced);
        Assert.True(gate.IsAccepted("0.1.0"));
    }

    /// <summary>
    /// Un blocco senza versione respingerebbe chiunque, compreso chi è aggiornato:
    /// va rifiutato al salvataggio, non scoperto dai giocatori.
    /// </summary>
    [Fact]
    public void NonSiPuoAttivareIlBloccoSenzaVersione()
    {
        ClientVersionGate gate = server.CreateClientVersionGate();

        (bool ok, string error) = gate.Update("   ", enforce: true, updateUrl: null);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void IlLinkDiAggiornamento_DeveEssereUnUrlWeb()
    {
        ClientVersionGate gate = server.CreateClientVersionGate();

        Assert.False(gate.Update("0.9.3", enforce: true, "javascript:alert(1)").ok);
        Assert.False(gate.Update("0.9.3", enforce: true, "accardndie.com").ok);
        Assert.True(gate.Update("0.9.3", enforce: true, "https://accardndie.com").ok);
        Assert.True(gate.Update("0.9.3", enforce: true, string.Empty).ok);
    }
}
