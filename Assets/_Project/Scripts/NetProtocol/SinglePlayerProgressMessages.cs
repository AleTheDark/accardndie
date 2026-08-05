using System;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class SinglePlayerProgressData
    {
        public int honey;
        public int accountLevel;
        public int accountExperience;
        public int accountTotalExperience;
        public int accountExperienceToNextLevel;
        public int pendingLevelRewards;
        public bool tutorialCompleted;
        public bool hardcoreUnlocked;
        public string[] unlockedChapters;
        public string[] unlockedStages;
        public string[] unlockedClasses;
        public string[] unlockedScenarios;
        public string[] unlockedSecondAbilities;

        /// <summary>
        /// Capitoli effettivamente portati a termine (boss finale sconfitto), distinti dai
        /// capitoli soltanto sbloccati. Alimenta i requisiti del Santuario.
        /// </summary>
        public string[] clearedChapters;

        /// <summary>Slot aggiuntivi della bisaccia acquistati.</summary>
        public string[] unlockedSlots;

        /// <summary>
        /// Oggetti sbloccati al Santuario, cioe' quelli che il negozio puo' vendere.
        /// Sbloccare non da' copie: quelle si comprano.
        /// </summary>
        public string[] unlockedItems;

        /// <summary>
        /// Consumabili scelti per la prossima run. Viaggiano con la progressione, non solo
        /// col catalogo del Santuario: la run deve poterli caricare senza che il giocatore
        /// sia passato dal Santuario in questa sessione.
        /// </summary>
        public string[] bagItems;

        /// <summary>
        /// Contatori cumulativi di campagna (nemici, boss, run). Servono a mostrare il
        /// progresso verso i requisiti del Santuario.
        /// </summary>
        public PlayerCounterData[] counters;
    }

    /// <summary>Un contatore cumulativo del giocatore.</summary>
    [Serializable]
    public sealed class PlayerCounterData
    {
        public string key;
        public int value;
    }

    /// <summary>
    /// Notifica che il boss finale di un capitolo e stato sconfitto. Il client manda solo
    /// l'id del boss: la mappa boss-capitolo e la concessione del capitolo successivo sono
    /// responsabilita del server.
    /// </summary>
    [Serializable]
    public sealed class SinglePlayerClearChapterRequest
    {
        public string bossId;
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
        public int matchExperience;

        /// <summary>Miniboss (golem e simili) sconfitti nella run.</summary>
        public int minibossesDefeated;

        /// <summary>Dadi tirati nella run: un tiro doppio ne conta due.</summary>
        public int diceRolled;

        /// <summary>Abilita' di classe attivate dalle pedine del giocatore nella run.</summary>
        public int abilitiesUsed;

        /// <summary>Consumabili usati nella run (lunghezza di <see cref="consumedItemIds"/>).</summary>
        public int itemsUsed;

        /// <summary>Esperienza guadagnata nella run, al lordo di quella spesa dal mercante.</summary>
        public int experienceEarned;

        /// <summary>Id dei boss e miniboss sconfitti nella run, per i contatori per-boss.</summary>
        public string[] defeatedBossIds;

        /// <summary>
        /// Consumabili della bisaccia davvero usati nella run: solo questi vengono scalati
        /// dalla scorta. Quelli non usati restano al giocatore.
        /// </summary>
        public string[] consumedItemIds;
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
        public int grantedAccountExperience;
        public int levelsGained;
    }
}
