using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AccardND.Server.Tests;

public sealed class TavernQuestBalanceTests
{
    [Theory]
    [InlineData(TavernQuestDifficulty.Easy, 1)]
    [InlineData(TavernQuestDifficulty.Intermediate, 2)]
    [InlineData(TavernQuestDifficulty.Advanced, 3)]
    public void Honey_reward_matches_quest_difficulty(TavernQuestDifficulty difficulty, int expected)
    {
        Assert.Equal(expected, TavernQuests.HoneyRewardFor(difficulty));
    }

    /// <summary>Un anno di date, per provare le invarianti su tutte le estrazioni possibili.</summary>
    public static TheoryData<string> Days()
    {
        var days = new TheoryData<string>();
        for (var date = new DateTime(2026, 1, 1); date.Year == 2026; date = date.AddDays(1))
            days.Add(date.ToString("yyyy-MM-dd"));
        return days;
    }

    [Theory]
    [InlineData("2026-08-11")]
    [InlineData("2026-12-31")]
    [InlineData("2027-01-01")]
    public void Daily_board_has_balanced_difficulties_and_points(string day)
    {
        IReadOnlyList<TavernQuests.QuestDefinition> quests = TavernQuests.DefinitionsForDay(day);

        Assert.Equal(10, quests.Count);
        Assert.Equal(5, quests.Count(quest => quest.Difficulty == TavernQuestDifficulty.Easy));
        Assert.Equal(3, quests.Count(quest => quest.Difficulty == TavernQuestDifficulty.Intermediate));
        Assert.Equal(2, quests.Count(quest => quest.Difficulty == TavernQuestDifficulty.Advanced));
        Assert.Equal(17, quests.Sum(quest => quest.BonusPoints));
        Assert.Equal(quests.Count, quests.Select(quest => quest.CounterKey).Distinct().Count());
    }

    [Fact]
    public void Daily_board_is_deterministic()
    {
        string[] first = TavernQuests.DefinitionsForDay("2026-08-11").Select(quest => quest.Id).ToArray();
        string[] second = TavernQuests.DefinitionsForDay("2026-08-11").Select(quest => quest.Id).ToArray();

        Assert.Equal(first, second);
    }

    /// <summary>
    /// La bacheca resta piena e con le quote giuste tutti i giorni dell'anno: le quest sono
    /// l'unica fonte di miele, e una data che ne estrae nove pagherebbe meno delle altre.
    /// </summary>
    [Theory]
    [MemberData(nameof(Days))]
    public void Every_day_of_the_year_fills_the_board(string day)
    {
        IReadOnlyList<TavernQuests.QuestDefinition> quests = TavernQuests.DefinitionsForDay(day);

        Assert.Equal(TavernQuests.QuestsPerDay, quests.Count);
        Assert.Equal(quests.Count, quests.Select(quest => quest.CounterKey).Distinct().Count());
        Assert.Equal(17, quests.Sum(quest => quest.BonusPoints));
    }

    /// <summary>
    /// Il premio di giornata non deve mai passare dall'arena: chi gioca solo in campagna
    /// (o chi non trova avversari) deve poter fare i punti richiesti con le altre quest.
    /// </summary>
    [Theory]
    [MemberData(nameof(Days))]
    public void The_daily_bonus_is_always_reachable_without_pvp(string day)
    {
        IReadOnlyList<TavernQuests.QuestDefinition> quests = TavernQuests.DefinitionsForDay(day);

        int pointsWithoutPvp = quests.Where(quest => !quest.Pvp).Sum(quest => quest.BonusPoints);

        Assert.True(quests.Count(quest => quest.Pvp) <= 2, $"{day}: troppe quest d'arena.");
        Assert.True(pointsWithoutPvp >= TavernQuests.BonusPointsRequired,
            $"{day}: senza PvP si arriva a {pointsWithoutPvp} punti su {TavernQuests.BonusPointsRequired}.");
    }

    /// <summary>
    /// Ogni quest deve poter avanzare: un id che punta a un contatore che nessuno scrive
    /// resterebbe a zero per sempre e brucerebbe uno dei dieci posti della giornata.
    /// </summary>
    [Fact]
    public void Every_quest_points_at_a_counter_the_server_writes()
    {
        var known = new HashSet<string>
        {
            CampaignCounters.EnemiesDefeated, CampaignCounters.RoomsCleared, CampaignCounters.RunsEnded,
            CampaignCounters.BossesDefeated, CampaignCounters.MinibossesDefeated, CampaignCounters.DiceRolled,
            CampaignCounters.AbilitiesUsed, CampaignCounters.ItemsUsed, CampaignCounters.ExperienceEarned,
            CampaignCounters.SupremesUsed, CampaignCounters.QuickChallenges, CampaignCounters.MerchantPurchases,
            CampaignCounters.GoldEarned, CampaignCounters.LevelsGained,
            CampaignCounters.PvpMatches, CampaignCounters.PvpWins, CampaignCounters.PvpRoundsWon
        };

        foreach (TavernQuests.QuestDefinition quest in TavernQuests.AllDefinitions())
            Assert.True(known.Contains(quest.CounterKey), $"{quest.Id}: contatore '{quest.CounterKey}' sconosciuto.");
    }

    /// <summary>
    /// Deploy a giornata iniziata: chi aveva gia' aperto la taverna ha a database le righe
    /// dell'estrazione vecchia, e AssignIfMissing gli aggiunge quelle della nuova senza poter
    /// togliere le prime (toglierle brucerebbe il progresso di chi ha gia' giocato). La
    /// bacheca deve restare di dieci quest: e' successo davvero, e si vedevano diciotto.
    /// </summary>
    [Fact]
    public void A_catalog_change_mid_day_does_not_double_the_board()
    {
        using var server = new TestServer();
        var progress = new SinglePlayerProgressService(server.Database);
        AccountIdentity player = server.RegisterAccount("mattiniero");

        // La bacheca di oggi, come l'avrebbe vista prima del deploy.
        Assert.Equal(TavernQuests.QuestsPerDay, progress.GetTavern(player).quests.Length);

        // Le righe che l'estrazione precedente si sarebbe lasciata dietro: quest del catalogo
        // che pero' non sono fra quelle di oggi.
        var today = TavernQuests.DefinitionsForDay(TavernQuests.TodayKey())
            .Select(quest => quest.Id)
            .ToHashSet(StringComparer.Ordinal);
        string[] leftovers = TavernQuests.AllDefinitions()
            .Where(quest => !today.Contains(quest.Id))
            .Take(8)
            .Select(quest => quest.Id)
            .ToArray();
        Assert.Equal(8, leftovers.Length);
        foreach (string questId in leftovers)
            GiveTavernQuestRow(server, player.PlayerId, questId);

        TavernData board = progress.GetTavern(player);

        Assert.Equal(TavernQuests.QuestsPerDay, board.quests.Length);
        Assert.Equal(17, board.quests.Sum(quest => quest.bonusPoints));
        foreach (TavernQuestData quest in board.quests)
            Assert.Contains(quest.questId, today);
    }

    private static void GiveTavernQuestRow(TestServer server, string playerId, string questId)
    {
        using SqliteConnection connection = server.Database.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO player_tavern_quests (player_id, day, quest_id, baseline)
            VALUES ($player, $day, $quest, 0)";
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$day", TavernQuests.TodayKey());
        command.Parameters.AddWithValue("$quest", questId);
        command.ExecuteNonQuery();
    }

    [Fact]
    public void Quest_ids_are_unique()
    {
        IReadOnlyList<TavernQuests.QuestDefinition> all = TavernQuests.AllDefinitions();

        Assert.Equal(all.Count, all.Select(quest => quest.Id).Distinct().Count());
    }
}
