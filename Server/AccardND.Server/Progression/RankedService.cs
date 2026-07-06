using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>Tier/divisione/LP derivati da un MMR.</summary>
public sealed record RankedTierInfo(
    int TierIndex, string TierName, string Division, int LeaguePoints, int GlobalDivision);

/// <summary>Stato ranked corrente di un giocatore (per ranked.get).</summary>
public sealed record RankedProgress(
    bool Ranked, int Mmr, int GamesPlayed, bool PlacementDone, int PlacementRemaining, RankedTierInfo Tier);

/// <summary>Variazione ranked di un giocatore dopo una partita (per match.result).</summary>
public sealed record PlayerRankedDelta(
    RankedTierInfo Before, RankedTierInfo After,
    int LpDelta, bool Promoted, bool Demoted, bool Placement, int PlacementRemaining);

public sealed record ApplyMatchResult(PlayerRankedDelta A, PlayerRankedDelta B);

public sealed record LeaderboardRow(
    string PlayerId, string Username, int Mmr, int GamesPlayed, bool PlacementDone, RankedTierInfo Tier);

/// <summary>
/// MMR nascosto (Elo) e sua traduzione in tier a leghe. Le scritture avvengono
/// dentro la transazione del <see cref="MatchResultRecorder"/> per essere atomiche
/// con match_history e statistiche.
/// </summary>
public sealed class RankedService
{
    private readonly AccardDatabase database;
    private readonly RankedConfig config;

    public RankedService(AccardDatabase database, ServerConfig serverConfig)
    {
        this.database = database;
        config = serverConfig.Ranked;
    }

    /// <summary>Traduce un MMR in tier, divisione e punti lega.</summary>
    public RankedTierInfo Describe(int mmr)
    {
        int totalDivisions = config.Tiers.Length * config.DivisionsPerTier;
        int globalDivision = (int)Math.Floor((mmr - config.TierFloor) / (double)config.DivisionWidth);
        globalDivision = Math.Clamp(globalDivision, 0, totalDivisions - 1);

        int tierIndex = globalDivision / config.DivisionsPerTier;
        int divisionWithinTier = globalDivision % config.DivisionsPerTier;
        int divisionFloor = config.TierFloor + globalDivision * config.DivisionWidth;
        int leaguePoints = Math.Clamp(
            (int)Math.Round((mmr - divisionFloor) * 100.0 / config.DivisionWidth), 0, 100);

        return new RankedTierInfo(
            tierIndex,
            config.Tiers[tierIndex],
            ToRoman(config.DivisionsPerTier - divisionWithinTier),
            leaguePoints,
            globalDivision);
    }

    public RankedProgress GetProgress(string playerId, int seasonId)
    {
        using SqliteConnection connection = database.Open();
        (int mmr, int games, bool placementDone, bool exists) = ReadState(connection, null, playerId, seasonId);
        return new RankedProgress(
            exists, mmr, games, placementDone,
            Math.Max(0, config.PlacementMatches - games),
            Describe(mmr));
    }

    public IReadOnlyList<LeaderboardRow> GetLeaderboard(int seasonId, int limit)
    {
        var rows = new List<LeaderboardRow>();
        using SqliteConnection connection = database.Open();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT r.player_id, COALESCE(a.username, ''), r.mmr, r.games_played, r.placement_done
            FROM ranked_state r
            LEFT JOIN accounts a ON a.player_id = r.player_id
            WHERE r.season_id=$season
            ORDER BY r.mmr DESC, r.games_played ASC LIMIT $limit";
        query.Parameters.AddWithValue("$season", seasonId);
        query.Parameters.AddWithValue("$limit", limit);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            int mmr = reader.GetInt32(2);
            rows.Add(new LeaderboardRow(
                reader.GetString(0), reader.GetString(1), mmr,
                reader.GetInt32(3), reader.GetInt32(4) != 0, Describe(mmr)));
        }
        return rows;
    }

    /// <summary>
    /// Applica l'esito ranked ad entrambi i giocatori dentro una transazione esistente.
    /// Winner: 0 = A, 1 = B.
    /// </summary>
    public ApplyMatchResult ApplyMatch(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerAId, string playerBId, int winner, int seasonId)
    {
        (int aMmr, int aGames, bool aDone, _) = ReadState(connection, transaction, playerAId, seasonId);
        (int bMmr, int bGames, bool bDone, _) = ReadState(connection, transaction, playerBId, seasonId);

        bool aWon = winner == 0;
        int aNew = NextMmr(aMmr, bMmr, aWon, placement: !aDone);
        int bNew = NextMmr(bMmr, aMmr, !aWon, placement: !bDone);

        int aGamesNew = aGames + 1;
        int bGamesNew = bGames + 1;
        bool aDoneNew = aDone || aGamesNew >= config.PlacementMatches;
        bool bDoneNew = bDone || bGamesNew >= config.PlacementMatches;

        WriteState(connection, transaction, playerAId, seasonId, aNew, aGamesNew, aDoneNew);
        WriteState(connection, transaction, playerBId, seasonId, bNew, bGamesNew, bDoneNew);

        return new ApplyMatchResult(
            BuildDelta(aMmr, aNew, aGamesNew, aDoneNew),
            BuildDelta(bMmr, bNew, bGamesNew, bDoneNew));
    }

    private PlayerRankedDelta BuildDelta(int mmrBefore, int mmrAfter, int gamesAfter, bool doneAfter)
    {
        RankedTierInfo before = Describe(mmrBefore);
        RankedTierInfo after = Describe(mmrAfter);
        int lpDelta = (int)Math.Round((mmrAfter - mmrBefore) * 100.0 / config.DivisionWidth);
        return new PlayerRankedDelta(
            before, after, lpDelta,
            after.GlobalDivision > before.GlobalDivision,
            after.GlobalDivision < before.GlobalDivision,
            !doneAfter,
            Math.Max(0, config.PlacementMatches - gamesAfter));
    }

    private int NextMmr(int mmr, int opponentMmr, bool won, bool placement)
    {
        double expected = 1.0 / (1.0 + Math.Pow(10, (opponentMmr - mmr) / 400.0));
        int k = placement ? config.PlacementK : config.StandardK;
        double next = mmr + k * ((won ? 1.0 : 0.0) - expected);
        return Math.Max(0, (int)Math.Round(next));
    }

    private (int Mmr, int Games, bool PlacementDone, bool Exists) ReadState(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int seasonId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT mmr, games_played, placement_done FROM ranked_state WHERE player_id=$id AND season_id=$season";
        query.Parameters.AddWithValue("$id", playerId);
        query.Parameters.AddWithValue("$season", seasonId);
        using SqliteDataReader reader = query.ExecuteReader();
        if (!reader.Read())
            return (config.StartMmr, 0, false, false);
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2) != 0, true);
    }

    private void WriteState(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, int seasonId, int mmr, int games, bool placementDone)
    {
        using SqliteCommand upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = @"
            INSERT INTO ranked_state (player_id, season_id, mmr, games_played, placement_done, peak_mmr, updated_at)
            VALUES ($id, $season, $mmr, $games, $done, $mmr, $now)
            ON CONFLICT(player_id, season_id) DO UPDATE SET
                mmr = excluded.mmr,
                games_played = excluded.games_played,
                placement_done = excluded.placement_done,
                peak_mmr = MAX(ranked_state.peak_mmr, excluded.mmr),
                updated_at = excluded.updated_at";
        upsert.Parameters.AddWithValue("$id", playerId);
        upsert.Parameters.AddWithValue("$season", seasonId);
        upsert.Parameters.AddWithValue("$mmr", mmr);
        upsert.Parameters.AddWithValue("$games", games);
        upsert.Parameters.AddWithValue("$done", placementDone ? 1 : 0);
        upsert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        upsert.ExecuteNonQuery();
    }

    private static string ToRoman(int value)
    {
        return value switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            8 => "VIII",
            9 => "IX",
            10 => "X",
            _ => value.ToString()
        };
    }
}
