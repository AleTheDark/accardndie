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
        private readonly int experiencePerLevel;
        private readonly int roomClearExperience;
        private readonly int maximumLevel;
        private readonly int roomsPerMasterLevel;
        private readonly int[] vigorDiceByLevel;

        public RunProgressState(int experiencePerLevel, int roomClearExperience, int maximumLevel,
            int roomsPerMasterLevel, IReadOnlyList<int> vigorDiceByLevel)
        {
            if (experiencePerLevel < 1) throw new ArgumentOutOfRangeException(nameof(experiencePerLevel));
            if (roomClearExperience < 0) throw new ArgumentOutOfRangeException(nameof(roomClearExperience));
            if (maximumLevel < 1) throw new ArgumentOutOfRangeException(nameof(maximumLevel));
            if (roomsPerMasterLevel < 1) throw new ArgumentOutOfRangeException(nameof(roomsPerMasterLevel));
            if (vigorDiceByLevel == null || vigorDiceByLevel.Count < maximumLevel)
                throw new ArgumentException("Serve un dado vigore per ogni livello.", nameof(vigorDiceByLevel));

            this.experiencePerLevel = experiencePerLevel;
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
        public int ExperienceToNextLevel => PlayerLevel >= maximumLevel ? 0 : experiencePerLevel - CurrentExperience;
        public int ExperiencePerLevel => experiencePerLevel;

        public RoomReward CompleteMonsterRoom(IEnumerable<int> defeatedMonsterStrengths)
        {
            if (defeatedMonsterStrengths == null) throw new ArgumentNullException(nameof(defeatedMonsterStrengths));
            int defeatedExperience = 0;
            foreach (int strength in defeatedMonsterStrengths)
                defeatedExperience += Math.Max(0, strength);

            return CompleteRoom(roomClearExperience, defeatedExperience);
        }

        public RoomReward CompleteNonCombatRoom(int experienceReward)
        {
            if (experienceReward < 0) throw new ArgumentOutOfRangeException(nameof(experienceReward));
            return CompleteRoom(experienceReward, 0);
        }

        private RoomReward CompleteRoom(int roomExperience, int defeatedExperience)
        {
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
            while (PlayerLevel < maximumLevel && CurrentExperience >= experiencePerLevel)
            {
                CurrentExperience -= experiencePerLevel;
                PlayerLevel++;
            }
            if (PlayerLevel >= maximumLevel)
                CurrentExperience = Math.Min(CurrentExperience, experiencePerLevel);
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
    }
}
