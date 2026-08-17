using AccardND.GameCore;
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

    /// <summary>
    /// Le classi che il vecchio tutorial monolitico consegnava gratis. Non e' piu' la
    /// dotazione di partenza - oggi il percorso regala il solo Guerriero e fa comprare Mago e
    /// Ladro col miele che dona (§8.1 del design) - ma resta la dotazione di chi quel
    /// tutorial lo aveva gia' finito: a nessuno si toglie quello che aveva.
    /// </summary>
    private static readonly string[] LegacyTutorialClassIds =
    {
        "mage",
        "warrior",
        "rogue"
    };

    // Listino delle sole modalita. Capitoli, classi e tecniche non stanno qui: costo e
    // prove vivono nel SanctuaryCatalog, che e la sorgente unica per le voci del Santuario.
    private static readonly Dictionary<(string Type, string Id), int> UnlockCosts = new()
    {
        [("mode", "hardcore")] = 50
    };

    // Il tutorial non paga piu' miele: le quest della taverna sono l'unico rubinetto.
    // Regala pero' il primo capitolo, altrimenti il giocatore appena uscito dal tutorial
    // resterebbe fermo un giorno intero prima di poter entrare in campagna.
    private const string TutorialRewardChapterId = ChapterCatalog.TutorialChapterId;
    private const int AdMultiplier = 3;

    // Il video moltiplica l'exp account di una run gia' conclusa.
    //
    // E' stato x2 per un breve periodo, per paura che x3 sopra il x2.5 del settimo capitolo
    // facesse una run da x7.5. Quel timore nasceva da un conto sbagliato: dava per scontato
    // che una run arrivasse al tetto delle 5000, mentre una run completa di 25 stanze ne
    // produce circa 650 (una stanza mostro paga 5-15 di base piu' la forza dei caduti, un
    // miniboss 50). Il x7.5 vero vale quindi ~490 di exp account, non migliaia: e' una run
    // ottima, non un salto di livelli.
    //
    // Con la curva nuova - dove un livello costa 100 + 25 per livello - il video e' quello
    // che tiene il ritmo su valori umani. Vedi Docs/talenti-design.md.
    private const int AccountAdMultiplier = 3;

    // Rete contro un client che mente sull'esperienza di run, non un tetto di bilanciamento:
    // una run vera ne produce circa un decimo, quindi non lo tocca mai.
    private const int DeathRewardExperienceCeiling = 5000;

    /// <summary>
    /// Punti per la prima uccisione del boss di un capitolo. Sono la sola sorgente di punti
    /// che non passa dal livello: premiano l'avanzamento in campagna invece del tempo
    /// passato a giocare, e su sette capitoli valgono 21 punti in tutto.
    /// </summary>

    // Per quanto un x3 mai riscosso resta in offerta nel profilo. Una finestra serve: senza,
    // il giocatore che torna dopo mesi si trova una pila di video da guardare e il momento
    // in cui la ricompensa e' stata guadagnata non significa piu' niente. Una settimana copre
    // la disconnessione a fine run, che e' il caso per cui l'offerta esiste.
    private const int PendingAdRewardWindowHours = 168;

    // Quante offerte al massimo il profilo mostra in una volta: sono le piu' recenti.
    private const int PendingAdRewardLimit = 20;

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

        int rewardMultiplier = request?.rewardMultiplier ?? 0;
        if (rewardMultiplier == 0)
            rewardMultiplier = 1;
        if (rewardMultiplier != 1 && rewardMultiplier != 5)
            return (null, ErrorCodes.InvalidProgressionRequest, "Moltiplicatore ricompensa non valido.");

        string error = TavernQuests.ClaimQuest(
            connection, transaction, identity.PlayerId, request?.questId, rewardMultiplier);
        if (error != null)
            return (null, ErrorCodes.InvalidProgressionRequest, error);

        if (rewardMultiplier == 1
            && TavernQuests.TryDescribe(request?.questId, out TavernQuests.QuestDefinition quest))
            RecordClaim(connection, transaction, NewClaimId(), identity.PlayerId, "tavern",
                TavernQuests.HoneyRewardFor(quest.Difficulty), 0, request?.questId);

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
    /// Compra una copia di un consumabile e la mette nella scorta. Tutti i consumabili del
    /// catalogo sono acquistabili fin dall'inizio; il Santuario gestisce soltanto gli slot.
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

        foreach (SanctuaryCatalog.Entry entry in SanctuaryCatalog.All.Where(entry => entry.Type != SanctuaryCatalog.TypeItem))
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
                copyCost = 0,
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
            shopCatalog = SanctuaryCatalog.All
                .Where(entry => entry.Type == SanctuaryCatalog.TypeItem)
                .Select(entry => new SanctuaryEntryData
                {
                    type = entry.Type,
                    id = entry.Id,
                    name = entry.Name,
                    description = entry.Description,
                    honeyCost = entry.HoneyCost,
                    copyCost = SanctuaryCatalog.CopyCostOf(entry),
                    owned = true,
                    available = true,
                    requirementsMet = true,
                    requirements = Array.Empty<SanctuaryRequirementData>()
                })
                .ToArray(),
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
            .Where(entry => entry.Type == SanctuaryCatalog.TypeItem)
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
    /// Cosa si puo' comprare adesso, a parita' di miele. Gli slot si comprano in ordine:
    /// il quarto prima del terzo farebbe pagare di piu' per lo stesso passo.
    /// </summary>
    private static bool IsSanctuaryEntryOfferable(SinglePlayerProgressData progress, SanctuaryCatalog.Entry entry)
    {
		if (entry.Type == SanctuaryCatalog.TypeSlot &&
			entry.Id == SanctuaryCatalog.MerchantUpgradeRelicTwoId)
		{
			return IsAlreadyUnlocked(progress, SanctuaryCatalog.TypeSlot,
				SanctuaryCatalog.MerchantUpgradeRelicOneId);
		}

		if (entry.Type == SanctuaryCatalog.TypeSlot &&
			entry.Id.StartsWith("loadout-slot-", StringComparison.OrdinalIgnoreCase) &&
			int.TryParse(entry.Id["loadout-slot-".Length..], out int loadoutSlot))
		{
			return loadoutSlot <= 2 || IsAlreadyUnlocked(
				progress, SanctuaryCatalog.TypeSlot, $"loadout-slot-{loadoutSlot - 1}");
		}

		if (entry.Type != SanctuaryCatalog.TypeSlot ||
			!entry.Id.StartsWith("bag-slot-", StringComparison.OrdinalIgnoreCase) ||
			!int.TryParse(entry.Id["bag-slot-".Length..], out int slotNumber) ||
			slotNumber <= SanctuaryCatalog.BaseBagSlots + 1)
		{
			return true;
		}

		return IsAlreadyUnlocked(progress, SanctuaryCatalog.TypeSlot, $"bag-slot-{slotNumber - 1}");
	}

    /// <summary>Perche' una voce a catalogo non e' acquistabile, detto al giocatore.</summary>
    private static string UnavailableReason(SanctuaryCatalog.Entry entry) => entry.Type switch
    {
        SanctuaryCatalog.TypeClass => "Classe base: si sblocca completando il tutorial.",
        _ => "Non ancora acquistabile."
    };

    public (SinglePlayerProgressData Progress, string ErrorCode, string Error) PurchaseUnlock(
        AccountIdentity identity,
        SinglePlayerPurchaseUnlockRequest request)
    {
        string type = NormalizeUnlockType(request?.type);
        string id = NormalizeKey(request?.id);
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id))
            return (null, ErrorCodes.InvalidProgressionRequest, "Unlock non valido.");

        // I capitoli non si comprano: si aprono battendo il boss del capitolo precedente, e
        // completarli si guadagna giocando. Il Santuario li ha venduti per un periodo, quindi
        // il rifiuto deve dire come si ottengono davvero invece di lasciar credere a un
        // problema di prezzo o di catalogo.
        if (type == "chapter")
            return (null, ErrorCodes.InvalidProgressionRequest,
                "Un capitolo si apre battendo il boss del capitolo precedente.");
        if (type == "chapterCleared")
            return (null, ErrorCodes.InvalidProgressionRequest, "Un capitolo si completa giocandolo.");

        // Le voci del Santuario (classi, tecniche) prendono costo e prove dal catalogo; le
        // modalita' restano sul listino semplice.
        SanctuaryCatalog.Entry catalogEntry = null;
        int cost;
        if (SanctuaryCatalog.TryGetEntry(type, id, out catalogEntry))
        {
            if (!catalogEntry.Available)
                return (null, ErrorCodes.InvalidProgressionRequest, UnavailableReason(catalogEntry));
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
        if (type == "class" && !current.tutorialCompleted
            && !IsTutorialGiftClass(current, id))
        {
            return (null, ErrorCodes.InvalidProgressionRequest,
                "Durante il tutorial puoi sbloccare soltanto la classe indicata dal percorso.");
        }

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

    private static bool IsTutorialGiftClass(SinglePlayerProgressData progress, string classId)
    {
        if (progress?.completedTutorialModules == null || string.IsNullOrWhiteSpace(classId))
            return false;

        foreach (TutorialModuleCatalog.Module module in TutorialModuleCatalog.All)
        {
            if (!string.Equals(module.PaysForClassId, classId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (progress.completedTutorialModules.Any(completed =>
                    string.Equals(completed, module.Id, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Registra il completamento di un capitolo a partire dal boss finale sconfitto e concede
    /// (senza costo) la classe premio del capitolo e l'accesso a quelli successivi. Idempotente:
    /// ripetere la chiamata per un capitolo gia' completato non cambia nulla.
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

        if (!ChapterCatalog.TryGetByFinalBoss(bossId, out ChapterCatalog.Chapter chapter))
            return (null, ErrorCodes.InvalidProgressionRequest, "Boss non associato a un capitolo.");

        string chapterId = chapter.Id;

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        bool firstClear = GrantUnlock(
            connection, transaction, identity.PlayerId, "chapterCleared", chapterId);
        foreach (string unlockedChapterId in ChapterCatalog.UnlocksAfterClearing(chapterId))
            GrantUnlock(connection, transaction, identity.PlayerId, "chapter", unlockedChapterId);

        // Punti talento per la prima uccisione del boss del capitolo. Una tantum e non
        // farmabile: rigiocare il capitolo continua a dare esperienza, ma i punti li paga
        // solo la prima volta, ed e' quello che li rende una spinta ad andare avanti invece
        // che a ripetere il capitolo piu' comodo.
        if (firstClear)
            GrantTalentPoints(
                connection,
                transaction,
                identity.PlayerId,
                AccountLevelCurve.TalentPointsPerFirstChapterClear);

        // La classe premio del capitolo. Resta comprabile al Santuario col miele: chi la
        // guadagna qui non paga, chi non arriva in fondo al capitolo puo' ancora prenderla
        // dall'altare. Chi l'aveva gia' comprata non riceve niente e non perde niente.
        if (!string.IsNullOrEmpty(chapter.RewardClassId))
            GrantUnlock(connection, transaction, identity.PlayerId, "class", chapter.RewardClassId);

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

        GrantLegacyTutorialClasses(connection, transaction, identity.PlayerId);
        GrantUnlock(connection, transaction, identity.PlayerId, "chapter", TutorialRewardChapterId);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimId, 0), null, null);
    }

    /// <summary>
    /// Chiude un modulo del tutorial progressivo e ne consegna la ricompensa.
    /// Idempotente per modulo: la riga in <c>single_player_unlocks</c> e' insieme lo stato
    /// del percorso e la guardia contro la doppia riscossione, quindi non serve un secondo
    /// contatore che potrebbe sfasarsi da quello che il giocatore ha davvero finito.
    ///
    /// Cosa spetta a un modulo lo dice <see cref="TutorialModuleCatalog"/>: il client manda
    /// solo l'id. E i moduli si riscuotono in fila - un client modificato non puo' saltare
    /// all'ultimo per portarsi a casa capitolo e oggetto senza aver giocato niente.
    /// </summary>
    public (SinglePlayerRewardResult Result, string ErrorCode, string Error) ClaimTutorialModuleReward(
        AccountIdentity identity,
        SinglePlayerTutorialModuleRequest request)
    {
        string moduleId = Normalize(request?.moduleId);
        if (!TutorialModuleCatalog.TryGet(moduleId, out TutorialModuleCatalog.Module module))
            return (null, ErrorCodes.InvalidProgressionRequest, "Modulo del tutorial sconosciuto.");

        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        string[] completed = ReadUnlocks(
            connection, transaction, identity.PlayerId, TutorialModuleCatalog.UnlockType);

        if (Array.IndexOf(completed, module.Id) >= 0)
        {
            // Gia' riscosso: risposta idempotente, niente di nuovo concesso.
            SinglePlayerProgressData unchanged = ReadProgress(connection, identity.PlayerId, transaction);
            transaction.Commit();
            return (BuildReward(unchanged, null, 0), null, null);
        }

        foreach (string requiredId in TutorialModuleCatalog.RequiredBefore(module))
        {
            if (Array.IndexOf(completed, requiredId) < 0)
                return (null, ErrorCodes.InvalidProgressionRequest,
                    "I moduli del tutorial vanno completati in ordine.");
        }

        int honey = TutorialModuleCatalog.HoneyOf(module);
        string claimId = NewClaimId();
        RecordClaim(connection, transaction, claimId, identity.PlayerId, "tutorial-module",
            honey, 0, Normalize(request?.moduleRunId));

        if (honey > 0)
            GrantHoney(connection, transaction, identity.PlayerId, honey);

        foreach (string classId in module.ClassIds)
            GrantUnlock(connection, transaction, identity.PlayerId, "class", classId);
        foreach (string chapterId in module.ChapterIds)
            GrantUnlock(connection, transaction, identity.PlayerId, "chapter", chapterId);
        foreach (string itemId in module.ItemIds)
            SanctuaryBag.AddToStash(connection, transaction, identity.PlayerId, itemId, 1);

        GrantUnlock(connection, transaction, identity.PlayerId,
            TutorialModuleCatalog.UnlockType, module.Id);

        if (module.CompletesTutorial)
            MarkTutorialCompleted(connection, transaction, identity.PlayerId);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimId, honey), null, null);
    }

    private static void MarkTutorialCompleted(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_progress
            SET tutorial_completed = 1, updated_at = $now
            WHERE player_id = $player";
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$player", playerId);
        update.ExecuteNonQuery();
    }

    /// <summary>
    /// Apre una run di campagna nello storico. La riga nasce qui e viene chiusa dalla death
    /// reward: le run che restano senza fine sono quelle abbandonate, ed erano invisibili
    /// finche' l'unico momento in cui il server sentiva parlare di una run era la morte.
    /// Idempotente sul runId del client: un rinvio non duplica la riga.
    /// </summary>
    public SinglePlayerRunStartAck RecordRunStart(
        AccountIdentity identity,
        SinglePlayerRunStartRequest request)
    {
        string runId = Normalize(request?.runId);
        string startedAt = DateTime.UtcNow.ToString("O");

        using SqliteConnection connection = database.Open();
        using SqliteCommand insert = connection.CreateCommand();
        // La riga si crea solo se quel runId non e' gia' noto: un secondo avvio con lo
        // stesso id e' un rinvio, non una run nuova. Senza runId (client vecchio) la
        // guardia non puo' funzionare e la riga si inserisce comunque.
        insert.CommandText = @"
            INSERT INTO campaign_runs
                (player_id, client_run_ref, mode, chapter_id, stage_id, started_at)
            SELECT $player, $ref, $mode, $chapter, $stage, $now
            WHERE $ref IS NULL OR NOT EXISTS (
                SELECT 1 FROM campaign_runs
                WHERE player_id = $player AND client_run_ref = $ref)";
        insert.Parameters.AddWithValue("$player", identity.PlayerId);
        insert.Parameters.AddWithValue("$ref", NullIfEmpty(runId));
        insert.Parameters.AddWithValue("$mode", NullIfEmpty(request?.mode));
        insert.Parameters.AddWithValue("$chapter", NullIfEmpty(request?.chapterId));
        insert.Parameters.AddWithValue("$stage", NullIfEmpty(request?.stageId));
        insert.Parameters.AddWithValue("$now", startedAt);
        insert.ExecuteNonQuery();

        return new SinglePlayerRunStartAck { runId = runId, startedAt = startedAt };
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
        SanctuaryBag.AddRunLootToStash(connection, transaction, identity.PlayerId, request?.keptItemIds);
        int levelsGained = GrantAccountExperience(connection, transaction, identity.PlayerId, baseAccountExperience);

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (BuildReward(progress, claimId, baseHoney, baseAccountExperience, levelsGained), null, null);
    }

    /// <summary>
    /// Le ricompense gia concesse su cui il moltiplicatore pubblicitario e ancora disponibile.
    /// Esistono perche a fine run il x3 puo saltare per motivi che non dipendono dal giocatore
    /// (rete caduta, annuncio non arrivato in tempo): la riga della reward resta a
    /// moltiplicatore 1 e il profilo la ripropone finche la finestra non scade.
    ///
    /// La finestra vale solo qui, in vetrina: <see cref="ClaimAdMultiplier"/> non la
    /// ricontrolla, perche una reward guadagnata resta del giocatore e un video partito a
    /// cavallo della scadenza non deve finire nel vuoto.
    ///
    /// Solo le reward di campagna ('death'): le partite classificate creano anche loro un
    /// claim moltiplicabile, ma passano da un'altra ad unit e da un'altra schermata, e
    /// mostrarle qui vorrebbe dire pagarle sul placement sbagliato.
    /// </summary>
    public SinglePlayerPendingAdRewardsData GetPendingAdRewards(AccountIdentity identity)
    {
        DateTime now = DateTime.UtcNow;
        string cutoff = now.AddHours(-PendingAdRewardWindowHours).ToString("O");

        using SqliteConnection connection = database.Open();
        using SqliteCommand query = connection.CreateCommand();
        query.CommandText = @"
            SELECT c.claim_id, c.reward_type, c.base_account_experience, c.created_at,
                   c.base_honey, r.chapter_id, r.rooms_cleared
            FROM single_player_reward_claims c
            LEFT JOIN campaign_runs r
                ON r.player_id = c.player_id AND r.client_run_ref = c.source_ref
            WHERE c.player_id = $player
              AND c.reward_type IN ('death', 'tavern')
              AND c.multiplier = 1
              AND c.ad_impression_id IS NULL
              AND c.dismissed_at IS NULL
              AND (c.base_account_experience > 0 OR c.base_honey > 0)
              AND c.created_at >= $cutoff
            ORDER BY c.created_at DESC
            LIMIT $limit";
        query.Parameters.AddWithValue("$player", identity.PlayerId);
        query.Parameters.AddWithValue("$cutoff", cutoff);
        query.Parameters.AddWithValue("$limit", PendingAdRewardLimit);

        List<SinglePlayerPendingAdRewardData> rewards = new();
        using SqliteDataReader reader = query.ExecuteReader();
        while (reader.Read())
        {
            string createdAt = reader.GetString(3);
            int baseAccountExperience = reader.GetInt32(2);
            rewards.Add(new SinglePlayerPendingAdRewardData
            {
                claimId = reader.GetString(0),
                rewardType = reader.GetString(1),
                baseAccountExperience = baseAccountExperience,
                extraAccountExperience = baseAccountExperience * (AccountAdMultiplier - 1),
                baseHoney = reader.GetInt32(4),
                extraHoney = reader.GetInt32(4) *
                    ((reader.GetString(1) == "tavern" ? 5 : AdMultiplier) - 1),
                chapterId = reader.IsDBNull(5) ? null : reader.GetString(5),
                roomsCleared = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                createdAt = createdAt,
                hoursLeft = HoursLeft(createdAt, now)
            });
        }

        return new SinglePlayerPendingAdRewardsData { rewards = rewards.ToArray() };
    }

    /// <summary>
    /// Quanto manca alla scadenza dell'offerta, arrotondato all'ora in su: "0 ore" su
    /// un'offerta ancora valida sarebbe una scadenza annunciata male. Una data illeggibile
    /// vale come offerta appena nata: meglio un'ora in piu' che togliere il x3 per un
    /// formato sbagliato.
    /// </summary>
    private static int HoursLeft(string createdAt, DateTime now)
    {
        if (!DateTime.TryParse(
                createdAt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime created))
            return PendingAdRewardWindowHours;

        double left = PendingAdRewardWindowHours - (now - created.ToUniversalTime()).TotalHours;
        return Math.Max(1, (int)Math.Ceiling(left));
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
        string rewardType;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = @"
                SELECT base_honey, base_account_experience, multiplier, reward_type
                FROM single_player_reward_claims
                WHERE claim_id = $claim AND player_id = $player";
            query.Parameters.AddWithValue("$claim", claimRef);
            query.Parameters.AddWithValue("$player", identity.PlayerId);
            using SqliteDataReader reader = query.ExecuteReader();
            if (!reader.Read())
                return (null, ErrorCodes.RewardClaimNotFound, "Ricompensa non trovata.");
            baseHoney = reader.GetInt32(0);
            baseAccountExperience = reader.GetInt32(1);
            multiplier = reader.GetInt32(2);
            rewardType = reader.GetString(3);
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
        int appliedMultiplier = rewardType == "tavern" ? 5 : AdMultiplier;
        int extraHoney = baseHoney * (appliedMultiplier - 1);
        int extraAccountExperience = baseAccountExperience * (AccountAdMultiplier - 1);
        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE single_player_reward_claims
                SET multiplier = $mult, ad_impression_id = $ad, multiplied_at = $now
                WHERE claim_id = $claim AND player_id = $player";
            update.Parameters.AddWithValue("$mult", appliedMultiplier);
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

    public (string ErrorCode, string Error) DismissPendingAdReward(
        AccountIdentity identity, SinglePlayerDismissPendingAdRewardRequest request)
    {
        string claimRef = Normalize(request?.rewardClaimId);
        if (string.IsNullOrEmpty(claimRef))
            return (ErrorCodes.InvalidProgressionRequest, "Messaggio non valido.");

        using SqliteConnection connection = database.Open();
        using SqliteCommand update = connection.CreateCommand();
        update.CommandText = @"
            UPDATE single_player_reward_claims SET dismissed_at = $now
            WHERE claim_id = $claim AND player_id = $player
              AND multiplier = 1 AND ad_impression_id IS NULL";
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$claim", claimRef);
        update.Parameters.AddWithValue("$player", identity.PlayerId);
        if (update.ExecuteNonQuery() == 0)
            return (ErrorCodes.RewardClaimNotFound, "Messaggio non trovato.");
        return (null, null);
    }

    /// <summary>
    /// Riscuote in un'unica operazione atomica i level-up ancora da notificare. Il client
    /// puo' solo chiedere il claim: conteggio e accredito restano interamente sul server.
    ///
    /// Paga in punti talento, non piu' in miele: vedi <see cref="TalentPointsForLevels"/>.
    /// </summary>
    public (SinglePlayerRewardResult Result, string ErrorCode, string Error) ClaimLevelRewards(
        AccountIdentity identity)
    {
        using SqliteConnection connection = database.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        EnsureProgressRow(connection, transaction, identity.PlayerId);

        int pendingLevels;
        int currentLevel;
        using (SqliteCommand query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = @"SELECT pending_level_rewards, account_level
                FROM single_player_progress WHERE player_id = $player";
            query.Parameters.AddWithValue("$player", identity.PlayerId);
            using SqliteDataReader reader = query.ExecuteReader();
            reader.Read();
            pendingLevels = Math.Max(0, reader.GetInt32(0));
            currentLevel = Math.Max(1, reader.GetInt32(1));
        }

        // I livelli non riscossi sono per forza gli ultimi raggiunti: sapere quali sono e'
        // quello che serve per il bonus dei livelli tondi, e si ricava dal contatore senza
        // doverne tenere un secondo in tabella.
        int points = TalentPointsForLevels(currentLevel - pendingLevels, currentLevel);
        using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE single_player_progress
                SET talent_points = talent_points + $points,
                    talent_points_earned = talent_points_earned + $points,
                    pending_level_rewards = 0,
                    updated_at = $now
                WHERE player_id = $player";
            update.Parameters.AddWithValue("$points", points);
            update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
            update.Parameters.AddWithValue("$player", identity.PlayerId);
            update.ExecuteNonQuery();
        }

        SinglePlayerProgressData progress = ReadProgress(connection, identity.PlayerId, transaction);
        transaction.Commit();
        return (
            BuildReward(progress, null, 0, levelsGained: pendingLevels, grantedTalentPoints: points),
            null,
            null);
    }

    /// <summary>
    /// Accredita punti talento fuori dal giro dei livelli. Alza anche il totale guadagnato,
    /// che e' lo storico su cui il profilo e il pannello admin ragionano.
    /// </summary>
    private static void GrantTalentPoints(
        SqliteConnection connection, SqliteTransaction transaction, string playerId, int points)
    {
        if (points <= 0)
            return;

        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_progress
            SET talent_points = talent_points + $points,
                talent_points_earned = talent_points_earned + $points,
                updated_at = $now
            WHERE player_id = $player";
        update.Parameters.AddWithValue("$points", points);
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$player", playerId);
        update.ExecuteNonQuery();
    }

    /// <summary>
    /// Punti talento consegnati salendo da <paramref name="fromLevel"/> (escluso) a
    /// <paramref name="toLevel"/> (incluso). La regola sta in
    /// <see cref="AccountLevelCurve.TalentPointsForLevels"/>, accanto alla curva di
    /// esperienza: il popup di level-up deve annunciare la stessa cifra che qui viene
    /// accreditata, e per farlo il client compila quello stesso sorgente.
    /// </summary>
    internal static int TalentPointsForLevels(int fromLevel, int toLevel) =>
        AccountLevelCurve.TalentPointsForLevels(fromLevel, toLevel);

    /// <summary>
    /// L'esperienza account di una run: un decimo di quella guadagnata giocando, per il
    /// moltiplicatore del capitolo.
    ///
    /// Il tetto vale sull'esperienza di run, non sul risultato: il moltiplicatore deve
    /// poterlo superare, altrimenti oltre le 5000 di run tutti i capitoli tornerebbero a
    /// pagare uguale ed esisterebbe solo sulla carta.
    ///
    /// La catena completa di moltiplicatori e': capitolo (qui), poi il video su una reward
    /// gia' concessa (<see cref="ClaimAdMultiplier"/>). Il pass stagionale, quando arrivera',
    /// e' un terzo fattore e va messo qui accanto al capitolo, non sul video: deve valere
    /// anche per chi non guarda annunci, altrimenti premia due volte la stessa cosa.
    /// </summary>
    private static int CalculateAccountExperience(SinglePlayerDeathRewardRequest request)
    {
        if (request == null)
            return 0;
        int farmedExperience = Math.Clamp(request.matchExperience, 0, DeathRewardExperienceCeiling);
        int percent = ChapterCatalog.AccountExperiencePercentOf(request.chapterId);
        return farmedExperience / 10 * percent / 100;
    }

    private static SinglePlayerRewardResult BuildReward(
        SinglePlayerProgressData progress,
        string claimId,
        int grantedHoney,
        int grantedAccountExperience = 0,
        int levelsGained = 0,
        int grantedTalentPoints = 0) => new()
    {
        progress = progress,
        rewardClaimId = claimId,
        grantedHoney = grantedHoney,
        grantedAccountExperience = grantedAccountExperience,
        levelsGained = levelsGained,
        grantedTalentPoints = grantedTalentPoints
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
    /// Chiude la run di campagna nello storico admin. Se l'avvio era stato registrato
    /// (<see cref="RecordRunStart"/>) aggiorna quella riga, cosi' inizio e fine restano la
    /// stessa run e la durata e' leggibile; altrimenti - client vecchio, oppure avvio perso
    /// perche' offline - la riga nasce qui come prima, senza started_at.
    /// I valori arrivano dal sommario client e vengono normalizzati prima del salvataggio.
    /// </summary>
    private static void RecordCampaignRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        SinglePlayerDeathRewardRequest request,
        int honeyReward)
    {
        if (CloseOpenCampaignRun(connection, transaction, playerId, request, honeyReward))
            return;

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

    /// <summary>
    /// Chiude la riga aperta all'avvio della run. Vero se ne ha trovata una: la
    /// corrispondenza e' il runId del client, e la condizione su <c>ended_at</c> evita che
    /// una reward rigiocata riscriva una run gia' chiusa.
    /// </summary>
    private static bool CloseOpenCampaignRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId,
        SinglePlayerDeathRewardRequest request,
        int honeyReward)
    {
        string runId = Normalize(request?.runId);
        if (string.IsNullOrEmpty(runId))
            return false;

        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE campaign_runs
            SET mode = COALESCE($mode, mode),
                chapter_id = COALESCE($chapter, chapter_id),
                stage_id = COALESCE($stage, stage_id),
                rooms_cleared = $rooms,
                enemies_defeated = $enemies,
                bosses_defeated = $bosses,
                minibosses_defeated = $minibosses,
                defeated_boss_ids = $bossIds,
                honey_reward = $honey,
                ended_at = $now
            WHERE player_id = $player AND client_run_ref = $ref AND ended_at IS NULL";
        update.Parameters.AddWithValue("$player", playerId);
        update.Parameters.AddWithValue("$ref", runId);
        update.Parameters.AddWithValue("$mode", NullIfEmpty(request?.mode));
        update.Parameters.AddWithValue("$chapter", NullIfEmpty(request?.chapterId));
        update.Parameters.AddWithValue("$stage", NullIfEmpty(request?.stageId));
        update.Parameters.AddWithValue("$rooms", Math.Max(0, request?.roomsCleared ?? 0));
        update.Parameters.AddWithValue("$enemies", Math.Max(0, request?.enemiesDefeated ?? 0));
        update.Parameters.AddWithValue("$bosses", Math.Max(0, request?.bossesDefeated ?? 0));
        update.Parameters.AddWithValue("$minibosses", Math.Max(0, request?.minibossesDefeated ?? 0));
        update.Parameters.AddWithValue("$bossIds", JoinBossIds(request?.defeatedBossIds));
        update.Parameters.AddWithValue("$honey", honeyReward);
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        return update.ExecuteNonQuery() > 0;
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

        AccountLevelProgress progress = AccountLevelCurve.Apply(level, current, total, amount);

        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = @"
            UPDATE single_player_progress
            SET account_level = $level,
                account_experience = $xp,
                account_total_experience = $total,
                account_experience_to_next_level = $next,
                pending_level_rewards = pending_level_rewards + $levels,
                updated_at = $now
            WHERE player_id = $player";
        update.Parameters.AddWithValue("$level", progress.Level);
        update.Parameters.AddWithValue("$xp", progress.Experience);
        update.Parameters.AddWithValue("$total", progress.TotalExperience);
        update.Parameters.AddWithValue("$next", progress.ExperienceToNextLevel);
        update.Parameters.AddWithValue("$levels", progress.LevelsGained);
        update.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$player", playerId);
        update.ExecuteNonQuery();
        return progress.LevelsGained;
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
            _ => Array.Empty<string>()
        };
        return Array.IndexOf(list ?? Array.Empty<string>(), id) >= 0;
    }

    /// <summary>
    /// Concede uno sblocco. Restituisce <c>true</c> solo se la riga non c'era: e' la
    /// "prima volta", ed e' quello che serve a pagare i premi una tantum senza tenere un
    /// secondo contatore che potrebbe sfasarsi dagli sblocchi veri.
    /// </summary>
    private static bool GrantUnlock(
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
        return insert.ExecuteNonQuery() > 0;
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
        insert.Parameters.AddWithValue("$next", AccountLevelCurve.ExperienceToNext(1));
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

        GrantLegacyTutorialClasses(connection, transaction, playerId);
        // Anche per chi aveva finito il tutorial quando ancora pagava miele: il primo
        // capitolo era comprabile con quei 60 vasetti, ora e' parte della dotazione.
        GrantUnlock(connection, transaction, playerId, "chapter", TutorialRewardChapterId);
        BackfillTutorialModules(connection, transaction, playerId);
    }

    /// <summary>
    /// Chi aveva finito il vecchio tutorial monolitico si vede segnare tutti i moduli del
    /// percorso nuovo: per lui l'onboarding e' finito, e i cancelli devono trovarlo aperto.
    /// Nessuna ricompensa retroattiva - i moduli risultano riscossi, non da riscuotere -
    /// e nessuna migrazione da lanciare a mano: succede al primo contatto dell'account.
    /// </summary>
    private static void BackfillTutorialModules(
        SqliteConnection connection, SqliteTransaction transaction, string playerId)
    {
        foreach (string moduleId in TutorialModuleCatalog.AllIds)
            GrantUnlock(connection, transaction, playerId, TutorialModuleCatalog.UnlockType, moduleId);
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
                       account_experience_to_next_level, pending_level_rewards,
                       tutorial_completed, hardcore_unlocked,
                       talent_points, talent_points_earned
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
                data.pendingLevelRewards = Math.Max(0, reader.GetInt32(5));
                data.tutorialCompleted = reader.GetInt32(6) != 0;
                data.hardcoreUnlocked = reader.GetInt32(7) != 0;
                data.talentPoints = Math.Max(0, reader.GetInt32(8));
                data.talentPointsEarned = Math.Max(0, reader.GetInt32(9));

                // La soglia si ricalcola dal livello invece di fidarsi della colonna: le
                // righe scritte prima della curva hanno tutte 100 in tabella, e senza questo
                // la barra resterebbe sbagliata fino al primo level-up successivo.
                data.accountExperienceToNextLevel =
                    AccountLevelCurve.ExperienceToNext(data.accountLevel);
            }
        }

        data.unlockedChapters = ReadUnlocks(connection, transaction, playerId, "chapter");
        data.unlockedStages = ReadUnlocks(connection, transaction, playerId, "stage");
        data.unlockedClasses = ReadUnlocks(connection, transaction, playerId, "class");
        if (data.tutorialCompleted)
            data.unlockedClasses = MergeLegacyTutorialClasses(data.unlockedClasses);
        data.unlockedScenarios = ReadUnlocks(connection, transaction, playerId, "scenario");
        data.unlockedSecondAbilities = ReadUnlocks(connection, transaction, playerId, "secondAbility");
        data.clearedChapters = ReadUnlocks(connection, transaction, playerId, "chapterCleared");
        data.unlockedSlots = ReadUnlocks(connection, transaction, playerId, "slot");
        data.completedTutorialModules =
            ReadUnlocks(connection, transaction, playerId, TutorialModuleCatalog.UnlockType);
        data.bagItems = SanctuaryBag.ReadBag(connection, transaction, playerId);
        data.counters = CampaignCounters.Read(connection, transaction, playerId);
        data.talentLoadout = TalentService.BuildLoadoutFor(connection, transaction, playerId);
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

    /// <summary>
    /// Rimette le tre classi del vecchio tutorial. Si chiama solo per chi quel tutorial lo
    /// aveva finito: il percorso nuovo consegna il Guerriero dal catalogo dei moduli e le
    /// altre due le fa comprare.
    /// </summary>
    private static void GrantLegacyTutorialClasses(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string playerId)
    {
        foreach (string classId in LegacyTutorialClassIds)
            GrantUnlock(connection, transaction, playerId, "class", classId);
    }

    /// <summary>
    /// Chi ha il flag del vecchio tutorial vede sempre le sue tre classi, anche se le righe
    /// di unlock non ci sono: e' la garanzia che nessuno perda la dotazione con cui giocava.
    /// </summary>
    private static string[] MergeLegacyTutorialClasses(string[] unlockedClasses)
    {
        var result = new List<string>(unlockedClasses ?? Array.Empty<string>());
        foreach (string classId in LegacyTutorialClassIds)
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
