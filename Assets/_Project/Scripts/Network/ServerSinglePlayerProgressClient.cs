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
        Task<SanctuaryData> BuySanctuaryItemAsync(string itemId);
        Task<SanctuaryData> SetSanctuaryBagAsync(string[] itemIds);
        Task<TavernData> GetTavernAsync();
        Task<TavernData> ClaimTavernQuestAsync(string questId);
        Task<TavernData> ClaimTavernBonusAsync();
        Task<SinglePlayerRewardOutcome> ClaimTutorialRewardAsync(string tutorialRunId);
        Task<SinglePlayerRewardOutcome> ClaimDeathRewardAsync(DeathRewardSummary summary);
        Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId);
    }

    /// <summary>Esito autoritativo di una reward: nuovo stato, id reward (per l'ad) e miele accreditato.</summary>
    public readonly struct SinglePlayerRewardOutcome
    {
        public SinglePlayerRewardOutcome(
            SinglePlayerProgressSave progress,
            string rewardClaimId,
            int grantedHoney,
            int grantedAccountExperience = 0,
            int levelsGained = 0)
        {
            Progress = progress;
            RewardClaimId = rewardClaimId;
            GrantedHoney = grantedHoney;
            GrantedAccountExperience = grantedAccountExperience;
            LevelsGained = levelsGained;
        }

        public SinglePlayerProgressSave Progress { get; }
        public string RewardClaimId { get; }
        public int GrantedHoney { get; }
        public int GrantedAccountExperience { get; }
        public int LevelsGained { get; }
    }

    /// <summary>Sommario di una run terminata, usato dal server per calcolare (con cap) la reward alla morte.</summary>
    public readonly struct DeathRewardSummary
    {
        public DeathRewardSummary(
            string runId, string mode, string chapterId, string stageId,
            int roomsCleared, int enemiesDefeated, int bossesDefeated, int matchExperience = 0,
            int minibossesDefeated = 0, string[] defeatedBossIds = null, string[] consumedItemIds = null,
            int diceRolled = 0, int abilitiesUsed = 0, int experienceEarned = 0)
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
    }

    /// <summary>
    /// Client remoto per la progressione single player server-authoritative.
    /// Richiede un PvpServerClient gia connesso e autenticato.
    /// </summary>
    public sealed class ServerSinglePlayerProgressClient : IServerSinglePlayerProgressClient
    {
        private const float DefaultTimeoutSeconds = 8f;

        private readonly PvpServerMessageDispatcher dispatcher;
        private readonly float timeoutSeconds;

        public ServerSinglePlayerProgressClient(PvpServerClient client, float timeoutSeconds = DefaultTimeoutSeconds)
            : this(new PvpServerMessageDispatcher(client), timeoutSeconds)
        {
        }

        public ServerSinglePlayerProgressClient(PvpServerMessageDispatcher dispatcher, float timeoutSeconds = DefaultTimeoutSeconds)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.timeoutSeconds = Mathf.Max(0.5f, timeoutSeconds);
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
                new SinglePlayerClearChapterRequest { bossId = bossId });
            return ToSave(data);
        }

        private async Task<SinglePlayerProgressSave> PurchaseRawAsync(string serverType, string id)
        {
            SinglePlayerProgressData data = await RequestProgressAsync(
                MessageTypes.SinglePlayerPurchaseUnlock,
                new SinglePlayerPurchaseUnlockRequest { type = serverType, id = id });
            return ToSave(data);
        }

        public async Task<SinglePlayerRewardOutcome> ClaimTutorialRewardAsync(string tutorialRunId)
        {
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimTutorialReward,
                new SinglePlayerTutorialRewardRequest { tutorialRunId = tutorialRunId });
            return ToOutcome(result);
        }

        public async Task<SinglePlayerRewardOutcome> ClaimDeathRewardAsync(DeathRewardSummary summary)
        {
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimDeathReward,
                new SinglePlayerDeathRewardRequest
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
                    diceRolled = summary.DiceRolled,
                    abilitiesUsed = summary.AbilitiesUsed,
                    // Gli oggetti usati sono esattamente quelli scalati dalla scorta: un campo
                    // a parte sarebbe un secondo conteggio della stessa cosa, e potrebbe mentire.
                    itemsUsed = summary.ConsumedItemIds.Length,
                    experienceEarned = summary.ExperienceEarned
                });
            return ToOutcome(result);
        }

        public async Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId)
        {
            SinglePlayerRewardResult result = await RequestRewardAsync(
                MessageTypes.SinglePlayerClaimAdMultiplier,
                new SinglePlayerAdMultiplierRequest
                {
                    rewardClaimId = rewardClaimId,
                    adImpressionId = adImpressionId
                });
            return ToOutcome(result);
        }

        /// <summary>
        /// Catalogo del Santuario con le prove gia' valutate dal server. Il client non
        /// conosce ne i costi ne le regole: li riceve e li disegna.
        /// </summary>
        public Task<SanctuaryData> GetSanctuaryAsync() =>
            RequestSanctuaryAsync(MessageTypes.SanctuaryGet, null, "Richiesta Santuario rifiutata.");

        /// <summary>Compra un consumabile: si somma alla scorta permanente.</summary>
        public Task<SanctuaryData> BuySanctuaryItemAsync(string itemId) =>
            RequestSanctuaryAsync(
                MessageTypes.SanctuaryBuyItem,
                new SanctuaryBuyItemRequest { itemId = itemId },
                "Acquisto oggetto rifiutato.");

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
        public Task<TavernData> ClaimTavernQuestAsync(string questId) =>
            RequestTavernAsync(
                MessageTypes.TavernClaimQuest,
                new TavernClaimQuestRequest { questId = questId },
                "Riscossione rifiutata.");

        /// <summary>Riscuote il premio per aver completato tutte le quest del giorno.</summary>
        public Task<TavernData> ClaimTavernBonusAsync() =>
            RequestTavernAsync(MessageTypes.TavernClaimBonus, null, "Premio di giornata rifiutato.");

        private async Task<TavernData> RequestTavernAsync(string messageType, object payload, string fallbackMessage)
        {
            Envelope envelope = await dispatcher.RequestAsync(
                messageType,
                payload,
                MessageTypes.TavernData,
                timeoutSeconds);

            ThrowIfError(envelope, fallbackMessage);
            return PvpServerClient.ParsePayload<TavernData>(envelope) ?? new TavernData();
        }

        private async Task<SanctuaryData> RequestSanctuaryAsync(string messageType, object payload, string fallbackMessage)
        {
            Envelope envelope = await dispatcher.RequestAsync(
                messageType,
                payload,
                MessageTypes.SanctuaryData,
                timeoutSeconds);

            ThrowIfError(envelope, fallbackMessage);
            return PvpServerClient.ParsePayload<SanctuaryData>(envelope) ?? new SanctuaryData();
        }

        private async Task<SinglePlayerProgressData> RequestProgressAsync(string messageType, object payload)
        {
            Envelope envelope = await dispatcher.RequestAsync(
                messageType,
                payload,
                MessageTypes.SinglePlayerProgressData,
                timeoutSeconds);

            ThrowIfError(envelope, "Richiesta progressione rifiutata.");
            return PvpServerClient.ParsePayload<SinglePlayerProgressData>(envelope);
        }

        private async Task<SinglePlayerRewardResult> RequestRewardAsync(string messageType, object payload)
        {
            Envelope envelope = await dispatcher.RequestAsync(
                messageType,
                payload,
                MessageTypes.SinglePlayerRewardResult,
                timeoutSeconds);

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
                Mathf.Max(0, result.levelsGained));
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
                tutorialCompleted = data.tutorialCompleted,
                hardcoreUnlocked = data.hardcoreUnlocked,
                unlockedChapters = ToList(data.unlockedChapters),
                unlockedStages = ToList(data.unlockedStages),
                unlockedClasses = ToList(data.unlockedClasses),
                unlockedScenarios = ToList(data.unlockedScenarios),
                unlockedSecondAbilities = ToList(data.unlockedSecondAbilities),
                clearedChapters = ToList(data.clearedChapters),
                unlockedSlots = ToList(data.unlockedSlots),
                unlockedItems = ToList(data.unlockedItems),
                bagItems = ToList(data.bagItems),
                counters = ToCounters(data.counters)
            };
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
            SinglePlayerUnlockType.Item => "item",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
