using System.Threading.Tasks;
using AccardND.Battlefield;
using AccardND.Localization;
using AccardND.Network;
using AccardND.NetProtocol;
using AccardND.PvpUi;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AccardND.UI
{
    /// <summary>Schermata iniziale: prepara gli update, autentica il giocatore e apre il gioco.</summary>
    public sealed class LoginScreenPrototype : MonoBehaviour
    {
        private const string BackgroundResource = "UI/Login/background_main_screen";
        private const string AnonymousButtonResource = "UI/CampaignRestyle/campaign_cta_blue";
        private const string RankedButtonResource = "UI/MultiplayerRestyle/ranked_cta_frame_v3";
        private const string MainSceneName = "MainScene";
        private const string ServerUrl = "wss://accardndie.com/ws";
        private const string NicknamePrefsKey = "AccardND.PvpNickname";
        private const string PlayerHudNamePrefsKey = "AccardND.PlayerHudName";
        private const string GuestModePrefsKey = "AccardND.GuestMode";
        private const string LoginProviderPrefsKey = "AccardND.LoginProvider";
        private const string GoogleLoginProvider = "google";
        private const int NicknameMaxLength = 18;
        private static Sprite anonymousButtonSprite;
        private static Sprite rankedButtonSprite;
        private Text statusText;
        private InputField nicknameInput;
        private Button loginButton;
        private Button nicknameConfirmButton;
        private GameObject updateOverlay;
        private Text updateMessageText;
        private Button updateActionButton;
        private string updateUrl;
        private bool busy;
        private PvpServerClient serverClient;
        private TaskCompletionSource<string> pendingNickname;
        private string signedInAccessToken;

        private void Awake()
        {
            BuildInterface();
        }

        private async void Start()
        {
            // Ripristina soltanto sessioni create da un accesso Google confermato.
            // Token delle versioni precedenti o di origine incerta non devono
            // saltare la scelta dell'account.
            if (PlayerPrefs.GetString(LoginProviderPrefsKey, string.Empty) == GoogleLoginProvider)
                await TryResumeSessionAsync();
            else
            {
                SetStatus(GameText.Get(GameTextKeys.Login.GoogleRequired));
                SetButtonsInteractable(true);
            }
        }

        /// <summary>
        /// Se esiste una sessione UGS salvata la riusa e va dritto nel gioco, senza
        /// bottoni né prompt Google. Altrimenti mostra i bottoni di login come al solito.
        /// </summary>
        private async Task TryResumeSessionAsync()
        {
            if (busy || !PvpUgsAuth.IsAvailable)
                return;

            busy = true;
            SetButtonsInteractable(false);
            try
            {
                SetStatus(GameText.Get(GameTextKeys.Login.RestoringSession));
                (string accessToken, string provider) = await PvpUgsAuth.TryResumeSessionAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    SetStatus(GameText.Get(GameTextKeys.Login.GoogleRequired));
                    SetButtonsInteractable(true);
                    busy = false;
                    return;
                }

                if (!PvpUgsAuth.IsCurrentSessionGoogle())
                {
                    PvpUgsAuth.ForgetSession();
                    PlayerPrefs.DeleteKey(LoginProviderPrefsKey);
                    PlayerPrefs.Save();
                    SetStatus(GameText.Get(GameTextKeys.Login.GoogleRequired));
                    SetButtonsInteractable(true);
                    busy = false;
                    return;
                }

                signedInAccessToken = accessToken;
                SetGuestMode(false);
                SetStatus(GameText.Format(GameTextKeys.Login.WelcomeBack, provider));
                if (await ConnectAccountServerAsync(false))
                    OpenMainScene();
            }
            catch (System.Exception exception)
            {
                // Sessione scaduta o server non raggiungibile: si ricade sui bottoni.
                Debug.LogWarning($"[Login] Ripristino sessione fallito: {exception.Message}");
                SetStatus(GameText.Get(GameTextKeys.Login.GoogleRequired));
                SetButtonsInteractable(true);
                busy = false;
            }
        }

        private void BuildInterface()
        {
            EnsureEventSystem();

            Canvas canvas = CreateObject<Canvas>("Login Canvas", transform);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(944f, 1676f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateObject<Image>("Background", canvas.transform);
            background.sprite = Resources.Load<Sprite>(BackgroundResource);
            background.color = Color.white;
            Stretch(background.rectTransform);
            AspectRatioFitter backgroundFitter = background.gameObject.AddComponent<AspectRatioFitter>();
            backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundFitter.aspectRatio = 944f / 1676f;

            Image shade = CreateObject<Image>("Readability Shade", canvas.transform);
            shade.color = new Color(0.01f, 0.005f, 0.03f, 0.28f);
            shade.raycastTarget = false;
            Stretch(shade.rectTransform);

            RectTransform panel = CreatePanel(canvas.transform);
            CreateTitle(panel, GameText.Get(GameTextKeys.Login.Title), new Vector2(0f, 225f), 46);
            CreateSubtitle(panel, GameText.Get(GameTextKeys.Login.Subtitle), new Vector2(0f, 177f), 18);

            statusText = CreateText(
                panel,
                "Status",
                GameText.Get(GameTextKeys.Login.InitialStatus),
                24,
                TextAnchor.MiddleCenter,
                new Color(0.88f, 0.92f, 0.96f));
            MmoUiTheme.StyleAsScreenTitle(statusText);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(statusText.rectTransform, new Vector2(0f, 52f), new Vector2(540f, 120f));

            nicknameInput = CreateInput(panel, "Nickname", GameText.Get(GameTextKeys.Login.NicknamePlaceholder), new Vector2(0f, -45f), false);
            nicknameInput.characterLimit = NicknameMaxLength;
            nicknameInput.gameObject.SetActive(false);
            nicknameInput.onEndEdit.AddListener(_ => SubmitNickname());

            loginButton = CreateButton(panel, "Login", GameText.Get(GameTextKeys.Login.GoogleButton), new Vector2(0f, -105f), 27);
            nicknameConfirmButton = CreateButton(panel, "Confirm Nickname", GameText.Get(GameTextKeys.Common.Confirm), new Vector2(0f, -195f), 28);
            nicknameConfirmButton.gameObject.SetActive(false);

            loginButton.onClick.AddListener(StartGoogleFlow);
            nicknameConfirmButton.onClick.AddListener(SubmitNickname);

            BuildUpdateOverlay(canvas.transform);
        }

        /// <summary>
        /// Avviso di aggiornamento: nasce spento e copre tutta la schermata quando il
        /// server rifiuta la versione. Da qui non si prosegue, quindi non ha un tasto
        /// per chiuderlo: l'unica azione è andare a prendere la build nuova.
        /// </summary>
        private void BuildUpdateOverlay(Transform canvasParent)
        {
            Image scrim = CreateObject<Image>("Update Overlay", canvasParent);
            scrim.color = new Color(0.02f, 0.01f, 0.05f, 0.86f);
            scrim.raycastTarget = true;
            Stretch(scrim.rectTransform);
            updateOverlay = scrim.gameObject;

            Image panel = CreateObject<Image>("Update Panel", scrim.transform);
            panel.sprite = MmoUiTheme.GetPanelSprite();
            panel.type = Image.Type.Sliced;
            panel.color = new Color(0.16f, 0.09f, 0.24f, 0.98f);
            SetRect(panel.rectTransform, Vector2.zero, new Vector2(660f, 540f));

            CreateTitle(panel.transform, GameText.Get(GameTextKeys.Login.UpdateTitle), new Vector2(0f, 185f), 42);

            updateMessageText = CreateText(
                panel.transform,
                "Update Message",
                string.Empty,
                26,
                TextAnchor.UpperCenter,
                new Color(0.9f, 0.93f, 0.97f));
            updateMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(updateMessageText.rectTransform, new Vector2(0f, 45f), new Vector2(570f, 200f));

            updateActionButton = CreateButton(panel.transform, "Update", GameText.Get(GameTextKeys.Login.UpdateNow), new Vector2(0f, -150f), 30);
            updateActionButton.onClick.AddListener(OpenUpdateUrl);

            updateOverlay.SetActive(false);
        }

        /// <summary>
        /// Il server ha rifiutato l'accesso perché la build è vecchia. Si resta sulla
        /// schermata di login: niente bottone Google (riprovare darebbe lo stesso
        /// rifiuto), solo l'avviso di aggiornare.
        /// </summary>
        private void ShowUpdateRequired(AuthResponse auth)
        {
            serverClient?.Dispose();
            serverClient = null;
            signedInAccessToken = null;

            updateUrl = auth?.updateUrl;
            string required = string.IsNullOrWhiteSpace(auth?.requiredVersion)
                ? GameText.Get(GameTextKeys.Login.UpdateRequiredGeneric)
                : GameText.Format(GameTextKeys.Login.UpdateRequiredVersion, auth.requiredVersion);
            if (updateMessageText != null)
            {
                updateMessageText.text = GameText.Format(
                    GameTextKeys.Login.UpdateMessage,
                    GameVersion.Current,
                    required);
            }

            if (updateActionButton != null)
                updateActionButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(updateUrl));

            SetNicknameUiVisible(false);
            if (loginButton != null)
                loginButton.gameObject.SetActive(false);
            SetStatus(GameText.Get(GameTextKeys.Login.VersionOutdated));
            if (updateOverlay != null)
                updateOverlay.SetActive(true);

            // busy resta true: da questa schermata non si entra in nessun caso.
        }

        private void OpenUpdateUrl()
        {
            if (!string.IsNullOrWhiteSpace(updateUrl))
                Application.OpenURL(updateUrl);
        }

        private async void StartGoogleFlow()
        {
            await StartOnlineFlowAsync();
        }

        private async Task StartOnlineFlowAsync()
        {
            if (busy)
                return;

            busy = true;
            SetButtonsInteractable(false);
            try
            {
                PlayerPrefs.DeleteKey(LoginProviderPrefsKey);
                PlayerPrefs.Save();
                SetGuestMode(false);
                await CheckForUpdatesAsync();
                await AuthenticateWithGoogleAsync();
                if (await ConnectAccountServerAsync(true))
                    OpenMainScene();
            }
            catch (System.Exception exception)
            {
                SetStatus(GameText.Format(GameTextKeys.Login.AccessFailed, exception.Message));
                SetButtonsInteractable(true);
                busy = false;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            SetStatus(GameText.Get(GameTextKeys.Login.CheckingUpdates));
            await PvpAsync.NextFrameAsync();

            // Punto di estensione per Play Asset Delivery / Addressables.
            // Google Play aggiorna gia' l'APK/AAB; qui in futuro scaricheremo asset bundle extra.
            await WaitSecondsAsync(0.35f);
            SetStatus(GameText.Get(GameTextKeys.Login.Updated));
        }

        private async Task AuthenticateWithGoogleAsync()
        {
            SetStatus(GameText.Format(GameTextKeys.Login.AccessingProvider, GoogleLoginLabel()));
            if (!PvpUgsAuth.IsAvailable)
            {
                throw new System.InvalidOperationException(GameText.Get(GameTextKeys.Login.AuthenticationUnavailable));
            }

            (string accessToken, string provider) = await PvpUgsAuth.SignInWithGoogleAsync();
            if (string.IsNullOrEmpty(accessToken))
                throw new System.InvalidOperationException(provider ?? GameText.Get(GameTextKeys.Login.MissingToken));

            signedInAccessToken = accessToken;
            PlayerPrefs.SetString(LoginProviderPrefsKey, GoogleLoginProvider);
            PlayerPrefs.Save();
            SetStatus(GameText.Format(GameTextKeys.Login.AccessComplete, provider));
            await WaitSecondsAsync(0.25f);
        }

        private static string GoogleLoginLabel()
        {
#if UNITY_EDITOR
            return "Unity Authentication";
#else
            return "Google";
#endif
        }

        private async Task<bool> ConnectAccountServerAsync(bool allowNicknameSelection)
        {
            SetStatus(GameText.Get(GameTextKeys.Login.CheckingAccount));
            serverClient?.Dispose();
            serverClient = new PvpServerClient();
            await serverClient.ConnectAsync(ServerUrl);

            if (string.IsNullOrEmpty(signedInAccessToken))
                throw new System.InvalidOperationException(GameText.Get(GameTextKeys.Login.MissingAccountToken));

            await serverClient.SendAsync(MessageTypes.AuthUgs, new UgsLoginRequest
            {
                accessToken = signedInAccessToken,
                displayName = string.Empty,
                authMethod = PvpUgsAuth.CurrentAuthMethod(),
                googleIdToken = PvpUgsAuth.LastGoogleIdToken,
                clientVersion = GameVersion.Current
            });

            AuthResponse auth = await WaitForMessageAsync<AuthResponse>(MessageTypes.AuthResponse, 12f);
            if (auth is { requiresUpdate: true })
            {
                ShowUpdateRequired(auth);
                return false;
            }

            if (auth == null || !auth.ok)
                throw new System.InvalidOperationException(auth?.error ?? GameText.Get(GameTextKeys.Login.AccountServerUnavailable));

            if (auth.requiresNickname)
            {
                if (!allowNicknameSelection)
                {
                    serverClient.Dispose();
                    serverClient = null;
                    signedInAccessToken = null;
                    PvpUgsAuth.ForgetSession();
                    PlayerPrefs.DeleteKey(LoginProviderPrefsKey);
                    PlayerPrefs.Save();
                    SetStatus(GameText.Get(GameTextKeys.Login.GoogleAccountRequired));
                    SetButtonsInteractable(true);
                    busy = false;
                    return false;
                }

                auth.username = await RequireNicknameAsync();
                auth.requiresNickname = false;
            }
            else
                SaveNickname(auth.username);

            AccountServerSession.Adopt(
                serverClient,
                new PvpServerMessageDispatcher(serverClient),
                auth,
                ServerUrl,
                BuildGoogleRefreshRequestAsync);
            serverClient = null;
            return true;
        }

        private static async Task<UgsLoginRequest> BuildGoogleRefreshRequestAsync()
        {
            var resume = await PvpUgsAuth.TryResumeSessionAsync();
            string accessToken = resume.AccessToken;
            return string.IsNullOrEmpty(accessToken)
                ? null
                : new UgsLoginRequest
                {
                    accessToken = accessToken,
                    displayName = string.Empty,
                    authMethod = PvpUgsAuth.CurrentAuthMethod(),
                    googleIdToken = PvpUgsAuth.LastGoogleIdToken,
                    clientVersion = GameVersion.Current
                };
        }

        private async Task<string> RequireNicknameAsync()
        {
            SetStatus(GameText.Get(GameTextKeys.Login.ChooseNickname));
            SetNicknameUiVisible(true);

            while (true)
            {
                pendingNickname = new TaskCompletionSource<string>();
                string nickname = await pendingNickname.Task;
                nickname = SanitizeNickname(nickname);
                if (nickname.Length < 3)
                {
                    SetStatus(GameText.Get(GameTextKeys.Login.NicknameTooShort));
                    continue;
                }

                SetStatus(GameText.Get(GameTextKeys.Login.CheckingNickname));
                await serverClient.SendAsync(MessageTypes.NicknameSet, new SetNicknameRequest
                {
                    nickname = nickname
                });

                NicknameResponse response = await WaitForMessageAsync<NicknameResponse>(MessageTypes.NicknameResponse, 8f);
                if (response is { ok: true })
                {
                    SaveNickname(response.nickname);
                    SetStatus(GameText.Format(GameTextKeys.Login.Welcome, response.nickname));
                    SetNicknameUiVisible(false);
                    await WaitSecondsAsync(0.35f);
                    return response.nickname;
                }

                SetStatus(response?.error ?? GameText.Get(GameTextKeys.Login.NicknameUnavailable));
            }
        }

        private async Task<T> WaitForMessageAsync<T>(string messageType, float timeoutSeconds) where T : class
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                while (serverClient != null && serverClient.TryDequeueMessage(out Envelope envelope))
                {
                    if (envelope.type == MessageTypes.Error)
                    {
                        ErrorMessage error = PvpServerClient.ParsePayload<ErrorMessage>(envelope);
                        throw new System.InvalidOperationException(error?.message ?? GameText.Get(GameTextKeys.Login.ServerError));
                    }

                    // Il server offre subito la partita rimasta in piedi, anche mentre qui
                    // si sta ancora aspettando la risposta di login: buttarla via
                    // significherebbe far rientrare il giocatore in lobby a partita viva.
                    if (envelope.type == MessageTypes.MatchResume)
                    {
                        AccountServerSession.StashMatchResume(
                            PvpServerClient.ParsePayload<MatchResumeState>(envelope));
                        continue;
                    }

                    if (envelope.type == messageType)
                        return PvpServerClient.ParsePayload<T>(envelope);
                }

                await PvpAsync.NextFrameAsync();
            }

            throw new System.TimeoutException(GameText.Get(GameTextKeys.Login.ServerTimeout));
        }

        private void SubmitNickname()
        {
            if (pendingNickname == null || pendingNickname.Task.IsCompleted)
                return;
            pendingNickname.TrySetResult(nicknameInput != null ? nicknameInput.text : string.Empty);
        }

        private void SetNicknameUiVisible(bool visible)
        {
            if (nicknameInput != null)
            {
                nicknameInput.gameObject.SetActive(visible);
                if (visible)
                    nicknameInput.ActivateInputField();
            }

            if (nicknameConfirmButton != null)
                nicknameConfirmButton.gameObject.SetActive(visible);
            if (loginButton != null)
                loginButton.gameObject.SetActive(!visible);
        }

        private void OpenMainScene()
        {
            SetStatus(GameText.Get(GameTextKeys.Login.OpeningMenu));
            SceneManager.LoadScene(MainSceneName);
        }

        private static void SaveNickname(string nickname)
        {
            nickname = SanitizeNickname(nickname);
            if (string.IsNullOrWhiteSpace(nickname))
                return;
            PlayerPrefs.SetString(NicknamePrefsKey, nickname);
            SavePlayerHudName(nickname);
            PlayerPrefs.Save();
        }

        private static void SavePlayerHudName(string displayName)
        {
            PlayerPrefs.SetString(PlayerHudNamePrefsKey, string.IsNullOrWhiteSpace(displayName) ? "Guest" : displayName.Trim());
            PlayerPrefs.Save();
        }

        private static void SetGuestMode(bool enabled)
        {
            PlayerPrefs.SetInt(GuestModePrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void SetStatus(string value)
        {
            if (statusText != null)
                statusText.text = value;
            Debug.Log($"[Login] {value}");
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (loginButton != null)
                loginButton.interactable = interactable;
        }

        private static string SanitizeNickname(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var builder = new System.Text.StringBuilder(NicknameMaxLength);
            foreach (char character in raw.Trim())
            {
                if (builder.Length >= NicknameMaxLength)
                    break;
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
                    builder.Append(character);
            }
            return builder.ToString();
        }

        private static async Task WaitSecondsAsync(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
                await PvpAsync.NextFrameAsync();
        }

        private void OnDestroy()
        {
            serverClient?.Dispose();
            serverClient = null;
        }

        private static RectTransform CreatePanel(Transform parent)
        {
            Image image = CreateObject<Image>("Login Panel", parent);
            image.sprite = MmoUiTheme.GetPanelSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.12f, 0.08f, 0.22f, 0.92f);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-2.4519f, 3.741f);
            rect.sizeDelta = new Vector2(654.9038f, 765.4821f);
            return rect;
        }

        private static InputField CreateInput(Transform parent, string name, string placeholder, Vector2 position, bool password)
        {
            Image frame = CreateObject<Image>(name, parent);
            frame.sprite = MmoUiTheme.GetSoftPanelSprite();
            frame.type = Image.Type.Sliced;
            frame.color = new Color(0.2f, 0.16f, 0.32f, 0.98f);
            SetRect(frame.rectTransform, position, new Vector2(510f, 58f));

            InputField input = frame.gameObject.AddComponent<InputField>();
            Text text = CreateText(frame.transform, "Text", string.Empty, 30, TextAnchor.MiddleLeft, Color.white);
            Text hint = CreateText(frame.transform, "Placeholder", placeholder, 26, TextAnchor.MiddleLeft, new Color(0.72f, 0.65f, 0.78f, 0.72f));
            SetStretch(text.rectTransform, 24f, 18f);
            SetStretch(hint.rectTransform, 24f, 18f);
            input.textComponent = text;
            input.placeholder = hint;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.lineType = InputField.LineType.SingleLine;
            input.targetGraphic = frame;
            return input;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, int fontSize = 34)
        {
            Image image = CreateObject<Image>(name, parent);
            MmoUiTheme.ButtonVariant variant = name == "Confirm Nickname"
                ? MmoUiTheme.ButtonVariant.Emerald
                : name == "Login"
                    ? MmoUiTheme.ButtonVariant.Gold
                    : MmoUiTheme.ButtonVariant.Arcane;
            image.sprite = MmoUiTheme.GetButtonSprite(variant);
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = new Color(1f, 1f, 1f, 1f);
            SetRect(image.rectTransform, position, new Vector2(550f, 145f));

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);

            Text text = CreateText(
                image.transform,
                "Label",
                label,
                fontSize,
                TextAnchor.MiddleCenter,
                Color.Lerp(Color.white, MmoUiTheme.AccentOf(variant), 0.12f));
            MmoUiTheme.StyleAsScreenTitle(text);
            text.fontStyle = FontStyle.Normal;
            Shadow goldDepth = text.gameObject.AddComponent<Shadow>();
            goldDepth.effectColor = new Color(0f, 0f, 0f, 0.9f);
            goldDepth.effectDistance = new Vector2(1.5f, -2f);
            SetStretch(text.rectTransform, 96f, 8f);

            if (name == "Login")
            {
                MmoUiTheme.ApplyBackButtonStyle(button, text);
                text.color = Color.white;
            }
            else if (name == "Anonymous")
                ApplyAnonymousButtonStyle(button, text);
            else if (name == "Confirm Nickname")
                ApplyRankedConfirmButtonStyle(button, text);

            return button;
        }

        private static void ApplyRankedConfirmButtonStyle(Button button, Text label)
        {
            Image image = button.GetComponent<Image>();
            Sprite frame = GetRankedButtonSprite();
            if (image != null && frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                button.targetGraphic = image;

                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.92f, 1f, 1f);
                colors.pressedColor = new Color(0.8f, 0.68f, 0.92f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;
            }

            MmoUiTheme.StyleAsScreenTitle(label);
            label.color = new Color(0.98f, 0.95f, 1f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = new Vector2(0.06f, 0.03f);
            label.rectTransform.anchorMax = new Vector2(0.94f, 0.97f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            PvpUiVfx.CreateRankedButton(
                (RectTransform)button.transform,
                MmoUiTheme.AccentOf(MmoUiTheme.ButtonVariant.Violet));
        }

        private static Sprite GetRankedButtonSprite()
        {
            if (rankedButtonSprite != null)
                return rankedButtonSprite;

            Texture2D texture = Resources.Load<Texture2D>(RankedButtonResource);
            if (texture == null)
            {
                Debug.LogError($"[Login Prototype] CTA ranked viola non trovato in Resources/{RankedButtonResource}.");
                return null;
            }

            Rect crop = new(
                texture.width * (145f / 2048f),
                texture.height * (196f / 820f),
                texture.width * (1692f / 2048f),
                texture.height * (400f / 820f));
            rankedButtonSprite = Sprite.Create(
                texture,
                crop,
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            rankedButtonSprite.name = "Login Confirm Ranked Purple CTA";
            rankedButtonSprite.hideFlags = HideFlags.HideAndDontSave;
            return rankedButtonSprite;
        }

        private static void ApplyAnonymousButtonStyle(Button button, Text label)
        {
            Image image = button.GetComponent<Image>();
            Sprite frame = GetAnonymousButtonSprite();
            if (image != null && frame != null)
            {
                image.sprite = frame;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                button.targetGraphic = image;

                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.86f, 0.94f, 1f, 1f);
                colors.pressedColor = new Color(0.38f, 0.56f, 0.82f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;
            }

            MmoUiTheme.StyleAsScreenTitle(label);
            label.color = new Color(0.94f, 0.97f, 1f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = new Vector2(0.12f, 0.03f);
            label.rectTransform.anchorMax = new Vector2(0.88f, 0.97f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        private static Sprite GetAnonymousButtonSprite()
        {
            if (anonymousButtonSprite != null)
                return anonymousButtonSprite;

            Texture2D texture = Resources.Load<Texture2D>(AnonymousButtonResource);
            if (texture == null)
            {
                Debug.LogError($"[Login Prototype] CTA blu non trovato in Resources/{AnonymousButtonResource}.");
                return null;
            }

            Rect crop = new(
                texture.width * (145f / 1983f),
                texture.height * (196f / 793f),
                texture.width * (1692f / 1983f),
                texture.height * (400f / 793f));
            anonymousButtonSprite = Sprite.Create(
                texture,
                crop,
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect);
            anonymousButtonSprite.name = "Login Anonymous Blue CTA";
            anonymousButtonSprite.hideFlags = HideFlags.HideAndDontSave;
            return anonymousButtonSprite;
        }

        private static void CreateTitle(Transform parent, string value, Vector2 position, int size)
        {
            Text text = CreateText(parent, "Title", value, size, TextAnchor.MiddleCenter, MmoUiTheme.Gold);
            MmoUiTheme.StyleAsScreenTitle(text);
            SetRect(text.rectTransform, position, new Vector2(560f, 64f));
        }

        private static void CreateSubtitle(Transform parent, string value, Vector2 position, int size)
        {
            Text text = CreateText(parent, "Subtitle", value, size, TextAnchor.MiddleCenter, MmoUiTheme.Arcane);
            SetRect(text.rectTransform, position, new Vector2(560f, 34f));
        }

        private static Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color)
        {
            Text text = CreateObject<Text>(name, parent);
            text.text = value;
            text.font = MmoUiTheme.TitleBoldFont;
            text.fontStyle = FontStyle.Normal;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            global::AccardND.Battlefield.EditableRuntimeText.Bind(text, fallbackDefaultText: value);
            return text;
        }

        private static T CreateObject<T>(string name, Transform parent) where T : Component
        {
            GameObject go = new(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetStretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }
    }
}
