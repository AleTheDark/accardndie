using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    /// <summary>
    /// Harness visuale autonomo per sviluppare la Sequenza delle Classi senza avviare una
    /// campagna. Aggiungere il componente a un GameObject vuoto e premere Play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProvaLampoMemoryDebugScene : MonoBehaviour, IFlashTrialCampaignScene
    {
        private const string ClassIconAtlasPath = "UI/DeckBuilder/class_icons_atlas";
        private const string PatternSfxPath = "SFX/QuickChallengeMemory/pat";

        private static readonly HeroClass[] Classes =
        {
            HeroClass.Barbarian,
            HeroClass.Paladin,
            HeroClass.Warrior,
            HeroClass.Mage,
            HeroClass.Necromancer,
            HeroClass.Priest,
            HeroClass.Assassin,
            HeroClass.Hunter,
            HeroClass.Rogue
        };

        [SerializeField, Range(3, 12)] private int maximumLevels = 8;
        [SerializeField, Range(0.15f, 1.5f)] private float symbolLightDuration = 0.55f;
        [SerializeField, Range(0.05f, 0.8f)] private float symbolPauseDuration = 0.2f;
        [SerializeField, Range(3f, 20f)] private float inputTimeout = 10f;

        private readonly Dictionary<HeroClass, Button> classButtons = new Dictionary<HeroClass, Button>();
        private readonly Dictionary<HeroClass, Color> classColors = new Dictionary<HeroClass, Color>();
        private readonly Dictionary<string, Sprite> classSprites = new Dictionary<string, Sprite>();
        private FlashTrialMemoryGame game;
        private Text titleText;
        private Text statusText;
        private Text scoreText;
        private Text countdownText;
        private Button restartButton;
        private Button forfeitButton;
        private RectTransform memoryPanel;
        private RectTransform slotPanel;
        private Image slotClassImage;
        private Text slotStrengthText;
        private Text slotCurrencyText;
        private Image slotCurrencyImage;
        private RectTransform[] slotReelContents;
        private Text slotRewardText;
        private Coroutine roundRoutine;
        private float lastInputAt;
        private bool acceptingInput;
        private AudioSource patternAudioSource;
        private AudioClip[] patternClips;
        private int attemptSeed;
        private System.Func<FlashTrialResult, int, FlashTrialSlotOutcome?> campaignReward;
        private System.Action<FlashTrialResult, int> campaignCompleted;

        public void ConfigureForCampaign(
            System.Func<FlashTrialResult, int, FlashTrialSlotOutcome?> resolveReward,
            System.Action<FlashTrialResult, int> completed)
        {
            campaignReward = resolveReward;
            campaignCompleted = completed;
        }

        private void Awake()
        {
            EnsureEventSystem();
            patternAudioSource = gameObject.AddComponent<AudioSource>();
            patternAudioSource.playOnAwake = false;
            patternAudioSource.loop = false;
            patternAudioSource.spatialBlend = 0f;
            patternAudioSource.volume = 1f;
            patternAudioSource.ignoreListenerPause = true;
            patternClips = new AudioClip[maximumLevels];
            for (int index = 0; index < patternClips.Length; index++)
                patternClips[index] = Resources.Load<AudioClip>(PatternSfxPath + (index + 1));
            BuildUi();
        }

        private void Start()
        {
            StartAttempt();
        }

        private void Update()
        {
            if (!acceptingInput || game == null || game.IsFinished)
                return;
            float remaining = Mathf.Max(0f, inputTimeout - (Time.unscaledTime - lastInputAt));
            countdownText.text = $"TEMPO  {Mathf.CeilToInt(remaining)}";
            countdownText.color = remaining <= 3f
                ? new Color(1f, 0.28f, 0.2f)
                : new Color(1f, 0.82f, 0.3f);
            if (remaining <= 0f)
                Finish(game.FinishForInactivity(), "Tempo scaduto");
        }

        private void StartAttempt()
        {
            if (roundRoutine != null)
                StopCoroutine(roundRoutine);
            attemptSeed = System.Guid.NewGuid().GetHashCode();
            game = new FlashTrialMemoryGame(attemptSeed, maximumLevels, Classes);
            acceptingInput = false;
            titleText.text = "MEMORIZZA LA SEQUENZA";
            statusText.text = "Osserva la sequenza";
            restartButton.gameObject.SetActive(false);
            forfeitButton.gameObject.SetActive(true);
            StartCoroutine(DebugUi.EnterFromTop(memoryPanel));
            slotPanel.gameObject.SetActive(false);
            ResetButtonColors();
            BeginRound();
        }

        private void BeginRound()
        {
            game.BeginNextRound();
            scoreText.text = $"RECORD ATTUALE  {game.CompletedLevels}   /   OBIETTIVO  {maximumLevels}";
            roundRoutine = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            acceptingInput = false;
            countdownText.gameObject.SetActive(false);
            SetButtonsInteractable(false);
            statusText.text = $"Livello {game.Sequence.Count}: osserva";
            yield return new WaitForSecondsRealtime(0.55f);

            for (int sequenceIndex = 0; sequenceIndex < game.Sequence.Count; sequenceIndex++)
            {
                HeroClass heroClass = game.Sequence[sequenceIndex];
                SetButtonLit(heroClass, true);
                PlayPatternInputSfx(sequenceIndex);
                yield return new WaitForSecondsRealtime(symbolLightDuration);
                SetButtonLit(heroClass, false);
                yield return new WaitForSecondsRealtime(symbolPauseDuration);
            }

            statusText.text = "Ora ripeti la sequenza";
            acceptingInput = true;
            lastInputAt = Time.unscaledTime;
            countdownText.text = $"TEMPO  {Mathf.CeilToInt(inputTimeout)}";
            countdownText.gameObject.SetActive(true);
            SetButtonsInteractable(true);
            roundRoutine = null;
        }

        private void Submit(HeroClass heroClass)
        {
            if (!acceptingInput || game == null || game.IsFinished)
                return;

            lastInputAt = Time.unscaledTime;
            StartCoroutine(FlashPlayerInput(heroClass));
            PlayPatternInputSfx(game.ExpectedInputIndex);
            FlashTrialMemoryInputResult result = game.Submit(heroClass);
            switch (result)
            {
                case FlashTrialMemoryInputResult.AwaitingInput:
                    statusText.text = $"Corretto  {game.ExpectedInputIndex}/{game.Sequence.Count}";
                    break;
                case FlashTrialMemoryInputResult.RoundCompleted:
                    acceptingInput = false;
                    SetButtonsInteractable(false);
                    scoreText.text = $"RECORD ATTUALE  {game.CompletedLevels}   /   OBIETTIVO  {maximumLevels}";
                    statusText.text = "Sequenza corretta! +1 simbolo";
                    StartCoroutine(BeginNextRoundAfterPause());
                    break;
                case FlashTrialMemoryInputResult.Perfect:
                    Finish(FlashTrialResult.Perfect, "Sequenza massima completata");
                    break;
                case FlashTrialMemoryInputResult.Failed:
                    Finish(FlashTrialMemoryGame.Evaluate(game.CompletedLevels, maximumLevels),
                        $"Errore su {ClassName(heroClass)}");
                    break;
            }
        }

        private void PlayPatternInputSfx(int sequenceIndex)
        {
            if (patternAudioSource == null || patternClips == null || patternClips.Length == 0)
                return;
            int clipIndex = Mathf.Clamp(sequenceIndex, 0, patternClips.Length - 1);
            AudioClip clip = patternClips[clipIndex];
            if (clip == null)
                return;
            patternAudioSource.Stop();
            patternAudioSource.PlayOneShot(clip);
        }

        private IEnumerator BeginNextRoundAfterPause()
        {
            yield return new WaitForSecondsRealtime(0.8f);
            if (game != null && !game.IsFinished)
                BeginRound();
        }

        private IEnumerator FlashPlayerInput(HeroClass heroClass)
        {
            SetButtonLit(heroClass, true);
            yield return new WaitForSecondsRealtime(0.14f);
            SetButtonLit(heroClass, false);
        }

        private void Forfeit()
        {
            if (game == null || game.IsFinished)
                return;
            acceptingInput = false;
            SetButtonsInteractable(false);
            QuickChallengeRoomDebugScene room = GetComponent<QuickChallengeRoomDebugScene>();
            if (room != null)
            {
                room.ShowForfeitPopup();
                return;
            }
            Finish(FlashTrialResult.Forfeited,
                "Rinuncia: nella campagna scatterebbero malus 50% e interstitial");
        }

        private void Finish(FlashTrialResult result, string reason)
        {
            acceptingInput = false;
            countdownText.gameObject.SetActive(false);
            SetButtonsInteractable(false);
            ResetButtonColors();
            statusText.text = $"{reason}\nRISULTATO: {ResultName(result)}";
            scoreText.text = $"SEQUENZA PIU' LUNGA  {game?.CompletedLevels ?? 0}";
            restartButton.gameObject.SetActive(false);
            forfeitButton.gameObject.SetActive(false);
            if (campaignCompleted != null)
            {
                StartCoroutine(CompleteCampaignTrial(result, game?.CompletedLevels ?? 0));
                return;
            }
            if (result != FlashTrialResult.Forfeited)
                StartCoroutine(TransitionToSlotMachine(result));
        }

        /// <summary>
        /// In campagna la slot mostra il premio deciso dalla stanza, non un'estrazione locale,
        /// e la stanza si chiude solo quando i rulli si sono fermati.
        /// </summary>
        private IEnumerator CompleteCampaignTrial(FlashTrialResult result, int completedLevels)
        {
            FlashTrialSlotOutcome? outcome = campaignReward?.Invoke(result, completedLevels);
            if (outcome.HasValue)
                yield return TransitionToSlotMachine(result, outcome.Value);
            campaignCompleted(result, completedLevels);
        }

        private IEnumerator TransitionToSlotMachine(FlashTrialResult result,
            FlashTrialSlotOutcome? campaignOutcome = null)
        {
            yield return new WaitForSecondsRealtime(1.15f);
            yield return DebugUi.ExitToLeft(memoryPanel);

            slotPanel.gameObject.SetActive(true);
            SetSlotContentsVisible(false);
            yield return DebugUi.EnterFromTop(slotPanel);
            SetSlotContentsVisible(true);
            yield return RollSlotMachine(result, campaignOutcome);
        }

        private IEnumerator RollSlotMachine(FlashTrialResult result,
            FlashTrialSlotOutcome? campaignOutcome = null)
        {
            FlashTrialSlotOutcome outcome;
            if (campaignOutcome.HasValue)
            {
                outcome = campaignOutcome.Value;
            }
            else
            {
                var machine = new FlashTrialSlotMachine(unchecked(attemptSeed * 31 + game.CompletedLevels));
                CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
                List<FlashTrialCardCandidate> candidates = database == null ? new List<FlashTrialCardCandidate>() : database.Cards
                    .Where(card => card != null && card.Category == CardCategory.Monster && card.CanEnterCombat)
                    .Select(card => new FlashTrialCardCandidate(card.Id, card.HeroClass, card.Strength)).ToList();
                outcome = candidates.Count > 0
                    ? machine.Roll(result, game.CompletedLevels, candidates)
                    : machine.Roll(result, game.CompletedLevels);
            }
            HeroClass[] classes = Classes;
            yield return DebugUi.RollSlotReels(slotPanel.gameObject, slotReelContents,
                (tick, rollClass, rollStrength, rollPrize) =>
                {
                    if (rollClass) slotClassImage.sprite = ClassSprite(classes[tick % classes.Length]);
                    if (rollStrength) slotStrengthText.text = (2 + tick % 9).ToString();
                    if (rollPrize) SetSlotCurrency(tick % 2 == 0);
                },
                reel =>
                {
                    if (reel == 0) slotClassImage.sprite = outcome.HasConsumableRewards
                        ? Resources.Load<Sprite>("UI/" + outcome.FirstConsumableResource)
                        : ClassSprite(outcome.HeroClass);
                    else if (reel == 1) slotStrengthText.text = outcome.HasConsumableRewards
                        ? (string.IsNullOrEmpty(outcome.SecondConsumableName) ? "—" : outcome.SecondConsumableName.ToUpperInvariant())
                        : outcome.Strength.ToString();
                    else SetSlotCurrency(outcome.Currency == FlashTrialCurrencyReward.Experience);
                });
            string currencyLabel = outcome.Currency == FlashTrialCurrencyReward.Experience ? "EXP" : "ORO";
            slotRewardText.text = outcome.HasConsumableRewards
                ? $"HAI VINTO\n{outcome.FirstConsumableName}" +
                  (string.IsNullOrEmpty(outcome.SecondConsumableName) ? "" : $" + {outcome.SecondConsumableName}") +
                  $"\n+ {outcome.Amount} {currencyLabel}"
                : string.IsNullOrEmpty(outcome.CardId)
                ? $"HAI VINTO\n+ {outcome.Amount} {currencyLabel}"
                : $"HAI VINTO\nCARTA {ClassName(outcome.HeroClass).ToUpperInvariant()} " +
                  $"DI POTENZA {outcome.Strength}\n+ {outcome.Amount} {currencyLabel}";
            yield return new WaitForSecondsRealtime(1.4f);
            yield return DebugUi.ExitToLeft(slotPanel);
            yield return DebugUi.RevealRewardCard(
                slotPanel.parent, outcome.CardId, waitForContinue: campaignOutcome.HasValue);
        }

        private void BuildUi()
        {
            Canvas canvas = new GameObject("Prova Lampo Memory Debug Canvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = AccardND.Battlefield.MmoUiTheme.BodyFont
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bool insideQuickChallenge = GetComponent<QuickChallengeRoomDebugScene>() != null;
            Image background = CreateImage("Background", canvas.transform, insideQuickChallenge
                ? new Color(0.01f, 0.015f, 0.03f, 0.18f)
                : new Color(0.025f, 0.03f, 0.055f, 1f));
            Stretch(background.rectTransform);

            foreach (Sprite sprite in Resources.LoadAll<Sprite>(ClassIconAtlasPath))
                classSprites[sprite.name] = sprite;

            memoryPanel = new GameObject("Memory Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            memoryPanel.SetParent(canvas.transform, false);
            Stretch(memoryPanel);

            slotPanel = new GameObject("Slot Machine Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            slotPanel.SetParent(canvas.transform, false);
            Stretch(slotPanel);
            BuildSlotMachineUi(slotPanel, font);
            slotPanel.gameObject.SetActive(false);

            titleText = CreateText("Title", memoryPanel, font, 60, FontStyle.Bold);
            titleText.color = new Color(1f, 0.82f, 0.3f);
            titleText.rectTransform.anchorMin = new Vector2(0.08f, 0.84f);
            titleText.rectTransform.anchorMax = new Vector2(0.92f, 0.94f);
            titleText.rectTransform.offsetMin = new Vector2(0f, -134f);
            titleText.rectTransform.offsetMax = new Vector2(0f, -134f);

            statusText = CreateText("Status", memoryPanel, font, 35, FontStyle.Bold);
            statusText.color = Color.white;
            statusText.rectTransform.anchorMin = new Vector2(0.18f, 0.75f);
            statusText.rectTransform.anchorMax = new Vector2(0.82f, 0.87f);
            statusText.rectTransform.offsetMin = new Vector2(0f, -131f);
            statusText.rectTransform.offsetMax = new Vector2(0f, -131f);

            scoreText = CreateText("Score", memoryPanel, font, 35, FontStyle.Normal);
            scoreText.color = new Color(0.72f, 0.82f, 0.94f);
            scoreText.rectTransform.anchorMin = new Vector2(0.2f, 0.68f);
            scoreText.rectTransform.anchorMax = new Vector2(0.8f, 0.75f);
            scoreText.rectTransform.offsetMin = new Vector2(0f, -927f);
            scoreText.rectTransform.offsetMax = new Vector2(0f, -927f);

            countdownText = CreateText("Countdown", memoryPanel, font, 28, FontStyle.Bold);
            countdownText.color = new Color(1f, 0.82f, 0.3f);
            SetRect(countdownText.rectTransform, new Vector2(0.76f, 0.68f), new Vector2(0.92f, 0.75f));
            countdownText.gameObject.SetActive(false);

            for (int index = 0; index < Classes.Length; index++)
            {
                HeroClass heroClass = Classes[index];
                int row = index / 3;
                int column = index % 3;
                Color baseColor = ClassColor(heroClass);
                classColors[heroClass] = baseColor;
                Sprite icon = ClassSprite(heroClass);
                Button button = CreateClassButton(heroClass, memoryPanel, font, icon, baseColor);
                const float cellSize = 213f;
                const float cellStep = 250f;
                Vector2 buttonPosition = heroClass == HeroClass.Assassin
                    || heroClass == HeroClass.Hunter
                    || heroClass == HeroClass.Rogue
                    ? new Vector2((column - 1) * cellStep, -219f)
                    : new Vector2((column - 1) * cellStep, (1 - row) * cellStep + 5f);
                SetFixedRect(
                    button.GetComponent<RectTransform>(),
                    buttonPosition,
                    new Vector2(cellSize, cellSize));
                HeroClass captured = heroClass;
                button.onClick.AddListener(() => Submit(captured));
                classButtons[heroClass] = button;
            }

            Image memoryFrame = DebugUi.ResourceImage("Memory Frame", memoryPanel,
                "UI/QuickChallenge/memory_frame");
            SetFixedRect(memoryFrame.rectTransform, new Vector2(0f, 5f), new Vector2(1020f, 1020f));

            restartButton = CreateTextButton("Restart", memoryPanel, font, "RIPROVA");
            SetRect(restartButton.GetComponent<RectTransform>(), new Vector2(0.55f, 0.06f), new Vector2(0.72f, 0.14f));
            restartButton.onClick.AddListener(StartAttempt);

            forfeitButton = CreateTextButton("Forfeit", memoryPanel, font, "RINUNCIA");
            SetRect(forfeitButton.GetComponent<RectTransform>(), new Vector2(0.34f, 0.045f), new Vector2(0.66f, 0.13f));
            Image forfeitImage = forfeitButton.GetComponent<Image>();
            Sprite forfeitSprite = Resources.Load<Sprite>("UI/CampaignRestyle/campaign_cta_back_red");
            if (forfeitImage != null && forfeitSprite != null)
            {
                forfeitImage.sprite = forfeitSprite;
                forfeitImage.type = Image.Type.Simple;
                forfeitImage.preserveAspect = false;
                forfeitImage.color = Color.white;
            }
            Text forfeitLabel = forfeitButton.GetComponentInChildren<Text>();
            if (forfeitLabel != null)
            {
                forfeitLabel.font = AccardND.Battlefield.MmoUiTheme.LoreFont;
                forfeitLabel.fontStyle = FontStyle.Normal;
                forfeitLabel.fontSize = 30;
            }
            forfeitButton.onClick.AddListener(Forfeit);
        }

        private void BuildSlotMachineUi(RectTransform parent, Font font)
        {
            Text title = CreateText("Slot Title", parent, font, 46, FontStyle.Bold);
            title.text = "RICOMPENSA DELLA PROVA";
            title.color = new Color(1f, 0.82f, 0.3f);
            SetRect(title.rectTransform, new Vector2(0.15f, 0.82f), new Vector2(0.85f, 0.94f));

            Image frame = DebugUi.ResourceImage("Slot Machine Frame", parent, "UI/QuickChallenge/slot_machine");
            SetFixedRect(frame.rectTransform, Vector2.zero, new Vector2(1120f, 448f));
            slotReelContents = DebugUi.CreateSlotReels(parent, new[] { -255f, 0f, 255f }, 235f);
            slotClassImage = DebugUi.Image("Class Reel Icon", slotReelContents[0], Color.white); slotClassImage.preserveAspect = true; DebugUi.SetRect(slotClassImage.rectTransform, Vector2.zero, Vector2.one);
            slotStrengthText = DebugUi.Text("Strength Reel", slotReelContents[1], font, 70, FontStyle.Bold); slotStrengthText.color = Color.white; DebugUi.SetRect(slotStrengthText.rectTransform, Vector2.zero, Vector2.one);
            slotCurrencyText = DebugUi.Text("Currency Reel", slotReelContents[2], font, 42, FontStyle.Bold); slotCurrencyText.color = Color.white; DebugUi.SetRect(slotCurrencyText.rectTransform, Vector2.zero, Vector2.one);
            slotCurrencyImage = DebugUi.ResourceImage("Gold", slotReelContents[2], "UI/Common/gold_coins"); DebugUi.SetRect(slotCurrencyImage.rectTransform, new Vector2(.18f,.18f), new Vector2(.82f,.82f));

            slotRewardText = CreateText("Slot Reward", parent, font, 30, FontStyle.Bold);
            slotRewardText.text = "ROLL IN CORSO...";
            slotRewardText.color = new Color(1f, 0.82f, 0.3f);
            Outline slotRewardOutline = slotRewardText.gameObject.AddComponent<Outline>();
            slotRewardOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            slotRewardOutline.effectDistance = new Vector2(2f, -2f);
            SetRect(slotRewardText.rectTransform, new Vector2(0.2f, 0.16f), new Vector2(0.8f, 0.36f));

        }

        private void SetSlotCurrency(bool experience) { slotCurrencyText.gameObject.SetActive(experience); slotCurrencyImage.gameObject.SetActive(!experience); slotCurrencyText.text = "EXP"; }

        private void SetSlotContentsVisible(bool visible)
        {
            slotClassImage.gameObject.SetActive(visible);
            slotStrengthText.gameObject.SetActive(visible);
            slotCurrencyText.gameObject.SetActive(visible);
            slotCurrencyImage.gameObject.SetActive(visible);
            slotRewardText.gameObject.SetActive(visible);
        }

        private Button CreateClassButton(HeroClass heroClass, Transform parent, Font font, Sprite icon, Color color)
        {
            GameObject root = new GameObject(heroClass + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            background.color = color;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;

            Image image = CreateImage("Icon", root.transform, Color.white);
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            if (heroClass == HeroClass.Priest
                || heroClass == HeroClass.Necromancer
                || heroClass == HeroClass.Mage)
            {
                image.rectTransform.offsetMin = new Vector2(0f, 7f);
                image.rectTransform.offsetMax = new Vector2(0f, 7f);
            }
            else if (heroClass == HeroClass.Warrior)
            {
                image.rectTransform.offsetMin = new Vector2(-7f, 0f);
                image.rectTransform.offsetMax = new Vector2(-7f, 0f);
            }

            return button;
        }

        private Sprite ClassSprite(HeroClass heroClass)
        {
            classSprites.TryGetValue("class_" + heroClass.ToString().ToLowerInvariant(), out Sprite sprite);
            return sprite;
        }

        private static Button CreateTextButton(string name, Transform parent, Font font, string label)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(0.22f, 0.16f, 0.08f, 0.98f);
            Text text = CreateText("Label", root.transform, font, 23, FontStyle.Bold);
            text.text = label;
            text.color = Color.white;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return root.GetComponent<Button>();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (Button button in classButtons.Values)
                button.interactable = interactable;
        }

        private void SetButtonLit(HeroClass heroClass, bool lit)
        {
            if (!classButtons.TryGetValue(heroClass, out Button button))
                return;
            Color baseColor = classColors[heroClass];
            button.GetComponent<Image>().color = lit
                ? Color.Lerp(baseColor, Color.white, 0.72f)
                : baseColor;
            button.transform.localScale = lit ? Vector3.one * 1.08f : Vector3.one;
        }

        private void ResetButtonColors()
        {
            foreach (HeroClass heroClass in Classes)
                SetButtonLit(heroClass, false);
        }

        private static Color ClassColor(HeroClass heroClass)
        {
            return HeroClassFamily.Of(heroClass) switch
            {
                ClassFamily.Might => new Color(0.48f, 0.15f, 0.11f, 0.98f),
                ClassFamily.Cunning => new Color(0.12f, 0.36f, 0.2f, 0.98f),
                _ => new Color(0.2f, 0.18f, 0.5f, 0.98f)
            };
        }

        private static string ClassName(HeroClass heroClass) => heroClass switch
        {
            HeroClass.Assassin => "Assassino",
            HeroClass.Warrior => "Guerriero",
            HeroClass.Mage => "Mago",
            HeroClass.Paladin => "Paladino",
            HeroClass.Rogue => "Ladro",
            HeroClass.Hunter => "Cacciatore",
            HeroClass.Barbarian => "Barbaro",
            HeroClass.Necromancer => "Necromante",
            HeroClass.Priest => "Sacerdote",
            _ => heroClass.ToString()
        };

        private static string ResultName(FlashTrialResult result) => result switch
        {
            FlashTrialResult.Perfect => "PERFETTO",
            FlashTrialResult.Excellent => "ECCELLENTE",
            FlashTrialResult.Good => "OTTIMO",
            FlashTrialResult.Completed => "COMPLETATO",
            FlashTrialResult.Forfeited => "RINUNCIA",
            _ => "FALLITO"
        };

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, FontStyle style)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one);
        }

        private static void SetFixedRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
