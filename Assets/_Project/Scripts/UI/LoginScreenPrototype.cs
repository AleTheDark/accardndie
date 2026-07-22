using System.Threading.Tasks;
using AccardND.Battlefield;
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
        private const string ButtonResource = "UI/Login/fantasy_login_button";
        private const string MainSceneName = "MainScene";
        private const string ServerUrl = "wss://accardndie.com/ws";
        private const string NicknamePrefsKey = "AccardND.PvpNickname";
        private const string PlayerHudNamePrefsKey = "AccardND.PlayerHudName";
        private const string GuestModePrefsKey = "AccardND.GuestMode";
        private const int NicknameMaxLength = 18;
        private static Sprite buttonSprite;
        private Text statusText;
        private InputField nicknameInput;
        private Button loginButton;
        private Button guestButton;
        private Button nicknameConfirmButton;
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
            await TryResumeSessionAsync();
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
                SetStatus("Ripristino sessione...");
                (string accessToken, string provider) = await PvpUgsAuth.TryResumeSessionAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    SetStatus("Accedi per giocare online.");
                    SetButtonsInteractable(true);
                    busy = false;
                    return;
                }

                signedInAccessToken = accessToken;
                SetGuestMode(false);
                SetStatus($"Bentornato ({provider}).");
                await ConnectAccountServerAsync();
                OpenMainScene();
            }
            catch (System.Exception exception)
            {
                // Sessione scaduta o server non raggiungibile: si ricade sui bottoni.
                Debug.LogWarning($"[Login] Ripristino sessione fallito: {exception.Message}");
                SetStatus("Accedi per giocare online.");
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
            CreateTitle(panel, "ACCEDI", new Vector2(0f, 225f), 46);
            CreateSubtitle(panel, "ENTRA NEL REGNO", new Vector2(0f, 177f), 18);

            statusText = CreateText(
                panel,
                "Status",
                "Controllo aggiornamenti e accesso al tuo account di gioco.",
                20,
                TextAnchor.MiddleCenter,
                new Color(0.88f, 0.92f, 0.96f));
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(statusText.rectTransform, new Vector2(0f, 52f), new Vector2(540f, 120f));

            nicknameInput = CreateInput(panel, "Nickname", "NICKNAME", new Vector2(0f, -45f), false);
            nicknameInput.characterLimit = NicknameMaxLength;
            nicknameInput.gameObject.SetActive(false);
            nicknameInput.onEndEdit.AddListener(_ => SubmitNickname());

            loginButton = CreateButton(panel, "Login", "ACCEDI GOOGLE", new Vector2(0f, -105f), 27);
            guestButton = CreateButton(panel, "Anonymous", "GIOCA ANONIMO", new Vector2(0f, -235f), 25);
            nicknameConfirmButton = CreateButton(panel, "Confirm Nickname", "CONFERMA", new Vector2(0f, -195f), 28);
            nicknameConfirmButton.gameObject.SetActive(false);

            loginButton.onClick.AddListener(StartGoogleFlow);
            guestButton.onClick.AddListener(StartAnonymousFlow);
            nicknameConfirmButton.onClick.AddListener(SubmitNickname);
        }

        private async void StartGoogleFlow()
        {
            await StartOnlineFlowAsync(LoginMode.Google);
        }

        private async void StartAnonymousFlow()
        {
            await StartOnlineFlowAsync(LoginMode.Anonymous);
        }

        private async Task StartOnlineFlowAsync(LoginMode mode)
        {
            if (busy)
                return;

            busy = true;
            SetButtonsInteractable(false);
            try
            {
                SetGuestMode(false);
                await CheckForUpdatesAsync();
                await AuthenticateAsync(mode);
                await ConnectAccountServerAsync();
                OpenMainScene();
            }
            catch (System.Exception exception)
            {
                SetStatus($"Accesso non riuscito: {exception.Message}\nPuoi riprovare con Google o anonimo.");
                SetButtonsInteractable(true);
                busy = false;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            SetStatus("Controllo aggiornamenti...");
            await PvpAsync.NextFrameAsync();

            // Punto di estensione per Play Asset Delivery / Addressables.
            // Google Play aggiorna gia' l'APK/AAB; qui in futuro scaricheremo asset bundle extra.
            await WaitSecondsAsync(0.35f);
            SetStatus("Gioco aggiornato.");
        }

        private async Task AuthenticateAsync(LoginMode mode)
        {
            SetStatus($"Accesso con {PlatformLoginLabel(mode)}...");
            if (!PvpUgsAuth.IsAvailable)
            {
                throw new System.InvalidOperationException("Unity Authentication non disponibile.");
            }

            (string accessToken, string provider) = mode == LoginMode.Google
                ? await PvpUgsAuth.SignInWithGoogleAsync()
                : await PvpUgsAuth.SignInAnonymouslyAsync();
            if (string.IsNullOrEmpty(accessToken))
                throw new System.InvalidOperationException(provider ?? "token mancante");

            signedInAccessToken = accessToken;
            SetStatus($"Accesso completato ({provider}).");
            await WaitSecondsAsync(0.25f);
        }

        private static string PlatformLoginLabel(LoginMode mode)
        {
            if (mode == LoginMode.Anonymous)
                return "account anonimo";

#if UNITY_WEBGL && !UNITY_EDITOR
            return "Google";
#elif UNITY_ANDROID
            return "Google Play Games";
#else
            return "Unity Authentication";
#endif
        }

        private enum LoginMode
        {
            Google,
            Anonymous
        }

        private async Task ConnectAccountServerAsync()
        {
            SetStatus("Controllo account di gioco...");
            serverClient?.Dispose();
            serverClient = new PvpServerClient();
            await serverClient.ConnectAsync(ServerUrl);

            if (string.IsNullOrEmpty(signedInAccessToken))
                throw new System.InvalidOperationException("token account non disponibile");

            await serverClient.SendAsync(MessageTypes.AuthUgs, new UgsLoginRequest
            {
                accessToken = signedInAccessToken,
                displayName = string.Empty
            });

            AuthResponse auth = await WaitForMessageAsync<AuthResponse>(MessageTypes.AuthResponse, 12f);
            if (auth == null || !auth.ok)
                throw new System.InvalidOperationException(auth?.error ?? "server account non disponibile");

            if (auth.requiresNickname)
                await RequireNicknameAsync();
            else
                SaveNickname(auth.username);
        }

        private async Task RequireNicknameAsync()
        {
            SetStatus("Scegli un nickname unico per continuare.");
            SetNicknameUiVisible(true);

            while (true)
            {
                pendingNickname = new TaskCompletionSource<string>();
                string nickname = await pendingNickname.Task;
                nickname = SanitizeNickname(nickname);
                if (nickname.Length < 3)
                {
                    SetStatus("Il nickname deve avere almeno 3 caratteri.");
                    continue;
                }

                SetStatus("Controllo nickname...");
                await serverClient.SendAsync(MessageTypes.NicknameSet, new SetNicknameRequest
                {
                    nickname = nickname
                });

                NicknameResponse response = await WaitForMessageAsync<NicknameResponse>(MessageTypes.NicknameResponse, 8f);
                if (response is { ok: true })
                {
                    SaveNickname(response.nickname);
                    SetStatus($"Benvenuto, {response.nickname}.");
                    SetNicknameUiVisible(false);
                    await WaitSecondsAsync(0.35f);
                    return;
                }

                SetStatus(response?.error ?? "Nickname non disponibile. Scegline un altro.");
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
                        throw new System.InvalidOperationException(error?.message ?? "errore server");
                    }

                    if (envelope.type == messageType)
                        return PvpServerClient.ParsePayload<T>(envelope);
                }

                await PvpAsync.NextFrameAsync();
            }

            throw new System.TimeoutException("timeout risposta server");
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
            if (guestButton != null)
                guestButton.gameObject.SetActive(!visible);
        }

        private void OpenMainScene()
        {
            SetStatus("Apro il menu...");
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
            if (guestButton != null)
                guestButton.interactable = interactable;
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
            rect.sizeDelta = new Vector2(650f, 960f);
            rect.anchoredPosition = new Vector2(0f, -350f);
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
            Text text = CreateText(frame.transform, "Text", string.Empty, 24, TextAnchor.MiddleLeft, Color.white);
            Text hint = CreateText(frame.transform, "Placeholder", placeholder, 20, TextAnchor.MiddleLeft, new Color(0.72f, 0.65f, 0.78f, 0.72f));
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
            image.sprite = LoadButtonSprite();
            // La cornice include già estremità, rune e glow: viene scalata come un unico
            // elemento per conservarne l'aspetto esatto. Il 9-slice resta utile solo per
            // varianti di larghezza future, dopo aver separato le estremità in layer.
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = new Color(1f, 1f, 1f, 1f);
            SetRect(image.rectTransform, position, new Vector2(550f, 145f));

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            MmoUiTheme.ApplyButtonColors(button);
            MmoUiTheme.AddMotion(button);

            Text text = CreateText(image.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, MmoUiTheme.Gold);
            text.fontStyle = FontStyle.Normal;
            Shadow goldDepth = text.gameObject.AddComponent<Shadow>();
            goldDepth.effectColor = new Color(0.32f, 0.08f, 0.015f, 1f);
            goldDepth.effectDistance = new Vector2(0f, -3f);
            SetStretch(text.rectTransform, 96f, 8f);
            return button;
        }

        private static Sprite LoadButtonSprite()
        {
            if (buttonSprite != null)
                return buttonSprite;

            buttonSprite = Resources.Load<Sprite>(ButtonResource);
            if (buttonSprite != null)
                return buttonSprite;

            // Alcune versioni dell'importer espongono prima la Texture2D dello Sprite.
            Texture2D texture = Resources.Load<Texture2D>(ButtonResource);
            if (texture == null)
            {
                Debug.LogError($"[Login Prototype] Asset pulsante non trovato in Resources/{ButtonResource}.");
                return MmoUiTheme.GetButtonSprite(MmoUiTheme.ButtonVariant.Violet);
            }

            buttonSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(400f, 205f, 400f, 205f));
            buttonSprite.name = "Fantasy Login Button (Runtime)";
            return buttonSprite;
        }

        private static void CreateTitle(Transform parent, string value, Vector2 position, int size)
        {
            Text text = CreateText(parent, "Title", value, size, TextAnchor.MiddleCenter, MmoUiTheme.Gold);
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
            text.font = MmoUiTheme.TitleFont;
            text.fontStyle = FontStyle.Bold;
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
