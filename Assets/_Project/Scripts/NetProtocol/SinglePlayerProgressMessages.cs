using System;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class SinglePlayerProgressData
    {
        public int honey;
        public bool tutorialCompleted;
        public bool hardcoreUnlocked;
        public string[] unlockedChapters;
        public string[] unlockedStages;
        public string[] unlockedClasses;
        public string[] unlockedScenarios;
        public string[] unlockedSecondAbilities;
    }

    [Serializable]
    public sealed class SinglePlayerPurchaseUnlockRequest
    {
        public string type;
        public string id;
    }

    [Serializable]
    public sealed class SinglePlayerTutorialRewardRequest
    {
        public string tutorialRunId;
    }

    [Serializable]
    public sealed class SinglePlayerDeathRewardRequest
    {
        public string runId;
        public string mode;
        public string chapterId;
        public string stageId;
        public int roomsCleared;
        public int enemiesDefeated;
        public int bossesDefeated;
    }

    [Serializable]
    public sealed class SinglePlayerAdMultiplierRequest
    {
        public string rewardClaimId;
        public string adImpressionId;
    }

    /// <summary>
    /// Risposta del server a una reward (tutorial/morte/ad): lo stato autoritativo aggiornato,
    /// l'id della reward concessa (per applicarci in seguito il moltiplicatore pubblicitario)
    /// e il miele effettivamente accreditato da questa richiesta.
    /// </summary>
    [Serializable]
    public sealed class SinglePlayerRewardResult
    {
        public SinglePlayerProgressData progress;
        public string rewardClaimId;
        public int grantedHoney;
    }
}
