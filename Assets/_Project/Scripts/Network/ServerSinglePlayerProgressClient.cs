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
        Task<SinglePlayerRewardOutcome> ClaimTutorialRewardAsync(string tutorialRunId);
        Task<SinglePlayerRewardOutcome> ClaimDeathRewardAsync(DeathRewardSummary summary);
        Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId);
    }

    /// <summary>Esito autoritativo di una reward: nuovo stato, id reward (per l'ad) e miele accreditato.</summary>
    public readonly struct SinglePlayerRewardOutcome
    {
        public SinglePlayerRewardOutcome(SinglePlayerProgressSave progress, string rewardClaimId, int grantedHoney)
        {
            Progress = progress;
            RewardClaimId = rewardClaimId;
            GrantedHoney = grantedHoney;
        }

        public SinglePlayerProgressSave Progress { get; }
        public string RewardClaimId { get; }
        public int GrantedHoney { get; }
    }

    /// <summary>Sommario di una run terminata, usato dal server per calcolare (con cap) la reward alla morte.</summary>
    public readonly struct DeathRewardSummary
    {
        public DeathRewardSummary(
            string runId, string mode, string chapterId, string stageId,
            int roomsCleared, int enemiesDefeated, int bossesDefeated)
        {
            RunId = runId;
            Mode = mode;
            ChapterId = chapterId;
            StageId = stageId;
            RoomsCleared = roomsCleared;
            EnemiesDefeated = enemiesDefeated;
            BossesDefeated = bossesDefeated;
        }

        public string RunId { get; }
        public string Mode { get; }
        public string ChapterId { get; }
        public string StageId { get; }
        public int RoomsCleared { get; }
        public int EnemiesDefeated { get; }
        public int BossesDefeated { get; }
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
                    bossesDefeated = summary.BossesDefeated
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
                Mathf.Max(0, result.grantedHoney));
        }

        private static SinglePlayerProgressSave ToSave(SinglePlayerProgressData data)
        {
            data ??= new SinglePlayerProgressData();
            return new SinglePlayerProgressSave
            {
                honey = Mathf.Max(0, data.honey),
                tutorialCompleted = data.tutorialCompleted,
                hardcoreUnlocked = data.hardcoreUnlocked,
                unlockedChapters = ToList(data.unlockedChapters),
                unlockedStages = ToList(data.unlockedStages),
                unlockedClasses = ToList(data.unlockedClasses),
                unlockedScenarios = ToList(data.unlockedScenarios),
                unlockedSecondAbilities = ToList(data.unlockedSecondAbilities)
            };
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
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
