using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Localization;
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
	private const string JurinashorBossCardId = "boss-jurinashor";
	private const string JurinashorSwordCardId = "boss-jurinashor-sword";
	private const string SeraphelBossCardId = "boss-seraphel";
	private const string SeraphelPhaseTwoCardId = "boss-seraphel-phase-2";
	private const string PalatirBossCardId = "boss-palatir";
	private const string MinibossGolemDebugSceneName = "MinibossGolemDebug";
	private const int MinibossGolemDebugPlayerLevel = 4;
	private const string MedusaDebugSceneName = "MedusaDebug";
	private const string BossDebugSceneName = "BossDebug";
	private const string PromotionalTrailerSceneName = "PromotionalTrailer";
	private const string MerchantDebugSceneName = "MerchantDebug";
	private const string LootRoomDebugSceneName = "LootRoomDebug";
	private const string ClassChoiceDebugSceneName = "ClassChoiceDebug";

	private const string MageVigorConstellationDebugSceneName = "MageVigorConstellationDebug";
	private const string DiceRollDebugSceneName = "DiceRollDebug";
	private const string LoginScreenPrototypeSceneName = "LoginScreenPrototype";
	private const string PlayerHudNamePrefsKey = "AccardND.PlayerHudName";
	// Il tutorial non paga piu' miele (quello arriva solo dalle quest della taverna): regala
	// il primo capitolo, cosi' chi esce dal tutorial puo' giocare subito.
	private const string TutorialCompletionChapterId = "chapter-1";
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
	private string campaignRunRewardId;
	private string pendingCampaignRewardClaimId;
	private int pendingCampaignRewardBaseAccountExperience;
	private bool pendingCampaignRewardAdClaimed;

	/// <summary>
	/// La reward di questa run esiste sul server (o ci arrivera' col replay dell'outbox),
	/// quindi il x3 saltato si ritrova nei messaggi del profilo. E' falso quando il server
	/// ha rifiutato la reward: li' non c'e' niente da recuperare, e mandare il giocatore a
	/// cercare una comunicazione che non arrivera' e' peggio del silenzio.
	/// </summary>
	private bool pendingCampaignRewardRecoverable;
	private GameObject campaignDefeatRewardPopup;
	private RectTransform campaignDefeatPetals;
	private Text campaignDefeatRewardBodyText;
	private Button campaignDefeatRewardDoubleButton;
	private Text campaignDefeatRewardDoubleButtonText;

	// Il mercato espone tre banchi mutuamente esclusivi: al primo acquisto quelli non scelti
	// restano chiusi per il resto della stanza. Vendita e recupero carte restano sempre attivi.
	private enum MerchantBranch
	{
		None,
		Cards,
		Items,
		Upgrades
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
		RogueSupremeEnemy,
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

	private enum CampaignConsumableType
	{
		Detector,
		SecondChance,
		Empower,
		SigilloRubino,
		DoubleExp,
		ManaGain5,
		ManaGain10,
		Jolly
	}

	private enum MinibossKind
	{
		ComposableGolem,
		Medusa
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
		public CampaignRoomRoll? RevealedRoom { get; }

		public CampaignDoor(CampaignRoomRoll? revealedRoom = null)
		{
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

		public string ScenarioId { get; }

		public RoomDifficulty Difficulty { get; }

		public CampaignRoomRoll(RoomType roomType, string scenarioId, RoomDifficulty difficulty)
		{
			RoomType = roomType;
			ScenarioId = scenarioId;
			Difficulty = difficulty;
		}
	}

	private sealed class BattleCardState
	{
		public CardDefinition Definition { get; private set; }

		public CombatCard Card { get; private set; }

		public PrototypeCardView View { get; }

		public bool BelongsToPlayer { get; }

		public CampaignCardInstance CampaignCard { get; }

		public int Initiative { get; set; }

		/// <summary>
		/// Il bonus dei talenti gia' assegnato al dado di questa pedina. Viaggia con la
		/// pedina invece di essere ricalcolato dalla sua posizione in fila: e' il numero
		/// che il giocatore ha visto accendersi sul dado durante lo schieramento.
		/// </summary>
		public int InitiativeTalentBonus { get; set; }

		/// <summary>"Apertura": questa pedina agisce per prima, qualunque sia il tiro.</summary>
		public bool OpensTheFight { get; set; }

		public int TieBreaker { get; set; }

		public bool Eliminated { get; set; }

		public bool AbilityArmed { get; set; }

		public bool AbilityUsed { get; set; }

		public bool AbilityUsedThisTurn { get; set; }

		public bool SupremeUsedThisTurn { get; set; }

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

		/// <summary>Ha gia ricevuto il bonus di una pedina sacrificata in questa battaglia.</summary>
		public bool HasEquipment { get; set; }

		/// <summary>
		/// Invisibilita' dell'Assassino: non selezionabile come bersaglio finche' non
		/// resta l'unica pedina attiva del suo schieramento. E' un buff, quindi la
		/// Purificazione del Sacerdote la rimuove.
		/// </summary>
		public bool IsUntargetable { get; set; }

		public int NecromancerMinions { get; set; }

		public bool Petrified { get; set; }

		public int SeraphelSeals { get; set; }

		public BattleCardState MarkedTarget { get; set; }

		public HashSet<BattleCardState> HunterMarkedTargets { get; } = new HashSet<BattleCardState>();

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
			if (campaignCard != null)
			{
				PermanentCombatBonus = campaignCard.PermanentItemBonus;
				// Gli upgrade della forgia/mercante non sono equipaggiamenti. HasEquipment
				// viene attivato soltanto quando una pedina viene sacrificata in battaglia.
				HasEquipment = false;
			}
		}

		public void TransformSeraphel(CardDefinition definition, int strength)
		{
			Definition = definition;
			Card = new CombatCard(definition.Id, definition.DisplayName, HeroClass.Priest, strength);
			View?.SetCardArtwork(definition);
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

		public string IconStatus { get; }

		public InspectionStatusDetail(string label, string description, Color color, string iconStatus = null)
		{
			Label = label;
			Description = description;
			Color = color;
			IconStatus = iconStatus;
		}
	}

	private sealed class DeploymentToken
	{
		public bool BelongsToPlayer { get; }

		public int Initiative { get; }

		public int TieBreaker { get; }

		public CardDefinition DeployedCard { get; set; }

		/// <summary>
		/// Il bonus dei talenti d'iniziativa che spetta a questo dado. Sta qui e non
		/// si ricava dall'ordine di schieramento perche' e' proprio quell'ordine che
		/// il bonus cambia: legarlo allo slot della fila voleva dire darlo sempre al
		/// tiro piu' basso, cioe' ribaltare la timeline a ogni combattimento.
		/// </summary>
		public int TalentInitiativeBonus { get; set; }

		/// <summary>"Apertura": questo dado batte qualunque numero in campo.</summary>
		public bool OpensTheFight { get; set; }

		/// <summary>Il numero che conta per l'ordine: il tiro piu' il bonus dei talenti.</summary>
		public int EffectiveInitiative => Initiative + TalentInitiativeBonus;

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

	/// <summary>
	/// I token schierati dal giocatore, nello stesso ordine delle iniziative qui sopra:
	/// servono a portare in battaglia il dado com'era nella timeline - bonus dei talenti
	/// e tie-breaker con cui le parita' sono gia' state sciolte a schermo.
	/// </summary>
	private readonly List<DeploymentToken> selectedPlayerDeploymentTokens = new List<DeploymentToken>();

	/// <summary>Gli stessi token, lato CPU: le parita' si sciolgono anche contro di lei.</summary>
	private readonly List<DeploymentToken> selectedCpuDeploymentTokens = new List<DeploymentToken>();

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
	private int draftEntranceAnimationVersion;
	private int activeDraftEntranceCards;

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

	private Image deckBuilderInnerBackgroundImage;

	private Image deckBuilderFrameImage;

	private AspectRatioFitter deckBuilderFrameAspectFitter;

	private Image deckBuilderTitlePanel;

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

	private RectTransform deckBuilderClassGridRoot;

	private readonly List<RectTransform> deckBuilderClassOptionRects = new List<RectTransform>();

	private readonly List<Button> deckBuilderClassOptionButtons = new List<Button>();

	private readonly List<Image> deckBuilderClassOptionImages = new List<Image>();

	private readonly List<HeroClass> deckBuilderClassOptionClasses = new List<HeroClass>();

	private Image deckBuilderStrengthImage;

	private RectTransform deckBuilderStrengthButtonRect;

	private RectTransform deckBuilderStrengthPreviousButtonRect;

	private RectTransform deckBuilderStrengthNextButtonRect;

	private Text deckBuilderStrengthBuyText;

	private GameObject deckBuilderToastRoot;

	private RectTransform deckBuilderToastRect;

	private Text deckBuilderToastText;

	private Coroutine deckBuilderToastRoutine;

	private Button startCampaignButton;

	private RectTransform startCampaignButtonRect;

	private Button prepareBagButton;

	private RectTransform prepareBagButtonRect;

	private bool deckBuilderPreparingBag;

	private bool deckBuilderBagSaving;

	private readonly List<string> deckBuilderSelectedBagItems = new List<string>();

	private readonly List<GameObject> deckBuilderBagItemViews = new List<GameObject>();

	private RectTransform deckBuilderSelectedBagRoot;

	private Text deckBuilderSelectedBagEmptyText;

	private Text deckBuilderBagEffectText;

	private readonly List<GameObject> deckBuilderSelectedBagViews = new List<GameObject>();

	private GameObject modeSelectionPanel;

	private Image modeSelectionImage;

	private AspectRatioFitter modeSelectionAspectFitter;

	private Button modeSelectionCampaignButton;

	private Button modeSelectionMultiplayerButton;

	private Button modeSelectionSanctuaryButton;

	private Button modeSelectionLibraryButton;

	private Button modeSelectionShopButton;

	private Button modeSelectionTavernButton;

	private Button modeSelectionProfileButton;

	private Button modeSelectionHallOfFameButton;
	private Text modeSelectionPvpLeaderText;
	private Text modeSelectionCampaignLeaderText;

	private readonly List<Button> modeSelectionHotspotButtons = new List<Button>();
	private readonly Dictionary<Button, RectTransform> modeSelectionHotspotRects = new Dictionary<Button, RectTransform>();
	private Image accountBannerImage;
	private RectTransform accountBannerPortraitRoot;
	private Image accountBannerPortraitImage;
	private Image accountBannerExperienceFill;
	private Text accountBannerNameText;
	private Text accountBannerLevelText;
	private Text accountBannerExperienceText;
	private readonly Text[] accountBannerInfoTexts = new Text[3];
	private Image accountHoneyPanelImage;
	private Text accountHoneyAmountText;

	private Coroutine campaignHubZoomRoutine;

	private GameObject campaignHubCinematicOverlay;

	private GameObject campaignModeSelectionPanel;

	private Image campaignModeSelectionFrameImage;

	private AspectRatioFitter campaignModeSelectionFrameAspectFitter;

	private Image campaignModeSelectionTitlePanel;

	private Text campaignModeSelectionHeadingText;

	private Button campaignModeAdventureButton;

	private RectTransform campaignModeBuilderButtonRect;

	private Button campaignModeHardcoreButton;

	private Text campaignModeHardcoreButtonText;

	private GameObject campaignHardcorePurchaseConfirmation;

	private Text campaignHardcorePurchaseConfirmationText;

	private Image campaignModeHardcoreEmblemImage;

	private AccardND.PvpUi.PvpUiVfx campaignModeHardcoreVfx;

	private RectTransform campaignModeDraftButtonRect;

	private Button campaignModeBackButton;

	private GameObject adventureChapterPanel;

	private Image adventureChapterInnerBackgroundImage;

	private Image adventureChapterFrameImage;

	private AspectRatioFitter adventureChapterFrameAspectFitter;

	private Image adventureChapterTitlePanel;

	private Text adventureChapterHeadingText;

	private RectTransform adventureChapterListRoot;

	private Button adventureChapterBackButton;

	private readonly List<GameObject> adventureChapterRows = new List<GameObject>();
	private System.Threading.Tasks.Task pendingAdventureChapterClearTask = System.Threading.Tasks.Task.CompletedTask;
	private bool pendingAdventureChapterTalentPointsReward;
	private bool pendingAdventureChapterClassReward;
	private bool pendingAdventureNextChapterReward;
	private System.Threading.Tasks.Task pendingCampaignRewardTask = System.Threading.Tasks.Task.CompletedTask;
	private GameObject classChoicePopup;
	private RectTransform classChoiceButtonsRoot;
	private Text classChoiceStatusText;
	private readonly List<GameObject> classChoiceButtonViews = new List<GameObject>();

	/// <summary>La scelta è già stata fatta: il popup resta chiuso e non si ridisegna.</summary>
	private bool classChoiceSubmitted;

	private GameObject adventureTutorialConfirmPopup;
	private RectTransform adventureTutorialConfirmDialog;

	private Text adventureTutorialConfirmTitleText;

	private Text adventureTutorialConfirmBodyText;

	private Image adventureRewardClassImage;

	private Image adventureRewardChapterImage;

	private Action adventureConfirmAction;

	private bool adventureScriptedTutorialActive;

	private int adventureScriptedTutorialStep;

	private bool adventureScriptedTutorialStepAcknowledged;

	private RectTransform adventureScriptedTutorialPendingTarget;

	private int adventureScriptedTutorialAllowedDraftIndex = -1;

	private bool adventureScriptedTutorialInspectionOpened;

	private bool adventureScriptedTutorialAwaitingAdvantageContinue;

	private bool adventureScriptedTutorialObjectiveShown;

	private GameObject adventureScriptedTutorialPanel;

	private Text adventureScriptedTutorialTitleText;

	private Text adventureScriptedTutorialBodyText;

	private Text adventureScriptedTutorialStepText;

	private Button adventureScriptedTutorialNextButton;

	private Coroutine adventureScriptedTutorialTextRoutine;

	private string adventureScriptedTutorialBodyFullText = string.Empty;

	private Image adventureScriptedTutorialSpotlight;

	private readonly List<Image> adventureScriptedTutorialDimmers = new List<Image>();

	private GameObject multiplayerPopup;

	private GameObject roomChoicePanel;

	private Image roomChoiceImage;

	private AspectRatioFitter roomChoiceAspectFitter;

	private Text roomChoiceCounterText;

	private int roomChoiceBackgroundIndex = 1;

	private Button roomChoiceLeftButton;

	private Button roomChoiceCenterButton;

	private Button roomChoiceRightButton;

	private readonly List<Text> roomChoiceRevealLabels = new List<Text>();

	private readonly List<CampaignDoor> campaignDoors = new List<CampaignDoor>();

	private HeroClass deckBuilderSelectedClass = HeroClass.Warrior;

	private int deckBuilderSelectedStrength = 2;

	/// <summary>Scarto applicato al seme per dare alla CPU un flusso casuale indipendente dai dadi.</summary>
	private const int CpuDecisionSeedOffset = 0x5C9A7;

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

	// Il messaggio e il banner restano visibili anche mentre si apre Opzioni.
	// Conserviamo la sorgente, non il risultato già tradotto, così un cambio
	// lingua può renderizzarli di nuovo senza cambiare lo stato del combattimento.
	private string currentBattlefieldMessage;
	private string currentTurnBannerLabel;

	private Image turnBannerImage;

	private Text turnBannerText;

	private RectTransform initiativeTimelineRoot;

	private RectTransform timelineBackgroundRect;

	private Text timelineCountdownText;

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

	private AccardND.PvpUi.PvpUiVfx merchantOpenButtonPulseVfx;

	private AccardND.PvpUi.PvpUiVfx merchantContinueButtonPulseVfx;

	private GameObject merchantPanel;

	private RectTransform merchantDeckCardsRoot;

	private RectTransform merchantGraveyardCardsRoot;

	private Text merchantDeckEmptyText;

	private Text merchantGraveyardEmptyText;

	private Button merchantDeckTabButton;

	private Text merchantDeckTabText;

	private Button merchantGraveyardTabButton;

	private Text merchantGraveyardTabText;

	private bool merchantShowingGraveyard;

	private AccardND.PvpUi.PvpUiVfx merchantDeckTabVfx;

	private AccardND.PvpUi.PvpUiVfx merchantGraveyardTabVfx;

	private Text merchantStatusText;

	private Text merchantSellText;

	private Button merchantSellButton;

	private Button merchantRecoverButton;

	private Button merchantUpgradeButton;

	private Button merchantCardsTabButton;

	private Button merchantItemsTabButton;

	private Button merchantUpgradesTabButton;

	private Text merchantCardsTabText;

	private Text merchantItemsTabText;

	private Text merchantUpgradesTabText;

	private Image merchantCardsTabLockImage;

	private Image merchantItemsTabLockImage;

	private Image merchantUpgradesTabLockImage;

	private AccardND.PvpUi.PvpUiVfx merchantCardsTabVfx;

	private AccardND.PvpUi.PvpUiVfx merchantItemsTabVfx;

	private AccardND.PvpUi.PvpUiVfx merchantUpgradesTabVfx;

	private RectTransform merchantShelfRoot;

	private readonly List<GameObject> merchantShelfViews = new List<GameObject>();

	private readonly List<MerchantCardOffer> merchantCardOffers = new List<MerchantCardOffer>();

	private readonly List<MerchantItemOffer> merchantItemOffers = new List<MerchantItemOffer>();

	private MerchantBranch merchantVisibleBranch = MerchantBranch.Cards;

	private MerchantBranch merchantLockedBranch = MerchantBranch.None;

	private int merchantStockRoomKey = -1;

	private readonly List<PrototypeCardView> merchantOwnedCardViews = new List<PrototypeCardView>();

	private CampaignCardInstance selectedMerchantSaleCard;

	private Text playerTitleText;

	private Text cpuTitleText;

	private Text restartButtonText;

	private ScreenFadeTransition roomTransition;

	private Button logButton;

	private Text settingsButtonLabel;

	private Sprite settingsButtonSprite;

	private Sprite hubButtonSprite;

	private Sprite accountHeaderSettingsSprite;

	private Button accountHeaderHubButton;

	private Button accountHeaderSettingsButton;

	private GameObject logPanel;

	private Text logText;

	private GameObject optionsPanel;

	private GameObject optionsBackdropPanel;

	private Button optionsMainMenuButton;

	/// <summary>
	/// Etichetta del bottone in basso a sinistra delle opzioni: "MENU" in campagna,
	/// "ARRENDITI" durante una partita PvP, dove abbandonare significa perdere.
	/// </summary>
	private Text optionsMainMenuButtonText;

	private GameObject returnToMenuConfirmPanel;

	private Text returnToMenuTitleText;

	private Text returnToMenuBodyText;

	private Text returnToMenuConfirmButtonText;

	/// <summary>true quando la conferma aperta è quella della resa, non del ritorno al menu.</summary>
	private bool returnToMenuConfirmIsSurrender;

	private GameObject logoutConfirmPanel;

	private Text sfxVolumeText;

	private Slider sfxVolumeSlider;

	private Button sfxMuteButton;

	private Text sfxMuteButtonText;

	private Text musicVolumeText;

	private Slider musicVolumeSlider;

	private Button musicMuteButton;

	private Text musicMuteButtonText;

	private Button implementationArchiveButton;

	private Text implementationArchiveButtonLabel;
	private Text implementationArchiveGoldText;

	private GameObject implementationArchivePanel;

	private GameObject implementationArchiveBackdropPanel;

	private RectTransform implementationArchiveButtonRect;

	private RectTransform implementationArchivePanelRect;

	private RectTransform implementationDeckRoot;

	private RectTransform implementationGraveyardRoot;

	private Text implementationDeckEmptyText;

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

	private int suppressCardInspectionUntilFrame = -1;

	private int suppressPaladinTargetSelectionUntilFrame = -1;

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

	private UnityAction inspectedPvpLoadoutAddAction;

	private bool inspectedPvpLoadoutActive;

	private bool inspectedCampaignConsumableActive;

	private CampaignConsumableType inspectedCampaignConsumableType;

	private bool rubySealTargetSelectionActive;
	private GameObject rubySealTargetPanel;
	private RectTransform rubySealTargetCardsRoot;
	private readonly List<PrototypeCardView> rubySealTargetCardViews = new List<PrototypeCardView>();

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
	// Nelle stanze boss la HUD di combattimento resta nascosta durante la scelta
	// della formazione e il tiro iniziativa: compare insieme al boss, non prima.
	private bool waitingForCampaignBossReveal;

	private bool deploymentInitiativesReady;

	private int currentDeploymentIndex;

	private int pendingDeploymentIndex = -1;

	private bool canAdvanceToNextRoom;

	private bool canRetryCampaignRoom;

	// Valorizzato esclusivamente da "Riprova stanza": impedisce che il nuovo
	// schieramento di campagna riproponga la stessa terna di iniziative al giocatore.
	private int[] campaignRetryPreviousPlayerInitiatives;

	private bool returningToStartAfterGameOver;

	private RoomType currentRoomType = RoomType.Monster;

	private string pendingScenarioId;

	private RoomDifficulty pendingRoomDifficulty = RoomDifficulty.Normal;

	private string campaignScenarioId;

	private string campaignScenarioBossId;

	// Capitolo Avventura da cui e' partita la run. Nullo per le run non avviate da un
	// capitolo: serve a mandare al server il vero id capitolo nel sommario di fine run.
	private string activeAdventureChapterId;

	// Boss e miniboss sconfitti in questa run, in ordine di sconfitta.
	private readonly List<string> defeatedBossIdsInRun = new List<string>();

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

	private int nextMonsterDifficultyIncrease;

	private bool nextDoorChoiceRevealed;

	private bool nextRoomEmpowered;

	private bool nextRoomDoubleExperience;

	private CampaignConsumableState campaignConsumables = new CampaignConsumableState();

	private ComposableGolem activeComposableGolem;

	private ComposableGolemFormStats[] retryComposableGolemForms;
	private int? retryComposableGolemHitPoints;
	private int? retrySeraphelHitPoints;
	private int? retryJurinashorHitPoints;
	private bool retryJurinashorPhaseTwo;

	private MedusaBoss activeMedusaBoss;
	private SeraphelBoss activeSeraphelBoss;

	private TrentorBoss activeTrentorBoss;
	private JurinashorBoss activeJurinashorBoss;

	private BragusBoss activeBragusBoss;
	private bool bragusBossPresentationActive;
	private bool trentorBossPresentationActive;

	private PalatirBoss activePalatirBoss;

	private bool merchantRoomsBlockedUntilMonster;

	private bool rewardRoomsBlockedUntilMonster;

	// Prezzo della Rinuncia: si consuma soltanto alla vittoria della prossima stanza Mostro.
	private bool nextMonsterRewardHalved;

	private bool debugForceFirstRoomComposableGolem;

	private bool debugForceFirstRoomMedusa;

	private bool debugForceFirstRoomTrentor;

	private bool debugForceFirstRoomBragus;

	private bool debugForceFirstRoomJurinashor;

	private bool debugForceFirstRoomPalatir;
	private bool debugForceFirstRoomSeraphel;
	private bool bossDebugSceneSession;

	private bool debugMerchantScene;

	private bool debugLootRoomScene;

	private bool debugClassChoiceScene;
	private static string bootstrapSceneName;
	private static BattleBoardController pendingCampaignSessionRecovery;

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
		// Conserva il nome ricevuto dall'evento sceneLoaded: Awake del nuovo controller
		// non deve dipendere da quale scena Unity renda attiva nello stesso frame.
		bootstrapSceneName = scene.name;
		// Il controller sopravvive ai normali cambi scena, ma non deve sopravvivere
		// al ritorno al login. In quel caso la UI della vecchia sessione viene
		// smontata durante lo scene unload; conservarne l'istanza impedirebbe al
		// bootstrap di crearne una nuova al successivo ingresso in MainScene,
		// lasciando visibili soltanto camera e skybox.
		if (string.Equals(
			scene.name,
			LoginScreenPrototypeSceneName,
			StringComparison.OrdinalIgnoreCase))
		{
			BattleBoardController staleController = Object.FindAnyObjectByType<BattleBoardController>();
			if ((Object)(object)staleController != (Object)null
				&& ReferenceEquals(staleController, pendingCampaignSessionRecovery))
			{
				staleController.SuspendCampaignForAuthentication();
				return;
			}
			if ((Object)(object)staleController != (Object)null)
				Object.Destroy((Object)staleController.gameObject);
			return;
		}

		// Il ritorno dal login per sessione scaduta non crea una nuova partita: riusa
		// esattamente il controller parcheggiato, inclusi gli stati non serializzabili
		// delle meccaniche presenti e future. Il popup lascia al giocatore la scelta.
		if ((Object)(object)pendingCampaignSessionRecovery != (Object)null)
		{
			BattleBoardController recovery = pendingCampaignSessionRecovery;
			pendingCampaignSessionRecovery = null;
			recovery.RestoreCampaignAfterAuthentication();
			return;
		}

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
			PromotionalTrailerSceneName,
			StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		// Le scene debug boss devono sempre partire da uno stato pulito. Il controller
		// e' DontDestroyOnLoad: riutilizzarlo dopo una sessione Bragus conserva scenario,
		// flag e UI del boss precedente (particolarmente con Domain Reload disattivato).
		bool bossDebugScene = string.Equals(scene.name, MinibossGolemDebugSceneName, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(scene.name, MedusaDebugSceneName, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(scene.name, BossDebugSceneName, StringComparison.OrdinalIgnoreCase);
		BattleBoardController existingController = Object.FindAnyObjectByType<BattleBoardController>();
		if (bossDebugScene && (Object)(object)existingController != (Object)null)
		{
			// Elimina immediatamente l'immagine residua del precedente boss mentre il
			// controller pulito ricostruisce la UI della scena debug di Seraphel.
			if (string.Equals(scene.name, BossDebugSceneName, StringComparison.OrdinalIgnoreCase)
				&& BossDebugSelection.Current == BossDebugScenario.Seraphel)
			{
				existingController.debugForceFirstRoomSeraphel = true;
				existingController.bragusBossPresentationActive = false;
				existingController.trentorBossPresentationActive = false;
				existingController.seraphelBossPresentationActive = false;
				existingController.RefreshScenarioBackground();
			}
			Object.Destroy((Object)existingController.gameObject);
			GameObject freshBoard = new GameObject("Accard N' Die - Battle Board");
			Object.DontDestroyOnLoad((Object)freshBoard);
			freshBoard.AddComponent<BattleBoardController>();
			return;
		}

		if (!((Object)(object)existingController != (Object)null))
		{
			GameObject val = new GameObject("Accard N' Die - Battle Board");
			Object.DontDestroyOnLoad((Object)val);
			val.AddComponent<BattleBoardController>();
		}
	}

	private async void Awake()
	{
		AccardND.Network.AccountServerSession.ReturningToLoginForExpiredSession -= PrepareCampaignSessionRecovery;
		AccardND.Network.AccountServerSession.ReturningToLoginForExpiredSession += PrepareCampaignSessionRecovery;
		configuration = Resources.Load<GameConfiguration>("GameConfiguration");
		if ((Object)(object)configuration == (Object)null)
		{
			configuration = ScriptableObject.CreateInstance<GameConfiguration>();
		}
		bool unifiedBossDebug = IsBootstrapOrLoadedScene(BossDebugSceneName);
		bossDebugSceneSession = unifiedBossDebug;
		BossDebugScenario debugBoss = BossDebugSelection.Current;
		debugForceFirstRoomTrentor = unifiedBossDebug && debugBoss == BossDebugScenario.Trentor;
		debugForceFirstRoomComposableGolem = !debugForceFirstRoomTrentor
			&& IsSceneLoaded(MinibossGolemDebugSceneName);
		// Durante sceneLoaded il nuovo controller puo' eseguire Awake prima che Unity
		// esponga la scena nella lista sceneCount. Conserviamo quindi anche il nome
		// passato al bootstrap, come gia' facciamo per la scena boss unificata.
		debugForceFirstRoomMedusa = IsBootstrapOrLoadedScene(MedusaDebugSceneName)
			|| (unifiedBossDebug && debugBoss == BossDebugScenario.Medusa);
		debugForceFirstRoomBragus = unifiedBossDebug && debugBoss == BossDebugScenario.Bragus;
		debugForceFirstRoomJurinashor = unifiedBossDebug && debugBoss == BossDebugScenario.Jurinashor;
		debugForceFirstRoomPalatir = unifiedBossDebug && debugBoss == BossDebugScenario.Palatir;
		debugForceFirstRoomSeraphel = unifiedBossDebug && debugBoss == BossDebugScenario.Seraphel;
		if (debugForceFirstRoomBragus || debugForceFirstRoomSeraphel)
			EnsureBossDebugAudioListener();
		debugMerchantScene = IsSceneLoaded(MerchantDebugSceneName);
		debugLootRoomScene = IsSceneLoaded(LootRoomDebugSceneName);
		debugClassChoiceScene = IsSceneLoaded(ClassChoiceDebugSceneName);
		int num = (configuration.UseRandomSeedEachSession ?Guid.NewGuid().GetHashCode() : configuration.Gameplay.RandomSeed);
		// Tenuti anche come sorgenti concrete: e' da li' che lo snapshot di battaglia
		// legge seme e numero di estrazioni, e senza quelli una battaglia ripresa
		// ritirerebbe i dadi da capo.
		battleRandom = new SeededRandomSource(num);
		random = battleRandom;
		combatResolver = new CombatResolver(random);
		// La CPU pesca da un flusso suo: se condividesse quello dei dadi, ogni pareggio
		// sciolto a sorte sposterebbe tutti i tiri successivi e a parita' di seme la
		// partita non sarebbe piu' riproducibile.
		battleCpuRandom = new SeededRandomSource(num ^ CpuDecisionSeedOffset);
		cpuDecisionService = new CpuDecisionService(battleCpuRandom);
		runProgress = CreateRunProgress();
		diceCatalog = Resources.Load<DiceSpriteCatalog>("DiceSpriteCatalog");
		battleAnimationPlayer = gameObject.AddComponent<BattlePresentationAnimationPlayer>();
		// La pubblicita' scrive nel diario di partita come tutto il resto: quando un
		// interstitial non parte serve poter distinguere una regola di frequenza da un
		// guasto della rete, e la console di Unity su un telefono non si legge.
		AccardND.Ads.AdService.Log = AppendLog;
		// Si prepara l'SDK subito, ma senza chiedere nessun annuncio: le richieste partono dai
		// posti che ne hanno bisogno (la taverna che si apre, la run che comincia, la partita
		// d'arena che parte). Caricarli tutti qui costava cinque richieste a sessione anche a
		// chi apriva il gioco e lo chiudeva, e un annuncio caricato all'avvio e' comunque
		// scaduto quando servirebbe.
		_ = AccardND.Ads.AdService.PrepareAsync();
		// Lo store degli acquisti si connette allo stesso modo: presto, in silenzio, e senza
		// che nessuno aspetti. Serve gia' al primo negozio aperto, per mostrare i prezzi
		// veri di Google invece dei segnaposto.
		InitializeIapBridge();
		InitializeAudio();
		// Le scene debug avviano direttamente questo controller e non attraversano la
		// schermata login, che normalmente attende le String Table. Senza questa attesa
		// la UI nasce con fallback italiani o con la chiave testuale non risolta.
		await GameText.InitializeAsync();
		if ((Object)(object)this == (Object)null)
			return;
		BuildInterface();
		GameText.LocaleChanged -= HandleGameLocaleChanged;
		GameText.LocaleChanged += HandleGameLocaleChanged;
		AppendLog($"SESSIONE AVVIATA - seed {num}");
		ShowModeSelection();
		_ = EnsureServerProgressAsync();
	}

	private void HandleGameLocaleChanged()
	{
		// Le etichette fisse usano EditableRuntimeText e si aggiornano da sole.
		// Le carte capitolo invece vengono generate al volo: ricrearle evita di
		// lasciare nella lingua precedente stato, sottotitolo e CTA gia' visibili.
		if ((Object)(object)adventureChapterPanel != (Object)null && adventureChapterPanel.activeSelf)
			RefreshAdventureChapterList();

		// La Forgia mantiene le sue etichette e le carte classe gia' create: aggiornarle
		// qui evita di lasciare il testo nella lingua selezionata prima dell'ingresso.
		if ((Object)(object)deckBuilderPanel != (Object)null && deckBuilderPanel.activeSelf)
			RefreshDeckBuilderView();

		if ((Object)(object)merchantPanel != (Object)null && merchantPanel.activeSelf)
		{
			RefreshMerchantPanel();
			SetTurnBanner(
				playerTurn: true,
				GameText.Get(GameTextKeys.Campaign.MerchantRoomCompleteBanner));
		}

		if ((Object)(object)tavernPanel != (Object)null && tavernPanel.activeSelf)
			ApplyTavernData(tavernData);

		if ((Object)(object)profilePanel != (Object)null && profilePanel.activeSelf)
			RefreshProfile();

		RefreshActiveCombatLocalePresentation();
	}

	/// <summary>
	/// Il combattimento non viene ricreato al cambio lingua: i badge e le azioni
	/// esistono già. Li rigeneriamo dallo stato di battaglia corrente per evitare
	/// un HUD misto tra due locale.
	/// </summary>
	private void RefreshActiveCombatLocalePresentation()
	{
		RefreshCombatHudRefactor();
		RefreshPlayerHud();
		RefreshInitiativeDisplay();

		foreach (BattleCardState card in playerCards)
		{
			if (card == null)
				continue;
			RefreshPersistentStatus(card);
			card.View?.ClearActionCalloutForLocaleChange();
		}
		foreach (BattleCardState card in cpuCards)
		{
			if (card == null)
				continue;
			RefreshPersistentStatus(card);
			card.View?.ClearActionCalloutForLocaleChange();
		}

		RefreshCardActionOverlays();
		RefreshOpenCardInspectionLocale();
		RefreshRetainedCombatTextLocale();
		UpdateInteractions();

		// Quando il giocatore sta scegliendo l'azione, banner e prompt sono
		// completamente ricostruibili dallo stato e vengono aggiornati subito.
		if (!gameFinished && !inputLocked && !attackTargetingActive
			&& activeAbilityUser == null && abilityTargetMode == AbilityTargetMode.None
			&& selectedPlayerIndex >= 0 && selectedPlayerIndex < playerCards.Count)
		{
			SetTurnBanner(playerTurn: true, GameText.Get(GameTextKeys.Combat.PlayerTurnBanner));
			SetBattlefieldMessage(GameText.Get(GameTextKeys.Combat.ChooseAction));
		}
	}

	private static bool IsSceneLoaded(string sceneName)
	{
		for (int index = 0; index < SceneManager.sceneCount; index++)
		{
			Scene scene = SceneManager.GetSceneAt(index);
			if (scene.isLoaded
				&& string.Equals(scene.name, sceneName, StringComparison.OrdinalIgnoreCase))
				return true;
		}
		return false;
	}

	private static bool IsBootstrapOrLoadedScene(string sceneName)
	{
		return string.Equals(bootstrapSceneName, sceneName, StringComparison.OrdinalIgnoreCase)
			|| IsSceneLoaded(sceneName);
	}

	private void EnsureBossDebugAudioListener()
	{
		AudioListener[] sceneListeners = Object.FindObjectsOfType<AudioListener>(includeInactive: true);
		for (int index = 0; index < sceneListeners.Length; index++)
		{
			AudioListener listener = sceneListeners[index];
			if ((Object)(object)listener != (Object)null && listener.gameObject != gameObject)
				listener.enabled = false;
		}

		AudioListener persistentListener = GetComponent<AudioListener>();
		if ((Object)(object)persistentListener == (Object)null)
			persistentListener = gameObject.AddComponent<AudioListener>();
		persistentListener.enabled = true;
	}

	private void Update()
	{
		UpdateTavernServerRefresh();
		ResumePendingPvpMatchIfAny();
		UpdatePvpTimelineCountdown();
		UpdatePowerBudget();
		UpdateStuckGestureWatchdog();

		if (IsEscapePressedThisFrame() && CloseTopmostOverlay())
		{
			return;
		}

		if (HasScreenGeometryChanged())
		{
			ApplyResponsiveLayout();
		}
	}

	/// <summary>
	/// Dice al resto del gioco quanto lavoro merita il frame corrente: a che
	/// ritmo disegnare, e quali VFX procedurali sono ancora visibili.
	/// </summary>
	private void UpdatePowerBudget()
	{
		AccardND.Battlefield.FrameRateGovernor.SetHighFrameRateScreen(!IsStaticScreenVisible());
		AccardND.Battlefield.UiVfxBudget.SetForegroundRoot(TopmostFullScreenOverlay());
	}

	/// <summary>
	/// Le schermate che restano immobili finche' non le si tocca. Fuori da
	/// queste siamo in battaglia (o in una transizione verso di essa), dove le
	/// animazioni partono senza preavviso e i sessanta frame servono davvero.
	/// </summary>
	private bool IsStaticScreenVisible()
	{
		return IsVisible(modeSelectionPanel)
			|| IsVisible(campaignModeSelectionPanel)
			|| IsVisible(adventureChapterPanel)
			|| IsVisible(shopPanel)
			|| IsVisible(tavernPanel)
			|| IsVisible(sanctuaryPanel)
			|| IsVisible(libraryPanel)
			|| IsVisible(profilePanel)
			|| IsVisible(merchantPanel)
			|| IsVisible(deckBuilderPanel)
			|| IsVisible(implementationArchivePanel);
	}

	/// <summary>
	/// Il pannello che copre per intero quello che c'e' sotto, dal piu' in alto
	/// al piu' in basso. I VFX fuori da questa gerarchia non li vede nessuno.
	/// Restano fuori dalla lista i pannelli che non coprono davvero lo schermo:
	/// congelare uno sfondo ancora visibile sembrerebbe un bug, non un risparmio.
	/// </summary>
	private Transform TopmostFullScreenOverlay()
	{
		if (IsVisible(campaignDefeatRewardPopup))
			return campaignDefeatRewardPopup.transform;
		if (IsVisible(cardInspectionPanel))
			return cardInspectionPanel.transform;
		if (IsVisible(auraCodexPanel))
			return auraCodexPanel.transform;
		if (IsVisible(implementationArchivePanel))
			return implementationArchivePanel.transform;
		if (IsVisible(optionsPanel))
			return optionsPanel.transform;
		if (IsVisible(merchantPanel))
			return merchantPanel.transform;

		return null;
	}

	/// <summary>
	/// Come <see cref="IsActive"/>, ma guarda l'intera catena dei genitori:
	/// un pannello acceso dentro un padre spento non lo vede nessuno.
	/// </summary>
	private static bool IsVisible(GameObject panel)
	{
		return (Object)(object)panel != (Object)null && panel.activeInHierarchy;
	}

	private static bool IsEscapePressedThisFrame()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
	}

	/// <summary>Pulizia degli effetti che non devono sopravvivere alla disattivazione della scena.</summary>
	private void OnDisable()
	{
		ClearManaDeltaCallouts();
		ClearEnemyManaDeltaCallouts();
		RestoreBossShakeTransform();
		DestroyBossTransitionBlackout();
	}

	private bool CloseTopmostOverlay()
	{
		if (IsActive(campaignDefeatRewardPopup))
		{
			ContinueAfterCampaignDefeatReward();
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
		else if (IsActive(logoutConfirmPanel))
		{
			HideLogoutConfirmation();
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
		else if (IsActive(merchantBranchConfirmPopup))
		{
			HideMerchantBranchConfirmPopup();
			return true;
		}
		else if (IsActive(merchantPanel))
		{
			CloseMerchantPanel();
			return true;
		}
		else if (IsActive(languageDropdownOverlay))
		{
			CloseLanguageDropdown();
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

	/// <summary>
	/// Passo temporale delle animazioni di interfaccia. Un frame lungo — il
	/// salto a sessanta del governor, decine di view create insieme all'ingresso
	/// in battaglia, un hitch del browser — non deve mangiarsi mezza animazione
	/// in un colpo: meglio allungarla di qualche millisecondo che vederla
	/// saltare da una posa all'altra.
	/// </summary>
	private static float AnimationDeltaTime()
	{
		return Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
	}

	/// <summary>
	/// Vero solo se la coroutine indicata ha davvero girato nell'ultimo frame.
	/// Un handle non nullo non basta a dimostrarlo: uno StopAllCoroutines lascia
	/// dietro di se' riferimenti a coroutine gia' morte, e chi decide in base a
	/// quelli finisce per non scrivere mai la posa finale.
	/// </summary>
	private static bool IsRoutineAlive(Coroutine routine, int lastFrame)
	{
		return routine != null && lastFrame >= Time.frameCount - 1;
	}

	private void LateUpdate()
	{
		if (draftActive)
		{
			// I layout group si ricostruiscono su Canvas.willRenderCanvases,
			// cioe' dopo questo LateUpdate: senza forzare prima la ricostruzione,
			// il ventaglio scriverebbe una posa che il gruppo appiattisce nello
			// stesso frame.
			Canvas.ForceUpdateCanvases();
			ApplyHandFan();
		}
		UpdateTurnCoinAnimation();
		UpdateMessageTextLayout();
	}

	private void BuildInterface()
	{
		EnsureEventSystem();
		Font builtinResource = AccardND.Battlefield.MmoUiTheme.BodyFont;
		confirmActionSprite = LoadSpriteResource("UI/confirm_button");
		cancelActionSprite = LoadSpriteResource("UI/cancel_button");
		infoActionSprite = LoadSpriteResource("UI/info_button");
		settingsButtonSprite = LoadSpriteResource("UI/SharedHeader/settings_gear");
		hubButtonSprite = LoadSpriteResource("UI/SharedHeader/hub_house");
		accountHeaderSettingsSprite = LoadSpriteResource("UI/SharedHeader/settings_gear");
		Canvas val = CreateCanvas();
		canvasRect = (RectTransform)((Component)val).transform;
		canvasScaler = ((Component)val).GetComponent<CanvasScaler>();
		scenarioCatalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
		StartingRoomConfiguration startingRoom = configuration.StartingRoom;
		// La debug boss seleziona lo scenario prima di costruire la UI. Non sostituirlo
		// con la stanza iniziale generica: il suo fondale deve essere visibile gia'
		// durante lo schieramento.
		if ((Object)(object)currentScenario == (Object)null)
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
		GameObject topInfoBar = new GameObject("Top Info Bar", typeof(RectTransform));
		topInfoBar.transform.SetParent((Transform)(object)safeAreaRoot, false);
		topInfoBarRect = (RectTransform)topInfoBar.transform;
		SetRect(topInfoBarRect, new Vector2(0.04f, 0.942f), new Vector2(0.84f, 0.992f));
		Text text = CreateText("Top Info Text", topInfoBar.transform, builtinResource, 50, (FontStyle)1, (TextAnchor)3);
		titleRect = text.rectTransform;
		topInfoText = text;
		topInfoText.text = GameText.Format(GameTextKeys.Combat.CpuHudRoom, 1);
		topInfoText.font = Resources.Load<Font>("Fonts/LifeCraft_Font") ?? AccardND.Battlefield.MmoUiTheme.BodyFont;
		topInfoText.fontStyle = FontStyle.Normal;
		topInfoText.alignment = TextAnchor.MiddleCenter;
		topInfoText.resizeTextForBestFit = true;
		topInfoText.resizeTextMinSize = 24;
		topInfoText.resizeTextMaxSize = 50;
		topInfoText.color = new Color(0.95f, 0.79f, 0.34f);
		Outline roomTitleOutline = ((Component)topInfoText).gameObject.AddComponent<Outline>();
		roomTitleOutline.effectColor = new Color(0.025f, 0.018f, 0.008f, 1f);
		roomTitleOutline.effectDistance = new Vector2(2.5f, -2.5f);
		roomTitleOutline.useGraphicAlpha = true;
		Stretch(topInfoText.rectTransform, 10f);
		((Component)topInfoBarRect).gameObject.SetActive(false);
		logButton = CreateImageButton("Options Button", (Transform)(object)safeAreaRoot, builtinResource, settingsButtonSprite, string.Empty);
		AddHubButtonOutline(logButton);
		((UnityEvent)logButton.onClick).AddListener(new UnityAction(ToggleOptionsPanel));
		Canvas optionsButtonCanvas = ((Component)logButton).gameObject.AddComponent<Canvas>();
		optionsButtonCanvas.overrideSorting = true;
		optionsButtonCanvas.sortingOrder = 400;
		((Component)logButton).gameObject.AddComponent<GraphicRaycaster>();
		SetRect((RectTransform)((Component)logButton).transform, new Vector2(0.84f, 0.902f), new Vector2(0.995f, 0.992f));
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
		SetRect(logText.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.875f));
		Text logTitle = CreateText("Battle Log Title", logPanel.transform, builtinResource, 22, (FontStyle)1, (TextAnchor)3);
		SetLocalizedText(logTitle, GameTextKeys.GameLog.BattleLogTitle, "REGISTRO DI BATTAGLIA");
		logTitle.color = new Color(0.95f, 0.79f, 0.34f);
		SetRect(logTitle.rectTransform, new Vector2(0.035f, 0.9f), new Vector2(0.78f, 0.985f));
		Button button = CreateButton("Close Log", logPanel.transform, builtinResource, "CHIUDI");
		((UnityEvent)button.onClick).AddListener(new UnityAction(ToggleLogPanel));
		SetRect((RectTransform)((Component)button).transform, new Vector2(0.84f, 0.93f), new Vector2(0.98f, 0.99f));
		logPanel.SetActive(false);
		CreateOptionsPanel(((Component)val).transform, builtinResource);
		CreateReturnToMenuConfirmation((Transform)(object)safeAreaRoot, builtinResource);
		CreateLogoutConfirmation((Transform)(object)safeAreaRoot, builtinResource);
		Text text2 = (cpuTitleText = CreateText("CPU Title", (Transform)(object)safeAreaRoot, builtinResource, 25, (FontStyle)1, (TextAnchor)3));
		AccardND.Battlefield.MmoUiTheme.StyleAsTitle(text2);
		cpuTitleRect = text2.rectTransform;
		text2.text = ((Object)(object)currentScenario != (Object)null)
			? GameText.Format(GameTextKeys.Combat.CpuMasterScenario, currentScenario.DisplayName.ToUpperInvariant())
			: GameText.Get(GameTextKeys.Combat.CpuMaster);
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
		timelineCountdownText = CreateText("Turn Countdown", ((Component)image6).transform, builtinResource, 30, (FontStyle)1, (TextAnchor)4);
		timelineCountdownText.color = new Color(0.95f, 0.79f, 0.34f);
		timelineCountdownText.raycastTarget = false;
		RectTransform countdownRect = timelineCountdownText.rectTransform;
		countdownRect.anchorMin = new Vector2(0f, 1f);
		countdownRect.anchorMax = new Vector2(1f, 1f);
		countdownRect.pivot = new Vector2(0.5f, 0f);
		countdownRect.anchoredPosition = new Vector2(0f, 8f);
		countdownRect.sizeDelta = new Vector2(0f, 38f);
		((Component)timelineCountdownText).gameObject.SetActive(false);
		initiativeTimelineRoot = new GameObject("Turn Timeline", new Type[1]
		{
			typeof(RectTransform)
		}).GetComponent<RectTransform>();
		((Transform)initiativeTimelineRoot).SetParent(((Component)image6).transform, false);
		Stretch(initiativeTimelineRoot, 4f);
		Image image7 = CreateImage("Message Panel", (Transform)(object)safeAreaRoot, new Color(0.015f, 0.025f, 0.04f, 0.56f));
		StylePanel(image7);
		messagePanelRect = image7.rectTransform;
		Canvas messagePanelCanvas = image7.gameObject.AddComponent<Canvas>();
		messagePanelCanvas.overrideSorting = true;
		messagePanelCanvas.sortingOrder = 300;
		image7.gameObject.AddComponent<GraphicRaycaster>();
		SetRect(image7.rectTransform, new Vector2(0.25f, 0.41f), new Vector2(0.75f, 0.555f));
		messageText = CreateText("Battle Log", ((Component)image7).transform, builtinResource, 22, (FontStyle)0, (TextAnchor)4);
		messageText.color = new Color(0.88f, 0.92f, 0.96f);
		SetRect(messageText.rectTransform, new Vector2(0.035f, 0.06f), new Vector2(0.65f, 0.66f));
		turnBannerImage = CreateImage("Current Turn Banner", ((Component)image7).transform, configuration.Visual.PlayerTurnColor);
		StylePanel(turnBannerImage);
		SetRect(turnBannerImage.rectTransform, new Vector2(0.1825f, 0.69f), new Vector2(0.8175f, 0.98f));
		turnBannerText = CreateText("Current Turn", ((Component)turnBannerImage).transform, builtinResource, 24, (FontStyle)1, (TextAnchor)4);
		turnBannerText.text = GameText.Get(GameTextKeys.Combat.Preparation);
		Stretch(turnBannerText.rectTransform, 4f);
		restartButton = CreateButton("Primary Action", ((Component)image7).transform, builtinResource, "CONTINUA");
		((UnityEvent)restartButton.onClick).AddListener(new UnityAction(HandlePrimaryAction));
		restartButtonText = ((Component)restartButton).GetComponentInChildren<Text>();
		SetPrimaryActionLabel(restartButtonText, PrimaryActionLabel.Continue);
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
		merchantOpenButtonPulseVfx = AccardND.PvpUi.PvpUiVfx.CreatePulseButton(
			(RectTransform)((Component)merchantBuyButton).transform,
			new Color(0.48f, 0.68f, 0.16f, 1f));
		merchantContinueButtonPulseVfx = AccardND.PvpUi.PvpUiVfx.CreatePulseButton(
			(RectTransform)((Component)restartButton).transform,
			new Color(0.12f, 0.58f, 0.92f, 1f));
		((Component)merchantOpenButtonPulseVfx).gameObject.SetActive(false);
		((Component)merchantContinueButtonPulseVfx).gameObject.SetActive(false);
		RectTransform val7 = (RectTransform)((Component)merchantBuyButton).transform;
		val7.anchorMin = new Vector2(0.69f, 0.54f);
		val7.anchorMax = new Vector2(0.97f, 0.92f);
		val7.offsetMin = Vector2.zero;
		val7.offsetMax = Vector2.zero;
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
		CreateTavernView(((Component)val).transform, builtinResource);
		CreateLibraryView(((Component)val).transform, builtinResource);
		CreateCampaignModeSelectionView(builtinResource);
		CreateSanctuaryView(builtinResource);
		CreateShopView(builtinResource);
		CreateProfileView(builtinResource);
		CreateAuraCodexView(((Component)val).transform, builtinResource);
		// Il velo di fine campagna deve coprire anche le aree esterne alla safe area
		// (notch/status bar). Il dialogo mantiene comunque il proprio layout centrale.
		CreateCampaignDefeatRewardPopup(((Component)val).transform, builtinResource);
		CreateCampaignLevelUpPopup((Transform)(object)safeAreaRoot, builtinResource);
		CreateClassChoicePopup((Transform)(object)safeAreaRoot, builtinResource);
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
			nextMonsterDifficultyIncrease = Math.Max(nextMonsterDifficultyIncrease, 1);
			return (description: " Presagio oscuro: la prossima stanza mostro sara di una difficolta piu alta.", bonusExperience: 0);
		default:
			return (description: " Nessun effetto.", bonusExperience: 0);
		}
	}

	private (string description, int bonusExperience) GrantRandomRewardCard(string source)
	{
		pendingMasterGiftReward = null;
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
		if (string.Equals(source, "DONO DEL MASTER", StringComparison.Ordinal))
		{
			pendingMasterGiftReward = cardDefinition;
		}
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
		ClearManaDeltaCallouts();
		ClearEnemyManaDeltaCallouts();
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
			if (playerCard != null && (Object)(object)playerCard.View != (Object)null)
				playerCard.View.SetInteractable(CanUsePlayerCardAction(playerCard) || CanInspectBattleCard(playerCard));
		}
		for (int i = 0; i < cpuCards.Count; i++)
		{
			BattleCardState cpuCard = cpuCards[i];
			// Le evocazioni di Jurinashor vengono rimosse subito dalla scena alla morte,
			// ma il loro stato resta nella lista per lo storico del combattimento.
			if (cpuCard == null || (Object)(object)cpuCard.View == (Object)null)
				continue;
			bool interactable = IsTutorialWarriorDuelActive
				? TutorialWarriorDuelAllowsEnemyTarget(cpuCard)
				: CanUseCpuCardAction(i) || CanInspectBattleCard(cpuCard);
			cpuCard.View.SetInteractable(interactable);
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
