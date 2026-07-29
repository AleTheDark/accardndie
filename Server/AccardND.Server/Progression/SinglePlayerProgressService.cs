using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Data;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace AccardND.Server.Progression;

public sealed class SinglePlayerProgressService
{
    private readonly AccardDatabase database;

    private static readonly string[] StarterClassIds =
    {
        "mage",
        "warrior",
        "rogue"
    };

    // Boss finale di ogni capitolo. Il client manda solo l'id del boss sconfitto: la mappa
    // vive qui perche' il completamento capitolo e la concessione del capitolo successivo
    // devono restare decisioni del server.
    private static readonly Dictionary<string, string> ChapterByFinalBoss = new()
    {
        ["boss-bragus"] = "chapter-1",
        ["trentor"] = "chapter-2",
        ["boss-medusa"] = "chapter-3",
        ["boss-palatir"] = "chapter-4"
    };

    // Capitolo che si sblocca completando quello precedente. Il capitolo 4 chiude la
    // campagna, quindi non compare come chiave.
    private static readonly Dictionary<string, string> NextChapter = new()
    {
        ["chapter-1"] = "chapter-2",
        ["chapter-2"] = "chapter-3",
        ["chapter-3"] = "chapter-4"
    };

    // Listino di capitoli e modalita. Classi e tecniche non stanno qui: costo e prove
    // vivono nel SanctuaryCatalog, che e la sorgente unica per le voci del Santuario.
    private static readonly Dictionary<(string Type, string Id), int> UnlockCosts = new()
    {
        [("chapter", "chapter-1")] = 25,
        [("chapter", "chapter-2")] = 75,
        [("chapter", "chapter-3")] = 120,
        [("chapter", "chapter-4")] = 180,
        [("mode", "hardcore")] = 50
    };

    // Il tutorial non paga piu' miele: le quest della taverna sono l'unico rubinetto.
    // Regala pero' il primo capitolo, altrimenti il giocatore appena uscito dal tutorial
    // resterebbe fermo un giorno intero prima di poter entrare in campagna.
    private const string TutorialRewardChapterId = "chapter-1";
    private const int AdMultiplier = 3;
    private const int AccountAdMultiplier = 3;
    private const int AccountExperiencePerLevel = 100;
    private const int DeathRewardExperienceCeiling = 5000;

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

    /// <summary>
    /// Catalogo del Santuario con le prove gia' valutate sul giocatore. Il client riceve
    /// descrizione e progresso di ogni prova e si limita a disegnarli: le regole restano qui.
    /// </summary>
    public SanctuaryData GetSanctuary(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);
        SanctuaryData data = BuildSanctuary(connection, transaction, identity.PlayerId);
        transaction.Commit();
        return data;
    }

    /// <summary>
    /// Bacheca della taverna: le quest di oggi con il progresso del giocatore. In transazione
    /// perche' il primo contatto della giornata assegna le quest e ne fissa i baseline.
    /// </summary>
    public TavernData GetTavern(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);
        TavernData data = BuildTavern(connection, transaction, identity.PlayerId);
        transaction.Commit();
        return data;
    }

    /// <summary>Riscuote la ricompensa di una quest completata (idempotente per quest e giornata).</summary>
    public (TavernData Data, string ErrorCode, string Error) ClaimTavernQuest(
        AccountIdentity identity,
        TavernClaimQuestRequest request)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        string error = TavernQuests.ClaimQuest(connection, transaction, identity.PlayerId, request?.questId);
        if (error != null)
            return (null, ErrorCodes.InvalidProgressionRequest, error);

        TavernData data = BuildTavern(connection, transaction, identity.PlayerId);
        transaction.Commit();
        return (data, null, null);
    }

    /// <summary>
    /// Riscuote il premio di giornata (tutte le quest completate): miele piu' avanzamento
    /// di daily_completed, che e' la prova del Sacerdote al Santuario.
    /// </summary>
    public (TavernData Data, string ErrorCode, string Error) ClaimTavernBonus(AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        string error = TavernQuests.ClaimBonus(connection, transaction, identity.PlayerId);
        if (error != null)
            return (null, ErrorCodes.InvalidProgressionRequest, error);

        TavernData data = BuildTavern(connection, transaction, identity.PlayerId);
        transaction.Commit();
        return (data, null, null);
    }

    private static TavernData BuildTavern(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        int honey = ReadHoney(connection, transaction, playerId);
        return TavernQuests.ReadOrAssign(connection, transaction, playerId, honey);
    }

    /// <summary>
    /// Compra una copia di un consumabile e la mette nella scorta. E' l'operazione del
    /// negozio, non del Santuario: il Santuario sblocca il diritto di comprare, il negozio
    /// vende i pezzi. Per questo l'oggetto deve gia' risultare sbloccato.
    /// Il prezzo per copia arrivera' col listino del negozio; per ora e' una frazione del
    /// costo di sblocco, cosi' la funzione e' utilizzabile ma non regala niente.
    /// </summary>
    public (SanctuaryData Data, string ErrorCode, string Error) BuyItem(
        AccountIdentity identity,
        SanctuaryBuyItemRequest request)
    {
        string itemId = NormalizeKey(request?.itemId);
        if (!SanctuaryCatalog.TryGetEntry(SanctuaryCatalog.TypeItem, itemId, out SanctuaryCatalog.Entry entry))
            return (null, ErrorCodes.InvalidProgressionRequest, "Oggetto non valido.");

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        SinglePlayerProgressData current = ReadProgress(connection, identity.PlayerId, transaction);
        if (!IsAlreadyUnlocked(current, "item", itemId))
            return (null, ErrorCodes.InvalidProgressionRequest, "Oggetto non ancora sbloccato al Santuario.");

        int copyCost = SanctuaryCatalog.CopyCostOf(entry);
        string offerId = NormalizeKey(request?.offerId);
        if (!string.IsNullOrEmpty(offerId))
        {
            ShopOfferData offer = BuildShopOffers(connection, transaction, identity.PlayerId, current)
                .FirstOrDefault(candidate => candidate.offerId == offerId && candidate.itemId == itemId);
            if (offer == null || offer.remaining <= 0)
                return (null, ErrorCodes.InvalidProgressionRequest, "Offerta scaduta o esaurita.");
            copyCost = offer.offerCost;
            IncrementShopOfferPurchase(connection, transaction, identity.PlayerId, ShopRotationKey(), itemId);
        }
        if (current.honey < copyCost)
            return (null, ErrorCodes.InsufficientHoney, "Vasetti di miele insufficienti.");

        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE single_player_progress
                SET honey = honey - $cost, updated_at = $now
                WHERE player_id = $player";
            update.Parameters.AddWithValue("$cost", copyCost);
            update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$player", identity.PlayerId);
            update.ExecuteNonQuery();
        }
        SanctuaryBag.AddToStash(connection, transaction, identity.PlayerId, itemId, 1);

        SanctuaryData data = BuildSanctuary(connection, transaction, identity.PlayerId);
        transaction.Commit();
        return (data, null, null);
    }

    /// <summary>Sostituisce la bisaccia scelta per la prossima run.</summary>
    public (SanctuaryData Data, string ErrorCode, string Error) SetBag(
        AccountIdentity identity,
        SanctuarySetBagRequest request)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        string error = SanctuaryBag.ReplaceBag(connection, transaction, identity.PlayerId, request?.itemIds);
        if (error != null)
            return (null, ErrorCodes.InvalidProgressionRequest, error);

        SanctuaryData data = BuildSanctuary(connection, transaction, identity.PlayerId);
        transaction.Commit();
        return (data, null, null);
    }

    private SanctuaryData BuildSanctuary(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        SinglePlayerProgressData progress = ReadProgress(connection, playerId, transaction);
        var context = new SanctuaryRequirementContext(progress);
        var entries = new List<SanctuaryEntryData>();

        foreach (SanctuaryCatalog.Entry entry in SanctuaryCatalog.All)
        {
            var requirements = new SanctuaryRequirementData[entry.Requirements.Length];
            for (int index = 0; index < entry.Requirements.Length; index++)
            {
                SanctuaryCatalog.Requirement requirement = entry.Requirements[index];
                int current = context.CurrentValue(requirement);
                requirements[index] = new SanctuaryRequirementData
                {
                    description = requirement.Description,
                    // Il progresso mostrato non supera la soglia: "5/2 boss" sarebbe rumore.
                    current = Math.Min(current, requirement.Threshold),
                    threshold = requirement.Threshold,
                    met = current >= requirement.Threshold
                };
            }

            bool owned = IsAlreadyUnlocked(progress, entry.Type, entry.Id);

            entries.Add(new SanctuaryEntryData
            {
                type = entry.Type,
                id = entry.Id,
                name = entry.Name,
                description = entry.Description,
                honeyCost = entry.HoneyCost,
                copyCost = entry.Type == SanctuaryCatalog.TypeItem ? SanctuaryCatalog.CopyCostOf(entry) : 0,
                owned = owned,
                available = entry.Available && IsSanctuaryEntryOfferable(progress, entry),
                requirementsMet = context.AreAllMet(entry.Requirements),
                requirements = requirements
            });
        }

        return new SanctuaryData
        {
            honey = progress.honey,
            entries = entries.ToArray(),
            bagSlots = SanctuaryBag.ReadSlots(connection, transaction, playerId),
            stash = SanctuaryBag.ReadStash(connection, transaction, playerId),
            bag = progress.bagItems,
            shopOffers = BuildShopOffers(connection, transaction, playerId, progress)
        };
    }

    private static string ShopRotationKey() => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private static ShopOfferData[] BuildShopOffers(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, SinglePlayerProgressData progress)
    {
        string rotation = ShopRotationKey();
        var unlocked = SanctuaryCatalog.All
            .Where(entry => entry.Type == SanctuaryCatalog.TypeItem && IsAlreadyUnlocked(progress, "item", entry.Id))
            .OrderBy(entry => StableShopValue(playerId + "|" + rotation + "|" + entry.Id))
            .Take(3)
            .ToArray();
        var offers = new List<ShopOfferData>();
        foreach (SanctuaryCatalog.Entry entry in unlocked)
        {
            int seed = StableShopValue(rotation + "|" + playerId + "|" + entry.Id);
            int quantity = 1 + Math.Abs(seed % 3);
            int discount = 20 + Math.Abs((seed / 7) % 4) * 5;
            int regular = SanctuaryCatalog.CopyCostOf(entry);
            int purchased = ReadShopOfferPurchases(connection, transaction, playerId, rotation, entry.Id);
            offers.Add(new ShopOfferData
            {
                offerId = rotation + "-" + entry.Id,
                itemId = entry.Id,
                regularCost = regular,
                offerCost = Math.Max(1, regular * (100 - discount) / 100),
                remaining = Math.Max(0, quantity - purchased),
                discountPercent = discount
            });
        }
        return offers.ToArray();
    }

    private static int StableShopValue(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(hash, 0) & int.MaxValue;
    }

    private static int ReadShopOfferPurchases(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string rotation, string itemId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"SELECT count FROM player_shop_offer_purchases
            WHERE player_id = $player AND rotation = $rotation AND item_id = $item";
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$rotation", rotation);
        command.Parameters.AddWithValue("$item", itemId);
        object value = command.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    private static void IncrementShopOfferPurchase(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, string rotation, string itemId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"INSERT INTO player_shop_offer_purchases(player_id, rotation, item_id, count)
            VALUES($player, $rotation, $item, 1)
            ON CONFLICT(player_id, rotation, item_id) DO UPDATE SET count = count + 1";
        command.Parameters.AddWithValue("$player", playerId);
        command.Parameters.AddWithValue("$rotation", rotation);
        command.Parameters.AddWithValue("$item", itemId);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Gli slot si comprano in ordine: offrire il quarto prima del terzo farebbe pagare di
    /// piu' per lo stesso passo.
    /// </summary>
    private static bool IsSanctuaryEntryOfferable(SinglePlayerProgressData progress, SanctuaryCatalog.Entry entry)
    {
        if (entry.Type != SanctuaryCatalog.TypeSlot || entry.Id != "bag-slot-4")
            return true;
        return IsAlreadyUnlocked(progress, "slot", "bag-slot-3");
    }

    public (SinglePlayerProgressData Progress, string ErrorCode, string Error) PurchaseUnlock(
        AccountIdentity identity,
        SinglePlayerPurchaseUnlockRequest request)
    {
        string type = NormalizeUnlockType(request?.type);
        string id = NormalizeKey(request?.id);
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id))
            return (null, ErrorCodes.InvalidProgressionRequest, "Unlock non valido.");

        // Il completamento di un capitolo si guadagna giocando, non si compra.
        if (type == "chapterCleared")
            return (null, ErrorCodes.InvalidProgressionRequest, "Un capitolo si completa giocandolo.");

        // Le voci del Santuario (classi, tecniche) prendono costo e prove dal catalogo; capitoli
        // e modalita' restano sul listino semplice.
        SanctuaryCatalog.Entry catalogEntry = null;
        int cost;
        if (SanctuaryCatalog.TryGetEntry(type, id, out catalogEntry))
        {
            if (!catalogEntry.Available)
            {
                return (null, ErrorCodes.InvalidProgressionRequest, catalogEntry.Type == SanctuaryCatalog.TypeClass
                    ? "Classe base: si sblocca completando il tutorial."
                    : "Non ancora acquistabile.");
            }
            cost = catalogEntry.HoneyCost;
        }
        else if (type == "class" || type == "secondAbility" || type == "slot" || type == "item")
        {
            return (null, ErrorCodes.InvalidProgressionRequest, "Voce non presente nel Santuario.");
        }
        else if (!UnlockCosts.TryGetValue((type, id), out cost))
        {
            return (null, ErrorCodes.InvalidProgressionRequest, "Unlock non acquistabile.");
        }

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        SinglePlayerProgressData current = ReadProgress(connection, identity.PlayerId, transaction);
        if (type == "class" && !current.tutorialCompleted)
            return (null, ErrorCodes.InvalidProgressionRequest, "Completa il tutorial prima di sbloccare nuove classi.");

        if (IsAlreadyUnlocked(current, type, id))
        {
            transaction.Commit();
            return (current, null, null);
        }

        if (catalogEntry != null && !IsSanctuaryEntryOfferable(current, catalogEntry))
        {
            return (null, ErrorCodes.InvalidProgressionRequest, "Sblocca prima lo slot precedente.");
        }

        // Le prove si valutano sullo stesso snapshot che il client ha visto: senza questo
        // controllo il gating sarebbe solo decorativo lato client.
        if (catalogEntry != null)
        {
            var context = new SanctuaryRequirementContext(current);
            SanctuaryCatalog.Requirement unmet =
                catalogEntry.Requirements.FirstOrDefault(requirement => !context.IsMet(requirement));
            if (unmet != null)
                return (null, ErrorCodes.RequirementsNotMet, $"Prova non ancora superata: {unmet.Description}.");
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
    /// Registra il completamento di un capitolo a partire dal boss finale sconfitto e concede
    /// (senza costo) lo sblocco del capitolo successivo. Idempotente: ripetere la chiamata per
    /// un capitolo gia' completato non cambia nulla.
    /// Nota: il combattimento single player e' client-side, quindi il server non puo' validare
    /// la vittoria; possiede pero' la mappa boss-capitolo e la concessione dello sblocco, che
    /// prima avvenivano solo nella cache locale del client e andavano perse alla sincronizzazione.
    /// </summary>
    public (SinglePlayerProgressData Progress, string ErrorCode, string Error) ClearChapter(
        AccountIdentity identity,
        SinglePlayerClearChapterRequest request)
    {
        string bossId = NormalizeKey(request?.bossId);
        if (string.IsNullOrEmpty(bossId))
            return (null, ErrorCodes.InvalidProgressionRequest, "Boss non valido.");

        if (!ChapterByFinalBoss.TryGetValue(bossId, out string chapterId))
            return (null, ErrorCodes.InvalidProgressionRequest, "Boss non associato a un capitolo.");

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        GrantUnlock(connection, transaction, identity.PlayerId, "chapterCleared", chapterId);
        if (NextChapter.TryGetValue(chapterId, out string nextChapterId))
            GrantUnlock(connection, transaction, identity.PlayerId, "chapter", nextChapterId);

        // Il contatore del boss cresce a ogni vittoria, anche su un capitolo gia' completato:
        // i requisiti del Santuario chiedono piu' vittorie sullo stesso boss. E' l'unlock a
        // essere idempotente, non il conteggio.
        if (CampaignCounters.TryGetBossCounterKey(bossId, out string bossCounterKey))
        {
            CampaignCounters.Increment(connection, transaction, identity.PlayerId, bossCounterKey, 1);
            CampaignCounters.Increment(connection, transaction, identity.PlayerId, CampaignCounters.BossesDefeated, 1);
        }

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (progress, null, null);
    }

    /// <summary>
    /// Registra il completamento del tutorial (idempotente: una sola volta per account,
    /// governata dal flag tutorial_completed). Non paga miele - quello arriva solo dalle
    /// quest della taverna - ma consegna le classi base e il primo capitolo, che sono la
    /// dotazione con cui si comincia a giocare.
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
            // Gia riscattata: risposta idempotente, niente di nuovo concesso.
            transaction.Commit();
            return (BuildReward(current, null, 0), null, null);
        }

        string claimId = NewClaimId();
        RecordClaim(connection, transaction, claimId, identity.PlayerId, "tutorial",
            0, 0, Normalize(request?.tutorialRunId));

        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE single_player_progress
                SET tutorial_completed = 1, updated_at = $now
                WHERE player_id = $player";
            update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$player", identity.PlayerId);
            update.ExecuteNonQuery();
        }

        GrantStarterClasses(connection, transaction, identity.PlayerId);
        GrantUnlock(connection, transaction, identity.PlayerId, "chapter", TutorialRewardChapterId);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimId, 0), null, null);
    }

    /// <summary>
    /// Concede la ricompensa di fine run. La campagna assegna esclusivamente esperienza
    /// account, pari a un decimo dell'esperienza non spesa nella run.
    /// Idempotente per runId: piu chiamate con lo stesso runId non accreditano due volte.
    /// Nota: il combattimento single player e client-side, quindi il server non puo validare
    /// pienamente l'evento; possiede pero formula, cap e idempotenza.
    /// </summary>
    public (SinglePlayerRewardResult Result, string ErrorCode, string Error) ClaimDeathReward(
        AccountIdentity identity,
        SinglePlayerDeathRewardRequest request)
    {
        const int baseHoney = 0;
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
        int baseAccountExperience = CalculateAccountExperience(request);
        RecordClaim(connection, transaction, claimId, identity.PlayerId, "death", baseHoney, baseAccountExperience, runId);
        RecordCampaignRun(connection, transaction, identity.PlayerId, request, baseHoney);
        // Dopo il controllo di idempotenza sul runId: una run riscattata due volte non deve
        // gonfiare i contatori.
        CampaignCounters.RecordRunSummary(connection, transaction, identity.PlayerId, request);
        SanctuaryBag.ConsumeFromStash(connection, transaction, identity.PlayerId, request?.consumedItemIds);
        int levelsGained = GrantAccountExperience(connection, transaction, identity.PlayerId, baseAccountExperience);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimId, baseHoney, baseAccountExperience, levelsGained), null, null);
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
        int baseAccountExperience;
        int multiplier;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = @"
                SELECT base_honey, base_account_experience, multiplier FROM single_player_reward_claims
                WHERE claim_id = $claim AND player_id = $player";
            query.Parameters.AddWithValue("$claim", claimRef);
            query.Parameters.AddWithValue("$player", identity.PlayerId);
            using SqliteDataReader reader = query.ExecuteReader();
            if (!reader.Read())
                return (null, ErrorCodes.RewardClaimNotFound, "Ricompensa non trovata.");
            baseHoney = reader.GetInt32(0);
            baseAccountExperience = reader.GetInt32(1);
            multiplier = reader.GetInt32(2);
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

        // baseHoney e' zero per ogni tipo di reward da quando le quest della taverna sono
        // l'unico rubinetto di miele: il moltiplicatore pubblicitario oggi vale solo sull'EXP
        // account. Il calcolo resta perche' e' la reward a decidere la propria base, non l'ad.
        int extraHoney = baseHoney * (AdMultiplier - 1);
        int extraAccountExperience = baseAccountExperience * (AccountAdMultiplier - 1);
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
        int levelsGained = GrantAccountExperience(connection, transaction, identity.PlayerId, extraAccountExperience);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimRef, extraHoney, extraAccountExperience, levelsGained), null, null);
    }

    private static int CalculateAccountExperience(SinglePlayerDeathRewardRequest request)
    {
        if (request == null)
            return 0;
        int farmedExperience = Math.Clamp(request.matchExperience, 0, DeathRewardExperienceCeiling);
        return farmedExperience / 10;
    }

    private static SinglePlayerRewardResult BuildReward(
        SinglePlayerProgressData progress,
        string claimId,
        int grantedHoney,
        int grantedAccountExperience = 0,
        int levelsGained = 0) => new()
    {
        progress = progress,
        rewardClaimId = claimId,
        grantedHoney = grantedHoney,
        grantedAccountExperience = grantedAccountExperience,
        levelsGained = levelsGained
    };

    private static string NewClaimId() => Guid.NewGuid().ToString("N");

    private static void RecordClaim(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string claimId,
        string playerId,
        string rewardType,
        int baseHoney,
        int baseAccountExperience,
        string sourceRef)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT INTO single_player_reward_claims
                (claim_id, player_id, reward_type, base_honey, base_account_experience, multiplier, source_ref, created_at)
            VALUES ($claim, $player, $type, $base, $xp, 1, $ref, $now)";
        insert.Parameters.AddWithValue("$claim", claimId);
        insert.Parameters.AddWithValue("$player", playerId);
        insert.Parameters.AddWithValue("$type", rewardType);
        insert.Parameters.AddWithValue("$base", baseHoney);
        insert.Parameters.AddWithValue("$xp", Math.Max(0, baseAccountExperience));
        insert.Parameters.AddWithValue("$ref", (object)sourceRef ?? DBNull.Value);
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();
    }

    /// <summary>
    /// Registra la run di campagna conclusa (una riga per morte) per lo storico admin.
    /// I valori arrivano dal sommario client e vengono normalizzati prima del salvataggio.
    /// </summary>
    private static void RecordCampaignRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        SinglePlayerDeathRewardRequest request,
        int honeyReward)
    {
        using SqliteCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = @"
            INSERT INTO campaign_runs
                (player_id, client_run_ref, mode, chapter_id, stage_id,
                 rooms_cleared, enemies_defeated, bosses_defeated, minibosses_defeated,
                 defeated_boss_ids, honey_reward, ended_at)
            VALUES ($player, $ref, $mode, $chapter, $stage,
                    $rooms, $enemies, $bosses, $minibosses, $bossIds, $honey, $now)";
        insert.Parameters.AddWithValue("$player", playerId);
        insert.Parameters.AddWithValue("$ref", NullIfEmpty(request?.runId));
        insert.Parameters.AddWithValue("$mode", NullIfEmpty(request?.mode));
        insert.Parameters.AddWithValue("$chapter", NullIfEmpty(request?.chapterId));
        insert.Parameters.AddWithValue("$stage", NullIfEmpty(request?.stageId));
        insert.Parameters.AddWithValue("$rooms", Math.Max(0, request?.roomsCleared ?? 0));
        insert.Parameters.AddWithValue("$enemies", Math.Max(0, request?.enemiesDefeated ?? 0));
        insert.Parameters.AddWithValue("$bosses", Math.Max(0, request?.bossesDefeated ?? 0));
        insert.Parameters.AddWithValue("$minibosses", Math.Max(0, request?.minibossesDefeated ?? 0));
        insert.Parameters.AddWithValue("$bossIds", JoinBossIds(request?.defeatedBossIds));
        insert.Parameters.AddWithValue("$honey", honeyReward);
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

    private static int GrantAccountExperience(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int amount)
    {
        if (amount <= 0)
            return 0;

        int level = 1;
        int current = 0;
        int total = 0;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = @"
                SELECT account_level, account_experience, account_total_experience
                FROM single_player_progress
                WHERE player_id = $player";
            query.Parameters.AddWithValue("$player", playerId);
            using SqliteDataReader reader = query.ExecuteReader();
            if (reader.Read())
            {
                level = Math.Max(1, reader.GetInt32(0));
                current = Math.Max(0, reader.GetInt32(1));
                total = Math.Max(0, reader.GetInt32(2));
            }
        }

        current += amount;
        total += amount;
        int levelsGained = 0;
        while (current >= AccountExperiencePerLevel)
        {
            current -= AccountExperiencePerLevel;
            level++;
            levelsGained++;
        }

        // Il livello account non paga piu' miele: salire di livello giocando era un rubinetto
        // parallelo a quello delle quest, e le quest devono restare l'unico.
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_progress
            SET account_level = $level,
                account_experience = $xp,
                account_total_experience = $total,
                account_experience_to_next_level = $next,
                updated_at = $now
            WHERE player_id = $player";
        update.Parameters.AddWithValue("$level", level);
        update.Parameters.AddWithValue("$xp", current);
        update.Parameters.AddWithValue("$total", total);
        update.Parameters.AddWithValue("$next", AccountExperiencePerLevel);
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$player", playerId);
        update.ExecuteNonQuery();
        return levelsGained;
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
            "chapterCleared" => progress.clearedChapters,
            "slot" => progress.unlockedSlots,
            "item" => progress.unlockedItems,
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
                (player_id, honey, account_level, account_experience, account_total_experience,
                 account_experience_to_next_level, tutorial_completed, hardcore_unlocked, updated_at)
            VALUES ($player, 0, 1, 0, 0, $next, 0, 0, $now)";
        insert.Parameters.AddWithValue("$player", playerId);
        insert.Parameters.AddWithValue("$next", AccountExperiencePerLevel);
        insert.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        insert.ExecuteNonQuery();

        // Le quest del giorno si assegnano al primo contatto di progressione, non alla prima
        // apertura della taverna. Il baseline e' il valore dei contatori all'assegnazione:
        // farlo scattare in taverna significava che una run giocata prima di passare
        // dall'oste finiva sotto la linea di partenza e non contava niente.
        // Qui il momento e' la connessione, che avviene prima di poter giocare.
        TavernQuests.AssignIfMissing(connection, transaction, playerId);

        if (!HasCompletedTutorial(connection, transaction, playerId))
            return;

        GrantStarterClasses(connection, transaction, playerId);
        // Anche per chi aveva finito il tutorial quando ancora pagava miele: il primo
        // capitolo era comprabile con quei 60 vasetti, ora e' parte della dotazione.
        GrantUnlock(connection, transaction, playerId, "chapter", TutorialRewardChapterId);
    }

    private static int ReadHoney(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "SELECT honey FROM single_player_progress WHERE player_id = $player";
        query.Parameters.AddWithValue("$player", playerId);
        object value = query.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
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
                SELECT honey, account_level, account_experience, account_total_experience,
                       account_experience_to_next_level, tutorial_completed, hardcore_unlocked
                FROM single_player_progress
                WHERE player_id = $player";
            query.Parameters.AddWithValue("$player", playerId);
            using SqliteDataReader reader = query.ExecuteReader();
            if (reader.Read())
            {
                data.honey = reader.GetInt32(0);
                data.accountLevel = Math.Max(1, reader.GetInt32(1));
                data.accountExperience = Math.Max(0, reader.GetInt32(2));
                data.accountTotalExperience = Math.Max(0, reader.GetInt32(3));
                data.accountExperienceToNextLevel = Math.Max(1, reader.GetInt32(4));
                data.tutorialCompleted = reader.GetInt32(5) != 0;
                data.hardcoreUnlocked = reader.GetInt32(6) != 0;
            }
        }

        data.unlockedChapters = ReadUnlocks(connection, transaction, playerId, "chapter");
        data.unlockedStages = ReadUnlocks(connection, transaction, playerId, "stage");
        data.unlockedClasses = ReadUnlocks(connection, transaction, playerId, "class");
        if (data.tutorialCompleted)
            data.unlockedClasses = MergeStarterClasses(data.unlockedClasses);
        data.unlockedScenarios = ReadUnlocks(connection, transaction, playerId, "scenario");
        data.unlockedSecondAbilities = ReadUnlocks(connection, transaction, playerId, "secondAbility");
        data.clearedChapters = ReadUnlocks(connection, transaction, playerId, "chapterCleared");
        data.unlockedSlots = ReadUnlocks(connection, transaction, playerId, "slot");
        data.unlockedItems = ReadUnlocks(connection, transaction, playerId, "item");
        data.bagItems = SanctuaryBag.ReadBag(connection, transaction, playerId);
        data.counters = CampaignCounters.Read(connection, transaction, playerId);
        return data;
    }

    private static bool HasCompletedTutorial(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId)
    {
        using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = @"
            SELECT tutorial_completed
            FROM single_player_progress
            WHERE player_id = $player";
        query.Parameters.AddWithValue("$player", playerId);
        object result = query.ExecuteScalar();
        return result != null && result != DBNull.Value && Convert.ToInt32(result) != 0;
    }

    private static void GrantStarterClasses(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId)
    {
        foreach (string classId in StarterClassIds)
            GrantUnlock(connection, transaction, playerId, "class", classId);
    }

    private static string[] MergeStarterClasses(string[] unlockedClasses)
    {
        var result = new List<string>(unlockedClasses ?? Array.Empty<string>());
        foreach (string classId in StarterClassIds)
        {
            if (result.IndexOf(classId) < 0)
                result.Add(classId);
        }
        return result.ToArray();
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

    private static string NormalizeKey(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string NormalizeUnlockType(string value)
    {
        string normalized = NormalizeKey(value);
        return normalized switch
        {
            "chapter" => "chapter",
            "stage" => "stage",
            "class" => "class",
            "scenario" => "scenario",
            "secondability" => "secondAbility",
            "chaptercleared" => "chapterCleared",
            "slot" => "slot",
            "item" => "item",
            "mode" => "mode",
            _ => normalized
        };
    }

    private static object NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    /// <summary>
    /// Normalizza e concatena gli id dei boss sconfitti. Il cap evita che un client
    /// manipolato faccia crescere la riga a piacere.
    /// </summary>
    private static object JoinBossIds(string[] bossIds)
    {
        if (bossIds == null || bossIds.Length == 0)
            return DBNull.Value;

        var normalized = new List<string>();
        foreach (string bossId in bossIds)
        {
            string key = NormalizeKey(bossId);
            if (!string.IsNullOrEmpty(key) && normalized.Count < 20)
                normalized.Add(key);
        }
        return normalized.Count == 0 ? DBNull.Value : string.Join(",", normalized);
    }
}
