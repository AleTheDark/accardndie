using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    public sealed class CombatResult
    {
        public CombatResult(
            VigorRollResult attackerRoll,
            VigorRollResult defenderRoll,
            int attackerTotal,
            int defenderTotal)
        {
            AttackerRoll = attackerRoll;
            DefenderRoll = defenderRoll;
            AttackerTotal = attackerTotal;
            DefenderTotal = defenderTotal;
        }

        public VigorRollResult AttackerRoll { get; }
        public VigorRollResult DefenderRoll { get; }
        public int AttackerVigor => AttackerRoll.SelectedRoll;
        public int DefenderVigor => DefenderRoll.SelectedRoll;
        public int AttackerTotal { get; }
        public int DefenderTotal { get; }

        // In caso di parita, come nel manuale, vince la difesa.
        public bool DefenderIsDefeated => AttackerTotal > DefenderTotal;
    }

    public readonly struct RoomReward
    {
        public RoomReward(int roomExperience, int defeatedMonsterExperience, int levelsGained, int gold = 0)
        {
            RoomExperience = roomExperience;
            DefeatedMonsterExperience = defeatedMonsterExperience;
            LevelsGained = levelsGained;
            Gold = gold;
        }

        public int RoomExperience { get; }
        public int DefeatedMonsterExperience { get; }
        public int TotalExperience => RoomExperience + DefeatedMonsterExperience;
        public int LevelsGained { get; }
        public int Gold { get; }
    }

    public sealed class RunProgressState
    {
        private readonly int[] experienceThresholdsByLevel;
        private readonly int roomClearExperience;
        private readonly int maximumLevel;
        private readonly int roomsPerMasterLevel;
        private readonly int[] vigorDiceByLevel;

        public RunProgressState(int experiencePerLevel, int roomClearExperience, int maximumLevel,
            int roomsPerMasterLevel, IReadOnlyList<int> vigorDiceByLevel)
            : this(BuildRepeatedExperienceThresholds(experiencePerLevel, maximumLevel), roomClearExperience,
                maximumLevel, roomsPerMasterLevel, vigorDiceByLevel)
        {
        }

        public RunProgressState(IReadOnlyList<int> experienceThresholdsByLevel, int roomClearExperience, int maximumLevel,
            int roomsPerMasterLevel, IReadOnlyList<int> vigorDiceByLevel)
        {
            if (experienceThresholdsByLevel == null)
                throw new ArgumentNullException(nameof(experienceThresholdsByLevel));
            if (roomClearExperience < 0) throw new ArgumentOutOfRangeException(nameof(roomClearExperience));
            if (maximumLevel < 1) throw new ArgumentOutOfRangeException(nameof(maximumLevel));
            if (roomsPerMasterLevel < 1) throw new ArgumentOutOfRangeException(nameof(roomsPerMasterLevel));
            if (vigorDiceByLevel == null || vigorDiceByLevel.Count < maximumLevel)
                throw new ArgumentException("Serve un dado vigore per ogni livello.", nameof(vigorDiceByLevel));

            int thresholdCount = Math.Max(0, maximumLevel - 1);
            if (experienceThresholdsByLevel.Count < thresholdCount)
                throw new ArgumentException("Serve una soglia esperienza per ogni passaggio di livello.", nameof(experienceThresholdsByLevel));

            this.experienceThresholdsByLevel = new int[thresholdCount];
            for (int index = 0; index < thresholdCount; index++)
            {
                int threshold = experienceThresholdsByLevel[index];
                if (threshold < 1)
                    throw new ArgumentOutOfRangeException(nameof(experienceThresholdsByLevel));
                this.experienceThresholdsByLevel[index] = threshold;
            }

            this.roomClearExperience = roomClearExperience;
            this.maximumLevel = maximumLevel;
            this.roomsPerMasterLevel = roomsPerMasterLevel;
            this.vigorDiceByLevel = new int[maximumLevel];
            for (int index = 0; index < maximumLevel; index++)
                this.vigorDiceByLevel[index] = vigorDiceByLevel[index];
        }

        public int PlayerLevel { get; private set; } = 1;
        public int CurrentExperience { get; private set; }
        public int TotalExperience { get; private set; }
        public int AvailableExperience { get; private set; }
        public int Gold { get; private set; }
        public int RoomsCleared { get; private set; }

        /// <summary>Nemici eliminati nella run. Alimenta i contatori di progressione permanente.</summary>
        public int EnemiesDefeated { get; private set; }

        /// <summary>Miniboss sconfitti nella run.</summary>
        public int MinibossesDefeated { get; private set; }

        /// <summary>
        /// Dadi tirati nella run: un tiro con due dadi ne conta due. Alimenta le quest della
        /// taverna, che sono l'unica fonte di miele.
        /// </summary>
        public int DiceRolled { get; private set; }

        /// <summary>Abilita' di classe attivate dalle pedine del giocatore nella run.</summary>
        public int AbilitiesUsed { get; private set; }

        /// <summary>
        /// Consumabili usati nella run, da qualunque parte arrivino: bisaccia, bottino delle
        /// stanze o acquisto al mercante. La bisaccia si conta a parte (per scalare la scorta),
        /// ma per le quest della taverna un oggetto usato e' un oggetto usato.
        /// </summary>
        public int ItemsUsed { get; private set; }

        /// <summary>Supreme attivate dalle pedine del giocatore nella run.</summary>
        public int SupremesUsed { get; private set; }

        /// <summary>Sfide veloci portate a termine: la rinuncia non conta.</summary>
        public int QuickChallengesCompleted { get; private set; }

        /// <summary>Acquisti conclusi al mercante, carte e oggetti insieme.</summary>
        public int MerchantPurchases { get; private set; }

        /// <summary>
        /// Oro guadagnato avventurandosi (stanze e prove lampo). Vendite e rimborsi del
        /// mercante non contano: sono spostamenti di oro gia' guadagnato, e farli contare
        /// renderebbe la quest completabile comprando e rivendendo la stessa carta.
        /// </summary>
        public int GoldEarned { get; private set; }

        /// <summary>Livelli guadagnati nella run, sommati anche fra piu' passaggi nella stessa stanza.</summary>
        public int LevelsGained { get; private set; }
        public int MasterLevel => Math.Min(maximumLevel, Math.Max(PlayerLevel, 1 + RoomsCleared / roomsPerMasterLevel));
        public int PlayerVigorDieSides => vigorDiceByLevel[PlayerLevel - 1];
        public int MasterVigorDieSides => vigorDiceByLevel[MasterLevel - 1];
        public int ExperienceToNextLevel => PlayerLevel >= maximumLevel ? 0 : ExperiencePerLevel - CurrentExperience;
        public int ExperiencePerLevel => PlayerLevel >= maximumLevel ? 0 : experienceThresholdsByLevel[PlayerLevel - 1];

        public int GetExperienceThresholdForLevel(int level)
        {
            if (level < 1 || level >= maximumLevel)
                return 0;
            return experienceThresholdsByLevel[level - 1];
        }

        public RoomReward CompleteMonsterRoom(IEnumerable<int> defeatedMonsterStrengths)
        {
            return CompleteMonsterRoom(defeatedMonsterStrengths, 1);
        }

        public RoomReward CompleteMonsterRoom(IEnumerable<int> defeatedMonsterStrengths, int experienceMultiplier)
        {
            if (defeatedMonsterStrengths == null) throw new ArgumentNullException(nameof(defeatedMonsterStrengths));
            int defeatedExperience = 0;
            foreach (int strength in defeatedMonsterStrengths)
                defeatedExperience += Math.Max(0, strength);

            int gold = MerchantEconomy.MonsterRoomGold(
                RoomsCleared + 1, roomClearExperience, defeatedExperience);
            return CompleteRoom(roomClearExperience, defeatedExperience, experienceMultiplier, gold);
        }

        public RoomReward CompleteMonsterRoom(IEnumerable<int> defeatedMonsterStrengths, int baseExperience, int experienceMultiplier)
        {
            return CompleteMonsterRoom(defeatedMonsterStrengths, baseExperience, experienceMultiplier, 1);
        }

        public RoomReward CompleteMonsterRoom(IEnumerable<int> defeatedMonsterStrengths, int baseExperience,
            int experienceMultiplier, int rewardDivisor)
        {
            if (defeatedMonsterStrengths == null) throw new ArgumentNullException(nameof(defeatedMonsterStrengths));
            if (baseExperience < 0) throw new ArgumentOutOfRangeException(nameof(baseExperience));
            if (rewardDivisor < 1) throw new ArgumentOutOfRangeException(nameof(rewardDivisor));
            int defeatedExperience = 0;
            foreach (int strength in defeatedMonsterStrengths)
                defeatedExperience += Math.Max(0, strength);
            int gold = MerchantEconomy.MonsterRoomGold(RoomsCleared + 1, baseExperience, defeatedExperience);
            return CompleteRoom(baseExperience, defeatedExperience, experienceMultiplier, gold, rewardDivisor);
        }

        // Il miniboss premia una cifra fissa: niente esperienza stanza, forza dei mostri o bonus.
        public RoomReward CompleteMinibossRoom(int experienceReward)
        {
            return CompleteMinibossRoom(experienceReward, 1);
        }

        public RoomReward CompleteMinibossRoom(int experienceReward, int experienceMultiplier)
        {
            if (experienceReward < 0) throw new ArgumentOutOfRangeException(nameof(experienceReward));
            MinibossesDefeated++;
            return CompleteRoom(experienceReward, 0, experienceMultiplier,
                MerchantEconomy.MinibossGold(RoomsCleared + 1));
        }

        /// <summary>
        /// Registra i nemici eliminati in una stanza. Separato da <see cref="CompleteMonsterRoom"/>
        /// perche' quella riceve le forze dei caduti, a cui il chiamante puo' accodare voci
        /// aggregate (bonus eroi caduti): contarne gli elementi darebbe un totale gonfiato.
        /// </summary>
        public void RecordEnemiesDefeated(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            EnemiesDefeated += count;
        }

        /// <summary>Registra dadi lanciati (uno per faccia, quindi due per un tiro doppio).</summary>
        public void RecordDiceRolled(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            DiceRolled += count;
        }

        /// <summary>Registra un'abilita' di classe attivata da una pedina del giocatore.</summary>
        public void RecordAbilityUsed()
        {
            AbilitiesUsed++;
        }

        /// <summary>Registra un consumabile usato, indipendentemente dalla sua provenienza.</summary>
        public void RecordItemUsed()
        {
            ItemsUsed++;
        }

        /// <summary>Registra una suprema attivata da una pedina del giocatore.</summary>
        public void RecordSupremeUsed()
        {
            SupremesUsed++;
        }

        /// <summary>Registra una Sfida veloce portata a termine (la rinuncia non la chiama).</summary>
        public void RecordQuickChallengeCompleted()
        {
            QuickChallengesCompleted++;
        }

        /// <summary>Registra un acquisto concluso al mercante, carta o oggetto che sia.</summary>
        public void RecordMerchantPurchase()
        {
            MerchantPurchases++;
        }

        /// <summary>
        /// Segna oro guadagnato dall'avventura. Separato da <see cref="AddGold"/> perche'
        /// quello serve anche a restituire oro (vendite, rimborsi di un acquisto fallito):
        /// contarli sarebbe un rubinetto aperto verso le quest della taverna.
        /// </summary>
        public void RecordGoldEarned(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            GoldEarned += amount;
        }

        public RoomReward CompleteNonCombatRoom(int experienceReward)
        {
            return CompleteNonCombatRoom(experienceReward, 1);
        }

        public RoomReward CompleteNonCombatRoom(int experienceReward, int experienceMultiplier)
        {
            if (experienceReward < 0) throw new ArgumentOutOfRangeException(nameof(experienceReward));
            return CompleteRoom(experienceReward, 0, experienceMultiplier);
        }

        private RoomReward CompleteRoom(int roomExperience, int defeatedExperience, int experienceMultiplier,
            int goldReward = 0, int rewardDivisor = 1)
        {
            experienceMultiplier = Math.Max(1, experienceMultiplier);
            rewardDivisor = Math.Max(1, rewardDivisor);
            roomExperience *= experienceMultiplier;
            defeatedExperience *= experienceMultiplier;
            roomExperience /= rewardDivisor;
            defeatedExperience /= rewardDivisor;
            goldReward /= rewardDivisor;
            int gained = roomExperience + defeatedExperience;
            int levelsGained = AddExperience(gained);
            AddGold(goldReward);
            RecordGoldEarned(goldReward);
            RoomsCleared++;
            return new RoomReward(roomExperience, defeatedExperience, levelsGained, goldReward);
        }

        public int AddExperience(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int previousLevel = PlayerLevel;
            TotalExperience += amount;
            AvailableExperience += amount;
            CurrentExperience += amount;
            while (PlayerLevel < maximumLevel && CurrentExperience >= ExperiencePerLevel)
            {
                CurrentExperience -= ExperiencePerLevel;
                PlayerLevel++;
            }
            if (PlayerLevel >= maximumLevel)
                CurrentExperience = 0;
            LevelsGained += PlayerLevel - previousLevel;
            return PlayerLevel - previousLevel;
        }

        public bool TrySpendExperience(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > AvailableExperience)
                return false;
            AvailableExperience -= amount;
            return true;
        }

        public void AddSpendableExperience(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            AvailableExperience += amount;
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Gold) return false;
            Gold -= amount;
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Gold += amount;
        }

        /// <summary>
        /// Ripristina i contatori da uno stato salvato (save/resume della run). La
        /// configurazione (soglie, dadi, livello massimo) resta quella del costruttore.
        /// </summary>
        public void RestoreProgress(int playerLevel, int currentExperience, int totalExperience,
            int availableExperience, int roomsCleared, int enemiesDefeated = 0, int minibossesDefeated = 0,
            int diceRolled = 0, int abilitiesUsed = 0, int gold = 0, int supremesUsed = 0,
            int quickChallengesCompleted = 0, int merchantPurchases = 0, int goldEarned = 0,
            int levelsGained = 0, int itemsUsed = 0)
        {
            if (playerLevel < 1 || playerLevel > maximumLevel)
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (currentExperience < 0) throw new ArgumentOutOfRangeException(nameof(currentExperience));
            if (totalExperience < 0) throw new ArgumentOutOfRangeException(nameof(totalExperience));
            if (availableExperience < 0) throw new ArgumentOutOfRangeException(nameof(availableExperience));
            if (roomsCleared < 0) throw new ArgumentOutOfRangeException(nameof(roomsCleared));
            if (enemiesDefeated < 0) throw new ArgumentOutOfRangeException(nameof(enemiesDefeated));
            if (minibossesDefeated < 0) throw new ArgumentOutOfRangeException(nameof(minibossesDefeated));
            if (diceRolled < 0) throw new ArgumentOutOfRangeException(nameof(diceRolled));
            if (abilitiesUsed < 0) throw new ArgumentOutOfRangeException(nameof(abilitiesUsed));
            if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold));
            if (supremesUsed < 0) throw new ArgumentOutOfRangeException(nameof(supremesUsed));
            if (quickChallengesCompleted < 0) throw new ArgumentOutOfRangeException(nameof(quickChallengesCompleted));
            if (merchantPurchases < 0) throw new ArgumentOutOfRangeException(nameof(merchantPurchases));
            if (goldEarned < 0) throw new ArgumentOutOfRangeException(nameof(goldEarned));
            if (levelsGained < 0) throw new ArgumentOutOfRangeException(nameof(levelsGained));
            if (itemsUsed < 0) throw new ArgumentOutOfRangeException(nameof(itemsUsed));

            PlayerLevel = playerLevel;
            // Al livello massimo l'invariante della classe tiene CurrentExperience a 0.
            CurrentExperience = playerLevel >= maximumLevel ? 0 : currentExperience;
            TotalExperience = totalExperience;
            AvailableExperience = availableExperience;
            RoomsCleared = roomsCleared;
            EnemiesDefeated = enemiesDefeated;
            MinibossesDefeated = minibossesDefeated;
            DiceRolled = diceRolled;
            AbilitiesUsed = abilitiesUsed;
            Gold = gold;
            SupremesUsed = supremesUsed;
            QuickChallengesCompleted = quickChallengesCompleted;
            MerchantPurchases = merchantPurchases;
            GoldEarned = goldEarned;
            LevelsGained = levelsGained;
            ItemsUsed = itemsUsed;
        }

        private static int[] BuildRepeatedExperienceThresholds(int experiencePerLevel, int maximumLevel)
        {
            if (experiencePerLevel < 1) throw new ArgumentOutOfRangeException(nameof(experiencePerLevel));

            int count = Math.Max(0, maximumLevel - 1);
            int[] thresholds = new int[count];
            for (int index = 0; index < count; index++)
                thresholds[index] = experiencePerLevel;
            return thresholds;
        }
    }

    /// <summary>Regole pure e testabili dell'economia interna di una run.</summary>
    public static class MerchantEconomy
    {
        private static readonly int[] RoomBandPercentages = { 100, 125, 155, 190, 230 };

        public static int RoomBand(int roomsCleared) => Math.Min(4, Math.Max(0, roomsCleared) / 5);

        public static int ScaleByRoom(int baseCost, int roomsCleared)
        {
            if (baseCost < 0) throw new ArgumentOutOfRangeException(nameof(baseCost));
            return baseCost;
        }

        public static int ApplyCaravanTax(int cost, int purchasesThisVisit)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (purchasesThisVisit < 0) throw new ArgumentOutOfRangeException(nameof(purchasesThisVisit));
            return cost;
        }

        public static int CardCost(int strength, int roomsCleared)
        {
            int value = Math.Max(0, strength);
            if (value <= 3) return 12;
            if (value <= 6) return 18;
            if (value <= 9) return 26;
            return 36;
        }

        public static int UpgradeCost(int currentStrength, int upgradesAlreadyBought)
        {
            if (upgradesAlreadyBought < 0) throw new ArgumentOutOfRangeException(nameof(upgradesAlreadyBought));
            int value = Math.Max(0, currentStrength);
            int baseCost = value <= 3 ? 14 : value <= 6 ? 20 : value <= 9 ? 28 : 38;
            return upgradesAlreadyBought == 0 ? baseCost : CeilPercentage(baseCost, 150);
        }

        public static int RecoveryCost(int strength, int roomsCleared) =>
            Math.Max(3, CeilPercentage(CardCost(strength, roomsCleared), 70));

        public static int MonsterRoomGold(int roomNumber, int baseExperience, int defeatedPower)
        {
            int band = RoomBand(Math.Max(0, roomNumber - 1));
            int difficultyBonus = baseExperience >= 15 ? 5 : baseExperience <= 5 ? 0 : 2;
            int formationBonus = Math.Min(5, Math.Max(0, defeatedPower) / 10);
            return 6 + band * 2 + difficultyBonus + formationBonus;
        }

        public static int MinibossGold(int roomNumber)
        {
            int band = RoomBand(Math.Max(0, roomNumber - 1));
            return 24 + band * 4;
        }

        private static int CeilPercentage(int value, int percentage) => (value * percentage + 99) / 100;
    }
}
