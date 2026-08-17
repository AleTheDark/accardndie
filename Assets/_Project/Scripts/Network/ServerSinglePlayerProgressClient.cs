using System;
using System.Threading.Tasks;
using AccardND.GameData;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    public interface IServerSinglePlayerProgressClient
    {
        Task<SinglePlayerProgressSave> LoadProgressAsync();
        Task<SinglePlayerProgressSave> PurchaseUnlockAsync(SinglePlayerUnlockType type, string id);
        Task<SinglePlayerProgressSave> PurchaseHardcoreAsync();
        Task<SinglePlayerProgressSave> ClearChapterAsync(string bossId);
        Task<SanctuaryData> GetSanctuaryAsync();
        Task<SanctuaryData> BuySanctuaryItemAsync(string itemId, string offerId = null);
        Task<SanctuaryData> SetSanctuaryBagAsync(string[] itemIds);
        Task<IapEntitlementsData> GetEntitlementsAsync();
        Task<IapRedeemResult> RedeemPurchaseAsync(string productId, string receipt);
        Task<TavernData> GetTavernAsync();
        Task<TavernData> ClaimTavernQuestAsync(string questId, int rewardMultiplier = 1);
        Task<TavernData> ClaimTavernBonusAsync();
        Task<TalentData> GetTalentsAsync();
        Task<TalentData> BuyTalentAsync(string talentId);
        Task<SinglePlayerRewardOutcome> ClaimTutorialRewardAsync(string tutorialRunId);
        Task<SinglePlayerRewardOutcome> ClaimTutorialModuleRewardAsync(string moduleId, string moduleRunId);
        Task NotifyRunStartedAsync(string runId, string mode, string chapterId, string stageId);
        Task<SinglePlayerRewardOutcome> ClaimDeathRewardAsync(DeathRewardSummary summary);
        Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId);
        Task<SinglePlayerRewardOutcome> ClaimLevelRewardsAsync();
        Task<SinglePlayerPendingAdRewardsData> GetPendingAdRewardsAsync();
        Task DismissPendingAdRewardAsync(string rewardClaimId);
    }

    /// <summary>Esito autoritativo di una reward: nuovo stato, id reward (per l'ad) e quanto e' stato accreditato.</summary>
    public readonly struct SinglePlayerRewardOutcome
    {
        public SinglePlayerRewardOutcome(
            SinglePlayerProgressSave progress,
            string rewardClaimId,
            int grantedHoney,
            int grantedAccountExperience = 0,
            int levelsGained = 0,
            int grantedTalentPoints = 0)
        {
            Progress = progress;
            RewardClaimId = rewardClaimId;
            GrantedHoney = grantedHoney;
            GrantedAccountExperience = grantedAccountExperience;
            LevelsGained = levelsGained;
            GrantedTalentPoints = grantedTalentPoints;
        }

        public SinglePlayerProgressSave Progress { get; }
        public string RewardClaimId { get; }
        public int GrantedHoney { get; }
        public int GrantedAccountExperience { get; }
        public int LevelsGained { get; }

        /// <summary>Punti talento pagati dai livelli riscossi.</summary>
        public int GrantedTalentPoints { get; }
    }

    /// <summary>Sommario di una run terminata, usato dal server per calcolare (con cap) la reward alla morte.</summary>
    public readonly struct DeathRewardSummary
    {
        public DeathRewardSummary(
            string runId, string mode, string chapterId, string stageId,
            int roomsCleared, int enemiesDefeated, int bossesDefeated, int matchExperience = 0,
            int minibossesDefeated = 0, string[] defeatedBossIds = null, string[] consumedItemIds = null,
            int diceRolled = 0, int abilitiesUsed = 0, int experienceEarned = 0,
            int supremesUsed = 0, int quickChallengesCompleted = 0, int merchantPurchases = 0,
            int goldEarned = 0, int levelsGained = 0, int itemsUsed = 0, string[] keptItemIds = null)
        {
            RunId = runId;
            Mode = mode;
            ChapterId = chapterId;
            StageId = stageId;
            RoomsCleared = roomsCleared;
            EnemiesDefeated = enemiesDefeated;
            BossesDefeated = bossesDefeated;
            MatchExperience = matchExperience;
            MinibossesDefeated = minibossesDefeated;
            DefeatedBossIds = defeatedBossIds ?? Array.Empty<string>();
            ConsumedItemIds = consumedItemIds ?? Array.Empty<string>();
            DiceRolled = diceRolled;
            AbilitiesUsed = abilitiesUsed;
            ExperienceEarned = experienceEarned;
            SupremesUsed = supremesUsed;
            QuickChallengesCompleted = quickChallengesCompleted;
            MerchantPurchases = merchantPurchases;
            GoldEarned = goldEarned;
            LevelsGained = levelsGained;
            ItemsUsed = itemsUsed;
            KeptItemIds = keptItemIds ?? Array.Empty<string>();
        }

        public string RunId { get; }
        public string Mode { get; }
        public string ChapterId { get; }
        public string StageId { get; }
        public int RoomsCleared { get; }
        public int EnemiesDefeated { get; }
        public int BossesDefeated { get; }
        public int MatchExperience { get; }
        public int MinibossesDefeated { get; }
        public string[] DefeatedBossIds { get; }
        public string[] ConsumedItemIds { get; }

        /// <summary>Dadi tirati nella run: alimenta le quest della taverna sui dadi.</summary>
        public int DiceRolled { get; }

        /// <summary>Abilita' di classe attivate dalle pedine del giocatore.</summary>
        public int AbilitiesUsed { get; }

        /// <summary>Esperienza guadagnata, al lordo di quella spesa dal mercante.</summary>
        public int ExperienceEarned { get; }

        /// <summary>Supreme attivate dalle pedine del giocatore.</summary>
        public int SupremesUsed { get; }

        /// <summary>Sfide veloci portate a termine: la rinuncia non conta.</summary>
        public int QuickChallengesCompleted { get; }

        /// <summary>Acquisti conclusi al mercante, carte e oggetti insieme.</summary>
        public int MerchantPurchases { get; }

        /// <summary>Oro guadagnato nelle stanze e nelle prove lampo.</summary>
        public int GoldEarned { get; }

        /// <summary>Livelli guadagnati nella run.</summary>
        public int LevelsGained { get; }

        /// <summary>
        /// Consumabili usati nella run, di qualunque provenienza: e' il numero che alimenta le
        /// quest della taverna. <see cref="ConsumedItemIds"/> resta il sottoinsieme che arriva
        /// dalla bisaccia, l'unico che scala la scorta.
        /// </summary>
        public int ItemsUsed { get; }

        /// <summary>
        /// Oggetti trovati o comprati in run e mai usati: il server li versa nella scorta, cosi'
        /// una run finita male non li porta via.
        /// </summary>
        public string[] KeptItemIds { get; }
    }

    /// <summary>
    /// Client remoto per la progressione single player server-authoritative.
    /// Richiede un PvpServerClient gia connesso e autenticato.
    /// </summary>
    public sealed class ServerSinglePlayerProgressClient : IServerSinglePlayerProgressClient
    {
        private const float DefaultTimeoutSeconds = 8f;

        private readonly PvpServerMessageDispatcher dispatcher;
        private readonly PersistentMutationOutbox outbox;
        private readonly float timeoutSeconds;

        /// <summary>Il replay in corso, o l'ultimo concluso.</summary>
        private Task replay;

        /// <summary>Il primo replay, quello dell'avvio: chi apre una schermata lo aspetta.</summary>
        public Task PendingMutationsReplayed => replay;

        public ServerSinglePlayerProgressClient(PvpServerClient client, float timeoutSeconds = DefaultTimeoutSeconds)
            : this(new PvpServerMessageDispatcher(client), timeoutSeconds)
        {
        }

        public ServerSinglePlayerProgressClient(
            PvpServerMessageDispatcher dispatcher,
            float timeoutSeconds = DefaultTimeoutSeconds,
            PersistentMutationOutbox outbox = null)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.outbox = outbox ?? new PersistentMutationOutbox();
            this.timeoutSeconds = Mathf.Max(0.5f, timeoutSeconds);
            replay = ReplayPersistentMutationsAsync();
        }

        public async Task<SinglePlayerProgressSave> LoadProgressAsync()
        {
            SinglePlayerProgressData data = await RequestProgressAsync(
                MessageTypes.SinglePlayerProgressGet,
                null);
            return ToSave(data);
        }

        public Task<SinglePlayerProgressSave> PurchaseUnlockAsync(SinglePlayerUnlockType type, string id) =>
            PurchaseRawAsync(ToServerUnlockType(type), id);

        // La modalita Hardcore e un flag server (type "mode"), non presente nell'enum degli unlock a lista.
        public Task<SinglePlayerProgressSave> PurchaseHardcoreAsync() => PurchaseRawAsync("mode", "hardcore");

        /// <summary>
        /// Notifica al server la sconfitta del boss finale di un capitolo. Il server ricava il
        /// capitolo dal boss, lo segna completato e concede il capitolo successivo.
        /// </summary>
        public async Task<SinglePlayerProgressSave> ClearChapterAsync(string bossId)
        {
            SinglePlayerProgressData data = await RequestProgressAsync(
                MessageTypes.SinglePlayerClearChapter,
                new SinglePlayerClearChapterRequest { bossId = bossId },
                persistent: true);
            return ToSave(data);
        }

        private async Task<SinglePlayerProgressSave> PurchaseRawAsync(string serverType, string id)
        {
            SinglePlayerProgressData data = await RequestProgressAsync(
                MessageTypes.SinglePlayerPurchaseUnlock,
                new SinglePlayerPurchaseUnlockRequest { type = serverType, id = id },
                persistent: true);
            return ToSave(data);
        }

        public async Task<SinglePlayerRewardOutcome> ClaimTutorialRewardAsync(string tutorialRunId)
        {
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimTutorialReward,
                new SinglePlayerTutorialRewardRequest { tutorialRunId = tutorialRunId },
                persistent: true);
            return ToOutcome(result);
        }

        /// <summary>
        /// Chiude un modulo del tutorial progressivo. Persistente come le altre reward: un
        /// modulo finito offline deve arrivare al server appena torna la rete, altrimenti il
        /// percorso resta fermo su una lezione che il giocatore ha gia' fatto.
        /// </summary>
        public async Task<SinglePlayerRewardOutcome> ClaimTutorialModuleRewardAsync(
            string moduleId, string moduleRunId)
        {
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimTutorialModule,
                new SinglePlayerTutorialModuleRequest { moduleId = moduleId, moduleRunId = moduleRunId },
                persistent: true);
            return ToOutcome(result);
        }

        /// <summary>
        /// Annuncia l'inizio di una run: il server apre la riga dello storico che la death
        /// reward chiudera'. Non e' persistente di proposito - un inizio rispedito ore dopo
        /// racconterebbe una run cominciata quando invece era gia' finita - e non fa parte
        /// della progressione: se il server non risponde, la run si gioca lo stesso.
        /// </summary>
        public async Task NotifyRunStartedAsync(string runId, string mode, string chapterId, string stageId)
        {
            Envelope envelope = await RequestAsync(
                MessageTypes.SinglePlayerRunStarted,
                new SinglePlayerRunStartRequest
                {
                    runId = runId,
                    mode = mode,
                    chapterId = chapterId,
                    stageId = stageId
                },
                MessageTypes.SinglePlayerRunStartedAck,
                persistent: false);

            ThrowIfError(envelope, "Avvio run non registrato.");
        }

        public async Task<SinglePlayerRewardOutcome> ClaimDeathRewardAsync(DeathRewardSummary summary)
        {
            SinglePlayerDeathRewardRequest request = CreateDeathRewardRequest(summary);
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimDeathReward,
                request,
                persistent: true);
            return ToOutcome(result);
        }

        public async Task<SinglePlayerRewardOutcome> ClaimLevelRewardsAsync()
        {
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimLevelRewards, null, persistent: false);
            return ToOutcome(result);
        }

        /// <summary>
        /// Salva il riepilogo anche quando, a fine run, non e' possibile costruire un client
        /// connesso. Alla prossima riconnessione il normale replay applichera' reward, stats
        /// e progressi quest usando lo stesso payload completo della richiesta online.
        /// </summary>
        public static bool QueueDeathRewardForReplay(
            DeathRewardSummary summary,
            string playerId = null,
            PersistentMutationOutbox persistentOutbox = null)
        {
            string owner = playerId ?? AccountServerSession.PlayerId;
            if (string.IsNullOrWhiteSpace(owner))
                return false;

            SinglePlayerDeathRewardRequest request = CreateDeathRewardRequest(summary);
            (persistentOutbox ?? new PersistentMutationOutbox()).Add(
                owner,
                MessageTypes.SinglePlayerClaimDeathReward,
                MessageTypes.SinglePlayerRewardResult,
                JsonUtility.ToJson(request));
            return true;
        }

        /// <summary>
        /// Salva il completamento di un capitolo quando il client remoto non puo' essere
        /// creato. Il replay usera' lo stesso endpoint persistente del percorso online.
        /// </summary>
        public static bool QueueChapterClearForReplay(
            string bossId,
            string playerId = null,
            PersistentMutationOutbox persistentOutbox = null)
        {
            string owner = playerId ?? AccountServerSession.PlayerId;
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(bossId))
                return false;

            var request = new SinglePlayerClearChapterRequest { bossId = bossId };
            (persistentOutbox ?? new PersistentMutationOutbox()).Add(
                owner,
                MessageTypes.SinglePlayerClearChapter,
                MessageTypes.SinglePlayerProgressData,
                JsonUtility.ToJson(request));
            return true;
        }

        private static SinglePlayerDeathRewardRequest CreateDeathRewardRequest(DeathRewardSummary summary) => new()
        {
            runId = summary.RunId,
            mode = summary.Mode,
            chapterId = summary.ChapterId,
            stageId = summary.StageId,
            roomsCleared = summary.RoomsCleared,
            enemiesDefeated = summary.EnemiesDefeated,
            bossesDefeated = summary.BossesDefeated,
            matchExperience = summary.MatchExperience,
            minibossesDefeated = summary.MinibossesDefeated,
            defeatedBossIds = summary.DefeatedBossIds,
            consumedItemIds = summary.ConsumedItemIds,
            keptItemIds = summary.KeptItemIds,
            diceRolled = summary.DiceRolled,
            abilitiesUsed = summary.AbilitiesUsed,
            // Non e' la lunghezza di consumedItemIds: quella conta solo la bisaccia, mentre la
            // quest guarda ogni oggetto usato, compresi bottino e acquisti al mercante.
            itemsUsed = summary.ItemsUsed,
            experienceEarned = summary.ExperienceEarned,
            supremesUsed = summary.SupremesUsed,
            quickChallengesCompleted = summary.QuickChallengesCompleted,
            merchantPurchases = summary.MerchantPurchases,
            goldEarned = summary.GoldEarned,
            levelsGained = summary.LevelsGained
        };

        public async Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId)
        {
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimAdMultiplier,
                new SinglePlayerAdMultiplierRequest
                {
                    rewardClaimId = rewardClaimId,
                    adImpressionId = adImpressionId
                },
                persistent: true);
            return ToOutcome(result);
        }

        /// <summary>
        /// Le reward gia' concesse che aspettano ancora il video del x3. Le decide il server:
        /// il client le riceve e le ripropone nel profilo, cosi' una disconnessione a fine
        /// run non brucia il moltiplicatore.
        /// </summary>
        public async Task<SinglePlayerPendingAdRewardsData> GetPendingAdRewardsAsync()
        {
            Envelope envelope = await RequestAsync(
                MessageTypes.SinglePlayerPendingAdRewardsGet,
                null,
                MessageTypes.SinglePlayerPendingAdRewardsData,
                persistent: false);

            ThrowIfError(envelope, "Richiesta ricompense in sospeso rifiutata.");
            return PvpServerClient.ParsePayload<SinglePlayerPendingAdRewardsData>(envelope)
                ?? new SinglePlayerPendingAdRewardsData();
        }

        public async Task DismissPendingAdRewardAsync(string rewardClaimId)
        {
            Envelope envelope = await RequestAsync(
                MessageTypes.SinglePlayerPendingAdRewardDismiss,
                new SinglePlayerDismissPendingAdRewardRequest { rewardClaimId = rewardClaimId },
                MessageTypes.SinglePlayerPendingAdRewardDismissed,
                persistent: true);
            ThrowIfError(envelope, "Eliminazione messaggio rifiutata.");
        }

        /// <summary>
        /// Catalogo del Santuario con le prove gia' valutate dal server. Il client non
        /// conosce ne i costi ne le regole: li riceve e li disegna.
        /// </summary>
        public Task<SanctuaryData> GetSanctuaryAsync() =>
            RequestSanctuaryAsync(MessageTypes.SanctuaryGet, null, "Richiesta Santuario rifiutata.");

        /// <summary>Compra un consumabile: si somma alla scorta permanente.</summary>
        public Task<SanctuaryData> BuySanctuaryItemAsync(string itemId, string offerId = null) =>
            RequestSanctuaryAsync(
                MessageTypes.SanctuaryBuyItem,
                new SanctuaryBuyItemRequest { itemId = itemId, offerId = offerId },
                "Acquisto oggetto rifiutato.",
                // Un consumabile pagato e non consegnato è la peggiore delle perdite:
                // il miele è già uscito. La bisaccia (SetBag) resta fuori di proposito,
                // è una preferenza che si rifà con un tocco.
                persistent: true);

        /// <summary>Cosa possiede l'account in fatto di acquisti a valuta reale.</summary>
        public async Task<IapEntitlementsData> GetEntitlementsAsync()
        {
            Envelope envelope = await RequestAsync(
                MessageTypes.IapGet, null, MessageTypes.IapData, persistent: false);
            ThrowIfError(envelope, "Richiesta acquisti rifiutata.");
            return PvpServerClient.ParsePayload<IapEntitlementsData>(envelope) ?? new IapEntitlementsData();
        }

        /// <summary>
        /// Manda una ricevuta dello store al server, che la verifica e concede. Va rinviata
        /// finche' non passa: il giocatore ha gia' pagato, quindi vale la persistenza come
        /// per un acquisto di consumabili.
        /// </summary>
        public async Task<IapRedeemResult> RedeemPurchaseAsync(string productId, string receipt)
        {
            Envelope envelope = await RequestAsync(
                MessageTypes.IapRedeem,
                new IapRedeemRequest { productId = productId, receipt = receipt },
                MessageTypes.IapRedeemResult,
                persistent: true);
            ThrowIfError(envelope, "Riscatto acquisto rifiutato.");
            return PvpServerClient.ParsePayload<IapRedeemResult>(envelope) ?? new IapRedeemResult();
        }

        /// <summary>Sostituisce la bisaccia scelta per la prossima run.</summary>
        public Task<SanctuaryData> SetSanctuaryBagAsync(string[] itemIds) =>
            RequestSanctuaryAsync(
                MessageTypes.SanctuarySetBag,
                new SanctuarySetBagRequest { itemIds = itemIds ?? Array.Empty<string>() },
                "Bisaccia rifiutata.");

        /// <summary>
        /// Bacheca della taverna: le quest di oggi gia' valutate dal server. Il client non
        /// conosce ne' il catalogo ne' le soglie, li riceve e li disegna.
        /// </summary>
        public Task<TavernData> GetTavernAsync() =>
            RequestTavernAsync(MessageTypes.TavernGet, null, "Richiesta taverna rifiutata.");

        /// <summary>Riscuote la ricompensa di una quest completata.</summary>
        public Task<TavernData> ClaimTavernQuestAsync(string questId, int rewardMultiplier = 1) =>
            RequestTavernAsync(
                MessageTypes.TavernClaimQuest,
                new TavernClaimQuestRequest { questId = questId, rewardMultiplier = rewardMultiplier },
                "Riscossione rifiutata.",
                persistent: true);

        /// <summary>Riscuote il premio per aver completato tutte le quest del giorno.</summary>
        public Task<TavernData> ClaimTavernBonusAsync() =>
            RequestTavernAsync(
                MessageTypes.TavernClaimBonus,
                null,
                "Premio di giornata rifiutato.",
                persistent: true);

        /// <summary>
        /// L'albero dei talenti gia' valutato: ranghi, prezzi e motivi dei blocchi. Come per
        /// il Santuario il catalogo vive sul server e il client si limita a disegnarlo.
        /// </summary>
        public Task<TalentData> GetTalentsAsync() =>
            RequestTalentsAsync(MessageTypes.TalentsGet, null, "Richiesta talenti rifiutata.");

        /// <summary>
        /// Compra un rango di talento. Persistente: e' una spesa, e una risposta persa non
        /// deve poter far sparire i punti senza dare il rango.
        /// </summary>
        public Task<TalentData> BuyTalentAsync(string talentId) =>
            RequestTalentsAsync(
                MessageTypes.TalentsBuy,
                new TalentBuyRequest { talentId = talentId },
                "Acquisto talento rifiutato.",
                persistent: true);

        /// <summary>
        /// Spedisce una richiesta. Quando <paramref name="persistent"/> è true la
        /// mutazione viene prima scritta su disco: se la risposta non arriva (rete
        /// caduta, gioco chiuso) riparte al prossimo avvio con lo stesso requestId.
        /// </summary>
        private async Task<Envelope> RequestAsync(
            string messageType, object payload, string expectedType, bool persistent)
        {
            if (!persistent)
                return await dispatcher.RequestAsync(messageType, payload, expectedType, timeoutSeconds);

            PersistentMutationOutbox.Entry mutation = outbox.Add(
                AccountServerSession.PlayerId,
                messageType,
                expectedType,
                payload == null ? string.Empty : JsonUtility.ToJson(payload));

            Envelope response = await dispatcher.RequestAsync(
                messageType, payload, expectedType, timeoutSeconds, mutation.requestId);
            // Anche un errore applicativo è una risposta definitiva; solo cadute e
            // timeout lasciano la mutazione su disco per il prossimo avvio.
            outbox.Remove(mutation.requestId);
            return response;
        }

        /// <summary>
        /// Rimanda al server le mutazioni rimaste su disco. Va chiamata a ogni ritorno
        /// della rete, non solo all'avvio: una reward che ha sfondato la grazia offline
        /// resta lì, e prima aspettava il riavvio successivo mentre al giocatore era
        /// stato promesso "verra' sincronizzata alla riconnessione".
        ///
        /// Un replay già in corso non viene raddoppiato: si restituisce quello.
        /// </summary>
        public Task ReplayPendingMutationsAsync()
        {
            if (replay is { IsCompleted: false })
                return replay;
            replay = ReplayPersistentMutationsAsync();
            return replay;
        }

        private async Task ReplayPersistentMutationsAsync()
        {
            await PvpAsync.NextFrameAsync();
            string playerId = AccountServerSession.PlayerId;
            foreach (PersistentMutationOutbox.Entry mutation in outbox.PendingFor(playerId))
            {
                object payload = DeserializePersistentPayload(mutation);
                if (!CanRebuildPayload(mutation, payload))
                {
                    Debug.LogError(
                        $"[Progress] Mutazione '{mutation.messageType}' non rigiocabile: manca il ramo che ne rilegge il corpo in DeserializePersistentPayload. La lascio in coda.");
                    continue;
                }

                try
                {
                    await dispatcher.RequestAsync(
                        mutation.messageType,
                        payload,
                        mutation.expectedType,
                        timeoutSeconds,
                        mutation.requestId);
                    outbox.Remove(mutation.requestId);
                }
                catch (Exception exception)
                {
                    Debug.Log($"[Progress] Mutazione persistita ancora in attesa: {exception.Message}");
                }
            }
        }

        /// <summary>
        /// Ricostruisce il payload di una mutazione ripescata dal disco. Ogni tipo
        /// spedito con <c>persistent: true</c> deve comparire qui, o al rinvio
        /// partirebbe senza corpo.
        /// </summary>
        private static object DeserializePersistentPayload(PersistentMutationOutbox.Entry mutation) =>
            mutation.messageType switch
            {
                MessageTypes.SinglePlayerClaimDeathReward =>
                    JsonUtility.FromJson<SinglePlayerDeathRewardRequest>(mutation.payloadJson),
                MessageTypes.SinglePlayerClaimTutorialReward =>
                    JsonUtility.FromJson<SinglePlayerTutorialRewardRequest>(mutation.payloadJson),
                MessageTypes.SinglePlayerClaimAdMultiplier =>
                    JsonUtility.FromJson<SinglePlayerAdMultiplierRequest>(mutation.payloadJson),
                MessageTypes.SinglePlayerPendingAdRewardDismiss =>
                    JsonUtility.FromJson<SinglePlayerDismissPendingAdRewardRequest>(mutation.payloadJson),
                MessageTypes.SinglePlayerPurchaseUnlock =>
                    JsonUtility.FromJson<SinglePlayerPurchaseUnlockRequest>(mutation.payloadJson),
                MessageTypes.SinglePlayerClearChapter =>
                    JsonUtility.FromJson<SinglePlayerClearChapterRequest>(mutation.payloadJson),
                MessageTypes.SinglePlayerClaimTutorialModule =>
                    JsonUtility.FromJson<SinglePlayerTutorialModuleRequest>(mutation.payloadJson),
                MessageTypes.SanctuaryBuyItem =>
                    JsonUtility.FromJson<SanctuaryBuyItemRequest>(mutation.payloadJson),
                MessageTypes.TavernClaimQuest =>
                    JsonUtility.FromJson<TavernClaimQuestRequest>(mutation.payloadJson),
                MessageTypes.TalentsBuy =>
                    JsonUtility.FromJson<TalentBuyRequest>(mutation.payloadJson),
                MessageTypes.IapRedeem =>
                    JsonUtility.FromJson<IapRedeemRequest>(mutation.payloadJson),
                // Le uniche due senza corpo: il premio di giornata e la riscossione dei
                // livelli non hanno niente da dire oltre al proprio tipo.
                MessageTypes.TavernClaimBonus => null,
                _ => null
            };

        /// <summary>
        /// true se il payload ricostruito è utilizzabile. Una mutazione con un corpo
        /// salvato che nessun ramo sa rileggere non va spedita: il server la rifiuterebbe
        /// per payload mancante e - trattandosi di tipi con dedup - si terrebbe quel
        /// rifiuto in memoria, bruciando il requestId anche per un rinvio fatto bene.
        /// Meglio accorgersene qui, dove si vede, che a valle dove non si vede più.
        /// </summary>
        private static bool CanRebuildPayload(PersistentMutationOutbox.Entry mutation, object payload) =>
            payload != null
            || string.IsNullOrEmpty(mutation.payloadJson)
            || mutation.payloadJson == "{}";

        private async Task<TavernData> RequestTavernAsync(
            string messageType, object payload, string fallbackMessage, bool persistent = false)
        {
            Envelope envelope = await RequestAsync(
                messageType, payload, MessageTypes.TavernData, persistent);

            ThrowIfError(envelope, fallbackMessage);
            return PvpServerClient.ParsePayload<TavernData>(envelope) ?? new TavernData();
        }

        private async Task<TalentData> RequestTalentsAsync(
            string messageType, object payload, string fallbackMessage, bool persistent = false)
        {
            Envelope envelope = await RequestAsync(
                messageType, payload, MessageTypes.TalentsData, persistent);

            ThrowIfError(envelope, fallbackMessage);
            return PvpServerClient.ParsePayload<TalentData>(envelope) ?? new TalentData();
        }

        private async Task<SanctuaryData> RequestSanctuaryAsync(
            string messageType, object payload, string fallbackMessage, bool persistent = false)
        {
            Envelope envelope = await RequestAsync(
                messageType, payload, MessageTypes.SanctuaryData, persistent);

            ThrowIfError(envelope, fallbackMessage);
            return PvpServerClient.ParsePayload<SanctuaryData>(envelope) ?? new SanctuaryData();
        }

        private async Task<SinglePlayerProgressData> RequestProgressAsync(
            string messageType, object payload, bool persistent = false)
        {
            Envelope envelope = await RequestAsync(
                messageType, payload, MessageTypes.SinglePlayerProgressData, persistent);

            ThrowIfError(envelope, "Richiesta progressione rifiutata.");
            return PvpServerClient.ParsePayload<SinglePlayerProgressData>(envelope);
        }

        private async Task<SinglePlayerRewardResult> RequestRewardAsync(
            string messageType, object payload, bool persistent = false)
        {
            Envelope envelope = await RequestAsync(
                messageType, payload, MessageTypes.SinglePlayerRewardResult, persistent);

            ThrowIfError(envelope, "Richiesta reward rifiutata.");
            return PvpServerClient.ParsePayload<SinglePlayerRewardResult>(envelope);
        }

        private static void ThrowIfError(Envelope envelope, string fallbackMessage)
        {
            if (envelope.type != MessageTypes.Error)
                return;
            ErrorMessage error = PvpServerClient.ParsePayload<ErrorMessage>(envelope);
            throw new InvalidOperationException(error?.message ?? fallbackMessage);
        }

        private static SinglePlayerRewardOutcome ToOutcome(SinglePlayerRewardResult result)
        {
            result ??= new SinglePlayerRewardResult();
            return new SinglePlayerRewardOutcome(
                ToSave(result.progress),
                result.rewardClaimId,
                Mathf.Max(0, result.grantedHoney),
                Mathf.Max(0, result.grantedAccountExperience),
                Mathf.Max(0, result.levelsGained),
                Mathf.Max(0, result.grantedTalentPoints));
        }

        private static SinglePlayerProgressSave ToSave(SinglePlayerProgressData data)
        {
            data ??= new SinglePlayerProgressData();
            return new SinglePlayerProgressSave
            {
                honey = Mathf.Max(0, data.honey),
                accountLevel = Mathf.Max(1, data.accountLevel),
                accountExperience = Mathf.Max(0, data.accountExperience),
                accountTotalExperience = Mathf.Max(0, data.accountTotalExperience),
                accountExperienceToNextLevel = data.accountExperienceToNextLevel <= 0
                    ? 100
                    : data.accountExperienceToNextLevel,
                pendingLevelRewards = Mathf.Max(0, data.pendingLevelRewards),
                talentPoints = Mathf.Max(0, data.talentPoints),
                talentPointsEarned = Mathf.Max(0, data.talentPointsEarned),
                tutorialCompleted = data.tutorialCompleted,
                completedTutorialModules = ToList(data.completedTutorialModules),
                hardcoreUnlocked = data.hardcoreUnlocked,
                unlockedChapters = ToList(data.unlockedChapters),
                unlockedStages = ToList(data.unlockedStages),
                unlockedClasses = ToList(data.unlockedClasses),
                pendingClassChoices = ToList(data.pendingClassChoices),
                unlockedScenarios = ToList(data.unlockedScenarios),
                unlockedSecondAbilities = ToList(data.unlockedSecondAbilities),
                clearedChapters = ToList(data.clearedChapters),
                unlockedSlots = ToList(data.unlockedSlots),
                bagItems = ToList(data.bagItems),
                counters = ToCounters(data.counters),
                talentLoadout = ToLoadout(data.talentLoadout)
            };
        }

        /// <summary>
        /// Un client vecchio o un server che non manda ancora il pacchetto lasciano il campo
        /// a null: il gioco deve partire lo stesso, con tutti i modificatori a zero.
        /// </summary>
        private static TalentLoadoutSave ToLoadout(TalentLoadoutData data)
        {
            var save = new TalentLoadoutSave();
            if (data == null)
                return save;

            save.startingGold = Mathf.Max(0, data.startingGold);
            save.startingEssence = Mathf.Max(0, data.startingEssence);
            save.merchantDiscountPercent = Mathf.Clamp(data.merchantDiscountPercent, 0, 90);
            save.recoveryDiscountPercent = Mathf.Clamp(data.recoveryDiscountPercent, 0, 90);
            save.opensEveryFight = data.opensEveryFight;
            save.forgeTemperedCards = Mathf.Max(0, data.forgeTemperedCards);
            save.firstMerchantUpgradeFree = data.firstMerchantUpgradeFree;
            save.masteryThresholdPercent = Mathf.Clamp(data.masteryThresholdPercent, 0, 90);
            save.roomChangeMana = Mathf.Max(0, data.roomChangeMana);
            save.bonusMaximumMana = Mathf.Max(0, data.bonusMaximumMana);
            save.firstAbilityFreeEachRoom = data.firstAbilityFreeEachRoom;
            save.bossVigorBonus = Mathf.Max(0, data.bossVigorBonus);
            save.extraLootItems = Mathf.Max(0, data.extraLootItems);
            save.savesFirstFallenCard = data.savesFirstFallenCard;

            if (data.initiativeBonusBySlot != null)
            {
                foreach (int bonus in data.initiativeBonusBySlot)
                    save.initiativeBonusBySlot.Add(Mathf.Max(0, bonus));
            }
            return save;
        }

        private static System.Collections.Generic.List<SinglePlayerCounterSave> ToCounters(PlayerCounterData[] values)
        {
            var counters = new System.Collections.Generic.List<SinglePlayerCounterSave>();
            if (values == null)
                return counters;

            foreach (PlayerCounterData counter in values)
            {
                if (counter != null && !string.IsNullOrWhiteSpace(counter.key))
                    counters.Add(new SinglePlayerCounterSave { key = counter.key, value = Mathf.Max(0, counter.value) });
            }
            return counters;
        }

        private static System.Collections.Generic.List<string> ToList(string[] values) =>
            values == null
                ? new System.Collections.Generic.List<string>()
                : new System.Collections.Generic.List<string>(values);

        private static string ToServerUnlockType(SinglePlayerUnlockType type) => type switch
        {
            SinglePlayerUnlockType.Chapter => "chapter",
            SinglePlayerUnlockType.Stage => "stage",
            SinglePlayerUnlockType.Class => "class",
            SinglePlayerUnlockType.Scenario => "scenario",
            SinglePlayerUnlockType.SecondAbility => "secondAbility",
            SinglePlayerUnlockType.Slot => "slot",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
