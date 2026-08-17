using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Toglie da <c>player_stats</c> le amichevoli che ci sono finite dentro prima che
/// <see cref="MatchResultRecorder"/> si fermasse alle classificate. Serve una volta sola:
/// senza, chi ha giocato in stanza si porta dietro per sempre vittorie e win rate che le
/// regole nuove non gli darebbero, e i numeri della sua scheda non tornano con quelli
/// della classifica.
///
/// Sottrae invece di ricalcolare tutto da <c>match_history</c>, che sarebbe piu' corto.
/// Il motivo e' che lo storico non e' completo: cancellare un account porta via anche le
/// sue partite, e un ricalcolo trasformerebbe le vittorie dei suoi avversari in partite
/// mai giocate. Sottraendo solo le amichevoli ancora in archivio si sbaglia per difetto -
/// resta dentro qualche amichevole giocata contro un account cancellato - ma non si toglie
/// a nessuno una partita che ha giocato davvero.
///
/// Le strisce non si sottraggono: sono un ordine, non una somma. Si rifanno rigiocando lo
/// storico classificato, e solo per chi ce l'ha tutto (vedi <see cref="RecomputeStreaks"/>).
/// </summary>
public static class FriendlyStatsCleanupMigration
{
    /// <summary>
    /// Chiave in <c>server_settings</c> che segna la pulizia fatta. Col nome della regola
    /// che l'ha resa necessaria: una prossima ripulitura sara' un'altra chiave.
    /// </summary>
    private const string SettingKey = "migration.stats.ranked-only-1";

    private static readonly string[] ForfeitReasons = { "forfeit", "timeout", "disconnect" };

    /// <summary>Aggregato di un giocatore in uno scope, come lo si ricava dallo storico.</summary>
    private sealed class Tally
    {
        public int Matches;
        public int Wins;
        public int Losses;
        public int Forfeits;
        public int RoundsWon;
        public int RoundsLost;
        public int Seconds;
    }

    /// <summary>
    /// Esegue la pulizia se non e' gia' stata fatta. Ritorna il numero di righe di
    /// player_stats corrette (0 anche quando era gia' stata eseguita).
    /// </summary>
    public static int RunIfNeeded(AccardDatabase database, ILogger logger = null)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        if (IsAlreadyDone(connection, transaction))
            return 0;

        int corrected = SubtractFriendlies(connection, transaction);
        int streaks = RecomputeStreaks(connection, transaction);

        MarkDone(connection, transaction);
        transaction.Commit();

        if (corrected > 0 || streaks > 0)
            logger?.LogInformation(
                "Pulizia statistiche: {Rows} aggregati ripuliti dalle amichevoli, {Streaks} strisce rifatte.",
                corrected, streaks);
        return corrected;
    }

    private static bool IsAlreadyDone(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT 1 FROM server_settings WHERE key = $key LIMIT 1";
        query.Parameters.AddWithValue("$key", SettingKey);
        return query.ExecuteScalar() != null;
    }

    private static void MarkDone(SqliteConnection connection, SqliteTransaction transaction)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT INTO server_settings (key, value, updated_at) VALUES ($key, $value, $now)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at";
        insert.Parameters.AddWithValue("$key", SettingKey);
        insert.Parameters.AddWithValue("$value", "done");
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    /// <summary>
    /// Sottrae le amichevoli dagli aggregati, scope per scope: 'lifetime' prende tutte le
    /// amichevoli del giocatore, 'season:&lt;id&gt;' solo quelle di quella stagione. I
    /// contatori non scendono sotto zero: se un aggregato era gia' incoerente (partite
    /// cancellate con un account) meglio uno zero di un numero negativo.
    /// </summary>
    private static int SubtractFriendlies(SqliteConnection connection, SqliteTransaction transaction)
    {
        var friendlies = new Dictionary<(string PlayerId, string Scope), Tally>();
        foreach (MatchRow match in ReadMatches(connection, transaction, ranked: false))
        {
            Accumulate(friendlies, match, playerIndex: 0);
            Accumulate(friendlies, match, playerIndex: 1);
        }
        if (friendlies.Count == 0)
            return 0;

        int corrected = 0;
        foreach (((string playerId, string scope), Tally tally) in friendlies)
        {
            using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE player_stats SET
                    matches             = MAX(0, matches - $matches),
                    wins                = MAX(0, wins - $wins),
                    losses              = MAX(0, losses - $losses),
                    forfeits            = MAX(0, forfeits - $forfeits),
                    rounds_won          = MAX(0, rounds_won - $roundsWon),
                    rounds_lost         = MAX(0, rounds_lost - $roundsLost),
                    total_match_seconds = MAX(0, total_match_seconds - $seconds)
                WHERE player_id = $id AND scope = $scope";
            update.Parameters.AddWithValue("$matches", tally.Matches);
            update.Parameters.AddWithValue("$wins", tally.Wins);
            update.Parameters.AddWithValue("$losses", tally.Losses);
            update.Parameters.AddWithValue("$forfeits", tally.Forfeits);
            update.Parameters.AddWithValue("$roundsWon", tally.RoundsWon);
            update.Parameters.AddWithValue("$roundsLost", tally.RoundsLost);
            update.Parameters.AddWithValue("$seconds", tally.Seconds);
            update.Parameters.AddWithValue("$id", playerId);
            update.Parameters.AddWithValue("$scope", scope);
            corrected += update.ExecuteNonQuery();
        }
        return corrected;
    }

    /// <summary>
    /// Rifa' current_streak e best_streak rigiocando in ordine le sole classificate.
    /// Vale solo per chi nello storico ha esattamente le partite che l'aggregato dice di
    /// avere: se ne mancano (account cancellati), una striscia ricostruita sarebbe piu'
    /// corta di quella vera, e togliere a qualcuno un "miglior risultato" che ha ottenuto
    /// e' peggio che lasciargli una striscia gonfiata da qualche amichevole.
    /// </summary>
    private static int RecomputeStreaks(SqliteConnection connection, SqliteTransaction transaction)
    {
        var streaks = new Dictionary<(string PlayerId, string Scope), (int Current, int Best, int Matches)>();
        foreach (MatchRow match in ReadMatches(connection, transaction, ranked: true))
        {
            AccumulateStreak(streaks, match, playerIndex: 0);
            AccumulateStreak(streaks, match, playerIndex: 1);
        }

        int rewritten = 0;
        foreach (((string playerId, string scope), (int current, int best, int matches)) in streaks)
        {
            using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE player_stats SET current_streak = $current, best_streak = $best
                WHERE player_id = $id AND scope = $scope AND matches = $matches";
            update.Parameters.AddWithValue("$current", current);
            update.Parameters.AddWithValue("$best", best);
            update.Parameters.AddWithValue("$id", playerId);
            update.Parameters.AddWithValue("$scope", scope);
            update.Parameters.AddWithValue("$matches", matches);
            rewritten += update.ExecuteNonQuery();
        }

        // Chi ha giocato solo amichevoli resta con una riga a zero partite: una striscia
        // dentro una riga vuota non e' un dato incerto, e' un residuo.
        using (SqliteCommand reset = connection.CreateCommand())
        {
            reset.Transaction = transaction;
            reset.CommandText = @"
                UPDATE player_stats SET current_streak = 0, best_streak = 0
                WHERE matches = 0 AND (current_streak <> 0 OR best_streak <> 0)";
            rewritten += reset.ExecuteNonQuery();
        }
        return rewritten;
    }

    private sealed record MatchRow(
        int SeasonId, string PlayerA, string PlayerB, int Winner,
        int ScoreA, int ScoreB, string EndedReason, int Seconds);

    /// <summary>Lo storico in ordine di conclusione: le strisce hanno bisogno dell'ordine.</summary>
    private static List<MatchRow> ReadMatches(
        SqliteConnection connection, SqliteTransaction transaction, bool ranked)
    {
        var matches = new List<MatchRow>();
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = @"
            SELECT season_id, player_a, player_b, winner, score_a, score_b, ended_reason,
                   started_at, ended_at
            FROM match_history WHERE ranked = $ranked ORDER BY ended_at, match_id";
        query.Parameters.AddWithValue("$ranked", ranked ? 1 : 0);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            matches.Add(new MatchRow(
                reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetString(6),
                ElapsedSeconds(reader.GetString(7), reader.GetString(8))));
        }
        return matches;
    }

    private static int ElapsedSeconds(string startedAt, string endedAt)
    {
        if (!DateTime.TryParse(startedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime start) ||
            !DateTime.TryParse(endedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime end))
            return 0;
        return Math.Max(0, (int)(end - start).TotalSeconds);
    }

    private static void Accumulate(
        Dictionary<(string, string), Tally> tallies, MatchRow match, int playerIndex)
    {
        string playerId = playerIndex == 0 ? match.PlayerA : match.PlayerB;
        bool won = match.Winner == playerIndex;
        bool lost = match.Winner == 1 - playerIndex;
        bool forfeited = lost && Array.IndexOf(ForfeitReasons, match.EndedReason) >= 0;
        int roundsWon = playerIndex == 0 ? match.ScoreA : match.ScoreB;
        int roundsLost = playerIndex == 0 ? match.ScoreB : match.ScoreA;

        foreach (string scope in new[] { "lifetime", $"season:{match.SeasonId}" })
        {
            if (!tallies.TryGetValue((playerId, scope), out Tally tally))
                tallies[(playerId, scope)] = tally = new Tally();
            tally.Matches++;
            if (won) tally.Wins++;
            if (lost) tally.Losses++;
            if (forfeited) tally.Forfeits++;
            tally.RoundsWon += roundsWon;
            tally.RoundsLost += roundsLost;
            tally.Seconds += match.Seconds;
        }
    }

    private static void AccumulateStreak(
        Dictionary<(string, string), (int Current, int Best, int Matches)> streaks,
        MatchRow match, int playerIndex)
    {
        string playerId = playerIndex == 0 ? match.PlayerA : match.PlayerB;
        bool won = match.Winner == playerIndex;

        foreach (string scope in new[] { "lifetime", $"season:{match.SeasonId}" })
        {
            streaks.TryGetValue((playerId, scope), out (int Current, int Best, int Matches) state);
            int current = won ? state.Current + 1 : 0;
            streaks[(playerId, scope)] =
                (current, Math.Max(state.Best, current), state.Matches + 1);
        }
    }
}
