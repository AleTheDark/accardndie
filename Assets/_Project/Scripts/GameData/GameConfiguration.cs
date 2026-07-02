using System;
using AccardND.GameCore;
using UnityEngine;

namespace AccardND.GameData
{
    [CreateAssetMenu(menuName = "Accard N' Die/Game Configuration", fileName = "GameConfiguration")]
    public sealed class GameConfiguration : ScriptableObject
    {
        public const int CurrentSchemaVersion = 26;

        [SerializeField, HideInInspector] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private AnimationConfiguration animation = new();
        [SerializeField] private GameplayConfiguration gameplay = new();
        [SerializeField] private ResponsiveLayoutConfiguration responsiveLayout = new();
        [SerializeField] private VisualConfiguration visual = new();
        [SerializeField] private StartingRoomConfiguration startingRoom = new();
        [SerializeField] private ProgressionConfiguration progression = new();
        [SerializeField] private ClassBalanceConfiguration classBalance = new();
        [SerializeField] private LoggingConfiguration logging = new();
        [SerializeField] private DeckBuildingConfiguration deckBuilding = new();
        [SerializeField] private PvpConfiguration pvp = new();

        public int SchemaVersion => schemaVersion;
        public AnimationConfiguration Animation => animation;
        public GameplayConfiguration Gameplay => gameplay;
        public ResponsiveLayoutConfiguration ResponsiveLayout => responsiveLayout;
        public VisualConfiguration Visual => visual;
        public StartingRoomConfiguration StartingRoom => startingRoom ??= new StartingRoomConfiguration();
        public ProgressionConfiguration Progression => progression ??= new ProgressionConfiguration();
        public ClassBalanceConfiguration ClassBalance => classBalance ??= new ClassBalanceConfiguration();
        public LoggingConfiguration Logging => logging ??= new LoggingConfiguration();
        public DeckBuildingConfiguration DeckBuilding => deckBuilding ??= new DeckBuildingConfiguration();
        public PvpConfiguration Pvp => pvp ??= new PvpConfiguration();
        public bool UseRandomSeedEachSession => schemaVersion < 4 || gameplay.UseRandomSeedEachSession;

#if UNITY_EDITOR
        public void UpgradeIfNeeded()
        {
            animation ??= new AnimationConfiguration();
            gameplay ??= new GameplayConfiguration();
            responsiveLayout ??= new ResponsiveLayoutConfiguration();
            visual ??= new VisualConfiguration();
            startingRoom ??= new StartingRoomConfiguration();
            progression ??= new ProgressionConfiguration();
            classBalance ??= new ClassBalanceConfiguration();
            logging ??= new LoggingConfiguration();
            deckBuilding ??= new DeckBuildingConfiguration();
            pvp ??= new PvpConfiguration();
            if (schemaVersion < 2)
                gameplay.UpgradeToVersion2();
            if (schemaVersion < 4)
                gameplay.UpgradeToVersion4();
            schemaVersion = CurrentSchemaVersion;
        }
#endif
    }

    [Serializable]
    public sealed class DeckBuildingConfiguration
    {
        [SerializeField, Min(1)] private int startingEssence = 75;
        [SerializeField, Range(6, 30)] private int deckSize = 9;
        [SerializeField, Range(3, 10)] private int combatHandSize = 6;
        [SerializeField, Range(1, 5)] private int formationSize = 3;
        [SerializeField, Min(1)] private int blindRandomCost = 5;
        [SerializeField, Min(1)] private int chosenClassCost = 7;
        [SerializeField, Min(0)] private int chosenStrengthBaseCost = 4;
        [SerializeField, Range(1, 5)] private int maximumCopiesPerCard = 1;
        [SerializeField] private int[] strengthWeights = { 20, 18, 16, 14, 11, 8, 6, 4, 3 };

        public int StartingEssence => startingEssence;
        public int DeckSize => deckSize;
        public int CombatHandSize => combatHandSize;
        public int FormationSize => formationSize;
        public int BlindRandomCost => blindRandomCost;
        public int ChosenClassCost => chosenClassCost;
        public int ChosenStrengthBaseCost => chosenStrengthBaseCost;
        public int MaximumCopiesPerCard => maximumCopiesPerCard;
        public int[] StrengthWeights => strengthWeights;

        public DeckBuildingRules ToRules() => new(
            startingEssence,
            deckSize,
            combatHandSize,
            formationSize,
            blindRandomCost,
            chosenClassCost,
            chosenStrengthBaseCost,
            maximumCopiesPerCard,
            strengthWeights);
    }

    [Serializable]
    public sealed class LoggingConfiguration
    {
        [SerializeField, Min(10)] private int maximumEntries = 250;
        [SerializeField, Range(5, 30)] private int visibleEntries = 14;
        [SerializeField] private bool echoToUnityConsole = true;
        [SerializeField] private bool includeTimestamp = true;

        public int MaximumEntries => maximumEntries;
        public int VisibleEntries => visibleEntries;
        public bool EchoToUnityConsole => echoToUnityConsole;
        public bool IncludeTimestamp => includeTimestamp;
    }

    [Serializable]
    public sealed class ClassBalanceConfiguration
    {
        [SerializeField] private bool rogueRerollsOnes = true;
        [SerializeField, Min(0)] private int barbarianRageBonus = 2;
        [SerializeField, Min(0)] private int hunterStrongTargetBonus = 2;
        [SerializeField, Min(0)] private int priestBlessingBonus = 2;

        public bool RogueRerollsOnes => rogueRerollsOnes;
        public int BarbarianRageBonus => barbarianRageBonus;
        public int HunterStrongTargetBonus => hunterStrongTargetBonus;
        public int PriestBlessingBonus => priestBlessingBonus;
    }

    [Serializable]
    public sealed class ProgressionConfiguration
    {
        [SerializeField, Min(1)] private int experiencePerLevel = 100;
        [SerializeField, Min(0)] private int monsterRoomClearExperience = 10;
        [SerializeField, Range(1, 12)] private int maximumLevel = 6;
        [SerializeField, Min(1)] private int roomsPerMasterLevel = 5;
        [SerializeField] private int[] vigorDiceByLevel = { 4, 6, 8, 10, 12, 20 };

        [Header("Generazione stanze")]
        [SerializeField, Min(1)] private int minibossEveryRooms = 10;
        [SerializeField, Min(2)] private int finalBossRoom = 25;
        [SerializeField, Min(1)] private int bossFormationSize = 1;
        [SerializeField, Min(0)] private int monsterRoomWeight = 60;
        [SerializeField, Min(0)] private int merchantRoomWeight = 15;
        [SerializeField, Min(0)] private int lootRoomWeight = 15;
        [SerializeField, Min(0)] private int opportunityRoomWeight = 10;
        [SerializeField, Min(0)] private int lootRoomExperience = 10;
        [SerializeField, Min(0)] private int opportunityRoomExperience = 0;
        [SerializeField, Min(0)] private int merchantRoomExperience = 0;
        [SerializeField, Min(0)] private int opportunityExperienceJackpot = 50;
        [SerializeField, Min(0)] private int lootReserveCards = 1;
        [SerializeField, Min(0)] private int merchantHeroCardCost = 15;
        [SerializeField, Min(0)] private int godMerchantHeroCardCost = 30;
        [SerializeField, Min(1)] private int godMerchantMinimumStrength = 7;

        public int ExperiencePerLevel => experiencePerLevel;
        public int MonsterRoomClearExperience => monsterRoomClearExperience;
        public int MaximumLevel => maximumLevel;
        public int RoomsPerMasterLevel => roomsPerMasterLevel;
        public int[] VigorDiceByLevel => vigorDiceByLevel;
        public int MinibossEveryRooms => minibossEveryRooms;
        public int FinalBossRoom => finalBossRoom;
        public int BossFormationSize => bossFormationSize;
        public int MonsterRoomWeight => monsterRoomWeight;
        public int MerchantRoomWeight => merchantRoomWeight;
        public int LootRoomWeight => lootRoomWeight;
        public int OpportunityRoomWeight => opportunityRoomWeight;
        public int LootRoomExperience => lootRoomExperience;
        public int OpportunityRoomExperience => opportunityRoomExperience;
        public int MerchantRoomExperience => merchantRoomExperience;
        public int OpportunityExperienceJackpot => opportunityExperienceJackpot;
        public int LootReserveCards => lootReserveCards;
        public int MerchantHeroCardCost => merchantHeroCardCost;
        public int GodMerchantHeroCardCost => godMerchantHeroCardCost;
        public int GodMerchantMinimumStrength => godMerchantMinimumStrength;
    }

    [Serializable]
    public sealed class StartingRoomConfiguration
    {
        [SerializeField] private RoomType roomType = RoomType.Monster;
        [SerializeField] private RoomDifficulty difficulty = RoomDifficulty.Normal;
        [SerializeField] private string bossId = string.Empty;
        [SerializeField] private string scenarioId = string.Empty;

        public RoomType RoomType => roomType;
        public RoomDifficulty Difficulty => difficulty;
        public string BossId => bossId;
        public string ScenarioId => scenarioId;
    }

    [Serializable]
    public sealed class VisualConfiguration
    {
        [Header("Campo di battaglia")]
        [SerializeField, Range(0f, 1f)] private float backgroundFillBrightness = 0.28f;
        [SerializeField, Range(0f, 1f)] private float terrainBrightness = 0.78f;
        [SerializeField, Range(0f, 1f)] private float tableOverlayOpacity = 0.16f;

        [Header("HUD combattimento")]
        [SerializeField] private Color playerTurnColor = new(0.04f, 0.5f, 0.55f, 0.98f);
        [SerializeField] private Color cpuTurnColor = new(0.58f, 0.14f, 0.12f, 0.98f);
        [SerializeField] private Color targetAdvantageColor = new(0.08f, 0.55f, 0.25f, 0.94f);
        [SerializeField] private Color targetNeutralColor = new(0.95f, 0.78f, 0.12f, 0.94f);
        [SerializeField] private Color targetDisadvantageColor = new(0.65f, 0.1f, 0.1f, 0.94f);

        public float BackgroundFillBrightness => backgroundFillBrightness;
        public float TerrainBrightness => terrainBrightness;
        public float TableOverlayOpacity => tableOverlayOpacity;
        public Color PlayerTurnColor => playerTurnColor;
        public Color CpuTurnColor => cpuTurnColor;
        public Color TargetAdvantageColor => targetAdvantageColor;
        public Color TargetNeutralColor => targetNeutralColor;
        public Color TargetDisadvantageColor => targetDisadvantageColor;
    }

    [Serializable]
    public sealed class AnimationConfiguration
    {
        [Header("Carte")]
        [SerializeField, Min(0f)] private float attackExpandDuration = 0.13f;
        [SerializeField, Min(0f)] private float attackReturnDuration = 0.16f;
        [SerializeField, Range(1f, 1.5f)] private float attackScale = 1.13f;
        [SerializeField, Min(0f)] private float defeatDuration = 0.28f;

        [Header("Dadi e turni")]
        [SerializeField, Min(0f)] private float diceRollDuration = 1.36f;
        [SerializeField, Min(0f)] private float diceResultHold = 0.85f;
        [SerializeField, Min(0f)] private float cpuThinkDelay = 0.65f;
        [SerializeField, Min(0f)] private float cpuDecisionReveal = 0.35f;
        [SerializeField, Min(0f)] private float turnResultPause = 0.65f;
        [SerializeField, Min(0f)] private float combatResultHold = 0.9f;

        [Header("Schieramento")]
        [SerializeField, Min(0f)] private float cardDeployDuration = 0.34f;
        [SerializeField, Min(0f)] private float cpuCardRevealDuration = 0.4f;
        [SerializeField, Range(1f, 1.2f)] private float selectedCardScale = 1.055f;
        [SerializeField, Min(0f)] private float draftCardEnterDuration = 0.18f;
        [SerializeField, Min(0f)] private float draftCardCenterHold = 0.2f;
        [SerializeField, Min(0f)] private float draftCardSettleDuration = 0.22f;
        [SerializeField, Min(0f)] private float draftCardEntranceStagger = 0.055f;
        [SerializeField, Min(0f)] private float draftCardEntranceInitialDelay = 0.2f;
        [SerializeField, Range(1f, 1.8f)] private float draftCardEntranceScale = 1.5f;

        [Header("Transizione stanze")]
        [SerializeField, Min(0f)] private float roomFadeOutDuration = 0.32f;
        [SerializeField, Min(0f)] private float roomBlackHoldDuration = 0.14f;
        [SerializeField, Min(0f)] private float roomFadeInDuration = 0.42f;

        public float AttackExpandDuration => attackExpandDuration;
        public float AttackReturnDuration => attackReturnDuration;
        public float AttackScale => attackScale;
        public float DefeatDuration => defeatDuration;
        public float DiceRollDuration => diceRollDuration;
        public float DiceResultHold => diceResultHold;
        public float CpuThinkDelay => cpuThinkDelay;
        public float CpuDecisionReveal => cpuDecisionReveal;
        public float TurnResultPause => turnResultPause;
        public float CombatResultHold => combatResultHold > 0f ? combatResultHold : 0.9f;
        public float CardDeployDuration => cardDeployDuration;
        public float CpuCardRevealDuration => cpuCardRevealDuration;
        public float SelectedCardScale => selectedCardScale;
        public float DraftCardEnterDuration => draftCardEnterDuration;
        public float DraftCardCenterHold => draftCardCenterHold;
        public float DraftCardSettleDuration => draftCardSettleDuration;
        public float DraftCardEntranceStagger => draftCardEntranceStagger;
        public float DraftCardEntranceInitialDelay => draftCardEntranceInitialDelay;
        public float DraftCardEntranceScale => draftCardEntranceScale;
        public float RoomFadeOutDuration => roomFadeOutDuration;
        public float RoomBlackHoldDuration => roomBlackHoldDuration;
        public float RoomFadeInDuration => roomFadeInDuration;
    }

    [Serializable]
    public sealed class GameplayConfiguration
    {
        [SerializeField, Min(2)] private int initiativeDieSides = 20;
        [SerializeField, Min(2)] private int vigorDieSides = 6;
        [SerializeField] private int randomSeed = 20260620;
        [SerializeField] private bool useRandomSeedEachSession = true;
        [SerializeField, Range(3, 8)] private int draftCandidateCount = 6;
        [SerializeField, Range(1, 5)] private int formationSize = 3;

        [Header("CPU")]
        [SerializeField] private CpuDifficultySetting cpuDifficulty = CpuDifficultySetting.Normal;
        [SerializeField, Min(0)] private int killProbabilityWeight = 1000;
        [SerializeField, Min(0)] private int classAdvantageWeight = 100;
        [SerializeField, Min(0)] private int weakerTargetWeight = 8;
        [SerializeField, Min(0)] private int randomTieBreaker = 5;

        public int InitiativeDieSides => initiativeDieSides;
        public int VigorDieSides => vigorDieSides;
        public int RandomSeed => randomSeed;
        public bool UseRandomSeedEachSession => useRandomSeedEachSession;
        public int DraftCandidateCount => draftCandidateCount > 0 ? draftCandidateCount : 6;
        public int FormationSize => formationSize > 0 ? formationSize : 3;
        public CpuDifficultySetting CpuDifficulty => cpuDifficulty;
        public int KillProbabilityWeight => killProbabilityWeight;
        public int ClassAdvantageWeight => classAdvantageWeight;
        public int WeakerTargetWeight => weakerTargetWeight;
        public int RandomTieBreaker => randomTieBreaker;

#if UNITY_EDITOR
        public void UpgradeToVersion2()
        {
            cpuDifficulty = CpuDifficultySetting.Normal;
            killProbabilityWeight = 1000;
        }

        public void UpgradeToVersion4()
        {
            useRandomSeedEachSession = true;
        }
#endif
    }

    public enum CpuDifficultySetting
    {
        Easy,
        Normal,
        Hard
    }

    [Serializable]
    public sealed class ResponsiveLayoutConfiguration
    {
        [SerializeField] private Vector2 portraitReferenceResolution = new(1080f, 1920f);
        [SerializeField] private Vector2 landscapeReferenceResolution = new(1920f, 1080f);
        [SerializeField, Range(0.5f, 2f)] private float compactAspectBreakpoint = 1.2f;
        [SerializeField, Range(0.5f, 1f)] private float compactRowWidth = 0.98f;
        [SerializeField, Range(0.5f, 1f)] private float landscapeRowWidth = 0.9f;
        [SerializeField, Min(300f)] private float landscapeMaximumRowWidth = 1200f;
        [SerializeField, Range(0.005f, 0.1f)] private float gapFraction = 0.025f;
        [SerializeField, Min(0f)] private float minimumGap = 12f;
        [SerializeField, Min(0f)] private float maximumGap = 34f;
        [SerializeField, Range(0.1f, 0.4f)] private float compactCardHeight = 0.29f;
        [SerializeField, Range(0.1f, 0.4f)] private float landscapeCardHeight = 0.3f;

        [Header("Mano a ventaglio")]
        [SerializeField, Range(0.25f, 0.75f)] private float landscapeHandHeight = 0.6f;
        [SerializeField, Range(0.2f, 0.6f)] private float compactHandHeight = 0.48f;
        [SerializeField, Range(0f, 0.6f)] private float handOverlap = 0.22f;
        [SerializeField, Range(0f, 20f)] private float handMaximumAngle = 15f;
        [SerializeField, Min(0f)] private float handEdgeDrop = 52f;

        public Vector2 PortraitReferenceResolution => portraitReferenceResolution;
        public Vector2 LandscapeReferenceResolution => landscapeReferenceResolution;
        public float CompactAspectBreakpoint => compactAspectBreakpoint;
        public float CompactRowWidth => compactRowWidth;
        public float LandscapeRowWidth => landscapeRowWidth;
        public float LandscapeMaximumRowWidth => landscapeMaximumRowWidth;
        public float GapFraction => gapFraction;
        public float MinimumGap => minimumGap;
        public float MaximumGap => maximumGap;
        public float CompactCardHeight => compactCardHeight;
        public float LandscapeCardHeight => landscapeCardHeight;
        public float LandscapeHandHeight => landscapeHandHeight;
        public float CompactHandHeight => compactHandHeight;
        public float HandOverlap => handOverlap;
        public float HandMaximumAngle => handMaximumAngle;
        public float HandEdgeDrop => handEdgeDrop;
    }
}
