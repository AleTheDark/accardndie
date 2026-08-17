using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// Il percorso del tutorial progressivo. Qui il rischio non e' un modulo che non parte - se
/// ne accorge chiunque - ma un modulo che paga due volte o che si lascia riscuotere fuori
/// ordine: i due doni da 40 vasetti sono l'unica sorgente di miele fuori dalla taverna, e
/// l'ultimo modulo consegna capitolo e oggetto.
/// </summary>
public sealed class TutorialModuleTests
{
    [Fact]
    public void The_first_module_hands_over_the_warrior_and_the_honey_for_the_mage()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("allievo");

        SinglePlayerRewardResult first = Claim(progress, player, "m1-warrior");

        Assert.Contains("m1-warrior", first.progress.completedTutorialModules);
        Assert.Contains("warrior", first.progress.unlockedClasses);
        // Il capitolo arriva alla fine del percorso, non all'inizio: prima si impara.
        Assert.DoesNotContain("chapter-1", first.progress.unlockedChapters);
        // E il flag non si alza a meta' strada: e' la porta del resto del gioco.
        Assert.False(first.progress.tutorialCompleted);
    }

    /// <summary>
    /// Il dono non e' un guadagno: vale esattamente la classe che il tour fa comprare subito
    /// dopo, e dopo l'acquisto il giocatore torna a zero. Se i due numeri divergessero, il
    /// miele smetterebbe di essere una cosa che si guadagna solo in taverna (in eccesso) o il
    /// tour resterebbe fermo davanti a un acquisto impossibile (in difetto).
    ///
    /// Vale per ogni modulo che paga una classe, cosi' il controllo non va aggiornato quando
    /// il percorso cambia: oggi sono il Guerriero e il Mago.
    /// </summary>
    [Fact]
    public void Every_gift_is_worth_exactly_the_class_it_pays_for()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("compratore");

        foreach (TutorialModuleCatalog.Module module in TutorialModuleCatalog.All)
        {
            SinglePlayerRewardResult step = Claim(progress, player, module.Id);
            if (module.PaysForClassId == null)
            {
                Assert.Equal(0, step.grantedHoney);
                continue;
            }

            Assert.True(SanctuaryCatalog.TryGetEntry(
                SanctuaryCatalog.TypeClass, module.PaysForClassId, out var paidClass));
            Assert.Equal(paidClass.HoneyCost, step.grantedHoney);

            // E il buono si spende davvero: dopo l'acquisto guidato il saldo torna a zero.
            SpendOn(server, player.PlayerId, paidClass.HoneyCost);
            Assert.Equal(0, progress.GetProgress(player).honey);
        }
    }

    [Fact]
    public void The_mage_can_be_bought_immediately_after_the_warrior_module()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("allievo-mago");
        Claim(progress, player, "m1-warrior");

        (SinglePlayerProgressData after, string code, string error) = progress.PurchaseUnlock(
            player,
            new SinglePlayerPurchaseUnlockRequest { type = "class", id = "mage" });

        Assert.True(code == null, error);
        Assert.Contains("mage", after.unlockedClasses);
        Assert.Equal(0, after.honey);
        Assert.False(after.tutorialCompleted);
    }

    [Fact]
    public void The_last_module_hands_over_the_chapter_the_item_and_closes_the_tutorial()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("diplomato");

        SinglePlayerRewardResult last = ClaimWholePath(progress, player);

        Assert.True(last.progress.tutorialCompleted);
        Assert.Contains("chapter-1", last.progress.unlockedChapters);
        Assert.Contains(ReadStash(server, player.PlayerId), item => item.ItemId == "second-chance");
    }

    [Fact]
    public void Claiming_the_same_module_twice_grants_nothing_the_second_time()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("insistente");

        SinglePlayerRewardResult first = Claim(progress, player, "m1-warrior");
        SinglePlayerRewardResult second = Claim(progress, player, "m1-warrior");

        Assert.Equal(0, second.grantedHoney);
        Assert.Equal(first.progress.honey, second.progress.honey);
        // Nessun claim nuovo: la risposta e' idempotente, non una seconda riscossione.
        Assert.Null(second.rewardClaimId);
    }

    /// <summary>
    /// Il caso che vale la pena difendere: un client modificato che salta all'ultimo modulo
    /// per portarsi a casa capitolo e Seconda Chance senza aver giocato niente.
    /// </summary>
    [Fact]
    public void A_module_cannot_be_claimed_out_of_order()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("furbo");

        (SinglePlayerRewardResult result, string code, string error) =
            progress.ClaimTutorialModuleReward(
                player, new SinglePlayerTutorialModuleRequest { moduleId = "m0-basics" });

        Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
        Assert.Contains("in ordine", error);
        Assert.Null(result);

        SinglePlayerProgressData after = progress.GetProgress(player);
        Assert.False(after.tutorialCompleted);
        Assert.Empty(after.completedTutorialModules);
        Assert.Empty(ReadStash(server, player.PlayerId));
    }

    [Fact]
    public void An_unknown_module_is_refused()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("inventore");

        (_, string code, _) = progress.ClaimTutorialModuleReward(
            player, new SinglePlayerTutorialModuleRequest { moduleId = "m9-inventato" });

        Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
        Assert.Empty(progress.GetProgress(player).completedTutorialModules);
    }

    /// <summary>
    /// Chi aveva finito il vecchio tutorial monolitico non deve ritrovarsi l'onboarding da
    /// rifare: al primo contatto col server i moduli risultano tutti chiusi. Senza soldi
    /// retroattivi, pero': i moduli sono riscossi, non da riscuotere.
    /// </summary>
    [Fact]
    public void An_account_that_finished_the_old_tutorial_starts_with_every_module_done()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("veterano");
        var progress = new SinglePlayerProgressService(server.Database);
        progress.ClaimTutorialReward(player, new SinglePlayerTutorialRewardRequest());
        int honeyBefore = progress.GetProgress(player).honey;

        SinglePlayerProgressData after = progress.GetProgress(player);

        Assert.Equal(TutorialModuleCatalog.Count, after.completedTutorialModules.Length);
        foreach (string moduleId in TutorialModuleCatalog.AllIds)
            Assert.Contains(moduleId, after.completedTutorialModules);
        Assert.Equal(honeyBefore, after.honey);
    }

    /// <summary>
    /// Il percorso e' finito ma non e' ancora stato riscosso l'ultimo modulo: il flag
    /// tutorial_completed non deve alzarsi da solo a meta' strada, perche' e' quello che
    /// apre taverna, biblioteca, profilo e arena.
    /// </summary>
    [Fact]
    public void The_tutorial_flag_stays_down_until_the_last_module()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("quasi");

        foreach (string moduleId in TutorialModuleCatalog.AllIds)
        {
            if (moduleId == "m0-basics")
                break;
            SinglePlayerRewardResult step = Claim(progress, player, moduleId);
            Assert.False(step.progress.tutorialCompleted);
        }
    }

    private static SinglePlayerRewardResult ClaimWholePath(
        SinglePlayerProgressService progress, AccountIdentity player)
    {
        SinglePlayerRewardResult last = null;
        foreach (string moduleId in TutorialModuleCatalog.AllIds)
            last = Claim(progress, player, moduleId);
        return last;
    }

    private static SinglePlayerRewardResult Claim(
        SinglePlayerProgressService progress, AccountIdentity player, string moduleId)
    {
        (SinglePlayerRewardResult result, string code, string error) =
            progress.ClaimTutorialModuleReward(
                player, new SinglePlayerTutorialModuleRequest { moduleId = moduleId });
        Assert.True(code == null, error);
        return result;
    }

    /// <summary>Simula la spesa del tour guidato senza passare dai requisiti del Santuario.</summary>
    private static void SpendOn(TestServer server, string playerId, int honey)
    {
        if (honey <= 0)
            return;

        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE single_player_progress SET honey = honey - $honey WHERE player_id = $player";
        command.Parameters.AddWithValue("$honey", honey);
        command.Parameters.AddWithValue("$player", playerId);
        command.ExecuteNonQuery();
    }

    private static List<(string ItemId, int Count)> ReadStash(TestServer server, string playerId)
    {
        var stash = new List<(string, int)>();
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT item_id, count FROM player_consumables WHERE player_id = $player AND count > 0";
        command.Parameters.AddWithValue("$player", playerId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            stash.Add((reader.GetString(0), reader.GetInt32(1)));
        return stash;
    }
}
