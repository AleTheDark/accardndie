using System;
using AccardND.Battlefield;
using AccardND.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Presentation.ReviewPrompt
{
    /// <summary>
    /// Il popup di recensione, costruito a codice come il resto della UI di campagna e
    /// vestito con <see cref="MmoUiTheme"/>.
    ///
    /// Va appeso alla <b>radice del Canvas</b>, non al rect della Safe Area: in battaglia
    /// i due non coincidono e un modale centrato dentro la Safe Area finisce fuori asse
    /// (vedi il pannello messaggi in BattleBoardController.Layout.cs).
    /// </summary>
    public sealed class ReviewPromptView
    {
        private readonly GameObject root;
        private readonly Text titleText;
        private readonly Text bodyText;
        private readonly Button confirmButton;
        private readonly Text confirmLabel;
        private readonly Button dismissButton;
        private readonly Text dismissLabel;
        private readonly Image[] stars = new Image[ReviewPromptPolicy.MaxStars];
        private readonly GameObject starRow;
        private readonly ReviewPromptMode mode;

        private int selectedStars;
        private bool resolved;

        /// <summary>Chiamata quando il popup si chiude: voto scelto e se e' andato allo store.</summary>
        public event Action<int, bool> Completed;

        /// <summary>
        /// Ordine di disegno del popup in gioco: sopra il velo di fine campagna, che sta
        /// a 950.
        /// </summary>
        public const int DefaultSortingOrder = 960;

        private const int TitleFontSize = 50;
        private const int BodyFontSize = 40;

        /// <summary>
        /// Le due CTA della famiglia campagna, le stesse del popup di fine capitolo.
        /// Il rosso e' "back": qui veste il rifiuto, che e' il suo ruolo anche altrove.
        /// </summary>
        private const string ConfirmCtaSprite = "UI/CampaignRestyle/campaign_cta_confirm_green";
        private const string DismissCtaSprite = "UI/CampaignRestyle/campaign_cta_back_red";

        public ReviewPromptView(Transform canvasRoot, ReviewPromptMode mode, int sortingOrder = DefaultSortingOrder)
        {
            this.mode = mode;

            root = new GameObject("Review Prompt Popup", typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(canvasRoot, worldPositionStays: false);

            var overlay = root.GetComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.82f);
            overlay.raycastTarget = true;
            Stretch(root.GetComponent<RectTransform>());

            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            RectTransform dialog = CreatePanel("Review Prompt Dialog", root.transform);
            SetRect(dialog, new Vector2(0.13f, 0.24f), new Vector2(0.87f, 0.76f));

            titleText = CreateText("Review Prompt Title", dialog, TitleFontSize, TextAnchor.MiddleCenter);
            MmoUiTheme.StyleAsTitle(titleText);
            titleText.fontSize = TitleFontSize;
            titleText.resizeTextForBestFit = false;
            titleText.color = MmoUiTheme.Gold;
            SetRect(titleText.rectTransform, new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.93f));

            bodyText = CreateText("Review Prompt Body", dialog, BodyFontSize, TextAnchor.MiddleCenter);
            bodyText.color = new Color(0.88f, 0.94f, 0.97f);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Corpo a corpo fisso: con il best-fit acceso la dimensione richiesta verrebbe
            // riscritta dal fitting. Overflow verticale invece di Truncate, cosi' una
            // traduzione piu' lunga esce dal riquadro invece di sparire a meta' frase.
            bodyText.resizeTextForBestFit = false;
            bodyText.fontSize = BodyFontSize;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            SetRect(bodyText.rectTransform, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.74f));

            starRow = new GameObject("Review Prompt Stars", typeof(RectTransform));
            starRow.transform.SetParent(dialog, worldPositionStays: false);
            SetRect(starRow.GetComponent<RectTransform>(), new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.5f));
            BuildStars(starRow.transform);

            confirmButton = CreateButton("Review Prompt Confirm", dialog, out confirmLabel);
            confirmButton.onClick.AddListener(OnConfirm);
            ApplyCampaignCta(confirmButton, confirmLabel, ConfirmCtaSprite);
            SetRect((RectTransform)confirmButton.transform, new Vector2(0.52f, 0.08f), new Vector2(0.94f, 0.26f));

            // Stesso alone pulsante dei CTA del mercante. Resta acceso: qui non c'e' una
            // fase in cui il bottone c'e' ma non va premuto, quindi non serve spegnerlo.
            AccardND.PvpUi.PvpUiVfx.CreatePulseButton(
                (RectTransform)confirmButton.transform,
                MmoUiTheme.AccentOf(MmoUiTheme.ButtonVariant.Emerald));

            dismissButton = CreateButton("Review Prompt Dismiss", dialog, out dismissLabel);
            dismissButton.onClick.AddListener(OnDismiss);
            ApplyCampaignCta(dismissButton, dismissLabel, DismissCtaSprite);
            SetRect((RectTransform)dismissButton.transform, new Vector2(0.06f, 0.08f), new Vector2(0.48f, 0.26f));

            ApplyAskingState();
        }

        public GameObject GameObject => root;

        /// <summary>Il voto attualmente selezionato: serve alla scena di debug.</summary>
        public int SelectedStars => selectedStars;

        public void Destroy()
        {
            if (root != null)
                UnityEngine.Object.Destroy(root);
        }

        private void BuildStars(Transform parent)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                int index = i;
                var starObject = new GameObject($"Star {i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
                starObject.transform.SetParent(parent, worldPositionStays: false);

                var image = starObject.GetComponent<Image>();
                image.sprite = ReviewStarSprite.Outline;
                image.preserveAspect = true;
                image.color = MmoUiTheme.TextMuted;
                stars[i] = image;

                var button = starObject.GetComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SelectStars(index + 1));
                MmoUiTheme.AddMotion(button);

                // Cinque colonne uguali, con un margine che le stacca fra loro.
                float slot = 1f / stars.Length;
                SetRect(
                    starObject.GetComponent<RectTransform>(),
                    new Vector2(index * slot + 0.015f, 0f),
                    new Vector2((index + 1) * slot - 0.015f, 1f));
            }
        }

        private void SelectStars(int value)
        {
            selectedStars = Mathf.Clamp(value, 0, ReviewPromptPolicy.MaxStars);
            for (int i = 0; i < stars.Length; i++)
            {
                bool lit = i < selectedStars;
                stars[i].sprite = lit ? ReviewStarSprite.Filled : ReviewStarSprite.Outline;
                stars[i].color = lit ? MmoUiTheme.Gold : MmoUiTheme.TextMuted;
            }

            confirmButton.interactable = selectedStars > 0;
        }

        private void ApplyAskingState()
        {
            bool usesStars = mode == ReviewPromptMode.StarGate;
            starRow.SetActive(usesStars);
            titleText.alignment = TextAnchor.MiddleCenter;

            if (usesStars)
            {
                titleText.text = GameText.GetLocalizedFallback(
                    GameTextKeys.ReviewPrompt.RatingTitle,
                    "TI STA PIACENDO?",
                    "ARE YOU ENJOYING IT?",
                    "GEFÄLLT ES DIR?",
                    "¿TE ESTÁ GUSTANDO?",
                    "ÇA TE PLAÎT ?");
                bodyText.text = "Hai appena chiuso il primo capitolo.\nQuante stelle daresti ad AcCard N' Die?";
                confirmLabel.text = "INVIA";
                confirmButton.interactable = false;
                // Senza le stelle il corpo del testo puo' occupare anche la loro fascia.
                SetRect(bodyText.rectTransform, new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.74f));
            }
            else
            {
                titleText.text = GameText.GetLocalizedFallback(
                    GameTextKeys.ReviewPrompt.StoreTitle,
                    "LASCI UNA RECENSIONE?",
                    "LEAVE A REVIEW?",
                    "MÖCHTEST DU UNS BEWERTEN?",
                    "¿QUIERES DEJAR UNA RESEÑA?",
                    "LAISSER UN AVIS ?");
                bodyText.text =
                    "Hai appena chiuso il primo capitolo.\n\nUna recensione sul Play Store "
                    + "aiuta altri giocatori a trovare il gioco.";
                confirmLabel.text = "RECENSISCI";
                confirmButton.interactable = true;
                SetRect(bodyText.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.74f));
            }

            dismissLabel.text = "NON ORA";
        }

        /// <summary>
        /// Schermata di ringraziamento per chi ha votato meno del massimo in
        /// <see cref="ReviewPromptMode.StarGate"/>: il popup non puo' chiudersi e basta,
        /// o il giocatore pensa che il tocco non abbia funzionato.
        /// </summary>
        private void ApplyThanksState()
        {
            starRow.SetActive(false);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = GameText.GetLocalizedFallback(
                GameTextKeys.ReviewPrompt.ThanksTitle,
                "GRAZIE",
                "THANK YOU",
                "DANKE",
                "GRACIAS",
                "MERCI");
            bodyText.text =
                "Il tuo voto resta qui nel gioco.\n\nSe qualcosa non ti ha convinto, scrivilo: "
                + "e' cosi' che il gioco migliora.";
            SetRect(bodyText.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.74f));

            confirmLabel.text = "CHIUDI";
            confirmButton.interactable = true;
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => Resolve(openedStore: false));

            // Il bottone di sinistra resta la CTA rossa in ogni schermata: cambia solo
            // l'etichetta.
            dismissLabel.text = "SCRIVICI";
            dismissButton.onClick.RemoveAllListeners();
            dismissButton.onClick.AddListener(OpenFeedbackMail);
        }

        private void OnConfirm()
        {
            if (resolved)
                return;

            int stars = mode == ReviewPromptMode.StarGate ? selectedStars : ReviewPromptPolicy.MaxStars;
            if (ReviewPromptPolicy.ShouldOpenStore(mode, stars))
            {
                StoreReviewLauncher.OpenStorePage();
                Resolve(openedStore: true);
                return;
            }

            // Meno del punteggio pieno in StarGate: niente store, si ringrazia e si chiude.
            ApplyThanksState();
        }

        private void OnDismiss() => Resolve(openedStore: false);

        private void OpenFeedbackMail()
        {
            string subject = Uri.EscapeDataString($"AcCard N' Die - {selectedStars} stelle");
            Application.OpenURL($"mailto:apesolutionneonvault@gmail.com?subject={subject}");
            Resolve(openedStore: false);
        }

        private void Resolve(bool openedStore)
        {
            if (resolved)
                return;

            resolved = true;
            Completed?.Invoke(selectedStars, openedStore);
        }

        // --- piccoli aiutanti di costruzione ------------------------------------------
        // Duplicano di proposito quelli privati di BattleBoardController: dipendere da un
        // partial da 19.000 righe per creare un'immagine legherebbe questo popup al god
        // object, che e' esattamente cio' che il TECH_DEBT chiede di smettere di fare.

        private static RectTransform CreatePanel(string name, Transform parent)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, worldPositionStays: false);

            var image = panel.GetComponent<Image>();
            image.color = MmoUiTheme.Panel;
            image.raycastTarget = true;

            Sprite sprite = MmoUiTheme.GetPanelSprite();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }

            return panel.GetComponent<RectTransform>();
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor anchor)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, worldPositionStays: false);

            var text = textObject.GetComponent<Text>();
            text.font = MmoUiTheme.BodyFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        /// <summary>
        /// Veste un bottone con una CTA della famiglia campagna. Ricalca
        /// <c>ApplyCampaignRewardContinueStyle</c>, che vive privato dentro
        /// BattleBoardController e da qui non e' raggiungibile: stesso font, stesso
        /// <see cref="Image.Type.Simple"/>, stessa tinta neutra che lascia parlare lo
        /// sprite.
        /// </summary>
        private static void ApplyCampaignCta(Button button, Text label, string spritePath)
        {
            Sprite sprite = LoadSprite(spritePath);
            var image = button.GetComponent<Image>();
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
                button.targetGraphic = image;
            }

            if (label == null)
                return;

            label.font = MmoUiTheme.LoreFont;
            label.fontSize = 30;
            label.fontStyle = FontStyle.Normal;
            label.resizeTextForBestFit = false;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }

        /// <summary>
        /// Come <c>LoadSpriteResource</c> del controller: se l'asset non e' importato
        /// come Sprite si ripiega sulla Texture2D e ne costruisce uno al volo, altrimenti
        /// le CTA importate come texture semplici non comparirebbero.
        /// </summary>
        private static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            if (spriteCache.TryGetValue(resourcePath, out Sprite cached) && cached != null)
                return cached;

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture == null)
                {
                    Debug.LogWarning($"[Recensione] sprite CTA non trovata: {resourcePath}");
                    return null;
                }

                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    pixelsPerUnit: 100f);
                sprite.name = texture.name;
                sprite.hideFlags = HideFlags.DontSave;
            }

            spriteCache[resourcePath] = sprite;
            return sprite;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> spriteCache = new();

        private static Button CreateButton(string name, Transform parent, out Text label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, worldPositionStays: false);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            label = CreateText($"{name} Label", buttonObject.transform, 28, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);

            MmoUiTheme.AddMotion(button);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
