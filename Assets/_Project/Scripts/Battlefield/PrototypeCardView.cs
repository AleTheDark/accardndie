using System;
using System.Collections;
using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    public sealed class PrototypeCardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly Dictionary<string, Sprite> runtimePreviewSprites = new();
        private static readonly Dictionary<string, Sprite> croppedArtworkSprites = new();
        private static readonly Dictionary<string, Sprite> statusIconCache = new();
        private static GameObject diceWindowPrefab;
        private static Sprite runtimeAuraSprite;
        private static Sprite runtimeSparkleSprite;
        private static Sprite petrifiedOverlaySprite;
        private static Sprite petrifiedFragmentSprite;
        private static Sprite necromancerDefeatSkullSprite;
        private static Sprite priestPurificationCrossSprite;
        private static Sprite priestPurificationHaloSprite;
        private static Sprite priestPurificationSparkSprite;
        private static Sprite bossHealthFrameSprite;
        private static Sprite bossHealthFillSprite;
        private static Sprite bossHealthGlowSprite;
        private static Sprite[] statusIconSprites;
        private static Texture2D[] statusIconTextures;
        private const string DiceWindowResourcePath = "DiceWindowPurple";
        private const string DiceRollTrigger = "startroll";
        private const float AnimatedDiceMinimumRollDuration = 1.36f;
        private const float AnimatedDiceMinimumResultHold = 0.85f;
        private const float AnimatedDicePositionJitter = 10f;
        private const float AnimatedDiceRotationJitter = 22f;
        private const float AnimatedDiceMinimumSpeed = 1.52f;
        private const float AnimatedDiceMaximumSpeed = 1.62f;
        private const float D4ScreenDiceAreaScale = 1.45f;
        private const float ScreenDiceAreaWidth = 0.38f;
        private const float CardAspect = 848f / 1264f;
        private const float ArtworkViewportAspect = CardAspect * (0.865f - 0.135f) / (0.805f - 0.325f);
        private const float ArtworkCropOverscan = 0.86f;
        private static readonly Color ActionLabelOutline = new(0.02f, 0.01f, 0f);
        private static readonly Color ActionLabelGreen = new(0.05f, 0.56f, 0.24f);
        private static readonly Color ActionLabelRed = new(0.66f, 0.08f, 0.05f);
        private static readonly Color ActionLabelOrange = new(0.78f, 0.32f, 0.06f);
        private static readonly Color ActionLabelBlue = new(0.05f, 0.28f, 0.76f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatusIconCaches()
        {
            statusIconCache.Clear();
            statusIconSprites = null;
            statusIconTextures = null;
            bossHealthFrameSprite = null;
            bossHealthFillSprite = null;
            bossHealthGlowSprite = null;
        }
        private static readonly Color SelectedAuraGold = new(1f, 0.78f, 0.16f, 1f);
        private static readonly Color SelectedBattlefieldGreen = new(0.2f, 1f, 0.65f, 1f);
        private static Font actionLabelFont;
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Outline selectionOutline;
        private Shadow liftShadow;
        private GameObject turnAuraRoot;
        private RectTransform turnAuraRect;
        private Image turnAuraImage;
        private readonly List<Image> turnSparkles = new();
        private GameObject battleAuraRoot;
        private RectTransform battleAuraRect;
        private Image battleAuraImage;
        private Text battleAuraLabel;
        private Text strengthText;
        private Text defeatedLabel;
        private Text targetHintLabel;
        private Image targetHintBackground;
        private GameObject targetHintAuraRoot;
        private RectTransform targetHintAuraRect;
        private Image targetHintAuraImage;
        private GameObject selectedAuraRoot;
        private RectTransform selectedAuraRect;
        private Image selectedAuraImage;
        private readonly List<Image> selectedSparkles = new();
        private readonly List<Image> defeatEffectImages = new();
        private readonly List<Transform> defeatEffectLayerAnchors = new();
        private Text statusLabel;
        private RectTransform statusIconRoot;
        private readonly List<GameObject> statusIconViews = new();
        private Image petrifiedOverlayImage;
        private GameObject healthBarRoot;
        private Image healthBarFill;
        private Image healthBarGlow;
        private Image healthBarTopShine;
        private Text healthBarText;
        private GameObject lifeIconsRoot;
        private Image leftLifeIcon;
        private Image rightLifeIcon;
        private Sprite heartIcon;
        private Sprite blackHeartIcon;
        private float statusIconSize = 56f;
        private GameObject diceRoot;
        private Image diceImage;
        private Image secondDiceImage;
        private Image firstAnimatedDiceBodyImage;
        private Image secondAnimatedDiceBodyImage;
        private GameObject firstAnimatedDiceRoot;
        private GameObject secondAnimatedDiceRoot;
        private Animator[] firstDiceAnimators = Array.Empty<Animator>();
        private Animator[] secondDiceAnimators = Array.Empty<Animator>();
        private Sprite[] firstAnimatedDiceResults = Array.Empty<Sprite>();
        private Sprite[] secondAnimatedDiceResults = Array.Empty<Sprite>();
        private Text diceCaption;
        private Text diceResult;
        private Dice3DRollView firstDice3D;
        private Dice3DRollView secondDice3D;
        private RectTransform firstDice3DArea;
        private RectTransform secondDice3DArea;
        private RectTransform screenDiceBounceArea;
        private bool diceRootDetachedToScreen;
        private Transform diceRootHomeParent;
        private int diceRootHomeSiblingIndex;
        private Vector2 diceRootHomeAnchorMin;
        private Vector2 diceRootHomeAnchorMax;
        private Vector2 diceRootHomeOffsetMin;
        private Vector2 diceRootHomeOffsetMax;
        private Vector3 diceRootHomeScale;
        private Quaternion diceRootHomeRotation;
        private bool diceRootOnLowerScreenHalf;
        private float screenDiceCardCenterX = 0.5f;
        private HeroClass cardHeroClass;
        private Coroutine diceCoroutine;
        private Coroutine motionCoroutine;
        private Coroutine turnAuraCoroutine;
        private Coroutine battleAuraCoroutine;
        private Coroutine targetHintAuraCoroutine;
        private Coroutine selectedAuraCoroutine;
        private RectTransform actionOverlayRoot;
        private GameConfiguration configuration;
        private Vector3 initialScale;
        private Color turnAuraColor;
        private bool turnAuraActive;
        private Color battleAuraColor;
        private bool battleAuraActive;
        private Color targetHintAuraColor;
        private bool targetHintAuraActive;
        private bool duelDetached;
        private Vector2 duelHomeAnchoredPosition;
        private Vector3 duelHomeScale;
        private Quaternion duelHomeRotation;
        private Func<bool> canDrag;
        private UnityAction<PrototypeCardView, PointerEventData> dragStarted;
        private UnityAction<PrototypeCardView, PointerEventData> dragEnded;
        private Transform dragHomeParent;
        private int dragHomeSiblingIndex;
        private Vector2 dragHomeAnchoredPosition;
        private GameObject dragPlaceholder;
        private Canvas dragCanvas;
        private RectTransform dragCanvasRect;
        private Vector3 dragWorldOffset;
        private bool dragging;
        private bool suppressDragRestore;
        private Coroutine pendingDragEndRoutine;
        private bool selectedVisual;
        private Coroutine selectionScaleCoroutine;
        private GameObject defeatCrackOverlay;
        private RectTransform composableGolemOrbitRoot;
        private RawImage composableGolemOrbitRenderImage;
        private RenderTexture composableGolemOrbitTexture;
        private GameObject composableGolemOrbitScene;
        private Transform composableGolemOrbit3DRoot;
        private Camera composableGolemOrbitCamera;
        private readonly Dictionary<ComposableGolemForm, Image> composableGolemOrbitImages = new();
        private readonly Dictionary<ComposableGolemForm, Transform> composableGolemOrbitStones = new();
        private readonly Dictionary<ComposableGolemForm, Vector3> composableGolemOrbitStonePositions = new();
        private readonly Dictionary<ComposableGolemForm, Vector3> composableGolemOrbitStoneScales = new();
        private ComposableGolemForm composableGolemActiveForm = ComposableGolemForm.Iron;
        private RectTransform palatirShieldOrbitRoot;
        private readonly Dictionary<ClassFamily, Image> palatirShieldImages = new();
        private readonly HashSet<ClassFamily> activePalatirShields = new();

        public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;
        public Button Button { get; private set; }
        public HeroClass HeroClass => cardHeroClass;
        public bool IsDragging => dragging;

        private void Update()
        {
            if (composableGolemOrbitRoot == null)
            {
                UpdatePalatirShieldOrbit();
                return;
            }

            if (composableGolemOrbit3DRoot != null && composableGolemOrbitStones.Count > 0)
            {
                composableGolemOrbit3DRoot.localRotation = Quaternion.Euler(0f, -Time.unscaledTime * 42f, 0f);
                foreach (KeyValuePair<ComposableGolemForm, Transform> pair in composableGolemOrbitStones)
                {
                    Transform stone = pair.Value;
                    if (stone == null)
                        continue;

                    bool active = pair.Key == composableGolemActiveForm;
                    float pulse = active ? 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.08f : 1f;
                    stone.localRotation = Quaternion.Euler(0f, Time.unscaledTime * (active ? 150f : 82f), Time.unscaledTime * (active ? 52f : 28f));
                    stone.localPosition = composableGolemOrbitStonePositions[pair.Key] + new Vector3(0f, Mathf.Sin(Time.unscaledTime * (active ? 5.2f : 2.6f) + (int)pair.Key) * (active ? 0.045f : 0.018f), 0f);
                    stone.localScale = composableGolemOrbitStoneScales[pair.Key] * (active ? 1.18f * pulse : 0.92f);
                }
                return;
            }

            if (composableGolemOrbitImages.Count == 0)
                return;

            composableGolemOrbitRoot.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 42f);
            foreach (KeyValuePair<ComposableGolemForm, Image> pair in composableGolemOrbitImages)
            {
                Image image = pair.Value;
                if (image == null)
                    continue;

                bool active = pair.Key == composableGolemActiveForm;
                float pulse = active ? 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.08f : 1f;
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * (active ? 128f : 74f));
                image.rectTransform.localScale = Vector3.one * (active ? 1.18f * pulse : 0.92f);
            }
            UpdatePalatirShieldOrbit();
        }

        private void OnDestroy()
        {
            if (diceRootDetachedToScreen && diceRoot != null)
                Destroy(diceRoot);
            if (composableGolemOrbitScene != null)
                Destroy(composableGolemOrbitScene);
            if (composableGolemOrbitTexture != null)
            {
                composableGolemOrbitTexture.Release();
                Destroy(composableGolemOrbitTexture);
            }
        }

        public readonly struct StatusToken
        {
            public StatusToken(string label, Color color)
            {
                Label = label;
                Color = color;
            }

            public string Label { get; }
            public Color Color { get; }
        }

        public static PrototypeCardView Create(
            Transform parent,
            CardDefinition definition,
            GameConfiguration configuration)
        {
            var root = new GameObject(
                definition.Id,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(CanvasGroup),
                typeof(LayoutElement),
                typeof(Outline),
                typeof(PrototypeCardView));
            root.transform.SetParent(parent, false);

            PrototypeCardView view = root.GetComponent<PrototypeCardView>();
            view.Configure(definition, configuration);
            return view;
        }

        public static PrototypeCardView CreateBattlefieldPreview(
            Transform parent,
            CardDefinition definition,
            GameConfiguration configuration)
        {
            var root = new GameObject(
                definition.Id + "-preview",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(CanvasGroup),
                typeof(LayoutElement),
                typeof(Outline),
                typeof(PrototypeCardView));
            root.transform.SetParent(parent, false);

            PrototypeCardView view = root.GetComponent<PrototypeCardView>();
            view.ConfigureBattlefieldPreview(definition, configuration);
            return view;
        }

        private void Configure(CardDefinition definition, GameConfiguration gameConfiguration)
        {
            bool isPalatir = IsPalatirDefinition(definition);
            configuration = gameConfiguration;
            cardHeroClass = isPalatir ? HeroClass.Mage : definition.HeroClass;
            rectTransform = (RectTransform)transform;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            initialScale = Vector3.one;
            canvasGroup = GetComponent<CanvasGroup>();

            LayoutElement layout = GetComponent<LayoutElement>();
            layout.minWidth = 60f;
            layout.preferredWidth = 180f;
            layout.flexibleWidth = 1f;
            layout.minHeight = 90f;
            layout.preferredHeight = 268f;

            Image frame = GetComponent<Image>();
            frame.color = Color.clear;
            frame.raycastTarget = true;

            Button = GetComponent<Button>();
            Button.targetGraphic = frame;
            Button.transition = Selectable.Transition.None;
            ColorBlock colors = Button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.78f, 0.9f, 0.9f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            Button.colors = colors;

            selectionOutline = GetComponent<Outline>();
            selectionOutline.effectColor = new Color(0.2f, 1f, 0.65f);
            selectionOutline.effectDistance = new Vector2(5f, -5f);
            selectionOutline.enabled = false;
            liftShadow = gameObject.AddComponent<Shadow>();
            liftShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            liftShadow.effectDistance = new Vector2(0f, -12f);
            liftShadow.enabled = false;
            EnsureBattleAura();
            EnsureTurnAura();

            RectTransform artViewport = new GameObject(
                "Artwork Viewport",
                typeof(RectTransform)).GetComponent<RectTransform>();
            artViewport.SetParent(transform, false);
            (Vector2 artworkAnchorMin, Vector2 artworkAnchorMax) = isPalatir
                ? (new Vector2(0.055f, 0.085f), new Vector2(0.945f, 0.925f))
                : ArtworkViewportAnchors(definition.HeroClass);
            SetAnchors(artViewport, artworkAnchorMin, artworkAnchorMax);

            Color backdropColor = (isPalatir ? new Color(0.1f, 0.04f, 0.22f) : ClassColor(definition.HeroClass)) * 0.42f;
            backdropColor.a = 1f;
            Image artBackdrop = CreateImage("Artwork Backdrop", artViewport, backdropColor);
            defeatEffectImages.Add(artBackdrop);
            Stretch(artBackdrop.rectTransform);

            Image art = CreateImage("Artwork", artViewport, Color.white);
            defeatEffectImages.Add(art);
            Sprite resolvedArtwork = ResolveCardArtwork(definition);
            Sprite framedArtwork = isPalatir ? resolvedArtwork : CreateCoverArtwork(resolvedArtwork, definition.HeroClass);
            art.sprite = framedArtwork;
            if (art.sprite == null)
            {
                art.color = Color.clear;
                Debug.LogWarning($"[Accard N' Die] Artwork carta non risolto: {definition.Id}. La carta grande non usera' BattlePreviews.");
            }
            art.preserveAspect = framedArtwork == resolvedArtwork;
            Stretch(art.rectTransform);

            if (!isPalatir)
            {
                Image holder = CreateImage("Class Holder", transform, Color.white);
                defeatEffectImages.Add(holder);
                defeatEffectLayerAnchors.Add(holder.transform);
                holder.sprite = Resources.Load<Sprite>($"CardBorders/{BorderResourceName(definition.HeroClass)}");
                holder.preserveAspect = true;
                Stretch(holder.rectTransform);
            }

            Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
            strengthText = CreateText("Strength", transform, font, 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            strengthText.text = definition.Strength.ToString();
            strengthText.color = Color.white;
            Outline strengthOutline = strengthText.gameObject.AddComponent<Outline>();
            strengthOutline.effectColor = Color.black;
            strengthOutline.effectDistance = new Vector2(2.5f, -2.5f);
            strengthOutline.useGraphicAlpha = true;
            SetAnchors(strengthText.rectTransform, new Vector2(0.735f, 0.7f), new Vector2(0.935f, 0.88f));

            int descriptionFontSize = DescriptionFontSize(definition.HeroClass);
            Text statsText = CreateText("Description", transform, font, descriptionFontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
            statsText.text = isPalatir
                ? "COSMICO"
                : string.IsNullOrWhiteSpace(definition.RulesText)
                ? $"{CardRulesGlossary.HeroClassNameUpper(definition.HeroClass)}\n{CardRulesGlossary.ShortAbilityText(definition.HeroClass, configuration?.ClassBalance)}"
                : $"{CardRulesGlossary.HeroClassNameUpper(definition.HeroClass)}\n{definition.RulesText}";
            statsText.color = isPalatir ? new Color(0.9f, 0.96f, 1f) : DescriptionColor(definition.HeroClass);
            statsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statsText.verticalOverflow = VerticalWrapMode.Truncate;
            statsText.resizeTextMinSize = 8;
            statsText.resizeTextMaxSize = DescriptionMaxFontSize(definition.HeroClass);
            ApplyDescriptionReadability(statsText, definition.HeroClass);
            (Vector2 descriptionAnchorMin, Vector2 descriptionAnchorMax) = DescriptionAnchors(definition.HeroClass);
            SetAnchors(statsText.rectTransform, descriptionAnchorMin, descriptionAnchorMax);

            targetHintBackground = CreateImage("Target Hint Background", transform, Color.clear);
            SetAnchors(targetHintBackground.rectTransform, new Vector2(0.14f, 0.61f), new Vector2(0.86f, 0.75f));
            targetHintBackground.gameObject.SetActive(false);

            targetHintLabel = CreateText("Target Hint", transform, font, 21, FontStyle.Bold, TextAnchor.MiddleCenter);
            targetHintLabel.color = Color.white;
            Outline targetTextOutline = targetHintLabel.gameObject.AddComponent<Outline>();
            targetTextOutline.effectColor = Color.black;
            targetTextOutline.effectDistance = new Vector2(2f, -2f);
            SetAnchors(targetHintLabel.rectTransform, new Vector2(0.15f, 0.615f), new Vector2(0.85f, 0.745f));
            targetHintLabel.gameObject.SetActive(false);

            statusLabel = CreateText("Status", transform, font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            statusLabel.color = Color.white;
            SetAnchors(statusLabel.rectTransform, new Vector2(0.16f, 0.67f), new Vector2(0.84f, 0.75f));
            statusLabel.gameObject.SetActive(false);
            statusLabel.raycastTarget = false;
            CreateStatusIconRoot(new Vector2(0.04f, -0.035f), new Vector2(0.96f, 0.175f), 60f, 7f);

            defeatedLabel = CreateText("Defeated", transform, font, 27, FontStyle.Bold, TextAnchor.MiddleCenter);
            defeatedLabel.text = "ELIMINATA";
            defeatedLabel.color = new Color(1f, 0.26f, 0.22f);
            Stretch(defeatedLabel.rectTransform);
            defeatedLabel.gameObject.SetActive(false);

            CreateDiceView(font);
        }

        private void ConfigureBattlefieldPreview(CardDefinition definition, GameConfiguration gameConfiguration)
        {
            configuration = gameConfiguration;
            cardHeroClass = IsPalatirDefinition(definition) ? HeroClass.Mage : definition.HeroClass;
            rectTransform = (RectTransform)transform;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            initialScale = Vector3.one;
            canvasGroup = GetComponent<CanvasGroup>();

            LayoutElement layout = GetComponent<LayoutElement>();
            layout.minWidth = 76f;
            layout.preferredWidth = 170f;
            layout.flexibleWidth = 1f;
            layout.minHeight = 76f;
            layout.preferredHeight = 170f;

            Image frame = GetComponent<Image>();
            frame.color = Color.clear;
            frame.raycastTarget = true;

            Button = GetComponent<Button>();
            Button.targetGraphic = frame;
            Button.transition = Selectable.Transition.None;
            ColorBlock colors = Button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.78f, 0.9f, 0.9f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            Button.colors = colors;

            selectionOutline = GetComponent<Outline>();
            selectionOutline.effectColor = new Color(0.2f, 1f, 0.65f);
            selectionOutline.effectDistance = new Vector2(5f, -5f);
            selectionOutline.enabled = false;
            liftShadow = gameObject.AddComponent<Shadow>();
            liftShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            liftShadow.effectDistance = new Vector2(0f, -10f);
            liftShadow.enabled = false;
            EnsureBattleAura();
            EnsureTurnAura();

            Image preview = CreateImage("Battle Preview", transform, Color.white);
            defeatEffectImages.Add(preview);
            defeatEffectLayerAnchors.Add(preview.transform);
            preview.sprite = ResolveBattlePreviewSprite(definition);
            if (preview.sprite == null)
                preview.color = Color.clear;
            preview.preserveAspect = true;
            Stretch(preview.rectTransform);
            CreateComposableGolemOrbitIfNeeded(definition, preview);
            CreatePalatirShieldOrbitIfNeeded(definition, preview);

            Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
            strengthText = CreateText("Dynamic Strength", transform, font, 44, FontStyle.Bold, TextAnchor.MiddleCenter);
            strengthText.text = definition.Strength.ToString();
            strengthText.color = Color.white;
            Outline strengthOutline = strengthText.gameObject.AddComponent<Outline>();
            strengthOutline.effectColor = Color.black;
            strengthOutline.effectDistance = new Vector2(3.2f, -3.2f);
            strengthOutline.useGraphicAlpha = true;
            SetAnchors(strengthText.rectTransform, new Vector2(0.36f, 0.02f), new Vector2(0.64f, 0.25f));
            strengthText.transform.SetAsLastSibling();

            targetHintBackground = CreateImage("Target Hint Background", transform, Color.clear);
            SetAnchors(targetHintBackground.rectTransform, new Vector2(0.08f, -0.16f), new Vector2(0.92f, 0.04f));
            targetHintBackground.gameObject.SetActive(false);

            targetHintLabel = CreateText("Target Hint", transform, font, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            targetHintLabel.color = Color.white;
            Outline targetTextOutline = targetHintLabel.gameObject.AddComponent<Outline>();
            targetTextOutline.effectColor = Color.black;
            targetTextOutline.effectDistance = new Vector2(2f, -2f);
            SetAnchors(targetHintLabel.rectTransform, new Vector2(0.09f, -0.155f), new Vector2(0.91f, 0.035f));
            targetHintLabel.gameObject.SetActive(false);

            statusLabel = CreateText("Status", transform, font, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            statusLabel.color = Color.white;
            Outline statusOutline = statusLabel.gameObject.AddComponent<Outline>();
            statusOutline.effectColor = Color.black;
            statusOutline.effectDistance = new Vector2(2f, -2f);
            SetAnchors(statusLabel.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.23f));
            statusLabel.gameObject.SetActive(false);
            statusLabel.raycastTarget = false;
            CreateStatusIconRoot(new Vector2(0.04f, -0.18f), new Vector2(0.96f, 0.08f), 56f, 6f);

            defeatedLabel = CreateText("Defeated", transform, font, 21, FontStyle.Bold, TextAnchor.MiddleCenter);
            defeatedLabel.text = "KO";
            defeatedLabel.color = new Color(1f, 0.26f, 0.22f);
            Outline defeatedOutline = defeatedLabel.gameObject.AddComponent<Outline>();
            defeatedOutline.effectColor = Color.black;
            defeatedOutline.effectDistance = new Vector2(2.5f, -2.5f);
            Stretch(defeatedLabel.rectTransform);
            defeatedLabel.gameObject.SetActive(false);

            CreateDiceView(font);
        }

        public void SetStrengthValue(int value)
        {
            if (strengthText != null)
                strengthText.text = value.ToString();
        }

        public void SetComposableGolemForm(ComposableGolemForm form, bool animate = true)
        {
            composableGolemActiveForm = form;
            SetComposableGolemOrbitVisible(true);
            RefreshComposableGolemOrbitVisuals();
        }

        public void SetPalatirShields(IReadOnlyCollection<ClassFamily> shields)
        {
            activePalatirShields.Clear();
            if (shields != null)
            {
                foreach (ClassFamily shield in shields)
                    activePalatirShields.Add(shield);
            }

            RefreshPalatirShieldVisuals();
        }

        public void RaiseStrengthText()
        {
            if (strengthText != null)
                strengthText.transform.SetAsLastSibling();
        }

        private void CreateComposableGolemOrbitIfNeeded(CardDefinition definition, Image preview)
        {
            if (definition == null || definition.Id != "miniboss-composable-golem")
                return;

            if (preview != null)
                preview.color = Color.white;

            composableGolemOrbitRoot = new GameObject(
                "Composable Golem Orbit",
                typeof(RectTransform),
                typeof(CanvasRenderer)).GetComponent<RectTransform>();
            composableGolemOrbitRoot.SetParent(transform, false);
            SetAnchors(composableGolemOrbitRoot, new Vector2(-0.18f, -0.18f), new Vector2(1.18f, 1.18f));
            composableGolemOrbitRoot.SetAsLastSibling();

            if (!CreateComposableGolem3DOrbit())
            {
                CreateComposableGolemOrbitImage(ComposableGolemForm.Iron, "Minibosses/ComposableGolem/Previews/golem_orbit_iron_preview", new Vector2(0f, 70f), new Vector2(58f, 58f), new Color(1f, 0.62f, 0.18f, 0.92f));
                CreateComposableGolemOrbitImage(ComposableGolemForm.Crystal, "Minibosses/ComposableGolem/Previews/golem_orbit_crystal_preview", new Vector2(-62f, -38f), new Vector2(44f, 44f), new Color(0.12f, 0.86f, 1f, 0.9f));
                CreateComposableGolemOrbitImage(ComposableGolemForm.Glass, "Minibosses/ComposableGolem/Previews/golem_orbit_glass_preview", new Vector2(62f, -38f), new Vector2(60f, 60f), new Color(0.62f, 1f, 0.9f, 0.82f));
            }
            RefreshComposableGolemOrbitVisuals();
        }

        private bool CreateComposableGolem3DOrbit()
        {
            GameObject iron = Resources.Load<GameObject>("Minibosses/ComposableGolem/Prefabs/GolemOrbit_Iron");
            GameObject crystal = Resources.Load<GameObject>("Minibosses/ComposableGolem/Prefabs/GolemOrbit_Crystal");
            GameObject glass = Resources.Load<GameObject>("Minibosses/ComposableGolem/Prefabs/GolemOrbit_Glass");
            if (iron == null || crystal == null || glass == null)
                return false;

            composableGolemOrbitRenderImage = new GameObject(
                "Composable Golem 3D Stones",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage)).GetComponent<RawImage>();
            composableGolemOrbitRenderImage.transform.SetParent(composableGolemOrbitRoot, false);
            Stretch(composableGolemOrbitRenderImage.rectTransform);
            composableGolemOrbitRenderImage.raycastTarget = false;

            composableGolemOrbitTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
            {
                name = "Composable Golem Stones RT",
                antiAliasing = 4
            };
            composableGolemOrbitTexture.Create();
            composableGolemOrbitRenderImage.texture = composableGolemOrbitTexture;
            composableGolemOrbitRenderImage.color = Color.white;

            composableGolemOrbitScene = new GameObject("Composable Golem Stones Render Scene");
            composableGolemOrbitScene.transform.position = new Vector3(16000f, 16000f, 16000f);
            composableGolemOrbit3DRoot = new GameObject("Composable Golem Stones Orbit Root").transform;
            composableGolemOrbit3DRoot.SetParent(composableGolemOrbitScene.transform, false);

            CreateComposableGolemOrbitStone(ComposableGolemForm.Iron, iron, new Vector3(0f, 0f, -0.92f), Vector3.one * 0.48f);
            CreateComposableGolemOrbitStone(ComposableGolemForm.Crystal, crystal, new Vector3(-0.82f, 0f, 0.48f), Vector3.one * 0.36f);
            CreateComposableGolemOrbitStone(ComposableGolemForm.Glass, glass, new Vector3(0.82f, 0f, 0.48f), Vector3.one * 0.5f);

            composableGolemOrbitCamera = new GameObject("Composable Golem Stones Camera", typeof(Camera)).GetComponent<Camera>();
            composableGolemOrbitCamera.transform.SetParent(composableGolemOrbitScene.transform, false);
            composableGolemOrbitCamera.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            composableGolemOrbitCamera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            composableGolemOrbitCamera.clearFlags = CameraClearFlags.SolidColor;
            composableGolemOrbitCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            composableGolemOrbitCamera.orthographic = true;
            composableGolemOrbitCamera.orthographicSize = 1.28f;
            composableGolemOrbitCamera.nearClipPlane = 0.01f;
            composableGolemOrbitCamera.farClipPlane = 10f;
            composableGolemOrbitCamera.targetTexture = composableGolemOrbitTexture;

            Light light = new GameObject("Composable Golem Stones Light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(composableGolemOrbitScene.transform, false);
            light.transform.localPosition = new Vector3(0.2f, 2.4f, -0.4f);
            light.type = LightType.Directional;
            light.intensity = 1.55f;
            light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            return true;
        }

        private void CreateComposableGolemOrbitStone(ComposableGolemForm form, GameObject prefab, Vector3 position, Vector3 scale)
        {
            Transform stone = Instantiate(prefab, composableGolemOrbit3DRoot, false).transform;
            stone.name = form + " Orbit Stone";
            stone.localPosition = position;
            stone.localRotation = Quaternion.identity;
            stone.localScale = scale;
            composableGolemOrbitStones[form] = stone;
            composableGolemOrbitStonePositions[form] = position;
            composableGolemOrbitStoneScales[form] = scale;
        }

        private void CreateComposableGolemOrbitImage(ComposableGolemForm form, string spritePath, Vector2 position, Vector2 size, Color fallbackColor)
        {
            if (composableGolemOrbitRoot == null)
                return;

            Image image = CreateImage(form + " Orbit", composableGolemOrbitRoot, fallbackColor);
            image.sprite = LoadPreviewSpriteWithTransparentBlack(spritePath);
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            composableGolemOrbitImages[form] = image;
        }

        private void RefreshComposableGolemOrbitVisuals()
        {
            foreach (KeyValuePair<ComposableGolemForm, Image> pair in composableGolemOrbitImages)
            {
                Image image = pair.Value;
                if (image == null)
                    continue;

                bool active = pair.Key == composableGolemActiveForm;
                image.color = GolemVfxPrimary(pair.Key);
                image.color = new Color(image.color.r, image.color.g, image.color.b, active ? 1f : 0.52f);
                image.rectTransform.localScale = Vector3.one * (active ? 1.18f : 0.92f);
                image.transform.SetAsLastSibling();
            }
        }

        private void SetComposableGolemOrbitVisible(bool visible)
        {
            if (composableGolemOrbitRoot != null)
                composableGolemOrbitRoot.gameObject.SetActive(visible);
        }

        private void CreatePalatirShieldOrbitIfNeeded(CardDefinition definition, Image preview)
        {
            if (definition == null || definition.Id != "boss-palatir")
                return;

            if (preview != null)
                preview.color = Color.white;

            palatirShieldOrbitRoot = new GameObject(
                "Palatir Family Shields Orbit",
                typeof(RectTransform),
                typeof(CanvasRenderer)).GetComponent<RectTransform>();
            palatirShieldOrbitRoot.SetParent(transform, false);
            SetAnchors(palatirShieldOrbitRoot, new Vector2(-0.48f, -0.48f), new Vector2(1.48f, 1.48f));
            palatirShieldOrbitRoot.SetAsLastSibling();

            CreatePalatirShieldImage(ClassFamily.Might, new Vector2(0f, 112f), PalatirShieldColor(ClassFamily.Might, 0.96f));
            CreatePalatirShieldImage(ClassFamily.Cunning, new Vector2(-106f, -62f), PalatirShieldColor(ClassFamily.Cunning, 0.96f));
            CreatePalatirShieldImage(ClassFamily.Magic, new Vector2(106f, -62f), PalatirShieldColor(ClassFamily.Magic, 0.96f));
            SetPalatirShields(new[] { ClassFamily.Might, ClassFamily.Cunning, ClassFamily.Magic });
        }

        private void CreatePalatirShieldImage(ClassFamily family, Vector2 position, Color color)
        {
            if (palatirShieldOrbitRoot == null)
                return;

            Image image = CreateImage("Palatir " + family + " Shield", palatirShieldOrbitRoot, color);
            image.sprite = CreatePalatirWorldSprite(family);
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(74f, 74f);
            palatirShieldImages[family] = image;
        }

        private void RefreshPalatirShieldVisuals()
        {
            foreach (KeyValuePair<ClassFamily, Image> pair in palatirShieldImages)
            {
                if (pair.Value == null)
                    continue;

                bool active = activePalatirShields.Contains(pair.Key);
                pair.Value.gameObject.SetActive(active);
                pair.Value.color = PalatirShieldColor(pair.Key, active ? 0.98f : 0f);
            }

            if (palatirShieldOrbitRoot != null)
                palatirShieldOrbitRoot.gameObject.SetActive(activePalatirShields.Count > 0);
        }

        private void UpdatePalatirShieldOrbit()
        {
            if (palatirShieldOrbitRoot == null || activePalatirShields.Count == 0)
                return;

            palatirShieldOrbitRoot.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 36f);
            int visibleIndex = 0;
            foreach (ClassFamily family in new[] { ClassFamily.Might, ClassFamily.Cunning, ClassFamily.Magic })
            {
                if (!palatirShieldImages.TryGetValue(family, out Image image) || image == null || !activePalatirShields.Contains(family))
                    continue;

                float angle = (360f / activePalatirShields.Count) * visibleIndex + Time.unscaledTime * 12f;
                float radians = angle * Mathf.Deg2Rad;
                image.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(radians) * 112f, Mathf.Cos(radians) * 112f);
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -palatirShieldOrbitRoot.localEulerAngles.z - angle * 0.25f);
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.5f + visibleIndex) * 0.08f;
                image.rectTransform.localScale = Vector3.one * pulse;
                visibleIndex++;
            }
        }

        private static Color PalatirShieldColor(ClassFamily family, float alpha)
        {
            Color color = family switch
            {
                ClassFamily.Might => new Color(1f, 0.08f, 0.04f, alpha),
                ClassFamily.Cunning => new Color(0.12f, 1f, 0.34f, alpha),
                ClassFamily.Magic => new Color(0.16f, 0.52f, 1f, alpha),
                _ => new Color(1f, 1f, 1f, alpha)
            };
            return color;
        }

        private static Sprite CreatePalatirWorldSprite(ClassFamily family)
        {
            string cacheKey = "palatir-world-" + family;
            if (runtimePreviewSprites.TryGetValue(cacheKey, out Sprite cached))
                return cached;

            const int width = 96;
            const int height = 96;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color color = PalatirShieldColor(family, 1f);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - width * 0.5f) / (width * 0.5f);
                    float ny = (y - height * 0.5f) / (height * 0.5f);
                    float distance = Mathf.Sqrt(nx * nx + ny * ny);
                    float planet = Mathf.Clamp01((1f - distance) * 7f);
                    float atmosphere = Mathf.Clamp01((1.18f - distance) * 3.2f) * (1f - planet * 0.35f);
                    float rim = Mathf.Clamp01((distance - 0.68f) * 4.2f) * planet;
                    float bandA = Mathf.Clamp01(1f - Mathf.Abs(ny + Mathf.Sin(nx * 4.8f) * 0.08f) * 9f) * planet;
                    float bandB = Mathf.Clamp01(1f - Mathf.Abs(ny * 1.6f - 0.36f + Mathf.Sin(nx * 7.2f) * 0.05f) * 10f) * planet;
                    float highlight = Mathf.Clamp01(1f - Mathf.Sqrt((nx + 0.32f) * (nx + 0.32f) * 4.2f + (ny - 0.34f) * (ny - 0.34f) * 4.2f));
                    float glow = Mathf.Clamp01(planet * (0.45f + rim * 0.36f + highlight * 0.42f + bandA * 0.28f + bandB * 0.18f));
                    float alphaF = Mathf.Clamp01(planet + atmosphere * 0.7f);
                    byte alpha = (byte)(alphaF * 245f);
                    pixels[y * width + x] = new Color32(
                        (byte)Mathf.Lerp(8f, color.r * 255f, glow + atmosphere * 0.22f),
                        (byte)Mathf.Lerp(10f, color.g * 255f, glow + atmosphere * 0.22f),
                        (byte)Mathf.Lerp(22f, color.b * 255f, glow + atmosphere * 0.22f),
                        alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Palatir " + family + " World";
            runtimePreviewSprites[cacheKey] = sprite;
            return sprite;
        }

        private static Sprite CreateCoverArtwork(Sprite source, HeroClass heroClass)
        {
            if (source == null)
                return null;

            string cacheKey = $"{source.GetInstanceID()}:{heroClass}";
            if (croppedArtworkSprites.TryGetValue(cacheKey, out Sprite cached))
                return cached;

            Rect sourceRect = source.textureRect;
            float sourceAspect = sourceRect.width / sourceRect.height;
            float croppedWidth;
            float croppedHeight;
            if (sourceAspect > ArtworkViewportAspect)
            {
                croppedHeight = sourceRect.height;
                croppedWidth = croppedHeight * ArtworkViewportAspect;
            }
            else
            {
                croppedWidth = sourceRect.width;
                croppedHeight = croppedWidth / ArtworkViewportAspect;
            }

            croppedWidth = Mathf.Min(sourceRect.width, croppedWidth * ArtworkCropOverscan);
            croppedHeight = Mathf.Min(sourceRect.height, croppedHeight * ArtworkCropOverscan);
            float croppedX = sourceRect.x + (sourceRect.width - croppedWidth) * 0.5f;
            float verticalAlignment = ArtworkCropVerticalAlignment(heroClass);
            float croppedY = sourceRect.y + (sourceRect.height - croppedHeight) * verticalAlignment;
            Rect croppedRect = new(
                croppedX,
                croppedY,
                croppedWidth,
                croppedHeight);
            Sprite cropped = Sprite.Create(
                source.texture,
                croppedRect,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit,
                0u,
                SpriteMeshType.FullRect,
                Vector4.zero);
            cropped.name = source.name + " Card Cover";
            croppedArtworkSprites[cacheKey] = cropped;
            return cropped;
        }

        private static bool IsPalatirDefinition(CardDefinition definition)
        {
            return definition != null && string.Equals(definition.Id, "boss-palatir", StringComparison.OrdinalIgnoreCase);
        }

        public void SetSelected(bool selected)
        {
            SetSelected(selected, draftStyle: false);
        }

        public void SetDraftSelected(bool selected)
        {
            SetSelected(selected, draftStyle: true);
        }

        private void SetSelected(bool selected, bool draftStyle)
        {
            if (dragging)
            {
                selectedVisual = selected;
                ApplySelectionChrome(true, draftStyle);
                return;
            }

            if (selectedVisual == selected)
            {
                ApplySelectionChrome(selected, draftStyle);
                return;
            }

            selectedVisual = selected;
            ApplySelectionChrome(selected, draftStyle);

            Vector3 targetScale = selected
                ? Vector3.one * configuration.Animation.SelectedCardScale
                : initialScale;
            AnimateSelectionScale(targetScale, 0.12f);
        }

        public void SetTargetHint(bool highlighted, Color color)
        {
            EnsureTargetHintAura();
            if (highlighted)
            {
                selectionOutline.enabled = true;
                selectionOutline.effectColor = color;
                SetTargetHintAura(true, color);
                if (liftShadow != null && !dragging)
                    liftShadow.enabled = true;
                return;
            }

            SetTargetHintAura(false, Color.clear);
            ApplySelectionChrome(selectedVisual, draftStyle: false);
        }

        private Color GetClassColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => new Color(0.15f, 0.85f, 0.35f, 1f),    // Toxic green
                HeroClass.Warrior => new Color(0.85f, 0.15f, 0.15f, 1f),     // Warrior Crimson
                HeroClass.Mage => new Color(0.15f, 0.65f, 0.95f, 1f),        // Arcane Blue
                HeroClass.Paladin => new Color(0.95f, 0.75f, 0.15f, 1f),     // Divine Gold
                HeroClass.Rogue => new Color(0.45f, 0.45f, 0.5f, 1f),        // Rogue Dark Gray
                HeroClass.Hunter => new Color(0.92f, 0.45f, 0.08f, 1f),      // Hunter Orange
                HeroClass.Barbarian => new Color(0.9f, 0.45f, 0.1f, 1f),     // Barbarian Orange
                HeroClass.Necromancer => new Color(0.65f, 0.15f, 0.85f, 1f),  // Necromancer Purple
                HeroClass.Priest => new Color(0.9f, 0.9f, 0.95f, 1f),        // Priest Pure White/Silver
                _ => new Color(0.2f, 1f, 0.65f, 1f)                          // Fallback cyan-green
            };
        }

        private void ApplySelectionChrome(bool selected)
        {
            ApplySelectionChrome(selected, draftStyle: false);
        }

        private void ApplySelectionChrome(bool selected, bool draftStyle)
        {
            selectionOutline.enabled = selected;
            selectionOutline.effectColor = draftStyle ?SelectedAuraGold : SelectedBattlefieldGreen;
            if (liftShadow != null && !dragging)
                liftShadow.enabled = selected;
            SetSelectedAura(selected && draftStyle);
        }

        public void SetTurnAura(bool active, bool playerOwned)
        {
            EnsureTurnAura();
            turnAuraColor = playerOwned
                ? new Color(0.25f, 1f, 0.78f, 1f)
                : new Color(1f, 0.34f, 0.24f, 1f);

            if (turnAuraActive == active)
                return;

            turnAuraActive = active;
            if (active)
            {
                turnAuraRoot.SetActive(true);
                if (turnAuraCoroutine == null)
                    turnAuraCoroutine = StartCoroutine(AnimateTurnAura());
            }
            else
            {
                if (turnAuraCoroutine != null)
                {
                    StopCoroutine(turnAuraCoroutine);
                    turnAuraCoroutine = null;
                }
                turnAuraRoot.SetActive(false);
                turnAuraRect.localScale = Vector3.one;
            }
        }

        public void SetBattleAura(bool active, Color color, string label)
        {
            EnsureBattleAura();
            battleAuraColor = color;
            battleAuraActive = active;
            battleAuraRoot.SetActive(active);
            if (battleAuraLabel != null)
                battleAuraLabel.text = label ?? string.Empty;

            if (active)
            {
                if (battleAuraCoroutine == null)
                    battleAuraCoroutine = StartCoroutine(AnimateBattleAura());
            }
            else
            {
                if (battleAuraCoroutine != null)
                {
                    StopCoroutine(battleAuraCoroutine);
                    battleAuraCoroutine = null;
                }
                battleAuraRect.localScale = Vector3.one;
            }
        }

        public void PlayDeploymentAnimation(
            Vector3 startWorldPosition,
            Quaternion startWorldRotation,
            float duration)
        {
            if (motionCoroutine != null)
                StopCoroutine(motionCoroutine);
            motionCoroutine = StartCoroutine(DeploymentRoutine(
                startWorldPosition,
                startWorldRotation,
                duration));
        }

        public void PlayRevealAnimation(float duration)
        {
            if (motionCoroutine != null)
                StopCoroutine(motionCoroutine);
            motionCoroutine = StartCoroutine(RevealRoutine(duration));
        }

        public void SetAlpha(float alpha)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = alpha;
            RefreshRaycastBlocking();
        }

        public void SetLayoutIgnored(bool ignored)
        {
            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout != null)
                layout.ignoreLayout = ignored;
        }

        public void SetInteractable(bool interactable)
        {
            Button.interactable = interactable;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.interactable = interactable;
            RefreshRaycastBlocking();
        }

        public void SetDragHandlers(
            Func<bool> canStartDrag,
            UnityAction<PrototypeCardView, PointerEventData> onDragStarted,
            UnityAction<PrototypeCardView, PointerEventData> onDragEnded)
        {
            canDrag = canStartDrag;
            dragStarted = onDragStarted;
            dragEnded = onDragEnded;
        }

        public void ClearDragHandlers()
        {
            canDrag = null;
            dragStarted = null;
            dragEnded = null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Button == null || !Button.interactable || canDrag == null || !canDrag())
                return;

            // The input module can momentarily drop the pointer mid-gesture (a stray
            // pointer-up), raising OnEndDrag and then OnBeginDrag again on the very next
            // frame. The end is deferred (see FinalizeDragEnd); if a fresh begin lands in
            // that window we cancel it and keep the same drag, instead of tearing the card
            // out of the hand and rebuilding it every frame, which was the flicker.
            if (pendingDragEndRoutine != null)
            {
                StopCoroutine(pendingDragEndRoutine);
                pendingDragEndRoutine = null;
                return;
            }

            if (dragging)
                return;

            dragging = true;
            suppressDragRestore = false;
            dragHomeParent = transform.parent;
            dragHomeSiblingIndex = transform.GetSiblingIndex();
            dragHomeAnchoredPosition = RectTransform.anchoredPosition;
            Vector3 dragStartWorldPosition = RectTransform.position;
            Quaternion dragStartWorldRotation = RectTransform.rotation;
            dragCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            dragCanvasRect = dragCanvas != null ? dragCanvas.transform as RectTransform : null;
            SetLayoutIgnored(true);
            CreateDragPlaceholder();
            RebuildDragHomeLayout();
            if (dragCanvasRect != null)
            {
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        dragCanvasRect,
                        eventData.position,
                        eventData.pressEventCamera,
                        out Vector3 pointerWorld))
                {
                    dragWorldOffset = dragStartWorldPosition - pointerWorld;
                }

                transform.SetParent(dragCanvasRect, true);
                RectTransform.position = dragStartWorldPosition;
                RectTransform.rotation = dragStartWorldRotation;
            }

            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
            selectionOutline.enabled = true;
            selectionOutline.effectColor = SelectedBattlefieldGreen;
            SetSelectedAura(false);
            if (liftShadow != null)
                liftShadow.enabled = true;
            StopSelectionScaleAnimation();
            rectTransform.localScale = Vector3.one * (configuration.Animation.SelectedCardScale + 0.035f);
            dragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || dragCanvasRect == null)
                return;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    dragCanvasRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector3 pointerWorld))
            {
                RectTransform.position = pointerWorld + dragWorldOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
                return;

            // Don't finalize immediately: a spurious end is followed by a fresh
            // OnBeginDrag on the next frame (which cancels this routine). Only a genuine
            // release lets the routine run to completion and restore the card to the hand.
            if (pendingDragEndRoutine != null)
                StopCoroutine(pendingDragEndRoutine);

            if (!gameObject.activeInHierarchy)
            {
                FinalizeDragEnd(eventData);
                return;
            }

            pendingDragEndRoutine = StartCoroutine(FinalizeDragEndAfterDelay(eventData));
        }

        private IEnumerator FinalizeDragEndAfterDelay(PointerEventData eventData)
        {
            yield return null;
            yield return null;
            pendingDragEndRoutine = null;
            FinalizeDragEnd(eventData);
        }

        private void FinalizeDragEnd(PointerEventData eventData)
        {
            if (!dragging)
                return;

            dragging = false;
            canvasGroup.alpha = 1f;
            dragEnded?.Invoke(this, eventData);
            RefreshRaycastBlocking();
            RestoreSelectionVisuals();
            if (!suppressDragRestore)
            {
                RestoreDragHome();
            }
            else
            {
                RestoreSuppressedDragHome();
                DestroyDragPlaceholder();
                RebuildDragHomeLayout();
            }
        }

        public void SuppressCurrentDragRestore()
        {
            suppressDragRestore = true;
        }

        private void RestoreDragHome()
        {
            if (dragHomeParent != null && transform.parent != dragHomeParent)
                transform.SetParent(dragHomeParent, false);
            transform.SetSiblingIndex(dragHomeSiblingIndex);
            RectTransform.anchoredPosition = dragHomeAnchoredPosition;
            SetLayoutIgnored(false);
            DestroyDragPlaceholder();
            RebuildDragHomeLayout();
            dragCanvas = null;
            dragCanvasRect = null;
        }

        private void RestoreSuppressedDragHome()
        {
            if (dragHomeParent != null && transform.parent != dragHomeParent)
                transform.SetParent(dragHomeParent, false);
            transform.SetSiblingIndex(dragHomeSiblingIndex);
            RectTransform.anchoredPosition = dragHomeAnchoredPosition;
            dragCanvas = null;
            dragCanvasRect = null;
        }

        private void CreateDragPlaceholder()
        {
            DestroyDragPlaceholder();
            if (dragHomeParent == null)
                return;

            dragPlaceholder = new GameObject("Card Drag Placeholder", typeof(RectTransform), typeof(LayoutElement));
            RectTransform placeholderRect = (RectTransform)dragPlaceholder.transform;
            placeholderRect.SetParent(dragHomeParent, false);
            placeholderRect.SetSiblingIndex(dragHomeSiblingIndex);
            placeholderRect.sizeDelta = RectTransform.rect.size;

            LayoutElement sourceLayout = GetComponent<LayoutElement>();
            LayoutElement placeholderLayout = dragPlaceholder.GetComponent<LayoutElement>();
            if (sourceLayout != null)
            {
                placeholderLayout.minWidth = sourceLayout.minWidth;
                placeholderLayout.minHeight = sourceLayout.minHeight;
                placeholderLayout.preferredWidth = sourceLayout.preferredWidth;
                placeholderLayout.preferredHeight = sourceLayout.preferredHeight;
                placeholderLayout.flexibleWidth = sourceLayout.flexibleWidth;
                placeholderLayout.flexibleHeight = sourceLayout.flexibleHeight;
            }
        }

        private void DestroyDragPlaceholder()
        {
            if (dragPlaceholder == null)
                return;

            Destroy(dragPlaceholder);
            dragPlaceholder = null;
        }

        private void RebuildDragHomeLayout()
        {
            if (dragHomeParent is RectTransform parentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                Canvas.ForceUpdateCanvases();
            }
        }

        private void RefreshRaycastBlocking()
        {
            if (canvasGroup == null)
                return;

            bool visible = canvasGroup.alpha > 0.01f;
            bool interactable = Button != null && Button.interactable;
            canvasGroup.blocksRaycasts = !dragging && visible && interactable;
        }

        private void RestoreSelectionVisuals()
        {
            ApplySelectionChrome(selectedVisual);

            Vector3 targetScale = selectedVisual
                ? Vector3.one * configuration.Animation.SelectedCardScale
                : initialScale;
            AnimateSelectionScale(targetScale, 0.1f);
        }

        private void AnimateSelectionScale(Vector3 targetScale, float duration)
        {
            StopSelectionScaleAnimation();

            if (!gameObject.activeInHierarchy || duration <= 0f)
            {
                rectTransform.localScale = targetScale;
                return;
            }

            selectionScaleCoroutine = StartCoroutine(SelectionScaleRoutine(targetScale, duration));
        }

        private void StopSelectionScaleAnimation()
        {
            if (selectionScaleCoroutine == null)
                return;

            StopCoroutine(selectionScaleCoroutine);
            selectionScaleCoroutine = null;
        }

        private IEnumerator SelectionScaleRoutine(Vector3 targetScale, float duration)
        {
            Vector3 startScale = rectTransform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                rectTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, progress);
                yield return null;
            }

            rectTransform.localScale = targetScale;
            selectionScaleCoroutine = null;
        }

        public void ShowClassAction(Sprite icon, UnityAction action)
        {
            ClearActionOverlay();
            RectTransform root = EnsureActionOverlay();
            SetActionOverlayBounds(new Vector2(0.03f, 0.94f), new Vector2(0.97f, 1.48f));
            Button button = CreateIconActionButton("Attack Action", root, icon, "Attacca", ActionLabelRed);
            SetAnchors(button.GetComponent<RectTransform>(), new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.93f));
            if (action != null)
                button.onClick.AddListener(action);
        }

        public void ShowDualActions(
            Sprite firstIcon,
            UnityAction firstAction,
            Sprite secondIcon,
            UnityAction secondAction)
        {
            ClearActionOverlay();
            RectTransform root = EnsureActionOverlay();
            SetActionOverlayBounds(new Vector2(0.03f, 0.94f), new Vector2(0.97f, 1.48f));

            Button first = CreateIconActionButton("Attack Action", root, firstIcon, "Attacca", ActionLabelRed);
            SetAnchors(first.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.47f, 0.93f));
            if (firstAction != null)
                first.onClick.AddListener(firstAction);

            Button second = CreateIconActionButton(
                "Secondary Card Action",
                root,
                secondIcon,
                GetActionLabelForSprite(secondIcon),
                GetActionLabelColorForSprite(secondIcon));
            SetAnchors(second.GetComponent<RectTransform>(), new Vector2(0.53f, 0.03f), new Vector2(0.98f, 0.93f));
            if (secondAction != null)
                second.onClick.AddListener(secondAction);
        }

        public void ShowTripleActions(
            Sprite firstIcon,
            UnityAction firstAction,
            Sprite secondIcon,
            UnityAction secondAction,
            Sprite thirdIcon,
            UnityAction thirdAction)
        {
            ClearActionOverlay();
            RectTransform root = EnsureActionOverlay();
            SetActionOverlayBounds(new Vector2(-0.02f, 0.92f), new Vector2(1.02f, 1.54f));

            Button first = CreateIconActionButton("Attack Action", root, firstIcon, "Attacca", ActionLabelRed);
            SetAnchors(first.GetComponent<RectTransform>(), new Vector2(0.00f, 0.02f), new Vector2(0.34f, 0.72f));
            if (firstAction != null)
                first.onClick.AddListener(firstAction);

            Button second = CreateIconActionButton("Ability Action", root, secondIcon, "Abilit\u00E0", ActionLabelBlue);
            SetAnchors(second.GetComponent<RectTransform>(), new Vector2(0.33f, 0.26f), new Vector2(0.67f, 0.96f));
            if (secondAction != null)
                second.onClick.AddListener(secondAction);

            Button third = CreateIconActionButton("Attachment Action", root, thirdIcon, "Equipaggia", ActionLabelOrange);
            SetAnchors(third.GetComponent<RectTransform>(), new Vector2(0.66f, 0.02f), new Vector2(1.00f, 0.72f));
            if (thirdAction != null)
                third.onClick.AddListener(thirdAction);
        }

        public void ShowConfirmCancelActions(Sprite confirmIcon, Sprite cancelIcon, UnityAction confirmAction, UnityAction cancelAction)
        {
            ClearActionOverlay();
            RectTransform root = EnsureActionOverlay();
            SetActionOverlayBounds(new Vector2(0.03f, 0.94f), new Vector2(0.97f, 1.48f));

            Button confirm = CreateIconActionButton("Confirm Action", root, confirmIcon, "Conferma", ActionLabelGreen);
            SetAnchors(confirm.GetComponent<RectTransform>(), new Vector2(0.00f, 0.03f), new Vector2(0.43f, 0.93f));
            if (confirmAction != null)
                confirm.onClick.AddListener(confirmAction);

            Button cancel = CreateIconActionButton("Cancel Action", root, cancelIcon, "Cancella", ActionLabelRed);
            SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.57f, 0.03f), new Vector2(1.00f, 0.93f));
            if (cancelAction != null)
                cancel.onClick.AddListener(cancelAction);
        }

        public void ShowConfirmInfoActions(Sprite confirmIcon, Sprite infoIcon, UnityAction confirmAction, UnityAction infoAction)
        {
            ClearActionOverlay();
            RectTransform root = EnsureActionOverlay();
            SetActionOverlayBounds(new Vector2(0.08f, 0.94f), new Vector2(0.92f, 1.46f));

            Button confirm = CreateIconActionButton("Confirm Action", root, confirmIcon, "Conferma", ActionLabelGreen);
            SetAnchors(confirm.GetComponent<RectTransform>(), new Vector2(0.00f, 0.03f), new Vector2(0.43f, 0.93f));
            SetActionButtonLabelAnchors(confirm, new Vector2(-0.08f, 0.78f), new Vector2(1.08f, 1.12f));
            if (confirmAction != null)
                confirm.onClick.AddListener(confirmAction);

            Button info = CreateIconActionButton("Info Action", root, infoIcon, "Info", ActionLabelBlue);
            SetAnchors(info.GetComponent<RectTransform>(), new Vector2(0.57f, 0.03f), new Vector2(1.00f, 0.93f));
            SetActionButtonLabelAnchors(info, new Vector2(-0.08f, 0.78f), new Vector2(1.08f, 1.12f));
            if (infoAction != null)
                info.onClick.AddListener(infoAction);
        }

        public void ShowCancelAction(Sprite cancelIcon, UnityAction cancelAction)
        {
            ClearActionOverlay();
            RectTransform root = EnsureActionOverlay();
            SetActionOverlayBounds(new Vector2(0.03f, 0.94f), new Vector2(0.97f, 1.48f));
            Button cancel = CreateIconActionButton("Cancel Action", root, cancelIcon, "Cancella", ActionLabelRed);
            SetAnchors(cancel.GetComponent<RectTransform>(), new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.93f));
            if (cancelAction != null)
                cancel.onClick.AddListener(cancelAction);
        }

        public void ShowCardClickAction(UnityAction action)
        {
            ClearActionOverlay();
            RectTransform root = EnsureActionOverlay();
            SetActionOverlayBounds(Vector2.zero, Vector2.one);

            Button button = CreateTransparentActionButton("Switch Deployment Choice", root);
            Stretch(button.GetComponent<RectTransform>());
            if (action != null)
                button.onClick.AddListener(action);
        }

        public void ClearActionOverlay()
        {
            if (actionOverlayRoot == null)
                return;

            for (int index = actionOverlayRoot.childCount - 1; index >= 0; index--)
                Destroy(actionOverlayRoot.GetChild(index).gameObject);
            actionOverlayRoot.gameObject.SetActive(false);
        }

        public void SetTargetHint(MatchupResult? matchup)
        {
            EnsureTargetHintAura();
            targetHintLabel.gameObject.SetActive(false);
            targetHintBackground.gameObject.SetActive(false);

            if (!matchup.HasValue)
            {
                selectionOutline.enabled = false;
                SetTargetHintAura(false, Color.clear);
                return;
            }

            selectionOutline.enabled = true;
            switch (matchup.Value)
            {
                case MatchupResult.Advantage:
                    SetTargetHintColor(configuration.Visual.TargetAdvantageColor);
                    break;
                case MatchupResult.Disadvantage:
                    SetTargetHintColor(configuration.Visual.TargetDisadvantageColor);
                    break;
                default:
                    SetTargetHintColor(configuration.Visual.TargetNeutralColor);
                    break;
            }
        }

        public void SetInitiative(int initiative)
        {
            // L'iniziativa viene mostrata esclusivamente nella timeline dei turni.
        }

        public void SetStatus(string status, Color color)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                SetStatuses(Array.Empty<StatusToken>());
                return;
            }

            SetStatuses(new StatusToken(status, color));
        }

        public void SetHealthBar(int current, int maximum, Color fillColor)
        {
            SetLifeIconsVisible(false);

            if (maximum <= 0)
            {
                SetHealthBarVisible(false);
                return;
            }

            if (healthBarRoot == null)
                CreateHealthBar(AccardND.Battlefield.MmoUiTheme.BodyFont);

            int clampedCurrent = Mathf.Clamp(current, 0, maximum);
            float normalized = Mathf.Clamp01((float)clampedCurrent / maximum);
            healthBarRoot.SetActive(true);
            healthBarRoot.transform.SetAsLastSibling();
            float fillRight = Mathf.Lerp(0.085f, 0.915f, normalized);
            float glowRight = Mathf.Lerp(0.078f, 0.922f, normalized);
            if (healthBarFill != null)
            {
                healthBarFill.color = fillColor;
                healthBarFill.rectTransform.anchorMax = new Vector2(fillRight, 0.68f);
            }
            if (healthBarGlow != null)
            {
                Color glow = Color.Lerp(fillColor, AccardND.Battlefield.MmoUiTheme.Arcane, 0.38f);
                healthBarGlow.color = new Color(glow.r, glow.g, glow.b, 0.34f + normalized * 0.32f);
                healthBarGlow.rectTransform.anchorMax = new Vector2(glowRight, 0.86f);
            }
            if (healthBarTopShine != null)
                healthBarTopShine.rectTransform.anchorMax = new Vector2(Mathf.Lerp(0.095f, 0.905f, normalized), 0.68f);
            if (healthBarText != null)
                healthBarText.text = $"HP {clampedCurrent}/{maximum}";
        }

        public void SetHealthBarVisible(bool visible)
        {
            if (healthBarRoot != null)
                healthBarRoot.SetActive(visible);
        }

        public void PlayComposableGolemHitEffect(ComposableGolemForm form)
        {
            string resourcePath = form switch
            {
                ComposableGolemForm.Crystal => "Minibosses/ComposableGolem/Previews/golem_hit_crystal_shatter_preview",
                ComposableGolemForm.Glass => "Minibosses/ComposableGolem/Previews/golem_hit_glass_splinters_preview",
                ComposableGolemForm.Iron => "Minibosses/ComposableGolem/Previews/golem_hit_iron_sparks_preview",
                _ => null
            };

            Sprite sprite = string.IsNullOrWhiteSpace(resourcePath) ? null : LoadPreviewSprite(resourcePath);
            if (sprite == null)
                return;

            StartCoroutine(PlayComposableGolemHitEffectRoutine(sprite, form));
        }

        public IEnumerator PlayComposableGolemAttackEffect(PrototypeCardView defender, ComposableGolemForm form, bool hit)
        {
            if (defender == null)
                yield break;

            StartCoroutine(PlayComposableGolemAttackAnticipation(form));
            StartCoroutine(PlayComposableGolemLocalVfx("Golem Attack Charge", form, strong: true, shield: false));
            yield return PlayComposableGolemTrail(defender, form, hit);
            StartCoroutine(defender.PlayComposableGolemImpactShake(hit ? 14f : 7f));
            yield return defender.PlayComposableGolemLocalVfx("Golem Attack Impact", form, hit, shield: false);
        }

        public void PlayAttachmentEquipEffect()
        {
            StartCoroutine(PlayAttachmentEquipEffectRoutine());
        }

        public IEnumerator PlayComposableGolemDefenseEffect(ComposableGolemForm form, bool resisted)
        {
            yield return PlayComposableGolemLocalVfx("Golem Defense VFX", form, resisted, shield: true);
        }

        public IEnumerator PlayPalatirShieldBreakEffect(ClassFamily family)
        {
            yield return PlayPalatirLocalVfx("Palatir Shield Break", family, burst: true);
        }

        public IEnumerator PlayPalatirShieldBlockEffect(ClassFamily family)
        {
            yield return PlayPalatirLocalVfx("Palatir Shield Block", family, burst: false);
        }

        public IEnumerator PlayPalatirCosmicAttackEffect(PrototypeCardView defender, bool hit)
        {
            if (defender == null)
                yield break;

            StartCoroutine(PlayPalatirLocalVfx("Palatir Cosmic Charge", ClassFamily.Magic, burst: false));
            yield return PlayPalatirCosmicTrail(defender, hit);
            yield return defender.PlayPalatirLocalVfx("Palatir Cosmic Impact", ClassFamily.Magic, burst: hit);
        }

        public void SetLifeIcons(int current, int maximum)
        {
            SetHealthBarVisible(false);

            if (maximum <= 0 || current <= 0)
            {
                SetLifeIconsVisible(false);
                return;
            }

            if (lifeIconsRoot == null)
                CreateLifeIcons();

            int clampedCurrent = Mathf.Clamp(current, 0, maximum);
            lifeIconsRoot.SetActive(clampedCurrent > 0);
            lifeIconsRoot.transform.SetAsLastSibling();

            if (leftLifeIcon != null)
            {
                leftLifeIcon.sprite = heartIcon;
                leftLifeIcon.color = heartIcon != null ? Color.white : new Color(0.95f, 0.05f, 0.06f, 1f);
                leftLifeIcon.gameObject.SetActive(clampedCurrent > 0);
            }

            if (rightLifeIcon != null)
            {
                bool full = clampedCurrent >= Mathf.Min(maximum, 2);
                Sprite icon = full ? heartIcon : blackHeartIcon;
                rightLifeIcon.sprite = icon;
                rightLifeIcon.color = icon != null
                    ? Color.white
                    : full
                        ? new Color(0.95f, 0.05f, 0.06f, 1f)
                        : new Color(0.03f, 0.025f, 0.025f, 1f);
                rightLifeIcon.gameObject.SetActive(clampedCurrent > 0);
            }
        }

        public void SetLifeIconsVisible(bool visible)
        {
            if (lifeIconsRoot != null)
                lifeIconsRoot.SetActive(visible);
        }

        public void SetStatuses(params StatusToken[] statuses)
        {
            ClearStatusIcons();
            if (statusLabel != null)
                statusLabel.gameObject.SetActive(false);
            if (ContainsDefeatStatus(statuses))
                SetComposableGolemOrbitVisible(false);
            SetPetrifiedOverlayVisible(ContainsPetrifiedStatus(statuses));
            if (statusIconRoot == null)
                return;

            bool visible = statuses != null && statuses.Length > 0;
            statusIconRoot.gameObject.SetActive(visible);
            if (!visible)
                return;

            for (int index = 0; index < statuses.Length; index++)
                CreateStatusIcon(statuses[index], index);
        }

        public IEnumerator PlayPetrifiedOverlayCrumble()
        {
            EnsurePetrifiedOverlay();
            if (petrifiedOverlayImage == null)
                yield break;

            petrifiedOverlayImage.gameObject.SetActive(true);
            RectTransform overlayRect = petrifiedOverlayImage.rectTransform;
            Vector3 originalScale = overlayRect.localScale;
            Quaternion originalRotation = overlayRect.localRotation;
            Color baseColor = new Color(0.74f, 0.78f, 0.78f, 0.72f);
            Rect cardRect = RectTransform.rect;
            int columns = 8;
            int rows = 10;
            List<(GameObject obj, RectTransform rect, Image image, Vector2 start, Vector2 velocity, float spin, float delay, float size)> fragments = new();
            Sprite fragmentSprite = GetPetrifiedFragmentSprite();
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    if (UnityEngine.Random.value < 0.16f)
                        continue;

                    Image fragment = CreateImage("Petrified Falling Fragment", transform, baseColor);
                    fragment.sprite = fragmentSprite;
                    fragment.preserveAspect = true;
                    fragment.raycastTarget = false;
                    RectTransform fragmentRect = fragment.rectTransform;
                    fragmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                    fragmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                    fragmentRect.pivot = new Vector2(0.5f, 0.5f);
                    float cellWidth = cardRect.width / columns;
                    float cellHeight = cardRect.height / rows;
                    float size = Mathf.Clamp(Mathf.Min(cellWidth, cellHeight) * UnityEngine.Random.Range(0.48f, 0.92f), 8f, 42f);
                    fragmentRect.sizeDelta = Vector2.one * size;
                    Vector2 start = new Vector2(
                        cardRect.xMin + (x + 0.5f) * cellWidth + UnityEngine.Random.Range(-cellWidth * 0.28f, cellWidth * 0.28f),
                        cardRect.yMin + (y + 0.5f) * cellHeight + UnityEngine.Random.Range(-cellHeight * 0.25f, cellHeight * 0.25f));
                    Vector2 velocity = new Vector2(
                        UnityEngine.Random.Range(-58f, 58f),
                        UnityEngine.Random.Range(-95f, -34f) - y * 8f);
                    fragments.Add((
                        fragment.gameObject,
                        fragmentRect,
                        fragment,
                        start,
                        velocity,
                        UnityEngine.Random.Range(-520f, 520f),
                        Mathf.Lerp(0.02f, 0.24f, 1f - y / (float)(rows - 1)) + UnityEngine.Random.Range(0f, 0.09f),
                        size));
                }
            }

            petrifiedOverlayImage.transform.SetAsLastSibling();
            foreach (var fragment in fragments)
                fragment.rect.SetAsLastSibling();
            if (statusIconRoot != null)
                statusIconRoot.SetAsLastSibling();

            float duration = 0.92f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float crack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.22f));
                float dissolve = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.16f) / 0.58f));
                float shake = (1f - progress) * 5.5f;
                overlayRect.localScale = originalScale * Mathf.Lerp(1.01f, 1.09f, crack);
                overlayRect.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(progress * Mathf.PI * 18f) * shake);
                petrifiedOverlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - dissolve) * 0.92f);

                for (int i = 0; i < fragments.Count; i++)
                {
                    var fragment = fragments[i];
                    float local = Mathf.Clamp01((progress - fragment.delay) / (1f - fragment.delay));
                    float fall = local * local;
                    Vector2 position = fragment.start
                        + fragment.velocity * local
                        + new Vector2(0f, -420f * fall);
                    fragment.rect.anchoredPosition = position;
                    fragment.rect.localRotation = Quaternion.Euler(0f, 0f, fragment.spin * local);
                    float scalePulse = Mathf.Lerp(0.55f, 1.12f, Mathf.Sin(local * Mathf.PI));
                    fragment.rect.localScale = Vector3.one * scalePulse;
                    float alpha = Mathf.Clamp01(Mathf.Min(local * 7f, (1f - local) * 2.4f));
                    fragment.image.color = new Color(
                        Mathf.Lerp(0.48f, 0.82f, UnityEngine.Random.value * 0.08f + 0.35f),
                        Mathf.Lerp(0.5f, 0.84f, 0.38f),
                        Mathf.Lerp(0.5f, 0.84f, 0.38f),
                        alpha * 0.88f);
                }

                yield return null;
            }

            overlayRect.localScale = originalScale;
            overlayRect.localRotation = originalRotation;
            petrifiedOverlayImage.color = baseColor;
            petrifiedOverlayImage.gameObject.SetActive(false);
            for (int i = 0; i < fragments.Count; i++)
            {
                if (fragments[i].obj != null)
                    Destroy(fragments[i].obj);
            }
        }

        private static bool ContainsPetrifiedStatus(StatusToken[] statuses)
        {
            if (statuses == null)
                return false;

            for (int i = 0; i < statuses.Length; i++)
            {
                string label = statuses[i].Label;
                if (!string.IsNullOrWhiteSpace(label)
                    && label.Trim().Equals("PIETRA", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetPetrifiedOverlayVisible(bool visible)
        {
            if (visible)
                EnsurePetrifiedOverlay();
            if (petrifiedOverlayImage == null)
                return;

            petrifiedOverlayImage.gameObject.SetActive(visible);
            petrifiedOverlayImage.color = new Color(0.74f, 0.78f, 0.78f, 0.72f);
            petrifiedOverlayImage.transform.SetAsLastSibling();
            if (statusIconRoot != null)
                statusIconRoot.SetAsLastSibling();
        }

        private void EnsurePetrifiedOverlay()
        {
            if (petrifiedOverlayImage != null)
                return;

            petrifiedOverlayImage = CreateImage("Petrified Overlay", transform, new Color(0.74f, 0.78f, 0.78f, 0.72f));
            petrifiedOverlayImage.sprite = GetPetrifiedOverlaySprite();
            petrifiedOverlayImage.preserveAspect = false;
            petrifiedOverlayImage.raycastTarget = false;
            Stretch(petrifiedOverlayImage.rectTransform);
            petrifiedOverlayImage.gameObject.SetActive(false);
        }

        private static bool ContainsDefeatStatus(StatusToken[] statuses)
        {
            if (statuses == null)
                return false;

            for (int i = 0; i < statuses.Length; i++)
            {
                string label = statuses[i].Label;
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                string normalized = label.Trim().ToUpperInvariant();
                if (normalized.Contains("MORTE") || normalized.Contains("KO") || normalized.Contains("ELIMINAT"))
                    return true;
            }

            return false;
        }

        public static Sprite GetStatusIconSprite(string status)
        {
            return ResolveStatusIcon(status);
        }

        public void PlayDiceRoll(
            DiceSpriteCatalog catalog,
            int sides,
            int result,
            string caption,
            float rollDuration,
            float resultHold)
        {
            if (diceCoroutine != null)
                CancelActiveDiceRoll();

            diceCoroutine = StartCoroutine(PlayDiceRollRoutine(
                catalog,
                sides,
                sides,
                result,
                0,
                false,
                result,
                VigorSelectionMode.Single,
                0,
                0,
                caption,
                rollDuration,
                resultHold,
                use3DDice: false));
        }

        public void PlayVigorRoll(
            DiceSpriteCatalog catalog,
            int sides,
            VigorRollResult roll,
            string caption,
            float rollDuration,
            float resultHold)
        {
            if (diceCoroutine != null)
                CancelActiveDiceRoll();

            diceCoroutine = StartCoroutine(PlayDiceRollRoutine(
                catalog,
                ResolveVisualDieSides(catalog, sides),
                roll.DieSides,
                roll.FirstRoll,
                roll.SecondRoll,
                roll.HasSecondRoll,
                roll.SelectedRoll,
                roll.SelectionMode,
                roll.FirstRollBeforeReroll,
                roll.SecondRollBeforeReroll,
                caption,
                rollDuration,
                resultHold,
                use3DDice: true));
        }

        public static float VigorRollPresentationDuration(VigorRollResult roll, float rollDuration, float resultHold)
        {
            float total = Mathf.Max(rollDuration, AnimatedDiceMinimumRollDuration);
            bool hasReroll = roll.FirstRollBeforeReroll > 0
                || (roll.HasSecondRoll && roll.SecondRollBeforeReroll > 0);
            if (hasReroll)
            {
                float rerollDuration = Mathf.Max(0.45f, rollDuration * 0.66f);
                total += 0.32f + Mathf.Max(rerollDuration, AnimatedDiceMinimumRollDuration * 0.72f);
            }

            return total + Mathf.Max(resultHold, AnimatedDiceMinimumResultHold);
        }

        private void CancelActiveDiceRoll()
        {
            if (diceCoroutine != null)
            {
                StopCoroutine(diceCoroutine);
                diceCoroutine = null;
            }
            if (firstDice3D != null)
                firstDice3D.Hide();
            if (secondDice3D != null)
                secondDice3D.Hide();
            EndScreenDiceRollLayout();
            if (diceRoot != null)
                diceRoot.SetActive(false);
        }

        public IEnumerator PlayAttackAnimation()
        {
            float scale = configuration.Animation.AttackScale;
            yield return ScaleOverTime(
                rectTransform.localScale,
                Vector3.one * scale,
                configuration.Animation.AttackExpandDuration);
            yield return ScaleOverTime(
                Vector3.one * scale,
                Vector3.one,
                configuration.Animation.AttackReturnDuration);
        }

        public IEnumerator MoveToDuelPoint(Vector3 worldPosition, float duration, float scale)
        {
            if (motionCoroutine != null)
            {
                StopCoroutine(motionCoroutine);
                motionCoroutine = null;
            }

            LayoutElement layout = GetComponent<LayoutElement>();
            if (!duelDetached)
            {
                duelHomeAnchoredPosition = rectTransform.anchoredPosition;
                duelHomeScale = rectTransform.localScale;
                duelHomeRotation = rectTransform.localRotation;
                duelDetached = true;
            }

            layout.ignoreLayout = true;
            transform.SetAsLastSibling();
            Vector3 startPosition = rectTransform.position;
            Vector3 startScale = rectTransform.localScale;
            Quaternion startRotation = rectTransform.localRotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                rectTransform.position = Vector3.LerpUnclamped(startPosition, worldPosition, progress);
                rectTransform.localScale = Vector3.LerpUnclamped(startScale, Vector3.one * scale, progress);
                rectTransform.localRotation = Quaternion.SlerpUnclamped(startRotation, Quaternion.identity, progress);
                yield return null;
            }

            rectTransform.position = worldPosition;
            rectTransform.localScale = Vector3.one * scale;
            rectTransform.localRotation = Quaternion.identity;
        }

        public IEnumerator ReturnFromDuelPoint(float duration)
        {
            if (!duelDetached)
                yield break;

            Vector2 startPosition = rectTransform.anchoredPosition;
            Vector3 startScale = rectTransform.localScale;
            Quaternion startRotation = rectTransform.localRotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, duelHomeAnchoredPosition, progress);
                rectTransform.localScale = Vector3.LerpUnclamped(startScale, duelHomeScale, progress);
                rectTransform.localRotation = Quaternion.SlerpUnclamped(startRotation, duelHomeRotation, progress);
                yield return null;
            }

            rectTransform.anchoredPosition = duelHomeAnchoredPosition;
            rectTransform.localScale = duelHomeScale;
            rectTransform.localRotation = duelHomeRotation;
            LayoutElement layout = GetComponent<LayoutElement>();
            layout.ignoreLayout = false;
            duelDetached = false;
        }

        private static readonly Dictionary<HeroClass, Sprite> cachedDefeatCrackSprites = new();
        private static Sprite cachedDefaultDefeatCrackSprite;
        private static Sprite cachedProceduralCrackSprite;
        private static Sprite cachedAshShardSprite;
        private static Sprite cachedDeathBurstRingSprite;
        private static Sprite cachedDeathBurstFlashSprite;
        private static readonly bool DeathCrackDebugLogs = true;

        private static Sprite GetDefeatCrackSprite(HeroClass? killerHeroClass = null)
        {
            return GetDefeatCrackSprite(killerHeroClass, out _, out _);
        }

        private static Sprite GetDefeatCrackSprite(
            HeroClass? killerHeroClass,
            out string resourcePath,
            out bool usedFallback)
        {
            if (killerHeroClass.HasValue)
            {
                HeroClass heroClass = killerHeroClass.Value;
                resourcePath = DefeatCrackResourceFor(heroClass);
                usedFallback = false;
                if (cachedDefeatCrackSprites.TryGetValue(heroClass, out Sprite cachedSprite) && cachedSprite != null)
                    return cachedSprite;

                Sprite classSprite = Resources.Load<Sprite>(resourcePath);
                if (classSprite != null)
                {
                    cachedDefeatCrackSprites[heroClass] = classSprite;
                    return classSprite;
                }
            }

            resourcePath = "UI/death_runic_icon";
            usedFallback = true;
            return LoadCrackSprite(ref cachedDefaultDefeatCrackSprite, "UI/death_runic_icon", null);
        }

        private static void LogDeathCrack(
            string phase,
            HeroClass? killerHeroClass,
            string resourcePath,
            Sprite sprite,
            Image image = null)
        {
            if (!DeathCrackDebugLogs)
                return;

            string spriteInfo = sprite == null
                ? "sprite=NULL"
                : $"sprite={sprite.name}, texture={sprite.texture?.name}, size={sprite.textureRect.width}x{sprite.textureRect.height}";
            string imageInfo = image == null
                ? string.Empty
                : $", imageActive={image.gameObject.activeInHierarchy}, alpha={image.color.a:0.00}, sibling={image.transform.GetSiblingIndex()}/{image.transform.parent?.childCount}";
            Debug.Log($"[DeathCrack] {phase}: killer={killerHeroClass?.ToString() ?? "NULL"}, resource={resourcePath}, {spriteInfo}{imageInfo}");
        }

        private static Sprite LoadCrackSprite(ref Sprite cachedSprite, string preferredResource, string fallbackResource)
        {
            if (cachedSprite != null)
                return cachedSprite;

            cachedSprite = Resources.Load<Sprite>(preferredResource);
            if (cachedSprite != null)
                return cachedSprite;

            if (!string.IsNullOrEmpty(fallbackResource))
            {
                cachedSprite = Resources.Load<Sprite>(fallbackResource);
                if (cachedSprite != null)
                    return cachedSprite;
            }

            cachedSprite = GetProceduralCrackSprite();
            return cachedSprite;
        }

        private static string DefeatCrackResourceFor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => "UI/assassin_crack",
                HeroClass.Warrior => "UI/warrior_crack",
                HeroClass.Mage => "UI/mage_crack",
                HeroClass.Paladin => "UI/paladin_crack",
                HeroClass.Rogue => "UI/rogue_crack",
                HeroClass.Hunter => "UI/hunter_crack",
                HeroClass.Barbarian => "UI/barbarian_crack",
                HeroClass.Necromancer => "UI/necromancer_crack",
                HeroClass.Priest => "UI/priest_crack",
                _ => "UI/death_runic_icon"
            };
        }

        private static Sprite GetProceduralCrackSprite()
        {
            if (cachedProceduralCrackSprite != null)
                return cachedProceduralCrackSprite;

            int width = 128;
            int height = 128;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Procedural Crack Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[width * height];
            Color32 clearColor = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clearColor;

            System.Random rand = new System.Random();
            int startX = width / 2;
            int startY = height / 2;
            int numMainBranches = rand.Next(5, 8);
            for (int b = 0; b < numMainBranches; b++)
            {
                float baseAngle = (360f / numMainBranches) * b + rand.Next(-15, 15);
                DrawProceduralCrackLine(pixels, width, height, startX, startY, baseAngle, 45f, rand);
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            cachedProceduralCrackSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            cachedProceduralCrackSprite.name = "Procedural Crack Sprite";
            cachedProceduralCrackSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedProceduralCrackSprite;
        }

        private static void DrawProceduralCrackLine(Color32[] pixels, int width, int height, int x, int y, float angle, float length, System.Random rand)
        {
            if (length < 4f) return;

            float rad = angle * Mathf.Deg2Rad;
            int nextX = Mathf.Clamp((int)(x + Mathf.Cos(rad) * length), 0, width - 1);
            int nextY = Mathf.Clamp((int)(y + Mathf.Sin(rad) * length), 0, height - 1);

            Color32 crackColor = new Color32(255, 255, 255, 255);
            DrawBresenhamLine(pixels, width, height, x, y, nextX, nextY, crackColor);

            if (rand.NextDouble() < 0.65)
            {
                float branchAngle = angle + rand.Next(-35, 35);
                DrawProceduralCrackLine(pixels, width, height, nextX, nextY, branchAngle, length * 0.75f, rand);
            }
            if (rand.NextDouble() < 0.35)
            {
                float branchAngle = angle + rand.Next(-65, 65);
                DrawProceduralCrackLine(pixels, width, height, nextX, nextY, branchAngle, length * 0.55f, rand);
            }
        }

        private static void DrawBresenhamLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;

            while (true)
            {
                for (int tx = 0; tx <= 1; tx++)
                {
                    for (int ty = 0; ty <= 1; ty++)
                    {
                        int px = x0 + tx;
                        int py = y0 + ty;
                        if (px >= 0 && px < width && py >= 0 && py < height)
                        {
                            pixels[py * width + px] = color;
                        }
                    }
                }

                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private static Sprite GetAshShardSprite()
        {
            if (cachedAshShardSprite != null)
                return cachedAshShardSprite;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Procedural Ash Shard Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = clear;

            Vector2 a = new Vector2(5f, 26f);
            Vector2 b = new Vector2(17f, 3f);
            Vector2 c = new Vector2(28f, 21f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    if (!PointInTriangle(p, a, b, c))
                        continue;

                    float edge = Mathf.Min(
                        DistanceToSegment(p, a, b),
                        Mathf.Min(DistanceToSegment(p, b, c), DistanceToSegment(p, c, a)));
                    byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(95f, 230f, Mathf.Clamp01(edge / 4f))), 0, 255);
                    byte value = (byte)Mathf.Clamp(44 + Mathf.RoundToInt((x * 13 + y * 7) % 34), 0, 255);
                    pixels[y * size + x] = new Color32(value, value, value, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            cachedAshShardSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            cachedAshShardSprite.name = "Procedural Ash Shard Sprite";
            cachedAshShardSprite.hideFlags = HideFlags.HideAndDontSave;
            return cachedAshShardSprite;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float denominator = Vector2.Dot(ab, ab);
            if (denominator <= 0.0001f)
                return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / denominator);
            return Vector2.Distance(p, a + ab * t);
        }

        private IEnumerator AnimateShatterParticles(
            RectTransform sourceRect,
            Color shardColor,
            Color burstColor,
            bool isTile = false)
        {
            int shardCount = isTile ? 14 : 34;
            List<RectTransform> shards = new List<RectTransform>();
            List<Vector2> velocities = new List<Vector2>();
            List<float> rotSpeeds = new List<float>();

            Vector2 center = sourceRect.anchoredPosition;
            Transform parent = sourceRect.parent;

            Sprite shardSprite = GetAshShardSprite();
            Image burstRing = CreateBurstImage(parent, "Death Crack Burst Ring", burstColor);
            Image burstFlash = CreateBurstImage(parent, "Death Crack Burst Flash", Color.Lerp(burstColor, Color.white, 0.38f));
            RectTransform ringRect = burstRing.rectTransform;
            RectTransform flashRect = burstFlash.rectTransform;
            ringRect.anchoredPosition = center;
            flashRect.anchoredPosition = center;

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shardObj = new GameObject("Shard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                shardObj.transform.SetParent(parent, false);
                Image shardImg = shardObj.GetComponent<Image>();
                shardImg.sprite = shardSprite;
                shardImg.color = shardColor;
                shardImg.raycastTarget = false;
                shardImg.preserveAspect = true;
                shardObj.transform.SetAsLastSibling();

                RectTransform sRect = shardObj.GetComponent<RectTransform>();
                float sizeMin = isTile ? 10f : 18f;
                float sizeMax = isTile ? 26f : 48f;
                sRect.sizeDelta = new Vector2(UnityEngine.Random.Range(sizeMin, sizeMax), UnityEngine.Random.Range(sizeMin, sizeMax));
                
                float offsetRange = isTile ? 30f : 78f;
                sRect.anchoredPosition = center + new Vector2(UnityEngine.Random.Range(-offsetRange, offsetRange), UnityEngine.Random.Range(-offsetRange, offsetRange));
                sRect.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));

                shards.Add(sRect);

                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = isTile ? UnityEngine.Random.Range(220f, 520f) : UnityEngine.Random.Range(420f, 980f);
                velocities.Add(new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + (isTile ? 120f : 240f)));
                rotSpeeds.Add(UnityEngine.Random.Range(-760f, 760f));
            }

            float elapsed = 0f;
            float duration = isTile ? 0.72f : 0.95f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float burstProgress = Mathf.Clamp01(t / 0.62f);
                float burstAlpha = Mathf.Sin(Mathf.Clamp01(1f - t) * Mathf.PI * 0.5f);
                float ringSize = Mathf.Lerp(isTile ? 90f : 180f, isTile ? 260f : 620f, Mathf.SmoothStep(0f, 1f, burstProgress));
                ringRect.sizeDelta = new Vector2(ringSize, ringSize);
                flashRect.sizeDelta = new Vector2(ringSize * 0.62f, ringSize * 0.62f);
                burstRing.color = new Color(burstColor.r, burstColor.g, burstColor.b, 0.42f * burstAlpha);
                burstFlash.color = new Color(burstColor.r, burstColor.g, burstColor.b, 0.26f * Mathf.Clamp01(1f - t * 3.2f));
                burstRing.transform.SetAsLastSibling();
                burstFlash.transform.SetAsLastSibling();

                for (int i = 0; i < shards.Count; i++)
                {
                    if (shards[i] == null) continue;
                    Vector2 vel = velocities[i];
                    vel.y -= (isTile ? 620f : 1100f) * Time.unscaledDeltaTime;
                    velocities[i] = vel;

                    shards[i].anchoredPosition += vel * Time.unscaledDeltaTime;
                    shards[i].localRotation *= Quaternion.Euler(0, 0, rotSpeeds[i] * Time.unscaledDeltaTime);

                    Image img = shards[i].GetComponent<Image>();
                    if (img != null)
                    {
                        Color color = Color.Lerp(shardColor, burstColor, Mathf.Clamp01(1f - t * 0.8f));
                        img.color = new Color(color.r, color.g, color.b, 1f - t);
                    }
                    shards[i].SetAsLastSibling();
                }
                yield return null;
            }

            foreach (var shard in shards)
            {
                if (shard != null) Destroy(shard.gameObject);
            }
            if (burstRing != null) Destroy(burstRing.gameObject);
            if (burstFlash != null) Destroy(burstFlash.gameObject);
        }

        private static Image CreateBurstImage(Transform parent, string name, Color color)
        {
            Image image = CreateImage(name, parent, color);
            image.sprite = name.Contains("Flash", StringComparison.Ordinal)
                ? GetDeathBurstFlashSprite()
                : GetDeathBurstRingSprite();
            image.preserveAspect = true;
            image.material = null;
            image.transform.SetAsLastSibling();
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            return image;
        }

        private static Sprite GetDeathBurstRingSprite()
        {
            if (cachedDeathBurstRingSprite != null)
                return cachedDeathBurstRingSprite;

            cachedDeathBurstRingSprite = CreateRadialBurstSprite("Death Burst Ring Sprite", ring: true);
            return cachedDeathBurstRingSprite;
        }

        private static Sprite GetDeathBurstFlashSprite()
        {
            if (cachedDeathBurstFlashSprite != null)
                return cachedDeathBurstFlashSprite;

            cachedDeathBurstFlashSprite = CreateRadialBurstSprite("Death Burst Flash Sprite", ring: false);
            return cachedDeathBurstFlashSprite;
        }

        private static Sprite CreateRadialBurstSprite(string name, bool ring)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(distance - 0.58f) * 7.8f) * Mathf.Clamp01(1.04f - distance)
                        : Mathf.Pow(Mathf.Clamp01(1f - distance), 1.8f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public IEnumerator PlayDefeatAnimation(
            GameObject timelineTile = null,
            Action timelineTileHidden = null,
            HeroClass? killerHeroClass = null)
        {
            SetComposableGolemOrbitVisible(false);
            ClearDefeatCrackOverlay();
            SetBattleAura(false, Color.clear, string.Empty);
            if (targetHintAuraRoot != null)
                SetTargetHintAura(false, Color.clear);
            defeatedLabel.gameObject.SetActive(false);
            SetStatus("MORTE", new Color(0.95f, 0.12f, 0.12f));

            Color crackColor = Color.white;
            Color transparentCrackColor = new Color(crackColor.r, crackColor.g, crackColor.b, 0f);
            Color ashShardColor = Color.Lerp(new Color(0.16f, 0.15f, 0.14f, 0.95f), DeathBurstColor(killerHeroClass), 0.36f);
            ashShardColor.a = 0.95f;
            Color deathBurstColor = DeathBurstColor(killerHeroClass);
            Color dissolveEdgeColor = new Color(0.38f, 0.42f, 0.46f, 1f);

            // Instantiate custom burn dissolve material
            Shader dissolveShader = Shader.Find("Custom/UIDissolve");
            Material dissolveMat = null;
            if (dissolveShader != null)
            {
                dissolveMat = new Material(dissolveShader);
                    Texture2D noise = Resources.Load<Texture2D>("UI/dissolve_ash_noise");
                    if (noise != null)
                    {
                        dissolveMat.SetTexture("_DissolveTex", noise);
                    dissolveMat.SetColor("_EdgeColor", dissolveEdgeColor);
                    dissolveMat.SetFloat("_EdgeWidth", 0.045f);
                    dissolveMat.SetFloat("_Amount", 0f);
                }
            }

            GameObject cardCrackObj = new GameObject("CrackOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardCrackObj.transform.SetParent(transform, false);
            Image cardCrackImg = cardCrackObj.GetComponent<Image>();
            string cardCrackResource;
            bool cardCrackUsedFallback;
            cardCrackImg.sprite = GetDefeatCrackSprite(killerHeroClass, out cardCrackResource, out cardCrackUsedFallback);
            cardCrackImg.color = transparentCrackColor;
            cardCrackImg.material = null;
            cardCrackImg.raycastTarget = false;
            Stretch((RectTransform)cardCrackObj.transform);
            cardCrackObj.transform.SetAsLastSibling();
            LogDeathCrack(
                cardCrackUsedFallback ? "created-card-overlay-fallback" : "created-card-overlay",
                killerHeroClass,
                cardCrackResource,
                cardCrackImg.sprite,
                cardCrackImg);

            Coroutine necromancerSkullPurge = killerHeroClass == HeroClass.Necromancer
                ? StartCoroutine(PlayNecromancerDefeatSkullPurge())
                : null;
            Coroutine priestPurification = killerHeroClass == HeroClass.Priest
                ? StartCoroutine(PlayPriestDefeatPurificationCrosses())
                : null;

            GameObject tileCrackObj = null;
            Image tileCrackImg = null;
            if (timelineTile != null)
            {
                tileCrackObj = new GameObject("TimelineCrackOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                tileCrackObj.transform.SetParent(timelineTile.transform, false);
                tileCrackImg = tileCrackObj.GetComponent<Image>();
                tileCrackImg.sprite = GetDefeatCrackSprite(killerHeroClass, out string tileCrackResource, out bool tileCrackUsedFallback);
                tileCrackImg.color = transparentCrackColor;
                tileCrackImg.material = null;
                tileCrackImg.raycastTarget = false;
                Stretch((RectTransform)tileCrackObj.transform);
                tileCrackObj.transform.SetAsLastSibling();
                LogDeathCrack(
                    tileCrackUsedFallback ? "created-timeline-overlay-fallback" : "created-timeline-overlay",
                    killerHeroClass,
                    tileCrackResource,
                    tileCrackImg.sprite,
                    tileCrackImg);
            }

            // Apply dissolve material to images
            List<Image> cardImages = DefeatEffectImages();
            if (dissolveMat != null)
            {
                foreach (var img in cardImages)
                {
                    if (img == null || img.gameObject.name == "CrackOverlay") continue;
                    img.material = dissolveMat;
                }
            }

            Image[] tileImages = null;
            if (timelineTile != null && dissolveMat != null)
            {
                tileImages = timelineTile.GetComponentsInChildren<Image>(true);
                foreach (var img in tileImages)
                {
                    if (img.gameObject.name == "TimelineCrackOverlay") continue;
                    img.material = dissolveMat;
                }
            }

            float elapsed = 0f;
            float crackDuration = 0.45f;
            Vector3 cardOriginalScale = rectTransform.localScale;
            Vector3 tileOriginalScale = timelineTile != null ? timelineTile.transform.localScale : Vector3.one;

            while (elapsed < crackDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / crackDuration);

                float crackAlpha = Mathf.Lerp(0f, 1f, progress);
                cardCrackImg.color = new Color(crackColor.r, crackColor.g, crackColor.b, crackAlpha);
                cardCrackObj.transform.SetAsLastSibling();
                if (tileCrackImg != null)
                {
                    tileCrackImg.color = new Color(crackColor.r, crackColor.g, crackColor.b, crackAlpha);
                    tileCrackObj.transform.SetAsLastSibling();
                }

                float shakeForce = (1f - progress) * 12f;
                rectTransform.localScale = cardOriginalScale * Mathf.Lerp(1.0f, 1.05f, progress);
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-shakeForce, shakeForce) * 0.4f);

                if (timelineTile != null)
                {
                    timelineTile.transform.localScale = tileOriginalScale * Mathf.Lerp(1.0f, 1.08f, progress);
                    timelineTile.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-shakeForce, shakeForce) * 0.6f);
                }

                yield return null;
            }

            if (tileCrackObj != null) Destroy(tileCrackObj);
            if (timelineTile != null)
            {
                timelineTile.transform.localScale = tileOriginalScale;
                if (tileImages != null)
                {
                    foreach (var img in tileImages)
                    {
                        if (img != null)
                            img.material = null;
                    }
                }
                timelineTile.SetActive(false);
                timelineTileHidden?.Invoke();
            }

            // Animate beautiful dissolve erosion in parallel with burst shatter particles!
            float dissolveElapsed = 0f;
            float dissolveDuration = 0.75f;

            Coroutine cardShatter = StartCoroutine(AnimateShatterParticles(rectTransform, ashShardColor, deathBurstColor));

            while (dissolveElapsed < dissolveDuration)
            {
                dissolveElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(dissolveElapsed / dissolveDuration);
                if (dissolveMat != null)
                {
                    dissolveMat.SetFloat("_Amount", progress);
                }
                if (cardCrackImg != null)
                {
                    cardCrackImg.color = new Color(crackColor.r, crackColor.g, crackColor.b, Mathf.Lerp(1f, 0.86f, progress));
                    cardCrackObj.transform.SetAsLastSibling();
                }
                yield return null;
            }

            yield return cardShatter;
            if (necromancerSkullPurge != null)
                yield return necromancerSkullPurge;
            if (priestPurification != null)
                yield return priestPurification;

            rectTransform.localScale = initialScale * 0.93f;
            canvasGroup.alpha = 0.52f;
            foreach (var img in cardImages)
            {
                if (img != null)
                    img.material = null;
            }
            if (cardCrackObj != null)
            {
                cardCrackObj.name = "DefeatCrackOverlay";
                cardCrackImg.color = new Color(crackColor.r, crackColor.g, crackColor.b, 0.86f);
                cardCrackObj.transform.SetAsLastSibling();
                defeatCrackOverlay = cardCrackObj;
                LogDeathCrack("final-overlay-reused", killerHeroClass, cardCrackResource, cardCrackImg.sprite, cardCrackImg);
            }
            else
            {
                defeatCrackOverlay = CreateCrackOverlay("DefeatCrackOverlay", transform, crackColor, 0.86f, killerHeroClass);
            }

            if (dissolveMat != null) Destroy(dissolveMat);

            if (duelDetached)
            {
                rectTransform.anchoredPosition = duelHomeAnchoredPosition;
                rectTransform.localRotation = duelHomeRotation;
                LayoutElement layout = GetComponent<LayoutElement>();
                layout.ignoreLayout = false;
                duelDetached = false;
            }
        }

        private IEnumerator PlayNecromancerDefeatSkullPurge()
        {
            const int skullCount = 5;
            Sprite skullSprite = GetNecromancerDefeatSkullSprite();
            var skulls = new List<(Image image, RectTransform rect, float angle, float wavePhase, float distanceBias, float amplitude, float spin)>(skullCount);
            Rect cardRect = rectTransform.rect;
            float cardWidth = Mathf.Max(260f, cardRect.width);
            float cardHeight = Mathf.Max(390f, cardRect.height);
            float maxDistance = Mathf.Max(cardWidth, cardHeight) * 0.46f;

            for (int i = 0; i < skullCount; i++)
            {
                Image skull = CreateImage("Necromancer Disinfest Skull", transform, new Color(0.56f, 0.95f, 0.68f, 0f));
                skull.sprite = skullSprite;
                skull.preserveAspect = true;
                skull.raycastTarget = false;
                RectTransform skullRect = skull.rectTransform;
                SetAnchors(skullRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                skullRect.sizeDelta = new Vector2(48f, 48f);
                skull.transform.SetAsLastSibling();

                float angle = Mathf.PI * 2f * i / skullCount + UnityEngine.Random.Range(-0.12f, 0.12f);
                float wavePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float distanceBias = UnityEngine.Random.Range(0.84f, 1.18f);
                float amplitude = UnityEngine.Random.Range(8f, 22f);
                float spin = UnityEngine.Random.Range(-80f, 80f);
                skulls.Add((skull, skullRect, angle, wavePhase, distanceBias, amplitude, spin));
            }

            const float duration = 1.18f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = Mathf.Clamp01(Mathf.Min(progress * 6.5f, (1f - progress) * 2.35f));
                float distance = Mathf.Lerp(4f, maxDistance, eased);

                for (int i = 0; i < skulls.Count; i++)
                {
                    var skull = skulls[i];
                    if (skull.image == null)
                        continue;

                    Vector2 direction = new Vector2(Mathf.Cos(skull.angle), Mathf.Sin(skull.angle));
                    Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                float wave = Mathf.Sin(progress * Mathf.PI * 3.1f + skull.wavePhase) * skull.amplitude * Mathf.Sin(progress * Mathf.PI);
                Vector2 position = direction * distance * skull.distanceBias + perpendicular * wave;
                skull.rect.anchoredPosition = position;
                skull.rect.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.92f, Mathf.Sin(progress * Mathf.PI));
                skull.rect.localRotation = Quaternion.Euler(0f, 0f, skull.angle * Mathf.Rad2Deg - 90f + skull.spin * progress);
                skull.image.color = new Color(0.58f, 0.96f, 0.72f, Mathf.Clamp01(fade * 0.74f));
                    skull.image.transform.SetAsLastSibling();
                }

                yield return null;
            }

            foreach (var skull in skulls)
            {
                if (skull.image != null)
                    Destroy(skull.image.gameObject);
            }
        }

        private IEnumerator PlayPriestDefeatPurificationCrosses()
        {
            const int crossCount = 12;
            const int sparkCount = 16;
            Sprite crossSprite = GetPriestPurificationCrossSprite();
            Sprite sparkSprite = GetPriestPurificationSparkSprite();
            var crosses = new List<(Image image, RectTransform rect, float phase, float startX, float rise, float radiusBias, float spin)>(crossCount);
            var sparks = new List<(Image image, RectTransform rect, float phase, float radiusBias, float size)>(sparkCount);
            Rect cardRect = rectTransform.rect;
            float cardWidth = Mathf.Max(260f, cardRect.width);
            float cardHeight = Mathf.Max(390f, cardRect.height);

            Image halo = CreateImage("Priest Purification Halo", transform, new Color(1f, 1f, 1f, 0f));
            halo.sprite = GetPriestPurificationHaloSprite();
            halo.preserveAspect = true;
            halo.raycastTarget = false;
            RectTransform haloRect = halo.rectTransform;
            SetAnchors(haloRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            haloRect.anchoredPosition = new Vector2(0f, -cardHeight * 0.12f);

            for (int i = 0; i < crossCount; i++)
            {
                Image cross = CreateImage("Priest Ancestral Purification Cross", transform, new Color(1f, 1f, 1f, 0f));
                cross.sprite = crossSprite;
                cross.preserveAspect = true;
                cross.raycastTarget = false;
                RectTransform crossRect = cross.rectTransform;
                SetAnchors(crossRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                crossRect.sizeDelta = new Vector2(72f, 102f) * UnityEngine.Random.Range(0.92f, 1.34f);
                cross.transform.SetAsLastSibling();

                float normalized = (i / (float)(crossCount - 1)) - 0.5f;
                float phase = Mathf.PI * 2f * i / crossCount + UnityEngine.Random.Range(-0.16f, 0.16f);
                float startX = normalized * cardWidth * 0.48f + UnityEngine.Random.Range(-18f, 18f);
                float rise = UnityEngine.Random.Range(-10f, 34f);
                float radiusBias = UnityEngine.Random.Range(0.82f, 1.24f);
                float spin = UnityEngine.Random.Range(-52f, 52f);
                crosses.Add((cross, crossRect, phase, startX, rise, radiusBias, spin));
            }

            for (int i = 0; i < sparkCount; i++)
            {
                Image spark = CreateImage("Priest Purification Spark", transform, new Color(1f, 1f, 1f, 0f));
                spark.sprite = sparkSprite;
                spark.preserveAspect = true;
                spark.raycastTarget = false;
                RectTransform sparkRect = spark.rectTransform;
                SetAnchors(sparkRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                float size = UnityEngine.Random.Range(22f, 44f);
                sparkRect.sizeDelta = new Vector2(size, size);
                spark.transform.SetAsLastSibling();

                float phase = Mathf.PI * 2f * i / sparkCount + UnityEngine.Random.Range(-0.28f, 0.28f);
                sparks.Add((spark, sparkRect, phase, UnityEngine.Random.Range(0.64f, 1.2f), size));
            }

            const float duration = 1.72f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float fade = Mathf.Clamp01(Mathf.Min(progress * 7.5f, (1f - progress) * 2.2f));
                float centerY = Mathf.Lerp(-cardHeight * 0.62f, cardHeight * 0.04f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.62f)));
                float radius = Mathf.Lerp(18f, cardWidth * 0.86f, eased);
                float haloSize = Mathf.Lerp(cardWidth * 0.34f, cardWidth * 1.48f, eased);

                haloRect.sizeDelta = new Vector2(haloSize, haloSize);
                haloRect.localRotation = Quaternion.Euler(0f, 0f, progress * 84f);
                halo.color = new Color(1f, 0.98f, 0.84f, Mathf.Clamp01(fade * 0.34f));
                halo.transform.SetAsLastSibling();

                for (int i = 0; i < crosses.Count; i++)
                {
                    var cross = crosses[i];
                    if (cross.image == null)
                        continue;

                    float angle = cross.phase + progress * Mathf.PI * 1.65f;
                    Vector2 orbit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle * 1.18f) * 0.72f) * radius * cross.radiusBias;
                    Vector2 bottomRise = new Vector2(Mathf.Lerp(cross.startX, 0f, eased * 0.72f), centerY + cross.rise);
                    float pulse = 0.86f + Mathf.Sin(progress * Mathf.PI * 2.7f + cross.phase) * 0.16f;
                    cross.rect.anchoredPosition = bottomRise + orbit;
                    cross.rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.72f, Mathf.Sin(progress * Mathf.PI)) * pulse;
                    float uprightWobble = Mathf.Sin(progress * Mathf.PI * 2.2f + cross.phase) * 7f + cross.spin * 0.08f;
                    cross.rect.localRotation = Quaternion.Euler(0f, 0f, uprightWobble);
                    cross.image.color = Color.Lerp(
                        new Color(1f, 1f, 1f, fade),
                        new Color(1f, 0.94f, 0.68f, fade * 0.92f),
                        Mathf.Clamp01(progress * 0.65f));
                    cross.image.transform.SetAsLastSibling();
                }

                for (int i = 0; i < sparks.Count; i++)
                {
                    var spark = sparks[i];
                    if (spark.image == null)
                        continue;

                    float angle = spark.phase - progress * Mathf.PI * 4.4f;
                    float sparkRadius = Mathf.Lerp(10f, cardWidth * 0.96f, eased) * spark.radiusBias;
                    float lift = Mathf.Lerp(-cardHeight * 0.48f, cardHeight * 0.18f, Mathf.Clamp01(progress * 1.18f));
                    spark.rect.anchoredPosition = new Vector2(Mathf.Cos(angle) * sparkRadius, lift + Mathf.Sin(angle * 1.42f) * sparkRadius * 0.42f);
                    spark.rect.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.08f, Mathf.Sin(progress * Mathf.PI));
                    spark.rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + progress * 220f);
                    spark.image.color = new Color(1f, 0.98f, 0.78f, Mathf.Clamp01(fade * 0.72f));
                    spark.image.transform.SetAsLastSibling();
                }

                yield return null;
            }

            foreach (var cross in crosses)
            {
                if (cross.image != null)
                    Destroy(cross.image.gameObject);
            }
            foreach (var spark in sparks)
            {
                if (spark.image != null)
                    Destroy(spark.image.gameObject);
            }
            if (halo != null)
                Destroy(halo.gameObject);
        }

        public void ResetState()
        {
            ClearDefeatCrackOverlay();
            // Reset material back to normal in case the card is revived/recycled
            List<Image> cardImages = DefeatEffectImages();
            foreach (var img in cardImages)
            {
                img.material = null;
            }

            if (diceCoroutine != null)
            {
                StopCoroutine(diceCoroutine);
                diceCoroutine = null;
            }
            if (motionCoroutine != null)
            {
                StopCoroutine(motionCoroutine);
                motionCoroutine = null;
            }
            if (pendingDragEndRoutine != null)
            {
                StopCoroutine(pendingDragEndRoutine);
                pendingDragEndRoutine = null;
            }
            dragging = false;

            diceRoot.SetActive(false);
            canvasGroup.alpha = 1f;
            if (duelDetached)
            {
                rectTransform.anchoredPosition = duelHomeAnchoredPosition;
                rectTransform.localScale = duelHomeScale;
                rectTransform.localRotation = duelHomeRotation;
            }
            else
            {
                rectTransform.localScale = initialScale;
                rectTransform.localRotation = Quaternion.identity;
            }
            LayoutElement resetLayout = GetComponent<LayoutElement>();
            if (resetLayout != null)
                resetLayout.ignoreLayout = false;
            duelDetached = false;
            selectedVisual = false;
            selectionOutline.enabled = false;
            SetSelectedAura(false);
            SetTurnAura(false, true);
            if (targetHintAuraRoot != null)
                SetTargetHintAura(false, Color.clear);
            targetHintLabel.gameObject.SetActive(false);
            targetHintBackground.gameObject.SetActive(false);
            SetStatus(null, Color.white);
            defeatedLabel.gameObject.SetActive(false);
            ClearActionOverlay();
        }

        private void CreateHealthBar(Font font)
        {
            if (healthBarRoot != null)
                return;

            healthBarRoot = new GameObject(
                "Health Bar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            healthBarRoot.transform.SetParent(transform, false);
            RectTransform rootRect = (RectTransform)healthBarRoot.transform;
            SetAnchors(rootRect, new Vector2(0.035f, 1.015f), new Vector2(0.965f, 1.215f));

            Image background = healthBarRoot.GetComponent<Image>();
            background.sprite = GetBossHealthFrameSprite();
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = false;

            Image trough = CreateImage("Health Trough", healthBarRoot.transform, new Color(0.012f, 0.019f, 0.032f, 0.96f));
            trough.sprite = AccardND.Battlefield.MmoUiTheme.GetSoftPanelSprite();
            trough.type = Image.Type.Sliced;
            SetAnchors(trough.rectTransform, new Vector2(0.075f, 0.24f), new Vector2(0.925f, 0.75f));

            healthBarFill = CreateImage("Health Fill", healthBarRoot.transform, new Color(0.78f, 0.06f, 0.06f, 0.98f));
            healthBarFill.sprite = GetBossHealthFillSprite();
            healthBarFill.type = Image.Type.Sliced;
            SetAnchors(healthBarFill.rectTransform, new Vector2(0.085f, 0.31f), new Vector2(0.915f, 0.68f));

            healthBarGlow = CreateImage("Health Arcane Glow", healthBarRoot.transform, new Color(0.15f, 0.82f, 0.95f, 0.44f));
            healthBarGlow.sprite = GetBossHealthGlowSprite();
            healthBarGlow.type = Image.Type.Sliced;
            SetAnchors(healthBarGlow.rectTransform, new Vector2(0.078f, 0.18f), new Vector2(0.922f, 0.86f));

            healthBarTopShine = CreateImage("Health Top Shine", healthBarRoot.transform, new Color(1f, 0.92f, 0.72f, 0.28f));
            healthBarTopShine.sprite = GetBossHealthGlowSprite();
            healthBarTopShine.type = Image.Type.Sliced;
            SetAnchors(healthBarTopShine.rectTransform, new Vector2(0.095f, 0.54f), new Vector2(0.905f, 0.68f));

            AccardND.Battlefield.MmoUiTheme.AddPanelGem(rootRect, "Health Left Gem", new Vector2(0.055f, 0.5f), new Vector2(28f, 28f), new Color(0.28f, 0.95f, 1f, 0.95f));
            AccardND.Battlefield.MmoUiTheme.AddPanelGem(rootRect, "Health Right Gem", new Vector2(0.945f, 0.5f), new Vector2(28f, 28f), new Color(0.28f, 0.95f, 1f, 0.95f));

            healthBarText = CreateText("Health Text", healthBarRoot.transform, font, 17, FontStyle.Bold, TextAnchor.MiddleCenter);
            AccardND.Battlefield.MmoUiTheme.StyleAsTitle(healthBarText);
            healthBarText.color = new Color(1f, 0.93f, 0.76f, 1f);
            Outline textOutline = healthBarText.gameObject.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0.006f, 0.014f, 0.95f);
            textOutline.effectDistance = new Vector2(1.7f, -1.7f);
            SetAnchors(healthBarText.rectTransform, new Vector2(0.15f, 0.18f), new Vector2(0.85f, 0.86f));

            healthBarRoot.transform.SetAsLastSibling();
            healthBarRoot.SetActive(false);
        }

        private static Sprite GetBossHealthFrameSprite()
        {
            if (bossHealthFrameSprite != null)
                return bossHealthFrameSprite;

            bossHealthFrameSprite = BakeBossHealthSprite("Boss Health Frame", 160, 42, new Vector4(28f, 28f, 16f, 16f), (x, y, d, xn, yn) =>
            {
                if (d < 0f)
                    return Color.clear;

                Color ink = new(0.004f, 0.006f, 0.012f, 1f);
                Color gold = new(1f, 0.76f, 0.34f, 1f);
                Color copper = new(0.42f, 0.2f, 0.07f, 1f);
                Color steel = new(0.12f, 0.28f, 0.36f, 1f);
                Color body = Color.Lerp(new Color(0.01f, 0.02f, 0.038f, 1f), new Color(0.06f, 0.1f, 0.13f, 1f), yn);

                Color color;
                if (d < 1.2f)
                    color = ink;
                else if (d < 4.8f)
                    color = Color.Lerp(copper, gold, Mathf.Clamp01(yn * 0.8f + (1f - xn) * 0.2f));
                else if (d < 7.2f)
                    color = Color.Lerp(ink, steel, (d - 4.8f) / 2.4f);
                else
                    color = body;

                if (yn > 0.74f && d > 8f)
                    color = Color.Lerp(color, new Color(0.16f, 0.72f, 0.88f, 1f), 0.16f);

                return new Color(color.r, color.g, color.b, Mathf.Clamp01(d + 0.5f));
            }, 10f);
            return bossHealthFrameSprite;
        }

        private static Sprite GetBossHealthFillSprite()
        {
            if (bossHealthFillSprite != null)
                return bossHealthFillSprite;

            bossHealthFillSprite = BakeBossHealthSprite("Boss Health Fill", 96, 20, new Vector4(10f, 10f, 6f, 6f), (x, y, d, xn, yn) =>
            {
                if (d < 0f)
                    return Color.clear;

                float shine = Mathf.Clamp01((yn - 0.48f) * 2.6f);
                Color color = Color.Lerp(new Color(0.55f, 0.02f, 0.018f, 1f), new Color(1f, 0.36f, 0.18f, 1f), yn);
                color = Color.Lerp(color, Color.white, shine * 0.22f);
                if (d < 1.8f)
                    color = Color.Lerp(new Color(0.08f, 0.006f, 0.004f, 1f), color, d / 1.8f);
                return new Color(color.r, color.g, color.b, Mathf.Clamp01(d + 0.35f));
            }, 5f);
            return bossHealthFillSprite;
        }

        private static Sprite GetBossHealthGlowSprite()
        {
            if (bossHealthGlowSprite != null)
                return bossHealthGlowSprite;

            bossHealthGlowSprite = BakeBossHealthSprite("Boss Health Glow", 96, 24, new Vector4(12f, 12f, 8f, 8f), (x, y, d, xn, yn) =>
            {
                if (d < -4f)
                    return Color.clear;

                float center = 1f - Mathf.Abs(yn - 0.5f) * 2f;
                float alpha = Mathf.Clamp01((d + 4f) / 8f) * center;
                Color color = Color.Lerp(new Color(0.2f, 0.9f, 1f, 1f), Color.white, Mathf.Clamp01(yn * 0.7f));
                return new Color(color.r, color.g, color.b, alpha);
            }, 8f);
            return bossHealthGlowSprite;
        }

        private delegate Color BossHealthPixelShader(int x, int y, float insideDistance, float xn, float yn);

        private static Sprite BakeBossHealthSprite(string name, int width, int height, Vector4 border, BossHealthPixelShader shade, float radius)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            float halfW = (width - 1) * 0.5f;
            float halfH = (height - 1) * 0.5f;

            for (int y = 0; y < height; y++)
            {
                float yn = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float xn = x / (float)(width - 1);
                    float qx = Mathf.Abs(x - halfW) - (halfW - radius);
                    float qy = Mathf.Abs(y - halfH) - (halfH - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float d = radius - (outside + inside);
                    pixels[y * width + x] = shade(x, y, d, xn, yn);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u,
                SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private IEnumerator PlayComposableGolemHitEffectRoutine(Sprite sprite, ComposableGolemForm form)
        {
            Image effect = CreateImage("Composable Golem Hit Effect", transform, Color.white);
            effect.sprite = sprite;
            effect.preserveAspect = true;
            effect.raycastTarget = false;
            SetAnchors(effect.rectTransform, new Vector2(-0.18f, -0.1f), new Vector2(1.18f, 1.18f));
            effect.transform.SetAsLastSibling();

            Color tint = form switch
            {
                ComposableGolemForm.Crystal => new Color(0.45f, 0.95f, 1f, 0.78f),
                ComposableGolemForm.Glass => new Color(0.78f, 1f, 0.9f, 0.66f),
                ComposableGolemForm.Iron => new Color(1f, 0.55f, 0.16f, 0.72f),
                _ => new Color(1f, 1f, 1f, 0.7f)
            };

            float elapsed = 0f;
            const float duration = 0.42f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Sin(progress * Mathf.PI) * tint.a;
                effect.color = new Color(tint.r, tint.g, tint.b, alpha);
                effect.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.28f, progress);
                yield return null;
            }

            if (effect != null)
                Destroy(effect.gameObject);
        }

        private IEnumerator PlayAttachmentEquipEffectRoutine()
        {
            Color auraColor = new(0.18f, 1f, 0.42f, 0f);
            Color auraEdge = new(0.72f, 1f, 0.5f, 0.72f);
            Image aura = CreateImage("Attachment Equip Aura", transform, auraColor);
            aura.sprite = GetAuraSprite();
            aura.type = Image.Type.Simple;
            aura.raycastTarget = false;
            SetAnchors(aura.rectTransform, new Vector2(-0.08f, -0.08f), new Vector2(1.08f, 1.08f));
            aura.transform.SetAsLastSibling();
            Outline auraOutline = aura.gameObject.AddComponent<Outline>();
            auraOutline.effectColor = auraEdge;
            auraOutline.effectDistance = new Vector2(4f, -4f);

            const int plusCount = 9;
            Text[] pluses = new Text[plusCount];
            Vector2[] startPositions = new Vector2[plusCount];
            float[] delays = new float[plusCount];
            float[] travelDistances = new float[plusCount];
            float[] sideDrifts = new float[plusCount];
            Font font = MmoUiTheme.TitleFont;
            for (int i = 0; i < plusCount; i++)
            {
                pluses[i] = CreateText("Attachment Equip Plus " + i, transform, font, 32, FontStyle.Bold, TextAnchor.MiddleCenter);
                pluses[i].color = new Color(0.72f, 1f, 0.5f, 0f);
                pluses[i].raycastTarget = false;
                RectTransform plusRect = pluses[i].rectTransform;
                SetAnchors(plusRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                plusRect.sizeDelta = new Vector2(44f, 44f);
                plusRect.localScale = Vector3.one;
                pluses[i].text = "+";
                Outline plusOutline = pluses[i].gameObject.AddComponent<Outline>();
                plusOutline.effectColor = new Color(0.02f, 0.16f, 0.04f, 0.9f);
                plusOutline.effectDistance = new Vector2(2f, -2f);
                pluses[i].transform.SetAsLastSibling();

                float spread = (i / (float)(plusCount - 1)) - 0.5f;
                float stagger = i % 3;
                startPositions[i] = new Vector2(spread * 150f, -62f - stagger * 13f);
                delays[i] = i * 0.045f;
                travelDistances[i] = 190f + (i % 4) * 18f;
                sideDrifts[i] = ((i % 2 == 0) ? 1f : -1f) * (16f + (i % 3) * 8f);
            }

            const int sparkleCount = 6;
            Image[] sparkles = new Image[sparkleCount];
            Vector2[] sparkleStarts = new Vector2[sparkleCount];
            float[] sparkleDelays = new float[sparkleCount];
            for (int i = 0; i < sparkleCount; i++)
            {
                sparkles[i] = CreateImage("Attachment Equip Sparkle " + i, transform, Color.white);
                sparkles[i].sprite = GetSparkleSprite();
                SetAnchors(sparkles[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                sparkles[i].rectTransform.sizeDelta = new Vector2(26f, 26f);
                sparkles[i].transform.SetAsLastSibling();
                sparkleStarts[i] = new Vector2(((i % 3) - 1) * 58f, -34f - (i / 3) * 22f);
                sparkleDelays[i] = 0.1f + i * 0.07f;
            }

            float elapsed = 0f;
            const float duration = 0.9f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float auraAlpha = Mathf.Sin(progress * Mathf.PI) * 0.38f;
                aura.color = new Color(auraColor.r, auraColor.g, auraColor.b, auraAlpha);
                aura.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.14f, progress);

                for (int i = 0; i < pluses.Length; i++)
                {
                    Text plus = pluses[i];
                    if (plus == null)
                        continue;

                    float localProgress = Mathf.Clamp01((elapsed - delays[i]) / (duration - delays[i] * 0.6f));
                    float alpha = Mathf.Sin(localProgress * Mathf.PI) * 0.95f;
                    plus.color = new Color(0.74f, 1f, 0.48f, alpha);
                    plus.rectTransform.anchoredPosition = startPositions[i]
                        + new Vector2(Mathf.Sin(localProgress * Mathf.PI) * sideDrifts[i], localProgress * travelDistances[i]);
                    float scale = Mathf.Lerp(0.72f, 1.18f, Mathf.Sin(localProgress * Mathf.PI));
                    plus.rectTransform.localScale = Vector3.one * scale;
                }

                for (int i = 0; i < sparkles.Length; i++)
                {
                    Image sparkle = sparkles[i];
                    if (sparkle == null)
                        continue;

                    float localProgress = Mathf.Clamp01((elapsed - sparkleDelays[i]) / 0.5f);
                    float alpha = Mathf.Sin(localProgress * Mathf.PI) * 0.68f;
                    sparkle.color = new Color(0.9f, 1f, 0.62f, alpha);
                    sparkle.rectTransform.anchoredPosition = sparkleStarts[i] + new Vector2(0f, localProgress * 132f);
                    sparkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, localProgress * 120f);
                    sparkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.58f, 1.05f, localProgress);
                }

                yield return null;
            }

            if (aura != null)
                Destroy(aura.gameObject);
            for (int i = 0; i < pluses.Length; i++)
            {
                if (pluses[i] != null)
                    Destroy(pluses[i].gameObject);
            }
            for (int i = 0; i < sparkles.Length; i++)
            {
                if (sparkles[i] != null)
                    Destroy(sparkles[i].gameObject);
            }
        }

        private IEnumerator PlayPalatirLocalVfx(string effectName, ClassFamily family, bool burst)
        {
            Color primary = PalatirShieldColor(family, 1f);
            Color secondary = new Color(0.92f, 0.78f, 1f, 1f);
            Image aura = CreateImage(effectName + " Aura", transform, primary);
            aura.sprite = GetAuraSprite();
            aura.raycastTarget = false;
            SetAnchors(aura.rectTransform, new Vector2(-0.18f, -0.18f), new Vector2(1.18f, 1.18f));
            aura.transform.SetAsLastSibling();

            const int sparkleCount = 12;
            Image[] sparkles = new Image[sparkleCount];
            Vector2[] directions = new Vector2[sparkleCount];
            for (int index = 0; index < sparkleCount; index++)
            {
                float angle = (360f / sparkleCount) * index * Mathf.Deg2Rad;
                directions[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                sparkles[index] = CreateImage(effectName + " Star " + index, transform, secondary);
                sparkles[index].sprite = GetSparkleSprite();
                sparkles[index].raycastTarget = false;
                SetAnchors(sparkles[index].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                sparkles[index].rectTransform.sizeDelta = new Vector2(index % 3 == 0 ? 34f : 22f, index % 3 == 0 ? 34f : 22f);
                sparkles[index].transform.SetAsLastSibling();
            }

            float elapsed = 0f;
            float duration = burst ? 0.62f : 0.44f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(progress * Mathf.PI);
                aura.color = new Color(primary.r, primary.g, primary.b, wave * (burst ? 0.46f : 0.28f));
                aura.rectTransform.localScale = Vector3.one * Mathf.Lerp(burst ? 0.72f : 0.9f, burst ? 1.42f : 1.12f, progress);

                for (int index = 0; index < sparkles.Length; index++)
                {
                    Image sparkle = sparkles[index];
                    if (sparkle == null)
                        continue;

                    float distance = Mathf.Lerp(burst ? 18f : 10f, burst ? 118f : 64f, progress);
                    sparkle.rectTransform.anchoredPosition = directions[index] * distance;
                    sparkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, progress * 220f + index * 17f);
                    sparkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.45f, burst ? 1.18f : 0.88f, wave);
                    Color sparkleColor = Color.Lerp(primary, secondary, index / (float)sparkles.Length);
                    sparkle.color = new Color(sparkleColor.r, sparkleColor.g, sparkleColor.b, wave * 0.86f);
                }
                yield return null;
            }

            if (aura != null)
                Destroy(aura.gameObject);
            for (int index = 0; index < sparkles.Length; index++)
            {
                if (sparkles[index] != null)
                    Destroy(sparkles[index].gameObject);
            }
        }

        private IEnumerator PlayPalatirCosmicTrail(PrototypeCardView defender, bool hit)
        {
            RectTransform parent = EffectCanvasRoot();
            if (parent == null || defender == null)
                yield break;

            Vector2 start = parent.InverseTransformPoint(RectTransform.position);
            Vector2 end = parent.InverseTransformPoint(defender.RectTransform.position);
            Vector2 direction = end - start;
            float length = Mathf.Max(24f, direction.magnitude);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Image core = CreateTrailImage(parent, "Palatir Cosmic Trail Core", new Color(0.82f, 0.24f, 1f, 0.9f), length, angle, 16f);
            Image glow = CreateTrailImage(parent, "Palatir Cosmic Trail Glow", new Color(0.18f, 0.84f, 1f, 0.34f), length, angle, 42f);
            Image star = CreateTrailImage(parent, "Palatir Cosmic Trail Star", Color.white, length * 0.42f, angle, 8f);

            Image[] trails = { glow, core, star };
            float elapsed = 0f;
            const float duration = 0.48f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                Vector2 center = Vector2.Lerp(start, end, eased);
                Vector2 normal = Perpendicular(direction).normalized;
                for (int index = 0; index < trails.Length; index++)
                {
                    Image trail = trails[index];
                    if (trail == null)
                        continue;

                    Color color = trail.color;
                    color.a = Mathf.Sin(progress * Mathf.PI) * (index == 0 ? 0.36f : 0.82f) * (hit ? 1f : 0.62f);
                    trail.color = color;
                    trail.rectTransform.anchoredPosition = center + normal * Mathf.Sin(progress * Mathf.PI * (index + 2)) * (10f + index * 7f);
                    trail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + Mathf.Sin(progress * Mathf.PI * 2f) * 7f);
                    trail.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(length * 0.12f, length * (index == 2 ? 0.32f : 0.86f), progress), trail.rectTransform.sizeDelta.y);
                }
                yield return null;
            }

            for (int index = 0; index < trails.Length; index++)
            {
                if (trails[index] != null)
                    Destroy(trails[index].gameObject);
            }
        }

        private IEnumerator PlayComposableGolemTrail(PrototypeCardView defender, ComposableGolemForm form, bool hit)
        {
            RectTransform parent = EffectCanvasRoot();
            if (parent == null || defender == null)
                yield break;

            Vector2 start = parent.InverseTransformPoint(RectTransform.position);
            Vector2 end = parent.InverseTransformPoint(defender.RectTransform.position);
            Vector2 direction = end - start;
            float length = Mathf.Max(18f, direction.magnitude);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector2 normal = Perpendicular(direction).normalized;
            Color primary = GolemVfxPrimary(form);
            Color secondary = GolemVfxSecondary(form);
            Color hotCore = Color.Lerp(primary, Color.white, form == ComposableGolemForm.Iron ? 0.18f : 0.52f);
            Image[] trails =
            {
                CreateTrailImage(parent, "Golem Attack Trail Atmospheric Wake", new Color(secondary.r, secondary.g, secondary.b, 0.18f), length, angle, 86f),
                CreateTrailImage(parent, "Golem Attack Trail Outer Glow", new Color(secondary.r, secondary.g, secondary.b, 0.28f), length, angle, 58f),
                CreateTrailImage(parent, "Golem Attack Trail Core", hotCore, length, angle, form == ComposableGolemForm.Iron ? 30f : 20f),
                CreateTrailImage(parent, "Golem Attack Trail Edge A", primary, length, angle, 8f),
                CreateTrailImage(parent, "Golem Attack Trail Edge B", secondary, length, angle, 7f),
                CreateTrailImage(parent, "Golem Attack Trail Spark", Color.Lerp(primary, Color.white, 0.62f), length, angle, 4f)
            };
            const int moteCount = 28;
            Image[] motes = new Image[moteCount];
            float[] moteOffsets = new float[moteCount];
            float[] moteSpeeds = new float[moteCount];
            for (int i = 0; i < moteCount; i++)
            {
                Color moteColor = i % 3 == 0 ? Color.Lerp(secondary, Color.white, 0.42f) : primary;
                motes[i] = CreateImage("Golem Attack Arc Mote " + i, parent, moteColor);
                motes[i].raycastTarget = false;
                RectTransform moteRect = motes[i].rectTransform;
                moteRect.anchorMin = new Vector2(0.5f, 0.5f);
                moteRect.anchorMax = new Vector2(0.5f, 0.5f);
                moteRect.pivot = new Vector2(0.5f, 0.5f);
                moteRect.sizeDelta = FormParticleSize(form, shield: false, i) * (i % 4 == 0 ? 0.92f : 0.48f);
                moteRect.localRotation = Quaternion.Euler(0f, 0f, angle + (i % 2 == 0 ? 90f : 0f));
                moteRect.SetAsLastSibling();
                moteOffsets[i] = Mathf.Lerp(-48f, 48f, (i % 9) / 8f) + Mathf.Sin(i * 2.17f) * 16f;
                moteSpeeds[i] = Mathf.Lerp(0.08f, 0.34f, (i % 5) / 4f);
            }

            Image targetTelegraph = CreateImage("Golem Attack Target Telegraph", parent, primary);
            targetTelegraph.raycastTarget = false;
            targetTelegraph.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            targetTelegraph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            targetTelegraph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            targetTelegraph.rectTransform.anchoredPosition = end;
            targetTelegraph.rectTransform.sizeDelta = new Vector2(142f, 142f);
            Outline telegraphOutline = targetTelegraph.gameObject.AddComponent<Outline>();
            telegraphOutline.effectColor = Color.Lerp(secondary, Color.white, 0.34f);
            telegraphOutline.effectDistance = new Vector2(5f, -5f);
            targetTelegraph.transform.SetAsLastSibling();

            float elapsed = 0f;
            const float duration = 0.66f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                float arcHeight = form switch
                {
                    ComposableGolemForm.Iron => Mathf.Sin(progress * Mathf.PI) * -14f,
                    ComposableGolemForm.Crystal => Mathf.Sin(progress * Mathf.PI) * 28f,
                    ComposableGolemForm.Glass => Mathf.Sin(progress * Mathf.PI * 2f) * 18f,
                    _ => 0f
                };
                Vector2 center = Vector2.Lerp(start, end, eased * 0.82f + 0.08f) + normal * arcHeight;
                for (int i = 0; i < trails.Length; i++)
                {
                    Image trail = trails[i];
                    if (trail == null)
                        continue;

                    float phase = Mathf.Clamp01(progress - i * 0.045f);
                    float alpha = Mathf.Sin(phase * Mathf.PI) * (hit ? 0.88f : 0.58f);
                    Color color = trail.color;
                    color.a = alpha * (i == 0 ? 0.18f : i == 1 ? 0.32f : i == 5 ? 0.95f : 0.72f);
                    trail.color = color;
                    float side = i == 3 ? -16f : i == 4 ? 16f : Mathf.Sin(progress * Mathf.PI * (i + 1)) * (i * 3.5f);
                    trail.rectTransform.anchoredPosition = center + normal * side;
                    trail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + Mathf.Sin(progress * Mathf.PI * 2f + i) * (form == ComposableGolemForm.Glass ? 10f : 4f));
                    trail.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(length * 0.12f, length * (i == 0 ? 1.08f : 0.96f), phase), trail.rectTransform.sizeDelta.y);
                }

                for (int i = 0; i < motes.Length; i++)
                {
                    Image mote = motes[i];
                    if (mote == null)
                        continue;

                    float travel = Mathf.Clamp01(progress * (1.08f + moteSpeeds[i]) - moteSpeeds[i]);
                    float wobble = Mathf.Sin((progress * 9f + i) * Mathf.PI) * (form == ComposableGolemForm.Iron ? 5f : 12f);
                    mote.rectTransform.anchoredPosition = Vector2.Lerp(start, end, travel) + normal * (moteOffsets[i] * Mathf.Sin(travel * Mathf.PI) + wobble);
                    mote.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.1f, Mathf.Sin(travel * Mathf.PI));
                    mote.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle + progress * (form == ComposableGolemForm.Crystal ? 420f : 250f) + i * 19f);
                    Color moteColor = mote.color;
                    moteColor.a = Mathf.Sin(travel * Mathf.PI) * (hit ? 0.82f : 0.44f);
                    mote.color = moteColor;
                }

                float telegraphPulse = Mathf.Clamp01(progress / 0.72f);
                targetTelegraph.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.22f, 0.42f, telegraphPulse);
                targetTelegraph.rectTransform.localRotation = Quaternion.Euler(0f, 0f, progress * (form == ComposableGolemForm.Iron ? -220f : 360f));
                targetTelegraph.color = new Color(primary.r, primary.g, primary.b, Mathf.Sin(telegraphPulse * Mathf.PI) * 0.28f);
                yield return null;
            }

            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null)
                    Destroy(trails[i].gameObject);
            }
            for (int i = 0; i < motes.Length; i++)
            {
                if (motes[i] != null)
                    Destroy(motes[i].gameObject);
            }
            if (targetTelegraph != null)
                Destroy(targetTelegraph.gameObject);
        }

        private IEnumerator PlayComposableGolemLocalVfx(string effectName, ComposableGolemForm form, bool strong, bool shield)
        {
            Color primary = GolemVfxPrimary(form);
            Color secondary = GolemVfxSecondary(form);
            Color whiteHot = Color.Lerp(primary, Color.white, form == ComposableGolemForm.Iron ? 0.28f : 0.7f);
            Image flash = CreateImage(effectName + " Flash", transform, whiteHot);
            flash.raycastTarget = false;
            SetAnchors(flash.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            flash.transform.SetAsLastSibling();

            Image ring = CreateImage(effectName + " Ring", transform, primary);
            ring.raycastTarget = false;
            SetAnchors(ring.rectTransform, new Vector2(-0.1f, -0.1f), new Vector2(1.1f, 1.1f));
            ring.transform.SetAsLastSibling();
            Outline ringOutline = ring.gameObject.AddComponent<Outline>();
            ringOutline.effectColor = secondary;
            ringOutline.effectDistance = shield ? new Vector2(6f, -6f) : new Vector2(3.5f, -3.5f);

            Image innerRing = CreateImage(effectName + " Inner Rune Ring", transform, secondary);
            innerRing.raycastTarget = false;
            SetAnchors(innerRing.rectTransform, new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f));
            innerRing.transform.SetAsLastSibling();
            Outline innerOutline = innerRing.gameObject.AddComponent<Outline>();
            innerOutline.effectColor = Color.Lerp(secondary, Color.white, 0.5f);
            innerOutline.effectDistance = new Vector2(2.5f, -2.5f);

            int particleCount = strong ? 18 : 12;
            Image[] particles = new Image[particleCount];
            Vector2[] directions = new Vector2[particleCount];
            for (int i = 0; i < particleCount; i++)
            {
                Color particleTint = i % 4 == 0 ? whiteHot : i % 2 == 0 ? primary : secondary;
                particles[i] = CreateImage(effectName + " Spark " + i, transform, particleTint);
                particles[i].raycastTarget = false;
                SetAnchors(particles[i].rectTransform, new Vector2(0.48f, 0.48f), new Vector2(0.52f, 0.52f));
                particles[i].transform.SetAsLastSibling();
                float angle = (360f / particleCount) * i + (shield ? 18f : 0f) + (i % 3) * 7f;
                directions[i] = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            }

            Image[] shieldFacets = shield ? new Image[8] : null;
            if (shieldFacets != null)
            {
                for (int i = 0; i < shieldFacets.Length; i++)
                {
                    Color facetTint = i % 2 == 0 ? Color.Lerp(primary, Color.white, 0.28f) : secondary;
                    shieldFacets[i] = CreateImage(effectName + " Defensive Facet " + i, transform, facetTint);
                    shieldFacets[i].raycastTarget = false;
                    SetAnchors(shieldFacets[i].rectTransform, new Vector2(0.48f, 0.48f), new Vector2(0.52f, 0.52f));
                    shieldFacets[i].transform.SetAsLastSibling();
                }
            }

            float elapsed = 0f;
            float duration = strong ? 0.82f : 0.62f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Sin(progress * Mathf.PI) * (strong ? 0.78f : 0.48f);
                float snap = 1f - Mathf.SmoothStep(0f, 1f, progress);
                flash.color = new Color(whiteHot.r, whiteHot.g, whiteHot.b, snap * (strong ? 0.28f : 0.16f));
                flash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.58f, 1.45f, progress);
                ring.color = new Color(primary.r, primary.g, primary.b, shield ? alpha * 0.34f : alpha * 0.22f);
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(shield ? 0.82f : 0.54f, strong ? 1.55f : 1.12f, progress);
                ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, progress * (form == ComposableGolemForm.Iron ? -90f : 160f));
                innerRing.color = new Color(secondary.r, secondary.g, secondary.b, alpha * (shield ? 0.48f : 0.34f));
                innerRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(strong ? 0.45f : 0.65f, strong ? 1.08f : 0.95f, progress);
                innerRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -progress * (form == ComposableGolemForm.Glass ? 300f : 210f));

                for (int i = 0; i < particles.Length; i++)
                {
                    Image particle = particles[i];
                    if (particle == null)
                        continue;

                    float swirl = Mathf.Sin(progress * Mathf.PI * (form == ComposableGolemForm.Crystal ? 2.5f : 1.4f) + i) * (strong ? 18f : 8f);
                    Vector2 tangent = Perpendicular(directions[i]);
                    float distance = Mathf.Lerp(6f, strong ? 92f : 54f, Mathf.SmoothStep(0f, 1f, progress));
                    particle.rectTransform.anchoredPosition = directions[i] * distance + tangent * swirl;
                    particle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(directions[i].y, directions[i].x) * Mathf.Rad2Deg + progress * 260f);
                    particle.rectTransform.sizeDelta = FormParticleSize(form, shield, i) * (strong && i % 5 == 0 ? 1.35f : 1f);
                    Color particleColor = particle.color;
                    particleColor.a = alpha * (i % 5 == 0 ? 1f : 0.72f);
                    particle.color = particleColor;
                }

                if (shieldFacets != null)
                {
                    for (int i = 0; i < shieldFacets.Length; i++)
                    {
                        Image facet = shieldFacets[i];
                        if (facet == null)
                            continue;

                        float angle = i * 45f + progress * (form == ComposableGolemForm.Iron ? -35f : 80f);
                        Vector2 direction = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                        Vector2 tangent = Perpendicular(direction);
                        float braceDistance = Mathf.Lerp(34f, strong ? 86f : 68f, Mathf.SmoothStep(0f, 1f, progress));
                        facet.rectTransform.anchoredPosition = direction * braceDistance;
                        facet.rectTransform.sizeDelta = FormShieldFacetSize(form, i) * Mathf.Lerp(0.7f, strong ? 1.16f : 0.96f, Mathf.Sin(progress * Mathf.PI));
                        facet.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                        Color facetColor = facet.color;
                        facetColor.a = alpha * (strong ? 0.78f : 0.55f);
                        facet.color = facetColor;
                    }
                }
                yield return null;
            }

            if (flash != null)
                Destroy(flash.gameObject);
            if (ring != null)
                Destroy(ring.gameObject);
            if (innerRing != null)
                Destroy(innerRing.gameObject);
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                    Destroy(particles[i].gameObject);
            }
            if (shieldFacets != null)
            {
                for (int i = 0; i < shieldFacets.Length; i++)
                {
                    if (shieldFacets[i] != null)
                        Destroy(shieldFacets[i].gameObject);
                }
            }
        }

        private IEnumerator PlayComposableGolemAttackAnticipation(ComposableGolemForm form)
        {
            Vector3 baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            Color primary = GolemVfxPrimary(form);
            Image aura = CreateImage("Golem Attack AAA Anticipation Aura", transform, primary);
            aura.raycastTarget = false;
            SetAnchors(aura.rectTransform, new Vector2(-0.18f, -0.18f), new Vector2(1.18f, 1.18f));
            aura.transform.SetAsLastSibling();

            float elapsed = 0f;
            const float duration = 0.28f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(progress * Mathf.PI);
                if (rectTransform != null)
                    rectTransform.localScale = baseScale * (1f + pulse * 0.055f);
                aura.color = new Color(primary.r, primary.g, primary.b, pulse * 0.18f);
                aura.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.22f, progress);
                yield return null;
            }

            if (rectTransform != null)
                rectTransform.localScale = baseScale;
            if (aura != null)
                Destroy(aura.gameObject);
        }

        private IEnumerator PlayComposableGolemImpactShake(float strength)
        {
            if (rectTransform == null)
                yield break;

            Vector2 basePosition = rectTransform.anchoredPosition;
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float falloff = 1f - progress;
                rectTransform.anchoredPosition = basePosition + new Vector2(
                    Mathf.Sin(progress * 72f) * strength * falloff,
                    Mathf.Cos(progress * 59f) * strength * 0.62f * falloff);
                yield return null;
            }

            rectTransform.anchoredPosition = basePosition;
        }

        private RectTransform EffectCanvasRoot()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : transform.root as RectTransform;
        }

        private static Image CreateTrailImage(RectTransform parent, string name, Color color, float length, float angle, float thickness)
        {
            Image trail = CreateImage(name, parent, color);
            trail.raycastTarget = false;
            RectTransform rect = trail.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(length, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            trail.transform.SetAsLastSibling();
            return trail;
        }

        private static Vector2 Perpendicular(Vector2 value)
        {
            return new Vector2(-value.y, value.x);
        }

        private static Vector2 FormParticleSize(ComposableGolemForm form, bool shield, int index)
        {
            return form switch
            {
                ComposableGolemForm.Iron => new Vector2(index % 2 == 0 ? 28f : 16f, shield ? 8f : 12f),
                ComposableGolemForm.Crystal => new Vector2(shield ? 14f : 18f, index % 2 == 0 ? 34f : 24f),
                ComposableGolemForm.Glass => new Vector2(index % 2 == 0 ? 22f : 12f, shield ? 22f : 30f),
                _ => new Vector2(18f, 18f)
            };
        }

        private static Vector2 FormShieldFacetSize(ComposableGolemForm form, int index)
        {
            return form switch
            {
                ComposableGolemForm.Iron => new Vector2(index % 2 == 0 ? 52f : 34f, 12f),
                ComposableGolemForm.Crystal => new Vector2(18f, index % 2 == 0 ? 58f : 42f),
                ComposableGolemForm.Glass => new Vector2(index % 2 == 0 ? 42f : 26f, index % 3 == 0 ? 46f : 32f),
                _ => new Vector2(32f, 20f)
            };
        }

        private static Color GolemVfxPrimary(ComposableGolemForm form)
        {
            return form switch
            {
                ComposableGolemForm.Iron => new Color(1f, 0.45f, 0.12f, 0.9f),
                ComposableGolemForm.Crystal => new Color(0.18f, 0.86f, 1f, 0.86f),
                ComposableGolemForm.Glass => new Color(0.64f, 1f, 0.9f, 0.66f),
                _ => Color.white
            };
        }

        private static Color GolemVfxSecondary(ComposableGolemForm form)
        {
            return form switch
            {
                ComposableGolemForm.Iron => new Color(1f, 0.78f, 0.26f, 0.72f),
                ComposableGolemForm.Crystal => new Color(0.72f, 0.96f, 1f, 0.7f),
                ComposableGolemForm.Glass => new Color(0.9f, 1f, 1f, 0.48f),
                _ => new Color(1f, 1f, 1f, 0.7f)
            };
        }

        private void CreateLifeIcons()
        {
            if (lifeIconsRoot != null)
                return;

            heartIcon = Resources.Load<Sprite>("UI/heart_icon");
            blackHeartIcon = Resources.Load<Sprite>("UI/blackheart_icon")
                ?? Resources.Load<Sprite>("UI/blackhearth_icon");

            lifeIconsRoot = new GameObject(
                "Life Icons",
                typeof(RectTransform));
            lifeIconsRoot.transform.SetParent(transform, false);
            Stretch((RectTransform)lifeIconsRoot.transform);

            leftLifeIcon = CreateLifeIcon("Left Life Icon", new Vector2(0.06f, 0.74f), new Vector2(0.28f, 0.96f));
            rightLifeIcon = CreateLifeIcon("Right Life Icon", new Vector2(0.72f, 0.74f), new Vector2(0.94f, 0.96f));

            lifeIconsRoot.transform.SetAsLastSibling();
            lifeIconsRoot.SetActive(false);
        }

        private Image CreateLifeIcon(string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            Image icon = CreateImage(name, lifeIconsRoot.transform, Color.white);
            icon.sprite = heartIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetAnchors(icon.rectTransform, anchorMin, anchorMax);
            return icon;
        }

        private IEnumerator DeploymentRoutine(
            Vector3 startWorldPosition,
            Quaternion startWorldRotation,
            float duration)
        {
            LayoutElement layout = GetComponent<LayoutElement>();
            Vector2 targetPosition = rectTransform.anchoredPosition;
            Quaternion targetRotation = Quaternion.identity;
            Vector3 targetScale = rectTransform.localScale;
            layout.ignoreLayout = true;
            rectTransform.position = startWorldPosition;
            rectTransform.rotation = startWorldRotation;
            Vector2 startPosition = rectTransform.anchoredPosition;
            Quaternion localStartRotation = rectTransform.localRotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, progress);
                rectTransform.localRotation = Quaternion.SlerpUnclamped(localStartRotation, targetRotation, progress);
                rectTransform.localScale = Vector3.LerpUnclamped(Vector3.one * 0.94f, targetScale, progress);
                yield return null;
            }
            rectTransform.anchoredPosition = targetPosition;
            rectTransform.localRotation = targetRotation;
            rectTransform.localScale = targetScale;
            layout.ignoreLayout = transform.parent != null
                && IsManualBattlefieldRow(transform.parent.name);
            motionCoroutine = null;
        }

        private IEnumerator RevealRoutine(float duration)
        {
            LayoutElement layout = GetComponent<LayoutElement>();
            Vector3 targetScale = rectTransform.localScale;
            Vector2 targetPosition = rectTransform.anchoredPosition;
            layout.ignoreLayout = true;
            rectTransform.anchoredPosition = targetPosition + new Vector2(0f, 55f);
            rectTransform.localScale = new Vector3(0.04f, targetScale.y * 0.92f, 1f);
            canvasGroup.alpha = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(targetPosition + new Vector2(0f, 55f), targetPosition, progress);
                rectTransform.localScale = new Vector3(
                    Mathf.Lerp(0.04f, targetScale.x, progress),
                    Mathf.Lerp(targetScale.y * 0.92f, targetScale.y, progress),
                    1f);
                canvasGroup.alpha = Mathf.Lerp(0.25f, 1f, progress);
                yield return null;
            }
            rectTransform.anchoredPosition = targetPosition;
            rectTransform.localScale = targetScale;
            canvasGroup.alpha = 1f;
            layout.ignoreLayout = transform.parent != null
                && IsManualBattlefieldRow(transform.parent.name);
            motionCoroutine = null;
        }

        private static bool IsManualBattlefieldRow(string parentName)
        {
            return parentName == "CPU Formation" || parentName == "Player Formation";
        }

        private void EnsureTurnAura()
        {
            if (turnAuraRoot != null)
                return;

            turnAuraRoot = new GameObject("Active Turn Aura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            turnAuraRoot.transform.SetParent(transform, false);
            turnAuraRoot.transform.SetAsFirstSibling();
            turnAuraRect = (RectTransform)turnAuraRoot.transform;
            SetAnchors(turnAuraRect, new Vector2(-0.07f, -0.07f), new Vector2(1.07f, 1.07f));

            turnAuraImage = turnAuraRoot.GetComponent<Image>();
            turnAuraImage.sprite = GetAuraSprite();
            turnAuraImage.raycastTarget = false;

            for (int index = 0; index < 10; index++)
            {
                Image sparkle = CreateImage($"Sparkle {index + 1}", turnAuraRoot.transform, Color.white);
                sparkle.sprite = GetSparkleSprite();
                RectTransform sparkleRect = sparkle.rectTransform;
                sparkleRect.anchorMin = new Vector2(0.5f, 0.5f);
                sparkleRect.anchorMax = new Vector2(0.5f, 0.5f);
                sparkleRect.pivot = new Vector2(0.5f, 0.5f);
                sparkleRect.sizeDelta = Vector2.one * (index % 3 == 0 ? 15f : 10f);
                turnSparkles.Add(sparkle);
            }

            turnAuraRoot.SetActive(false);
        }

        private void EnsureBattleAura()
        {
            if (battleAuraRoot != null)
                return;

            battleAuraRoot = new GameObject("Battle Aura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            battleAuraRoot.transform.SetParent(transform, false);
            battleAuraRoot.transform.SetAsFirstSibling();
            battleAuraRect = (RectTransform)battleAuraRoot.transform;
            SetAnchors(battleAuraRect, new Vector2(-0.035f, -0.035f), new Vector2(1.035f, 1.035f));

            battleAuraImage = battleAuraRoot.GetComponent<Image>();
            battleAuraImage.sprite = GetAuraSprite();
            battleAuraImage.raycastTarget = false;

            Font font = AccardND.Battlefield.MmoUiTheme.BodyFont;
            battleAuraLabel = CreateText("Battle Aura Label", battleAuraRoot.transform, font, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
            battleAuraLabel.color = Color.white;
            Outline labelOutline = battleAuraLabel.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.78f);
            labelOutline.effectDistance = new Vector2(1.8f, -1.8f);
            SetAnchors(battleAuraLabel.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 1.02f));
            battleAuraLabel.gameObject.SetActive(false);

            battleAuraRoot.SetActive(false);
        }

        private void EnsureTargetHintAura()
        {
            if (targetHintAuraRoot != null)
                return;

            targetHintAuraRoot = new GameObject("Target Hint Aura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            targetHintAuraRoot.transform.SetParent(transform, false);
            targetHintAuraRoot.transform.SetAsFirstSibling();
            targetHintAuraRect = (RectTransform)targetHintAuraRoot.transform;
            SetAnchors(targetHintAuraRect, new Vector2(-0.055f, -0.055f), new Vector2(1.055f, 1.055f));

            targetHintAuraImage = targetHintAuraRoot.GetComponent<Image>();
            targetHintAuraImage.sprite = GetAuraSprite();
            targetHintAuraImage.raycastTarget = false;
            targetHintAuraRoot.SetActive(false);
        }

        private void EnsureSelectedAura()
        {
            if (selectedAuraRoot != null)
                return;

            selectedAuraRoot = new GameObject("Selected Spark Aura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            selectedAuraRoot.transform.SetParent(transform, false);
            selectedAuraRoot.transform.SetAsFirstSibling();
            selectedAuraRect = (RectTransform)selectedAuraRoot.transform;
            SetAnchors(selectedAuraRect, new Vector2(-0.105f, -0.105f), new Vector2(1.105f, 1.105f));

            selectedAuraImage = selectedAuraRoot.GetComponent<Image>();
            selectedAuraImage.sprite = GetAuraSprite();
            selectedAuraImage.raycastTarget = false;

            for (int index = 0; index < 12; index++)
            {
                Image sparkle = CreateImage($"Selected Sparkle {index + 1}", selectedAuraRoot.transform, Color.white);
                sparkle.sprite = GetSparkleSprite();
                sparkle.raycastTarget = false;
                RectTransform sparkleRect = sparkle.rectTransform;
                sparkleRect.anchorMin = new Vector2(0.5f, 0.5f);
                sparkleRect.anchorMax = new Vector2(0.5f, 0.5f);
                sparkleRect.pivot = new Vector2(0.5f, 0.5f);
                sparkleRect.sizeDelta = Vector2.one * (index % 4 == 0 ? 18f : 12f);
                selectedSparkles.Add(sparkle);
            }

            selectedAuraRoot.SetActive(false);
        }

        private void SetSelectedAura(bool active)
        {
            if (!active && selectedAuraRoot == null)
                return;

            EnsureSelectedAura();
            selectedAuraRoot.SetActive(active);
            if (active)
            {
                if (selectedAuraCoroutine == null)
                    selectedAuraCoroutine = StartCoroutine(AnimateSelectedAura());
                return;
            }

            if (selectedAuraCoroutine != null)
            {
                StopCoroutine(selectedAuraCoroutine);
                selectedAuraCoroutine = null;
            }

            selectedAuraRect.localScale = Vector3.one;
        }

        private void SetTargetHintColor(Color color)
        {
            selectionOutline.effectColor = color;
            SetTargetHintAura(true, color);
        }

        private void SetTargetHintAura(bool active, Color color)
        {
            targetHintAuraColor = color;
            targetHintAuraActive = active;
            if (battleAuraRoot != null)
                battleAuraRoot.SetActive(!active && battleAuraActive);
            targetHintAuraRoot.SetActive(active);

            if (active)
            {
                if (targetHintAuraCoroutine == null)
                    targetHintAuraCoroutine = StartCoroutine(AnimateTargetHintAura());
            }
            else
            {
                if (targetHintAuraCoroutine != null)
                {
                    StopCoroutine(targetHintAuraCoroutine);
                    targetHintAuraCoroutine = null;
                }

                targetHintAuraRect.localScale = Vector3.one;
            }
        }

        private GameObject CreateCrackOverlay(
            string objectName,
            Transform parent,
            Color color,
            float alpha,
            HeroClass? killerHeroClass = null)
        {
            GameObject crackObj = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            crackObj.transform.SetParent(parent, false);
            Image crackImg = crackObj.GetComponent<Image>();
            crackImg.sprite = GetDefeatCrackSprite(killerHeroClass, out string resourcePath, out bool usedFallback);
            crackImg.color = new Color(color.r, color.g, color.b, alpha);
            crackImg.material = null;
            crackImg.raycastTarget = false;
            Stretch((RectTransform)crackObj.transform);
            crackObj.transform.SetAsLastSibling();
            LogDeathCrack(
                usedFallback ? objectName + "-fallback" : objectName,
                killerHeroClass,
                resourcePath,
                crackImg.sprite,
                crackImg);
            return crackObj;
        }

        private static Sprite GetNecromancerDefeatSkullSprite()
        {
            if (necromancerDefeatSkullSprite != null)
                return necromancerDefeatSkullSprite;

            necromancerDefeatSkullSprite = Resources.Load<Sprite>("UI/necromancer_skull");
            if (necromancerDefeatSkullSprite != null)
                return necromancerDefeatSkullSprite;

            return necromancerDefeatSkullSprite = CreateFallbackNecromancerDefeatSkullSprite("Necromancer Defeat Skull Fallback");
        }

        private static Sprite GetPriestPurificationCrossSprite()
        {
            if (priestPurificationCrossSprite != null)
                return priestPurificationCrossSprite;

            priestPurificationCrossSprite = Resources.Load<Sprite>("UI/priest_purification_cross");
            if (priestPurificationCrossSprite != null)
                return priestPurificationCrossSprite;

            priestPurificationCrossSprite = Resources.Load<Sprite>("UI/priest_sacred_cross");
            if (priestPurificationCrossSprite != null)
                return priestPurificationCrossSprite;

            return priestPurificationCrossSprite = CreateFallbackPriestPurificationCrossSprite("Priest Purification Cross Fallback");
        }

        private static Sprite GetPriestPurificationHaloSprite()
        {
            if (priestPurificationHaloSprite != null)
                return priestPurificationHaloSprite;

            priestPurificationHaloSprite = Resources.Load<Sprite>("UI/priest_purification_halo");
            if (priestPurificationHaloSprite != null)
                return priestPurificationHaloSprite;

            return priestPurificationHaloSprite = GetDeathBurstRingSprite();
        }

        private static Sprite GetPriestPurificationSparkSprite()
        {
            if (priestPurificationSparkSprite != null)
                return priestPurificationSparkSprite;

            priestPurificationSparkSprite = Resources.Load<Sprite>("UI/priest_purification_spark");
            if (priestPurificationSparkSprite != null)
                return priestPurificationSparkSprite;

            return priestPurificationSparkSprite = GetPriestPurificationCrossSprite();
        }

        private static Sprite CreateFallbackPriestPurificationCrossSprite(string name)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - 47.5f) / 47.5f;
                    float dy = (y - 47.5f) / 47.5f;
                    float vertical = Mathf.Clamp01(1f - Mathf.Abs(dx) * 8.2f) * Mathf.Clamp01(1f - Mathf.Abs(dy) * 1.32f);
                    float horizontal = Mathf.Clamp01(1f - Mathf.Abs(dy + 0.2f) * 8.8f) * Mathf.Clamp01(1f - Mathf.Abs(dx) * 2.1f);
                    float core = Mathf.Clamp01(vertical + horizontal);
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.1f) * 0.55f;
                    byte alpha = (byte)Mathf.Clamp(Mathf.RoundToInt((core + glow * 0.62f) * 255f), 0, 255);
                    byte warm = (byte)Mathf.Lerp(226f, 255f, Mathf.Clamp01(core + glow));
                    pixels[y * size + x] = new Color32(255, warm, 214, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreateFallbackNecromancerDefeatSkullSprite(string name)
        {
            const int size = 96;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - 47.5f) / 47.5f;
                    float dy = (y - 47.5f) / 47.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float head = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx * 1.02f + (dy + 0.16f) * (dy + 0.16f) * 1.42f));
                    float cheek = Mathf.Clamp01(1f - Mathf.Abs(dx) * 1.65f) * Mathf.Clamp01(1f - Mathf.Abs(dy - 0.22f) * 2.6f);
                    float jaw = Mathf.Clamp01(1f - Mathf.Abs(dx) * 2.08f) * Mathf.Clamp01(1f - Mathf.Abs(dy - 0.54f) * 3.35f);
                    float leftEye = Mathf.Clamp01(1f - Mathf.Sqrt((dx + 0.32f) * (dx + 0.32f) * 34f + (dy + 0.08f) * (dy + 0.08f) * 46f));
                    float rightEye = Mathf.Clamp01(1f - Mathf.Sqrt((dx - 0.32f) * (dx - 0.32f) * 34f + (dy + 0.08f) * (dy + 0.08f) * 46f));
                    float nose = Mathf.Clamp01(1f - (Mathf.Abs(dx) * 6.4f + Mathf.Abs(dy - 0.18f) * 4.7f));
                    float teeth = Mathf.Clamp01(1f - Mathf.Abs(dy - 0.58f) * 20f) * Mathf.Clamp01(Mathf.Abs(Mathf.Sin((dx + 0.5f) * 34f)) * 1.55f);
                    float cracks = Mathf.Clamp01(1f - Mathf.Abs(dx + dy * 0.35f + Mathf.Sin(dy * 17f) * 0.04f) * 28f)
                        * Mathf.Clamp01(1f - Mathf.Abs(radius - 0.42f) * 2.8f);
                    float glow = Mathf.Clamp01(1f - radius) * 0.36f;
                    float silhouette = Mathf.Clamp01(head * 1.12f + cheek * 0.42f + jaw * 0.92f);
                    float holes = Mathf.Clamp01(leftEye + rightEye + nose);
                    byte a = (byte)Mathf.Clamp((silhouette - holes * 0.82f + teeth * 0.34f + cracks * 0.35f + glow) * 255f, 0f, 255f);
                    byte r = (byte)Mathf.Lerp(112f, 205f, Mathf.Clamp01(head + jaw));
                    byte g = (byte)Mathf.Lerp(164f, 255f, Mathf.Clamp01(head + jaw + glow));
                    byte b = (byte)Mathf.Lerp(82f, 138f, Mathf.Clamp01(head));
                    pixels[y * size + x] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private List<Image> DefeatEffectImages()
        {
            defeatEffectImages.RemoveAll(image => image == null);
            return defeatEffectImages;
        }

        private void PlaceDefeatEffectOverlay(Transform overlay)
        {
            if (overlay == null)
                return;

            int siblingIndex = -1;
            for (int index = 0; index < defeatEffectLayerAnchors.Count; index++)
            {
                Transform anchor = defeatEffectLayerAnchors[index];
                if (anchor != null && anchor.parent == transform)
                    siblingIndex = Mathf.Max(siblingIndex, anchor.GetSiblingIndex());
            }

            if (siblingIndex >= 0)
                overlay.SetSiblingIndex(Mathf.Min(siblingIndex + 1, transform.childCount - 1));
            else
                overlay.SetAsFirstSibling();
        }

        private void ClearDefeatCrackOverlay()
        {
            if (defeatCrackOverlay == null)
                return;

            Destroy(defeatCrackOverlay);
            defeatCrackOverlay = null;
        }

        private IEnumerator AnimateSelectedAura()
        {
            while (selectedAuraRoot != null && selectedAuraRoot.activeSelf)
            {
                float time = Time.unscaledTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 5.1f);
                selectedAuraImage.color = new Color(1f, 0.72f, 0.08f, Mathf.Lerp(0.26f, 0.54f, pulse));
                selectedAuraRect.localScale = Vector3.one * Mathf.Lerp(0.99f, 1.045f, pulse);

                Rect rect = selectedAuraRect.rect;
                float radiusX = Mathf.Max(54f, rect.width * 0.43f);
                float radiusY = Mathf.Max(54f, rect.height * 0.43f);
                for (int index = 0; index < selectedSparkles.Count; index++)
                {
                    Image sparkle = selectedSparkles[index];
                    if (sparkle == null)
                        continue;

                    float phase = index * 0.62f;
                    float orbit = time * (1.02f + index * 0.035f) + phase;
                    float flicker = 0.5f + 0.5f * Mathf.Sin(time * (7.4f + index * 0.33f) + phase * 2.7f);
                    RectTransform sparkleRect = sparkle.rectTransform;
                    sparkleRect.anchoredPosition = new Vector2(
                        Mathf.Cos(orbit) * radiusX,
                        Mathf.Sin(orbit * 1.12f) * radiusY);
                    sparkleRect.localRotation = Quaternion.Euler(0f, 0f, time * 165f + index * 31f);
                    sparkleRect.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.42f, flicker);

                    Color sparkleColor = Color.Lerp(Color.white, SelectedAuraGold, 0.42f);
                    sparkleColor.a = Mathf.Lerp(0.18f, 0.98f, flicker);
                    sparkle.color = sparkleColor;
                }

                yield return null;
            }

            selectedAuraCoroutine = null;
        }

        private IEnumerator AnimateTurnAura()
        {
            while (turnAuraActive)
            {
                float time = Time.unscaledTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4.2f);
                Color aura = turnAuraColor;
                aura.a = Mathf.Lerp(0.18f, 0.42f, pulse);
                turnAuraImage.color = aura;
                turnAuraRect.localScale = Vector3.one * Mathf.Lerp(0.99f, 1.018f, pulse);

                Rect rect = turnAuraRect.rect;
                float radiusX = Mathf.Max(48f, rect.width * 0.39f);
                float radiusY = Mathf.Max(48f, rect.height * 0.39f);
                for (int index = 0; index < turnSparkles.Count; index++)
                {
                    Image sparkle = turnSparkles[index];
                    float phase = index * 0.73f;
                    float orbit = time * (0.72f + index * 0.025f) + phase;
                    float flicker = 0.5f + 0.5f * Mathf.Sin(time * (5.5f + index * 0.4f) + phase * 2.1f);
                    RectTransform sparkleRect = sparkle.rectTransform;
                    sparkleRect.anchoredPosition = new Vector2(
                        Mathf.Cos(orbit) * radiusX,
                        Mathf.Sin(orbit * 1.07f) * radiusY);
                    sparkleRect.localRotation = Quaternion.Euler(0f, 0f, time * 120f + index * 37f);
                    sparkleRect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.28f, flicker);

                    Color sparkleColor = Color.Lerp(Color.white, turnAuraColor, 0.35f);
                    sparkleColor.a = Mathf.Lerp(0.22f, 0.95f, flicker);
                    sparkle.color = sparkleColor;
                }

                yield return null;
            }

            turnAuraCoroutine = null;
        }

        private IEnumerator AnimateBattleAura()
        {
            while (battleAuraActive)
            {
                float time = Time.unscaledTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2.35f);
                Color aura = battleAuraColor;
                aura.a = Mathf.Lerp(0.2f, 0.36f, pulse);
                battleAuraImage.color = aura;
                battleAuraRect.localScale = Vector3.one * Mathf.Lerp(0.992f, 1.012f, pulse);

                if (battleAuraLabel != null)
                {
                    Color labelColor = Color.Lerp(Color.white, battleAuraColor, 0.28f);
                    labelColor.a = Mathf.Lerp(0.86f, 1f, pulse);
                    battleAuraLabel.color = labelColor;
                }

                yield return null;
            }

            battleAuraCoroutine = null;
        }

        private IEnumerator AnimateTargetHintAura()
        {
            while (targetHintAuraActive)
            {
                float time = Time.unscaledTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3.1f);
                Color aura = targetHintAuraColor;
                aura.a = Mathf.Lerp(0.24f, 0.48f, pulse);
                targetHintAuraImage.color = aura;
                targetHintAuraRect.localScale = Vector3.one * Mathf.Lerp(0.995f, 1.025f, pulse);

                yield return null;
            }

            targetHintAuraCoroutine = null;
        }

        private void CreateDiceView(Font font)
        {
            diceRoot = new GameObject(
                "Card Dice",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Canvas));
            diceRoot.transform.SetParent(transform, false);

            RectTransform diceRootRect = (RectTransform)diceRoot.transform;
            diceRootRect.anchorMin = new Vector2(0.17f, 1.03f);
            diceRootRect.anchorMax = new Vector2(0.83f, 1.7f);
            diceRootRect.offsetMin = Vector2.zero;
            diceRootRect.offsetMax = Vector2.zero;

            Canvas diceCanvas = diceRoot.GetComponent<Canvas>();
            diceCanvas.overrideSorting = true;
            diceCanvas.sortingOrder = 250;

            Image background = diceRoot.GetComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = false;

            diceCaption = CreateText("Caption", diceRoot.transform, font, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            diceCaption.color = new Color(0.92f, 0.82f, 0.42f);
            SetAnchors(diceCaption.rectTransform, new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.99f));

            firstAnimatedDiceRoot = CreateAnimatedDice(
                "Animated Die",
                diceRoot.transform,
                out diceImage,
                out firstAnimatedDiceBodyImage,
                out firstDiceAnimators,
                out firstAnimatedDiceResults);
            if (firstAnimatedDiceRoot == null)
            {
                diceImage = CreateImage("Die", diceRoot.transform, Color.white);
                diceImage.preserveAspect = true;
                SetAnchors(diceImage.rectTransform, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f));
            }

            secondAnimatedDiceRoot = CreateAnimatedDice(
                "Second Animated Die",
                diceRoot.transform,
                out secondDiceImage,
                out secondAnimatedDiceBodyImage,
                out secondDiceAnimators,
                out secondAnimatedDiceResults);
            if (secondAnimatedDiceRoot == null)
            {
                secondDiceImage = CreateImage("Second Die", diceRoot.transform, Color.white);
                secondDiceImage.preserveAspect = true;
            }
            secondDiceImage.gameObject.SetActive(false);

            diceResult = CreateText("Result", diceRoot.transform, font, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            diceResult.color = Color.white;
            SetAnchors(diceResult.rectTransform, new Vector2(0.05f, 0.01f), new Vector2(0.95f, 0.2f));

            diceRoot.SetActive(false);
        }

        private static GameObject CreateAnimatedDice(
            string name,
            Transform parent,
            out Image resultImage,
            out Image bodyImage,
            out Animator[] animators,
            out Sprite[] resultSprites)
        {
            resultImage = null;
            bodyImage = null;
            animators = Array.Empty<Animator>();
            resultSprites = Array.Empty<Sprite>();
            GameObject prefab = LoadDiceWindowPrefab();
            if (prefab == null)
                return null;

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = name;
            foreach (Button button in instance.GetComponentsInChildren<Button>(true))
                button.gameObject.SetActive(false);
            foreach (Behaviour behaviour in instance.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "Dice")
                {
                    Sprite[] extractedSprites = ExtractDiceResultSprites(behaviour);
                    if (HasAnySprite(extractedSprites))
                        resultSprites = extractedSprites;
                    behaviour.enabled = false;
                }
            }
            HideAnimatedDiceContainerElements(instance.transform);

            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(0.12f, 0.18f);
            rect.anchorMax = new Vector2(0.88f, 0.82f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            bodyImage = FindImage(instance.transform, "DicePurple");
            resultImage = bodyImage
                ?? FindImage(instance.transform, "DiceResult_pur")
                ?? instance.GetComponentInChildren<Image>(true);
            if (resultImage != null)
            {
                resultImage.preserveAspect = true;
                resultImage.transform.SetAsLastSibling();
            }
            if (bodyImage != null)
            {
                bodyImage.enabled = true;
                bodyImage.preserveAspect = true;
            }

            animators = instance.GetComponentsInChildren<Animator>(true);
            return instance;
        }

        private static Sprite[] ExtractDiceResultSprites(Behaviour diceBehaviour)
        {
            Sprite[] sprites = new Sprite[20];
            Type diceType = diceBehaviour.GetType();
            for (int index = 0; index < sprites.Length; index++)
            {
                var field = diceType.GetField(
                    $"dice{index + 1}",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (field != null)
                    sprites[index] = field.GetValue(diceBehaviour) as Sprite;
            }

            return sprites;
        }

        private static bool HasAnySprite(Sprite[] sprites)
        {
            if (sprites == null)
                return false;

            foreach (Sprite sprite in sprites)
            {
                if (sprite != null)
                    return true;
            }

            return false;
        }

        private static void HideAnimatedDiceContainerElements(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Desk"
                    || child.name == "TextResult"
                    || child.name == "Text (TMP)"
                    || child.name == "ButtonZeroAlpha")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static GameObject LoadDiceWindowPrefab()
        {
            if (diceWindowPrefab == null)
                diceWindowPrefab = Resources.Load<GameObject>(DiceWindowResourcePath);
            return diceWindowPrefab;
        }

        private static Image FindImage(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName && child.TryGetComponent(out Image image))
                    return image;
            }

            return null;
        }

        private void CreateStatusIconRoot(Vector2 minimum, Vector2 maximum, float iconSize, float spacing)
        {
            statusIconSize = iconSize;
            statusIconRoot = new GameObject(
                "Status Icons",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            statusIconRoot.SetParent(transform, false);
            SetAnchors(statusIconRoot, minimum, maximum);

            HorizontalLayoutGroup layout = statusIconRoot.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.spacing = spacing;
            layout.padding = new RectOffset(0, 0, 0, 0);

            LayoutElement element = statusIconRoot.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = iconSize;
            statusIconRoot.gameObject.SetActive(false);
        }

        private void CreateStatusIcon(StatusToken status, int index)
        {
            Sprite sprite = ResolveStatusIcon(status.Label);
            GameObject icon = new GameObject(
                $"Status Icon {index + 1}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(LayoutElement));
            icon.transform.SetParent(statusIconRoot, false);

            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.sizeDelta = Vector2.one * statusIconSize;

            Image image = icon.GetComponent<Image>();
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : StatusFallbackColor(status.Color);
            image.raycastTarget = false;
            image.preserveAspect = true;

            Outline outline = icon.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);

            LayoutElement element = icon.GetComponent<LayoutElement>();
            element.preferredWidth = statusIconSize;
            element.preferredHeight = statusIconSize;
            statusIconViews.Add(icon);
        }

        private void ClearStatusIcons()
        {
            for (int index = statusIconViews.Count - 1; index >= 0; index--)
            {
                if (statusIconViews[index] != null)
                    Destroy(statusIconViews[index]);
            }

            statusIconViews.Clear();
            if (statusIconRoot != null)
                statusIconRoot.gameObject.SetActive(false);
        }

        private static Sprite ResolveStatusIcon(string status)
        {
            string iconName = StatusIconName(status);
            if (string.IsNullOrEmpty(iconName))
                return null;

            if (statusIconCache.TryGetValue(iconName, out Sprite cached))
                return cached;

            Sprite exact = Resources.Load<Sprite>($"StatusIcons/{iconName}");
            if (exact == null)
                exact = CreateSpriteFromTexture(Resources.Load<Texture2D>($"StatusIcons/{iconName}"));
            if (exact == null)
            {
                string resourceName = ExplicitStatusIconResourceName(iconName);
                if (!string.IsNullOrEmpty(resourceName))
                {
                    exact = resourceName.Contains("/")
                        ? Resources.Load<Sprite>(resourceName)
                        : Resources.Load<Sprite>($"StatusIcons/{resourceName}");
                    if (exact == null)
                    {
                        exact = resourceName.Contains("/")
                            ? CreateSpriteFromTexture(Resources.Load<Texture2D>(resourceName))
                            : CreateSpriteFromTexture(Resources.Load<Texture2D>($"StatusIcons/{resourceName}"));
                    }
                }
            }
            Sprite resolved = exact != null
                ? exact
                : FindStatusIconByKeywords(iconName);
            statusIconCache[iconName] = resolved;
            return resolved;
        }

        private static string ExplicitStatusIconResourceName(string iconName)
        {
            return iconName switch
            {
                "death" => "death_debuff",
                "aura_might" => "might_aura",
                "aura_cunning" => "cunning_aura",
                "aura_magic" => "magic_aura",
                "aura_formation" => "formation_aura",
                "aura_warrior" => "warrior_aura",
                "aura_barbarian" => "barbarian_aura",
                "aura_paladin" => "paladin_aura",
                "aura_rogue" => "rogue_aura",
                "aura_assassin" => "assassin_aura",
                "aura_hunter" => "hunter_aura",
                "aura_mage" => "mage_aura",
                "aura_necromancer" => "necromancer_aura",
                "aura_priest" => "priest_aura",
                "debuff_inhibited" => "assassin_debuff",
                "debuff_marked" => "hunter_debuff",
                "debuff_vigor_down" => "debuff_mage",
                "buff_protection" => "paladin_buff",
                "buff_blessing" => "priest_buff",
                "buff_might" => "might_buff",
                "fury_buff" => "fury_buff",
                "buff_attack" => "barbarian_buff",
                "agreement_buff" => "agreement_buff",
                "buff_attachment" => "UI/attachment_button",
                "buff_spirit" => "buff_spirit",
                "ability" => "UI/ability_button",
                _ => null
            };
        }

        private static string StatusIconName(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return string.Empty;

            string normalized = status.Trim().ToUpperInvariant();
            normalized = normalized
                .Replace("AURA FAMIGLIA ", "AURA ")
                .Replace("AURA CLASSE ", "AURA ");
            if (normalized.StartsWith("AURA +", StringComparison.Ordinal)
                || (normalized.Contains("AURA FORZUTA") && normalized.Contains("MORTE"))
                || (normalized.Contains("AURA FORTUZA") && normalized.Contains("MORTE")))
                return "buff_might";
            if (normalized.Contains("MORTE") || normalized.Contains("KO") || normalized.Contains("ELIMINAT"))
                return "death";
            if (normalized.Contains("AURA MIGHT") || normalized.Contains("AURA FORZA") || normalized.Contains("AURA FORTUZA"))
                return "aura_might";
            if (normalized.Contains("AURA CUNNING") || normalized.Contains("AURA ASTUZIA") || normalized.Contains("AURA ASTUTA"))
                return "aura_cunning";
            if (normalized.Contains("AURA MAGIC") || normalized.Contains("AURA MAGIA") || normalized.Contains("AURA MAGICA"))
                return "aura_magic";
            if (normalized.Contains("AURA FORMAZIONE"))
                return "aura_formation";
            if (normalized.Contains("AURA WARRIOR") || normalized.Contains("AURA GUERRIERO"))
                return "aura_warrior";
            if (normalized.Contains("AURA BARBAR"))
                return "aura_barbarian";
            if (normalized.Contains("AURA PALAD"))
                return "aura_paladin";
            if (normalized.Contains("AURA ROGUE") || normalized.Contains("AURA LADRO"))
                return "aura_rogue";
            if (normalized.Contains("AURA ASSASS"))
                return "aura_assassin";
            if (normalized.Contains("AURA HUNTER") || normalized.Contains("AURA CACCIATORE"))
                return "aura_hunter";
            if (normalized.Contains("AURA MAGE") || normalized.Contains("AURA MAGO"))
                return "aura_mage";
            if (normalized.Contains("AURA NECRO"))
                return "aura_necromancer";
            if (normalized.Contains("AURA PRIEST") || normalized.Contains("AURA SACERDOTE"))
                return "aura_priest";
            if (normalized.Contains("ABILITA") || normalized.Contains("ABILITÀ") || normalized.Contains("ABILITY"))
                return "ability";
            if (normalized.Contains("DIFESA DADO +1"))
                return "aura_magic";
            if (normalized.Contains("INIB"))
                return "debuff_inhibited";
            if (normalized.Contains("DADO") || normalized.Contains("VIGOR"))
                return "debuff_vigor_down";
            if (normalized.Contains("MARC") || normalized.Contains("PREDA"))
                return "debuff_marked";
            if (normalized.StartsWith("-", StringComparison.Ordinal) || normalized.Contains(" -"))
                return "debuff_power_down";
            if (normalized.Contains("PROTEZ"))
                return "buff_protection";
            if (normalized.Contains("SPIRITO"))
                return "buff_spirit";
            if (normalized.Contains("REROLL"))
                return "buff_reroll";
            if (normalized.Contains("BENED"))
                return "buff_blessing";
            if (normalized.Contains("FURIA"))
                return "fury_buff";
            if (normalized.Contains("ATTACH") || normalized.Contains("EQUIP") || normalized.Contains("EQUIPAGGIA") || normalized.Contains("LEGAME") || normalized.Contains("POTENZIA"))
                return "buff_attachment";
            if (normalized.Contains("ACCORD") || normalized.Contains("AGREEMENT") || normalized.Contains("FORZA"))
                return "agreement_buff";
            if (normalized.Contains("BONUS") || normalized.Contains("FURIA"))
                return "buff_attack";

            return string.Empty;
        }

        private static Sprite FindStatusIconByKeywords(string iconName)
        {
            statusIconSprites ??= Resources.LoadAll<Sprite>("StatusIcons");
            statusIconTextures ??= Resources.LoadAll<Texture2D>("StatusIcons");
            if ((statusIconSprites == null || statusIconSprites.Length == 0)
                && (statusIconTextures == null || statusIconTextures.Length == 0))
                return null;

            string[] keywords = StatusIconKeywords(iconName);
            Sprite best = null;
            int bestScore = 0;
            if (statusIconSprites != null)
            {
                foreach (Sprite sprite in statusIconSprites)
                {
                    if (sprite == null)
                        continue;

                    int score = ScoreIconName(sprite.name, keywords);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = sprite;
                    }
                }
            }

            if (statusIconTextures != null)
            {
                foreach (Texture2D texture in statusIconTextures)
                {
                    if (texture == null)
                        continue;

                    int score = ScoreIconName(texture.name, keywords);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = CreateSpriteFromTexture(texture);
                    }
                }
            }

            return bestScore >= 2 ? best : null;
        }

        private static int ScoreIconName(string candidateName, IReadOnlyList<string> keywords)
        {
            string spriteName = NormalizeIconText(candidateName);
            int score = 0;
            for (int index = 0; index < keywords.Count; index++)
            {
                if (spriteName.Contains(keywords[index], StringComparison.Ordinal))
                    score += index == 0 ? 5 : 2;
            }

            return score;
        }

        private static Sprite CreateSpriteFromTexture(Texture2D texture)
        {
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static string[] StatusIconKeywords(string iconName)
        {
            return iconName switch
            {
                "death" => new[] { "death", "morte", "morta", "morto", "ko", "dead", "skull", "teschio", "elimin" },
                "aura_might" => new[] { "might", "fortuza", "forza", "potenza", "aura" },
                "aura_cunning" => new[] { "cunning", "astuta", "astuzia", "furbizia", "aura" },
                "aura_magic" => new[] { "magic", "magica", "magia", "magico", "aura" },
                "aura_formation" => new[] { "formation", "formazione", "trio", "aura" },
                "aura_warrior" => new[] { "warrior", "guerriero", "guerra", "aura" },
                "aura_barbarian" => new[] { "barbarian", "barbaro", "furia", "aura" },
                "aura_paladin" => new[] { "paladin", "paladino", "scudo", "aura" },
                "aura_rogue" => new[] { "rogue", "ladro", "aura" },
                "aura_assassin" => new[] { "assassin", "assassino", "ombra", "aura" },
                "aura_hunter" => new[] { "hunter", "cacciatore", "preda", "aura" },
                "aura_mage" => new[] { "mage", "mago", "magic", "aura" },
                "aura_necromancer" => new[] { "necromancer", "negromante", "necro", "spirito", "aura" },
                "aura_priest" => new[] { "priest", "sacerdote", "benedizione", "aura" },
                "debuff_inhibited" => new[] { "assassin", "assassino", "inib", "stun", "stop", "silenzio", "debuff" },
                "debuff_marked" => new[] { "hunter", "cacciatore", "marc", "preda", "target", "bersaglio", "mark", "debuff" },
                "debuff_power_down" => new[] { "down", "meno", "malus", "weak", "debole", "debuff" },
                "debuff_vigor_down" => new[] { "mage", "mago", "dado", "vigor", "vigore", "dice", "debuff" },
                "fury_buff" => new[] { "fury", "furia", "barbarian", "barbaro", "rage", "buff" },
                "buff_attack" => new[] { "fury", "barbarian", "barbaro", "attack", "attacco", "bonus", "furia", "rage", "buff" },
                "agreement_buff" => new[] { "agreement", "accord", "legame", "attach", "attachment", "buff" },
                "buff_attachment" => new[] { "attach", "attachment", "legame", "equip", "buff" },
                "buff_blessing" => new[] { "priest", "sacerdote", "bened", "bless", "sacro", "buff" },
                "buff_protection" => new[] { "paladin", "paladino", "protez", "shield", "scudo", "guardia", "buff" },
                "buff_reroll" => new[] { "reroll", "rilancio", "dado", "dice", "buff" },
                "buff_spirit" => new[] { "spirit", "spirito", "ghost", "necro", "buff" },
                _ => new[] { NormalizeIconText(iconName) }
            };
        }

        private static string NormalizeIconText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text
                .Trim()
                .ToLowerInvariant()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);
        }

        private static Color StatusFallbackColor(Color color)
        {
            Color fallback = color;
            fallback.a = 0.92f;
            return fallback;
        }

        private RectTransform EnsureActionOverlay()
        {
            if (actionOverlayRoot != null)
            {
                actionOverlayRoot.gameObject.SetActive(true);
                actionOverlayRoot.SetAsLastSibling();
                return actionOverlayRoot;
            }

            var root = new GameObject(
                "Card Action Overlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup),
                typeof(LayoutElement),
                typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            actionOverlayRoot = (RectTransform)root.transform;
            SetActionOverlayBounds(new Vector2(0.05f, 0.96f), new Vector2(0.95f, 1.42f));

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 330;

            CanvasGroup overlayGroup = root.GetComponent<CanvasGroup>();
            overlayGroup.ignoreParentGroups = true;
            overlayGroup.interactable = true;
            overlayGroup.blocksRaycasts = true;

            return actionOverlayRoot;
        }

        private void SetActionOverlayBounds(Vector2 anchorMin, Vector2 anchorMax)
        {
            actionOverlayRoot.anchorMin = anchorMin;
            actionOverlayRoot.anchorMax = anchorMax;
            actionOverlayRoot.offsetMin = Vector2.zero;
            actionOverlayRoot.offsetMax = Vector2.zero;
        }

        private static Button CreateIconActionButton(
            string name,
            Transform parent,
            Sprite sprite,
            string label = null,
            Color? labelColor = null)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(Shadow));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;

            Shadow shadow = buttonObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(4f, -4f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            if (!string.IsNullOrWhiteSpace(label))
                CreateActionButtonLabel(buttonObject.transform, label, labelColor ?? Color.white);
            return button;
        }

        private static void CreateActionButtonLabel(Transform parent, string label, Color labelColor)
        {
            Font font = GetActionLabelFont();
            Text text = CreateText("Action Label", parent, font, 17, FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = labelColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 17;

            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = ActionLabelOutline;
            outline.effectDistance = new Vector2(1.25f, -1.25f);

            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
            shadow.effectDistance = new Vector2(1.8f, -1.8f);

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(-0.08f, 0.9f);
            rect.anchorMax = new Vector2(1.08f, 1.24f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetActionButtonLabelAnchors(Button button, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (button == null)
                return;

            Text label = button.GetComponentInChildren<Text>();
            if (label == null)
                return;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font GetActionLabelFont()
        {
            if (actionLabelFont != null)
                return actionLabelFont;

            actionLabelFont = MmoUiTheme.TitleFont;
            return actionLabelFont;
        }

        private static string GetActionLabelForSprite(Sprite sprite)
        {
            string spriteName = sprite == null ? string.Empty : sprite.name;
            if (spriteName.IndexOf("ability_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Abilit\u00E0";
            if (spriteName.IndexOf("attachment_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Equipaggia";
            if (spriteName.IndexOf("cancel_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Cancella";
            if (spriteName.IndexOf("confirm_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Conferma";
            if (spriteName.IndexOf("attack_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Attacca";

            return null;
        }

        private static Color GetActionLabelColorForSprite(Sprite sprite)
        {
            string spriteName = sprite == null ? string.Empty : sprite.name;
            if (spriteName.IndexOf("ability_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return ActionLabelBlue;
            if (spriteName.IndexOf("attachment_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return ActionLabelOrange;
            if (spriteName.IndexOf("confirm_button", StringComparison.OrdinalIgnoreCase) >= 0)
                return ActionLabelGreen;

            return ActionLabelRed;
        }

        private static Button CreateTransparentActionButton(string name, Transform parent)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            return button;
        }

        private IEnumerator PlayDiceRollRoutine(
            DiceSpriteCatalog catalog,
            int visualSides,
            int displaySides,
            int firstResult,
            int secondResult,
            bool hasSecondResult,
            int selectedResult,
            VigorSelectionMode selectionMode,
            int firstBeforeReroll,
            int secondBeforeReroll,
            string caption,
            float rollDuration,
            float resultHold,
            bool use3DDice)
        {
            diceRoot.SetActive(true);
            diceCaption.text = NormalizeDiceCaption(caption);
            diceResult.text = string.Empty;
            diceImage.rectTransform.localScale = Vector3.one;
            secondDiceImage.rectTransform.localScale = Vector3.one;
            ConfigureDiceLayout(hasSecondResult);

            bool uses3DDice = use3DDice && Dice3DRollView.IsSupported(visualSides);
            bool usesAnimatedDice = !uses3DDice && firstAnimatedDiceRoot != null;
            bool firstRerolls = firstBeforeReroll > 0;
            bool secondRerolls = hasSecondResult && secondBeforeReroll > 0;
            bool hasReroll = firstRerolls || secondRerolls;
            int firstInitialResult = firstRerolls ? firstBeforeReroll : firstResult;
            int secondInitialResult = secondRerolls ? secondBeforeReroll : secondResult;
            if (uses3DDice)
            {
                // Dado 3D scriptato: il risultato è già noto, il dado rotola e
                // decelera fino a fermarsi sulla faccia giusta.
                SetDiceVisible(firstAnimatedDiceRoot, diceImage, false);
                SetDiceVisible(secondAnimatedDiceRoot, secondDiceImage, false);
                BeginScreenDiceRollLayout();
                ConfigureScreenDiceTextLayout();
                EnsureDice3DViews(hasSecondResult, visualSides);
                firstDice3D.StartScriptedRoll(visualSides, cardHeroClass, firstInitialResult, rollDuration);
                if (hasSecondResult)
                    secondDice3D.StartScriptedRoll(visualSides, cardHeroClass, secondInitialResult, rollDuration);
                yield return new WaitForSecondsRealtime(rollDuration);
            }
            else if (usesAnimatedDice)
            {
                SetDiceBodyVisible(firstAnimatedDiceBodyImage, true);
                SetDiceBodyVisible(secondAnimatedDiceBodyImage, hasSecondResult);
                SetDiceResultVisible(diceImage, firstAnimatedDiceBodyImage, false);
                SetDiceResultVisible(secondDiceImage, secondAnimatedDiceBodyImage, false);
                RandomizeAnimatedDiceMotion(firstAnimatedDiceRoot, firstDiceAnimators);
                if (hasSecondResult)
                    RandomizeAnimatedDiceMotion(secondAnimatedDiceRoot, secondDiceAnimators);
                PlayDiceAnimators(firstDiceAnimators);
                if (hasSecondResult)
                    PlayDiceAnimators(secondDiceAnimators);
                yield return new WaitForSecondsRealtime(Mathf.Max(rollDuration, AnimatedDiceMinimumRollDuration));
                StopDiceAnimators(firstDiceAnimators);
                if (hasSecondResult)
                    StopDiceAnimators(secondDiceAnimators);
            }
            else
            {
                Sprite[] frames = catalog != null ? catalog.FindFrames(visualSides) : Array.Empty<Sprite>();
                if (frames.Length > 0)
                {
                    float frameDuration = rollDuration / frames.Length;
                    for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                    {
                        diceImage.sprite = frames[frameIndex];
                        if (hasSecondResult)
                            secondDiceImage.sprite = frames[(frameIndex + frames.Length / 2) % frames.Length];
                        diceImage.rectTransform.localScale = Vector3.one * (frameIndex % 2 == 0 ? 0.94f : 1.04f);
                        secondDiceImage.rectTransform.localScale = Vector3.one * (frameIndex % 2 == 0 ? 1.04f : 0.94f);
                        yield return new WaitForSecondsRealtime(frameDuration);
                    }
                }
                else
                {
                    yield return new WaitForSecondsRealtime(rollDuration);
                }
            }

            Sprite resultSprite = usesAnimatedDice
                ? FindAnimatedResult(firstAnimatedDiceResults, firstInitialResult)
                : catalog != null ? catalog.FindResult(visualSides, firstInitialResult) : null;
            if (resultSprite != null)
                diceImage.sprite = resultSprite;
            if (usesAnimatedDice)
            {
                SetDiceResultSprite(diceImage, firstAnimatedDiceBodyImage, resultSprite);
                SetDiceResultVisible(diceImage, firstAnimatedDiceBodyImage, true);
            }
            if (hasSecondResult)
            {
                Sprite secondResultSprite = usesAnimatedDice
                    ? FindAnimatedResult(secondAnimatedDiceResults, secondInitialResult)
                    : catalog != null ? catalog.FindResult(visualSides, secondInitialResult) : null;
                if (secondResultSprite != null)
                    secondDiceImage.sprite = secondResultSprite;
                if (usesAnimatedDice)
                {
                    SetDiceResultSprite(secondDiceImage, secondAnimatedDiceBodyImage, secondResultSprite);
                    SetDiceResultVisible(secondDiceImage, secondAnimatedDiceBodyImage, true);
                }
            }
            diceImage.rectTransform.localScale = Vector3.one;
            secondDiceImage.rectTransform.localScale = Vector3.one;

            if (hasReroll)
            {
                yield return new WaitForSecondsRealtime(0.32f);

                diceCaption.text = NormalizeDiceCaption(caption);
                float rerollDuration = Mathf.Max(0.45f, rollDuration * 0.66f);

                if (uses3DDice)
                {
                    if (firstRerolls)
                        firstDice3D.StartScriptedRoll(visualSides, cardHeroClass, firstResult, rerollDuration);
                    if (secondRerolls && secondDice3D != null)
                        secondDice3D.StartScriptedRoll(visualSides, cardHeroClass, secondResult, rerollDuration);
                    yield return new WaitForSecondsRealtime(rerollDuration);
                }
                else if (usesAnimatedDice)
                {
                    if (firstRerolls)
                    {
                        SetDiceBodyVisible(firstAnimatedDiceBodyImage, true);
                        SetDiceResultVisible(diceImage, firstAnimatedDiceBodyImage, false);
                        RandomizeAnimatedDiceMotion(firstAnimatedDiceRoot, firstDiceAnimators);
                        PlayDiceAnimators(firstDiceAnimators);
                    }
                    if (secondRerolls)
                    {
                        SetDiceBodyVisible(secondAnimatedDiceBodyImage, true);
                        SetDiceResultVisible(secondDiceImage, secondAnimatedDiceBodyImage, false);
                        RandomizeAnimatedDiceMotion(secondAnimatedDiceRoot, secondDiceAnimators);
                        PlayDiceAnimators(secondDiceAnimators);
                    }
                    yield return new WaitForSecondsRealtime(Mathf.Max(rerollDuration, AnimatedDiceMinimumRollDuration * 0.72f));
                    if (firstRerolls)
                        StopDiceAnimators(firstDiceAnimators);
                    if (secondRerolls)
                        StopDiceAnimators(secondDiceAnimators);
                }
                else
                {
                    Sprite[] frames = catalog != null ? catalog.FindFrames(visualSides) : Array.Empty<Sprite>();
                    if (frames.Length > 0)
                    {
                        float frameDuration = rerollDuration / frames.Length;
                        for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                        {
                            if (firstRerolls)
                            {
                                diceImage.sprite = frames[frameIndex];
                                diceImage.rectTransform.localScale = Vector3.one * (frameIndex % 2 == 0 ? 0.94f : 1.04f);
                            }
                            if (secondRerolls)
                            {
                                secondDiceImage.sprite = frames[(frameIndex + frames.Length / 2) % frames.Length];
                                secondDiceImage.rectTransform.localScale = Vector3.one * (frameIndex % 2 == 0 ? 1.04f : 0.94f);
                            }
                            yield return new WaitForSecondsRealtime(frameDuration);
                        }
                    }
                    else
                    {
                        yield return new WaitForSecondsRealtime(rerollDuration);
                    }
                }

                resultSprite = usesAnimatedDice
                    ? FindAnimatedResult(firstAnimatedDiceResults, firstResult)
                    : catalog != null ? catalog.FindResult(visualSides, firstResult) : null;
                if (resultSprite != null)
                    diceImage.sprite = resultSprite;
                if (usesAnimatedDice)
                {
                    SetDiceResultSprite(diceImage, firstAnimatedDiceBodyImage, resultSprite);
                    SetDiceResultVisible(diceImage, firstAnimatedDiceBodyImage, true);
                }
                if (hasSecondResult)
                {
                    Sprite secondResultSprite = usesAnimatedDice
                        ? FindAnimatedResult(secondAnimatedDiceResults, secondResult)
                        : catalog != null ? catalog.FindResult(visualSides, secondResult) : null;
                    if (secondResultSprite != null)
                        secondDiceImage.sprite = secondResultSprite;
                    if (usesAnimatedDice)
                    {
                        SetDiceResultSprite(secondDiceImage, secondAnimatedDiceBodyImage, secondResultSprite);
                        SetDiceResultVisible(secondDiceImage, secondAnimatedDiceBodyImage, true);
                    }
                }
                diceImage.rectTransform.localScale = Vector3.one;
                secondDiceImage.rectTransform.localScale = Vector3.one;
            }

            diceResult.text = string.Empty;
            yield return new WaitForSecondsRealtime(usesAnimatedDice
                ? Mathf.Max(resultHold, AnimatedDiceMinimumResultHold)
                : resultHold);

            diceRoot.SetActive(false);
            if (uses3DDice)
            {
                firstDice3D.Hide();
                if (secondDice3D != null)
                    secondDice3D.Hide();
                EndScreenDiceRollLayout();
            }
            if (usesAnimatedDice)
            {
                SetDiceResultVisible(diceImage, firstAnimatedDiceBodyImage, false);
                SetDiceResultVisible(secondDiceImage, secondAnimatedDiceBodyImage, false);
                SetDiceBodyVisible(firstAnimatedDiceBodyImage, true);
                SetDiceBodyVisible(secondAnimatedDiceBodyImage, true);
            }
            diceCoroutine = null;
        }

        private static void PlayDiceAnimators(Animator[] animators)
        {
            foreach (Animator animator in animators)
            {
                if (animator == null)
                    continue;

                animator.enabled = true;
                animator.ResetTrigger(DiceRollTrigger);
                animator.SetTrigger(DiceRollTrigger);
            }
        }

        private static void StopDiceAnimators(Animator[] animators)
        {
            foreach (Animator animator in animators)
            {
                if (animator != null)
                    animator.enabled = false;
            }
        }

        private static void RandomizeAnimatedDiceMotion(GameObject diceRoot, Animator[] animators)
        {
            if (diceRoot != null && diceRoot.transform is RectTransform rect)
            {
                rect.anchoredPosition = new Vector2(
                    UnityEngine.Random.Range(-AnimatedDicePositionJitter, AnimatedDicePositionJitter),
                    UnityEngine.Random.Range(-AnimatedDicePositionJitter, AnimatedDicePositionJitter));
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    UnityEngine.Random.Range(-AnimatedDiceRotationJitter, AnimatedDiceRotationJitter));
                rect.localScale = Vector3.one * UnityEngine.Random.Range(0.94f, 1.08f);
            }

            foreach (Animator animator in animators)
            {
                if (animator != null)
                    animator.speed = UnityEngine.Random.Range(AnimatedDiceMinimumSpeed, AnimatedDiceMaximumSpeed);
            }
        }

        private static Sprite FindAnimatedResult(Sprite[] sprites, int result)
        {
            int index = result - 1;
            return index >= 0 && index < sprites.Length ? sprites[index] : null;
        }

        private static int ResolveVisualDieSides(DiceSpriteCatalog catalog, int logicalSides)
        {
            if (catalog != null && catalog.FindFrames(logicalSides).Length > 0)
                return logicalSides;
            return logicalSides == 3 ? 6 : logicalSides;
        }

        private void EnsureDice3DViews(bool showTwoDice, int visualSides)
        {
            if (firstDice3DArea == null)
            {
                firstDice3DArea = new GameObject("Die 3D Area", typeof(RectTransform)).GetComponent<RectTransform>();
                firstDice3DArea.SetParent(diceRoot.transform, false);
                firstDice3D = Dice3DRollView.Create(firstDice3DArea);
            }
            if (showTwoDice && secondDice3DArea == null)
            {
                secondDice3DArea = new GameObject("Second Die 3D Area", typeof(RectTransform)).GetComponent<RectTransform>();
                secondDice3DArea.SetParent(diceRoot.transform, false);
                secondDice3D = Dice3DRollView.Create(secondDice3DArea);
            }


            // Punto di riposo vicino alla label del tiro: ATTACCANTE nella fascia
            // bassa, DIFENSORE nella fascia alta.
            float diceCenterX = Mathf.Clamp(screenDiceCardCenterX, 0.16f, 0.84f);
            float diceAreaScale = visualSides == 4 ? D4ScreenDiceAreaScale : 1f;
            float diceWidth = ScreenDiceAreaWidth * diceAreaScale;
            if (showTwoDice)
            {
                float gap = 0.02f;
                float groupWidth = diceWidth * 2f + gap;
                float groupMinX = ClampDiceGroupMin(diceCenterX, groupWidth);
                float diceMinY = diceRootOnLowerScreenHalf ? 0.1f : 0.58f;
                float diceMaxY = diceRootOnLowerScreenHalf ? 0.48f : 0.96f;
                SetAnchors(firstDice3DArea, new Vector2(groupMinX, diceMinY), new Vector2(groupMinX + diceWidth, diceMaxY));
                SetAnchors(secondDice3DArea, new Vector2(groupMinX + diceWidth + gap, diceMinY), new Vector2(groupMinX + groupWidth, diceMaxY));
            }
            else
            {
                float diceMinX = Mathf.Clamp(diceCenterX - diceWidth * 0.5f, 0.04f, 0.96f - diceWidth);
                float diceMinY = diceRootOnLowerScreenHalf ? 0.1f : 0.58f;
                float diceMaxY = diceRootOnLowerScreenHalf ? 0.46f : 0.94f;
                SetAnchors(firstDice3DArea, new Vector2(diceMinX, diceMinY), new Vector2(diceMinX + diceWidth, diceMaxY));
                if (secondDice3D != null)
                    secondDice3D.Hide();
            }

            // I dadi rimbalzano sulle pareti dell'intera metà campo (diceRoot,
            // ancorato alla metà schermo di chi tira) e si urtano tra loro.
            RectTransform bounceArea = screenDiceBounceArea != null
                ? screenDiceBounceArea
                : (RectTransform)diceRoot.transform;
            firstDice3D.SetBounceArea(bounceArea, showTwoDice ? secondDice3D : null);
            if (secondDice3D != null)
                secondDice3D.SetBounceArea(bounceArea, showTwoDice ? firstDice3D : null);
        }

        private static float ClampDiceGroupMin(float centerX, float groupWidth)
        {
            float centered = centerX - groupWidth * 0.5f;
            if (groupWidth >= 0.96f)
                return centered;
            return Mathf.Clamp(centered, 0.02f, 0.98f - groupWidth);
        }

        private void BeginScreenDiceRollLayout()
        {
            if (diceRootDetachedToScreen || diceRoot == null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            if (rootCanvas == null || rootCanvas.transform == null)
                return;

            RectTransform diceRootRect = (RectTransform)diceRoot.transform;
            diceRootHomeParent = diceRootRect.parent;
            diceRootHomeSiblingIndex = diceRootRect.GetSiblingIndex();
            diceRootHomeAnchorMin = diceRootRect.anchorMin;
            diceRootHomeAnchorMax = diceRootRect.anchorMax;
            diceRootHomeOffsetMin = diceRootRect.offsetMin;
            diceRootHomeOffsetMax = diceRootRect.offsetMax;
            diceRootHomeScale = diceRootRect.localScale;
            diceRootHomeRotation = diceRootRect.localRotation;

            diceRootRect.SetParent(rootCanvas.transform, false);
            diceRootRect.SetAsLastSibling();
            diceRootRect.localScale = Vector3.one;
            diceRootRect.localRotation = Quaternion.identity;

            BuildDiceRootAnchorsNearCard(rootCanvas, out Vector2 minimum, out Vector2 maximum);
            SetAnchors(diceRootRect, minimum, maximum);
            ConfigureScreenDiceBounceArea(rootCanvas, diceRootOnLowerScreenHalf);
            diceRootDetachedToScreen = true;
        }

        private void EndScreenDiceRollLayout()
        {
            if (!diceRootDetachedToScreen || diceRoot == null || diceRootHomeParent == null)
                return;

            RectTransform diceRootRect = (RectTransform)diceRoot.transform;
            diceRootRect.SetParent(diceRootHomeParent, false);
            diceRootRect.SetSiblingIndex(Mathf.Clamp(diceRootHomeSiblingIndex, 0, diceRootHomeParent.childCount - 1));
            diceRootRect.anchorMin = diceRootHomeAnchorMin;
            diceRootRect.anchorMax = diceRootHomeAnchorMax;
            diceRootRect.offsetMin = diceRootHomeOffsetMin;
            diceRootRect.offsetMax = diceRootHomeOffsetMax;
            diceRootRect.localScale = diceRootHomeScale;
            diceRootRect.localRotation = diceRootHomeRotation;
            diceRootDetachedToScreen = false;
            if (screenDiceBounceArea != null)
            {
                Destroy(screenDiceBounceArea.gameObject);
                screenDiceBounceArea = null;
            }
            SetAnchors(diceCaption.rectTransform, new Vector2(0.03f, 0.82f), new Vector2(0.97f, 0.99f));
            SetAnchors(diceResult.rectTransform, new Vector2(0.05f, 0.01f), new Vector2(0.95f, 0.2f));
        }

        private bool IsCardInLowerScreenHalf(Canvas rootCanvas)
        {
            Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                camera,
                rectTransform.TransformPoint(rectTransform.rect.center));
            return screenPoint.y < Screen.height * 0.5f;
        }

        private void BuildDiceRootAnchorsNearCard(
            Canvas rootCanvas,
            out Vector2 minimum,
            out Vector2 maximum)
        {
            Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int index = 0; index < corners.Length; index++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);
            float centerX = Mathf.Clamp01(((minX + maxX) * 0.5f) / screenWidth);
            float cardTop = Mathf.Clamp01(maxY / screenHeight);
            float cardBottom = Mathf.Clamp01(minY / screenHeight);
            float cardWidth = Mathf.Clamp01((maxX - minX) / screenWidth);

            float width = Mathf.Clamp(cardWidth * 2.1f, 0.34f, 0.48f);
            float height = 0.34f;
            float gap = 0.004f;
            bool canPlaceAbove = cardTop + gap + height <= 0.985f;
            bool canPlaceBelow = cardBottom - gap - height >= 0.015f;
            bool placeAbove = canPlaceAbove || !canPlaceBelow;
            diceRootOnLowerScreenHalf = placeAbove;
            float yMin = placeAbove
                ? Mathf.Min(cardTop + gap, 0.985f - height)
                : Mathf.Max(0.015f, cardBottom - gap - height);
            float xMin = Mathf.Clamp(centerX - width * 0.5f, 0.015f, 0.985f - width);
            screenDiceCardCenterX = Mathf.Clamp((centerX - xMin) / width, 0.12f, 0.88f);

            minimum = new Vector2(xMin, yMin);
            maximum = new Vector2(xMin + width, yMin + height);
        }

        private void ConfigureScreenDiceBounceArea(Canvas rootCanvas, bool cardIsInLowerHalf)
        {
            if (rootCanvas == null || rootCanvas.transform == null)
                return;

            if (screenDiceBounceArea == null)
            {
                screenDiceBounceArea = new GameObject("Screen Dice Bounce Area", typeof(RectTransform)).GetComponent<RectTransform>();
                screenDiceBounceArea.SetParent(rootCanvas.transform, false);
                screenDiceBounceArea.SetAsLastSibling();
            }

            Camera camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                camera,
                rectTransform.TransformPoint(rectTransform.rect.center));
            float screenWidth = Mathf.Max(1f, Screen.width);
            float centerX = Mathf.Clamp01(screenPoint.x / screenWidth);
            float width = 0.56f;
            float xMin = Mathf.Clamp(centerX - width * 0.5f, 0.015f, 0.985f - width);
            Vector2 minimum = cardIsInLowerHalf
                ? new Vector2(xMin, 0.02f)
                : new Vector2(xMin, 0.51f);
            Vector2 maximum = cardIsInLowerHalf
                ? new Vector2(xMin + width, 0.49f)
                : new Vector2(xMin + width, 0.98f);
            SetAnchors(screenDiceBounceArea, minimum, maximum);
        }

        private void ConfigureDiceLayout(bool showTwoDice)
        {
            SetDiceVisible(firstAnimatedDiceRoot, diceImage, true);
            SetDiceVisible(secondAnimatedDiceRoot, secondDiceImage, showTwoDice);
            if (showTwoDice)
            {
                SetDiceAnchors(firstAnimatedDiceRoot, diceImage, new Vector2(0.01f, 0.2f), new Vector2(0.5f, 0.82f));
                SetDiceAnchors(secondAnimatedDiceRoot, secondDiceImage, new Vector2(0.5f, 0.2f), new Vector2(0.99f, 0.82f));
            }
            else
            {
                SetDiceAnchors(firstAnimatedDiceRoot, diceImage, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f));
            }
        }

        private void ConfigureScreenDiceTextLayout()
        {
            float labelHalfWidth = 0.22f;
            float labelMinX = Mathf.Clamp(screenDiceCardCenterX - labelHalfWidth, 0.02f, 0.98f - labelHalfWidth * 2f);
            float labelMaxX = labelMinX + labelHalfWidth * 2f;
            if (diceRootOnLowerScreenHalf)
                SetAnchors(diceCaption.rectTransform, new Vector2(labelMinX, 0f), new Vector2(labelMaxX, 0.1f));
            else
                SetAnchors(diceCaption.rectTransform, new Vector2(labelMinX, 0.84f), new Vector2(labelMaxX, 0.94f));
            SetAnchors(diceResult.rectTransform, new Vector2(0.05f, 0.01f), new Vector2(0.95f, 0.2f));
        }

        private static string NormalizeDiceCaption(string caption)
        {
            string normalized = string.IsNullOrWhiteSpace(caption) ? string.Empty : caption.ToUpperInvariant();
            if (normalized.Contains("DIFESA"))
                return "DIFENSORE";
            if (normalized.Contains("ATTACCO"))
                return "ATTACCANTE";
            return caption;
        }

        private static void SetDiceVisible(GameObject animatedRoot, Image image, bool visible)
        {
            if (animatedRoot != null)
                animatedRoot.SetActive(visible);
            if (image != null)
                image.gameObject.SetActive(visible);
        }

        private static void SetDiceResultSprite(Image resultImage, Image bodyImage, Sprite sprite)
        {
            Image target = bodyImage != null ? bodyImage : resultImage;
            if (target == null || sprite == null)
                return;

            target.sprite = sprite;
            target.enabled = true;
        }

        private static void SetDiceResultVisible(Image image, Image bodyImage, bool visible)
        {
            if (image == null)
                return;

            if (image == bodyImage)
            {
                image.enabled = true;
                return;
            }

            image.enabled = visible && image.sprite != null;
            if (!visible)
                image.sprite = null;
        }

        private static void SetDiceBodyVisible(Image image, bool visible)
        {
            if (image != null)
                image.enabled = visible;
        }

        private static void SetDiceAnchors(GameObject animatedRoot, Image image, Vector2 minimum, Vector2 maximum)
        {
            RectTransform target = animatedRoot != null
                ? (RectTransform)animatedRoot.transform
                : image.rectTransform;
            SetAnchors(target, minimum, maximum);
        }

        private IEnumerator ScaleOverTime(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                rectTransform.localScale = Vector3.LerpUnclamped(from, to, progress);
                yield return null;
            }

            rectTransform.localScale = to;
        }

        private static Color ClassColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => new Color(0.18f, 0.55f, 0.34f),
                HeroClass.Warrior => new Color(0.24f, 0.3f, 0.38f),
                HeroClass.Mage => new Color(0.26f, 0.48f, 0.82f),
                HeroClass.Paladin => new Color(0.9f, 0.68f, 0.18f),
                HeroClass.Rogue => new Color(0.2f, 0.2f, 0.2f),
                HeroClass.Hunter => new Color(0.48f, 0.34f, 0.15f),
                HeroClass.Barbarian => new Color(0.46f, 0.24f, 0.12f),
                HeroClass.Necromancer => new Color(0.12f, 0.42f, 0.24f),
                HeroClass.Priest => new Color(0.82f, 0.86f, 0.94f),
                _ => Color.gray
            };
        }

        private static Color DeathBurstColor(HeroClass? heroClass)
        {
            if (!heroClass.HasValue)
                return new Color(0.82f, 0.84f, 0.88f, 1f);

            return heroClass.Value switch
            {
                HeroClass.Assassin => new Color(1f, 0.08f, 0.04f, 1f),
                HeroClass.Warrior => new Color(0.78f, 0.82f, 0.86f, 1f),
                HeroClass.Mage => new Color(0.18f, 0.68f, 1f, 1f),
                HeroClass.Paladin => new Color(1f, 0.82f, 0.22f, 1f),
                HeroClass.Rogue => new Color(0.05f, 0.05f, 0.05f, 1f),
                HeroClass.Hunter => new Color(1f, 0.42f, 0.02f, 1f),
                HeroClass.Barbarian => new Color(1f, 0.22f, 0.02f, 1f),
                HeroClass.Necromancer => new Color(0.42f, 1f, 0.12f, 1f),
                HeroClass.Priest => new Color(1f, 0.96f, 0.72f, 1f),
                _ => new Color(0.82f, 0.84f, 0.88f, 1f)
            };
        }

        private static Color DescriptionColor(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => new Color(0.96f, 0.91f, 0.78f),
                HeroClass.Rogue => new Color(0.98f, 0.95f, 0.88f),
                HeroClass.Hunter => new Color(1f, 0.86f, 0.44f),
                HeroClass.Barbarian => new Color(1f, 0.78f, 0.56f),
                HeroClass.Necromancer => new Color(0.72f, 1f, 0.72f),
                HeroClass.Priest => new Color(0.13f, 0.1f, 0.28f),
                HeroClass.Warrior => new Color(0.92f, 0.9f, 0.84f),
                HeroClass.Mage => new Color(0.9f, 0.96f, 1f),
                HeroClass.Paladin => new Color(0.18f, 0.11f, 0.03f),
                _ => new Color(0.12f, 0.09f, 0.06f)
            };
        }

        private static int DescriptionFontSize(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Paladin => 16,
                _ => 18
            };
        }

        private static int DescriptionMaxFontSize(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Paladin => 15,
                _ => 17
            };
        }

        private static (Vector2 minimum, Vector2 maximum) DescriptionAnchors(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Priest => (new Vector2(0.16f, 0.1f), new Vector2(0.84f, 0.3f)),
                _ => (new Vector2(0.16f, 0.115f), new Vector2(0.84f, 0.315f))
            };
        }

        private static (Vector2 minimum, Vector2 maximum) ArtworkViewportAnchors(HeroClass heroClass)
        {
            const float left = 0.135f;
            const float right = 0.865f;
            const float bottom = 0.325f;
            const float top = 0.805f;
            if (heroClass == HeroClass.Priest)
            {
                return (
                    new Vector2(left, bottom - 0.01f),
                    new Vector2(right, top + 0.01f));
            }

            float verticalOffset = heroClass switch
            {
                HeroClass.Warrior or HeroClass.Mage or HeroClass.Hunter or HeroClass.Rogue => 0.02f,
                HeroClass.Assassin or HeroClass.Barbarian or HeroClass.Necromancer or HeroClass.Paladin => 0.01f,
                _ => 0f
            };

            return (
                new Vector2(left, bottom + verticalOffset),
                new Vector2(right, top + verticalOffset));
        }

        private static float ArtworkCropVerticalAlignment(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Priest => 0.68f,
                _ => 1f
            };
        }

        private static void ApplyDescriptionReadability(Text text, HeroClass heroClass)
        {
            if (text == null)
                return;

            Color outlineColor = DescriptionOutlineColor(heroClass, text.color);
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(1.35f, -1.35f);
            outline.useGraphicAlpha = true;

            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, Mathf.Min(outlineColor.a, 0.72f));
            shadow.effectDistance = new Vector2(0.9f, -0.9f);
            shadow.useGraphicAlpha = true;
        }

        private static Color DescriptionOutlineColor(HeroClass heroClass, Color textColor)
        {
            return heroClass switch
            {
                HeroClass.Priest or HeroClass.Paladin => new Color(1f, 0.88f, 0.42f, 0.88f),
                _ => IsLightColor(textColor)
                    ? new Color(0f, 0f, 0f, 0.86f)
                    : new Color(1f, 0.91f, 0.62f, 0.86f)
            };
        }

        private static bool IsLightColor(Color color)
        {
            return (color.r * 0.299f) + (color.g * 0.587f) + (color.b * 0.114f) > 0.55f;
        }

        private static string BorderResourceName(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => "card_border_assassin",
                HeroClass.Warrior => "card_border_warrior",
                HeroClass.Mage => "card_border_mage",
                HeroClass.Paladin => "card_border_paladin",
                HeroClass.Rogue => "card_border_rogue",
                HeroClass.Hunter => "card_border_hunter",
                HeroClass.Barbarian => "card_border_barbarian",
                HeroClass.Necromancer => "card_border_necromancer",
                HeroClass.Priest => "card_border_priest",
                _ => "card_border_warrior"
            };
        }

        private static Sprite ResolveBattlePreviewSprite(CardDefinition definition)
        {
            if (definition != null && definition.Id == "boss-medusa")
            {
                Sprite medusaToken = LoadPreviewSprite("BattlePreviews/boss_medusa");
                if (medusaToken != null)
                    return medusaToken;
            }

            if (definition != null && definition.Id == "miniboss-composable-golem")
            {
                Sprite minibossToken = LoadPreviewSprite("BattlePreviews/miniboss_golem_componibile_token");
                if (minibossToken != null)
                    return minibossToken;
            }

            if (definition != null && definition.Id == "trentor")
            {
                Sprite trentorToken = LoadPreviewSprite("BattlePreviews/boss_trentor_token");
                if (trentorToken != null)
                    return trentorToken;
            }

            if (definition != null && definition.Id == "boss-bragus")
            {
                Sprite bragusToken = LoadPreviewSprite("BattlePreviews/boss_bragus_token");
                if (bragusToken != null)
                    return bragusToken;
            }

            if (definition != null && definition.Id == "boss-palatir")
            {
                Sprite palatirToken = LoadPreviewSprite("BattlePreviews/boss_palatir_token");
                if (palatirToken != null)
                    return palatirToken;
            }

            string className = PreviewClassName(definition.HeroClass);
            string creatureName = PreviewCreatureName(definition.DisplayName, definition.Strength);
            var candidates = new List<string>
            {
                $"BattlePreviews/{className}_ring",
                $"BattlePreviews/{definition.Strength}_{creatureName}_{className}",
                $"BattlePreviews/{definition.Strength}_{PreviewCreatureNameFromId(definition.Id, definition.Strength)}_{className}",
                $"BattlePreviews/{definition.Strength}_{creatureName}_assasin",
                $"BattlePreviews/{definition.Strength}_{creatureName}_assassin",
                $"BattlePreviews/{definition.Strength}_alien_{className}",
                $"BattlePreviews/{definition.Strength}_whitealien_{className}"
            };
            if (definition.Id.Contains("whitealien"))
            {
                candidates.Add($"BattlePreviews/8_whitealien_{className}");
                candidates.Add($"BattlePreviews/8_alien_{className}");
            }

            foreach (string candidate in candidates)
            {
                Sprite sprite = LoadPreviewSprite(candidate);
                if (sprite != null)
                    return sprite;
            }

            return LoadPreviewSprite("BattlePreviews/warrior_ring");
        }

        private static Sprite ResolveCardArtwork(CardDefinition definition)
        {
            if (definition == null)
                return null;

            if (definition.Artwork != null)
                return definition.Artwork;

            return ResolveCardArtworkFromBattlePreviews(definition);
        }

        private static Sprite ResolveCardArtworkFromBattlePreviews(CardDefinition definition)
        {
            string className = PreviewClassName(definition.HeroClass);
            string creatureName = PreviewCreatureName(definition.DisplayName, definition.Strength);
            string creatureNameFromId = PreviewCreatureNameFromId(definition.Id, definition.Strength);
            var candidates = new List<string>
            {
                $"BattlePreviews/{definition.Strength}_{creatureName}_{className}",
                $"BattlePreviews/{definition.Strength}_{creatureNameFromId}_{className}"
            };

            if (definition.HeroClass == HeroClass.Paladin)
            {
                candidates.Add($"BattlePreviews/{definition.Strength}_{creatureName}_paladin");
                candidates.Add($"BattlePreviews/{definition.Strength}_{creatureNameFromId}_paladin");
            }

            if (!string.IsNullOrWhiteSpace(definition.Id) && definition.Id.Contains("whitealien"))
            {
                candidates.Add($"BattlePreviews/{definition.Strength}_whitealien_{className}");
                candidates.Add($"BattlePreviews/8_whitealien_{className}");
                candidates.Add($"BattlePreviews/8_alien_{className}");
            }

            HashSet<string> visitedCandidates = new HashSet<string>();
            foreach (string candidate in candidates)
            {
                if (!visitedCandidates.Add(candidate))
                    continue;

                Sprite sprite = LoadPreviewSprite(candidate);
                if (sprite != null)
                    return sprite;
            }

            return null;
        }

        private static Sprite LoadPreviewSprite(string resourcePath)
        {
            if (runtimePreviewSprites.TryGetValue(resourcePath, out Sprite cached))
                return cached;

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            runtimePreviewSprites[resourcePath] = sprite;
            return sprite;
        }

        private static Sprite LoadPreviewSpriteWithTransparentBlack(string resourcePath)
        {
            string cacheKey = resourcePath + ":transparent-black";
            if (runtimePreviewSprites.TryGetValue(cacheKey, out Sprite cached))
                return cached;

            Texture2D source = Resources.Load<Texture2D>(resourcePath);
            if (source == null)
            {
                Sprite fallback = LoadPreviewSprite(resourcePath);
                if (fallback != null)
                    source = fallback.texture;
            }

            if (source == null)
                return null;

            Texture2D readable = CopyToReadableTexture(source);
            Color32[] pixels = readable.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                int brightness = pixel.r + pixel.g + pixel.b;
                if (brightness < 54)
                {
                    pixels[i] = new Color32(pixel.r, pixel.g, pixel.b, 0);
                }
                else if (brightness < 96)
                {
                    byte alpha = (byte)Mathf.Clamp((brightness - 54) * 6, 0, pixel.a);
                    pixels[i] = new Color32(pixel.r, pixel.g, pixel.b, alpha);
                }
            }

            readable.SetPixels32(pixels);
            readable.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            Sprite sprite = Sprite.Create(
                readable,
                new Rect(0f, 0f, readable.width, readable.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = source.name + " Transparent";
            runtimePreviewSprites[cacheKey] = sprite;
            return sprite;
        }

        private static Texture2D CopyToReadableTexture(Texture2D source)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            Texture2D readable = new(source.width, source.height, TextureFormat.RGBA32, mipChain: false)
            {
                name = source.name + " Runtime Copy"
            };
            readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            readable.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return readable;
        }

        private static string PreviewClassName(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => "assassin",
                HeroClass.Warrior => "warrior",
                HeroClass.Mage => "mage",
                HeroClass.Paladin => "paladin",
                HeroClass.Rogue => "rogue",
                HeroClass.Hunter => "hunter",
                HeroClass.Barbarian => "barbarian",
                HeroClass.Necromancer => "necromancer",
                HeroClass.Priest => "priest",
                _ => "warrior"
            };
        }

        private static string PreviewCreatureName(string displayName, int strength)
        {
            if (strength >= 10)
                return "champion";

            return NormalizePreviewName(displayName);
        }

        private static string PreviewCreatureNameFromId(string id, int strength)
        {
            if (strength >= 10)
                return "champion";
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            string[] parts = id.Split('-');
            return parts.Length > 1 ? NormalizePreviewName(parts[1]) : NormalizePreviewName(id);
        }

        private static string NormalizePreviewName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return value
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);
        }

        private static Sprite GetAuraSprite()
        {
            if (runtimeAuraSprite != null)
                return runtimeAuraSprite;

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Turn Aura"
            };
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.78f) / 0.22f);
                    float softEdge = Mathf.Clamp01(1f - distance);
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Max(ring * 0.9f, softEdge * 0.22f));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            runtimeAuraSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            return runtimeAuraSprite;
        }

        private static Sprite GetSparkleSprite()
        {
            if (runtimeSparkleSprite != null)
                return runtimeSparkleSprite;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Turn Sparkle"
            };
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float diagonal = Mathf.Max(Mathf.Abs(delta.x + delta.y), Mathf.Abs(delta.x - delta.y));
                    float axial = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                    float star = Mathf.Clamp01(1f - Mathf.Min(diagonal * 0.11f, axial * 0.18f));
                    byte alpha = (byte)Mathf.RoundToInt(255f * star * star);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            runtimeSparkleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            return runtimeSparkleSprite;
        }

        private static Sprite GetPetrifiedOverlaySprite()
        {
            if (petrifiedOverlaySprite != null)
                return petrifiedOverlaySprite;

            petrifiedOverlaySprite = Resources.Load<Sprite>("UI/medusa_petrified_overlay");
            if (petrifiedOverlaySprite != null)
                return petrifiedOverlaySprite;

            Texture2D texture = Resources.Load<Texture2D>("UI/medusa_petrified_overlay");
            if (texture != null)
            {
                petrifiedOverlaySprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }

            return petrifiedOverlaySprite;
        }

        private static Sprite GetPetrifiedFragmentSprite()
        {
            if (petrifiedFragmentSprite != null)
                return petrifiedFragmentSprite;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - 31.5f) / 31.5f;
                    float ny = (y - 31.5f) / 31.5f;
                    float edgeA = nx + ny * 0.28f + 0.82f;
                    float edgeB = -nx + ny * 0.52f + 0.76f;
                    float edgeC = -ny + nx * 0.18f + 0.78f;
                    float edgeD = ny - nx * 0.42f + 0.86f;
                    float shape = Mathf.Clamp01(edgeA * 5.5f)
                        * Mathf.Clamp01(edgeB * 5.5f)
                        * Mathf.Clamp01(edgeC * 5.5f)
                        * Mathf.Clamp01(edgeD * 5.5f);
                    float chip = Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                    float vein = Mathf.Clamp01(1f - Mathf.Abs(nx + ny * 0.7f + Mathf.Sin(ny * 8f) * 0.05f) * 9f);
                    float highlight = Mathf.Clamp01(1f - Mathf.Sqrt((nx + 0.28f) * (nx + 0.28f) * 4.8f + (ny + 0.22f) * (ny + 0.22f) * 5.8f));
                    float alpha = shape * Mathf.Clamp01(0.82f + chip * 0.35f);
                    byte shade = (byte)Mathf.Lerp(96f, 226f, Mathf.Clamp01(chip * 0.55f + highlight * 0.55f - vein * 0.25f));
                    if (vein > 0.2f)
                        shade = (byte)Mathf.Lerp(shade, 44f, vein);
                    pixels[y * size + x] = new Color32(shade, shade, shade, (byte)(alpha * 245f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            petrifiedFragmentSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            petrifiedFragmentSprite.name = "Petrified Fragment";
            petrifiedFragmentSprite.hideFlags = HideFlags.HideAndDontSave;
            return petrifiedFragmentSprite;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            int size,
            FontStyle style,
            TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = size;
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text);
            return text;
        }

        private static void SetAnchors(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }
    }
}
