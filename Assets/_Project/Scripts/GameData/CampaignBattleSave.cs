using System;
using System.Collections.Generic;

namespace AccardND.GameData
{
    /// <summary>
    /// Una pedina in campo, con tutto quello che si porta dietro a metà battaglia.
    /// Rispecchia BattleCardState del controller: quando lì nasce un campo di stato,
    /// deve nascere anche qui, o riprendere la partita lo perde per strada.
    ///
    /// Le viste non ci sono e non devono esserci: quelle si ricostruiscono.
    /// </summary>
    [Serializable]
    public sealed class CampaignBattlePawnSave
    {
        /// <summary>Nessun riferimento: MarkedTarget, ProtectedAlly e AttachedTo vuoti valgono questo.</summary>
        public const int NoPawn = int.MinValue;

        public string definitionId;

        /// <summary>Carta del mazzo di campagna a cui appartiene la pedina, 0 se non ne ha.</summary>
        public int campaignInstanceId;

        /// <summary>
        /// Forza della carta da combattimento. Si salva perché non sempre coincide con
        /// quella della definizione: Seraphel si trasforma a metà scontro e la sua pedina
        /// continua con forza e classe nuove.
        /// </summary>
        public int combatStrength;

        public int initiative;
        public int initiativeTalentBonus;
        public bool opensTheFight;
        public int tieBreaker;
        public bool eliminated;

        public bool abilityArmed;
        public bool abilityUsed;
        public bool abilityUsedThisTurn;
        public bool supremeUsedThisTurn;

        public int pendingAttackBonus;
        public int pendingAttackBonusKind;
        public int permanentCombatBonus;
        public int mightAuraCombatBonus;

        public int inhibitedTurns;
        public bool wasInhibited;
        public int pendingVigorStepPenalty;

        public bool isSpirit;
        public int revivedRound;
        public bool isAttachment;
        public bool hasEquipment;
        public bool isUntargetable;
        public int necromancerMinions;
        public bool petrified;
        public int seraphelSeals;

        // Riferimenti ad altre pedine, codificati con CampaignBattleSave.EncodePawn.
        public int markedTarget = NoPawn;
        public List<int> hunterMarkedTargets = new List<int>();
        public int protectedAlly = NoPawn;
        public int attachedTo = NoPawn;
    }

    /// <summary>Una forma del golem, con il bonus di potenza che ha accumulato.</summary>
    [Serializable]
    public sealed class CampaignGolemFormSave
    {
        public int form;
        public int basePower;
        public int powerBonus;
        public int vigorDieSides;
    }

    /// <summary>
    /// Il boss della stanza, se c'è. Un solo tipo per volta: il campo <see cref="kind"/>
    /// dice quale, e i campi che non gli appartengono restano a zero.
    /// </summary>
    [Serializable]
    public sealed class CampaignBattleBossSave
    {
        public const string None = "";
        public const string Golem = "golem";
        public const string Medusa = "medusa";
        public const string Seraphel = "seraphel";
        public const string Trentor = "trentor";
        public const string Bragus = "bragus";
        public const string Palatir = "palatir";

        public string kind = None;
        public int maxHitPoints;
        public int hitPoints;

        /// <summary>Trentor: i turni già giocati decidono cosa fa al prossimo.</summary>
        public int turnsTaken;

        /// <summary>Seraphel: una volta scattata resta scattata, anche se poi si cura.</summary>
        public bool phaseTwo;

        // Golem.
        public int activeForm;
        public int roundsInActiveForm;
        public bool hasInitiative;
        public int initiative;
        public int roundsPerForm;
        public List<CampaignGolemFormSave> forms = new List<CampaignGolemFormSave>();
    }

    /// <summary>Supreme già usate da una classe: alimentano il sovrapprezzo di ripetizione.</summary>
    [Serializable]
    public sealed class CampaignSupremeUseSave
    {
        public string heroClass;
        public int uses;
    }

    /// <summary>Una riserva di mana: quanto c'è e quali supreme sono già state spese.</summary>
    [Serializable]
    public sealed class CampaignManaSave
    {
        public int current;
        public List<CampaignSupremeUseSave> supremeUses = new List<CampaignSupremeUseSave>();
    }

    /// <summary>
    /// Fotografia di una battaglia in corso, presa al confine fra due turni - l'unico
    /// istante in cui non c'è niente a mezz'aria: nessun dado che rotola, nessuna
    /// animazione a metà, nessun bersaglio da scegliere.
    ///
    /// Serve a mantenere una promessa semplice: riaprire il gioco ti rimette dov'eri,
    /// con le stesse pedine schierate, gli stessi HP del boss, i morti che restano
    /// morti, il mana giusto e i buff ancora attivi.
    /// </summary>
    [Serializable]
    public sealed class CampaignBattleSave
    {
        /// <summary>
        /// Come si indica una pedina dentro lo snapshot: gli alleati con il loro indice,
        /// i nemici con il complemento negativo. Un intero solo, e lo zero non è ambiguo.
        /// </summary>
        public static int EncodePawn(bool belongsToPlayer, int index) =>
            belongsToPlayer ? index : -(index + 1);

        public static bool DecodeBelongsToPlayer(int encoded) => encoded >= 0;

        public static int DecodeIndex(int encoded) => encoded >= 0 ? encoded : -encoded - 1;

        /// <summary>
        /// Zero di proposito: JsonUtility, davanti a un salvataggio senza battaglia,
        /// ricostruisce comunque un oggetto vuoto con i valori di default. Partendo da
        /// zero quell'oggetto si riconosce per quello che è, "nessuna battaglia".
        /// </summary>
        public int roundNumber;

        public int currentTurnIndex;
        public int roomType;

        public List<CampaignBattlePawnSave> playerPawns = new List<CampaignBattlePawnSave>();
        public List<CampaignBattlePawnSave> cpuPawns = new List<CampaignBattlePawnSave>();

        /// <summary>La timeline nell'ordine giocato, pedine codificate con <see cref="EncodePawn"/>.</summary>
        public List<int> turnOrder = new List<int>();

        /// <summary>
        /// Le formazioni di partenza della stanza: servono a "riprova stanza", che rimonta
        /// il campo com'era prima che cominciasse.
        /// </summary>
        public List<string> initialPlayerFormation = new List<string>();
        public List<int> initialPlayerCampaignInstances = new List<int>();
        public List<string> initialCpuFormation = new List<string>();

        public int playerAura;
        public int cpuAura;
        public bool formationAuraUsed;
        public bool necromancerSpiritUsed;

        // Regole a colpo singolo accese prima del combattimento (bottino, talenti, eventi).
        public bool skipNextCombatCooldown;
        public bool nextCombatFallenHeroesGrantExperience;
        public bool nextCombatAssassinsActLast;
        public bool nextCombatWarriorsLowerVigor;
        public bool nextCombatTankDuel;

        public CampaignBattleBossSave boss = new CampaignBattleBossSave();

        public CampaignManaSave playerMana = new CampaignManaSave();
        public CampaignManaSave cpuMana = new CampaignManaSave();

        /// <summary>Pedine che hanno già fruttato mana cadendo: non devono fruttarlo due volte.</summary>
        public List<int> manaEliminations = new List<int>();

        /// <summary>Pedine che hanno già pagato la loro abilità primaria in questa stanza.</summary>
        public List<int> paidPrimaryAbilities = new List<int>();

        public bool freePrimaryAbilityAvailable;

        /// <summary>
        /// Dove sono arrivati i due flussi di dadi (quello della partita e quello delle
        /// decisioni della CPU). Ripartire da capo avrebbe reso il salvataggio un pulsante
        /// "ritira i dadi": basta chiudere l'app davanti a un turno andato male.
        /// </summary>
        public int randomSeed;
        public int randomDraws;
        public int cpuRandomSeed;
        public int cpuRandomDraws;
    }
}
