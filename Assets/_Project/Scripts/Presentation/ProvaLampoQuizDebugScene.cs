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
    [DisallowMultipleComponent]
    public sealed class ProvaLampoQuizDebugScene : MonoBehaviour, IFlashTrialCampaignScene
    {
        private readonly FlashTrialQuizQuestion[] questions = FlashTrialQuizCatalog.Questions;

        [SerializeField, Range(5f, 30f)] private float timeLimit = 15f;
        private FlashTrialQuizGame game;
        private FlashTrialQuizSession session;
        private RectTransform quizPanel;
        private RectTransform slotPanel;
        private Image slotClassImage;
        private Text slotStrengthText;
        private Text slotCurrencyText;
        private Image slotCurrencyImage;
        private RectTransform[] slotReelContents;
        private Text slotRewardText;
        private readonly Dictionary<string, Sprite> classSprites = new Dictionary<string, Sprite>();
        private Text questionText;
        private Text titleText;
        private Text countdownText;
        private Text resultText;
        private Button[] answerButtons;
        private Text[] answerLabels;
        private AudioSource answerAudioSource;
        private AudioClip correctAnswerSfx;
        private AudioClip wrongAnswerSfx;
        private float startedAt;
        private bool active;
        private int questionIndex = -1;
        private int[] questionOrder;
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
            DebugUi.EnsureEventSystem();
            answerAudioSource = gameObject.AddComponent<AudioSource>();
            answerAudioSource.playOnAwake = false;
            answerAudioSource.loop = false;
            answerAudioSource.spatialBlend = 0f;
            answerAudioSource.ignoreListenerPause = true;
            correctAnswerSfx = Resources.Load<AudioClip>("SFX/correct_answer");
            wrongAnswerSfx = Resources.Load<AudioClip>("SFX/wrong_answer");
            BuildUi();
            StartSession();
        }

        private void Update()
        {
            if (!active) return;
            float remaining = Mathf.Max(0f, timeLimit - (Time.unscaledTime - startedAt));
            countdownText.text = Mathf.CeilToInt(remaining).ToString();
            countdownText.color = remaining <= 3f ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.82f, 0.3f);
            if (remaining <= 0f)
                FinishQuestion(correctAnswer: false, "Tempo scaduto");
        }

        private void StartSession()
        {
            StopAllCoroutines();
            session = new FlashTrialQuizSession();
            questionIndex = -1;
            questionOrder = Enumerable.Range(0, questions.Length).ToArray();
            var random = new System.Random(System.Environment.TickCount);
            for (int index = questionOrder.Length - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                (questionOrder[index], questionOrder[other]) = (questionOrder[other], questionOrder[index]);
            }
            quizPanel.gameObject.SetActive(true);
            StartCoroutine(DebugUi.EnterFromTop(quizPanel));
            slotPanel.gameObject.SetActive(false);
            StartQuestion();
        }

        private void StartQuestion()
        {
            questionIndex = questionOrder[session.QuestionsAnswered];
            FlashTrialQuizQuestion question = questions[questionIndex];
            game = new FlashTrialQuizGame(question.CorrectAnswerIndex, timeLimit);
            titleText.text = $"DOMANDA {session.QuestionsAnswered + 1}/{FlashTrialQuizSession.TotalQuestions}";
            questionText.text = question.LocalizedQuestion;
            for (int index = 0; index < 3; index++)
            {
                answerLabels[index].text = question.LocalizedAnswer(index);
                answerLabels[index].color = Color.white;
                answerButtons[index].interactable = true;
            }
            resultText.text = "Scegli una risposta";
            startedAt = Time.unscaledTime;
            active = true;
        }

        private void Submit(int index)
        {
            if (!active) return;
            float elapsed = Time.unscaledTime - startedAt;
            FlashTrialResult result = game.Submit(index, elapsed);
            bool correctAnswer = index == questions[questionIndex].CorrectAnswerIndex;
            ShowAnswerFeedback();
            PlayAnswerSfx(correctAnswer ? correctAnswerSfx : wrongAnswerSfx);
            FinishQuestion(correctAnswer,
                correctAnswer ? "Risposta corretta" : "Risposta errata");
        }

        private void ShowAnswerFeedback()
        {
            Color correctColor = new Color(0.25f, 1f, 0.32f, 1f);
            Color wrongColor = new Color(1f, 0.22f, 0.18f, 1f);
            for (int index = 0; index < answerLabels.Length; index++)
                answerLabels[index].color = index == questions[questionIndex].CorrectAnswerIndex ? correctColor : wrongColor;
        }

        private void PlayAnswerSfx(AudioClip clip)
        {
            if (answerAudioSource == null || clip == null)
                return;
            answerAudioSource.Stop();
            answerAudioSource.PlayOneShot(clip);
        }

        private void FinishQuestion(bool correctAnswer, string reason)
        {
            active = false;
            session.RecordAnswer(correctAnswer);
            foreach (Button button in answerButtons) button.interactable = false;
            resultText.text = $"{reason}  -  CORRETTE {session.CorrectAnswers}/{session.QuestionsAnswered}";
            if (session.IsComplete)
                StartCoroutine(FinishSessionAfterPause());
            else
                StartCoroutine(NextQuestionAfterPause());
        }

        private IEnumerator NextQuestionAfterPause()
        {
            yield return new WaitForSecondsRealtime(0.85f);
            StartQuestion();
        }

        private IEnumerator FinishSessionAfterPause()
        {
            yield return new WaitForSecondsRealtime(1f);
            FlashTrialResult result = session.Result;
            string label = session.CorrectAnswers switch
            {
                3 => "EXCELLENT",
                2 => "GOOD",
                1 => "BAD",
                _ => "FAILED"
            };
            resultText.text = $"RISULTATO {session.CorrectAnswers}/3  -  {label}";
            yield return new WaitForSecondsRealtime(0.9f);
            if (campaignCompleted != null)
            {
                // In campagna la slot mostra il premio deciso dalla stanza, non un'estrazione
                // locale, e la stanza si chiude solo quando i rulli si sono fermati.
                FlashTrialSlotOutcome? campaignOutcome = campaignReward?.Invoke(result, session.CorrectAnswers);
                if (campaignOutcome.HasValue)
                {
                    yield return DebugUi.ExitToLeft(quizPanel);
                    quizPanel.gameObject.SetActive(false);
                    slotPanel.gameObject.SetActive(true);
                    SetSlotContentsVisible(false);
                    yield return DebugUi.EnterFromTop(slotPanel);
                    SetSlotContentsVisible(true);
                    yield return RollSlot(result, campaignOutcome.Value);
                }
                campaignCompleted(result, session.CorrectAnswers);
                yield break;
            }
            yield return DebugUi.ExitToLeft(quizPanel);
            quizPanel.gameObject.SetActive(false);
            slotPanel.gameObject.SetActive(true);
            SetSlotContentsVisible(false);
            yield return DebugUi.EnterFromTop(slotPanel);
            SetSlotContentsVisible(true);
            yield return RollSlot(result);
        }

        private IEnumerator RollSlot(FlashTrialResult result,
            FlashTrialSlotOutcome? campaignOutcome = null)
        {
            FlashTrialSlotOutcome outcome;
            if (campaignOutcome.HasValue)
            {
                outcome = campaignOutcome.Value;
            }
            else
            {
                int targetLevels = session.CorrectAnswers switch { 3 => 7, 2 => 5, 1 => 2, _ => 0 };
                var machine = new FlashTrialSlotMachine(questionIndex * 7919 + session.CorrectAnswers + 1);
                List<FlashTrialCardCandidate> candidates = LoadCandidates();
                outcome = candidates.Count > 0
                    ? machine.Roll(result, targetLevels, candidates)
                    : machine.Roll(result, targetLevels);
            }
            HeroClass[] classes = { HeroClass.Assassin, HeroClass.Warrior, HeroClass.Mage,
                HeroClass.Paladin, HeroClass.Rogue, HeroClass.Hunter, HeroClass.Barbarian,
                HeroClass.Necromancer, HeroClass.Priest };
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
                ? outcome.FirstConsumableName +
                  (string.IsNullOrEmpty(outcome.SecondConsumableName) ? "" : " + " + outcome.SecondConsumableName) +
                  $"\n+{outcome.Amount} {currencyLabel}"
                : string.IsNullOrEmpty(outcome.CardId)
                ? $"+{outcome.Amount} {currencyLabel}"
                : $"CARTA {outcome.HeroClass.ToString().ToUpperInvariant()} POTENZA {outcome.Strength}\n" +
                  $"+{outcome.Amount} {currencyLabel}";
            yield return new WaitForSecondsRealtime(1.4f);
            yield return DebugUi.ExitToLeft(slotPanel);
            yield return DebugUi.RevealRewardCard(
                slotPanel.parent, outcome.CardId, waitForContinue: campaignOutcome.HasValue);
        }

        private void BuildUi()
        {
            Canvas canvas = DebugUi.CreateCanvas("Prova Lampo Quiz Debug", transform);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (Screen.height > Screen.width)
            {
                // Su telefono il quiz va impaginato in uno spazio logico portrait.
                scaler.referenceResolution = new Vector2(900f, 1600f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            Font font = DebugUi.Font;
            DebugUi.Background(canvas.transform, GetComponent<QuickChallengeRoomDebugScene>() != null);
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("UI/DeckBuilder/class_icons_atlas"))
                classSprites[sprite.name] = sprite;
            quizPanel = new GameObject("Quiz Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            quizPanel.SetParent(canvas.transform, false);
            DebugUi.SetRect(quizPanel, Vector2.zero, Vector2.one);
            // Il Canvas di debug usa una reference landscape. Su telefono verticale il
            // contenitore veniva quindi ridotto a circa metà schermo: ingrandiamo tutto
            // insieme per preservare l'allineamento fra frame, testi e aree cliccabili.
            slotPanel = new GameObject("Slot Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            slotPanel.SetParent(canvas.transform, false);
            DebugUi.SetRect(slotPanel, Vector2.zero, Vector2.one);
            BuildSlotUi(slotPanel, font);
            slotPanel.gameObject.SetActive(false);

            Image quizFrame = DebugUi.ResourceImage("Quiz Frame", quizPanel,
                "UI/QuickChallenge/quiz_frame");
            Fixed(quizFrame.rectTransform, new Vector2(0f, 0f), new Vector2(900f, 1125f));

            Font titleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC") ?? font;
            titleText = DebugUi.Text("Title", quizPanel, titleFont, 44, FontStyle.Bold);
            titleText.text = "DOMANDA 1/3";
            titleText.color = new Color(1f, 0.82f, 0.3f);
            Fixed(titleText.rectTransform, new Vector2(-33f, 404f), new Vector2(430f, 64f));

            Font countdownFont = Resources.Load<Font>("Fonts/Alegreya") ?? font;
            countdownText = DebugUi.Text("Countdown", quizPanel, countdownFont, 50, FontStyle.Normal);
            Fixed(countdownText.rectTransform, new Vector2(243f, 416f), new Vector2(190f, 80f));
            questionText = DebugUi.Text("Question", quizPanel, font, 34, FontStyle.Bold);
            questionText.color = Color.white;
            Fixed(questionText.rectTransform, new Vector2(0f, 150f), new Vector2(570f, 210f));

            answerButtons = new Button[3];
            answerLabels = new Text[3];
            for (int index = 0; index < 3; index++)
            {
                int captured = index;
                answerButtons[index] = DebugUi.Button("Answer " + index, quizPanel, font, string.Empty);
                answerLabels[index] = answerButtons[index].GetComponentInChildren<Text>();
                Image answerImage = answerButtons[index].GetComponent<Image>();
                answerImage.color = new Color(1f, 1f, 1f, 0f);
                ColorBlock transparentColors = answerButtons[index].colors;
                transparentColors.normalColor = Color.clear;
                transparentColors.highlightedColor = new Color(1f, 1f, 1f, .08f);
                transparentColors.pressedColor = new Color(1f, 1f, 1f, .14f);
                transparentColors.selectedColor = Color.clear;
                answerButtons[index].colors = transparentColors;
                Fixed(answerButtons[index].GetComponent<RectTransform>(),
                    new Vector2(0f, -80f - index * 140f), new Vector2(550f, 100f));
                answerButtons[index].onClick.AddListener(() => Submit(captured));
            }
            resultText = DebugUi.Text("Result", quizPanel, font, 30, FontStyle.Bold);
            resultText.color = new Color(0.72f, 0.82f, 0.94f);
            Fixed(resultText.rectTransform, new Vector2(-2f, 557f), new Vector2(470f, 44f));
            Button forfeit = DebugUi.Button("Forfeit", quizPanel, font, "RINUNCIA");
            DebugUi.SetRect(forfeit.GetComponent<RectTransform>(), new Vector2(0.34f, 0.055f), new Vector2(0.66f, 0.14f));
            ApplyRedCampaignCta(forfeit);
            forfeit.onClick.AddListener(RequestForfeit);
        }

        private void RequestForfeit()
        {
            active = false;
            foreach (Button button in answerButtons)
                button.interactable = false;
            QuickChallengeRoomDebugScene room = GetComponent<QuickChallengeRoomDebugScene>();
            if (room != null)
                room.ShowForfeitPopup();
            else
                campaignCompleted?.Invoke(FlashTrialResult.Forfeited, session?.CorrectAnswers ?? 0);
        }

        private static void ApplyRedCampaignCta(Button button)
        {
            Image image = button.GetComponent<Image>();
            Sprite sprite = Resources.Load<Sprite>("UI/CampaignRestyle/campaign_cta_back_red");
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.font = AccardND.Battlefield.MmoUiTheme.LoreFont;
                label.fontStyle = FontStyle.Normal;
                label.fontSize = 30;
            }
        }

        private void BuildSlotUi(RectTransform parent, Font font)
        {
            Text title = DebugUi.Text("Slot Title", parent, font, 44, FontStyle.Bold);
            title.text = "RICOMPENSA DEL QUIZ"; title.color = new Color(1f, 0.82f, 0.3f);
            DebugUi.SetRect(title.rectTransform, new Vector2(0.15f, 0.82f), new Vector2(0.85f, 0.94f));
            Image slotFrame = DebugUi.ResourceImage("Slot Machine Frame", parent, "UI/QuickChallenge/slot_machine");
            Fixed(slotFrame.rectTransform, Vector2.zero, new Vector2(950f, 380f));
            slotReelContents = DebugUi.CreateSlotReels(parent, new[] { -214f, 0f, 222f }, 200f);
            slotClassImage = DebugUi.Image("Class", slotReelContents[0], Color.white); slotClassImage.preserveAspect = true; DebugUi.SetRect(slotClassImage.rectTransform, Vector2.zero, Vector2.one);
            slotStrengthText = DebugUi.Text("Strength", slotReelContents[1], font, 70, FontStyle.Bold); slotStrengthText.color = Color.white; DebugUi.SetRect(slotStrengthText.rectTransform, Vector2.zero, Vector2.one);
            slotCurrencyText = DebugUi.Text("Currency", slotReelContents[2], font, 42, FontStyle.Bold); slotCurrencyText.color = Color.white; DebugUi.SetRect(slotCurrencyText.rectTransform, Vector2.zero, Vector2.one);
            slotCurrencyImage = DebugUi.ResourceImage("Gold", slotReelContents[2], "UI/Common/gold_coins"); DebugUi.SetRect(slotCurrencyImage.rectTransform, new Vector2(.18f,.18f), new Vector2(.82f,.82f));
            slotRewardText = DebugUi.Text("Reward", parent, font, 28, FontStyle.Bold); slotRewardText.color = new Color(1f, 0.82f, 0.3f);
            Outline rewardOutline = slotRewardText.gameObject.AddComponent<Outline>();
            rewardOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            rewardOutline.effectDistance = new Vector2(2f, -2f);
            DebugUi.SetRect(slotRewardText.rectTransform, new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.35f));
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

        private List<FlashTrialCardCandidate> LoadCandidates()
        {
            CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
            return database == null ? new List<FlashTrialCardCandidate>() : database.Cards
                .Where(card => card != null && card.Category == CardCategory.Monster && card.CanEnterCombat)
                .Select(card => new FlashTrialCardCandidate(card.Id, card.HeroClass, card.Strength)).ToList();
        }
        private Sprite ClassSprite(HeroClass heroClass) { classSprites.TryGetValue("class_" + heroClass.ToString().ToLowerInvariant(), out Sprite sprite); return sprite; }
        private static void Fixed(RectTransform rect, Vector2 pos, Vector2 size) { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = pos; rect.sizeDelta = size; }
    }

    internal static class DebugUi
    {
        private const float RewardFireworksVolume = 0.35f;
        private static Sprite rewardParticleSprite;
        public static Font Font => AccardND.Battlefield.MmoUiTheme.BodyFont
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static Canvas CreateCanvas(string name, Transform parent)
        {
            Canvas canvas = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(parent, false); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true; canvas.sortingOrder = 5000;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }
        public static void Background(Transform parent, bool transparent = false) { Image image = Image("Background", parent, transparent ? new Color(0.01f, 0.015f, 0.03f, 0.05f) : new Color(0.025f, 0.03f, 0.055f, 1f)); SetRect(image.rectTransform, Vector2.zero, Vector2.one); }

        public static Image ResourceImage(string name, Transform parent, string resourcePath)
        {
            Image image = Image(name, parent, Color.white);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                image.preserveAspect = true;
            }
            image.raycastTarget = false;
            return image;
        }

        public static RectTransform[] CreateSlotReels(Transform parent, float[] centers, float size)
        {
            var result = new RectTransform[centers.Length];
            for (int i = 0; i < centers.Length; i++)
            {
                RectTransform mask = new GameObject("Reel Mask " + i, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                mask.SetParent(parent, false); mask.anchorMin = mask.anchorMax = mask.pivot = new Vector2(.5f,.5f); mask.anchoredPosition = new Vector2(centers[i], 0f); mask.sizeDelta = new Vector2(size,size);
                RectTransform content = new GameObject("Falling Content " + i, typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(mask, false); SetRect(content, Vector2.zero, Vector2.one); result[i] = content;
            }
            return result;
        }

        public static void DropReels(RectTransform[] reels, float elapsed, float duration)
        {
            for (int i = 0; i < reels.Length; i++)
            {
                float stop = duration - (reels.Length - 1 - i) * .18f;
                reels[i].anchoredPosition = elapsed >= stop ? Vector2.zero : new Vector2(0f, 300f - Mathf.Repeat(elapsed * 1050f + i * 145f, 600f));
            }
        }

        public static IEnumerator RollSlotReels(
            GameObject owner,
            RectTransform[] reels,
            System.Action<int, bool, bool, bool> updateValues,
            System.Action<int> stopReel,
            float speedMultiplier = 1f)
        {
            const float duration = 2.4f;
            float[] stopTimes = { duration - .72f, duration - .36f, duration };
            AudioSource music = owner.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("SFX/slot_machine");
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.ignoreListenerPause = true;
            if (music.clip != null) music.Play();
            AudioSource reveal = owner.AddComponent<AudioSource>();
            reveal.playOnAwake = false;
            reveal.spatialBlend = 0f;
            reveal.ignoreListenerPause = true;
            AudioClip revealClip = Resources.Load<AudioClip>("SFX/prize_reveal");

            float elapsed = 0f;
            int tick = -1;
            int stopped = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                int current = Mathf.FloorToInt(elapsed / (.075f / speedMultiplier));
                if (current != tick)
                {
                    tick = current;
                    updateValues?.Invoke(tick, elapsed < stopTimes[0], elapsed < stopTimes[1], elapsed < stopTimes[2]);
                }
                while (stopped < 3 && elapsed >= stopTimes[stopped])
                {
                    stopReel?.Invoke(stopped);
                    if (revealClip != null) reveal.PlayOneShot(revealClip);
                    stopped++;
                }
                for (int index = 0; index < reels.Length; index++)
                    reels[index].anchoredPosition = elapsed >= stopTimes[index]
                        ? Vector2.zero
                        : new Vector2(0f, 300f - Mathf.Repeat(elapsed * 1050f * speedMultiplier + index * 145f, 600f));
                yield return null;
            }
            for (int index = 0; index < reels.Length; index++) reels[index].anchoredPosition = Vector2.zero;
            music.Stop();
            Object.Destroy(music);
            Object.Destroy(reveal, revealClip != null ? revealClip.length : 0f);
        }

        public static IEnumerator EnterFromTop(RectTransform panel, float duration = .55f)
        {
            Vector2 start = new Vector2(0f, 1300f);
            panel.anchoredPosition = start;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                panel.anchoredPosition = Vector2.LerpUnclamped(start, Vector2.zero, t);
                yield return null;
            }
            panel.anchoredPosition = Vector2.zero;
        }

        public static IEnumerator ExitToLeft(RectTransform panel, float duration = .55f)
        {
            Vector2 start = panel.anchoredPosition;
            Vector2 end = new Vector2(-2200f, start.y);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                panel.anchoredPosition = Vector2.LerpUnclamped(start, end, t);
                yield return null;
            }
            panel.anchoredPosition = end;
        }

        public static IEnumerator RevealRewardCard(
            Transform parent, string cardId, bool waitForContinue = false)
        {
            CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
            CardDefinition reward = database != null ? database.FindById(cardId) : null;
            if (reward == null) yield break;
            GameConfiguration configuration = Resources.Load<GameConfiguration>("GameConfiguration");
            if (configuration == null) yield break;

            RectTransform root = new GameObject("Quick Challenge Card Reveal", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            root.SetParent(parent, false); SetRect(root, Vector2.zero, Vector2.one); root.SetAsLastSibling();
            CanvasGroup group = root.GetComponent<CanvasGroup>(); group.alpha = 0f; group.blocksRaycasts = false;
            AudioSource fireworksSfx = StartLoopingFireworksSfx(root.gameObject);

            Image veil = Image("Reward Veil", root, new Color(.015f,.01f,.03f,.42f)); SetRect(veil.rectTransform, Vector2.zero, Vector2.one);
            Image halo = RewardImage("Golden Halo", root, new Color(1f,.74f,.22f,.32f));
            FixedReward(halo.rectTransform, Vector2.zero, new Vector2(760f,760f));
            Image ring = RewardImage("Arcane Ring", root, new Color(.38f,.95f,1f,.24f));
            FixedReward(ring.rectTransform, Vector2.zero, new Vector2(620f,620f));

            var sparks = new List<DebugRewardParticle>(96);
            Color[] sparkColors = { new Color(1f,.82f,.25f), new Color(.25f,.9f,1f), new Color(1f,.32f,.62f), new Color(.7f,1f,.42f), new Color(.86f,.62f,1f) };
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 48; i++)
            {
                Image spark = RewardImage("Loot Spark", root, sparkColors[i % sparkColors.Length]);
                RectTransform sparkRect = spark.rectTransform; FixedReward(sparkRect, new Vector2(side * Random.Range(245f,380f), Random.Range(-84f,36f)), Vector2.one * Random.Range(8f,22f));
                float angle = Mathf.Lerp(-74f,74f,(i % 20) / 19f) + (i / 20) * 12f;
                Vector2 direction = Quaternion.Euler(0f,0f,angle) * Vector2.right; direction.x = side * Mathf.Abs(direction.x);
                sparks.Add(new DebugRewardParticle(sparkRect, spark, sparkRect.anchoredPosition, direction.normalized, Random.Range(170f,500f), Random.Range(0f,.34f)));
            }

            Font rewardTitleFont = AccardND.Battlefield.MmoUiTheme.DisplayFont;
            if (rewardTitleFont == null)
                rewardTitleFont = Font;
            Text title = Text("Reward Title", root, rewardTitleFont, 38, FontStyle.Normal);
            title.text = "RICOMPENSA OTTENUTA";
            title.color = new Color(1f, .88f, .48f, 1f);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(.28f, .12f, .02f, .95f);
            titleOutline.effectDistance = new Vector2(3f, -3f);
            Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, .72f);
            titleShadow.effectDistance = new Vector2(0f, -7f);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(.5f, .72f);
            titleRect.pivot = new Vector2(.5f, .5f);
            titleRect.anchoredPosition = new Vector2(0f, 75f);
            titleRect.sizeDelta = new Vector2(780f, 82f);
            titleRect.localScale = Vector3.one * 1.3f;

            PrototypeCardView card = PrototypeCardView.Create(root, reward, configuration);
            card.SetInteractable(false); card.SetLayoutIgnored(true); card.SetAlpha(0f);
            RectTransform rect = card.RectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(330f,492f);
            const float duration = .95f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float appear = Mathf.SmoothStep(0f,1f,Mathf.Clamp01(t/.28f));
                float settle = Mathf.SmoothStep(0f,1f,Mathf.Clamp01((t-.12f)/.46f));
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 8f) * .035f;
                group.alpha = appear; card.SetAlpha(appear);
                rect.anchoredPosition = Vector2.LerpUnclamped(new Vector2(0f,-210f), new Vector2(0f,-18f), settle);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(1.72f, 1.34f * pulse, settle);
                rect.localRotation = Quaternion.Euler(0f,0f,Mathf.LerpUnclamped(-10f, 2f * Mathf.Sin(t * Mathf.PI * 2f), settle));
                halo.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(.55f,1.18f + .08f * Mathf.Sin(t * Mathf.PI * 6f),appear);
                ring.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(.45f,1.42f,appear);
                ring.rectTransform.localRotation = Quaternion.Euler(0f,0f,t * 150f);
                foreach (DebugRewardParticle spark in sparks)
                {
                    float local = Mathf.Clamp01((t - spark.Delay) / .64f); float burst = Mathf.Sin(local * Mathf.PI);
                    Vector2 drift = spark.Direction * spark.Distance * Mathf.SmoothStep(0f,1f,local); drift.y -= 90f * local * local;
                    spark.Rect.anchoredPosition = spark.Origin + drift; spark.Rect.localScale = Vector3.one * Mathf.Lerp(.35f,1.18f,burst);
                    Color color = spark.Image.color; color.a = burst; spark.Image.color = color;
                }
                yield return null;
            }
            group.alpha = 1f; card.SetAlpha(1f); rect.anchoredPosition = new Vector2(0f,-18f); rect.localScale = Vector3.one * 1.34f; rect.localRotation = Quaternion.identity;

            if (!waitForContinue)
                yield break;

            bool continueClicked = false;
            Button continueButton = Button("Continue", root, Font, "CONTINUA");
            SetRect(continueButton.GetComponent<RectTransform>(), new Vector2(.34f, .055f), new Vector2(.66f, .145f));
            Image continueImage = continueButton.GetComponent<Image>();
            Sprite continueSprite = Resources.Load<Sprite>("UI/CampaignRestyle/campaign_cta_confirm_green");
            if (continueImage != null && continueSprite != null)
            {
                continueImage.sprite = continueSprite;
                continueImage.type = UnityEngine.UI.Image.Type.Simple;
                continueImage.preserveAspect = false;
                continueImage.color = Color.white;
            }
            Text continueLabel = continueButton.GetComponentInChildren<Text>();
            if (continueLabel != null)
            {
                continueLabel.font = AccardND.Battlefield.MmoUiTheme.LoreFont;
                continueLabel.fontStyle = FontStyle.Normal;
                continueLabel.fontSize = 30;
            }
            group.blocksRaycasts = true;
            continueButton.onClick.AddListener(() =>
            {
                continueClicked = true;
                if (fireworksSfx != null)
                    fireworksSfx.Stop();
            });
            float fireworksClock = 0f;
            while (!continueClicked)
            {
                fireworksClock += Time.unscaledDeltaTime;
                UpdateLoopingRewardSparks(sparks, fireworksClock);
                halo.rectTransform.localScale = Vector3.one * (1.15f + .06f * Mathf.Sin(fireworksClock * Mathf.PI * 2.4f));
                ring.rectTransform.localScale = Vector3.one * (1.35f + .08f * Mathf.Sin(fireworksClock * Mathf.PI * 3.1f));
                ring.rectTransform.localRotation = Quaternion.Euler(0f, 0f, fireworksClock * 55f);
                yield return null;
            }
        }

        private static AudioSource StartLoopingFireworksSfx(GameObject root)
        {
            AudioClip clip = Resources.Load<AudioClip>("SFX/loot_fireworks");
            if (clip == null)
                return null;
            AudioSource source = root.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.volume = RewardFireworksVolume;
            source.Play();
            return source;
        }

        private static void UpdateLoopingRewardSparks(List<DebugRewardParticle> sparks, float clock)
        {
            for (int index = 0; index < sparks.Count; index++)
            {
                DebugRewardParticle spark = sparks[index];
                float cycle = Mathf.Repeat(clock + index * .037f, 1.65f);
                float local = Mathf.Clamp01((cycle - spark.Delay) / .72f);
                float active = cycle >= spark.Delay && cycle <= spark.Delay + .72f ? 1f : 0f;
                float burst = Mathf.Sin(local * Mathf.PI) * active;
                Vector2 drift = spark.Direction * spark.Distance * Mathf.SmoothStep(0f, 1f, local);
                drift.y -= 90f * local * local;
                spark.Rect.anchoredPosition = spark.Origin + drift;
                spark.Rect.localScale = Vector3.one * Mathf.Lerp(.28f, 1.28f, burst);
                Color color = spark.Image.color;
                color.a = Mathf.Clamp01(burst);
                spark.Image.color = color;
            }
        }

        private static Image RewardImage(string name, Transform parent, Color color) { Image image = Image(name,parent,color); image.sprite = RewardParticleSprite(); image.raycastTarget = false; return image; }
        private static void FixedReward(RectTransform rect, Vector2 position, Vector2 size) { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static Sprite RewardParticleSprite()
        {
            if (rewardParticleSprite != null) return rewardParticleSprite;
            var texture = new Texture2D(32,32,TextureFormat.RGBA32,false);
            for (int y=0;y<32;y++) for(int x=0;x<32;x++) { Vector2 p = new Vector2((x+.5f)/32f-.5f,(y+.5f)/32f-.5f); float a = Mathf.Clamp01(1f-p.magnitude*2f); texture.SetPixel(x,y,new Color(1f,1f,1f,a*a)); }
            texture.Apply(); rewardParticleSprite = Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(.5f,.5f),100f); return rewardParticleSprite;
        }
        private sealed class DebugRewardParticle
        {
            public readonly RectTransform Rect; public readonly Image Image; public readonly Vector2 Origin, Direction; public readonly float Distance, Delay;
            public DebugRewardParticle(RectTransform rect, Image image, Vector2 origin, Vector2 direction, float distance, float delay) { Rect=rect; Image=image; Origin=origin; Direction=direction; Distance=distance; Delay=delay; }
        }
        public static Image Image(string name, Transform parent, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = color; return image; }
        public static Text Text(string name, Transform parent, Font font, int size, FontStyle style) { var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); Text text = go.GetComponent<Text>(); text.font = font; text.fontSize = size; text.fontStyle = style; text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; return text; }
        public static Button Button(string name, Transform parent, Font font, string label) { var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false); go.GetComponent<Image>().color = new Color(0.18f, 0.15f, 0.24f, 0.98f); Text text = Text("Label", go.transform, font, 25, FontStyle.Bold); text.text = label; text.color = Color.white; text.raycastTarget = false; SetRect(text.rectTransform, Vector2.zero, Vector2.one); return go.GetComponent<Button>(); }
        public static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        public static void EnsureEventSystem() { if (Object.FindAnyObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); }
    }
}
