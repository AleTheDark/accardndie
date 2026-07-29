using System;
using System.Threading.Tasks;
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
            AccountServerSessionDriver.Ensure();
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
                    new SessionResumeRequest { sessionToken = sessionToken },
                    MessageTypes.AuthResponse);
            }

            var auth = PvpServerClient.ParsePayload<AuthResponse>(response);
            if (auth is not { ok: true })
            {
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
            var host = new GameObject("AccountServerSession") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            instance = host.AddComponent<AccountServerSessionDriver>();
            instance.BuildConnectionBadge();
        }

        private void OnEnable() => AccountServerSession.ReconnectFailed += HandleSessionLost;

        private void OnDisable() => AccountServerSession.ReconnectFailed -= HandleSessionLost;

        private void HandleSessionLost() => sessionLost = true;

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
