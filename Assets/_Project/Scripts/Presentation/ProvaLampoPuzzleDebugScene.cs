using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    [DisallowMultipleComponent]
    public sealed class ProvaLampoPuzzleDebugScene : MonoBehaviour, IFlashTrialCampaignScene
    {
        [SerializeField, Range(10, 100)] private int shuffleMoves = 50;
        [SerializeField] private int seed = 2048;
        private FlashTrialSlidingPuzzle puzzle;
        private Button[] cells;
        private Sprite[] tileSprites;
        private Text statsText;
        private Text resultText;
        private RectTransform puzzlePanel;
        private RectTransform slotPanel;
        private Image slotClassImage;
        private Text slotStrengthText;
        private Text slotCurrencyText;
        private Image slotCurrencyImage;
        private RectTransform[] slotReelContents;
        private Text slotRewardText;
        private readonly Dictionary<string, Sprite> classSprites = new Dictionary<string, Sprite>();
        private float startedAt;
        private bool active;
        private bool showingPreview;
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
            BuildUi();
            StartPuzzle();
        }

        private void Update()
        {
            if (active) RefreshStats();
        }

        private void StartPuzzle()
        {
            StartCoroutine(StartPuzzleSequence());
        }

        private IEnumerator StartPuzzleSequence()
        {
            seed++;
            ReplaceArtworkTiles(seed);
            puzzle = new FlashTrialSlidingPuzzle(seed);
            active = false;
            showingPreview = true;
            puzzlePanel.gameObject.SetActive(true);
            slotPanel.gameObject.SetActive(false);
            resultText.text = "Memorizza l'immagine";
            RefreshBoard();

            yield return DebugUi.EnterFromTop(puzzlePanel);
            yield return new WaitForSecondsRealtime(3f);

            showingPreview = false;
            puzzle.Shuffle(shuffleMoves);
            startedAt = Time.unscaledTime;
            active = true;
            resultText.text = "Ricostruisci l'immagine";
            RefreshBoard();
        }

        private void Move(int index)
        {
            if (!active || !puzzle.TryMove(index)) return;
            RefreshBoard();
            if (!puzzle.IsSolved) return;
            active = false;
            float seconds = Time.unscaledTime - startedAt;
            FlashTrialResult result = FlashTrialSlidingPuzzle.Evaluate(seconds, puzzle.Moves);
            resultText.text = $"PUZZLE COMPLETATO  -  {result.ToString().ToUpperInvariant()}\n" +
                $"{seconds:0.0} secondi, {puzzle.Moves} mosse";
            if (campaignCompleted != null)
            {
                StartCoroutine(CompleteCampaignTrial(result));
                return;
            }
            StartCoroutine(ShowSlotAfterPause(result));
        }

        /// <summary>
        /// In campagna la slot mostra il premio deciso dalla stanza, non un'estrazione locale,
        /// e la stanza si chiude solo quando i rulli si sono fermati.
        /// </summary>
        private IEnumerator CompleteCampaignTrial(FlashTrialResult result)
        {
            int levels = PuzzleRewardLevels(result);
            FlashTrialSlotOutcome? outcome = campaignReward?.Invoke(result, levels);
            if (outcome.HasValue)
                yield return ShowSlotAfterPause(result, outcome.Value);
            campaignCompleted(result, levels);
        }

        private static int PuzzleRewardLevels(FlashTrialResult result) => result switch
        {
            FlashTrialResult.Perfect => 8,
            FlashTrialResult.Excellent => 7,
            FlashTrialResult.Good => 5,
            _ => 3
        };

        private IEnumerator ShowSlotAfterPause(FlashTrialResult result,
            FlashTrialSlotOutcome? campaignOutcome = null)
        {
            yield return new WaitForSecondsRealtime(1.1f);
            yield return DebugUi.ExitToLeft(puzzlePanel);
            puzzlePanel.gameObject.SetActive(false);
            slotPanel.gameObject.SetActive(true);
            SetSlotContentsVisible(false);
            yield return DebugUi.EnterFromTop(slotPanel);
            SetSlotContentsVisible(true);
            yield return RollSlot(result, campaignOutcome);
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
                int targetLevels = PuzzleRewardLevels(result);
                var machine = new FlashTrialSlotMachine(seed * 37 + puzzle.Moves);
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
                },
                speedMultiplier: 4.5f);
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

        private void RefreshBoard()
        {
            for (int index = 0; index < cells.Length; index++)
            {
                int tile = showingPreview ? index : puzzle.Cells[index];
                Image image = cells[index].GetComponent<Image>();
                bool visible = tile >= 0;
                image.enabled = visible;
                image.sprite = visible && tileSprites != null ? tileSprites[tile] : null;
                // DebugUi.Button nasce con un fondo viola scuro. Image moltiplica quel tint
                // per i pixel dello sprite e rendeva l'artwork quasi nera: una tessera con
                // immagine deve invece essere mostrata con il suo colore originale.
                image.color = visible ? Color.white : new Color(0.09f, 0.075f, 0.11f, 1f);
                Text fallback = cells[index].GetComponentInChildren<Text>();
                fallback.text = visible && image.sprite == null ? (tile + 1).ToString() : string.Empty;
                cells[index].interactable = active && visible;
            }
            RefreshStats();
        }

        private void RefreshStats()
        {
            float seconds = Mathf.Max(0f, Time.unscaledTime - startedAt);
            statsText.text = $"TEMPO  {seconds:0.0}s     MOSSE  {puzzle?.Moves ?? 0}";
        }

        private void BuildUi()
        {
            Canvas canvas = DebugUi.CreateCanvas("Prova Lampo Puzzle Debug", transform);
            Font font = DebugUi.Font;
            DebugUi.Background(canvas.transform, GetComponent<QuickChallengeRoomDebugScene>() != null);
            foreach (Sprite sprite in Resources.LoadAll<Sprite>("UI/DeckBuilder/class_icons_atlas"))
                classSprites[sprite.name] = sprite;
            puzzlePanel = new GameObject("Puzzle Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            puzzlePanel.SetParent(canvas.transform, false);
            DebugUi.SetRect(puzzlePanel, Vector2.zero, Vector2.one);
            slotPanel = new GameObject("Slot Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            slotPanel.SetParent(canvas.transform, false);
            DebugUi.SetRect(slotPanel, Vector2.zero, Vector2.one);
            BuildSlotUi(slotPanel, font);
            slotPanel.gameObject.SetActive(false);

            Font titleFont = AccardND.Battlefield.MmoUiTheme.LoreFont ?? font;
            Text title = DebugUi.Text("Title", puzzlePanel, titleFont, 70, FontStyle.Normal);
            title.text = "PUZZLE";
            title.color = new Color(1f, 0.82f, 0.3f);
            title.rectTransform.anchorMin = new Vector2(0.12f, 0.88f);
            title.rectTransform.anchorMax = new Vector2(0.88f, 0.97f);
            title.rectTransform.offsetMin = new Vector2(0f, -357f);
            title.rectTransform.offsetMax = new Vector2(0f, -357f);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            titleOutline.effectDistance = new Vector2(3f, -3f);
            statsText = DebugUi.Text("Stats", puzzlePanel, font, 35, FontStyle.Bold);
            statsText.color = Color.white;
            statsText.rectTransform.anchorMin = new Vector2(0.3f, 0.8f);
            statsText.rectTransform.anchorMax = new Vector2(0.7f, 0.87f);
            statsText.rectTransform.offsetMin = new Vector2(0f, -239f);
            statsText.rectTransform.offsetMax = new Vector2(0f, -239f);

            RectTransform grid = new GameObject("Puzzle Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(puzzlePanel, false);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.pivot = new Vector2(0.5f, 0.5f);
            grid.anchoredPosition = new Vector2(0f, 35f);
            grid.sizeDelta = new Vector2(570f, 570f);

            cells = new Button[9];
            for (int index = 0; index < 9; index++)
            {
                int captured = index;
                cells[index] = DebugUi.Button("Cell " + index, grid, font, string.Empty);
                RectTransform rect = cells[index].GetComponent<RectTransform>();
                int row = index / 3; int column = index % 3;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(column * 190f, -row * 190f);
                rect.sizeDelta = new Vector2(186f, 186f);
                cells[index].GetComponent<Image>().preserveAspect = false;
                ColorBlock cellColors = cells[index].colors;
                cellColors.disabledColor = Color.white;
                cells[index].colors = cellColors;
                cells[index].onClick.AddListener(() => Move(captured));
            }

            Image puzzleFrame = DebugUi.ResourceImage("Puzzle Frame", puzzlePanel,
                "UI/QuickChallenge/puzzle_frame");
            Fixed(puzzleFrame.rectTransform, new Vector2(0f, 35f), new Vector2(750f, 750f));

            resultText = DebugUi.Text("Result", puzzlePanel, font, 35, FontStyle.Bold);
            resultText.color = new Color(0.72f, 0.82f, 0.94f);
            resultText.rectTransform.anchorMin = new Vector2(0.17f, 0.05f);
            resultText.rectTransform.anchorMax = new Vector2(0.67f, 0.16f);
            resultText.rectTransform.offsetMin = new Vector2(82f, 486f);
            resultText.rectTransform.offsetMax = new Vector2(82f, 486f);
            Button forfeit = DebugUi.Button("Forfeit", puzzlePanel, font, "RINUNCIA");
            DebugUi.SetRect(forfeit.GetComponent<RectTransform>(), new Vector2(0.34f, 0.055f), new Vector2(0.66f, 0.14f));
            Image forfeitImage = forfeit.GetComponent<Image>();
            Sprite forfeitSprite = Resources.Load<Sprite>("UI/CampaignRestyle/campaign_cta_back_red");
            if (forfeitImage != null && forfeitSprite != null)
            {
                forfeitImage.sprite = forfeitSprite;
                forfeitImage.type = Image.Type.Simple;
                forfeitImage.preserveAspect = false;
                forfeitImage.color = Color.white;
            }
            Text forfeitLabel = forfeit.GetComponentInChildren<Text>();
            if (forfeitLabel != null)
            {
                forfeitLabel.font = AccardND.Battlefield.MmoUiTheme.LoreFont;
                forfeitLabel.fontStyle = FontStyle.Normal;
                forfeitLabel.fontSize = 30;
            }
            forfeit.onClick.AddListener(RequestForfeit);
        }

        private void RequestForfeit()
        {
            active = false;
            foreach (Button cell in cells)
                cell.interactable = false;
            QuickChallengeRoomDebugScene room = GetComponent<QuickChallengeRoomDebugScene>();
            if (room != null)
                room.ShowForfeitPopup();
            else
                campaignCompleted?.Invoke(FlashTrialResult.Forfeited, 0);
        }

        private void ReplaceArtworkTiles(int selectionSeed)
        {
            DestroyArtworkTiles();
            tileSprites = CreateArtworkTiles(selectionSeed);
        }

        private static Sprite[] CreateArtworkTiles(int selectionSeed)
        {
            CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
            List<Sprite> artworks = database == null ? new List<Sprite>() : database.Cards
                .Where(card => card != null
                    && card.Artwork != null
                    && card.Category == CardCategory.Monster
                    && (card.Strength == 2 || card.Strength == 3))
                .Select(card => card.Artwork).Distinct().ToList();
            var random = new System.Random(selectionSeed);
            Sprite artwork = artworks.Count > 0 ? artworks[random.Next(artworks.Count)] : null;
            if (artwork == null) return null;
            Rect source = artwork.textureRect;
            float width = source.width / 3f; float height = source.height / 3f;
            var result = new Sprite[9];
            for (int tile = 0; tile < 9; tile++)
            {
                int row = tile / 3; int column = tile % 3;
                Rect rect = new Rect(source.x + column * width, source.y + (2 - row) * height, width, height);
                result[tile] = Sprite.Create(artwork.texture, rect, new Vector2(0.5f, 0.5f), artwork.pixelsPerUnit);
                result[tile].name = "Puzzle Tile " + tile;
            }
            return result;
        }

        private void BuildSlotUi(RectTransform parent, Font font)
        {
            Text title = DebugUi.Text("Slot Title", parent, font, 70, FontStyle.Bold);
            title.text = "RICOMPENSA DEL PUZZLE";
            title.color = new Color(1f, 0.82f, 0.3f);
            title.rectTransform.anchorMin = new Vector2(0.15f, 0.82f);
            title.rectTransform.anchorMax = new Vector2(0.85f, 0.94f);
            title.rectTransform.offsetMin = new Vector2(0f, -256f);
            title.rectTransform.offsetMax = new Vector2(0f, -256f);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            titleOutline.effectDistance = new Vector2(3f, -3f);
            Image frame = DebugUi.ResourceImage("Slot Machine Frame", parent, "UI/QuickChallenge/slot_machine");
            Fixed(frame.rectTransform, Vector2.zero, new Vector2(1120f, 448f));
            slotReelContents = DebugUi.CreateSlotReels(parent, new[] { -265f, 0f, 265f }, 235f);
            slotClassImage = DebugUi.Image("Class", slotReelContents[0], Color.white); slotClassImage.preserveAspect = true; DebugUi.SetRect(slotClassImage.rectTransform, Vector2.zero, Vector2.one);
            slotStrengthText = DebugUi.Text("Strength", slotReelContents[1], font, 70, FontStyle.Bold); slotStrengthText.color = Color.white; DebugUi.SetRect(slotStrengthText.rectTransform, Vector2.zero, Vector2.one);
            slotCurrencyText = DebugUi.Text("Currency", slotReelContents[2], font, 42, FontStyle.Bold); slotCurrencyText.color = Color.white; DebugUi.SetRect(slotCurrencyText.rectTransform, Vector2.zero, Vector2.one);
            slotCurrencyImage = DebugUi.ResourceImage("Gold", slotReelContents[2], "UI/Common/gold_coins"); DebugUi.SetRect(slotCurrencyImage.rectTransform, new Vector2(.18f,.18f), new Vector2(.82f,.82f));
            slotRewardText = DebugUi.Text("Reward", parent, font, 28, FontStyle.Bold);
            slotRewardText.color = new Color(1f, 0.82f, 0.3f);
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

        private static List<FlashTrialCardCandidate> LoadCandidates()
        {
            CardDatabase database = Resources.Load<CardDatabase>("CardDatabase");
            return database == null ? new List<FlashTrialCardCandidate>() : database.Cards
                .Where(card => card != null && card.Category == CardCategory.Monster && card.CanEnterCombat)
                .Select(card => new FlashTrialCardCandidate(card.Id, card.HeroClass, card.Strength)).ToList();
        }

        private Sprite ClassSprite(HeroClass heroClass)
        {
            classSprites.TryGetValue("class_" + heroClass.ToString().ToLowerInvariant(), out Sprite sprite);
            return sprite;
        }

        private static void Fixed(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void OnDestroy()
        {
            DestroyArtworkTiles();
        }

        private void DestroyArtworkTiles()
        {
            if (tileSprites == null) return;
            foreach (Sprite sprite in tileSprites) if (sprite != null) Destroy(sprite);
            tileSprites = null;
        }
    }
}
