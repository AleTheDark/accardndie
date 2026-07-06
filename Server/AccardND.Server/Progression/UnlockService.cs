using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

public sealed record IconEntry(string IconId, string Name, string Source, string UnlockRef, bool Unlocked);

/// <summary>
/// Catalogo icone e concessioni. Le sorgenti gestite in Fase 3: free (icone classe,
/// date a tutti), tier (raggiungere un tier), campaign (sconfiggere un mostro in
/// campagna). achievement/halloffame arriveranno nelle rispettive fasi. Tutte le
/// concessioni sono idempotenti.
/// </summary>
public sealed class UnlockService
{
    public const string DefaultIconId = "class-warrior";

    // Icone gratuite: una per classe eroe. Il client mappa l'id all'artwork.
    private static readonly (string Id, string Name)[] ClassIcons =
    {
        ("class-assassin", "Assassino"), ("class-warrior", "Guerriero"), ("class-mage", "Mago"),
        ("class-paladin", "Paladino"), ("class-rogue", "Ladro"), ("class-hunter", "Cacciatore"),
        ("class-barbarian", "Barbaro"), ("class-necromancer", "Negromante"), ("class-priest", "Prete")
    };

    // Icone esclusive di fine stagione: concesse in base al piazzamento finale.
    private static readonly (string Id, string Name, int MaxRank)[] HallOfFameIcons =
    {
        ("hof-top1", "Campione di Stagione", 1),
        ("hof-top3", "Podio di Stagione", 3),
        ("hof-top10", "Top 10 di Stagione", 10)
    };

    // Icone premio degli achievement (concesse dallo sblocco del traguardo).
    private static readonly (string Id, string Name)[] AchievementIcons =
    {
        ("ach-veteran", "Veterano"),
        ("ach-streak", "Inarrestabile"),
        ("ach-onnipotente", "Divinità")
    };

    private readonly AccardDatabase database;
    private readonly RankedConfig ranked;
    private readonly string[] monsterFamilies;

    public UnlockService(AccardDatabase database, ServerConfig config)
    {
        this.database = database;
        ranked = config.Ranked;
        monsterFamilies = config.CampaignMonsters;
        SeedCatalog();
    }

    /// <summary>Id icona del tier n-esimo (0 = più basso).</summary>
    public string TierIconId(int tierIndex) => $"tier-{ranked.Tiers[tierIndex].ToLowerInvariant()}";

    /// <summary>Dà a un giocatore tutte le icone gratuite (idempotente).</summary>
    public void GrantFreeIcons(SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        foreach ((string id, _) in ClassIcons)
            Grant(connection, transaction, playerId, id, "free");
    }

    /// <summary>Concede le icone dei tier fino a quello raggiunto (incluso). Idempotente.</summary>
    public void GrantTierIcons(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int tierIndex)
    {
        for (int index = 0; index <= tierIndex && index < ranked.Tiers.Length; index++)
            Grant(connection, transaction, playerId, TierIconId(index), "tier");
    }

    /// <summary>Concede le icone esclusive di fine stagione in base al piazzamento (1-based).</summary>
    public void GrantHallOfFameIcons(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int finalRank)
    {
        foreach ((string id, _, int maxRank) in HallOfFameIcons)
            if (finalRank <= maxRank)
                Grant(connection, transaction, playerId, id, "halloffame");
    }

    /// <summary>Concede una singola icona (usata dai premi achievement). Idempotente.</summary>
    public void GrantIcon(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, string iconId, string source)
    {
        if (!string.IsNullOrEmpty(iconId))
            Grant(connection, transaction, playerId, iconId, source);
    }

    /// <summary>
    /// Registra i mostri sconfitti in campagna e concede le icone corrispondenti.
    /// Ritorna gli id delle icone appena sbloccate.
    /// </summary>
    public IReadOnlyList<string> GrantCampaignIcons(string playerId, IEnumerable<string> monsters)
    {
        var newlyUnlocked = new List<string>();
        if (monsters == null)
            return newlyUnlocked;

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        string now = DateTime.UtcNow.ToString("O");

        foreach (string raw in monsters)
        {
            string family = raw?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(family) ||
                Array.IndexOf(monsterFamilies, family) < 0)
                continue;

            using (SqliteCommand kill = connection.CreateCommand())
            {
                kill.Transaction = transaction;
                kill.CommandText = @"
                    INSERT INTO campaign_kills (player_id, monster_id, kills, first_killed_at)
                    VALUES ($id, $m, 1, $now)
                    ON CONFLICT(player_id, monster_id) DO UPDATE SET kills = kills + 1";
                kill.Parameters.AddWithValue("$id", playerId);
                kill.Parameters.AddWithValue("$m", family);
                kill.Parameters.AddWithValue("$now", now);
                kill.ExecuteNonQuery();
            }

            if (Grant(connection, transaction, playerId, $"monster-{family}", "campaign"))
                newlyUnlocked.Add($"monster-{family}");
        }

        transaction.Commit();
        return newlyUnlocked;
    }

    /// <summary>Catalogo completo con flag di sblocco per il giocatore.</summary>
    public IReadOnlyList<IconEntry> GetCatalog(SqliteConnection connection, string playerId)
    {
        var owned = new HashSet<string>();
        using (SqliteCommand ownedQuery = connection.CreateCommand())
        {
            ownedQuery.CommandText = "SELECT icon_id FROM player_icons WHERE player_id=$id";
            ownedQuery.Parameters.AddWithValue("$id", playerId);
            using SqliteDataReader reader = ownedQuery.ExecuteReader();
            while (reader.Read())
                owned.Add(reader.GetString(0));
        }

        var entries = new List<IconEntry>();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT icon_id, name, source, unlock_ref FROM icons ORDER BY sort_order, icon_id";
        using SqliteDataReader catalog = query.ExecuteReader();
        while (catalog.Read())
        {
            string iconId = catalog.GetString(0);
            entries.Add(new IconEntry(
                iconId,
                catalog.GetString(1),
                catalog.GetString(2),
                catalog.IsDBNull(3) ? null : catalog.GetString(3),
                owned.Contains(iconId)));
        }
        return entries;
    }

    public bool OwnsIcon(SqliteConnection connection, string playerId, string iconId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT 1 FROM player_icons WHERE player_id=$id AND icon_id=$icon LIMIT 1";
        query.Parameters.AddWithValue("$id", playerId);
        query.Parameters.AddWithValue("$icon", iconId);
        return query.ExecuteScalar() != null;
    }

    public int CountOwned(SqliteConnection connection, string playerId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM player_icons WHERE player_id=$id";
        query.Parameters.AddWithValue("$id", playerId);
        return (int)(long)query.ExecuteScalar();
    }

    public int CatalogSize(SqliteConnection connection)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM icons";
        return (int)(long)query.ExecuteScalar();
    }

    /// <summary>Concede un'icona se non già posseduta. true se era nuova.</summary>
    private static bool Grant(
        SqliteConnection connection, SqliteTransaction transaction,
        string playerId, string iconId, string source)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT OR IGNORE INTO player_icons (player_id, icon_id, unlocked_at, unlock_source)
            VALUES ($id, $icon, $now, $source)";
        insert.Parameters.AddWithValue("$id", playerId);
        insert.Parameters.AddWithValue("$icon", iconId);
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.Parameters.AddWithValue("$source", source);
        return insert.ExecuteNonQuery() > 0;
    }

    private void SeedCatalog()
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        int order = 0;

        foreach ((string id, string name) in ClassIcons)
            SeedIcon(connection, transaction, id, name, "free", null, order++);

        for (int index = 0; index < ranked.Tiers.Length; index++)
            SeedIcon(connection, transaction, TierIconId(index), ranked.Tiers[index], "tier", ranked.Tiers[index], order++);

        foreach (string family in monsterFamilies)
            SeedIcon(connection, transaction, $"monster-{family}", Capitalize(family), "campaign", family, order++);

        foreach ((string id, string name, int maxRank) in HallOfFameIcons)
            SeedIcon(connection, transaction, id, name, "halloffame", maxRank.ToString(), order++);

        foreach ((string id, string name) in AchievementIcons)
            SeedIcon(connection, transaction, id, name, "achievement", null, order++);

        transaction.Commit();
    }

    private static void SeedIcon(
        SqliteConnection connection, SqliteTransaction transaction,
        string iconId, string name, string source, string unlockRef, int sortOrder)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT INTO icons (icon_id, name, source, unlock_ref, sort_order)
            VALUES ($id, $name, $source, $ref, $order)
            ON CONFLICT(icon_id) DO UPDATE SET
                name = excluded.name, source = excluded.source,
                unlock_ref = excluded.unlock_ref, sort_order = excluded.sort_order";
        insert.Parameters.AddWithValue("$id", iconId);
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$source", source);
        insert.Parameters.AddWithValue("$ref", (object)unlockRef ?? DBNull.Value);
        insert.Parameters.AddWithValue("$order", sortOrder);
        insert.ExecuteNonQuery();
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
