using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    public interface IFlashTrialCampaignScene
    {
        /// <param name="resolveReward">
        /// Restituisce il premio autorevole della run per il risultato ottenuto, oppure null
        /// quando non c'e' premio (rinuncia). La slot mostra questo esito: i rulli non possono
        /// estrarre per conto loro, altrimenti annuncerebbero una carta che la campagna non da'.
        /// </param>
        /// <param name="completed">Chiude la stanza: va chiamato dopo l'animazione della slot.</param>
        void ConfigureForCampaign(
            System.Func<FlashTrialResult, int, FlashTrialSlotOutcome?> resolveReward,
            System.Action<FlashTrialResult, int> completed);
    }

    /// <summary>
    /// Harness della stanza Sfida Veloce completa. Mostra lo scenario reale e permette di
    /// estrarre casualmente o forzare uno dei tre minigiochi disponibili.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuickChallengeRoomDebugScene : MonoBehaviour
    {
        private GameObject landingPanel;
        private Text descriptionText;
        private Button memoryButton;
        private Button quizButton;
        private Button puzzleButton;
        private Button randomButton;
        private Button forfeitButton;
        private GameObject forfeitPopup;
        private System.Func<FlashTrialResult, int, FlashTrialSlotOutcome?> campaignReward;
        private System.Action<FlashTrialResult, int> campaignCompleted;
        private System.Action campaignForfeitWithoutMalus;

        internal void ConfigureForCampaign(
            System.Func<FlashTrialResult, int, FlashTrialSlotOutcome?> resolveReward,
            System.Action<FlashTrialResult, int> completed,
            System.Action forfeitWithoutMalus = null)
        {
            campaignReward = resolveReward;
            campaignCompleted = completed;
            campaignForfeitWithoutMalus = forfeitWithoutMalus;
            memoryButton.gameObject.SetActive(false);
            quizButton.gameObject.SetActive(false);
            puzzleButton.gameObject.SetActive(false);
            AccardND.Ads.AdService.Warm(AccardND.Ads.AdPlacement.FlashTrialForfeit);
        }

        private void Awake()
        {
            DebugUi.EnsureEventSystem();
            BuildLanding();
        }

        private void BuildLanding()
        {
            Canvas canvas = DebugUi.CreateCanvas("Quick Challenge Room Debug", transform);
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            Font font = DebugUi.Font;

            Image background = DebugUi.Image("Scenario Background", canvas.transform, Color.white);
            DebugUi.SetRect(background.rectTransform, Vector2.zero, Vector2.one);
            background.sprite = LoadScenarioBackground();
            background.preserveAspect = false;
            if (background.sprite == null)
                background.color = new Color(0.025f, 0.03f, 0.055f, 1f);

            landingPanel = new GameObject("Quick Challenge Landing", typeof(RectTransform));
            landingPanel.transform.SetParent(canvas.transform, false);
            DebugUi.SetRect(landingPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

            Image veil = DebugUi.Image("Readability Veil", landingPanel.transform, new Color(0.01f, 0.015f, 0.03f, 0.58f));
            DebugUi.SetRect(veil.rectTransform, Vector2.zero, Vector2.one);

            Font titleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC");
            if (titleFont == null)
                titleFont = font;
            Text title = DebugUi.Text("Title", landingPanel.transform, titleFont, 70, FontStyle.Bold);
            title.text = "SFIDA VELOCE";
            title.color = new Color(1f, 0.82f, 0.3f);
            title.rectTransform.anchorMin = new Vector2(0.15f, 0.78f);
            title.rectTransform.anchorMax = new Vector2(0.85f, 0.92f);
            title.rectTransform.offsetMin = new Vector2(0f, -92f);
            title.rectTransform.offsetMax = new Vector2(0f, -92f);
            Outline titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            titleOutline.effectDistance = new Vector2(3f, -3f);

            descriptionText = DebugUi.Text("Description", landingPanel.transform, font, 40, FontStyle.Bold);
            descriptionText.text = "Supera la sfida per ottenere una ricompensa: più veloce e accurato sarai, maggiore sarà il premio.\n" +
                "In caso di rinuncia viene applicato un malus nella prossima stanza.";
            descriptionText.color = Color.white;
            descriptionText.rectTransform.anchorMin = new Vector2(0.2f, 0.61f);
            descriptionText.rectTransform.anchorMax = new Vector2(0.8f, 0.76f);
            descriptionText.rectTransform.offsetMin = new Vector2(0f, -733f);
            descriptionText.rectTransform.offsetMax = new Vector2(0f, -733f);

            randomButton = DebugUi.Button("Random Challenge", landingPanel.transform, font, "SFIDA");
            DebugUi.SetRect(randomButton.GetComponent<RectTransform>(), new Vector2(0.12f, 0.16f), new Vector2(0.48f, 0.25f));
            ApplyCampaignCta(randomButton, "UI/CampaignRestyle/campaign_cta_confirm_green");
            randomButton.onClick.AddListener(StartRandomChallenge);

            memoryButton = DebugUi.Button("Memory", landingPanel.transform, font, "MEMORY");
            DebugUi.SetRect(memoryButton.GetComponent<RectTransform>(), new Vector2(0.18f, 0.34f), new Vector2(0.38f, 0.43f));
            memoryButton.onClick.AddListener(() => StartChallenge<ProvaLampoMemoryDebugScene>());

            quizButton = DebugUi.Button("Quiz", landingPanel.transform, font, "QUIZ");
            DebugUi.SetRect(quizButton.GetComponent<RectTransform>(), new Vector2(0.4f, 0.34f), new Vector2(0.6f, 0.43f));
            quizButton.onClick.AddListener(() => StartChallenge<ProvaLampoQuizDebugScene>());

            puzzleButton = DebugUi.Button("Puzzle", landingPanel.transform, font, "PUZZLE 3×3");
            DebugUi.SetRect(puzzleButton.GetComponent<RectTransform>(), new Vector2(0.62f, 0.34f), new Vector2(0.82f, 0.43f));
            puzzleButton.onClick.AddListener(() => StartChallenge<ProvaLampoPuzzleDebugScene>());

            forfeitButton = DebugUi.Button("Forfeit", landingPanel.transform, font, "RINUNCIA");
            DebugUi.SetRect(forfeitButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.16f), new Vector2(0.88f, 0.25f));
            ApplyCampaignCta(forfeitButton, "UI/CampaignRestyle/campaign_cta_back_red");
            forfeitButton.onClick.AddListener(ShowForfeitPopup);

            BuildForfeitPopup(canvas.transform, font);
        }

        private void StartRandomChallenge()
        {
            switch (Random.Range(0, 3))
            {
                case 0: StartChallenge<ProvaLampoMemoryDebugScene>(); break;
                case 1: StartChallenge<ProvaLampoQuizDebugScene>(); break;
                default: StartChallenge<ProvaLampoPuzzleDebugScene>(); break;
            }
        }

        private void StartChallenge<T>() where T : MonoBehaviour
        {
            if (GetComponent<T>() != null)
                return;
            if (landingPanel != null)
                landingPanel.SetActive(false);
            try
            {
                T challenge = gameObject.AddComponent<T>();
                if (challenge == null)
                    throw new MissingComponentException($"Impossibile avviare {typeof(T).Name}");
                if (campaignCompleted != null && challenge is IFlashTrialCampaignScene campaignScene)
                    campaignScene.ConfigureForCampaign(campaignReward, campaignCompleted);
            }
            catch (System.Exception exception)
            {
                if (landingPanel != null)
                    landingPanel.SetActive(true);
                Debug.LogException(exception, this);
            }
        }

        private void BuildForfeitPopup(Transform parent, Font font)
        {
            forfeitPopup = new GameObject("Forfeit Confirmation", typeof(RectTransform),
                typeof(Canvas), typeof(GraphicRaycaster));
            forfeitPopup.transform.SetParent(parent, false);
            DebugUi.SetRect(forfeitPopup.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            Canvas popupCanvas = forfeitPopup.GetComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 6000;

            Image veil = DebugUi.Image("Popup Veil", forfeitPopup.transform, new Color(0f, 0f, 0f, .78f));
            DebugUi.SetRect(veil.rectTransform, Vector2.zero, Vector2.one);
            Image panel = DebugUi.Image("Popup Panel", forfeitPopup.transform, new Color(1f, 1f, 1f, .98f));
            panel.sprite = AccardND.Battlefield.MmoUiTheme.GetPanelSprite();
            panel.type = Image.Type.Sliced;
            DebugUi.SetRect(panel.rectTransform, new Vector2(.12f, .28f), new Vector2(.88f, .72f));
            AccardND.Battlefield.MmoUiTheme.AddPanelGem(
                panel.rectTransform,
                "Panel Crystal",
                new Vector2(.5f, 1f),
                new Vector2(28f, 28f),
                new Color(.82f, .96f, 1f, .78f));

            Font titleFont = Resources.Load<Font>("Fonts/IMFellEnglishSC") ?? font;
            Text title = DebugUi.Text("Title", panel.transform, titleFont, 58, FontStyle.Bold);
            title.text = "RINUNCIA";
            title.color = new Color(1f, .82f, .3f);
            DebugUi.SetRect(title.rectTransform, new Vector2(.08f, .7f), new Vector2(.92f, .92f));

            Text message = DebugUi.Text("Message", panel.transform, font, 32, FontStyle.Bold);
            message.text = "Se rinunci con malus, guadagnerai oro ed esperienza dimezzati nel prossimo combattimento.";
            message.color = Color.white;
            DebugUi.SetRect(message.rectTransform, new Vector2(.1f, .38f), new Vector2(.9f, .7f));

            Button withoutMalus = DebugUi.Button("Without Malus", panel.transform, font, "SENZA MALUS");
            DebugUi.SetRect(withoutMalus.GetComponent<RectTransform>(), new Vector2(.08f, .1f), new Vector2(.48f, .32f));
            ApplyCampaignCta(withoutMalus, "UI/CampaignRestyle/campaign_cta_confirm_green");
            withoutMalus.onClick.AddListener(() => ForfeitWithoutMalus(withoutMalus, message));

            Button withMalus = DebugUi.Button("With Malus", panel.transform, font, "CON MALUS");
            DebugUi.SetRect(withMalus.GetComponent<RectTransform>(), new Vector2(.52f, .1f), new Vector2(.92f, .32f));
            ApplyCampaignCta(withMalus, "UI/CampaignRestyle/campaign_cta_back_red");
            withMalus.onClick.AddListener(ForfeitWithMalus);
            forfeitPopup.SetActive(false);
        }

        internal void ShowForfeitPopup()
        {
            if (forfeitPopup != null)
                forfeitPopup.SetActive(true);
        }

        private async void ForfeitWithoutMalus(Button button, Text message)
        {
            button.interactable = false;
            AccardND.Ads.AdResult result = await AccardND.Ads.AdService.ShowAsync(
                AccardND.Ads.AdPlacement.FlashTrialForfeit, asGate: true);
            button.interactable = true;
            if (!result.Grants)
            {
                message.text = "Annuncio non disponibile. Riprova oppure continua con il malus.";
                return;
            }

            if (campaignForfeitWithoutMalus != null)
                campaignForfeitWithoutMalus();
            else if (campaignCompleted != null)
                campaignCompleted(FlashTrialResult.Forfeited, 0);
            else
                forfeitPopup.SetActive(false);
        }

        private void ForfeitWithMalus()
        {
            if (campaignCompleted != null)
                campaignCompleted(FlashTrialResult.Forfeited, 0);
            else
                forfeitPopup.SetActive(false);
        }

        private static void ApplyCampaignCta(Button button, string resourcePath)
        {
            Image image = button.GetComponent<Image>();
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
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

        private static Sprite LoadScenarioBackground()
        {
            ScenarioCatalog catalog = Resources.Load<ScenarioCatalog>("ScenarioCatalog");
            ScenarioDefinition scenario = catalog != null ? catalog.FindById("quick_challenge") : null;
            if (scenario == null)
                return null;
            bool landscape = (float)Screen.width / Mathf.Max(1, Screen.height) >= 1.2f;
            return landscape && scenario.BackgroundLandscape != null
                ? scenario.BackgroundLandscape
                : scenario.Background;
        }
    }
}
