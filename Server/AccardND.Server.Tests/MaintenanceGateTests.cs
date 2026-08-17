using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La manutenzione si accende *per* riavviare, quindi le due cose che devono
/// reggere sono: che da accesa non entri nessuno, e che sopravviva al riavvio.
/// Se si perdesse al riavvio il server riaprirebbe da solo nel mezzo del deploy,
/// che è esattamente il momento in cui non deve entrare nessuno.
/// </summary>
public sealed class MaintenanceGateTests : IDisposable
{
    private readonly TestServer server = new();

    public void Dispose() => server.Dispose();

    [Fact]
    public void DiDefault_IlServerEAperto()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();

        Assert.False(gate.IsActive);
        Assert.Equal(string.Empty, gate.Message);
        Assert.Null(gate.SinceUtc);
    }

    [Fact]
    public void AccenderlaChiudeIlPortone()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();

        (bool ok, string error) = gate.Update(enabled: true, "Torniamo alle 18:00");

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(gate.IsActive);
        Assert.Equal("Torniamo alle 18:00", gate.Message);
        Assert.NotNull(gate.SinceUtc);
    }

    [Fact]
    public void SpegnerlaRiapre()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();
        gate.Update(enabled: true, "chiuso");

        Assert.True(gate.Update(enabled: false, string.Empty).ok);

        Assert.False(gate.IsActive);
        Assert.Null(gate.SinceUtc);
    }

    /// <summary>
    /// Il motivo per cui lo stato sta sul DB e non in serverconfig.json: quel file
    /// lo riscrive il deploy, e il deploy è il momento in cui la manutenzione serve.
    /// </summary>
    [Fact]
    public void LaManutenzione_SopravviveAlRiavvio()
    {
        server.CreateMaintenanceGate().Update(enabled: true, "Aggiornamento in corso");

        MaintenanceGate afterRestart = server.RestartAndCreateMaintenanceGate();

        Assert.True(afterRestart.IsActive);
        Assert.Equal("Aggiornamento in corso", afterRestart.Message);
        Assert.NotNull(afterRestart.SinceUtc);
    }

    [Fact]
    public void SpegnerlaPrimaDelRiavvio_LasciaIlServerAperto()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();
        gate.Update(enabled: true, "chiuso");
        gate.Update(enabled: false, string.Empty);

        Assert.False(server.RestartAndCreateMaintenanceGate().IsActive);
    }

    /// <summary>
    /// Il timestamp risponde a "da quanto siamo giù": correggere il messaggio a
    /// manutenzione già accesa non deve far ripartire il conteggio da zero.
    /// </summary>
    [Fact]
    public void CorreggereIlMessaggio_NonAzzeraLInizio()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();
        gate.Update(enabled: true, "un attimo");
        DateTime? since = gate.SinceUtc;

        gate.Update(enabled: true, "torniamo alle 18:00");

        Assert.Equal(since, gate.SinceUtc);
        Assert.Equal("torniamo alle 18:00", gate.Message);
    }

    [Fact]
    public void RiaccenderlaDopoAverlaSpenta_RipartaDaCapo()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();
        gate.Update(enabled: true, "prima");
        DateTime? first = gate.SinceUtc;
        gate.Update(enabled: false, string.Empty);

        gate.Update(enabled: true, "seconda");

        Assert.NotNull(gate.SinceUtc);
        Assert.NotEqual(first, gate.SinceUtc);
    }

    [Fact]
    public void IlMessaggioTroppoLungo_VieneRifiutato()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();

        (bool ok, string error) = gate.Update(
            enabled: true, new string('x', MaintenanceGate.MaxMessageLength + 1));

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.False(gate.IsActive);
    }

    [Fact]
    public void IlMessaggioVuoto_EAmmesso()
    {
        MaintenanceGate gate = server.CreateMaintenanceGate();

        Assert.True(gate.Update(enabled: true, null).ok);

        Assert.True(gate.IsActive);
        Assert.Equal(string.Empty, gate.Message);
    }
}
