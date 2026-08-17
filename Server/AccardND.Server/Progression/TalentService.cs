using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;

namespace AccardND.Server.Progression;

/// <summary>
/// L'albero dei talenti dal lato del giocatore: cosa possiede, cosa puo' comprare e quali
/// modificatori la sua run si porta dietro.
///
/// L'acquisto e' server-authoritative come gli sblocchi del Santuario. Il combattimento
/// single player resta client-side e non e' pienamente validabile, ma la <em>spesa</em> di
/// una valuta non puo' stare sul client: e' l'unica parte che il server puo' possedere
/// davvero, quindi la possiede.
/// </summary>
public sealed class TalentService
{
    private readonly AccardDatabase database;

    /// <summary>Nome e sottotitolo di ogni ramo, per la schermata.</summary>
    private static readonly (string Id, string Name, string Description)[] Branches =
    {
        (TalentCatalog.BranchPurse, "Economia", "Oro, essenza e prezzi: quanto ti puoi permettere in una run."),
        (TalentCatalog.BranchInitiative, "Iniziativa", "Chi agisce per primo quando comincia lo scontro."),
        (TalentCatalog.BranchMastery, "Maestria", "Salire di livello prima e avere piu' mana per le abilita'."),
        (TalentCatalog.BranchOccasion, "Occasioni", "Vantaggi che scattano da soli, nel momento giusto.")
    };

    public TalentService(AccardDatabase database)
    {
        this.database = database;
    }

    public TalentData GetTalents(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        return BuildTalents(connection, null, identity.PlayerId);
    }

    /// <summary>
    /// Compra un rango. Valida in transazione punti, cancello di tier e rango massimo: il
    /// client manda solo l'id del nodo e non calcola nessun prezzo.
    /// </summary>
    public (TalentData Data, string ErrorCode, string Error) BuyTalent(
        AccountIdentity identity,
        TalentBuyRequest request)
    {
        string talentId = request?.talentId?.Trim();
        if (string.IsNullOrEmpty(talentId) || !TalentCatalog.TryGet(talentId, out TalentCatalog.Talent talent))
            return (null, ErrorCodes.InvalidProgressionRequest, "Talento sconosciuto.");

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();

        Dictionary<string, int> ranks = ReadRanks(connection, transaction, identity.PlayerId);
        int currentRank = ranks.TryGetValue(talent.Id, out int owned) ? owned : 0;
        if (currentRank >= TalentCatalog.MaxRankOf(talent))
            return (null, ErrorCodes.InvalidProgressionRequest, "Questo talento e' gia' al rango massimo.");

        int gate = TalentCatalog.TierGateOf(talent);
        int spentInBranch = PointsSpentInBranch(ranks, talent.Branch);
        if (spentInBranch < gate)
        {
            return (
                null,
                ErrorCodes.InvalidProgressionRequest,
                $"Servono {gate} propoli spesi in questo ramo: ne hai {spentInBranch}.");
        }

        int available = ReadPoints(connection, transaction, identity.PlayerId);
        if (available < talent.CostPerRank)
        {
            return (
                null,
                ErrorCodes.InvalidProgressionRequest,
                $"Ti servono {talent.CostPerRank} propoli: ne hai {available}.");
        }

        using (SqliteCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = @"
                INSERT INTO player_talents (player_id, talent_id, rank, updated_at)
                VALUES ($player, $talent, $rank, $now)
                ON CONFLICT(player_id, talent_id) DO UPDATE SET rank = $rank, updated_at = $now";
            upsert.Parameters.AddWithValue("$player", identity.PlayerId);
            upsert.Parameters.AddWithValue("$talent", talent.Id);
            upsert.Parameters.AddWithValue("$rank", currentRank + 1);
            upsert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            upsert.ExecuteNonQuery();
        }

        using (SqliteCommand spend = connection.CreateCommand())
        {
            spend.Transaction = transaction;
            spend.CommandText = @"
                UPDATE single_player_progress
                SET talent_points = talent_points - $cost, updated_at = $now
                WHERE player_id = $player";
            spend.Parameters.AddWithValue("$cost", talent.CostPerRank);
            spend.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            spend.Parameters.AddWithValue("$player", identity.PlayerId);
            spend.ExecuteNonQuery();
        }

        TalentData data = BuildTalents(connection, transaction, identity.PlayerId);
        transaction.Commit();
        return (data, null, null);
    }

    /// <summary>
    /// I modificatori di una run, risolti dai ranghi posseduti. Piatto e senza logica: il
    /// client lo innesta e basta.
    /// </summary>
    public TalentLoadoutData GetLoadout(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        return BuildLoadout(ReadRanks(connection, null, identity.PlayerId));
    }

    /// <summary>
    /// Come sopra, ma su una connessione gia' aperta: la progressione lo allega al proprio
    /// snapshot senza aprire una seconda connessione a ogni lettura.
    /// </summary>
    internal static TalentLoadoutData BuildLoadoutFor(
        SqliteConnection connection, SqliteTransaction transaction, string playerId) =>
        BuildLoadout(ReadRanks(connection, transaction, playerId));

    private static TalentLoadoutData BuildLoadout(Dictionary<string, int> ranks)
    {
        var loadout = new TalentLoadoutData
        {
            // Tre pedine in formazione, tre bonus: l'array e' sempre pieno anche a zero,
            // cosi' il client puo' indicizzarlo senza controllare la lunghezza.
            initiativeBonusBySlot = new int[3]
        };

        loadout.startingGold = ValueOf(ranks, "purse-travel-fund");
        // startingEssence resta a zero: nessun talento lo alimenta piu' da quando "Forgia
        // generosa" e' stato ritirato. Il campo non e' stato tolto dal protocollo apposta -
        // lo leggono un save del client e un paio di test - e toglierlo avrebbe voluto dire
        // toccare il formato di salvataggio per guadagnarci un intero.
        loadout.merchantDiscountPercent = ValueOf(ranks, "purse-kind-merchant");
        loadout.forgeTemperedCards = ValueOf(ranks, "purse-smith-temper");
        loadout.firstMerchantUpgradeFree = RankOf(ranks, "purse-first-deal") > 0;

        loadout.initiativeBonusBySlot[0] = ValueOf(ranks, "initiative-vanguard");
        loadout.initiativeBonusBySlot[1] = ValueOf(ranks, "initiative-flanker");
        loadout.initiativeBonusBySlot[2] = ValueOf(ranks, "initiative-rearguard");
        loadout.opensEveryFight = RankOf(ranks, "initiative-opening") > 0;

        loadout.masteryThresholdPercent = ValueOf(ranks, "mastery-apprentice");
        loadout.roomChangeMana = ValueOf(ranks, "mastery-focus");
        loadout.bonusMaximumMana = ValueOf(ranks, "mastery-reserve");
        loadout.firstAbilityFreeEachRoom = RankOf(ranks, "mastery-trance") > 0;

        loadout.recoveryDiscountPercent = ValueOf(ranks, "occasion-recovery");
        loadout.bossVigorBonus = ValueOf(ranks, "occasion-challenger");
        loadout.extraLootItems = ValueOf(ranks, "occasion-seeker");
        loadout.savesFirstFallenCard = RankOf(ranks, "occasion-second-wind") > 0;

        return loadout;
    }

    private TalentData BuildTalents(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        Dictionary<string, int> ranks = ReadRanks(connection, transaction, playerId);
        int available = ReadPoints(connection, transaction, playerId);
        int earned = ReadPointsEarned(connection, transaction, playerId);

        var branches = new List<TalentBranchData>();
        foreach ((string id, string name, string description) in Branches)
        {
            branches.Add(new TalentBranchData
            {
                id = id,
                name = name,
                description = description,
                pointsSpent = PointsSpentInBranch(ranks, id)
            });
        }

        var entries = new List<TalentEntryData>();
        foreach (TalentCatalog.Talent talent in TalentCatalog.All)
        {
            int rank = ranks.TryGetValue(talent.Id, out int owned) ? owned : 0;
            int maxRank = TalentCatalog.MaxRankOf(talent);
            int gate = TalentCatalog.TierGateOf(talent);
            int spent = PointsSpentInBranch(ranks, talent.Branch);
            bool tierUnlocked = spent >= gate;
            bool maxed = rank >= maxRank;
            bool affordable = available >= talent.CostPerRank;

            entries.Add(new TalentEntryData
            {
                id = talent.Id,
                branch = talent.Branch,
                tier = talent.Tier,
                name = talent.Name,
                description = talent.Description,
                rank = rank,
                maxRank = maxRank,
                nextCost = maxed ? 0 : talent.CostPerRank,
                currentValue = TalentCatalog.ValueAtRank(talent, rank),
                nextValue = maxed ? 0 : TalentCatalog.ValueAtRank(talent, rank + 1),
                currentValueText = TalentCatalog.FormatValueAtRank(talent, rank),
                nextValueText = maxed ? null : TalentCatalog.FormatValueAtRank(talent, rank + 1),
                tierGate = gate,
                tierUnlocked = tierUnlocked,
                purchasable = !maxed && tierUnlocked && affordable,
                lockedReason = LockedReason(maxed, tierUnlocked, affordable, gate, spent, talent.CostPerRank)
            });
        }

        return new TalentData
        {
            talentPoints = available,
            talentPointsEarned = earned,
            branches = branches.ToArray(),
            talents = entries.ToArray()
        };
    }

    /// <summary>
    /// Il motivo del blocco, in ordine di importanza: il tier chiuso viene prima dei punti
    /// mancanti, perche' e' quello che dice al giocatore dove deve spendere.
    ///
    /// La valuta si chiama "propoli" qui come sul contatore della schermata. Prima queste
    /// righe dicevano "punti talento" mentre il favo diceva "PUNTI PROPOLI": due nomi per la
    /// stessa cosa, a mezzo centimetro di distanza.
    /// </summary>
    private static string LockedReason(
        bool maxed, bool tierUnlocked, bool affordable, int gate, int spent, int cost)
    {
        if (maxed)
            return "Rango massimo raggiunto.";
        if (!tierUnlocked)
            return $"Spendi altri {gate - spent} propoli in questo ramo per aprire questa cella.";
        if (!affordable)
            return $"Ti servono {cost} propoli.";
        return null;
    }

    private static int PointsSpentInBranch(Dictionary<string, int> ranks, string branch)
    {
        int spent = 0;
        foreach (TalentCatalog.Talent talent in TalentCatalog.All)
        {
            if (!string.Equals(talent.Branch, branch, StringComparison.Ordinal))
                continue;
            if (ranks.TryGetValue(talent.Id, out int rank))
                spent += rank * talent.CostPerRank;
        }
        return spent;
    }

    private static int RankOf(Dictionary<string, int> ranks, string talentId) =>
        ranks.TryGetValue(talentId, out int rank) ? rank : 0;

    private static int ValueOf(Dictionary<string, int> ranks, string talentId) =>
        TalentCatalog.TryGet(talentId, out TalentCatalog.Talent talent)
            ? TalentCatalog.ValueAtRank(talent, RankOf(ranks, talentId))
            : 0;

    private static Dictionary<string, int> ReadRanks(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT talent_id, rank FROM player_talents WHERE player_id = $player";
        query.Parameters.AddWithValue("$player", playerId);
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            // I nodi tolti dal catalogo restano in tabella ma non contano piu': leggere una
            // riga orfana come punti spesi falserebbe i cancelli di tier.
            string id = reader.GetString(0);
            if (TalentCatalog.TryGet(id, out _))
                ranks[id] = Math.Max(0, reader.GetInt32(1));
        }
        return ranks;
    }

    private static int ReadPoints(
        SqliteConnection connection, SqliteTransaction transaction, string playerId) =>
        ReadColumn(connection, transaction, playerId, "talent_points");

    private static int ReadPointsEarned(
        SqliteConnection connection, SqliteTransaction transaction, string playerId) =>
        ReadColumn(connection, transaction, playerId, "talent_points_earned");

    private static int ReadColumn(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string column)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = $"SELECT {column} FROM single_player_progress WHERE player_id = $player";
        query.Parameters.AddWithValue("$player", playerId);
        object value = query.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Math.Max(0, Convert.ToInt32(value));
    }
}
