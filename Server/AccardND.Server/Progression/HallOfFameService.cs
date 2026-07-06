using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>Query di sola lettura sugli snapshot di fine stagione (Hall of Fame).</summary>
public sealed class HallOfFameService
{
    private readonly AccardDatabase database;

    public HallOfFameService(AccardDatabase database)
    {
        this.database = database;
    }

    /// <summary>Stagioni concluse che hanno uno snapshot, dalla più recente.</summary>
    public HallOfFameSeasonsData GetSeasons()
    {
        var seasons = new List<HallOfFameSeasonDto>();
        using SqliteConnection connection = database.Open();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT s.season_id, s.name, s.starts_at, s.ends_at, COUNT(h.player_id)
            FROM hall_of_fame h
            JOIN seasons s ON s.season_id = h.season_id
            GROUP BY s.season_id, s.name, s.starts_at, s.ends_at
            ORDER BY s.season_id DESC";
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            seasons.Add(new HallOfFameSeasonDto
            {
                seasonId = reader.GetInt32(0),
                name = reader.GetString(1),
                startedAt = reader.IsDBNull(2) ? null : reader.GetString(2),
                endedAt = reader.IsDBNull(3) ? null : reader.GetString(3),
                participants = reader.GetInt32(4)
            });
        }
        return new HallOfFameSeasonsData { seasons = seasons.ToArray() };
    }

    /// <summary>
    /// Classifica finale di una stagione (seasonId ≤ 0 = stagione conclusa più recente),
    /// più la riga personale del richiedente se fuori dai primi <paramref name="limit"/>.
    /// </summary>
    public HallOfFameData GetHallOfFame(int seasonId, AccountIdentity requester, int limit)
    {
        using SqliteConnection connection = database.Open();

        if (seasonId <= 0)
            seasonId = MostRecentSeason(connection);

        var data = new HallOfFameData
        {
            seasonId = seasonId,
            seasonName = ReadSeasonName(connection, seasonId),
            entries = ReadEntries(connection, seasonId, limit, out bool requesterInTop, requester?.PlayerId)
        };

        // Riga personale se il richiedente ha partecipato ma è oltre il limite mostrato.
        if (requester != null && !requesterInTop)
            data.you = ReadEntry(connection, seasonId, requester.PlayerId);

        return data;
    }

    private static int MostRecentSeason(SqliteConnection connection)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT COALESCE(MAX(season_id), 0) FROM hall_of_fame";
        return (int)(long)query.ExecuteScalar();
    }

    private static string ReadSeasonName(SqliteConnection connection, int seasonId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT name FROM seasons WHERE season_id=$id";
        query.Parameters.AddWithValue("$id", seasonId);
        return query.ExecuteScalar() as string ?? string.Empty;
    }

    private static HallOfFameEntry[] ReadEntries(
        SqliteConnection connection, int seasonId, int limit, out bool requesterInTop, string requesterId)
    {
        requesterInTop = false;
        var entries = new List<HallOfFameEntry>();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT h.final_rank, h.player_id, COALESCE(a.username, ''), h.final_tier, h.final_division,
                   h.final_mmr, h.wins, h.losses
            FROM hall_of_fame h
            LEFT JOIN accounts a ON a.player_id = h.player_id
            WHERE h.season_id=$season
            ORDER BY h.final_rank LIMIT $limit";
        query.Parameters.AddWithValue("$season", seasonId);
        query.Parameters.AddWithValue("$limit", limit);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            string playerId = reader.GetString(1);
            if (playerId == requesterId)
                requesterInTop = true;
            entries.Add(ToEntry(reader));
        }
        return entries.ToArray();
    }

    private static HallOfFameEntry ReadEntry(SqliteConnection connection, int seasonId, string playerId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT h.final_rank, h.player_id, COALESCE(a.username, ''), h.final_tier, h.final_division,
                   h.final_mmr, h.wins, h.losses
            FROM hall_of_fame h
            LEFT JOIN accounts a ON a.player_id = h.player_id
            WHERE h.season_id=$season AND h.player_id=$id";
        query.Parameters.AddWithValue("$season", seasonId);
        query.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        return reader.Read() ? ToEntry(reader) : null;
    }

    private static HallOfFameEntry ToEntry(SqliteDataReader reader) => new()
    {
        rank = reader.GetInt32(0),
        playerId = reader.GetString(1),
        username = reader.GetString(2),
        tier = reader.GetString(3),
        division = reader.GetString(4),
        finalMmr = reader.GetInt32(5),
        wins = reader.GetInt32(6),
        losses = reader.GetInt32(7)
    };
}
