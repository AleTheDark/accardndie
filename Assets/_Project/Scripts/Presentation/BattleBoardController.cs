using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.PvpUi;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AccardND.Presentation
{
public sealed partial class BattleBoardController : MonoBehaviour, IPvpMatchView
{
	private const string ComposableGolemCardId = "miniboss-composable-golem";
	private const string MedusaBossCardId = "boss-medusa";
	private const string TrentorBossCardId = "trentor";
	private const string BragusBossCardId = "boss-bragus";
	private const string PalatirBossCardId = "boss-palatir";
	private const string MinibossGolemDebugSceneName = "MinibossGolemDebug";
	private const string MedusaBossDebugSceneName = "MedusaBossDebug";
	private const string TrentorBossDebugSceneName = "TrentorBossDebug";
	private const string BragusBossDebugSceneName = "BragusBossDebug";
	private const string PalatirBossDebugSceneName = "PalatirBossDebug";
	private const string MageVigorConstellationDebugSceneName = "MageVigorConstellationDebug";
	private const string DiceRollDebugSceneName = "DiceRollDebug";
	private const string LoginScreenPrototypeSceneName = "LoginScreenPrototype";
	private const string PlayerHudNamePrefsKey = "AccardND.PlayerHudName";
	private const int TutorialCompletionHoneyReward = 60;
	private const int HardcoreUnlockHoneyCost = 50;
	private static readonly Color AttackTargetLineColor = new(1f, 0.04f, 0.02f, 1f);
	private static readonly Color AttachmentTargetLineColor = new(1f, 0.45f, 0.03f, 1f);
	private static readonly Color AbilityTargetLineColor = new(0.1f, 0.58f, 1f, 1f);
	// Cache di visualizzazione: la UI legge sempre da qui. Quando c'e' una connessione server
	// autenticata, questo servizio locale viene rispecchiato dallo stato autoritativo del server.
	private readonly SinglePlayerProgressService singlePlayerProgressService = new SinglePlayerProgressService();
	// Progressione server-authoritative: attiva quando il link e' connesso. Se resta null il
	// single player usa il servizio locale (offline/dev), senza cambiare comportamento.
	private const bool ServerProgressEnabled = true;
	private AccardND.Network.SinglePlayerServerLink singlePlayerServerLink;
	private AccardND.Network.ServerSinglePlayerProgressRepository serverProgress;
	private bool ServerProgressReady =>
		serverProgress != null && singlePlayerServerLink != null && singlePlayerServerLink.IsReady;

	private enum MerchantBuyMode
	{
		Random,
		Class,
		Strength
	}

	private enum AbilityTargetMode
	{
		None,
		AssassinEnemy,
		MageEnemy,
		HunterEnemy,
		PaladinAlly,
		NecromancerAlly,
		PriestAlly,
		AttachmentAlly
	}

	private enum BattleAuraType
	{
		None,
		Formation,
		Might,
		Cunning,
		Magic,
		Warrior,
		Barbarian,
		Paladin,
		Rogue,
		Assassin,
		Hunter,
		Mage,
		Necromancer,
		Priest
	}

	private enum CampaignDoorDifficulty
	{
		Easy,
		Normal,
		Hard
	}

	private enum CampaignConsumableType
	{
		Detector,
		SecondChance,
		Defrost,
		Empower,
		DoubleExp
	}

	private enum MinibossKind
	{
		ComposableGolem
	}

	private enum CpuEncounterKind
	{
		MonsterFormation,
		BossFormation,
		ComposableGolem,
		Medusa,
		Trentor,
		Bragus,
		Palatir
	}

	private readonly struct CampaignDoor
	{
		public CampaignDoorDifficulty Difficulty { get; }

		public CampaignRoomRoll? RevealedRoom { get; }

		public CampaignDoor(CampaignDoorDifficulty difficulty)
		{
			Difficulty = difficulty;
			RevealedRoom = null;
		}

		public CampaignDoor(CampaignDoorDifficulty difficulty, CampaignRoomRoll revealedRoom)
		{
			Difficulty = difficulty;
			RevealedRoom = revealedRoom;
		}
	}

	private sealed class CampaignConsumableState
	{
		private readonly Dictionary<CampaignConsumableType, int> quantities = new Dictionary<CampaignConsumableType, int>();

		public int GetQuantity(CampaignConsumableType type)
		{
			return quantities.TryGetValue(type, out int quantity) ? quantity : 0;
		}

		public void Add(CampaignConsumableType type, int amount = 1)
		{
			if (amount <= 0)
				return;
			quantities[type] = GetQuantity(type) + amount;
		}

		public bool TryConsume(CampaignConsumableType type)
		{
			int quantity = GetQuantity(type);
			if (quantity <= 0)
				return false;
			quantities[type] = quantity - 1;
			return true;
		}

		public void Clear()
		{
			quantities.Clear();
		}
	}

	private readonly struct CampaignRoomRoll
	{
		public RoomType RoomType { get; }

		public int MonsterTier { get; }

		public string ScenarioId { get; }

		public RoomDifficulty Difficulty { get; }

		public CampaignRoomRoll(RoomType roomType, int monsterTier, string scenarioId, RoomDifficulty difficulty)
		{
			RoomType = roomType;
			MonsterTier = monsterTier;
			ScenarioId = scenarioId;
			Difficulty = difficulty;
		}
	}

	private sealed class BattleCardState
	{
		public CardDefinition Definition { get; }

		public CombatCard Card { get; }

		public PrototypeCardView View { get; }

		public bool BelongsToPlayer { get; }

		public CampaignCardInstance CampaignCard { get; }

		public int Initiative { get; set; }

		public int TieBreaker { get; set; }

		public bool Eliminated { get; set; }

		public bool AbilityArmed { get; set; }

		public bool AbilityUsed { get; set; }

		public int PendingAttackBonus { get; set; }

		public PendingAttackBonusKind PendingAttackBonusKind { get; set; }

		public int PermanentCombatBonus { get; set; }

		public int MightAuraCombatBonus { get; set; }

		public int InhibitedTurns { get; set; }

		public bool WasInhibited { get; set; }

		public int PendingVigorStepPenalty { get; set; }

		public bool IsSpirit { get; set; }

		public int RevivedRound { get; set; }

		public bool IsAttachment { get; set; }

		public bool Petrified { get; set; }

		public BattleCardState MarkedTarget { get; set; }

		public BattleCardState ProtectedAlly { get; set; }

		public BattleCardState AttachedTo { get; set; }

		public BattleCardState(CardDefinition definition, PrototypeCardView view, bool belongsToPlayer, CampaignCardInstance campaignCard = null)
		{
			Definition = definition;
			Card = string.Equals(definition.Id, MedusaBossCardId, StringComparison.OrdinalIgnoreCase)
				? new CombatCard(definition.Id, definition.DisplayName, HeroClass.Mage, MedusaBoss.CardStrength)
				: string.Equals(definition.Id, BragusBossCardId, StringComparison.OrdinalIgnoreCase)
					? new CombatCard(definition.Id, definition.DisplayName, HeroClass.Barbarian, BragusBoss.CardStrength)
					: string.Equals(definition.Id, PalatirBossCardId, StringComparison.OrdinalIgnoreCase)
						? new CombatCard(definition.Id, definition.DisplayName, HeroClass.Mage, PalatirBoss.CardStrength)
						: definition.CreateCombatCard();
			View = view;
			BelongsToPlayer = belongsToPlayer;
			CampaignCard = campaignCard;
		}
	}

	private enum PendingAttackBonusKind
	{
		None,
		Fury,
		Blessing
	}

	private readonly struct InspectionStatusDetail
	{
		public string Label { get; }

		public string Description { get; }

		public Color Color { get; }

		public InspectionStatusDetail(string label, string description, Color color)
		{
			Label = label;
			Description = description;
			Color = color;
		}
	}

	private sealed class DeploymentToken
	{
		public bool BelongsToPlayer { get; }

		public int Initiative { get; }

		public int TieBreaker { get; }

		public DeploymentToken(bool belongsToPlayer, int initiative, int tieBreaker)
		{
			BelongsToPlayer = belongsToPlayer;
			Initiative = initiative;
			TieBreaker = tieBreaker;
		}
	}

	private readonly struct HandRedealPose
	{
		public Vector3 WorldPosition { get; }

		public Quaternion WorldRotation { get; }

		public HandRedealPose(Vector3 worldPosition, Quaternion worldRotation)
		{
			WorldPosition = worldPosition;
			WorldRotation = worldRotation;
		}
	}

	private static Sprite helpAuraSprite;

	private static readonly Dictionary<string, Sprite> spriteResourceCache = new Dictionary<string, Sprite>();

	private readonly List<BattleCardState> playerCards = new List<BattleCardState>();

	private readonly List<BattleCardState> cpuCards = new List<BattleCardState>();

	private readonly List<BattleCardState> turnOrder = new List<BattleCardState>();

	private readonly List<CardDefinition> draftCandidates = new List<CardDefinition>();

	private readonly List<CampaignCardInstance> draftCampaignCards = new List<CampaignCardInstance>();

	private readonly List<DeploymentToken> deploymentOrder = new List<DeploymentToken>();

	private readonly List<CardDefinition> cpuDeploymentHand = new List<CardDefinition>();

	private readonly List<CardDefinition> selectedCpuDeploymentCards = new List<CardDefinition>();

	private readonly List<int> selectedPlayerDeploymentInitiatives = new List<int>();

	private readonly List<int> selectedCpuDeploymentInitiatives = new List<int>();

	private readonly List<PrototypeCardView> cpuDeploymentPreviewViews = new List<PrototypeCardView>();

	private readonly List<PrototypeCardView> playerDeploymentPreviewViews = new List<PrototypeCardView>();

	private readonly List<CardDefinition> playerReserve = new List<CardDefinition>();

	private readonly List<CardDefinition> initialPlayerReserve = new List<CardDefinition>();

	private readonly List<CardDefinition> initialPlayerFormation = new List<CardDefinition>();

	private readonly List<CampaignCardInstance> initialPlayerCampaignFormation = new List<CampaignCardInstance>();

	private readonly List<CardDefinition> initialCpuFormation = new List<CardDefinition>();

	private readonly List<CardDefinition> survivingCpuFormation = new List<CardDefinition>();

	private readonly List<PrototypeCardView> draftViews = new List<PrototypeCardView>();

	private readonly HashSet<PrototypeCardView> draftEntranceAnimatingViews = new HashSet<PrototypeCardView>();

	private readonly HashSet<PrototypeCardView> handRelayoutAnimatingViews = new HashSet<PrototypeCardView>();

	private readonly List<GameObject> draftEntranceOverlayObjects = new List<GameObject>();

	private Coroutine draftEntranceCoroutine;

	private Coroutine handRelayoutCoroutine;

	private Coroutine playerBattlefieldRowTransitionCoroutine;

	private readonly HashSet<int> selectedDraftCards = new HashSet<int>();

	private readonly List<int> selectedPlayerDeploymentIndices = new List<int>();

	private readonly List<string> gameLogEntries = new List<string>();

	private InitialDeckBuilder initialDeckBuilder;

	private CampaignDeckState campaignDeck;

	private GameObject initialDraftPanel;

	private Image initialDraftFrameImage;

	private AspectRatioFitter initialDraftFrameAspectFitter;

	private Text initialDraftHeadingText;

	private Text initialDraftStatusText;

	private Text initialDraftPromptText;

	private RectTransform initialDraftOffersRoot;

	private RectTransform initialDraftDeckRoot;

	private Text initialDraftDeckText;

	private Button initialDraftConfirmButton;

	private RectTransform initialDraftConfirmButtonRect;

	private Text initialDraftConfirmButtonText;

	private readonly List<CardDefinition> initialDraftOffers = new List<CardDefinition>();

	private readonly List<CardDefinition> initialDraftDeck = new List<CardDefinition>();

	private readonly List<PrototypeCardView> initialDraftOfferViews = new List<PrototypeCardView>();

	private readonly List<PrototypeCardView> initialDraftDeckViews = new List<PrototypeCardView>();

	private readonly HashSet<int> initialDraftSelectedIndices = new HashSet<int>();

	private HeroClass? initialDraftCaptainClass;

	private bool initialDraftChoosingCaptain;

	private GameObject deckBuilderPanel;

	private Image deckBuilderFrameImage;

	private AspectRatioFitter deckBuilderFrameAspectFitter;

	private Text deckBuilderHeadingText;

	private Text deckBuilderStatusText;

	private Text deckBuilderCardsText;

	private RectTransform deckBuilderCardsRoot;

	private readonly List<PrototypeCardView> deckBuilderCardViews = new List<PrototypeCardView>();

	private RectTransform deckBuilderRandomButtonRect;

	private Text deckBuilderRandomBuyText;

	private Text deckBuilderClassText;

	private Image deckBuilderClassImage;

	private RectTransform deckBuilderClassButtonRect;

	private RectTransform deckBuilderClassPreviousButtonRect;

	private RectTransform deckBuilderClassNextButtonRect;

	private Text deckBuilderClassBuyText;

	private Image deckBuilderStrengthImage;

	private RectTransform deckBuilderStrengthButtonRect;

	private RectTransform deckBuilderStrengthPreviousButtonRect;

	private RectTransform deckBuilderStrengthNextButtonRect;

	private Text deckBuilderStrengthBuyText;

	private GameObject deckBuilderToastRoot;

	private RectTransform deckBuilderToastRect;

	private Text deckBuilderToastText;

	private Coroutine deckBuilderToastRoutine;

	private Image startCampaignHelpAura;

	private RectTransform startCampaignHelpAuraRect;

	private Button startCampaignButton;

	private RectTransform startCampaignButtonRect;

	private GameObject modeSelectionPanel;

	private Image modeSelectionImage;

	private AspectRatioFitter modeSelectionAspectFitter;

	private Button modeSelectionCampaignButton;

	private Button modeSelectionMultiplayerButton;

	private Button modeSelectionTutorialButton;

	private Button tutorialAdvanceButton;

	private GameObject campaignModeSelectionPanel;

	private Image campaignModeSelectionFrameImage;

	private AspectRatioFitter campaignModeSelectionFrameAspectFitter;

	private Text campaignModeSelectionHeadingText;

	private Text campaignModeSelectionPromptText;

	private Text campaignModeSelectionProgressText;

	private Button campaignModeAdventureButton;

	private RectTransform campaignModeBuilderButtonRect;

	private Button campaignModeHardcoreButton;

	private Text campaignModeHardcoreButtonText;

	private RectTransform campaignModeDraftButtonRect;

	private GameObject adventureChapterPanel;

	private Text adventureChapterHeadingText;

	private Text adventureChapterProgressText;

	private RectTransform adventureChapterListRoot;

	private Button adventureChapterBackButton;

	private readonly List<GameObject> adventureChapterRows = new List<GameObject>();

	private GameObject adventureTutorialConfirmPopup;

	private Text adventureTutorialConfirmBodyText;

	private GameObject guidedTutorialPanel;

	private Text guidedTutorialTitleText;

	private Text guidedTutorialBodyText;

	private Text guidedTutorialStepText;

	private Button guidedTutorialPreviousButton;

	private Button guidedTutorialNextButton;

	private Text guidedTutorialNextButtonText;

	private int guidedTutorialStepIndex;

	private bool adventureScriptedTutorialActive;

	private int adventureScriptedTutorialStep;

	private GameObject adventureScriptedTutorialPanel;

	private Text adventureScriptedTutorialTitleText;

	private Text adventureScriptedTutorialBodyText;

	private Text adventureScriptedTutorialStepText;

	private Image adventureScriptedTutorialSpotlight;

	private readonly List<Image> adventureScriptedTutorialDimmers = new List<Image>();

	private int tutorialPageIndex;

	private bool modeSelectionTutorialActive;

	private bool tutorialReturnToModeSelection;

	private bool tutorialPreviousInputLocked;

	private GameObject multiplayerPopup;

	private GameObject roomChoicePanel;

	private Image roomChoiceImage;

	private AspectRatioFitter roomChoiceAspectFitter;

	private int roomChoiceBackgroundIndex = 1;

	private Button roomChoiceLeftButton;

	private Button roomChoiceCenterButton;

	private Button roomChoiceRightButton;

	private readonly List<Text> roomChoiceRevealLabels = new List<Text>();

	private readonly List<CampaignDoor> campaignDoors = new List<CampaignDoor>();

	private HeroClass deckBuilderSelectedClass = HeroClass.Warrior;

	private int deckBuilderSelectedStrength = 2;

	private IRandomSource random;

	private CombatResolver combatResolver;

	private CpuDecisionService cpuDecisionService;

	private RunProgressState runProgress;

	private DiceSpriteCatalog diceCatalog;

	private BattlePresentationAnimationPlayer battleAnimationPlayer;

	private GameConfiguration configuration;

	private CardDatabase cardDatabase;

	private FormationDraftService formationDraftService;

	private Text messageText;

	private Image turnBannerImage;

	private Text turnBannerText;

	private RectTransform initiativeTimelineRoot;

	private RectTransform timelineBackgroundRect;

	private Vector2 timelineBackgroundBaseMin;

	private Vector2 timelineBackgroundBaseMax;

	private bool hasTimelineBackgroundBaseRect;

	private readonly List<string> campaignTimelineOrderKeys = new();

	private bool timelineLayoutVertical;

	private Text roundText;

	private RectTransform campaignZoneRect;

	private Text campaignZoneText;

	private Button restartButton;

	private Button confirmActionButton;

	private Text confirmActionButtonText;

	private Button cancelActionButton;

	private Button abilityButton;

	private Button attachmentButton;

	private Text attachmentButtonText;

	private Sprite confirmActionSprite;

	private Sprite cancelActionSprite;

	private Sprite infoActionSprite;

	private Button merchantBuyButton;

	private Text merchantBuyButtonText;

	private GameObject merchantPanel;

	private RectTransform merchantDeckCardsRoot;

	private RectTransform merchantGraveyardCardsRoot;

	private Text merchantDeckEmptyText;

	private Text merchantGraveyardEmptyText;

	private Text merchantStatusText;

	private Text merchantSellText;

	private Button merchantSellButton;

	private Button merchantRecoverButton;

	private Button merchantRandomBuyButton;

	private Button merchantClassBuyButton;

	private Button merchantStrengthBuyButton;

	private Image merchantClassImage;

	private Image merchantStrengthImage;

	private Text merchantClassText;

	private readonly List<PrototypeCardView> merchantOwnedCardViews = new List<PrototypeCardView>();

	private CampaignCardInstance selectedMerchantSaleCard;

	private Text playerTitleText;

	private Text cpuTitleText;

	private Text restartButtonText;

	private ScreenFadeTransition roomTransition;

	private Button logButton;

	private Text settingsButtonLabel;

	private Sprite settingsButtonSprite;

	private GameObject logPanel;

	private Text logText;

	private GameObject optionsPanel;

	private GameObject optionsBackdropPanel;

	private GameObject returnToMenuConfirmPanel;

	private Text sfxVolumeText;

	private Button sfxMuteButton;

	private Text sfxMuteButtonText;

	private Text musicVolumeText;

	private Button musicMuteButton;

	private Text musicMuteButtonText;

	private Button implementationArchiveButton;

	private Text implementationArchiveButtonLabel;

	private GameObject implementationArchivePanel;

	private GameObject implementationArchiveBackdropPanel;

	private RectTransform implementationArchiveButtonRect;

	private RectTransform implementationArchivePanelRect;

	private RectTransform implementationDeckRoot;

	private RectTransform implementationCooldownRoot;

	private RectTransform implementationGraveyardRoot;

	private Text implementationDeckEmptyText;

	private Text implementationCooldownEmptyText;

	private Text implementationGraveyardEmptyText;

	private readonly List<PrototypeCardView> implementationArchiveCardViews = new List<PrototypeCardView>();

	private readonly List<GameObject> implementationConsumableViews = new List<GameObject>();

	private RectTransform implementationConsumablesRoot;

	private Text implementationConsumablesEmptyText;

	private GameObject combatResultRoot;

	private Text combatScoreText;

	private Text combatDiceText;

	private Text combatOutcomeText;

	private GameObject cardInspectionPanel;

	private RectTransform cardInspectionBookRoot;

	private Image cardInspectionBookImage;

	private AspectRatioFitter cardInspectionBookAspectFitter;

	private RectTransform cardInspectionSlot;

	private Text cardInspectionSummaryText;

	private RectTransform cardInspectionStatusRoot;

	private Button cardInspectionCloseButton;

	private Button cardInspectionDraftConfirmButton;

	private RectTransform cardInspectionDraftConfirmButtonRect;

	private Text cardInspectionDraftConfirmButtonText;

	private PrototypeCardView inspectedCardView;

	private bool cardInspectionPausedGame;

	private float cardInspectionPreviousTimeScale = 1f;

	private int inspectedInitialDraftOfferIndex = -1;

	private bool inspectedCampaignConsumableActive;

	private CampaignConsumableType inspectedCampaignConsumableType;

	private readonly List<GameObject> cardInspectionStatusRows = new List<GameObject>();

	private RectTransform topInfoBarRect;

	private Text topInfoText;

	private RectTransform playerRow;

	private RectTransform playerHandRow;

	private RectTransform cpuRow;

	private RectTransform safeAreaRoot;

	private RectTransform tableGlowRect;

	private RectTransform titleRect;

	private RectTransform cpuTitleRect;

	private RectTransform messagePanelRect;

	private RectTransform playerTitleRect;

	private CanvasScaler canvasScaler;

	private RectTransform canvasRect;

	private Image backgroundFillImage;

	private Image terrainImage;

	private AspectRatioFitter terrainAspectFitter;

	private ScenarioCatalog scenarioCatalog;

	private ScenarioDefinition currentScenario;

	private string currentScenarioDisplayOverride;

	private int previousScreenWidth;

	private int previousScreenHeight;

	private Rect previousSafeArea;

	private ScreenOrientation previousScreenOrientation;

	private bool combatChromeVisible;

	private int selectedPlayerIndex = -1;

	private int currentTurnIndex;

	private int roundNumber;

	private bool inputLocked;

	private bool gameFinished;

	private bool draftActive;

	private bool deploymentDraftActive;

	private bool deploymentInitiativesReady;

	private int currentDeploymentIndex;

	private int pendingDeploymentIndex = -1;

	private bool canAdvanceToNextRoom;

	private bool canRetryCampaignRoom;

	private bool returningToStartAfterGameOver;

	private RoomType currentRoomType = RoomType.Monster;

	private int currentMonsterTier = 2;

	private string pendingScenarioId;

	private RoomDifficulty pendingRoomDifficulty = RoomDifficulty.Normal;

	private string campaignScenarioId;

	private string campaignScenarioBossId;

	private AbilityTargetMode abilityTargetMode;

	private bool attackTargetingActive;

	private bool messagePanelHiddenForDuel;

	private BattleCardState activeAbilityUser;

	private BattleCardState activeAttachmentSource;

	private BattleCardState pendingAbilityUser;

	private BattleAuraType playerAura;

	private BattleAuraType cpuAura;

	private bool formationAuraUsed;

	private bool necromancerSpiritUsed;

	private bool skipNextCombatCooldown;

	private bool nextCombatFallenHeroesGrantExperience;

	private bool nextCombatAssassinsActLast;

	private bool nextCombatWarriorsLowerVigor;

	private bool nextCombatTankDuel;

	private int nextMonsterTierBonus;

	private bool nextDoorChoiceRevealed;

	private bool nextRoomEmpowered;

	private bool nextRoomDoubleExperience;

	private CampaignConsumableState campaignConsumables = new CampaignConsumableState();

	private ComposableGolem activeComposableGolem;

	private ComposableGolemFormStats[] retryComposableGolemForms;

	private MedusaBoss activeMedusaBoss;

	private TrentorBoss activeTrentorBoss;

	private BragusBoss activeBragusBoss;

	private PalatirBoss activePalatirBoss;

	private bool merchantRoomsBlockedUntilMonster;

	private bool rewardRoomsBlockedUntilMonster;

	private bool debugForceFirstRoomComposableGolem;

	private bool debugForceFirstRoomMedusa;

	private bool debugForceFirstRoomTrentor;

	private bool debugForceFirstRoomBragus;

	private bool debugForceFirstRoomPalatir;

	private HeroClass merchantSelectedClass = HeroClass.Warrior;

	private int merchantSelectedStrength = 2;

	[RuntimeInitializeOnLoadMethod(/*Could not decode attribute arguments.*/)]
	private static void Bootstrap()
	{
		SceneManager.sceneLoaded -= BootstrapLoadedScene;
		SceneManager.sceneLoaded += BootstrapLoadedScene;
		EnsureControllerForScene(SceneManager.GetActiveScene());
	}

	private static void BootstrapLoadedScene(Scene scene, LoadSceneMode mode)
	{
		EnsureControllerForScene(scene);
	}

	private static void EnsureControllerForScene(Scene scene)
	{
		if (string.Equals(
			scene.name,
			MageVigorConstellationDebugSceneName,
			StringComparison.OrdinalIgnoreCase)
			|| string.Equals(
			scene.name,
			DiceRollDebugSceneName,
			StringComparison.OrdinalIgnoreCase)
			|| string.Equals(
			scene.name,
			LoginScreenPrototypeSceneName,
			StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		if (!((Object)(object)Object.FindAnyObjectByType<BattleBoardController>() != (Object)null))
		{
			GameObject val = new GameObject("Accard N' Die - Battle Board");
			Object.DontDestroyOnLoad((Object)val);
			val.AddComponent<BattleBoardController>();
		}
	}

	private void Awake()
	{
		configuration = Resources.Load<GameConfiguration>("GameConfiguration");
		if ((Object)(object)configuration == (Object)null)
		{
			configuration = ScriptableObject.CreateInstance<GameConfiguration>();
		}
		debugForceFirstRoomComposableGolem = string.Equals(
			SceneManager.GetActiveScene().name,
			MinibossGolemDebugSceneName,
			StringComparison.OrdinalIgnoreCase);
		debugForceFirstRoomMedusa = string.Equals(
			SceneManager.GetActiveScene().name,
			MedusaBossDebugSceneName,
			StringComparison.OrdinalIgnoreCase);
		debugForceFirstRoomTrentor = string.Equals(
			SceneManager.GetActiveScene().name,
			TrentorBossDebugSceneName,
			StringComparison.OrdinalIgnoreCase);
		debugForceFirstRoomBragus = string.Equals(
			SceneManager.GetActiveScene().name,
			BragusBossDebugSceneName,
			StringComparison.OrdinalIgnoreCase);
		debugForceFirstRoomPalatir = string.Equals(
			SceneManager.GetActiveScene().name,
			PalatirBossDebugSceneName,
			StringComparison.OrdinalIgnoreCase);
		int num = (configuration.UseRandomSeedEachSession ?Guid.NewGuid().GetHashCode() : configuration.Gameplay.RandomSeed);
		random = new SeededRandomSource(num);
		combatResolver = new CombatResolver(random);
		cpuDecisionService = new CpuDecisionService(random);
		runProgress = CreateRunProgress();
		diceCatalog = Resources.Load<DiceSpriteCatalog>("DiceSpriteCatalog");
		battleAnimationPlayer = gameObject.AddComponent<BattlePresentationAnimationPlayer>();
		InitializeAudio();
		BuildInterface();
		AppendLog($"SESSIONE AVVIATA - seed {num}");
		if (debugForceFirstRoomComposableGolem)
		{
			AppendLog("DEBUG - scena MinibossGolemDebug: prima stanza forzata su Golem Componibile.");
		}
		if (debugForceFirstRoomMedusa)
		{
			AppendLog("DEBUG - scena MedusaBossDebug: prima stanza forzata su Medusa.");
		}
		if (debugForceFirstRoomTrentor)
		{
			AppendLog("DEBUG - scena TrentorBossDebug: prima stanza forzata su Trentor.");
		}
		if (debugForceFirstRoomBragus)
		{
			AppendLog("DEBUG - scena BragusBossDebug: prima stanza forzata su Bragus.");
		}
		if (debugForceFirstRoomPalatir)
		{
			AppendLog("DEBUG - scena PalatirBossDebug: prima stanza forzata su Palatir.");
		}
		ShowModeSelection();
	}

	private void Update()
	{
		if (IsEscapePressedThisFrame() && CloseTopmostOverlay())
		{
			return;
		}

		if (HasScreenGeometryChanged())
		{
			ApplyResponsiveLayout();
		}
	}

	private static bool IsEscapePressedThisFrame()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
	}

	private bool CloseTopmostOverlay()
	{
		if (IsActive(hintPanel))
		{
			DismissHint();
			return true;
		}
		else if (IsActive(auraCodexPanel))
		{
			CloseAuraCodex();
			return true;
		}
		else if (IsActive(returnToMenuConfirmPanel))
		{
			HideReturnToMenuConfirmation();
			return true;
		}
		else if (IsActive(cardInspectionPanel))
		{
			CloseCardInspection();
			return true;
		}
		else if (IsActive(implementationArchivePanel))
		{
			CloseImplementationArchive();
			return true;
		}
		else if (IsActive(merchantPanel))
		{
			CloseMerchantPanel();
			return true;
		}
		else if (IsActive(optionsPanel))
		{
			CloseOptionsPanel();
			return true;
		}

		return false;
	}

	private static bool IsActive(GameObject panel)
	{
		return (Object)(object)panel != (Object)null && panel.activeSelf;
	}

	private bool HasScreenGeometryChanged()
	{
		return Screen.width != previousScreenWidth
			|| Screen.height != previousScreenHeight
			|| Screen.safeArea != previousSafeArea
			|| Screen.orientation != previousScreenOrientation;
	}

	private void LateUpdate()
	{
		if (draftActive)
		{
			ApplyHandFan();
		}
		UpdateMessageTextLayout();
	}

	private void BuildInterface()
	{
		EnsureEventSystem();
		Font builtinResource = AccardND.Battlefield.MmoUiTheme.BodyFont;
		confirmActionSprite = LoadSpriteResource("UI/confirm_button");
		cancelActionSprite = LoadSpriteResource("UI/cancel_button");
		infoActionSprite = LoadSpriteResource("UI/info_button");
		settingsButtonSprite = LoadSpriteResource("UI/settings_button");
		Canvas val = CreateCanvas();
		canvasRect = (RectTransform)((Component)val).transform;
		canvasScaler = ((Component)val).GetComponent<CanvasScaler>();
		scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		StartingRoomConfiguration startingRoom = configuration.StartingRoom;
		currentScenario = (((Object)(object)scenarioCatalog != (Object)null) ?scenarioCatalog.Select(startingRoom.RoomType, startingRoom.Difficulty, startingRoom.BossId, startingRoom.ScenarioId) : null);
		Sprite sprite = CurrentScenarioBackgroundSprite();
		VisualConfiguration visual = configuration.Visual;
		float backgroundFillBrightness = visual.BackgroundFillBrightness;
		backgroundFillImage = CreateImage("Background", ((Component)val).transform, new Color(backgroundFillBrightness, backgroundFillBrightness, backgroundFillBrightness));
		backgroundFillImage.sprite = sprite;
		Image image = backgroundFillImage;
		Stretch(image.rectTransform);
		terrainImage = CreateImage("Terrain", ((Component)image).transform, Color.white);
		terrainImage.sprite = sprite;
		terrainImage.preserveAspect = true;
		float terrainBrightness = visual.TerrainBrightness;
		terrainImage.color = new Color(terrainBrightness, terrainBrightness, terrainBrightness);
		Stretch(terrainImage.rectTransform);
		terrainAspectFitter = ConfigureFittedBackground(terrainImage, sprite, 0.5625f);
		safeAreaRoot = new GameObject("Safe Area", new Type[1] { typeof(RectTransform) }).GetComponent<RectTransform>();
		((Transform)safeAreaRoot).SetParent(((Component)image).transform, false);
		Stretch(safeAreaRoot);
		Image image2 = CreateImage("Table Glow", (Transform)(object)safeAreaRoot, new Color(0.025f, 0.06f, 0.07f, visual.TableOverlayOpacity));
		tableGlowRect = image2.rectTransform;
		image2.rectTransform.anchorMin = new Vector2(0.08f, 0.13f);
		image2.rectTransform.anchorMax = new Vector2(0.92f, 0.87f);
		image2.rectTransform.offsetMin = Vector2.zero;
		image2.rectTransform.offsetMax = Vector2.zero;
		Image image3 = CreateImage("Top Info Bar", (Transform)(object)safeAreaRoot, new Color(0.01f, 0.025f, 0.035f, 0.92f));
		StylePanel(image3);
		topInfoBarRect = image3.rectTransform;
		SetRect(topInfoBarRect, new Vector2(0.04f, 0.942f), new Vector2(0.84f, 0.992f));
		Text text = CreateText("Top Info Text", ((Component)image3).transform, builtinResource, 19, (FontStyle)1, (TextAnchor)3);
		titleRect = text.rectTransform;
		topInfoText = text;
		topInfoText.text = "STANZA 1  |  ROUND 0  |  LV 1  |  EXP 0/100  |  D4  |  DISP 0";
		topInfoText.color = new Color(0.95f, 0.79f, 0.34f);
		Stretch(topInfoText.rectTransform, 10f);
		((Component)topInfoBarRect).gameObject.SetActive(false);
		logButton = CreateImageButton("Options Button", (Transform)(object)safeAreaRoot, builtinResource, settingsButtonSprite, string.Empty);
		((UnityEvent)logButton.onClick).AddListener(new UnityAction(ToggleOptionsPanel));
		SetRect((RectTransform)((Component)logButton).transform, new Vector2(0.84f, 0.902f), new Vector2(0.995f, 0.992f));
		settingsButtonLabel = CreateText("Options Button Label", (Transform)(object)safeAreaRoot, builtinResource, 20, (FontStyle)1, (TextAnchor)4);
		settingsButtonLabel.text = "settings";
		settingsButtonLabel.color = new Color(0.95f, 0.79f, 0.34f);
		settingsButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
		settingsButtonLabel.verticalOverflow = VerticalWrapMode.Truncate;
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(settingsButtonLabel);
		Outline settingsButtonLabelOutline = ((Component)settingsButtonLabel).gameObject.AddComponent<Outline>();
		settingsButtonLabelOutline.effectColor = new Color(0.04f, 0.02f, 0.01f, 0.95f);
		settingsButtonLabelOutline.effectDistance = new Vector2(2f, -2f);
		SetRect(settingsButtonLabel.rectTransform, new Vector2(0.825f, 0.882f), new Vector2(1f, 0.924f));
		Image image4 = CreateImage("Game Log Panel", (Transform)(object)safeAreaRoot, new Color(0.008f, 0.014f, 0.022f, 0.97f));
		StylePanel(image4);
		logPanel = ((Component)image4).gameObject;
		SetRect(image4.rectTransform, new Vector2(0.08f, 0.1f), new Vector2(0.92f, 0.9f));
		Canvas obj = logPanel.AddComponent<Canvas>();
		obj.overrideSorting = true;
		obj.sortingOrder = 500;
		logPanel.AddComponent<GraphicRaycaster>();
		logText = CreateText("Log Entries", logPanel.transform, builtinResource, 18, (FontStyle)0, (TextAnchor)6);
		logText.color = new Color(0.82f, 0.9f, 0.92f);
		SetRect(logText.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.9f));
		Button button = CreateButton("Close Log", logPanel.transform, builtinResource, "CHIUDI");
		((UnityEvent)button.onClick).AddListener(new UnityAction(ToggleLogPanel));
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.84f, 0.93f), new Vector2(0.98f, 0.99f));
		logPanel.SetActive(false);
		Image optionsBackdrop = CreateImage("Options Backdrop", ((Component)val).transform, new Color(0f, 0f, 0f, 0.72f));
		optionsBackdrop.raycastTarget = true;
		Stretch(optionsBackdrop.rectTransform);
		optionsBackdropPanel = ((Component)optionsBackdrop).gameObject;
		Button optionsBackdropButton = optionsBackdropPanel.AddComponent<Button>();
		optionsBackdropButton.transition = Selectable.Transition.None;
		((UnityEvent)optionsBackdropButton.onClick).AddListener(new UnityAction(CloseOptionsPanel));
		Canvas optionsBackdropCanvas = optionsBackdropPanel.AddComponent<Canvas>();
		optionsBackdropCanvas.overrideSorting = true;
		optionsBackdropCanvas.sortingOrder = 519;
		optionsBackdropPanel.AddComponent<GraphicRaycaster>();
		optionsBackdropPanel.SetActive(false);
		Image imageOptions = CreateImage("Options Panel", (Transform)(object)safeAreaRoot, new Color(0.008f, 0.014f, 0.022f, 0.97f));
		StylePanel(imageOptions);
		optionsPanel = ((Component)imageOptions).gameObject;
		SetRect(imageOptions.rectTransform, new Vector2(0.64f, 0.52f), new Vector2(0.98f, 0.92f));
		Canvas optionsCanvas = optionsPanel.AddComponent<Canvas>();
		optionsCanvas.overrideSorting = true;
		optionsCanvas.sortingOrder = 520;
		optionsPanel.AddComponent<GraphicRaycaster>();
		Text optionsTitle = CreateText("Options Title", optionsPanel.transform, builtinResource, 22, (FontStyle)1, (TextAnchor)4);
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(optionsTitle);
		optionsTitle.text = "OPZIONI";
		optionsTitle.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(optionsTitle.rectTransform, new Vector2(0.06f, 0.87f), new Vector2(0.94f, 0.97f));
		Button optionsLogButton = CreateButton("Options Open Log", optionsPanel.transform, builtinResource, "LOG");
		((UnityEvent)optionsLogButton.onClick).AddListener(new UnityAction(OpenLogFromOptions));
		SetRect((RectTransform)((Component)optionsLogButton).transform, new Vector2(0.06f, 0.72f), new Vector2(0.32f, 0.84f));
		Button optionsAuraButton = CreateButton("Options Open Aura Codex", optionsPanel.transform, builtinResource, "AURE");
		((UnityEvent)optionsAuraButton.onClick).AddListener(new UnityAction(OpenAuraCodexFromOptions));
		SetRect((RectTransform)((Component)optionsAuraButton).transform, new Vector2(0.34f, 0.72f), new Vector2(0.6f, 0.84f));
		Button optionsTutorialButton = CreateImageButton("Options Tutorial", optionsPanel.transform, builtinResource, LoadSpriteResource("UI/tutorial_button"), string.Empty);
		((UnityEvent)optionsTutorialButton.onClick).AddListener(new UnityAction(StartTutorialFromOptions));
		SetRect((RectTransform)((Component)optionsTutorialButton).transform, new Vector2(0.62f, 0.69f), new Vector2(0.94f, 0.86f));
		Button resetHintsButton = CreateButton("Options Reset Hints", optionsPanel.transform, builtinResource, "RESET HINT");
		((UnityEvent)resetHintsButton.onClick).AddListener(new UnityAction(ResetHintsFromOptions));
		SetRect((RectTransform)((Component)resetHintsButton).transform, new Vector2(0.06f, 0.59f), new Vector2(0.94f, 0.68f));
		Text volumeLabel = CreateText("SFX Volume Label", optionsPanel.transform, builtinResource, 17, (FontStyle)1, (TextAnchor)4);
		volumeLabel.text = "VOLUME SFX";
		volumeLabel.color = new Color(0.82f, 0.9f, 0.92f);
		SetRect(volumeLabel.rectTransform, new Vector2(0.06f, 0.49f), new Vector2(0.94f, 0.57f));
		Button sfxDownButton = CreateButton("SFX Volume Down", optionsPanel.transform, builtinResource, "-");
		((UnityEvent)sfxDownButton.onClick).AddListener(new UnityAction(DecreaseSfxVolume));
		SetRect((RectTransform)((Component)sfxDownButton).transform, new Vector2(0.06f, 0.38f), new Vector2(0.25f, 0.48f));
		sfxVolumeText = CreateText("SFX Volume Value", optionsPanel.transform, builtinResource, 19, (FontStyle)1, (TextAnchor)4);
		sfxVolumeText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(sfxVolumeText.rectTransform, new Vector2(0.28f, 0.38f), new Vector2(0.54f, 0.48f));
		Button sfxUpButton = CreateButton("SFX Volume Up", optionsPanel.transform, builtinResource, "+");
		((UnityEvent)sfxUpButton.onClick).AddListener(new UnityAction(IncreaseSfxVolume));
		SetRect((RectTransform)((Component)sfxUpButton).transform, new Vector2(0.57f, 0.38f), new Vector2(0.76f, 0.48f));
		sfxMuteButton = CreateButton("SFX Mute", optionsPanel.transform, builtinResource, "MUTE");
		((UnityEvent)sfxMuteButton.onClick).AddListener(new UnityAction(ToggleSfxMute));
		sfxMuteButtonText = ((Component)sfxMuteButton).GetComponentInChildren<Text>();
		SetRect((RectTransform)((Component)sfxMuteButton).transform, new Vector2(0.79f, 0.38f), new Vector2(0.94f, 0.48f));
		Text musicLabel = CreateText("Music Volume Label", optionsPanel.transform, builtinResource, 17, (FontStyle)1, (TextAnchor)4);
		musicLabel.text = "VOLUME MUSICA";
		musicLabel.color = new Color(0.82f, 0.9f, 0.92f);
		SetRect(musicLabel.rectTransform, new Vector2(0.06f, 0.27f), new Vector2(0.94f, 0.35f));
		Button musicDownButton = CreateButton("Music Volume Down", optionsPanel.transform, builtinResource, "-");
		((UnityEvent)musicDownButton.onClick).AddListener(new UnityAction(DecreaseMusicVolume));
		SetRect((RectTransform)((Component)musicDownButton).transform, new Vector2(0.06f, 0.16f), new Vector2(0.25f, 0.26f));
		musicVolumeText = CreateText("Music Volume Value", optionsPanel.transform, builtinResource, 19, (FontStyle)1, (TextAnchor)4);
		musicVolumeText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(musicVolumeText.rectTransform, new Vector2(0.28f, 0.16f), new Vector2(0.54f, 0.26f));
		Button musicUpButton = CreateButton("Music Volume Up", optionsPanel.transform, builtinResource, "+");
		((UnityEvent)musicUpButton.onClick).AddListener(new UnityAction(IncreaseMusicVolume));
		SetRect((RectTransform)((Component)musicUpButton).transform, new Vector2(0.57f, 0.16f), new Vector2(0.76f, 0.26f));
		musicMuteButton = CreateButton("Music Mute", optionsPanel.transform, builtinResource, "MUTE");
		((UnityEvent)musicMuteButton.onClick).AddListener(new UnityAction(ToggleMusicMute));
		musicMuteButtonText = ((Component)musicMuteButton).GetComponentInChildren<Text>();
		SetRect((RectTransform)((Component)musicMuteButton).transform, new Vector2(0.79f, 0.16f), new Vector2(0.94f, 0.26f));
		Button closeOptionsButton = CreateButton("Close Options", optionsPanel.transform, builtinResource, "CHIUDI");
		((UnityEvent)closeOptionsButton.onClick).AddListener(new UnityAction(ToggleOptionsPanel));
		SetRect((RectTransform)((Component)closeOptionsButton).transform, new Vector2(0.06f, 0.04f), new Vector2(0.47f, 0.14f));
		Button mainMenuButton = CreateButton("Options Main Menu", optionsPanel.transform, builtinResource, "MENU");
		((UnityEvent)mainMenuButton.onClick).AddListener(new UnityAction(ReturnToMainMenuFromOptions));
		SetRect((RectTransform)((Component)mainMenuButton).transform, new Vector2(0.53f, 0.04f), new Vector2(0.94f, 0.14f));
		SetOptionsPanelVisible(false);
		CreateReturnToMenuConfirmation((Transform)(object)safeAreaRoot, builtinResource);
		RefreshSfxOptionsUi();
		RefreshMusicOptionsUi();
		Text text2 = (cpuTitleText = CreateText("CPU Title", (Transform)(object)safeAreaRoot, builtinResource, 25, (FontStyle)1, (TextAnchor)3));
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(text2);
		cpuTitleRect = text2.rectTransform;
		text2.text = (((Object)(object)currentScenario != (Object)null) ?("CPU - IL MASTER   *   " + currentScenario.DisplayName.ToUpperInvariant()) : "CPU - IL MASTER");
		SetRect(text2.rectTransform, new Vector2(0.12f, 0.805f), new Vector2(0.88f, 0.85f));
		((Component)text2).gameObject.SetActive(false);
		cpuRow = CreateCardRow("CPU Formation", (Transform)(object)safeAreaRoot, new Vector2(0.5f, 0.67f));
		roundText = CreateText("Round", (Transform)(object)safeAreaRoot, builtinResource, 20, (FontStyle)1, (TextAnchor)3);
		roundText.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(roundText.rectTransform, new Vector2(0.17f, 0.575f), new Vector2(0.31f, 0.625f));
		((Component)roundText).gameObject.SetActive(false);
		Image image5 = CreateImage("Campaign Card Zones", (Transform)(object)safeAreaRoot, new Color(0.015f, 0.04f, 0.055f, 0.94f));
		StylePanel(image5);
		campaignZoneRect = image5.rectTransform;
		campaignZoneText = CreateText("Campaign Zone Counts", ((Component)image5).transform, builtinResource, 18, (FontStyle)1, (TextAnchor)4);
		Stretch(campaignZoneText.rectTransform, 3f);
		SetRect(campaignZoneRect, new Vector2(0.62f, 0.575f), new Vector2(0.83f, 0.625f));
		((Component)image5).gameObject.SetActive(false);
		CreatePlayerHudView(builtinResource);
		Image image6 = CreateImage("Turn Timeline Background", (Transform)(object)safeAreaRoot, new Color(0.01f, 0.025f, 0.035f, 0.88f));
		StylePanel(image6);
		timelineBackgroundRect = image6.rectTransform;
		SetRect(image6.rectTransform, new Vector2(0.18f, 0.865f), new Vector2(0.82f, 0.91f));
		initiativeTimelineRoot = new GameObject("Turn Timeline", new Type[1]
		{
			typeof(RectTransform)
		}).GetComponent<RectTransform>();
		((Transform)initiativeTimelineRoot).SetParent(((Component)image6).transform, false);
		Stretch(initiativeTimelineRoot, 4f);
		Image image7 = CreateImage("Message Panel", (Transform)(object)safeAreaRoot, new Color(0.015f, 0.025f, 0.04f, 0.56f));
		StylePanel(image7);
		messagePanelRect = image7.rectTransform;
		SetRect(image7.rectTransform, new Vector2(0.25f, 0.41f), new Vector2(0.75f, 0.555f));
		messageText = CreateText("Battle Log", ((Component)image7).transform, builtinResource, 22, (FontStyle)0, (TextAnchor)4);
		messageText.color = new Color(0.88f, 0.92f, 0.96f);
		SetRect(messageText.rectTransform, new Vector2(0.035f, 0.06f), new Vector2(0.65f, 0.66f));
		turnBannerImage = CreateImage("Current Turn Banner", ((Component)image7).transform, configuration.Visual.PlayerTurnColor);
		StylePanel(turnBannerImage);
		SetRect(turnBannerImage.rectTransform, new Vector2(0.1825f, 0.69f), new Vector2(0.8175f, 0.98f));
		turnBannerText = CreateText("Current Turn", ((Component)turnBannerImage).transform, builtinResource, 24, (FontStyle)1, (TextAnchor)4);
		turnBannerText.text = "PREPARAZIONE";
		Stretch(turnBannerText.rectTransform, 4f);
		restartButton = CreateButton("Restart", ((Component)image7).transform, builtinResource, "RICOMINCIA");
		((UnityEvent)restartButton.onClick).AddListener(new UnityAction(HandlePrimaryAction));
		restartButtonText = ((Component)restartButton).GetComponentInChildren<Text>();
		((Component)restartButton).gameObject.SetActive(false);
		RectTransform val2 = (RectTransform)((Component)restartButton).transform;
		val2.anchorMin = new Vector2(0.69f, 0.14f);
		val2.anchorMax = new Vector2(0.97f, 0.58f);
		val2.offsetMin = Vector2.zero;
		val2.offsetMax = Vector2.zero;
		confirmActionButton = CreateButton("Confirm Action", ((Component)image7).transform, builtinResource, "CONFERMA");
		((UnityEvent)confirmActionButton.onClick).AddListener(new UnityAction(HandleConfirmAction));
		confirmActionButtonText = ((Component)confirmActionButton).GetComponentInChildren<Text>();
		((Component)confirmActionButton).gameObject.SetActive(false);
		RectTransform val3 = (RectTransform)((Component)confirmActionButton).transform;
		val3.anchorMin = new Vector2(0.67f, 0.16f);
		val3.anchorMax = new Vector2(0.97f, 0.84f);
		val3.offsetMin = Vector2.zero;
		val3.offsetMax = Vector2.zero;
		cancelActionButton = CreateButton("Cancel Pending Action", ((Component)image7).transform, builtinResource, "ANNULLA");
		((UnityEvent)cancelActionButton.onClick).AddListener(new UnityAction(CancelPendingAction));
		((Component)cancelActionButton).gameObject.SetActive(false);
		RectTransform val4 = (RectTransform)((Component)cancelActionButton).transform;
		val4.anchorMin = new Vector2(0.37f, 0.16f);
		val4.anchorMax = new Vector2(0.64f, 0.84f);
		val4.offsetMin = Vector2.zero;
		val4.offsetMax = Vector2.zero;
		abilityButton = CreateButton("Class Ability", ((Component)image7).transform, builtinResource, "ABILITA");
		((UnityEvent)abilityButton.onClick).AddListener(new UnityAction(ActivateCurrentAbility));
		((Component)abilityButton).gameObject.SetActive(false);
		RectTransform val5 = (RectTransform)((Component)abilityButton).transform;
		val5.anchorMin = new Vector2(0.69f, 0.51f);
		val5.anchorMax = new Vector2(0.97f, 0.84f);
		val5.offsetMin = Vector2.zero;
		val5.offsetMax = Vector2.zero;
		attachmentButton = CreateButton("Attachment", ((Component)image7).transform, builtinResource, "POTENZIA");
		((UnityEvent)attachmentButton.onClick).AddListener(new UnityAction(ActivateCurrentAttachment));
		((Component)attachmentButton).gameObject.SetActive(false);
		attachmentButtonText = ((Component)attachmentButton).GetComponentInChildren<Text>();
		RectTransform val6 = (RectTransform)((Component)attachmentButton).transform;
		val6.anchorMin = new Vector2(0.69f, 0.16f);
		val6.anchorMax = new Vector2(0.97f, 0.49f);
		val6.offsetMin = Vector2.zero;
		val6.offsetMax = Vector2.zero;
		merchantBuyButton = CreateButton("Merchant Buy", ((Component)image7).transform, builtinResource, "MERCATO");
		((UnityEvent)merchantBuyButton.onClick).AddListener(new UnityAction(OpenMerchantPanel));
		((Component)merchantBuyButton).gameObject.SetActive(false);
		merchantBuyButtonText = ((Component)merchantBuyButton).GetComponentInChildren<Text>();
		RectTransform val7 = (RectTransform)((Component)merchantBuyButton).transform;
		val7.anchorMin = new Vector2(0.69f, 0.54f);
		val7.anchorMax = new Vector2(0.97f, 0.92f);
		val7.offsetMin = Vector2.zero;
		val7.offsetMax = Vector2.zero;
		Text text3 = (playerTitleText = CreateText("Player Title", (Transform)(object)safeAreaRoot, builtinResource, 25, (FontStyle)1, (TextAnchor)3));
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(text3);
		playerTitleRect = text3.rectTransform;
		text3.text = "LA TUA FORMAZIONE";
		SetRect(text3.rectTransform, new Vector2(0.12f, 0.32f), new Vector2(0.88f, 0.38f));
		playerRow = CreateCardRow("Player Formation", (Transform)(object)safeAreaRoot, new Vector2(0.5f, 0.17f));
		playerHandRow = CreateCardRow("Player Hand", (Transform)(object)safeAreaRoot, new Vector2(0.5f, 0.08f));
		CreateCombatResultView(builtinResource);
		CreateMerchantView(((Component)val).transform, builtinResource);
		CreateImplementationArchiveView(((Component)val).transform, builtinResource);
		CreateDeckBuilderView(builtinResource);
		CreateInitialDraftView(builtinResource);
		CreateRoomChoiceView(((Component)val).transform, builtinResource);
		CreateCardInspectionOverlay(((Component)val).transform, builtinResource);
		CreateRoomTransitionOverlay(((Component)val).transform);
		CreateModeSelectionView(((Component)val).transform, builtinResource);
		CreateCampaignModeSelectionView(builtinResource);
		CreateHintOverlay((Transform)(object)safeAreaRoot, builtinResource);
		CreateAuraCodexView(((Component)val).transform, builtinResource);
		RefreshPlayerHud();
		RefreshCpuHud();
		RefreshRoomHud("PREPARAZIONE", (((Object)(object)currentScenario != (Object)null) ?currentScenario.DisplayName.ToUpperInvariant() : "SCENARIO"));
		ApplyResponsiveLayout();
	}


	private List<CardDefinition> GetCampaignRewardPool()
	{
		List<CardDefinition> list = new List<CardDefinition>();
		foreach (CardDefinition card in cardDatabase.Cards)
		{
			if (!((Object)(object)card == (Object)null) && card.Category == CardCategory.Monster && card.CanEnterCombat && (campaignDeck == null || !campaignDeck.ContainsEquivalentDefinition(card)))
			{
				list.Add(card);
			}
		}
		return list;
	}

	private bool TryAddCardToPlayerCollection(CardDefinition cardDefinition)
	{
		if ((Object)(object)cardDefinition == (Object)null)
		{
			return false;
		}
		if (campaignDeck != null && campaignDeck.AddCard(cardDefinition) == null)
		{
			return false;
		}
		playerReserve.Add(cardDefinition);
		initialPlayerReserve.Add(cardDefinition);
		return true;
	}

	private (string description, int bonusExperience) ResolveOpportunity(int roll)
	{
		switch (roll)
		{
		case 1:
			return GrantRandomRewardCard("DONO DEL MASTER");
		case 2:
			return (description: $" Jackpot! +{configuration.Progression.OpportunityExperienceJackpot} EXP.", bonusExperience: configuration.Progression.OpportunityExperienceJackpot);
		case 3:
			skipNextCombatCooldown = true;
			return (description: " Le carte schierate nella prossima vittoria non entreranno in cooldown.", bonusExperience: 0);
		case 4:
			return (description: " Evento misterioso: il presagio dello scenario si intensifica.", bonusExperience: 0);
		case 5:
			return RecoverRandomGraveyardCard();
		case 6:
			nextCombatFallenHeroesGrantExperience = true;
			return (description: " Nel prossimo combattimento gli eroi caduti valgono EXP pari alla loro forza.", bonusExperience: 0);
		case 7:
			return ResolveMasterChallenge();
		case 8:
			return GrantRandomConsumable("PROVA CONSUMABILE");
		case 9:
			nextCombatAssassinsActLast = true;
			return (description: " Nel prossimo combattimento gli Assassini partiranno ultimi in iniziativa.", bonusExperience: 0);
		case 10:
			nextCombatTankDuel = true;
			return (description: " Nel prossimo combattimento i Paladini aprono il duello: +2 ai tuoi, -1 alla CPU.", bonusExperience: 0);
		case 11:
			nextCombatWarriorsLowerVigor = true;
			return (description: " Nel prossimo combattimento tutti i Guerrieri useranno un dado Vigore inferiore.", bonusExperience: 0);
		case 12:
			nextMonsterTierBonus = Math.Max(nextMonsterTierBonus, 1);
			return (description: " Presagio oscuro: il prossimo mostro sara di un tier piu alto.", bonusExperience: 0);
		default:
			return (description: " Nessun effetto.", bonusExperience: 0);
		}
	}

	private (string description, int bonusExperience) GrantRandomRewardCard(string source)
	{
		List<CardDefinition> campaignRewardPool = GetCampaignRewardPool();
		if (campaignRewardPool.Count == 0)
		{
			return (description: " " + source + ": nessuna carta disponibile.", bonusExperience: 0);
		}
		CardDefinition cardDefinition = formationDraftService.DrawCandidates(campaignRewardPool, 1)[0];
		if (!TryAddCardToPlayerCollection(cardDefinition))
		{
			return (description: " " + source + ": nessuna carta nuova disponibile.", bonusExperience: 0);
		}
		string displayName = CardDisplayNames.MarketName(cardDefinition);
		AppendLog(source + " - " + displayName);
		return (description: " " + source + ": ottieni " + displayName + ".", bonusExperience: 0);
	}

	private (string description, int bonusExperience) RecoverRandomGraveyardCard()
	{
		if (campaignDeck == null)
		{
			return GrantRandomRewardCard("SCAMBIO DELLA FORMAZIONE");
		}
		List<CampaignCardInstance> list = campaignDeck.Cards.Where((CampaignCardInstance card) => card.Zone == CampaignCardZone.Graveyard).ToList();
		if (list.Count == 0)
		{
			return GrantRandomRewardCard("SCAMBIO DELLA FORMAZIONE");
		}
		CampaignCardInstance campaignCardInstance = list[random.NextInclusive(0, list.Count - 1)];
		campaignDeck.RecoverFromGraveyard(campaignCardInstance);
		string displayName = CardDisplayNames.MarketName(campaignCardInstance.Definition);
		AppendLog("RECUPERO - " + displayName + " torna nel mazzo.");
		return (description: " Recuperi " + displayName + " dal cimitero.", bonusExperience: 0);
	}

	private (string description, int bonusExperience) ResolveMasterChallenge()
	{
		int num = random.NextInclusive(1, runProgress.PlayerVigorDieSides);
		int num2 = random.NextInclusive(1, runProgress.MasterVigorDieSides);
		AppendLog($"SFIDA MASTER - TU D{runProgress.PlayerVigorDieSides}={num}, MASTER D{runProgress.MasterVigorDieSides}={num2}");
		if (num < num2)
		{
			return (description: $" Sfida del Master persa ({num} vs {num2}): nessun premio.", bonusExperience: 0);
		}
		int num3 = Math.Max(10, configuration.Progression.OpportunityExperienceJackpot / 2);
		return (description: $" Sfida del Master vinta ({num} vs {num2}): +{num3} EXP.", bonusExperience: num3);
	}

	private (string description, int bonusExperience) RevealCampaignScenario()
	{
		if (!string.IsNullOrWhiteSpace(campaignScenarioId))
		{
			return (description: $" Scenario gia rivelato: {ActiveCampaignScenarioLabel()} resta attivo fino a fine campagna.", bonusExperience: 0);
		}
		if ((Object)(object)scenarioCatalog == (Object)null)
		{
			scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		}
		if ((Object)(object)scenarioCatalog == (Object)null)
		{
			AppendLog("SCENARIO CAMPAGNA - catalogo scenari non trovato.");
			return (description: " Evento misterioso: nessuno scenario disponibile.", bonusExperience: 0);
		}
		List<ScenarioDefinition> candidates = scenarioCatalog.Scenarios
			.Where((ScenarioDefinition scenario) => (Object)(object)scenario != (Object)null
				&& scenario.RoomType == RoomType.Boss
				&& (Object)(object)FindCardDefinition(scenario.BossId) != (Object)null)
			.ToList();
		if (candidates.Count == 0)
		{
			AppendLog("SCENARIO CAMPAGNA - nessuno scenario Boss con carta boss configurata.");
			return (description: " Evento misterioso: nessuno scenario disponibile.", bonusExperience: 0);
		}
		ScenarioDefinition selected = candidates[random.NextInclusive(0, candidates.Count - 1)];
		campaignScenarioId = selected.Id;
		campaignScenarioBossId = selected.BossId;
		string label = string.IsNullOrWhiteSpace(selected.DisplayName) ? selected.Id : selected.DisplayName;
		AppendLog($"SCENARIO CAMPAGNA RIVELATO - {label}, boss {campaignScenarioBossId}.");
		return (description: $" Scenario rivelato: {label}. I suoi effetti restano attivi fino a fine campagna.", bonusExperience: 0);
	}

	private void ResetBattle()
	{
		((MonoBehaviour)this).StopAllCoroutines();
		ClearDraftEntranceState();
		abilityTargetMode = AbilityTargetMode.None;
		activeAbilityUser = null;
		selectedPlayerIndex = -1;
		inputLocked = false;
		gameFinished = false;
		((Component)restartButton).gameObject.SetActive(false);
		((Component)abilityButton).gameObject.SetActive(false);
		((Component)merchantBuyButton).gameObject.SetActive(false);
		ConfigureActionButtonLayout(merchantVisible: false);
		foreach (BattleCardState playerCard in playerCards)
		{
			Object.Destroy((Object)(object)((Component)playerCard.View).gameObject);
		}
		foreach (BattleCardState cpuCard in cpuCards)
		{
			Object.Destroy((Object)(object)((Component)cpuCard.View).gameObject);
		}
		playerCards.Clear();
		cpuCards.Clear();
		turnOrder.Clear();
		playerReserve.Clear();
		playerReserve.AddRange(initialPlayerReserve);
		for (int i = 0; i < initialPlayerFormation.Count; i++)
		{
			AddCard(playerCards, playerRow, initialPlayerFormation[i], belongsToPlayer: true, i, (i < initialPlayerCampaignFormation.Count) ?initialPlayerCampaignFormation[i] : null);
		}
		for (int j = 0; j < initialCpuFormation.Count; j++)
		{
			AddCard(cpuCards, cpuRow, initialCpuFormation[j], belongsToPlayer: false, j);
		}
		ApplyResponsiveLayout();
		StartBattle();
	}

	private void UpdateInteractions()
	{
		foreach (BattleCardState playerCard in playerCards)
		{
			playerCard.View.SetInteractable(CanUsePlayerCardAction(playerCard) || CanInspectBattleCard(playerCard));
		}
		for (int i = 0; i < cpuCards.Count; i++)
		{
			cpuCards[i].View.SetInteractable(CanUseCpuCardAction(i) || CanInspectBattleCard(cpuCards[i]));
		}
		RefreshCardActionOverlays();
	}

	private static bool HasAliveCard(IEnumerable<BattleCardState> cards)
	{
		foreach (BattleCardState card in cards)
		{
			if (!card.Eliminated)
			{
				return true;
			}
		}
		return false;
	}

}
}
