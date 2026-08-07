using System;
using System.Threading.Tasks;
using AccardND.Localization;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AccardND.Network
{
    /// <summary>
    /// Sessione server unica dell'account. La connessione autenticata nella schermata
    /// di login sopravvive al cambio scena ed e' condivisa da Hub, progressione e PvP.
    ///
    /// Sopravvive anche alle cadute di rete: un driver invisibile la pompa ogni frame
    /// e la riapre da sola col token di sessione. Senza, bastava un blip in ascensore
    /// per ritrovarsi offline per il resto della partita, con Santuario e taverna
    /// chiusi e le ricompense di fine run non registrate.
    /// </summary>
    public static class AccountServerSession
    {
        private static PvpServerClient client;
        private static PvpServerMessageDispatcher dispatcher;
        private static AuthResponse identity;
        private static string sessionToken;
        private static string serverUrl;
        private static Func<Task<UgsLoginRequest>> refreshGoogleAuthentication;

        public static event Action Disconnected;
        public static event Action Reconnected;
        public static event Action ReconnectFailed;

        /// <summary>
        /// Il server ha chiuso questa sessione perché l'account è entrato altrove.
        /// Non viene gestito qui dentro ma raccolto dal driver al frame successivo:
        /// smontare la sessione mentre si sta smaltendo la sua stessa coda di
        /// messaggi significherebbe segarsi il ramo sotto i piedi.
        /// </summary>
        private static string pendingKickMessage;

        /// <summary>
        /// Partita PvP che il server ci sta restituendo dopo una caduta o una riapertura
        /// dell'app. Vive qui e non nel PvP perché al momento in cui arriva il giocatore
        /// può essere ovunque: sulla schermata di login, nell'Hub, in campagna. Chi sa
        /// mostrare un match la raccoglie appena esiste; finché non lo fa, resta qui.
        /// </summary>
        private static MatchResumeState pendingMatchResume;

        /// <summary>true se c'è una partita da riprendere e nessuno l'ha ancora raccolta.</summary>
        public static bool HasPendingMatchResume => pendingMatchResume != null;

        public static bool IsReady =>
            client is { IsConnected: true } &&
            dispatcher != null &&
            identity is { ok: true };

        /// <summary>
        /// true quando la sessione esiste ma il socket è momentaneamente giù: le
        /// schermate possono aspettare invece di ripiegare sui dati locali.
        /// </summary>
        public static bool IsReconnecting => dispatcher is { IsReconnecting: true };
        public static string PlayerId => identity?.playerId;

        public static bool TryGet(
            out PvpServerClient sharedClient,
            out PvpServerMessageDispatcher sharedDispatcher,
            out AuthResponse sharedIdentity)
        {
            sharedClient = client;
            sharedDispatcher = dispatcher;
            sharedIdentity = identity;
            return IsReady;
        }

        public static void Adopt(
            PvpServerClient authenticatedClient,
            PvpServerMessageDispatcher authenticatedDispatcher,
            AuthResponse authenticatedIdentity,
            string url = null,
            Func<Task<UgsLoginRequest>> googleRefresh = null)
        {
            if (authenticatedClient == null)
                throw new System.ArgumentNullException(nameof(authenticatedClient));
            if (authenticatedDispatcher == null)
                throw new System.ArgumentNullException(nameof(authenticatedDispatcher));
            if (authenticatedIdentity is not { ok: true })
                throw new System.ArgumentException("Identita' server non autenticata.", nameof(authenticatedIdentity));

            if (client != null && !ReferenceEquals(client, authenticatedClient))
                client.Dispose();

            client = authenticatedClient;
            dispatcher = authenticatedDispatcher;
            identity = authenticatedIdentity;
            if (!string.IsNullOrEmpty(authenticatedIdentity.sessionToken))
                sessionToken = authenticatedIdentity.sessionToken;
            if (!string.IsNullOrEmpty(url))
                serverUrl = url;
            if (googleRefresh != null)
                refreshGoogleAuthentication = googleRefresh;

            EnableAutoReconnect();
            WatchForSessionKick();
            AccountServerSessionDriver.Ensure();
        }

        /// <summary>
        /// Resta in ascolto del messaggio con cui il server slogga questa sessione
        /// quando l'account entra da un'altra parte. Vive qui e non in una schermata
        /// perché deve funzionare ovunque si trovi il giocatore.
        /// </summary>
        private static void WatchForSessionKick()
        {
            if (dispatcher == null)
                return;
            dispatcher.UnhandledMessage -= HandleServerMessage;
            dispatcher.UnhandledMessage += HandleServerMessage;
        }

        private static void HandleServerMessage(Envelope envelope)
        {
            if (envelope?.type == MessageTypes.MatchResume)
            {
                StashMatchResume(PvpServerClient.ParsePayload<MatchResumeState>(envelope));
                return;
            }

            if (envelope?.type != MessageTypes.SessionKicked)
                return;

            var kick = PvpServerClient.ParsePayload<SessionKickedMessage>(envelope);
            pendingKickMessage = string.IsNullOrWhiteSpace(kick?.message)
                ? GameText.GetOrFallbackSilent(
                    GameTextKeys.Account.SessionUsedElsewhere,
                    "Il tuo account è stato usato su un altro dispositivo.")
                : kick.message;
            Debug.LogWarning($"[Net] Sessione chiusa dal server: {pendingKickMessage}");

            // Stacca subito la riconnessione automatica: senza, il client rientrerebbe
            // col token di sessione e sloggherebbe a sua volta il dispositivo nuovo.
            dispatcher?.ConfigureReconnect(null, null);
            sessionToken = null;
        }

        /// <summary>
        /// C'è un avviso di sloggatura in attesa di essere mostrato. Vale anche come
        /// "questa sessione è finita": chi la sta riaprendo deve mollare.
        /// </summary>
        private static bool SessionKickPending => pendingKickMessage != null;

        /// <summary>
        /// L'app è in secondo piano (telefono in tasca, altra app davanti). Solo su
        /// mobile: nell'editor e su desktop non arriva mai la pausa, quindi lì il
        /// comportamento resta quello di sempre.
        /// </summary>
        private static bool ApplicationIsPaused => AccountServerSessionDriver.ApplicationPaused;

        /// <summary>Raccoglie l'avviso di sloggatura, una volta sola.</summary>
        internal static bool TryConsumeSessionKick(out string message)
        {
            message = pendingKickMessage;
            pendingKickMessage = null;
            return message != null;
        }

        /// <summary>
        /// Mette da parte una partita da riprendere. Pubblico perché il match.resume può
        /// arrivare anche nel bel mezzo dell'handshake di login, prima che esista un
        /// dispatcher che smisti i messaggi.
        /// </summary>
        public static void StashMatchResume(MatchResumeState resume)
        {
            if (resume == null)
                return;
            pendingMatchResume = resume;
            Debug.Log($"[Net] Partita PvP da riprendere in attesa: {resume.events?.Length ?? 0} eventi.");
        }

        /// <summary>Raccoglie la partita da riprendere, una volta sola.</summary>
        public static bool TryConsumeMatchResume(out MatchResumeState resume)
        {
            resume = pendingMatchResume;
            pendingMatchResume = null;
            return resume != null;
        }

        /// <summary>
        /// Collega la riconnessione automatica: alla caduta il dispatcher riapre il
        /// socket e si riautentica col token, senza ripassare da Google (che è lento e
        /// può fallire proprio mentre la rete traballa).
        /// </summary>
        private static void EnableAutoReconnect()
        {
            if (string.IsNullOrEmpty(serverUrl) || dispatcher == null)
                return;
            dispatcher.Disconnected -= ForwardDisconnected;
            dispatcher.Reconnected -= ForwardReconnected;
            dispatcher.ReconnectFailed -= ForwardReconnectFailed;
            dispatcher.Disconnected += ForwardDisconnected;
            dispatcher.Reconnected += ForwardReconnected;
            dispatcher.ReconnectFailed += ForwardReconnectFailed;
            dispatcher.ConfigureReconnect(serverUrl, ResumeSessionAsync);
        }

        private static void ForwardDisconnected() => Disconnected?.Invoke();
        private static void ForwardReconnected() => Reconnected?.Invoke();
        private static void ForwardReconnectFailed() => ReconnectFailed?.Invoke();

        private static async Task<bool> ResumeSessionAsync(
            PvpServerClient reconnected, PvpServerMessageDispatcher owner)
        {
            Envelope response = null;
            if (!string.IsNullOrEmpty(sessionToken))
            {
                response = await owner.HandshakeAsync(
                    MessageTypes.AuthSession,
                    new SessionResumeRequest
                    {
                        sessionToken = sessionToken,
                        clientVersion = GameVersion.Current
                    },
                    MessageTypes.AuthResponse);
            }

            var auth = PvpServerClient.ParsePayload<AuthResponse>(response);

            // Il server ha risposto che questa sessione era già stata chiusa perché
            // l'account è entrato altrove (l'avviso è arrivato mentre aspettavamo la
            // risposta ed è stato instradato dall'handshake). Non è un token scaduto:
            // rifare il login Google qui sbatterebbe fuori il dispositivo su cui il
            // giocatore sta giocando adesso, e da lì si rimpallerebbe a vuoto.
            if (SessionKickPending)
                return false;

            if (auth is { requiresUpdate: true })
            {
                // Il server ha alzato la versione richiesta mentre giocavamo: rifare il
                // login Google darebbe lo stesso rifiuto. Si molla subito, così il
                // giocatore torna al login e lì legge l'avviso di aggiornare.
                Debug.LogWarning($"[Net] Sessione chiusa: {auth.error}");
                return false;
            }

            if (auth is not { ok: true })
            {
                // Il login Google completo non è un riaggancio: è un accesso nuovo, e un
                // accesso nuovo slogga chi è dentro. Con l'app in secondo piano (su Android
                // continua a girare, vedi runInBackground) il telefono in tasca si
                // riprenderebbe da solo la sessione a chi sta giocando sul PC. Qui si molla:
                // al ritorno in primo piano il giocatore trova il badge per rientrare.
                if (ApplicationIsPaused)
                {
                    Debug.Log(
                        "[Net] Token non più valido con l'app in secondo piano: niente accesso automatico, si rientra al ritorno.");
                    return false;
                }

                Debug.LogWarning($"[Net] Token server non valido ({auth?.error ?? "nessuna risposta"}), provo la sessione Google.");
                if (refreshGoogleAuthentication == null)
                    return false;
                UgsLoginRequest request = await refreshGoogleAuthentication();
                if (request == null || string.IsNullOrEmpty(request.accessToken))
                    return false;
                response = await owner.HandshakeAsync(
                    MessageTypes.AuthUgs,
                    request,
                    MessageTypes.AuthResponse);
                auth = PvpServerClient.ParsePayload<AuthResponse>(response);
                if (auth is not { ok: true })
                    return false;
            }

            if (!string.IsNullOrEmpty(auth.sessionToken))
                sessionToken = auth.sessionToken;
            UpdateIdentity(auth.playerId, auth.username, auth.requiresNickname);
            Debug.Log("[Net] Sessione server ripresa.");
            return true;
        }

        public static void UpdateIdentity(string playerId, string username, bool requiresNickname = false)
        {
            if (identity == null)
                return;

            if (!string.IsNullOrWhiteSpace(playerId))
                identity.playerId = playerId;
            if (!string.IsNullOrWhiteSpace(username))
                identity.username = username.Trim();
            identity.requiresNickname = requiresNickname;
        }

        /// <summary>Pompa la sessione condivisa quando nessuna schermata lo sta già facendo.</summary>
        internal static void Tick() => dispatcher?.Pump();

        /// <summary>
        /// Butta via una sessione che non tornerà: il token è stato rifiutato e la
        /// riconnessione si è arresa. Serve prima di rimandare il giocatore al login,
        /// altrimenti la schermata troverebbe una sessione morta che si dichiara viva.
        /// Gli eventi restano collegati: chi ascolta è vivo quanto la sua scena.
        /// </summary>
        public static void Invalidate()
        {
            client?.Dispose();
            client = null;
            dispatcher = null;
            identity = null;
            sessionToken = null;
            // Senza sessione non c'è modo di giocare il match che stavamo per riprendere:
            // tenerlo in sospeso aprirebbe un tavolo muto al prossimo accesso.
            pendingMatchResume = null;
        }

        /// <summary>
        /// Chiude la sessione per scelta del giocatore (logout dalle impostazioni):
        /// butta via la connessione e spegne la riconnessione automatica, che
        /// altrimenti riaprirebbe subito il socket appena chiuso. Dimenticare
        /// l'accesso Google tocca a chi chiama: sta in un altro assembly.
        /// </summary>
        public static void SignOut()
        {
            dispatcher?.ConfigureReconnect(null, null);
            pendingKickMessage = null;
            Invalidate();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            client?.Dispose();
            client = null;
            dispatcher = null;
            identity = null;
            sessionToken = null;
            serverUrl = null;
            refreshGoogleAuthentication = null;
            pendingKickMessage = null;
            pendingMatchResume = null;
            Disconnected = null;
            Reconnected = null;
            ReconnectFailed = null;
        }
    }

    /// <summary>
    /// Unico compito: dare un battito alla sessione condivisa. Senza qualcuno che
    /// pompi ogni frame, la riconnessione non partirebbe mai fuori dalle schermate
    /// PvP - ed è proprio in campagna che serve di più.
    /// </summary>
    internal sealed class AccountServerSessionDriver : MonoBehaviour
    {
        /// <summary>Prima scena della build: quella del login.</summary>
        private const string LoginSceneName = "LoginScreenPrototype";

        private static readonly Color ReconnectingColor = new(0.35f, 0.08f, 0.06f, 0.94f);

        /// <summary>Più acceso di quello di riconnessione: qui non si sta più aspettando.</summary>
        private static readonly Color SessionLostColor = new(0.62f, 0.12f, 0.10f, 0.97f);

        private static AccountServerSessionDriver instance;

        /// <summary>
        /// true mentre l'app è in secondo piano su mobile. Statico perché lo legge la
        /// sessione, che non è un MonoBehaviour e non riceve la pausa.
        /// </summary>
        internal static bool ApplicationPaused { get; private set; }

        private GameObject kickedOverlay;
        private GameObject connectionBadge;
        private Text connectionBadgeText;
        private Image connectionBadgeBackground;
        private Button connectionBadgeButton;

        /// <summary>
        /// La riconnessione ha smesso di provarci. Da qui non si torna indietro da soli:
        /// senza questo stato il badge sparirebbe (IsReconnecting torna false) proprio
        /// nell'istante in cui la sessione muore, e il giocatore leggerebbe solo
        /// "non disponibile offline" senza capire che deve rifare il login.
        /// </summary>
        private bool sessionLost;

        internal static void Ensure()
        {
            if (instance != null)
                return;
            // Una sessione appena adottata nasce con il giocatore davanti allo schermo:
            // senza questo, un "in pausa" rimasto in piedi da un play mode precedente
            // (dominio non ricaricato) bloccherebbe il rientro automatico.
            ApplicationPaused = false;
            var host = new GameObject("AccountServerSession") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            instance = host.AddComponent<AccountServerSessionDriver>();
            instance.BuildConnectionBadge();
        }

        private void OnEnable() => AccountServerSession.ReconnectFailed += HandleSessionLost;

        private void OnDisable() => AccountServerSession.ReconnectFailed -= HandleSessionLost;

        private void HandleSessionLost() => sessionLost = true;

        /// <summary>
        /// Su Android l'app continua a girare in secondo piano (runInBackground), quindi
        /// la riconnessione automatica lavora anche a telefono bloccato: qui si segna che
        /// il giocatore non è davanti allo schermo, così un token scaduto non si trasforma
        /// in un accesso nuovo che slogga l'altro dispositivo.
        /// </summary>
        private void OnApplicationPause(bool paused) => ApplicationPaused = paused;

        private void BuildConnectionBadge()
        {
            var canvasObject = new GameObject(
                "Connection Status", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            connectionBadge = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(Button));
            connectionBadge.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)connectionBadge.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-18f, -18f);
            rect.sizeDelta = new Vector2(330f, 52f);
            connectionBadgeBackground = connectionBadge.GetComponent<Image>();
            connectionBadgeBackground.color = ReconnectingColor;
            connectionBadgeButton = connectionBadge.GetComponent<Button>();
            connectionBadgeButton.targetGraphic = connectionBadgeBackground;
            // Niente tinta automatica: il colore lo decide lo stato della sessione, e
            // la tinta "disabilitato" sbiadirebbe proprio l'avviso di riconnessione.
            connectionBadgeButton.transition = Selectable.Transition.None;
            connectionBadgeButton.onClick.AddListener(ReturnToLogin);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(connectionBadge.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);
            connectionBadgeText = textObject.GetComponent<Text>();
            connectionBadgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            connectionBadgeText.fontSize = 20;
            connectionBadgeText.alignment = TextAnchor.MiddleCenter;
            connectionBadgeText.color = Color.white;
            connectionBadge.SetActive(false);
        }

        private void Update()
        {
            AccountServerSession.Tick();

            if (AccountServerSession.TryConsumeSessionKick(out string kickMessage))
                ShowSessionKicked(kickMessage);

            // Sloggati: il badge "riconnessione" non ha più senso, non tornerà nessuno.
            if (kickedOverlay != null && kickedOverlay.activeSelf)
                return;

            if (connectionBadge == null)
                return;

            // Un login riuscito (anche quello dopo il tocco sul badge) chiude il lutto.
            if (AccountServerSession.IsReady)
                sessionLost = false;

            bool reconnecting = !AccountServerSession.IsReady && AccountServerSession.IsReconnecting;
            connectionBadge.SetActive(sessionLost || reconnecting);
            if (!connectionBadge.activeSelf)
                return;

            connectionBadgeText.text = sessionLost
                ? "SESSIONE SCADUTA · TOCCA PER RIENTRARE"
                : "RETE ASSENTE · RICONNESSIONE…";
            connectionBadgeText.fontSize = sessionLost ? 18 : 20;
            connectionBadgeBackground.color = sessionLost ? SessionLostColor : ReconnectingColor;
            // Durante la riconnessione non c'è niente da toccare: sta già succedendo.
            connectionBadgeButton.interactable = sessionLost;
        }

        /// <summary>
        /// L'account è entrato da un'altra parte e il server ha chiuso questa
        /// sessione. Non si torna al gioco: si avvisa e si esce. Il pannello copre
        /// tutto e non ha modo di essere chiuso se non col suo bottone, perché da
        /// qui in poi qualunque cosa il giocatore tocchi parlerebbe con una sessione
        /// che non esiste più.
        /// </summary>
        private void ShowSessionKicked(string message)
        {
            AccountServerSession.Invalidate();
            sessionLost = false;
            if (connectionBadge != null)
                connectionBadge.SetActive(false);
            if (kickedOverlay != null)
            {
                kickedOverlay.SetActive(true);
                return;
            }

            var canvasObject = new GameObject(
                "Session Kicked", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Sopra il badge di connessione e sopra qualunque UI di gioco.
            canvas.sortingOrder = 6000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            kickedOverlay = canvasObject;

            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(canvasObject.transform, false);
            StretchFull((RectTransform)scrim.transform);
            scrim.GetComponent<Image>().color = new Color(0.02f, 0.01f, 0.04f, 0.92f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(660f, 320f);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.05f, 0.08f, 0.99f);

            var textObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            var messageRect = (RectTransform)textObject.transform;
            messageRect.anchorMin = new Vector2(0f, 0.35f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(28f, 0f);
            messageRect.offsetMax = new Vector2(-28f, -24f);
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.color = Color.white;
            text.text = GameText.GetOrFallbackSilent(
                GameTextKeys.Account.SessionClosedBody,
                "{0}\n\nQuesta sessione è stata chiusa.",
                message);

            var buttonObject = new GameObject("Quit", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = new Vector2(0.25f, 0.08f);
            buttonRect.anchorMax = new Vector2(0.75f, 0.28f);
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonObject.GetComponent<Image>().color = new Color(0.55f, 0.12f, 0.12f, 1f);
            buttonObject.GetComponent<Button>().onClick.AddListener(QuitAfterKick);

            var buttonLabel = new GameObject("Label", typeof(RectTransform), typeof(Text));
            buttonLabel.transform.SetParent(buttonObject.transform, false);
            StretchFull((RectTransform)buttonLabel.transform);
            var label = buttonLabel.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = QuitClosesApplication
                ? GameText.GetOrFallbackSilent(GameTextKeys.Account.CloseGame, "CHIUDI IL GIOCO")
                : GameText.GetOrFallbackSilent(GameTextKeys.Account.ReturnToLogin, "TORNA AL LOGIN");
        }

        /// <summary>
        /// Nel browser una scheda non si chiude da sola: lì l'equivalente onesto di
        /// "chiudi l'app" è tornare alla schermata di login.
        /// </summary>
        private static bool QuitClosesApplication =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        private void QuitAfterKick()
        {
            if (!QuitClosesApplication)
            {
                if (kickedOverlay != null)
                    kickedOverlay.SetActive(false);
                SceneManager.LoadScene(LoginSceneName);
                return;
            }

            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Riporta al login buttando via la sessione morta. È l'unica via d'uscita:
        /// senza, il giocatore resta in campagna con Santuario e taverna chiusi e
        /// nessun modo di riaprirli se non riavviando il gioco.
        /// </summary>
        private void ReturnToLogin()
        {
            if (!sessionLost)
                return;
            sessionLost = false;
            connectionBadge.SetActive(false);
            AccountServerSession.Invalidate();
            SceneManager.LoadScene(LoginSceneName);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
