using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AccardND.Server.Tests;

/// <summary>
/// La campagna a sette capitoli: la catena degli sblocchi, le classi premio e - soprattutto -
/// la rinumerazione della progressione di chi stava gia' giocando. Quest'ultima e' l'unica
/// parte del restyling che puo' distruggere dati veri, quindi e' quella coperta di piu'.
/// </summary>
public sealed class ChapterProgressionTests
{
    [Fact]
    public void Clearing_a_chapter_grants_its_reward_class_and_opens_the_next()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("scalatore");

        SinglePlayerProgressData after = Clear(progress, player, "trentor");

        Assert.Contains("chapter-1", after.clearedChapters);
        Assert.Contains("chapter-2", after.unlockedChapters);
        Assert.Contains("hunter", after.unlockedClasses);
    }

    [Fact]
    public void Clearing_the_last_playable_chapter_grants_no_class_and_no_next()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("finalista");

        SinglePlayerProgressData after = Clear(progress, player, "boss-palatir");

        Assert.Contains("chapter-7", after.clearedChapters);
        // Il settimo chiude la campagna: il suo premio e' il cosmetico dei dadi, che non e'
        // un unlock di progressione e non deve comparire qui.
        Assert.DoesNotContain(after.unlockedChapters, id => id == "chapter-8");
    }

    /// <summary>
    /// Con i boss di 3, 4 e 5 ancora da scrivere, fermarsi al capitolo successivo lascerebbe
    /// il giocatore davanti a una porta che non si apre. La catena deve arrivare fino al
    /// primo capitolo giocabile, senza saltare quelli in mezzo.
    /// </summary>
    [Fact]
    public void The_chain_reaches_the_next_playable_chapter_without_skipping_the_dark_ones()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("nebbioso");

        SinglePlayerProgressData after = Clear(progress, player, "boss-bragus");

        Assert.Contains("chapter-3", after.unlockedChapters);
        Assert.Contains("chapter-4", after.unlockedChapters);
        Assert.Contains("chapter-5", after.unlockedChapters);
        Assert.Contains("chapter-6", after.unlockedChapters);
		// Il sesto e' il gate in attesa del nuovo boss: non si deve saltare al settimo.
        Assert.DoesNotContain("chapter-7", after.unlockedChapters);
    }

    [Fact]
    public void Clearing_the_same_chapter_twice_changes_nothing()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("ripetente");

        Clear(progress, player, "trentor");
        SinglePlayerProgressData after = Clear(progress, player, "trentor");

        Assert.Single(after.clearedChapters, id => id == "chapter-1");
        Assert.Single(after.unlockedClasses, id => id == "hunter");
    }

    /// <summary>
    /// I capitoli non sono in vendita: si aprono battendo il boss del capitolo precedente.
    /// Il Santuario li ha venduti per un periodo, quindi la richiesta d'acquisto puo' ancora
    /// arrivare da un client vecchio, e deve essere rifiutata senza scalare miele.
    /// </summary>
    [Theory]
    [InlineData("chapter-2")]
    [InlineData("chapter-3")]
    [InlineData("chapter-7")]
    public void A_chapter_is_not_for_sale(string chapterId)
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("compratore");
        progress.ClaimTutorialReward(player, new SinglePlayerTutorialRewardRequest());
        GiveHoney(server, player.PlayerId, 1000);

        (_, string code, string error) = progress.PurchaseUnlock(
            player, new SinglePlayerPurchaseUnlockRequest { type = "chapter", id = chapterId });

        Assert.Equal(ErrorCodes.InvalidProgressionRequest, code);
        // Il rifiuto deve dire come si ottiene davvero: il miele c'e', non e' un problema
        // di prezzo, e un messaggio generico manderebbe il giocatore a cercare un banco.
        Assert.Contains("battendo il boss", error);

        SinglePlayerProgressData after = progress.GetProgress(player);
        Assert.DoesNotContain(chapterId, after.unlockedChapters);
        Assert.Equal(1000, after.honey);
    }

    [Fact]
    public void Migration_moves_old_chapters_to_their_new_position()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("veterano");

        // Com'era prima del restyling: Nebbia (1) e Rampicanti (2) completati, Specchi (3)
        // comprato ma mai finito.
        GiveUnlock(server, player.PlayerId, "chapterCleared", "chapter-1");
        GiveUnlock(server, player.PlayerId, "chapterCleared", "chapter-2");
        GiveUnlock(server, player.PlayerId, "chapter", "chapter-1");
        GiveUnlock(server, player.PlayerId, "chapter", "chapter-2");
        GiveUnlock(server, player.PlayerId, "chapter", "chapter-3");

        ChapterRemapMigration.RunIfNeeded(server.Database, NullLogger.Instance);
        SinglePlayerProgressData after = new SinglePlayerProgressService(server.Database).GetProgress(player);

        // La Nebbia era il primo capitolo e ora e' il secondo; i Rampicanti il contrario.
        // Entrambi restano completati, perche' entrambi sono stati giocati davvero.
        Assert.Contains("chapter-1", after.clearedChapters);
        Assert.Contains("chapter-2", after.clearedChapters);
        // Gli Specchi erano il terzo e sono il sesto: comprato, non completato.
        Assert.Contains("chapter-6", after.unlockedChapters);
        Assert.DoesNotContain("chapter-6", after.clearedChapters);
    }

    [Fact]
    public void Migration_leaves_no_locked_chapter_under_an_open_one()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("saltato");

        // Aveva solo gli Specchi (vecchio 3, nuovo 6): dopo la rinumerazione i capitoli 1-5
        // sarebbero rimasti chiusi sotto un capitolo aperto.
        GiveUnlock(server, player.PlayerId, "chapter", "chapter-3");

        ChapterRemapMigration.RunIfNeeded(server.Database, NullLogger.Instance);
        SinglePlayerProgressData after = new SinglePlayerProgressService(server.Database).GetProgress(player);

        for (int number = 1; number <= 6; number++)
            Assert.Contains($"chapter-{number}", after.unlockedChapters);
        Assert.DoesNotContain("chapter-7", after.unlockedChapters);
        // Il riempimento da' accesso, non vittorie: nessun capitolo risulta completato.
        Assert.Empty(after.clearedChapters);
    }

    /// <summary>
    /// Il punto piu' facile da sbagliare: chi ha battuto Medusa arriva al sesto capitolo, e
    /// il riempimento gli apre i cinque prima. Le classi premio devono seguire i capitoli
    /// davvero completati, non quelli aperti dal riempimento, altrimenti una sola vittoria
    /// consegna sei classi e svuota meta' Santuario.
    /// </summary>
    [Fact]
    public void Migration_grants_only_the_classes_of_chapters_actually_cleared()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("medusiano");

        GiveUnlock(server, player.PlayerId, "chapter", "chapter-3");
        GiveUnlock(server, player.PlayerId, "chapterCleared", "chapter-3");

        ChapterRemapMigration.RunIfNeeded(server.Database, NullLogger.Instance);
        SinglePlayerProgressData after = new SinglePlayerProgressService(server.Database).GetProgress(player);

        Assert.Contains("paladin", after.unlockedClasses);
        Assert.DoesNotContain("hunter", after.unlockedClasses);
        Assert.DoesNotContain("barbarian", after.unlockedClasses);
        Assert.DoesNotContain("necromancer", after.unlockedClasses);
    }

    [Fact]
    public void Migration_runs_once_even_across_restarts()
    {
        using var server = new TestServer();
        AccountIdentity player = server.RegisterAccount("riavviato");
        GiveUnlock(server, player.PlayerId, "chapter", "chapter-1");
        GiveUnlock(server, player.PlayerId, "chapterCleared", "chapter-1");

        ChapterRemapMigration.RunIfNeeded(server.Database, NullLogger.Instance);
        SinglePlayerProgressData first = new SinglePlayerProgressService(server.Database).GetProgress(player);

        // Rigirarla scambierebbe di nuovo primo e secondo capitolo, riportando la Nebbia
        // dove non deve stare. La chiave in server_settings esiste per impedirlo.
        ChapterRemapMigration.RunIfNeeded(server.Database, NullLogger.Instance);
        SinglePlayerProgressData second = new SinglePlayerProgressService(server.Database).GetProgress(player);

        Assert.Equal(first.clearedChapters, second.clearedChapters);
        Assert.Contains("chapter-2", second.clearedChapters);
        Assert.DoesNotContain("chapter-1", second.clearedChapters);
    }

    [Fact]
    public void Every_advanced_class_is_the_reward_of_exactly_one_chapter()
    {
        string[] rewards = ChapterCatalog.All
            .Select(chapter => chapter.RewardClassId)
            .Where(id => id != null)
            .ToArray();

        Assert.Equal(rewards.Length, rewards.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (string classId in rewards)
        {
            Assert.True(
                SanctuaryCatalog.TryGetEntry(SanctuaryCatalog.TypeClass, classId, out _),
                $"La classe premio '{classId}' non esiste nel Santuario.");
        }
    }

    private static SinglePlayerProgressData Clear(
        SinglePlayerProgressService progress, AccountIdentity player, string bossId)
    {
        (SinglePlayerProgressData data, string code, string error) = progress.ClearChapter(
            player, new SinglePlayerClearChapterRequest { bossId = bossId });
        Assert.True(code == null, error);
        return data;
    }

    private static void GiveHoney(TestServer server, string playerId, int honey)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO single_player_progress (player_id, honey, updated_at)
            VALUES ($player, $honey, $now)
            ON CONFLICT(player_id) DO UPDATE SET honey = excluded.honey";
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$honey", honey);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void GiveUnlock(TestServer server, string playerId, string type, string id)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO single_player_unlocks (player_id, unlock_type, unlock_id, unlocked_at)
            VALUES ($player, $type, $id, $now)";
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
