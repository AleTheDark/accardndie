using System;

namespace AccardND.NetProtocol
{
    public enum TavernQuestDifficulty
    {
        Easy,
        Intermediate,
        Advanced
    }

    /// <summary>
    /// Una quest giornaliera della taverna, gia' valutata sul giocatore. Il client riceve
    /// titolo, descrizione e progresso: il catalogo e le soglie restano sul server.
    /// </summary>
    [Serializable]
    public sealed class TavernQuestData
    {
        public string questId;

        /// <summary>
        /// Chiavi stabili risolte dal client nella String Table "Game". Titolo e
        /// descrizione restano temporaneamente nel payload come fallback compatibile con
        /// server/client meno recenti, ma non sono piu' la fonte primaria della UI.
        /// </summary>
        public string titleKey;
        public string descriptionKey;

        public string title;
        public string description;
        public int current;
        public int threshold;
        public bool completed;

        public TavernQuestDifficulty difficulty;

        /// <summary>Punti assegnati al bonus giornaliero quando la quest viene completata.</summary>
        public int bonusPoints;

        /// <summary>Ricompensa gia' riscossa: la riga si mostra come chiusa.</summary>
        public bool claimed;

        public int honeyReward;
    }

    /// <summary>
    /// Bacheca della taverna: le quest del giorno con il loro progresso piu' lo stato del
    /// premio di giornata (risposta a tavern.get e a ogni riscossione).
    ///
    /// Le quest non si accettano: sono attive dall'assegnazione. Il client non calcola
    /// nulla, disegna quello che arriva.
    /// </summary>
    [Serializable]
    public sealed class TavernData
    {
        /// <summary>Vasetti di miele del giocatore dopo l'operazione.</summary>
        public int honey;

        public TavernQuestData[] quests;

        /// <summary>Quante quest del giorno sono complete (riscosse o no).</summary>
        public int completedCount;

        /// <summary>Punti prodotti dalle quest completate, indipendentemente dalla riscossione.</summary>
        public int completedBonusPoints;

        /// <summary>
        /// Quante quest servono per il premio di giornata. E' meno del numero di quest in
        /// bacheca: quelle d'arena o di fine capitolo possono restare fuori portata senza
        /// costare il premio.
        /// </summary>
        public int questsRequiredForBonus;

        /// <summary>Soglia autoritativa di punti per sbloccare il premio giornaliero.</summary>
        public int bonusPointsRequired;

        /// <summary>Premio extra per aver raggiunto la soglia di quest completate.</summary>
        public int bonusHoneyReward;

        /// <summary>La soglia e' raggiunta: il premio e' riscuotibile.</summary>
        public bool bonusAvailable;

        public bool bonusClaimed;

        /// <summary>
        /// Secondi che mancano al cambio delle quest. Arriva dal server perche' l'orologio
        /// del dispositivo puo' essere sfasato: il client lo fa solo scorrere.
        /// </summary>
        public int secondsToRefresh;
    }

    /// <summary>Riscossione della ricompensa di una singola quest completata.</summary>
    [Serializable]
    public sealed class TavernClaimQuestRequest
    {
        public string questId;

        /// <summary>
        /// Moltiplicatore scelto dal giocatore: 1 per la riscossione gratuita, 5 dopo il
        /// rewarded video. Zero resta equivalente a 1 per compatibilita' coi vecchi client.
        /// </summary>
        public int rewardMultiplier;
    }
}
