using AccardND.GameCore;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Registra l'esito di una partita: una riga in match_history e gli aggregati
/// player_stats (lifetime + stagione) in un'unica transazione. Punto di aggancio
/// che nelle fasi successive orchestrerà anche MMR/rank e sblocchi.
///
/// Solo il matchmaking produce partite classificate. Quelle giocate in una stanza
/// (pubblica, protetta o privata) sono amichevoli: si fermano a match_history, che
/// è lo storico del pannello admin, e non toccano niente di quello che i giocatori
/// vedono - né statistiche, né MMR, né quest, né esperienza. Il confine sta qui e
/// non nelle query di lettura: un aggregato scritto una volta di troppo poi non si
/// sa più da dove ripulirlo.
/// </summary>
/// <summary>Esito della registrazione, per comporre i messaggi match.result.</summary>
public sealed record MatchRecordResult(
    bool Ranked, PlayerRankedDelta A, PlayerRankedDelta B,
    IReadOnlyList<string> AchievementsA, IReadOnlyList<string> AchievementsB,
	AccountExperienceReward ExperienceA = null, AccountExperienceReward ExperienceB = null)
{
    private static readonly string[] None = Array.Empty<string>();
    public static readonly MatchRecordResult Unranked = new(false, null, null, None, None);
}

public sealed record AccountExperienceReward(string ClaimId, int Experience, int LevelsGained);

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

        // Amichevole: lo storico è tutto quello che se ne tiene.
        if (!outcome.Ranked)
        {
            transaction.Commit();
            return MatchRecordResult.Unranked;
        }

        foreach (string scope in new[] { "lifetime", seasonScope })
        {
            UpdateScope(connection, transaction, outcome.PlayerA.PlayerId, scope,
                aWon, aLost, forfeit && aLost, outcome.ScoreA, outcome.ScoreB, seconds);
            UpdateScope(connection, transaction, outcome.PlayerB.PlayerId, scope,
                bWon, bLost, forfeit && bLost, outcome.ScoreB, outcome.ScoreA, seconds);
        }

        // Contatori per le quest della taverna. Restano separati da player_stats perche'
        // quelli sono aggregati di scope (lifetime/stagione) mentre le quest lavorano sulla
        // differenza rispetto a un baseline giornaliero, che vive in player_counters.
        // Anche questi si fermano alle classificate: le quest sono l'unica fonte di miele,
        // e una stanza privata fra due complici e' la partita piu' facile da produrre in
        // serie che ci sia.
        CampaignCounters.RecordPvpMatch(connection, transaction, outcome.PlayerA.PlayerId,
            aWon, forfeit && aLost, outcome.ScoreA);
        CampaignCounters.RecordPvpMatch(connection, transaction, outcome.PlayerB.PlayerId,
            bWon, forfeit && bLost, outcome.ScoreB);

        PlayerRankedDelta deltaA = null;
        PlayerRankedDelta deltaB = null;
        // Una partita senza vincitore (lo spegnimento del server) sta negli aggregati come
        // partita giocata, ma non ha un esito da dare all'MMR.
        bool isRanked = outcome.Winner is 0 or 1;
        if (isRanked)
        {
            ApplyMatchResult applied = ranked.ApplyMatch(
                connection, transaction,
                outcome.PlayerA.PlayerId, outcome.PlayerB.PlayerId, outcome.Winner,
                outcome.ScoreA, outcome.ScoreB, seasonId);
            deltaA = applied.A;
            deltaB = applied.B;

            // Le icone-tier si sbloccano solo a piazzamento concluso (tier "raggiunto").
            if (!applied.A.Placement)
                unlocks.GrantTierIcons(connection, transaction, outcome.PlayerA.PlayerId, applied.A.After.TierIndex);
            if (!applied.B.Placement)
                unlocks.GrantTierIcons(connection, transaction, outcome.PlayerB.PlayerId, applied.B.After.TierIndex);
        }

		AccountExperienceReward experienceA = isRanked
			? GrantRankedRoundExperience(connection, transaction, outcome.PlayerA.PlayerId, outcome.RoomCode, outcome.ScoreA)
			: null;
		AccountExperienceReward experienceB = isRanked
			? GrantRankedRoundExperience(connection, transaction, outcome.PlayerB.PlayerId, outcome.RoomCode, outcome.ScoreB)
			: null;

        // Gli achievement leggono gli aggregati appena scritti: le amichevoli non li muovono,
        // quindi non c'e' niente da rivalutare (si e' già tornati sopra).
        IReadOnlyList<string> achievementsA =
            achievements.EvaluateAfterMatch(connection, transaction, outcome.PlayerA.PlayerId, seasonId);
        IReadOnlyList<string> achievementsB =
            achievements.EvaluateAfterMatch(connection, transaction, outcome.PlayerB.PlayerId, seasonId);

        transaction.Commit();
        return new MatchRecordResult(isRanked, deltaA, deltaB, achievementsA, achievementsB, experienceA, experienceB);
    }

	private static AccountExperienceReward GrantRankedRoundExperience(
		SqliteConnection connection, SqliteTransaction transaction,
		string playerId, string roomCode, int roundsWon)
	{
		int experience = Math.Max(0, roundsWon) * 5;
		if (experience <= 0)
			return null;

		using (SqliteCommand ensure = connection.CreateCommand())
		{
			ensure.Transaction = transaction;
			ensure.CommandText = @"
				INSERT OR IGNORE INTO single_player_progress
					(player_id, account_level, account_experience, account_total_experience,
					 account_experience_to_next_level, updated_at)
				VALUES ($id, 1, 0, 0, $next, $now)";
			ensure.Parameters.AddWithValue("$next", AccountLevelCurve.ExperienceToNext(1));
			ensure.Parameters.AddWithValue("$id", playerId);
			ensure.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
			ensure.ExecuteNonQuery();
		}

		string claimId = $"pvp-ranked-{roomCode}-{playerId}-{Guid.NewGuid():N}";
		using (SqliteCommand claim = connection.CreateCommand())
		{
			claim.Transaction = transaction;
			claim.CommandText = @"
				INSERT INTO single_player_reward_claims
					(claim_id, player_id, reward_type, base_honey, base_account_experience, multiplier, source_ref, created_at)
				VALUES ($claim, $id, 'pvp_ranked', 0, $xp, 1, $source, $now)";
			claim.Parameters.AddWithValue("$claim", claimId);
			claim.Parameters.AddWithValue("$id", playerId);
			claim.Parameters.AddWithValue("$xp", experience);
			claim.Parameters.AddWithValue("$source", (object)roomCode ?? DBNull.Value);
			claim.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
			claim.ExecuteNonQuery();
		}

		int level;
		int current;
		int total;
		using (SqliteCommand read = connection.CreateCommand())
		{
			read.Transaction = transaction;
			read.CommandText = @"SELECT account_level, account_experience, account_total_experience
				FROM single_player_progress WHERE player_id=$id";
			read.Parameters.AddWithValue("$id", playerId);
			using SqliteDataReader reader = read.ExecuteReader();
			reader.Read();
			level = reader.GetInt32(0);
			current = reader.GetInt32(1);
			total = reader.GetInt32(2);
		}

		// La curva la possiede AccountLevelCurve. Qui c'era una copia a mano della vecchia
		// soglia fissa a 100: con una curva vera sarebbe divergita dal ramo campagna, e il
		// livello sarebbe salito a ritmi diversi a seconda di dove veniva guadagnata l'exp.
		AccountLevelProgress progress = AccountLevelCurve.Apply(level, current, total, experience);
		using (SqliteCommand update = connection.CreateCommand())
		{
			update.Transaction = transaction;
			update.CommandText = @"
				UPDATE single_player_progress SET
					account_level=$level, account_experience=$current,
					account_total_experience=$total,
					account_experience_to_next_level=$next,
					pending_level_rewards=pending_level_rewards+$levels,
					updated_at=$now WHERE player_id=$id";
			update.Parameters.AddWithValue("$level", progress.Level);
			update.Parameters.AddWithValue("$current", progress.Experience);
			update.Parameters.AddWithValue("$total", progress.TotalExperience);
			update.Parameters.AddWithValue("$next", progress.ExperienceToNextLevel);
			update.Parameters.AddWithValue("$levels", progress.LevelsGained);
			update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
			update.Parameters.AddWithValue("$id", playerId);
			update.ExecuteNonQuery();
		}
		return new AccountExperienceReward(claimId, experience, progress.LevelsGained);
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
