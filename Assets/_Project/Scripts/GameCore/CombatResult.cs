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
            if (defeatedMonsterStrengths == null) throw new ArgumentNullException(nameof(defeatedMonsterStrengths));
            if (baseExperience < 0) throw new ArgumentOutOfRangeException(nameof(baseExperience));
            int defeatedExperience = 0;
            foreach (int strength in defeatedMonsterStrengths)
                defeatedExperience += Math.Max(0, strength);
            int gold = MerchantEconomy.MonsterRoomGold(RoomsCleared + 1, baseExperience, defeatedExperience);
            return CompleteRoom(baseExperience, defeatedExperience, experienceMultiplier, gold);
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

        public RoomReward CompleteNonCombatRoom(int experienceReward)
        {
            return CompleteNonCombatRoom(experienceReward, 1);
        }

        public RoomReward CompleteNonCombatRoom(int experienceReward, int experienceMultiplier)
        {
            if (experienceReward < 0) throw new ArgumentOutOfRangeException(nameof(experienceReward));
            return CompleteRoom(experienceReward, 0, experienceMultiplier);
        }

        private RoomReward CompleteRoom(int roomExperience, int defeatedExperience, int experienceMultiplier, int goldReward = 0)
        {
            experienceMultiplier = Math.Max(1, experienceMultiplier);
            roomExperience *= experienceMultiplier;
            defeatedExperience *= experienceMultiplier;
            int gained = roomExperience + defeatedExperience;
            int levelsGained = AddExperience(gained);
            AddGold(goldReward);
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
            int diceRolled = 0, int abilitiesUsed = 0, int gold = 0)
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
            return CeilPercentage(baseCost, RoomBandPercentages[RoomBand(roomsCleared)]);
        }

        public static int ApplyCaravanTax(int cost, int purchasesThisVisit)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (purchasesThisVisit < 0) throw new ArgumentOutOfRangeException(nameof(purchasesThisVisit));
            decimal multiplier = 1m;
            for (int purchase = 0; purchase < purchasesThisVisit; purchase++) multiplier *= 1.5m;
            return (int)Math.Ceiling(cost * multiplier);
        }

        public static int CardCost(int strength, int roomsCleared) =>
            ScaleByRoom(12 + Math.Max(0, strength) * 3, roomsCleared);

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
