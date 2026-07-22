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
        public RoomReward(int roomExperience, int defeatedMonsterExperience, int levelsGained)
        {
            RoomExperience = roomExperience;
            DefeatedMonsterExperience = defeatedMonsterExperience;
            LevelsGained = levelsGained;
        }

        public int RoomExperience { get; }
        public int DefeatedMonsterExperience { get; }
        public int TotalExperience => RoomExperience + DefeatedMonsterExperience;
        public int LevelsGained { get; }
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
        public int RoomsCleared { get; private set; }
        public int MasterLevel => Math.Min(maximumLevel, Math.Max(PlayerLevel, 1 + RoomsCleared / roomsPerMasterLevel));
        public int PlayerVigorDieSides => vigorDiceByLevel[PlayerLevel - 1];
        public int MasterVigorDieSides => vigorDiceByLevel[MasterLevel - 1];
        public int ExperienceToNextLevel => PlayerLevel >= maximumLevel ? 0 : ExperiencePerLevel - CurrentExperience;
        public int ExperiencePerLevel => PlayerLevel >= maximumLevel ? 0 : experienceThresholdsByLevel[PlayerLevel - 1];

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

            return CompleteRoom(roomClearExperience, defeatedExperience, experienceMultiplier);
        }

        // Il miniboss premia una cifra fissa: niente esperienza stanza, forza dei mostri o bonus.
        public RoomReward CompleteMinibossRoom(int experienceReward)
        {
            return CompleteMinibossRoom(experienceReward, 1);
        }

        public RoomReward CompleteMinibossRoom(int experienceReward, int experienceMultiplier)
        {
            if (experienceReward < 0) throw new ArgumentOutOfRangeException(nameof(experienceReward));
            return CompleteRoom(experienceReward, 0, experienceMultiplier);
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

        private RoomReward CompleteRoom(int roomExperience, int defeatedExperience, int experienceMultiplier)
        {
            experienceMultiplier = Math.Max(1, experienceMultiplier);
            roomExperience *= experienceMultiplier;
            defeatedExperience *= experienceMultiplier;
            int gained = roomExperience + defeatedExperience;
            int levelsGained = AddExperience(gained);
            RoomsCleared++;
            return new RoomReward(roomExperience, defeatedExperience, levelsGained);
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

        /// <summary>
        /// Ripristina i contatori da uno stato salvato (save/resume della run). La
        /// configurazione (soglie, dadi, livello massimo) resta quella del costruttore.
        /// </summary>
        public void RestoreProgress(int playerLevel, int currentExperience, int totalExperience,
            int availableExperience, int roomsCleared)
        {
            if (playerLevel < 1 || playerLevel > maximumLevel)
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (currentExperience < 0) throw new ArgumentOutOfRangeException(nameof(currentExperience));
            if (totalExperience < 0) throw new ArgumentOutOfRangeException(nameof(totalExperience));
            if (availableExperience < 0) throw new ArgumentOutOfRangeException(nameof(availableExperience));
            if (roomsCleared < 0) throw new ArgumentOutOfRangeException(nameof(roomsCleared));

            PlayerLevel = playerLevel;
            // Al livello massimo l'invariante della classe tiene CurrentExperience a 0.
            CurrentExperience = playerLevel >= maximumLevel ? 0 : currentExperience;
            TotalExperience = totalExperience;
            AvailableExperience = availableExperience;
            RoomsCleared = roomsCleared;
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
}
