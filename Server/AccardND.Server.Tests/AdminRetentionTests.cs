using System.Text.Json;
using AccardND.Server.Accounts;
using AccardND.Server.Admin;
using AccardND.Server.Progression;
using AccardND.Server.Sessions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La retention per coorte del pannello admin. Il database e' vero, quindi la query
/// gira davvero: e' l'unico modo per accorgersi che un confronto fra date sbagliato
/// restituisce zero invece di un numero.
/// </summary>
public sealed class AdminRetentionTests
{
    [Fact]
    public void Counts_the_exact_day_and_not_the_days_before_it()
    {
        using var server = new TestServer();
        AdminService admin = CreateAdmin(server);
        DateTime cohortDay = DateTime.UtcNow.Date.AddDays(-40);

        // Quattro modi diversi di tornare (o non tornare) dopo la registrazione.
        AccountIdentity fedele = Register(server, "fedele", cohortDay);
        AccountIdentity giornoDopo = Register(server, "giornodopo", cohortDay);
        AccountIdentity terzoGiorno = Register(server, "terzogiorno", cohortDay);
        Register(server, "maipiu", cohortDay);

        Login(server, fedele, cohortDay.AddDays(1));
        Login(server, fedele, cohortDay.AddDays(7));
        Login(server, fedele, cohortDay.AddDays(30));
        Login(server, giornoDopo, cohortDay.AddDays(1));
        // Tornare il terzo giorno non fa D1 ne' D7: la misura e' "quel giorno",
        // non "entro quel giorno". Se questa riga finisse nel D7 vorrebbe dire che
        // il confronto e' diventato un >=.
        Login(server, terzoGiorno, cohortDay.AddDays(3));

        JsonElement cohort = CohortOf(admin.GetRetention(60), cohortDay);

        Assert.Equal(4, cohort.GetProperty("cohort").GetInt32());
        Assert.Equal(2, cohort.GetProperty("d1").GetInt32());
        Assert.Equal(1, cohort.GetProperty("d7").GetInt32());
        Assert.Equal(1, cohort.GetProperty("d30").GetInt32());
    }

    [Fact]
    public void Several_logins_on_the_same_day_are_one_returning_player()
    {
        using var server = new TestServer();
        AdminService admin = CreateAdmin(server);
        DateTime cohortDay = DateTime.UtcNow.Date.AddDays(-10);

        AccountIdentity insonne = Register(server, "insonne", cohortDay);
        // Tre riaperture dell'app nello stesso giorno: login_events ha una riga per
        // autenticazione, ma la retention conta le persone.
        Login(server, insonne, cohortDay.AddDays(1).AddHours(2));
        Login(server, insonne, cohortDay.AddDays(1).AddHours(13));
        Login(server, insonne, cohortDay.AddDays(1).AddHours(22));

        JsonElement cohort = CohortOf(admin.GetRetention(30), cohortDay);

        Assert.Equal(1, cohort.GetProperty("cohort").GetInt32());
        Assert.Equal(1, cohort.GetProperty("d1").GetInt32());
    }

    [Fact]
    public void Immature_cohorts_report_nothing_instead_of_zero()
    {
        using var server = new TestServer();
        AdminService admin = CreateAdmin(server);
        DateTime today = DateTime.UtcNow.Date;

        // Chi si e' registrato oggi non ha ancora avuto nessuno dei tre giorni, e chi
        // si e' registrato tre giorni fa ha avuto il primo ma non il settimo.
        Register(server, "appenaarrivato", today);
        AccountIdentity treGiorniFa = Register(server, "tregiornifa", today.AddDays(-3));
        Login(server, treGiorniFa, today.AddDays(-2));

        JsonElement retention = Serialize(admin.GetRetention(30));

        JsonElement oggi = CohortOf(retention, today);
        Assert.Equal(1, oggi.GetProperty("cohort").GetInt32());
        Assert.Equal(JsonValueKind.Null, oggi.GetProperty("d1").ValueKind);
        Assert.Equal(JsonValueKind.Null, oggi.GetProperty("d7").ValueKind);
        Assert.Equal(JsonValueKind.Null, oggi.GetProperty("d30").ValueKind);

        JsonElement recente = CohortOf(retention, today.AddDays(-3));
        Assert.Equal(1, recente.GetProperty("d1").GetInt32());
        Assert.Equal(JsonValueKind.Null, recente.GetProperty("d7").ValueKind);

        // E la media non se ne accorge: il denominatore del D1 e' il solo account
        // maturo, quello del D7 e' vuoto. Contare le coorti acerbe come "non
        // tornati" e' il modo piu' facile per leggere una retention meta' del vero.
        JsonElement summary = retention.GetProperty("summary");
        Assert.Equal(1, summary.GetProperty("d1").GetInt32());
        Assert.Equal(1, summary.GetProperty("d1Of").GetInt32());
        Assert.Equal(0, summary.GetProperty("d7Of").GetInt32());
        Assert.Equal(2, retention.GetProperty("accounts").GetInt32());
    }

    [Fact]
    public void The_summary_weighs_cohorts_by_their_size()
    {
        using var server = new TestServer();
        AdminService admin = CreateAdmin(server);
        DateTime grande = DateTime.UtcNow.Date.AddDays(-20);
        DateTime piccola = DateTime.UtcNow.Date.AddDays(-15);

        // Coorte da 3 con un ritorno, coorte da 1 con un ritorno: la media pesata e'
        // 2 su 4 (50%), non la media delle due percentuali (66%).
        for (int i = 0; i < 3; i++)
        {
            AccountIdentity player = Register(server, $"grande{i}", grande);
            if (i == 0) Login(server, player, grande.AddDays(1));
        }
        Login(server, Register(server, "piccola", piccola), piccola.AddDays(1));

        JsonElement summary = Serialize(admin.GetRetention(60)).GetProperty("summary");

        Assert.Equal(2, summary.GetProperty("d1").GetInt32());
        Assert.Equal(4, summary.GetProperty("d1Of").GetInt32());
    }

    [Fact]
    public void Cohorts_come_back_newest_first_and_only_from_the_window()
    {
        using var server = new TestServer();
        AdminService admin = CreateAdmin(server);
        DateTime today = DateTime.UtcNow.Date;

        Register(server, "vecchissimo", today.AddDays(-100));
        Register(server, "dentro", today.AddDays(-20));
        Register(server, "recente", today.AddDays(-2));

        JsonElement retention = Serialize(admin.GetRetention(30));
        JsonElement cohorts = retention.GetProperty("cohorts");

        Assert.Equal(2, cohorts.GetArrayLength());
        Assert.Equal(Day(today.AddDays(-2)), cohorts[0].GetProperty("day").GetString());
        Assert.Equal(Day(today.AddDays(-20)), cohorts[1].GetProperty("day").GetString());
        Assert.Equal(2, retention.GetProperty("accounts").GetInt32());
    }

    // ---- Impalcatura --------------------------------------------------------

    private static AdminService CreateAdmin(TestServer server)
    {
        var ranked = new RankedService(server.Database, server.Config);
        var seasons = new SeasonService(
            server.Database, server.Config, ranked, new UnlockService(server.Database, server.Config));
        return new AdminService(
            server.Database, new PresenceRegistry(), seasons, ranked,
            new AccountEraser(server.Database));
    }

    /// <summary>
    /// Registra un account e lo retrodata: <c>Register</c> scrive sempre "adesso", e
    /// una retention si prova solo con account nati in giorni diversi.
    /// </summary>
    private static AccountIdentity Register(TestServer server, string username, DateTime day)
    {
        AccountIdentity identity = server.RegisterAccount(username);
        server.Execute(
            $"UPDATE accounts SET created_at='{day.AddHours(10):O}' " +
            $"WHERE player_id='{identity.PlayerId}'");
        // La registrazione puo' aver lasciato un login datato adesso: cadrebbe in una
        // coorte diversa da quella dell'account e sporcherebbe i conti.
        server.Execute($"DELETE FROM login_events WHERE player_id='{identity.PlayerId}'");
        return identity;
    }

    private static void Login(TestServer server, AccountIdentity player, DateTime when) =>
        server.Execute(
            "INSERT INTO login_events (player_id, provider, occurred_at) " +
            $"VALUES ('{player.PlayerId}','password','{when:O}')");

    private static JsonElement CohortOf(object retention, DateTime day) =>
        CohortOf(Serialize(retention), day);

    private static JsonElement CohortOf(JsonElement retention, DateTime day)
    {
        foreach (JsonElement cohort in retention.GetProperty("cohorts").EnumerateArray())
        {
            if (cohort.GetProperty("day").GetString() == Day(day))
                return cohort;
        }
        throw new InvalidOperationException($"Nessuna coorte per il {Day(day)}.");
    }

    private static string Day(DateTime day) => day.ToString("yyyy-MM-dd");

    private static JsonElement Serialize(object payload) =>
        JsonSerializer.SerializeToElement(payload);
}
