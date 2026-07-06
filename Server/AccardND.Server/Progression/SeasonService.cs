using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Gestisce la stagione attiva e il rollover trimestrale: a scadenza archivia la
/// classifica finale nella Hall of Fame, applica il soft reset dell'MMR e apre una
/// nuova stagione. Il lifetime delle statistiche resta; la nuova stagione riparte
/// con uno scope nuovo.
/// </summary>
public sealed class SeasonService
{
    private readonly AccardDatabase database;
    private readonly RankedService ranked;
    private readonly UnlockService unlocks;
    private readonly SeasonConfig seasonConfig;
    private readonly int startMmr;
    private readonly object gate = new();

    public SeasonService(AccardDatabase database, ServerConfig config, RankedService ranked, UnlockService unlocks)
    {
        this.database = database;
        this.ranked = ranked;
        this.unlocks = unlocks;
        seasonConfig = config.Season;
        startMmr = config.Ranked.StartMmr;
        EnsureActiveSeason();
    }

    /// <summary>Id della stagione attualmente attiva.</summary>
    public int ActiveSeasonId { get; private set; }

    /// <summary>Nome della stagione attiva.</summary>
    public string ActiveSeasonName { get; private set; }

    /// <summary>Chiave di scope per le statistiche della stagione attiva.</summary>
    public string ActiveSeasonScope => $"season:{ActiveSeasonId}";

    /// <summary>
    /// Se la stagione attiva è scaduta, esegue il rollover. Idempotente e thread-safe.
    /// Ritorna true se ha ruotato.
    /// </summary>
    public bool RolloverIfDue(DateTime nowUtc)
    {
        lock (gate)
        {
            using SqliteConnection connection = database.Open();

            int oldSeasonId;
            using (SqliteCommand query = connection.CreateCommand())
            {
                query.CommandText =
                    "SELECT season_id, ends_at FROM seasons WHERE is_active=1 ORDER BY season_id DESC LIMIT 1";
                using SqliteDataReader reader = query.ExecuteReader();
                if (!reader.Read() || reader.IsDBNull(1))
                    return false;
                DateTime endsAt = DateTime.Parse(
                    reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);
                if (endsAt > nowUtc)
                    return false;
                oldSeasonId = reader.GetInt32(0);
            }

            using SqliteTransaction transaction = connection.BeginTransaction();
            SnapshotHallOfFame(connection, transaction, oldSeasonId, nowUtc);
            int newSeasonId = OpenNextSeason(connection, transaction, oldSeasonId, nowUtc, out string newName);
            SoftResetLadder(connection, transaction, oldSeasonId, newSeasonId, nowUtc);
            transaction.Commit();

            ActiveSeasonId = newSeasonId;
            ActiveSeasonName = newName;
            return true;
        }
    }

    private void SnapshotHallOfFame(
        SqliteConnection connection, SqliteTransaction transaction, int seasonId, DateTime nowUtc)
    {
        var ladder = new List<(string PlayerId, int Mmr)>();
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText =
                "SELECT player_id, mmr FROM ranked_state WHERE season_id=$season ORDER BY mmr DESC, games_played ASC";
            query.Parameters.AddWithValue("$season", seasonId);
            using SqliteDataReader reader = query.ExecuteReader();
            while (reader.Read())
                ladder.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        string now = nowUtc.ToString("O");
        int rank = 1;
        foreach ((string playerId, int mmr) in ladder)
        {
            RankedTierInfo tier = ranked.Describe(mmr);
            (int wins, int losses) = ReadSeasonRecord(connection, transaction, playerId, seasonId);
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR IGNORE INTO hall_of_fame
                    (season_id, player_id, final_rank, final_tier, final_division, final_mmr, wins, losses, snapshot_at)
                VALUES ($season, $id, $rank, $tier, $div, $mmr, $wins, $losses, $now)";
            insert.Parameters.AddWithValue("$season", seasonId);
            insert.Parameters.AddWithValue("$id", playerId);
            insert.Parameters.AddWithValue("$rank", rank);
            insert.Parameters.AddWithValue("$tier", tier.TierName);
            insert.Parameters.AddWithValue("$div", tier.Division);
            insert.Parameters.AddWithValue("$mmr", mmr);
            insert.Parameters.AddWithValue("$wins", wins);
            insert.Parameters.AddWithValue("$losses", losses);
            insert.Parameters.AddWithValue("$now", now);
            insert.ExecuteNonQuery();

            unlocks.GrantHallOfFameIcons(connection, transaction, playerId, rank);
            rank++;
        }
    }

    private int OpenNextSeason(
        SqliteConnection connection, SqliteTransaction transaction,
        int oldSeasonId, DateTime nowUtc, out string newName)
    {
        using (SqliteCommand deactivate = connection.CreateCommand())
        {
            deactivate.Transaction = transaction;
            deactivate.CommandText = "UPDATE seasons SET is_active=0 WHERE season_id=$id";
            deactivate.Parameters.AddWithValue("$id", oldSeasonId);
            deactivate.ExecuteNonQuery();
        }

        int seasonNumber;
        using (SqliteCommand count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM seasons";
            seasonNumber = (int)(long)count.ExecuteScalar() + 1;
        }

        newName = $"Stagione {seasonNumber}";
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            "INSERT INTO seasons (name, starts_at, ends_at, is_active) VALUES ($name, $start, $end, 1) RETURNING season_id";
        insert.Parameters.AddWithValue("$name", newName);
        insert.Parameters.AddWithValue("$start", nowUtc.ToString("O"));
        insert.Parameters.AddWithValue("$end", nowUtc.AddDays(seasonConfig.DurationDays).ToString("O"));
        return (int)(long)insert.ExecuteScalar();
    }

    private void SoftResetLadder(
        SqliteConnection connection, SqliteTransaction transaction,
        int oldSeasonId, int newSeasonId, DateTime nowUtc)
    {
        var carry = new List<(string PlayerId, int Mmr)>();
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = "SELECT player_id, mmr FROM ranked_state WHERE season_id=$season";
            query.Parameters.AddWithValue("$season", oldSeasonId);
            using SqliteDataReader reader = query.ExecuteReader();
            while (reader.Read())
                carry.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        string now = nowUtc.ToString("O");
        foreach ((string playerId, int oldMmr) in carry)
        {
            int newMmr = (int)Math.Round(startMmr + (oldMmr - startMmr) * seasonConfig.SoftResetFactor);
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR IGNORE INTO ranked_state
                    (player_id, season_id, mmr, games_played, placement_done, peak_mmr, updated_at)
                VALUES ($id, $season, $mmr, 0, 0, $mmr, $now)";
            insert.Parameters.AddWithValue("$id", playerId);
            insert.Parameters.AddWithValue("$season", newSeasonId);
            insert.Parameters.AddWithValue("$mmr", newMmr);
            insert.Parameters.AddWithValue("$now", now);
            insert.ExecuteNonQuery();
        }
    }

    private static (int Wins, int Losses) ReadSeasonRecord(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int seasonId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT wins, losses FROM player_stats WHERE player_id=$id AND scope=$scope";
        query.Parameters.AddWithValue("$id", playerId);
        query.Parameters.AddWithValue("$scope", $"season:{seasonId}");
        using SqliteDataReader reader = query.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), reader.GetInt32(1)) : (0, 0);
    }

    private void EnsureActiveSeason()
    {
        using SqliteConnection connection = database.Open();

        using (SqliteCommand query = connection.CreateCommand())
        {
            query.CommandText =
                "SELECT season_id, name FROM seasons WHERE is_active=1 ORDER BY season_id DESC LIMIT 1";
            using SqliteDataReader reader = query.ExecuteReader();
            if (reader.Read())
            {
                ActiveSeasonId = reader.GetInt32(0);
                ActiveSeasonName = reader.GetString(1);
                return;
            }
        }

        DateTime now = DateTime.UtcNow;
        ActiveSeasonName = "Stagione 1";
        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO seasons (name, starts_at, ends_at, is_active) VALUES ($name, $start, $end, 1) RETURNING season_id";
        insert.Parameters.AddWithValue("$name", ActiveSeasonName);
        insert.Parameters.AddWithValue("$start", now.ToString("O"));
        insert.Parameters.AddWithValue("$end", now.AddDays(seasonConfig.DurationDays).ToString("O"));
        ActiveSeasonId = (int)(long)insert.ExecuteScalar();
    }
}
