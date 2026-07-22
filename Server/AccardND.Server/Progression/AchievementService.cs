using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Traguardi valutati a fine partita sugli aggregati del giocatore. Lo sblocco è
/// permanente (resta anche dopo il soft reset) e può concedere un'icona premio.
/// Metriche: wins, matches, best_streak (da player_stats lifetime), tier (indice
/// tier di picco della stagione attiva).
/// </summary>
public sealed class AchievementService
{
    private sealed record Definition(
        string Id, string Name, string Description, string Metric, int Threshold, string RewardIcon);

    private static readonly Definition[] Catalog =
    {
        new("ach-first-win", "Prima vittoria", "Vinci la tua prima partita.", "wins", 1, null),
        new("ach-wins-10", "Sfidante", "Vinci 10 partite.", "wins", 10, null),
        new("ach-wins-50", "Veterano", "Vinci 50 partite.", "wins", 50, "ach-veteran"),
        new("ach-matches-100", "Instancabile", "Gioca 100 partite.", "matches", 100, null),
        new("ach-streak-5", "Inarrestabile", "Vinci 5 partite di fila.", "best_streak", 5, "ach-streak"),
        new("ach-tier-esperto", "Esperto riconosciuto", "Raggiungi il tier Esperto.", "tier", 2, null),
        new("ach-boss-medusa", "Boss sconfitto", "Sconfiggi Medusa alla fine della campagna.", "campaign_boss", 1, "boss-medusa"),
        new("ach-tier-onnipotente", "Divinità", "Raggiungi il tier Onnipotente.", "tier", 4, "ach-onnipotente")
    };

    private readonly AccardDatabase database;
    private readonly RankedService ranked;
    private readonly UnlockService unlocks;

    public AchievementService(AccardDatabase database, RankedService ranked, UnlockService unlocks)
    {
        this.database = database;
        this.ranked = ranked;
        this.unlocks = unlocks;
        SeedCatalog();
    }

    /// <summary>
    /// Valuta i traguardi del giocatore dentro una transazione esistente e ritorna
    /// i nomi di quelli appena sbloccati (concedendo le icone premio).
    /// </summary>
    public IReadOnlyList<string> EvaluateAfterMatch(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int seasonId)
    {
        (int wins, int matches, int bestStreak) = ReadLifetime(connection, transaction, playerId);
        int peakTierIndex = ReadPeakTierIndex(connection, transaction, playerId, seasonId);
        HashSet<string> alreadyUnlocked = ReadUnlocked(connection, transaction, playerId);

        var newlyUnlocked = new List<string>();
        string now = DateTime.UtcNow.ToString("O");

        foreach (Definition def in Catalog)
        {
            int value = def.Metric switch
            {
                "wins" => wins,
                "matches" => matches,
                "best_streak" => bestStreak,
                "tier" => peakTierIndex,
                "campaign_boss" => alreadyUnlocked.Contains(def.Id) ? def.Threshold : 0,
                _ => 0
            };

            int progress = Math.Clamp(value, 0, def.Threshold);
            bool nowUnlocked = !alreadyUnlocked.Contains(def.Id) && value >= def.Threshold;

            using (SqliteCommand upsert = connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = @"
                    INSERT INTO player_achievements (player_id, achievement_id, progress, unlocked_at)
                    VALUES ($id, $ach, $progress, $unlocked)
                    ON CONFLICT(player_id, achievement_id) DO UPDATE SET
                        progress = $progress,
                        unlocked_at = COALESCE(player_achievements.unlocked_at, $unlocked)";
                upsert.Parameters.AddWithValue("$id", playerId);
                upsert.Parameters.AddWithValue("$ach", def.Id);
                upsert.Parameters.AddWithValue("$progress", progress);
                upsert.Parameters.AddWithValue("$unlocked", nowUnlocked ? now : (object)DBNull.Value);
                upsert.ExecuteNonQuery();
            }

            if (nowUnlocked)
            {
                unlocks.GrantIcon(connection, transaction, playerId, def.RewardIcon, "achievement");
                newlyUnlocked.Add(def.Name);
            }
        }

        return newlyUnlocked;
    }

    public AchievementsData GetAchievements(AccountIdentity identity)
    {
        var owned = new Dictionary<string, (int Progress, bool Unlocked)>();
        using SqliteConnection connection = database.Open();
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText =
                "SELECT achievement_id, progress, unlocked_at FROM player_achievements WHERE player_id=$id";
            query.Parameters.AddWithValue("$id", identity.PlayerId);
            using SqliteDataReader reader = query.ExecuteReader();
            while (reader.Read())
                owned[reader.GetString(0)] = (reader.GetInt32(1), !reader.IsDBNull(2));
        }

        var list = new AchievementDto[Catalog.Length];
        for (int index = 0; index < Catalog.Length; index++)
        {
            Definition def = Catalog[index];
            owned.TryGetValue(def.Id, out (int Progress, bool Unlocked) state);
            list[index] = new AchievementDto
            {
                achievementId = def.Id,
                name = def.Name,
                description = def.Description,
                progress = state.Progress,
                threshold = def.Threshold,
                unlocked = state.Unlocked
            };
        }
        return new AchievementsData { achievements = list };
    }

    public IReadOnlyList<string> UnlockCampaignBossVictory(AccountIdentity identity, IEnumerable<string> bosses)
    {
        if (bosses == null || !bosses.Any(boss =>
                string.Equals(boss?.Trim(), "boss-medusa", StringComparison.OrdinalIgnoreCase)))
            return Array.Empty<string>();

        Definition def = Catalog.First(item => item.Id == "ach-boss-medusa");
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        HashSet<string> alreadyUnlocked = ReadUnlocked(connection, transaction, identity.PlayerId);
        bool nowUnlocked = !alreadyUnlocked.Contains(def.Id);

        using (SqliteCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = @"
                INSERT INTO player_achievements (player_id, achievement_id, progress, unlocked_at)
                VALUES ($id, $ach, $progress, $unlocked)
                ON CONFLICT(player_id, achievement_id) DO UPDATE SET
                    progress = $progress,
                    unlocked_at = COALESCE(player_achievements.unlocked_at, $unlocked)";
            upsert.Parameters.AddWithValue("$id", identity.PlayerId);
            upsert.Parameters.AddWithValue("$ach", def.Id);
            upsert.Parameters.AddWithValue("$progress", def.Threshold);
            upsert.Parameters.AddWithValue("$unlocked", nowUnlocked ? DateTime.UtcNow.ToString("O") : (object)DBNull.Value);
            upsert.ExecuteNonQuery();
        }

        if (nowUnlocked)
            unlocks.GrantIcon(connection, transaction, identity.PlayerId, def.RewardIcon, "achievement");

        transaction.Commit();
        return nowUnlocked ? new[] { def.Name } : Array.Empty<string>();
    }

    private static (int Wins, int Matches, int BestStreak) ReadLifetime(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT wins, matches, best_streak FROM player_stats WHERE player_id=$id AND scope='lifetime'";
        query.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)) : (0, 0, 0);
    }

    private int ReadPeakTierIndex(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int seasonId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT peak_mmr FROM ranked_state WHERE player_id=$id AND season_id=$season";
        query.Parameters.AddWithValue("$id", playerId);
        query.Parameters.AddWithValue("$season", seasonId);
        object peak = query.ExecuteScalar();
        return peak == null ? -1 : ranked.Describe((int)(long)peak).TierIndex;
    }

    private static HashSet<string> ReadUnlocked(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        var unlocked = new HashSet<string>();
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT achievement_id FROM player_achievements WHERE player_id=$id AND unlocked_at IS NOT NULL";
        query.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
            unlocked.Add(reader.GetString(0));
        return unlocked;
    }

    private void SeedCatalog()
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        int order = 0;
        foreach (Definition def in Catalog)
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO achievements (achievement_id, name, description, metric, threshold, reward_icon, sort_order)
                VALUES ($id, $name, $desc, $metric, $threshold, $reward, $order)
                ON CONFLICT(achievement_id) DO UPDATE SET
                    name = excluded.name, description = excluded.description,
                    metric = excluded.metric, threshold = excluded.threshold,
                    reward_icon = excluded.reward_icon, sort_order = excluded.sort_order";
            insert.Parameters.AddWithValue("$id", def.Id);
            insert.Parameters.AddWithValue("$name", def.Name);
            insert.Parameters.AddWithValue("$desc", def.Description);
            insert.Parameters.AddWithValue("$metric", def.Metric);
            insert.Parameters.AddWithValue("$threshold", def.Threshold);
            insert.Parameters.AddWithValue("$reward", (object)def.RewardIcon ?? DBNull.Value);
            insert.Parameters.AddWithValue("$order", order++);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
