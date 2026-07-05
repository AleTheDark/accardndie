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
        private const float CardAspect = 848f / 1264f;
        private const float ArtworkViewportAspect = CardAspect * (0.865f - 0.135f) / (0.805f - 0.325f);
        private const float ArtworkCropOverscan = 0.86f;
        private static readonly Color ActionLabelOutline = new(0.02f, 0.01f, 0f);
        private static readonly Color ActionLabelGreen = new(0.05f, 0.56f, 0.24f);
        private static readonly Color ActionLabelRed = new(0.66f, 0.08f, 0.05f);
        private static readonly Color ActionLabelOrange = new(0.78f, 0.32f, 0.06f);
        private static readonly Color ActionLabelBlue = new(0.05f, 0.28f, 0.76f);
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
        private Text statusLabel;
        private RectTransform statusIconRoot;
        private readonly List<GameObject> statusIconViews = new();
        private GameObject healthBarRoot;
        private Image healthBarFill;
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
        private HeroClass cardHeroClass;
        private Coroutine diceCoroutine;
        private Coroutine motionCoroutine;
        private Coroutine turnAuraCoroutine;
        private Coroutine battleAuraCoroutine;
        private Coroutine targetHintAuraCoroutine;
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

        public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;
        public Button Button { get; private set; }
        public bool IsDragging => dragging;

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
            configuration = gameConfiguration;
            cardHeroClass = definition.HeroClass;
            rectTransform = (RectTransform)transform;
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
            (Vector2 artworkAnchorMin, Vector2 artworkAnchorMax) = ArtworkViewportAnchors(definition.HeroClass);
            SetAnchors(artViewport, artworkAnchorMin, artworkAnchorMax);

            Color backdropColor = ClassColor(definition.HeroClass) * 0.42f;
            backdropColor.a = 1f;
            Image artBackdrop = CreateImage("Artwork Backdrop", artViewport, backdropColor);
            Stretch(artBackdrop.rectTransform);

            Image art = CreateImage("Artwork", artViewport, Color.white);
            Sprite resolvedArtwork = ResolveCardArtwork(definition);
            Sprite framedArtwork = CreateCoverArtwork(resolvedArtwork, definition.HeroClass);
            art.sprite = framedArtwork;
            if (art.sprite == null)
            {
                art.color = Color.clear;
                Debug.LogWarning($"[Accard N' Die] Artwork carta non risolto: {definition.Id}. La carta grande non usera' BattlePreviews.");
            }
            art.preserveAspect = framedArtwork == resolvedArtwork;
            Stretch(art.rectTransform);

            Image holder = CreateImage("Class Holder", transform, Color.white);
            holder.sprite = Resources.Load<Sprite>($"CardBorders/{BorderResourceName(definition.HeroClass)}");
            holder.preserveAspect = true;
            Stretch(holder.rectTransform);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            statsText.text = string.IsNullOrWhiteSpace(definition.RulesText)
                ? $"{ClassName(definition.HeroClass)}\n{ClassMechanicText(definition.HeroClass)}"
                : $"{ClassName(definition.HeroClass)}\n{definition.RulesText}";
            statsText.color = DescriptionColor(definition.HeroClass);
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
            cardHeroClass = definition.HeroClass;
            rectTransform = (RectTransform)transform;
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
            preview.sprite = ResolveBattlePreviewSprite(definition);
            if (preview.sprite == null)
                preview.color = Color.clear;
            preview.preserveAspect = true;
            Stretch(preview.rectTransform);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        public void RaiseStrengthText()
        {
            if (strengthText != null)
                strengthText.transform.SetAsLastSibling();
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

        public void SetSelected(bool selected)
        {
            if (dragging)
            {
                selectedVisual = selected;
                ApplySelectionChrome(true);
                return;
            }

            if (selectedVisual == selected)
            {
                ApplySelectionChrome(selected);
                return;
            }

            selectedVisual = selected;
            ApplySelectionChrome(selected);

            Vector3 targetScale = selected
                ? Vector3.one * configuration.Animation.SelectedCardScale
                : initialScale;
            AnimateSelectionScale(targetScale, 0.12f);
        }

        public void SetTargetHint(bool highlighted, Color color)
        {
            if (highlighted)
            {
                selectionOutline.enabled = true;
                selectionOutline.effectColor = color;
                if (liftShadow != null && !dragging)
                    liftShadow.enabled = true;
                return;
            }

            ApplySelectionChrome(selectedVisual);
        }

        private void ApplySelectionChrome(bool selected)
        {
            selectionOutline.enabled = selected;
            selectionOutline.effectColor = new Color(0.2f, 1f, 0.65f);
            if (liftShadow != null && !dragging)
                liftShadow.enabled = selected;
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
            selectionOutline.effectColor = new Color(0.45f, 1f, 0.88f, 1f);
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
                CreateHealthBar(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

            int clampedCurrent = Mathf.Clamp(current, 0, maximum);
            float normalized = Mathf.Clamp01((float)clampedCurrent / maximum);
            healthBarRoot.SetActive(true);
            healthBarRoot.transform.SetAsLastSibling();
            if (healthBarFill != null)
            {
                healthBarFill.color = fillColor;
                healthBarFill.rectTransform.anchorMax = new Vector2(normalized, 1f);
            }
            if (healthBarText != null)
                healthBarText.text = $"HP {clampedCurrent}/{maximum}";
        }

        public void SetHealthBarVisible(bool visible)
        {
            if (healthBarRoot != null)
                healthBarRoot.SetActive(visible);
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
            if (statusIconRoot == null)
                return;

            bool visible = statuses != null && statuses.Length > 0;
            statusIconRoot.gameObject.SetActive(visible);
            if (!visible)
                return;

            for (int index = 0; index < statuses.Length; index++)
                CreateStatusIcon(statuses[index], index);
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
                StopCoroutine(diceCoroutine);

            diceCoroutine = StartCoroutine(PlayDiceRollRoutine(
                catalog,
                sides,
                sides,
                result,
                0,
                false,
                result,
                VigorSelectionMode.Single,
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
                StopCoroutine(diceCoroutine);

            diceCoroutine = StartCoroutine(PlayDiceRollRoutine(
                catalog,
                ResolveVisualDieSides(catalog, sides),
                roll.DieSides,
                roll.FirstRoll,
                roll.SecondRoll,
                roll.HasSecondRoll,
                roll.SelectedRoll,
                roll.SelectionMode,
                caption,
                rollDuration,
                resultHold,
                use3DDice: true));
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

        public IEnumerator PlayDefeatAnimation()
        {
            SetBattleAura(false, Color.clear, string.Empty);
            if (targetHintAuraRoot != null)
                SetTargetHintAura(false, Color.clear);
            defeatedLabel.gameObject.SetActive(false);
            SetStatus("MORTE", new Color(0.95f, 0.12f, 0.12f));
            float elapsed = 0f;
            float duration = configuration.Animation.DefeatDuration;
            if (duration <= 0f)
            {
                canvasGroup.alpha = 0.38f;
                rectTransform.localScale = initialScale * 0.93f;
                yield break;
            }
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float shake = Mathf.Sin(progress * 60f) * (1f - progress) * 7f;
                canvasGroup.alpha = Mathf.Lerp(1f, 0.38f, progress);
                rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, initialScale * 0.93f, progress);
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, shake);
                yield return null;
            }
            canvasGroup.alpha = 0.38f;
            rectTransform.localScale = initialScale * 0.93f;
            if (duelDetached)
            {
                rectTransform.anchoredPosition = duelHomeAnchoredPosition;
                rectTransform.localRotation = duelHomeRotation;
                LayoutElement layout = GetComponent<LayoutElement>();
                layout.ignoreLayout = false;
                duelDetached = false;
            }
        }

        public void ResetState()
        {
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
            selectionOutline.enabled = false;
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
            SetAnchors(rootRect, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.94f));

            Image background = healthBarRoot.GetComponent<Image>();
            background.color = new Color(0.04f, 0.008f, 0.008f, 0.92f);
            background.raycastTarget = false;
            Outline outline = healthBarRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
            outline.effectDistance = new Vector2(2f, -2f);

            healthBarFill = CreateImage("Health Fill", healthBarRoot.transform, new Color(0.78f, 0.06f, 0.06f, 0.98f));
            healthBarFill.type = Image.Type.Filled;
            SetAnchors(healthBarFill.rectTransform, new Vector2(0.025f, 0.17f), new Vector2(0.975f, 0.83f));

            healthBarText = CreateText("Health Text", healthBarRoot.transform, font, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
            healthBarText.color = Color.white;
            Outline textOutline = healthBarText.gameObject.AddComponent<Outline>();
            textOutline.effectColor = Color.black;
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);
            Stretch(healthBarText.rectTransform);

            healthBarRoot.transform.SetAsLastSibling();
            healthBarRoot.SetActive(false);
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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

            diceCaption = CreateText("Caption", diceRoot.transform, font, 15, FontStyle.Bold, TextAnchor.MiddleCenter);
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
                    exact = Resources.Load<Sprite>($"StatusIcons/{resourceName}");
                    if (exact == null)
                        exact = CreateSpriteFromTexture(Resources.Load<Texture2D>($"StatusIcons/{resourceName}"));
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
                "fury_buff" => "fury_buff",
                "buff_attack" => "barbarian_buff",
                "agreement_buff" => "agreement_buff",
                "buff_attachment" => "agreement_buff",
                "buff_spirit" => "buff_spirit",
                _ => null
            };
        }

        private static string StatusIconName(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return string.Empty;

            string normalized = status.Trim().ToUpperInvariant();
            if (normalized.Contains("MORTE") || normalized.Contains("KO") || normalized.Contains("ELIMINAT"))
                return "death";
            if (normalized.Contains("AURA MIGHT"))
                return "aura_might";
            if (normalized.Contains("AURA CUNNING"))
                return "aura_cunning";
            if (normalized.Contains("AURA MAGIC"))
                return "aura_magic";
            if (normalized.Contains("AURA FORMAZIONE"))
                return "aura_formation";
            if (normalized.Contains("AURA WARRIOR"))
                return "aura_warrior";
            if (normalized.Contains("AURA BARBAR"))
                return "aura_barbarian";
            if (normalized.Contains("AURA PALAD"))
                return "aura_paladin";
            if (normalized.Contains("AURA ROGUE"))
                return "aura_rogue";
            if (normalized.Contains("AURA ASSASS"))
                return "aura_assassin";
            if (normalized.Contains("AURA HUNTER"))
                return "aura_hunter";
            if (normalized.Contains("AURA MAGE"))
                return "aura_mage";
            if (normalized.Contains("AURA NECRO"))
                return "aura_necromancer";
            if (normalized.Contains("AURA PRIEST"))
                return "aura_priest";
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
            if (normalized.Contains("ATTACH") || normalized.Contains("ACCORD") || normalized.Contains("AGREEMENT") || normalized.Contains("LEGAME"))
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
                "aura_might" => new[] { "might", "forza", "potenza", "aura" },
                "aura_cunning" => new[] { "cunning", "astuzia", "furbizia", "aura" },
                "aura_magic" => new[] { "magic", "magia", "magico", "aura" },
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
                typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            actionOverlayRoot = (RectTransform)root.transform;
            SetActionOverlayBounds(new Vector2(0.05f, 0.96f), new Vector2(0.95f, 1.42f));

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

        private static Font GetActionLabelFont()
        {
            if (actionLabelFont != null)
                return actionLabelFont;

            string[] fantasyFontFallbacks =
            {
                "Morpheus",
                "Cinzel",
                "Trajan Pro",
                "Palatino Linotype",
                "Georgia",
                "Times New Roman"
            };
            actionLabelFont = Font.CreateDynamicFontFromOSFont(fantasyFontFallbacks, 17)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            string caption,
            float rollDuration,
            float resultHold,
            bool use3DDice)
        {
            diceRoot.SetActive(true);
            diceCaption.text = caption;
            diceResult.text = string.Empty;
            diceImage.rectTransform.localScale = Vector3.one;
            secondDiceImage.rectTransform.localScale = Vector3.one;
            ConfigureDiceLayout(hasSecondResult);

            bool uses3DDice = use3DDice && Dice3DRollView.IsSupported(visualSides);
            bool usesAnimatedDice = !uses3DDice && firstAnimatedDiceRoot != null;
            if (uses3DDice)
            {
                // Dado 3D scriptato: il risultato è già noto, il dado rotola e
                // decelera fino a fermarsi sulla faccia giusta.
                SetDiceVisible(firstAnimatedDiceRoot, diceImage, false);
                SetDiceVisible(secondAnimatedDiceRoot, secondDiceImage, false);
                EnsureDice3DViews(hasSecondResult);
                firstDice3D.StartScriptedRoll(visualSides, cardHeroClass, firstResult, rollDuration);
                if (hasSecondResult)
                    secondDice3D.StartScriptedRoll(visualSides, cardHeroClass, secondResult, rollDuration);
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
            if (usesAnimatedDice)
            {
                diceResult.text = string.Empty;
            }
            else if (hasSecondResult)
            {
                string choice = selectionMode switch
                {
                    VigorSelectionMode.Highest => "MAX",
                    VigorSelectionMode.Lowest => "MIN",
                    VigorSelectionMode.Sum => "SOMMA",
                    _ => "RISULTATO"
                };
                diceResult.text = $"{firstResult} / {secondResult}  →  {choice} {selectedResult}";
            }
            else
            {
                diceResult.text = $"D{displaySides}  →  {selectedResult}";
            }
            yield return new WaitForSecondsRealtime(usesAnimatedDice
                ? Mathf.Max(resultHold, AnimatedDiceMinimumResultHold)
                : resultHold);

            diceRoot.SetActive(false);
            if (uses3DDice)
            {
                firstDice3D.Hide();
                if (secondDice3D != null)
                    secondDice3D.Hide();
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

        private void EnsureDice3DViews(bool showTwoDice)
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

            if (showTwoDice)
            {
                SetAnchors(firstDice3DArea, new Vector2(0.01f, 0.2f), new Vector2(0.5f, 0.82f));
                SetAnchors(secondDice3DArea, new Vector2(0.5f, 0.2f), new Vector2(0.99f, 0.82f));
            }
            else
            {
                SetAnchors(firstDice3DArea, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f));
                if (secondDice3D != null)
                    secondDice3D.Hide();
            }
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

        private static string ClassName(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Assassin => "ASSASSINO",
                HeroClass.Warrior => "GUERRIERO",
                HeroClass.Mage => "MAGO",
                HeroClass.Paladin => "PALADINO",
                HeroClass.Rogue => "LADRO",
                HeroClass.Hunter => "CACCIATORE",
                HeroClass.Barbarian => "BARBARO",
                HeroClass.Necromancer => "NEGROMANTE",
                HeroClass.Priest => "SACERDOTE",
                _ => heroClass.ToString().ToUpperInvariant()
            };
        }

        private static string ClassMechanicText(HeroClass heroClass)
        {
            return heroClass switch
            {
                HeroClass.Rogue => "RITIRA GLI 1 IN ATTACCO",
                HeroClass.Hunter => "+2 CONTRO IL BERSAGLIO MARCATO",
                HeroClass.Barbarian => "FURIA +2 ATTACCO/DIFESA",
                HeroClass.Necromancer => "RIALZA UN ALLEATO ELIMINATO",
                HeroClass.Priest => "BENEDICE L'ALLEATO PIÙ DEBOLE",
                HeroClass.Assassin => "INIBISCE UN NEMICO",
                HeroClass.Warrior => "SOMMA DUE DADI VIGORE",
                HeroClass.Mage => "ABBASSA IL DADO NEMICO",
                HeroClass.Paladin => "PROTEGGE O SI DIFENDE",
                _ => string.Empty
            };
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
