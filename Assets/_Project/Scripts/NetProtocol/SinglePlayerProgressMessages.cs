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
        /// <summary>
        /// Quanta esperienza serve <em>in tutto</em> per il livello successivo: e' il
        /// denominatore della barra, non quanta ne manca.
        /// </summary>
        public int accountExperienceToNextLevel;

        public int pendingLevelRewards;

        /// <summary>Punti talento non ancora spesi.</summary>
        public int talentPoints;

        /// <summary>Punti talento guadagnati in tutto, spesi compresi. Solo per la UI.</summary>
        public int talentPointsEarned;

        public bool tutorialCompleted;

        /// <summary>
        /// Moduli del tutorial progressivo gia' portati a termine. Da qui derivano tutti i
        /// cancelli dell'onboarding: e' l'unico stato del percorso, e non esiste un secondo
        /// contatore che potrebbe sfasarsi.
        /// </summary>
        public string[] completedTutorialModules;

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

        /// <summary>
        /// I modificatori dei talenti gia' risolti per la prossima run. Viaggiano con la
        /// progressione perche' la run deve poterli caricare senza essere passata dalla
        /// schermata dei talenti in questa sessione, esattamente come la bisaccia.
        /// </summary>
        public TalentLoadoutData talentLoadout;
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

    /// <summary>
    /// Fine di un modulo del tutorial progressivo. Il client dichiara solo quale modulo ha
    /// portato a termine: cosa spetti a quel modulo lo decide il catalogo del server.
    /// Idempotente per <c>moduleId</c>, come tutte le reward.
    /// </summary>
    [Serializable]
    public sealed class SinglePlayerTutorialModuleRequest
    {
        public string moduleId;

        /// <summary>Riferimento del client, solo per ritrovare la riscossione nei log.</summary>
        public string moduleRunId;
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

        /// <summary>
        /// Consumabili usati nella run, di qualunque provenienza: bisaccia, bottino delle stanze
        /// o acquisto al mercante usato subito. Non e' la lunghezza di
        /// <see cref="consumedItemIds"/>, che copre solo la bisaccia.
        /// </summary>
        public int itemsUsed;

        /// <summary>Esperienza guadagnata nella run, al lordo di quella spesa dal mercante.</summary>
        public int experienceEarned;

        /// <summary>Supreme attivate dalle pedine del giocatore nella run.</summary>
        public int supremesUsed;

        /// <summary>Sfide veloci portate a termine nella run: la rinuncia non conta.</summary>
        public int quickChallengesCompleted;

        /// <summary>Acquisti conclusi al mercante nella run, carte e oggetti insieme.</summary>
        public int merchantPurchases;

        /// <summary>Oro guadagnato nelle stanze e nelle prove lampo, al netto di vendite e rimborsi.</summary>
        public int goldEarned;

        /// <summary>Livelli guadagnati nella run.</summary>
        public int levelsGained;

        /// <summary>Id dei boss e miniboss sconfitti nella run, per i contatori per-boss.</summary>
        public string[] defeatedBossIds;

        /// <summary>
        /// Consumabili della bisaccia davvero usati nella run: solo questi vengono scalati
        /// dalla scorta. Quelli non usati restano al giocatore.
        /// </summary>
        public string[] consumedItemIds;

        /// <summary>
        /// Oggetti trovati nelle stanze o comprati al mercante e mai usati: il server li versa
        /// nella scorta permanente. La run finita non se li porta via, restano disponibili per
        /// la bisaccia della prossima.
        /// </summary>
        public string[] keptItemIds;
    }

    [Serializable]
    public sealed class SinglePlayerAdMultiplierRequest
    {
        public string rewardClaimId;
        public string adImpressionId;
    }

    [Serializable]
    public sealed class SinglePlayerDismissPendingAdRewardRequest
    {
        public string rewardClaimId;
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

        /// <summary>Miele gia' accreditato e miele aggiuntivo ottenibile col video.</summary>
        public int baseHoney;
        public int extraHoney;

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

        /// <summary>
        /// Punti talento consegnati dalla riscossione dei livelli. Sostituisce il miele che
        /// il livello pagava prima: e' quello che la schermata di level-up deve annunciare.
        /// </summary>
        public int grantedTalentPoints;
    }
}
