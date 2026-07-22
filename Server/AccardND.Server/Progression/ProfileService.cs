using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// Profilo giocatore: icona selezionata, bio, riepilogo ranked/statistiche e
/// gestione delle icone. Garantisce lazy la creazione del profilo con le icone
/// gratuite alla prima interazione.
/// </summary>
public sealed class ProfileService
{
    private readonly AccardDatabase database;
    private readonly UnlockService unlocks;
    private readonly RankedService ranked;
    private readonly StatsService stats;
    private readonly SeasonService seasons;

    public ProfileService(
        AccardDatabase database, UnlockService unlocks, RankedService ranked,
        StatsService stats, SeasonService seasons)
    {
        this.database = database;
        this.unlocks = unlocks;
        this.ranked = ranked;
        this.stats = stats;
        this.seasons = seasons;
    }

    public ProfileData GetProfile(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        EnsureProfile(connection, identity);

        (string selectedIcon, string bio) = ReadProfile(connection, identity.PlayerId);
        RankedProgress progress = ranked.GetProgress(identity.PlayerId, seasons.ActiveSeasonId);
        PlayerStatsDto lifetime = stats.GetStats(identity).lifetime;

        return new ProfileData
        {
            playerId = identity.PlayerId,
            username = identity.Username,
            selectedIconId = selectedIcon,
            bio = bio,
            ranked = progress.Ranked,
            placement = progress.Ranked && !progress.PlacementDone,
            placementRemaining = progress.PlacementRemaining,
            tier = progress.Tier.TierName,
            division = progress.Tier.Division,
            leaguePoints = progress.Tier.LeaguePoints,
            wins = lifetime.wins,
            losses = lifetime.losses,
            winRatePercent = lifetime.winRatePercent,
            currentStreak = lifetime.currentStreak,
            bestStreak = lifetime.bestStreak,
            roundsWon = lifetime.roundsWon,
            roundsLost = lifetime.roundsLost,
            forfeits = lifetime.forfeits,
            iconsUnlocked = unlocks.CountOwned(connection, identity.PlayerId),
            iconsTotal = unlocks.CatalogSize(connection),
            seasonId = seasons.ActiveSeasonId,
            seasonName = seasons.ActiveSeasonName
        };
    }

    public IconsData GetIcons(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        EnsureProfile(connection, identity);

        (string selectedIcon, _) = ReadProfile(connection, identity.PlayerId);
        IReadOnlyList<IconEntry> catalog = unlocks.GetCatalog(connection, identity.PlayerId);
        var icons = new IconDto[catalog.Count];
        for (int index = 0; index < catalog.Count; index++)
        {
            IconEntry entry = catalog[index];
            icons[index] = new IconDto
            {
                iconId = entry.IconId,
                name = entry.Name,
                source = entry.Source,
                unlockRef = entry.UnlockRef,
                unlocked = entry.Unlocked
            };
        }
        return new IconsData { selectedIconId = selectedIcon, icons = icons };
    }

    /// <summary>Imposta l'icona selezionata se il giocatore la possiede.</summary>
    public bool TrySetIcon(AccountIdentity identity, string iconId, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(iconId))
        {
            error = "Icona non valida.";
            return false;
        }

        using SqliteConnection connection = database.Open();
        EnsureProfile(connection, identity);

        if (!unlocks.OwnsIcon(connection, identity.PlayerId, iconId))
        {
            error = "Icona non sbloccata.";
            return false;
        }

        using SqliteCommand update = connection.CreateCommand();
        update.CommandText =
            "UPDATE profiles SET selected_icon_id=$icon, updated_at=$now WHERE player_id=$id";
        update.Parameters.AddWithValue("$icon", iconId);
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$id", identity.PlayerId);
        update.ExecuteNonQuery();
        return true;
    }

    public IReadOnlyList<string> ReportCampaignKills(
        AccountIdentity identity,
        IEnumerable<string> monsters,
        IEnumerable<string> bosses)
    {
        using (SqliteConnection connection = database.Open())
            EnsureProfile(connection, identity);
        return unlocks.GrantCampaignIcons(identity.PlayerId, monsters, bosses);
    }

    /// <summary>Crea il profilo (con icone gratuite e icona di default) se manca.</summary>
    public void EnsureProfile(SqliteConnection connection, AccountIdentity identity)
    {
        bool exists;
        using (SqliteCommand check = connection.CreateCommand())
        {
            check.CommandText = "SELECT 1 FROM profiles WHERE player_id=$id LIMIT 1";
            check.Parameters.AddWithValue("$id", identity.PlayerId);
            exists = check.ExecuteScalar() != null;
        }
        if (exists)
            return;

        using SqliteTransaction transaction = connection.BeginTransaction();
        unlocks.GrantFreeIcons(connection, transaction, identity.PlayerId);
        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT OR IGNORE INTO profiles (player_id, selected_icon_id, updated_at)
                VALUES ($id, $icon, $now)";
            insert.Parameters.AddWithValue("$id", identity.PlayerId);
            insert.Parameters.AddWithValue("$icon", UnlockService.DefaultIconId);
            insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static (string SelectedIcon, string Bio) ReadProfile(SqliteConnection connection, string playerId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = "SELECT selected_icon_id, bio FROM profiles WHERE player_id=$id";
        query.Parameters.AddWithValue("$id", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        if (!reader.Read())
            return (UnlockService.DefaultIconId, null);
        return (
            reader.IsDBNull(0) ? UnlockService.DefaultIconId : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }
}
