using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>Query di sola lettura sugli aggregati player_stats per la UI.</summary>
public sealed class StatsService
{
    private readonly AccardDatabase database;
    private readonly SeasonService seasons;

    public StatsService(AccardDatabase database, SeasonService seasons)
    {
        this.database = database;
        this.seasons = seasons;
    }

    public StatsData GetStats(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        return new StatsData
        {
            playerId = identity.PlayerId,
            username = identity.Username,
            seasonId = seasons.ActiveSeasonId,
            seasonName = ReadSeasonName(connection, seasons.ActiveSeasonId),
            lifetime = ReadScope(connection, identity.PlayerId, "lifetime"),
            season = ReadScope(connection, identity.PlayerId, seasons.ActiveSeasonScope)
        };
    }

    private static string ReadSeasonName(SqliteConnection connection, int seasonId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT name FROM seasons WHERE season_id=$id";
        query.Parameters.AddWithValue("$id", seasonId);
        return query.ExecuteScalar() as string ?? string.Empty;
    }

    private static PlayerStatsDto ReadScope(SqliteConnection connection, string playerId, string scope)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT matches, wins, losses, forfeits, rounds_won, rounds_lost, current_streak, best_streak
            FROM player_stats WHERE player_id=$id AND scope=$scope";
        query.Parameters.AddWithValue("$id", playerId);
        query.Parameters.AddWithValue("$scope", scope);

        using SqliteDataReader reader = query.ExecuteReader();
        if (!reader.Read())
            return new PlayerStatsDto();

        int matches = reader.GetInt32(0);
        int wins = reader.GetInt32(1);
        return new PlayerStatsDto
        {
            matches = matches,
            wins = wins,
            losses = reader.GetInt32(2),
            forfeits = reader.GetInt32(3),
            roundsWon = reader.GetInt32(4),
            roundsLost = reader.GetInt32(5),
            currentStreak = reader.GetInt32(6),
            bestStreak = reader.GetInt32(7),
            winRatePercent = matches > 0 ? (int)Math.Round(wins * 100.0 / matches) : 0
        };
    }
}
