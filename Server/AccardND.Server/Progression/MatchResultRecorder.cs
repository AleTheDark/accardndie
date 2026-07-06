using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Registra l'esito di una partita: una riga in match_history e gli aggregati
/// player_stats (lifetime + stagione) in un'unica transazione. Punto di aggancio
/// che nelle fasi successive orchestrerà anche MMR/rank e sblocchi.
/// </summary>
/// <summary>Esito della registrazione, per comporre i messaggi match.result.</summary>
public sealed record MatchRecordResult(
    bool Ranked, PlayerRankedDelta A, PlayerRankedDelta B,
    IReadOnlyList<string> AchievementsA, IReadOnlyList<string> AchievementsB)
{
    private static readonly string[] None = Array.Empty<string>();
    public static readonly MatchRecordResult Unranked = new(false, null, null, None, None);
}

public sealed class MatchResultRecorder
{
    private static readonly string[] ForfeitReasons = { "forfeit", "timeout", "disconnect" };

    private readonly AccardDatabase database;
    private readonly SeasonService seasons;
    private readonly RankedService ranked;
    private readonly UnlockService unlocks;
    private readonly AchievementService achievements;
    private readonly ILogger<MatchResultRecorder> logger;

    public MatchResultRecorder(
        AccardDatabase database, SeasonService seasons, RankedService ranked,
        UnlockService unlocks, AchievementService achievements, ILogger<MatchResultRecorder> logger)
    {
        this.database = database;
        this.seasons = seasons;
        this.ranked = ranked;
        this.unlocks = unlocks;
        this.achievements = achievements;
        this.logger = logger;
    }

    public Task<MatchRecordResult> RecordAsync(MatchOutcome outcome)
    {
        try
        {
            return Task.FromResult(Record(outcome));
        }
        catch (Exception exception)
        {
            // Il match è già concluso lato client: un errore di persistenza non deve propagarsi.
            logger.LogError(exception, "Registrazione esito match fallita ({Reason}).", outcome.EndedReason);
            throw;
        }
    }

    private MatchRecordResult Record(MatchOutcome outcome)
    {
        int seasonId = seasons.ActiveSeasonId;
        string seasonScope = seasons.ActiveSeasonScope;
        bool forfeit = Array.IndexOf(ForfeitReasons, outcome.EndedReason) >= 0;
        int seconds = Math.Max(0, (int)(outcome.EndedAt - outcome.StartedAt).TotalSeconds);

        bool aWon = outcome.Winner == 0;
        bool bWon = outcome.Winner == 1;
        bool aLost = outcome.Winner == 1;
        bool bLost = outcome.Winner == 0;

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO match_history
                    (season_id, room_code, ranked, player_a, player_b, winner,
                     score_a, score_b, ended_reason, started_at, ended_at)
                VALUES ($season, $room, $ranked, $a, $b, $winner,
                        $sa, $sb, $reason, $started, $ended)";
            insert.Parameters.AddWithValue("$season", seasonId);
            insert.Parameters.AddWithValue("$room", (object)outcome.RoomCode ?? DBNull.Value);
            insert.Parameters.AddWithValue("$ranked", outcome.Ranked ? 1 : 0);
            insert.Parameters.AddWithValue("$a", outcome.PlayerA.PlayerId);
            insert.Parameters.AddWithValue("$b", outcome.PlayerB.PlayerId);
            insert.Parameters.AddWithValue("$winner", outcome.Winner);
            insert.Parameters.AddWithValue("$sa", outcome.ScoreA);
            insert.Parameters.AddWithValue("$sb", outcome.ScoreB);
            insert.Parameters.AddWithValue("$reason", outcome.EndedReason);
            insert.Parameters.AddWithValue("$started", outcome.StartedAt.ToString("O"));
            insert.Parameters.AddWithValue("$ended", outcome.EndedAt.ToString("O"));
            insert.ExecuteNonQuery();
        }

        foreach (string scope in new[] { "lifetime", seasonScope })
        {
            UpdateScope(connection, transaction, outcome.PlayerA.PlayerId, scope,
                aWon, aLost, forfeit && aLost, outcome.ScoreA, outcome.ScoreB, seconds);
            UpdateScope(connection, transaction, outcome.PlayerB.PlayerId, scope,
                bWon, bLost, forfeit && bLost, outcome.ScoreB, outcome.ScoreA, seconds);
        }

        PlayerRankedDelta deltaA = null;
        PlayerRankedDelta deltaB = null;
        bool isRanked = outcome.Ranked && outcome.Winner is 0 or 1;
        if (isRanked)
        {
            ApplyMatchResult applied = ranked.ApplyMatch(
                connection, transaction,
                outcome.PlayerA.PlayerId, outcome.PlayerB.PlayerId, outcome.Winner, seasonId);
            deltaA = applied.A;
            deltaB = applied.B;

            // Le icone-tier si sbloccano solo a piazzamento concluso (tier "raggiunto").
            if (!applied.A.Placement)
                unlocks.GrantTierIcons(connection, transaction, outcome.PlayerA.PlayerId, applied.A.After.TierIndex);
            if (!applied.B.Placement)
                unlocks.GrantTierIcons(connection, transaction, outcome.PlayerB.PlayerId, applied.B.After.TierIndex);
        }

        // Gli achievement si valutano sempre (anche nelle amichevoli): usano gli aggregati appena scritti.
        IReadOnlyList<string> achievementsA =
            achievements.EvaluateAfterMatch(connection, transaction, outcome.PlayerA.PlayerId, seasonId);
        IReadOnlyList<string> achievementsB =
            achievements.EvaluateAfterMatch(connection, transaction, outcome.PlayerB.PlayerId, seasonId);

        transaction.Commit();
        return new MatchRecordResult(isRanked, deltaA, deltaB, achievementsA, achievementsB);
    }

    private static void UpdateScope(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, string scope,
        bool won, bool lost, bool forfeited, int roundsWon, int roundsLost, int seconds)
    {
        using (SqliteCommand ensure = connection.CreateCommand())
        {
            ensure.Transaction = transaction;
            ensure.CommandText =
                "INSERT OR IGNORE INTO player_stats (player_id, scope) VALUES ($id, $scope)";
            ensure.Parameters.AddWithValue("$id", playerId);
            ensure.Parameters.AddWithValue("$scope", scope);
            ensure.ExecuteNonQuery();
        }

        int currentStreak;
        int bestStreak;
        using (SqliteCommand read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText =
                "SELECT current_streak, best_streak FROM player_stats WHERE player_id=$id AND scope=$scope";
            read.Parameters.AddWithValue("$id", playerId);
            read.Parameters.AddWithValue("$scope", scope);
            using SqliteDataReader reader = read.ExecuteReader();
            reader.Read();
            currentStreak = reader.GetInt32(0);
            bestStreak = reader.GetInt32(1);
        }

        int newStreak = won ? currentStreak + 1 : 0;
        int newBest = Math.Max(bestStreak, newStreak);

        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE player_stats SET
                matches             = matches + 1,
                wins                = wins + $win,
                losses              = losses + $loss,
                forfeits            = forfeits + $forfeit,
                rounds_won          = rounds_won + $rw,
                rounds_lost         = rounds_lost + $rl,
                total_match_seconds = total_match_seconds + $secs,
                current_streak      = $streak,
                best_streak         = $best
            WHERE player_id=$id AND scope=$scope";
        update.Parameters.AddWithValue("$win", won ? 1 : 0);
        update.Parameters.AddWithValue("$loss", lost ? 1 : 0);
        update.Parameters.AddWithValue("$forfeit", forfeited ? 1 : 0);
        update.Parameters.AddWithValue("$rw", roundsWon);
        update.Parameters.AddWithValue("$rl", roundsLost);
        update.Parameters.AddWithValue("$secs", seconds);
        update.Parameters.AddWithValue("$streak", newStreak);
        update.Parameters.AddWithValue("$best", newBest);
        update.Parameters.AddWithValue("$id", playerId);
        update.Parameters.AddWithValue("$scope", scope);
        update.ExecuteNonQuery();
    }
}
