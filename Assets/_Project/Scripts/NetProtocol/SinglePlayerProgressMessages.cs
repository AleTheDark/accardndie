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
        /// <summary>Classi offerte da una ricompensa ancora da scegliere.</summary>
        public string[] pendingClassChoices;
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
    public sealed class SinglePlayerChooseClassRequest
    {
        public string classId;
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

    /// <summary>
    /// Apertura di una run di campagna: stesso <c>runId</c> che chiudera' la run con la
    /// death reward, cosi' inizio e fine sono la stessa riga nello storico. Il momento lo
    /// mette il server, il client dichiara solo cosa sta per giocare.
    /// </summary>
    [Serializable]
    public sealed class SinglePlayerRunStartRequest
    {
        public string runId;
        public string mode;
        public string chapterId;
        public string stageId;
    }

    /// <summary>Conferma della presa in carico: il client la usa solo per sapere che e' arrivata.</summary>
    [Serializable]
    public sealed class SinglePlayerRunStartAck
    {
        public string runId;
        public string startedAt;
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
    /// Una ricompensa gia' concessa su cui il moltiplicatore pubblicitario e' ancora
    /// disponibile: il video non e' mai partito (rete assente, annuncio non pronto, popup
    /// chiuso). Il server la ripropone finche' la finestra non scade, cosi' una caduta di
    /// connessione a fine run non brucia il x3.
    /// </summary>
    [Serializable]
    public sealed class SinglePlayerPendingAdRewardData
    {
        public string claimId;

        /// <summary>'death' per le run di campagna, 'tutorial' per la reward del tutorial.</summary>
        public string rewardType;

        /// <summary>EXP account gia' accreditata da questa reward.</summary>
        public int baseAccountExperience;

        /// <summary>EXP account che il video aggiungerebbe (base * (moltiplicatore - 1)).</summary>
        public int extraAccountExperience;

        /// <summary>Capitolo della run, quando la reward viene da una campagna.</summary>
        public string chapterId;

        /// <summary>Stanze superate nella run, per riconoscere di quale partita si parla.</summary>
        public int roomsCleared;

        /// <summary>Momento in cui la reward e' stata concessa (ISO 8601 UTC).</summary>
        public string createdAt;

        /// <summary>Ore che restano prima che l'offerta scada.</summary>
        public int hoursLeft;
    }

    /// <summary>Le offerte di moltiplicatore ancora in piedi per questo giocatore.</summary>
    [Serializable]
    public sealed class SinglePlayerPendingAdRewardsData
    {
        public SinglePlayerPendingAdRewardData[] rewards;
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
