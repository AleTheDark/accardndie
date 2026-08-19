using System;
using System.Collections.Generic;
using AccardND.GameCore;

namespace AccardND.GameData
{
    /// <summary>Una carta del mazzo di campagna in uno snapshot salvato.</summary>
    [Serializable]
    public sealed class CampaignCardSave
    {
        public string definitionId;
        public int zone;
        public int instanceId;
        public int permanentItemBonus;
        public bool hasRubySeal;
        public int merchantUpgradeCount;
    }

    /// <summary>Un consumabile posseduto in uno snapshot salvato.</summary>
    [Serializable]
    public sealed class CampaignConsumableSave
    {
        public string type;
        public int count;
    }

    /// <summary>
    /// Una delle tre porte della scelta della via, come è stata estratta. Le porte si
    /// salvano perché la scelta è già stata fatta dal caso: senza, riaprire il gioco
    /// davanti alle porte le riestrarrebbe, e il Detector appena speso avrebbe mostrato
    /// tre stanze che non esistono più.
    /// </summary>
    [Serializable]
    public sealed class CampaignDoorSave
    {
        /// <summary>true se dietro la porta si vede già cosa c'è (Detector).</summary>
        public bool revealed;

        public int roomType;
        public string scenarioId;
        public int difficulty;
    }

    /// <summary>
    /// Dove si è fermato il giocatore fra una battaglia e l'altra: davanti alle tre porte,
    /// oppure già dentro la stanza che ha scelto.
    ///
    /// Serve a una regola sola: il punto di ripresa non deve mai stare prima di una scelta
    /// già fatta. Senza, entrare in una stanza mostro e riaprire il gioco riportava alla
    /// scelta della via, e la porta si poteva cambiare finché non usciva la stanza giusta.
    /// </summary>
    [Serializable]
    public sealed class CampaignRoomStateSave
    {
        public List<CampaignDoorSave> doors = new List<CampaignDoorSave>();

        /// <summary>Lo sfondo estratto per la schermata delle porte: cambiarlo si vedrebbe.</summary>
        public int backgroundIndex = 1;

        /// <summary>true quando la porta è stata varcata: si riprende dentro la stanza.</summary>
        public bool roomEntered;

        public int roomType;
        public string scenarioId;
        public int roomDifficulty;

        /// <summary>
        /// I due flussi di dadi come stavano sulla soglia della stanza, non come stanno
        /// adesso. Una stanza ripresa si rimonta da capo: se ripartisse dai dadi correnti
        /// - avanzati da tutto quello che è successo dentro - il mercante rifarebbe la
        /// vetrina e il mostro cambierebbe formazione. Mettere in pausa il gioco tornerebbe
        /// a essere un modo di riestrarre il contenuto della stanza.
        /// </summary>
        public int entryRandomSeed;
        public int entryRandomDraws;
        public int entryCpuRandomSeed;
        public int entryCpuRandomDraws;

        /// <summary>C'è qualcosa da riprendere: le porte estratte o la stanza già aperta.</summary>
        public bool HasState => roomEntered || (doors != null && doors.Count > 0);
    }

    /// <summary>
    /// Stato serializzabile di una run di campagna (save/resume). Contiene solo dati:
    /// niente riferimenti a UnityEngine.Object, così è (de)serializzabile con JsonUtility.
    /// I punti di salvataggio sono due: la schermata "scelta della via", dove il
    /// combattimento è smontato, e il confine fra due turni di una battaglia in corso
    /// (vedi <see cref="battle"/>).
    /// </summary>
    [Serializable]
    public sealed class CampaignRunSave
    {
        /// <summary>
        /// La v2 ha aggiunto la battaglia in corso, la v3 la stanza in corso e la patch
        /// che ha scritto il salvataggio. Le versioni vecchie del formato restano leggibili,
        /// ma da adesso decide <see cref="gameVersion"/>: una run si riprende solo con la
        /// stessa patch con cui è stata giocata (vedi CampaignRunSaveService).
        /// </summary>
        public const int CurrentVersion = 3;

        /// <summary>La prima versione, senza snapshot di battaglia.</summary>
        public const int MinimumSupportedVersion = 1;

		public const int DefaultPlayerMana = 3;

        public int version = CurrentVersion;

        /// <summary>
        /// La versione del gioco che ha scritto il salvataggio (Application.version).
        /// Una run comincia con le carte, i costi e le stanze della sua patch: ripresa con
        /// un'altra riprenderebbe uno stato che quella patch non sa più leggere. Vuota nei
        /// salvataggi scritti prima della v3, che infatti non si riprendono più.
        /// </summary>
        public string gameVersion;

        // Progressione (contatori di RunProgressState)
        public int playerLevel = 1;
        public int currentExperience;
        public int totalExperience;
        public int availableExperience;
        public int gold;
        public int roomsCleared;
        public int enemiesDefeated;
        public int minibossesDefeated;
        public int diceRolled;
        public int abilitiesUsed;

        // Contatori introdotti con le quest di taverna su supreme, sfide veloci, mercante,
        // oro e livelli. Restano a zero nei save creati prima: una run ripresa parte da li'
        // invece di rifiutare il salvataggio.
        public int supremesUsed;
        public int quickChallengesCompleted;
        public int merchantPurchases;
        public int goldEarned;
        public int levelsGained;

        // Oggetti usati nella run, bisaccia e non. Nei save creati prima resta a zero: una run
        // ripresa riparte da li' invece di rifiutare il salvataggio.
        public int itemsUsed;

		// Riserva globale del giocatore. Il valore iniziale mantiene compatibili i save v1
		// creati prima dell'introduzione del mana in campagna.
		public int playerMana = DefaultPlayerMana;

        // Boss e miniboss sconfitti nella run, per i contatori di progressione permanente.
        public List<string> defeatedBossIds = new List<string>();

        // Bisaccia: cosa e' stato portato e cosa e' gia' stato usato. Serve perche' una run
        // ripresa deve ancora saper distinguere gli oggetti della bisaccia da quelli trovati.
        public List<string> runBagItemIds = new List<string>();
        public List<string> consumedBagItemIds = new List<string>();

        // Mazzo di campagna
        public List<CampaignCardSave> deck = new List<CampaignCardSave>();
        public int nextInstanceId = 1;

        // Identificativo della run lato client: e' la chiave con cui il server ha aperto la
        // riga dello storico all'avvio. Conservarlo fa si' che una run ripresa dopo un
        // riavvio chiuda la propria riga invece di crearne una seconda e lasciare la prima
        // fra le abbandonate. Vuoto sui save creati prima di questa versione.
        public string runRewardId;

        // Stato scenario / regole di stanza (popolato dal controller in fase di wiring)
        public string campaignScenarioId;
        public string campaignScenarioBossId;
        public string adventureChapterId;
        public bool merchantRoomsBlockedUntilMonster;
        public bool rewardRoomsBlockedUntilMonster;

        // Talenti una-tantum gia' spesi in questa run. Senza salvarli, uscire e riprendere
        // la run li riarmerebbe, e un talento "una volta per run" diventerebbe una volta per
        // ogni volta che si riapre il gioco.
        public bool freeMerchantUpgradeUsed;

        /// <summary>Il "Secondo fiato" ha gia' salvato una pedina in questa run.</summary>
        public bool secondWindUsed;
		public int nextMonsterDifficultyIncrease;
        public bool nextDoorChoiceRevealed;
        public bool nextMonsterRewardHalved;

        // Regole a colpo singolo armate fuori dal combattimento: le arma il bottino (le
        // opportunità) o un oggetto, e vengono spese nella stanza dopo. Finivano solo nello
        // snapshot di battaglia, quindi chiudere il gioco fra due stanze le buttava via
        // insieme all'oggetto o all'evento che le aveva concesse.
        public bool skipNextCombatCooldown;
        public bool nextCombatFallenHeroesGrantExperience;
        public bool nextCombatAssassinsActLast;
        public bool nextCombatWarriorsLowerVigor;
        public bool nextCombatTankDuel;
        public bool nextRoomEmpowered;
        public bool nextRoomDoubleExperience;

        /// <summary>
        /// Dove sono arrivati i due flussi di dadi della run (quello della partita e quello
        /// delle decisioni della CPU). È lo stesso motivo dei dadi salvati in battaglia: se
        /// una run ripresa ripartisse da dadi nuovi, riaprire il gioco sarebbe il modo più
        /// comodo di riestrarre le porte e il contenuto della stanza.
        /// </summary>
        public int randomSeed;
        public int randomDraws;
        public int cpuRandomSeed;
        public int cpuRandomDraws;

        /// <summary>
        /// La stanza in corso: le porte estratte e, se il giocatore ne ha già varcata una,
        /// quale. Null nei salvataggi scritti prima della v3.
        /// </summary>
        public CampaignRoomStateSave roomState;

        // Consumabili posseduti (mappati dal controller in fase di wiring)
        public List<CampaignConsumableSave> consumables = new List<CampaignConsumableSave>();

        /// <summary>
        /// La battaglia in corso, se il salvataggio è stato preso durante uno scontro.
        /// Null (o con <c>roundNumber</c> a zero) quando la run è ferma alla scelta della
        /// via, che resta il caso normale.
        /// </summary>
        public CampaignBattleSave battle;

        /// <summary>true se questo salvataggio riporta a metà scontro.</summary>
        public bool HasBattle => battle != null && battle.roundNumber > 0
            && (battle.playerPawns.Count > 0 || battle.cpuPawns.Count > 0);

        /// <summary>true se questo salvataggio sa dire davanti a quali porte - o dentro
        /// quale stanza - si era fermata la run.</summary>
        public bool HasRoomState => roomState != null && roomState.HasState;
    }

    /// <summary>
    /// Mappa lo stato di dominio (RunProgressState, CampaignDeckState) da/verso
    /// <see cref="CampaignRunSave"/>. Gli scalari di scenario/regole li imposta direttamente
    /// il controller sul DTO.
    /// </summary>
    public static class CampaignRunMapper
    {
        public static void WriteProgress(CampaignRunSave save, RunProgressState progress)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            save.playerLevel = progress.PlayerLevel;
            save.currentExperience = progress.CurrentExperience;
            save.totalExperience = progress.TotalExperience;
            save.availableExperience = progress.AvailableExperience;
            save.gold = progress.Gold;
            save.roomsCleared = progress.RoomsCleared;
            save.enemiesDefeated = progress.EnemiesDefeated;
            save.minibossesDefeated = progress.MinibossesDefeated;
            save.diceRolled = progress.DiceRolled;
            save.abilitiesUsed = progress.AbilitiesUsed;
            save.supremesUsed = progress.SupremesUsed;
            save.quickChallengesCompleted = progress.QuickChallengesCompleted;
            save.merchantPurchases = progress.MerchantPurchases;
            save.goldEarned = progress.GoldEarned;
            save.levelsGained = progress.LevelsGained;
            save.itemsUsed = progress.ItemsUsed;
        }

        public static void ReadProgress(CampaignRunSave save, RunProgressState progress)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            progress.RestoreProgress(save.playerLevel, save.currentExperience,
                save.totalExperience, save.availableExperience, save.roomsCleared,
                save.enemiesDefeated, save.minibossesDefeated,
                save.diceRolled, save.abilitiesUsed, save.gold,
                save.supremesUsed, save.quickChallengesCompleted, save.merchantPurchases,
                save.goldEarned, save.levelsGained, save.itemsUsed);
        }

        public static void WriteDeck(CampaignRunSave save, CampaignDeckState deck)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (deck == null) throw new ArgumentNullException(nameof(deck));

            save.deck = new List<CampaignCardSave>(deck.Cards.Count);
            foreach (CampaignCardInstance card in deck.Cards)
            {
                save.deck.Add(new CampaignCardSave
                {
                    definitionId = card.Definition.Id,
                    zone = (int)card.Zone,
                    instanceId = card.InstanceId,
                    permanentItemBonus = card.PermanentItemBonus,
                    hasRubySeal = card.HasRubySeal,
                    merchantUpgradeCount = card.MerchantUpgradeCount
                });
            }
            save.nextInstanceId = deck.NextInstanceId;
        }

        /// <summary>
        /// Ricostruisce il mazzo dallo snapshot. <paramref name="resolve"/> mappa un id carta a
        /// una CardDefinition (es. CardDatabase.FindById); le carte non più nel database vengono
        /// saltate, così un aggiornamento del gioco non rompe un salvataggio vecchio.
        /// </summary>
        public static void ReadDeck(CampaignRunSave save, CampaignDeckState deck, Func<string, CardDefinition> resolve)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));

            var entries = new List<CampaignCardRestoreEntry>(save.deck?.Count ?? 0);
            if (save.deck != null)
            {
                foreach (CampaignCardSave card in save.deck)
                {
                    CardDefinition definition = resolve(card.definitionId);
                    if (definition == null)
                        continue;
                    entries.Add(new CampaignCardRestoreEntry(
                        definition,
                        (CampaignCardZone)card.zone,
                        card.instanceId,
                        card.permanentItemBonus,
                        card.hasRubySeal,
                        card.merchantUpgradeCount));
                }
            }
            deck.RestoreFrom(entries, save.nextInstanceId);
        }
    }
}
