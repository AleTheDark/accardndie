using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

public sealed class SinglePlayerProgressService
{
    private readonly AccardDatabase database;

    // Catalogo costi provvisorio, allineato alle costanti client (BattleBoardController).
    // In seguito dovra diventare config server-authoritative caricata da file/DB.
    private static readonly Dictionary<(string Type, string Id), int> UnlockCosts = new()
    {
        [("chapter", "chapter-1")] = 25,
        [("chapter", "chapter-2")] = 75,
        [("chapter", "chapter-3")] = 120,
        [("chapter", "chapter-4")] = 180,
        [("mode", "hardcore")] = 50
    };

    private const int TutorialRewardHoney = 60;
    private const int AdMultiplier = 3;
    private const int DeathRewardFloor = 5;
    private const int DeathRewardCeiling = 300;

    public SinglePlayerProgressService(AccardDatabase database)
    {
        this.database = database;
    }

    public SinglePlayerProgressData GetProgress(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        EnsureProgressRow(connection, null, identity.PlayerId);
        return ReadProgress(connection, identity.PlayerId);
    }

    public (SinglePlayerProgressData Progress, string ErrorCode, string Error) PurchaseUnlock(
        AccountIdentity identity,
        SinglePlayerPurchaseUnlockRequest request)
    {
        string type = Normalize(request?.type);
        string id = Normalize(request?.id);
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id))
            return (null, ErrorCodes.InvalidProgressionRequest, "Unlock non valido.");

        if (!UnlockCosts.TryGetValue((type, id), out int cost))
            return (null, ErrorCodes.InvalidProgressionRequest, "Unlock non acquistabile.");

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        SinglePlayerProgressData current = ReadProgress(connection, identity.PlayerId, transaction);
        if (IsAlreadyUnlocked(current, type, id))
        {
            transaction.Commit();
            return (current, null, null);
        }

        if (current.honey < cost)
            return (null, ErrorCodes.InsufficientHoney, "Vasetti di miele insufficienti.");

        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE single_player_progress
                SET honey = honey - $cost,
                    hardcore_unlocked = CASE WHEN $type = 'mode' AND $id = 'hardcore' THEN 1 ELSE hardcore_unlocked END,
                    updated_at = $now
                WHERE player_id = $player";
            update.Parameters.AddWithValue("$cost", cost);
            update.Parameters.AddWithValue("$type", type);
            update.Parameters.AddWithValue("$id", id);
            update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$player", identity.PlayerId);
            update.ExecuteNonQuery();
        }

        if (type != "mode")
            GrantUnlock(connection, transaction, identity.PlayerId, type, id);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (progress, null, null);
    }

    /// <summary>
    /// Concede la ricompensa di completamento tutorial (idempotente: una sola volta per account,
    /// governata dal flag tutorial_completed). Il server possiede l'importo.
    /// </summary>
    public (SinglePlayerRewardResult Result, string ErrorCode, string Error) ClaimTutorialReward(
        AccountIdentity identity,
        SinglePlayerTutorialRewardRequest request)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        SinglePlayerProgressData current = ReadProgress(connection, identity.PlayerId, transaction);
        if (current.tutorialCompleted)
        {
            // Gia riscattata: nessun nuovo miele, risposta idempotente.
            transaction.Commit();
            return (BuildReward(current, null, 0), null, null);
        }

        string claimId = NewClaimId();
        RecordClaim(connection, transaction, claimId, identity.PlayerId, "tutorial",
            TutorialRewardHoney, Normalize(request?.tutorialRunId));

        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE single_player_progress
                SET honey = honey + $honey, tutorial_completed = 1, updated_at = $now
                WHERE player_id = $player";
            update.Parameters.AddWithValue("$honey", TutorialRewardHoney);
            update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$player", identity.PlayerId);
            update.ExecuteNonQuery();
        }

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimId, TutorialRewardHoney), null, null);
    }

    /// <summary>
    /// Concede la ricompensa in miele alla morte. L'importo e calcolato dal server con una
    /// formula provvisoria su un sommario riportato dal client e limitato (cap anti-spoof).
    /// Idempotente per runId: piu chiamate con lo stesso runId non accreditano due volte.
    /// Nota: il combattimento single player e client-side, quindi il server non puo validare
    /// pienamente l'evento; possiede pero formula, cap e idempotenza.
    /// </summary>
    public (SinglePlayerRewardResult Result, string ErrorCode, string Error) ClaimDeathReward(
        AccountIdentity identity,
        SinglePlayerDeathRewardRequest request)
    {
        int baseHoney = CalculateDeathReward(request);
        string runId = Normalize(request?.runId);

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        if (!string.IsNullOrEmpty(runId) &&
            TryFindDeathClaim(connection, transaction, identity.PlayerId, runId, out string existingClaimId))
        {
            // Stessa run gia riscattata: risposta idempotente senza nuovo accredito.
            SinglePlayerProgressData unchanged = ReadProgress(connection, identity.PlayerId, transaction);
            transaction.Commit();
            return (BuildReward(unchanged, existingClaimId, 0), null, null);
        }

        string claimId = NewClaimId();
        RecordClaim(connection, transaction, claimId, identity.PlayerId, "death", baseHoney, runId);
        GrantHoney(connection, transaction, identity.PlayerId, baseHoney);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimId, baseHoney), null, null);
    }

    /// <summary>
    /// Applica il moltiplicatore pubblicitario a una reward gia concessa: accredita la parte
    /// aggiuntiva (base * (moltiplicatore - 1)). Idempotente sulla reward gia moltiplicata e
    /// sull'adImpressionId gia usato (una pubblicita non puo essere riscattata due volte).
    /// La verifica reale dell'ad (SSV lato provider) non e ancora integrata.
    /// </summary>
    public (SinglePlayerRewardResult Result, string ErrorCode, string Error) ClaimAdMultiplier(
        AccountIdentity identity,
        SinglePlayerAdMultiplierRequest request)
    {
        string claimRef = Normalize(request?.rewardClaimId);
        string adId = Normalize(request?.adImpressionId);
        if (string.IsNullOrEmpty(claimRef) || string.IsNullOrEmpty(adId))
            return (null, ErrorCodes.InvalidProgressionRequest, "Richiesta moltiplicatore non valida.");

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        int baseHoney;
        int multiplier;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = @"
                SELECT base_honey, multiplier FROM single_player_reward_claims
                WHERE claim_id = $claim AND player_id = $player";
            query.Parameters.AddWithValue("$claim", claimRef);
            query.Parameters.AddWithValue("$player", identity.PlayerId);
            using SqliteDataReader reader = query.ExecuteReader();
            if (!reader.Read())
                return (null, ErrorCodes.RewardClaimNotFound, "Ricompensa non trovata.");
            baseHoney = reader.GetInt32(0);
            multiplier = reader.GetInt32(1);
        }

        if (multiplier > 1)
        {
            // Gia moltiplicata: idempotente, nessun ulteriore accredito.
            SinglePlayerProgressData unchanged = ReadProgress(connection, identity.PlayerId, transaction);
            transaction.Commit();
            return (BuildReward(unchanged, claimRef, 0), null, null);
        }

        if (IsAdImpressionUsed(connection, transaction, adId))
            return (null, ErrorCodes.AdAlreadyUsed, "Pubblicita gia utilizzata.");

        int extraHoney = baseHoney * (AdMultiplier - 1);
        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE single_player_reward_claims
                SET multiplier = $mult, ad_impression_id = $ad, multiplied_at = $now
                WHERE claim_id = $claim AND player_id = $player";
            update.Parameters.AddWithValue("$mult", AdMultiplier);
            update.Parameters.AddWithValue("$ad", adId);
            update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$claim", claimRef);
            update.Parameters.AddWithValue("$player", identity.PlayerId);
            update.ExecuteNonQuery();
        }
        GrantHoney(connection, transaction, identity.PlayerId, extraHoney);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimRef, extraHoney), null, null);
    }

    private static int CalculateDeathReward(SinglePlayerDeathRewardRequest request)
    {
        if (request == null)
            return DeathRewardFloor;
        int rooms = Math.Clamp(request.roomsCleared, 0, 50);
        int enemies = Math.Clamp(request.enemiesDefeated, 0, 500);
        int bosses = Math.Clamp(request.bossesDefeated, 0, 20);
        int honey = DeathRewardFloor + rooms * 3 + enemies + bosses * 10;
        return Math.Clamp(honey, DeathRewardFloor, DeathRewardCeiling);
    }

    private static SinglePlayerRewardResult BuildReward(
        SinglePlayerProgressData progress, string claimId, int grantedHoney) => new()
    {
        progress = progress,
        rewardClaimId = claimId,
        grantedHoney = grantedHoney
    };

    private static string NewClaimId() => Guid.NewGuid().ToString("N");

    private static void RecordClaim(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string claimId,
        string playerId,
        string rewardType,
        int baseHoney,
        string sourceRef)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT INTO single_player_reward_claims
                (claim_id, player_id, reward_type, base_honey, multiplier, source_ref, created_at)
            VALUES ($claim, $player, $type, $base, 1, $ref, $now)";
        insert.Parameters.AddWithValue("$claim", claimId);
        insert.Parameters.AddWithValue("$player", playerId);
        insert.Parameters.AddWithValue("$type", rewardType);
        insert.Parameters.AddWithValue("$base", baseHoney);
        insert.Parameters.AddWithValue("$ref", (object)sourceRef ?? DBNull.Value);
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    private static void GrantHoney(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int amount)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_progress
            SET honey = honey + $honey, updated_at = $now
            WHERE player_id = $player";
        update.Parameters.AddWithValue("$honey", amount);
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$player", playerId);
        update.ExecuteNonQuery();
    }

    private static bool TryFindDeathClaim(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        string runId,
        out string claimId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = @"
            SELECT claim_id FROM single_player_reward_claims
            WHERE player_id = $player AND reward_type = 'death' AND source_ref = $ref
            LIMIT 1";
        query.Parameters.AddWithValue("$player", playerId);
        query.Parameters.AddWithValue("$ref", runId);
        using SqliteDataReader reader = query.ExecuteReader();
        if (reader.Read())
        {
            claimId = reader.GetString(0);
            return true;
        }
        claimId = null;
        return false;
    }

    private static bool IsAdImpressionUsed(
        SqliteConnection connection, SqliteTransaction transaction, string adId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            "SELECT 1 FROM single_player_reward_claims WHERE ad_impression_id = $ad LIMIT 1";
        query.Parameters.AddWithValue("$ad", adId);
        using SqliteDataReader reader = query.ExecuteReader();
        return reader.Read();
    }

    private static bool IsAlreadyUnlocked(SinglePlayerProgressData progress, string type, string id)
    {
        if (type == "mode" && id == "hardcore")
            return progress.hardcoreUnlocked;

        string[] list = type switch
        {
            "chapter" => progress.unlockedChapters,
            "stage" => progress.unlockedStages,
            "class" => progress.unlockedClasses,
            "scenario" => progress.unlockedScenarios,
            "secondAbility" => progress.unlockedSecondAbilities,
            _ => Array.Empty<string>()
        };
        return Array.IndexOf(list ?? Array.Empty<string>(), id) >= 0;
    }

    private static void GrantUnlock(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        string type,
        string id)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT OR IGNORE INTO single_player_unlocks (player_id, unlock_type, unlock_id, unlocked_at)
            VALUES ($player, $type, $id, $now)";
        insert.Parameters.AddWithValue("$player", playerId);
        insert.Parameters.AddWithValue("$type", type);
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    private static void EnsureProgressRow(SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT OR IGNORE INTO single_player_progress
                (player_id, honey, tutorial_completed, hardcore_unlocked, updated_at)
            VALUES ($player, 0, 0, 0, $now)";
        insert.Parameters.AddWithValue("$player", playerId);
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    private static SinglePlayerProgressData ReadProgress(
        SqliteConnection connection,
        string playerId,
        SqliteTransaction transaction = null)
    {
        var data = new SinglePlayerProgressData();
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = @"
                SELECT honey, tutorial_completed, hardcore_unlocked
                FROM single_player_progress
                WHERE player_id = $player";
            query.Parameters.AddWithValue("$player", playerId);
            using SqliteDataReader reader = query.ExecuteReader();
            if (reader.Read())
            {
                data.honey = reader.GetInt32(0);
                data.tutorialCompleted = reader.GetInt32(1) != 0;
                data.hardcoreUnlocked = reader.GetInt32(2) != 0;
            }
        }

        data.unlockedChapters = ReadUnlocks(connection, transaction, playerId, "chapter");
        data.unlockedStages = ReadUnlocks(connection, transaction, playerId, "stage");
        data.unlockedClasses = ReadUnlocks(connection, transaction, playerId, "class");
        data.unlockedScenarios = ReadUnlocks(connection, transaction, playerId, "scenario");
        data.unlockedSecondAbilities = ReadUnlocks(connection, transaction, playerId, "secondAbility");
        return data;
    }

    private static string[] ReadUnlocks(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        string type)
    {
        var result = new List<string>();
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = @"
            SELECT unlock_id
            FROM single_player_unlocks
            WHERE player_id = $player AND unlock_type = $type
            ORDER BY unlocked_at, unlock_id";
        query.Parameters.AddWithValue("$player", playerId);
        query.Parameters.AddWithValue("$type", type);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result.ToArray();
    }

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
