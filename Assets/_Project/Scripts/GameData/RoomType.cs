namespace AccardND.GameData
{
    public enum RoomType
    {
        Any,
        Monster,
        Boss,
        Merchant,
        Loot,
        UnexpectedOpportunity
    }

    public enum RoomDifficulty
    {
        Any,
        Easy,
        Normal,
        Hard
    }

    /// <summary>Regole autorevoli delle difficolta' mostro; i nomi enum storici preservano gli asset Unity.</summary>
    public readonly struct RoomDifficultyRules
    {
        public string DisplayName { get; }
        public int MinimumFormationPower { get; }
        public int MaximumFormationPower { get; }
        public int BaseExperience { get; }
        public bool CpuUsesAbilities { get; }
        public bool CpuUsesMana { get; }
        public bool CpuUsesAttachments { get; }
        public bool CpuCanSkip { get; }
        public bool GrantsLootStyleCard { get; }

        private RoomDifficultyRules(string name, int minPower, int maxPower, int baseExperience,
            bool abilities, bool mana, bool attachments, bool canSkip, bool lootCard)
        {
            DisplayName = name;
            MinimumFormationPower = minPower;
            MaximumFormationPower = maxPower;
            BaseExperience = baseExperience;
            CpuUsesAbilities = abilities;
            CpuUsesMana = mana;
            CpuUsesAttachments = attachments;
            CpuCanSkip = canSkip;
            GrantsLootStyleCard = lootCard;
        }

        public static RoomDifficultyRules For(RoomDifficulty difficulty)
        {
            switch (difficulty)
            {
                case RoomDifficulty.Easy:
                    return new RoomDifficultyRules("Accessibile", 6, 20, 5, false, false, false, false, false);
                case RoomDifficulty.Hard:
                    return new RoomDifficultyRules("Diabolica", 24, 30, 15, true, true, true, true, true);
                default:
                    return new RoomDifficultyRules("Normale", 6, 25, 10, true, true, true, true, false);
            }
        }
    }

    /// <summary>Pesi di estrazione delle stanze mostro per scenario e fascia della run.</summary>
    public readonly struct ScenarioMonsterDifficultyWeights
    {
        public int Accessible { get; }
        public int Normal { get; }
        public int Diabolic { get; }
        public int Total => Accessible + Normal + Diabolic;

        private ScenarioMonsterDifficultyWeights(int accessible, int normal, int diabolic)
        {
            Accessible = accessible;
            Normal = normal;
            Diabolic = diabolic;
        }

        public static ScenarioMonsterDifficultyWeights For(int scenarioNumber, int roomNumber)
        {
            int scenario = scenarioNumber < 1 ? 1 : (scenarioNumber > 9 ? 9 : scenarioNumber);
            bool secondHalf = roomNumber >= 11;
            if (!secondHalf)
            {
                switch (scenario)
                {
                    case 1: return new ScenarioMonsterDifficultyWeights(60, 40, 0);
                    case 2: return new ScenarioMonsterDifficultyWeights(50, 50, 0);
                    case 3: return new ScenarioMonsterDifficultyWeights(40, 60, 0);
                    case 4: return new ScenarioMonsterDifficultyWeights(30, 65, 5);
                    case 5: return new ScenarioMonsterDifficultyWeights(20, 70, 10);
                    case 6: return new ScenarioMonsterDifficultyWeights(10, 80, 10);
                    case 7: return new ScenarioMonsterDifficultyWeights(0, 90, 10);
                    case 8: return new ScenarioMonsterDifficultyWeights(0, 80, 20);
                    default: return new ScenarioMonsterDifficultyWeights(0, 70, 30);
                }
            }

            switch (scenario)
            {
                case 1: return new ScenarioMonsterDifficultyWeights(25, 55, 20);
                case 2: return new ScenarioMonsterDifficultyWeights(25, 50, 30);
                case 3: return new ScenarioMonsterDifficultyWeights(20, 40, 40);
                case 4: return new ScenarioMonsterDifficultyWeights(15, 35, 50);
                case 5: return new ScenarioMonsterDifficultyWeights(10, 30, 60);
                case 6: return new ScenarioMonsterDifficultyWeights(5, 25, 70);
                case 7: return new ScenarioMonsterDifficultyWeights(0, 20, 80);
                case 8: return new ScenarioMonsterDifficultyWeights(0, 10, 90);
                default: return new ScenarioMonsterDifficultyWeights(0, 0, 100);
            }
        }
    }
}
