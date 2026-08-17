using System;

namespace AccardND.NetProtocol
{
    /// <summary>
    /// Un nodo dell'albero dei talenti gia' valutato sul giocatore. Il client riceve rango,
    /// prezzo, effetto e il motivo per cui eventualmente non si puo' comprare: le regole
    /// restano sul server, qui c'e' solo quello che serve a disegnare la cella.
    /// </summary>
    [Serializable]
    public sealed class TalentEntryData
    {
        public string id;
        public string branch;
        public int tier;
        public string name;
        public string description;

        /// <summary>Rango posseduto. Zero se il nodo non e' mai stato comprato.</summary>
        public int rank;

        public int maxRank;

        /// <summary>Costo del prossimo rango. Zero quando il nodo e' al massimo.</summary>
        public int nextCost;

        /// <summary>Effetto al rango posseduto, gia' nell'unita' del nodo.</summary>
        public int currentValue;

        /// <summary>Effetto che avrebbe al rango successivo. Zero se al massimo.</summary>
        public int nextValue;

        /// <summary>
        /// L'effetto attuale scritto per il giocatore, unita' di misura compresa
        /// ("+6 oro alla partenza"). Null al rango zero e sui nodi senza un numero da
        /// mostrare.
        ///
        /// Arriva gia' composto dal server perche' l'unita' appartiene al nodo: il numero
        /// nudo che il client mostrava prima valeva monete su un talento e percentuali su
        /// quello accanto, scritti identici.
        /// </summary>
        public string currentValueText;

        /// <summary>L'effetto del prossimo rango, scritto. Null quando il nodo e' al massimo.</summary>
        public string nextValueText;

        /// <summary>Punti da spendere nel ramo per aprire il tier di questo nodo.</summary>
        public int tierGate;

        /// <summary>Il tier e' aperto: manca solo il prezzo.</summary>
        public bool tierUnlocked;

        /// <summary>Comprabile subito: tier aperto, rango disponibile e punti sufficienti.</summary>
        public bool purchasable;

        /// <summary>
        /// Perche' non si puo' comprare, gia' scritto per il giocatore. Null quando
        /// <see cref="purchasable"/> e' vero.
        /// </summary>
        public string lockedReason;
    }

    /// <summary>Un ramo dell'albero con quanto ci si e' gia' speso dentro.</summary>
    [Serializable]
    public sealed class TalentBranchData
    {
        public string id;
        public string name;
        public string description;
        public int pointsSpent;
    }

    /// <summary>
    /// L'albero completo con i progressi del giocatore (risposta a talents.get).
    /// </summary>
    [Serializable]
    public sealed class TalentData
    {
        public int talentPoints;
        public int talentPointsEarned;
        public TalentBranchData[] branches;
        public TalentEntryData[] talents;
    }

    /// <summary>Acquisto di un rango. Il server valida punti, tier e rango massimo.</summary>
    [Serializable]
    public sealed class TalentBuyRequest
    {
        public string talentId;
    }

    /// <summary>
    /// I modificatori che i talenti applicano a una run, gia' risolti dal server e piatti.
    /// Viaggiano dentro la progressione, non su un messaggio a parte: la run deve poterli
    /// caricare senza un secondo giro di rete, e un solo punto di ingresso vuol dire che
    /// cambiare un talento e' cambiare un campo qui e nient'altro.
    ///
    /// Nessun campo di questo pacchetto puo' aumentare l'esperienza guadagnata nella run:
    /// vedi TalentCatalog per il perche'.
    /// </summary>
    [Serializable]
    public sealed class TalentLoadoutData
    {
        /// <summary>Oro in tasca all'inizio della run.</summary>
        public int startingGold;

        /// <summary>Essenza in piu' nella forgia, oltre a quella di configurazione.</summary>
        public int startingEssence;

        /// <summary>Sconto percentuale sui prezzi del mercante.</summary>
        public int merchantDiscountPercent;

        /// <summary>Sconto percentuale sul costo di recupero di una pedina.</summary>
        public int recoveryDiscountPercent;

        /// <summary>
        /// Bonus ai dadi d'iniziativa del giocatore, in ordine di tiro: indice 0 e' il primo
        /// dado. Si applica all'ordinamento, mai al numero mostrato sul dado.
        /// </summary>
        public int[] initiativeBonusBySlot;

        /// <summary>
        /// "Apertura": il primo dado d'iniziativa del giocatore batte qualunque altro tiro.
        /// Prima qui c'era la vittoria delle parita' d'iniziativa, che non si verificano mai
        /// perche' i tiri sono estratti unici fra tutti i combattenti.
        /// </summary>
        public bool opensEveryFight;

        /// <summary>Quante carte del mazzo appena forgiato guadagnano un punto forza.</summary>
        public int forgeTemperedCards;

        /// <summary>Il primo potenziamento comprato dal mercante non si paga.</summary>
        public bool firstMerchantUpgradeFree;

        /// <summary>
        /// Sconto percentuale su tutte le soglie di livello della run. E' rimasto l'unico
        /// campo che tocca la progressione: quattro nodi che spingevano sulla stessa leva
        /// accorciavano la run invece di cambiarla.
        /// </summary>
        public int masteryThresholdPercent;

        /// <summary>"Concentrazione": mana recuperato entrando in ogni stanza nuova.</summary>
        public int roomChangeMana;

        /// <summary>"Riserva": quanto si alza il tetto della riserva di mana (10 di base).</summary>
        public int bonusMaximumMana;

        /// <summary>
        /// "Trance": la prima abilita' <em>base</em> di ogni stanza non costa mana. Le
        /// supreme restano a prezzo pieno: si pagano su un percorso separato.
        /// </summary>
        public bool firstAbilityFreeEachRoom;

        /// <summary>Bonus al primo tiro di vigore contro miniboss e boss.</summary>
        public int bossVigorBonus;

        /// <summary>Consumabili in piu' consegnati da ogni stanza bottino.</summary>
        public int extraLootItems;

        /// <summary>
        /// "Secondo fiato": la prima pedina che cade in una run non finisce al cimitero, ma
        /// torna nel mazzo. Prima questo campo copriva il primo recupero al mercato - stessa
        /// leva del nodo "Recupero", che pero' costa la meta' e sconta tutti i recuperi.
        /// </summary>
        public bool savesFirstFallenCard;
    }
}
