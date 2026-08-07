using System;
using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.GameCore.Pvp;
using AccardND.GameData;
using AccardND.Localization;
using AccardND.Network;
using AccardND.NetProtocol;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Entry point della modalità PvP: aggiungilo a un GameObject in una scena
    /// vuota, imposta username/password (e codice stanza per il join)
    /// dall'Inspector, avvia il server e premi Play.
    /// </summary>
    public sealed class PvpBootstrap : MonoBehaviour, IBattlePresentationActions
    {
        private const string NicknamePrefsKey = "AccardND.PvpNickname";
        private const string PlayerHudNamePrefsKey = "AccardND.PlayerHudName";
        private const int NicknameMaxLength = 18;

        [SerializeField] private string serverUrl = "wss://accardndie.com/ws";
        [SerializeField, Tooltip("Nome mostrato al server; vuoto = derivato dal dispositivo.")]
        private string username = string.Empty;
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private GameConfiguration configuration;

        private PvpServerClient client;
        private PvpServerMessageDispatcher dispatcher;
        private PvpClientMatchState state;
        private PvpLobbyScreen lobby;
        private PvpProfileScreen profileScreen;
        private PvpLeaderboardScreen leaderboardScreen;
        private PvpMatchResultOverlay resultOverlay;
        private RectTransform challengePrompt;
        private RectTransform nicknameDialog;
        private InputField nicknameInput;
        private Text nicknameStatusText;
        private bool nicknameRequired;
        private string myPlayerId = string.Empty;
        private PvpLoadoutBuilderScreen builder;
        private PvpLoadoutDto confirmedLoadout;
        private Transform canvasRoot;
        private GameObject canvasObject;
        private List<LoadoutCardDto> myLoadout;
        private IPvpMatchView matchView;
        private System.Action onClosed;
        private System.Action onSettings;
        private bool authenticated;
        private bool connecting;
        private bool stateDirty;
        private bool startInLeaderboard;
        private bool ownsConnection = true;
        private bool rankedQueueRequested;
        private BattleSfxPlayer battleSfx;
        private readonly List<BattlePresentationEvent> pendingAnimationEvents = new();
        private bool surrenderSent;
        private RectTransform reconnectOverlay;
        private Text reconnectOverlayText;
        private float opponentReconnectDeadline;
        private string disconnectedOpponentName;

        public PvpServerMessageDispatcher ServerDispatcher => dispatcher;
        public bool IsAuthenticated => authenticated;

        /// <summary>Da chiamare subito dopo AddComponent quando il PvP è lanciato dal menu di gioco.</summary>
        public void Configure(
            CardDatabase database,
            System.Action closedCallback,
            IPvpMatchView view = null,
            System.Action settingsCallback = null)
        {
            cardDatabase = database;
            onClosed = closedCallback;
            matchView = view;
            onSettings = settingsCallback;
        }

        /// <summary>
        /// Avvia il collegamento PvP direttamente sulla classifica dell'Hub,
        /// mantenendo autenticazione e dati autoritativi identici alla lobby.
        /// </summary>
        public void ConfigureLeaderboard(
            CardDatabase database, System.Action closedCallback, IPvpMatchView view = null)
        {
            Configure(database, closedCallback, view);
            startInLeaderboard = true;
        }

        private async void Start()
        {
            BuildCanvas();
            ShowLobby();
            if (startInLeaderboard)
                CreateLeaderboardScreen();

            if (AccountServerSession.TryGet(
                    out PvpServerClient sharedClient,
                    out PvpServerMessageDispatcher sharedDispatcher,
                    out AuthResponse sharedIdentity))
            {
                client = sharedClient;
                dispatcher = sharedDispatcher;
                ownsConnection = false;
                dispatcher.UnhandledMessage += HandleMessage;
                SubscribeConnectionEvents();
                CompleteAuthentication(sharedIdentity, adoptConnection: false);
                return;
            }

            EnsureDisplayName();
            client = new PvpServerClient();
            dispatcher = new PvpServerMessageDispatcher(client);
            dispatcher.UnhandledMessage += HandleMessage;
            SubscribeConnectionEvents();
            await ConnectAndAuthenticateAsync();
        }

        private async System.Threading.Tasks.Task ConnectAndAuthenticateAsync()
        {
            if (connecting || client == null || client.IsConnected)
                return;
            // La sessione si sta già riaprendo da sola col token: aprire un secondo
            // socket in parallelo darebbe due connessioni per lo stesso account.
            if (dispatcher is { IsReconnecting: true })
            {
                SetConnectionStatus("Riconnessione in corso...");
                return;
            }
            connecting = true;
            try
            {
                SetConnectionStatus($"Connessione a {serverUrl}...");
                await client.ConnectAsync(serverUrl);
                SetConnectionStatus("Connesso. Autenticazione...");

                // Unica strada: Unity Authentication con provider Google. Non c'e'
                // piu' un ripiego su account locale o ospite: un accesso che non
                // arriva da Google non deve creare un profilo.
                if (PvpUgsAuth.IsAvailable)
                {
                    (string accessToken, string authProvider) = await PvpUgsAuth.TryResumeSessionAsync();
                    if (string.IsNullOrEmpty(accessToken))
                        (accessToken, authProvider) = await PvpUgsAuth.SignInAsync();
                    if (accessToken != null)
                    {
                        SetConnectionStatus($"Autenticazione {authProvider} riuscita. Accesso al server...");
                        await dispatcher.SendAsync(MessageTypes.AuthUgs, new UgsLoginRequest
                        {
                            accessToken = accessToken,
                            displayName = username,
                            authMethod = PvpUgsAuth.CurrentAuthMethod(),
                            googleIdToken = PvpUgsAuth.LastGoogleIdToken,
                            clientVersion = GameVersion.Current
                        });
                        return;
                    }
                    SetConnectionStatus("Accesso Google non riuscito: riprova.");
                    return;
                }

                SetConnectionStatus("Unity Authentication non disponibile: impossibile accedere.");
            }
            catch (System.Exception exception)
            {
                SetConnectionStatus(
                    $"Server non raggiungibile ({exception.Message}).\n"
                    + "Avvia il server con: dotnet run in Server/AccardND.Server - poi premi di nuovo un pulsante.");
            }
            finally
            {
                connecting = false;
            }
        }

        private void SetConnectionStatus(string message)
        {
            lobby?.SetStatus(message);
            leaderboardScreen?.SetStatus(message);
        }

        // --- Cadute di rete ---

        private void SubscribeConnectionEvents()
        {
            if (dispatcher == null)
                return;
            dispatcher.Disconnected += HandleConnectionLost;
            dispatcher.Reconnected += HandleConnectionRestored;
            dispatcher.ReconnectFailed += HandleReconnectGivenUp;
        }

        private void UnsubscribeConnectionEvents()
        {
            if (dispatcher == null)
                return;
            dispatcher.Disconnected -= HandleConnectionLost;
            dispatcher.Reconnected -= HandleConnectionRestored;
            dispatcher.ReconnectFailed -= HandleReconnectGivenUp;
        }

        /// <summary>
        /// Il socket è caduto. Non è una sconfitta: il server tiene la partita in pausa
        /// e il dispatcher sta già riaprendo la connessione.
        /// </summary>
        private void HandleConnectionLost()
        {
            SetConnectionStatus("Connessione persa: riconnessione in corso...");
            state?.AddNotice("Connessione persa: riconnessione in corso...");
            ShowReconnectOverlay("CONNESSIONE PERSA\nRiconnessione in corso...");
            stateDirty = true;
        }

        private void HandleConnectionRestored()
        {
            SetConnectionStatus("Connessione ripristinata.");
            state?.AddNotice("Connessione ripristinata.");
            HideReconnectOverlay();
            stateDirty = true;
        }

        private void HandleReconnectGivenUp()
        {
            SetConnectionStatus("Sessione scaduta: rientra dal menu per riaccedere.");
            if (state != null)
                LeaveToLobby();
        }

        /// <summary>Guardia dei pulsanti: se la connessione manca la ritenta invece di lanciare eccezioni.</summary>
        private async System.Threading.Tasks.Task<bool> EnsureReadyAsync()
        {
            if (client is { IsConnected: true } && authenticated)
                return true;
            if (client is not { IsConnected: true })
            {
                await ConnectAndAuthenticateAsync();
                return false;
            }
            lobby.SetStatus("Autenticazione in corso: riprova tra un istante.");
            return false;
        }

        private void Update()
        {
            lobby?.Tick();
            leaderboardScreen?.Tick();
            UpdateReconnectOverlay();
            if (dispatcher == null)
                return;
            dispatcher.Pump();
            // Una partita da riprendere può essere arrivata prima che questa schermata
            // esistesse (login, Hub): la sessione la tiene da parte e noi la raccogliamo
            // appena siamo in piedi.
            if (AccountServerSession.TryConsumeMatchResume(out MatchResumeState resume))
                ResumeMatch(resume);
            if (stateDirty)
            {
                stateDirty = false;
                matchView?.UpdatePvpMatch(state, myLoadout, pendingAnimationEvents);
                pendingAnimationEvents.Clear();
            }
        }

        private async void HandleMessage(Envelope envelope)
        {
            switch (envelope.type)
            {
                case MessageTypes.AuthResponse:
                {
                    var auth = PvpServerClient.ParsePayload<AuthResponse>(envelope);
                    if (auth.ok)
                        CompleteAuthentication(auth, adoptConnection: true);
                    else
                        SetConnectionStatus($"Autenticazione fallita: {auth.error}");
                    break;
                }

                case MessageTypes.NicknameResponse:
                {
                    var response = PvpServerClient.ParsePayload<NicknameResponse>(envelope);
                    if (!response.ok)
                    {
                        if (nicknameStatusText != null)
                            nicknameStatusText.text = response.error ?? "Nickname non disponibile.";
                        break;
                    }

                    nicknameRequired = false;
                    username = response.nickname;
                    PlayerPrefs.SetString(NicknamePrefsKey, username);
                    PlayerPrefs.SetString(PlayerHudNamePrefsKey, username);
                    PlayerPrefs.Save();
                    nicknameDialog?.gameObject.SetActive(false);
                    lobby.SetPlayerName(username);
                    lobby.SetStatus($"Nickname salvato: {username}.");
                    AccountServerSession.UpdateIdentity(myPlayerId, username);
                    break;
                }

                case MessageTypes.RoomCreated:
                {
                    var created = PvpServerClient.ParsePayload<RoomCreated>(envelope);
                    lobby.SetWaitingForOpponent(false);
                    lobby.ShowRoom(created);
                    lobby.SetStatus(created.isPublic
                        ? "Stanza aperta: comparirà nell'elenco finché non entra un avversario."
                        : "Condividi il codice: il match parte quando l'avversario entra.");
                    break;
                }

                case MessageTypes.RoomsData:
                    lobby.SetRooms(PvpServerClient.ParsePayload<RoomsData>(envelope));
                    break;

                case MessageTypes.QueueStatus:
                    // La conferma del join puo' arrivare dopo che il giocatore ha gia'
                    // premuto Annulla. In quel caso non deve riaprire il pannello d'attesa.
                    if (rankedQueueRequested)
                    {
                        lobby.SetWaitingForOpponent(true);
                        lobby.SetStatus("In coda: in attesa di un avversario...");
                    }
                    break;

                case MessageTypes.MatchFound:
                    rankedQueueRequested = false;
                    lobby.SetWaitingForOpponent(false);
                    lobby.SetStatus("Avversario trovato!");
                    break;

                case MessageTypes.MatchStart:
                {
                    rankedQueueRequested = false;
                    surrenderSent = false;
                    state = new PvpClientMatchState();
                    state.Changed += () => stateDirty = true;
                    state.ApplyMatchStart(PvpServerClient.ParsePayload<MatchStart>(envelope));
                    Debug.Log($"[PvP] Match iniziato: sono G{state.MyIndex} contro '{state.OpponentName}'.");
                    // Il triplicatore si offre a fine partita, ma solo se l'annuncio e' gia'
                    // pronto: si chiede adesso, che e' l'unico anticipo disponibile. Chi resta
                    // in lobby senza giocare non costa nessuna richiesta.
                    AccardND.Ads.AdService.Warm(AccardND.Ads.AdPlacement.PvpExperienceTriple);
                    ShowMatch();
                    break;
                }

                case MessageTypes.MatchHand:
                {
                    MatchHand hand = PvpServerClient.ParsePayload<MatchHand>(envelope);
                    Debug.Log($"[PvP] Mano round {hand.roundNumber}: {string.Join(", ", hand.handDefinitionIds ?? new string[0])}");
                    state?.ApplyHand(hand);
                    break;
                }

                case MessageTypes.MatchEvent:
                {
                    MatchEventDto matchEvent = PvpServerClient.ParsePayload<MatchEventDto>(envelope);
                    Debug.Log($"[PvP] Evento {matchEvent.type}: player {matchEvent.player}, slot {matchEvent.slot}");
                    BattlePresentationEvent presentationEvent =
                        PvpBattlePresentationMapper.ToPresentationEvent(matchEvent, state);
                    state?.Apply(matchEvent);
                    if (presentationEvent != null)
                        pendingAnimationEvents.Add(presentationEvent);
                    break;
                }

                case MessageTypes.MatchResult:
                    ShowMatchResult(PvpServerClient.ParsePayload<MatchResultData>(envelope));
                    break;

                case MessageTypes.ProfileData:
                {
                    var data = PvpServerClient.ParsePayload<ProfileData>(envelope);
                    CachePvpLeague(data);
                    lobby?.SetProfile(data);
                    profileScreen?.SetProfile(data);
                    profileScreen?.SyncSelectedIcon(data.selectedIconId);
                    leaderboardScreen?.SetProfile(data);
                    break;
                }

                case MessageTypes.SinglePlayerProgressData:
                {
                    var data = PvpServerClient.ParsePayload<SinglePlayerProgressData>(envelope);
                    lobby?.SetAccountProgress(data);
                    leaderboardScreen?.SetAccountProgress(data);
                    break;
                }

                case MessageTypes.IconsData:
                    profileScreen?.SetIcons(PvpServerClient.ParsePayload<IconsData>(envelope));
                    break;

                case MessageTypes.FriendsData:
                    profileScreen?.SetFriends(PvpServerClient.ParsePayload<FriendsData>(envelope));
                    break;

                case MessageTypes.LeaderboardData:
                {
                    var data = PvpServerClient.ParsePayload<LeaderboardData>(envelope);
                    profileScreen?.SetLeaderboard(data);
                    leaderboardScreen?.SetLeaderboard(data);
                    break;
                }

                case MessageTypes.HallOfFameSeasonsData:
                {
                    var data = PvpServerClient.ParsePayload<HallOfFameSeasonsData>(envelope);
                    profileScreen?.SetHallOfFameSeasons(data);
                    leaderboardScreen?.SetHallOfFameSeasons(data);
                    break;
                }

                case MessageTypes.HallOfFameData:
                {
                    var data = PvpServerClient.ParsePayload<HallOfFameData>(envelope);
                    profileScreen?.SetHallOfFame(data);
                    leaderboardScreen?.SetHallOfFame(data);
                    break;
                }

                case MessageTypes.AchievementsData:
                    profileScreen?.SetAchievements(PvpServerClient.ParsePayload<AchievementsData>(envelope));
                    break;

                case MessageTypes.FriendPresence:
                {
                    var update = PvpServerClient.ParsePayload<FriendPresenceUpdate>(envelope);
                    profileScreen?.ApplyPresence(update.playerId, update.presence);
                    break;
                }

                case MessageTypes.FriendChallengeReceived:
                    ShowChallengePrompt(PvpServerClient.ParsePayload<FriendChallengeReceived>(envelope));
                    break;

                case MessageTypes.MatchOpponentDisconnected:
                {
                    var dropped = PvpServerClient.ParsePayload<MatchOpponentDisconnected>(envelope);
                    // La partita è ferma per entrambi: il server rifiuta le azioni
                    // finché l'altro non rientra, quindi qui basta dirlo.
                    state?.AddNotice(
                        $"{dropped.opponentName} ha perso la connessione: {dropped.secondsRemaining}s per rientrare.");
                    disconnectedOpponentName = dropped.opponentName;
                    opponentReconnectDeadline = Time.unscaledTime + Mathf.Max(0, dropped.secondsRemaining);
                    ShowReconnectOverlay(string.Empty);
                    stateDirty = true;
                    break;
                }

                case MessageTypes.MatchOpponentReconnected:
                {
                    var back = PvpServerClient.ParsePayload<MatchOpponentReconnected>(envelope);
                    state?.AddNotice($"{back.opponentName} è rientrato: si riprende.");
                    HideReconnectOverlay();
                    stateDirty = true;
                    break;
                }

                case MessageTypes.MatchOpponentLeft:
                    if (state != null)
                        lobby.SetStatus("L'avversario ha lasciato la partita.");
                    LeaveToLobby();
                    break;

                case MessageTypes.Error:
                {
                    var error = PvpServerClient.ParsePayload<ErrorMessage>(envelope);
                    Debug.LogWarning($"[PvP] {error.code}: {error.message}");
                    if (profileScreen != null)
                        profileScreen.SetStatus($"Errore: {error.message}");
                    else if (leaderboardScreen != null)
                        leaderboardScreen.SetStatus($"Errore: {error.message}");
                    else if (state == null)
                    {
                        lobby.SetWaitingForOpponent(false);
                        lobby.SetStatus($"Errore: {error.message}");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Rientro in una partita rimasta in piedi mentre eravamo offline. Lo stato si
        /// ricostruisce riapplicando il log eventi dall'inizio: è lo stesso codice della
        /// diretta, ma senza animazioni, così si riparte dal tavolo com'era davvero
        /// invece di indovinarlo.
        /// </summary>
        private void ResumeMatch(MatchResumeState resume)
        {
            if (resume == null)
                return;

            // Il loadout arriva dal server: dopo un riavvio dell'app non ce l'abbiamo
            // più, e quello salvato in locale potrebbe essere stato cambiato nel builder
            // dopo l'inizio della partita.
            if (resume.yourLoadout?.cards is { Length: > 0 })
                myLoadout = new List<LoadoutCardDto>(resume.yourLoadout.cards);

            state = new PvpClientMatchState();
            state.Changed += () => stateDirty = true;
            state.ApplyMatchStart(new MatchStart
            {
                opponentName = resume.opponentName,
                yourPlayerIndex = resume.yourPlayerIndex
            });

            if (resume.events != null)
            {
                foreach (MatchEventDto matchEvent in resume.events)
                    state.Apply(matchEvent);
            }
            if (resume.hand?.handIndices is { Length: > 0 })
                state.ApplyHand(resume.hand);

            pendingAnimationEvents.Clear();
            HideReconnectOverlay();
            Debug.Log($"[PvP] Partita ripresa: {resume.events?.Length ?? 0} eventi riapplicati.");
            ShowMatch();
            state.AddNotice(resume.reconnectSecondsRemaining > 0
                ? GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpStatus.ReconnectRemaining,
                    "Partita ripresa: restano {0}s di riconnessione per questo match.",
                    resume.reconnectSecondsRemaining)
                : GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpStatus.ReconnectExpired,
                    "Partita ripresa: hai esaurito il tempo di riconnessione, un'altra caduta è sconfitta."));
            stateDirty = true;
        }

        // --- Azioni lobby ---

        private async void CreateRoom(string roomName, string mode, bool isPublic)
        {
            if (!await EnsureReadyAsync())
                return;
            if (!RequireLoadout(out PvpLoadoutDto loadout))
                return;
            try
            {
                lobby.SetStatus("Creazione stanza...");
                lobby.SetWaitingForOpponent(false);
                await dispatcher.SendAsync(MessageTypes.RoomCreate, new CreateRoomRequest
                {
                    loadout = loadout,
                    roomName = roomName,
                    mode = mode,
                    isPublic = isPublic
                });
            }
            catch (System.Exception exception)
            {
                lobby.SetWaitingForOpponent(false);
                lobby.SetStatus($"Invio fallito: {exception.Message}");
            }
        }

        private async void JoinRoom(string roomCode)
        {
            if (!await EnsureReadyAsync())
                return;
            string code = (roomCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length < 6)
            {
                lobby.SetStatus("Componi il codice a 6 caratteri col tastierino.");
                return;
            }
            if (!RequireLoadout(out PvpLoadoutDto loadout))
                return;
            try
            {
                lobby.SetStatus($"Ingresso nella stanza {code}...");
                await dispatcher.SendAsync(MessageTypes.RoomJoin, new JoinRoomRequest
                {
                    code = code,
                    loadout = loadout
                });
            }
            catch (System.Exception exception)
            {
                lobby.SetStatus($"Invio fallito: {exception.Message}");
            }
        }

        private async void LeaveRoom()
        {
            if (client is { IsConnected: true })
            {
                try
                {
                    await dispatcher.SendAsync(MessageTypes.RoomLeave);
                }
                catch (System.Exception exception)
                {
                    lobby.SetStatus($"Uscita dalla stanza fallita: {exception.Message}");
                    return;
                }
            }
            lobby.ClearRoomCode();
            lobby.SetWaitingForOpponent(false);
            lobby.SetStatus("Stanza chiusa. Crea una stanza o cerca un avversario.");
        }

        private async void JoinQueue()
        {
            if (!await EnsureReadyAsync())
                return;
            if (!RequireLoadout(out PvpLoadoutDto loadout))
                return;
            try
            {
                rankedQueueRequested = true;
                lobby.SetStatus("Ingresso in coda...");
                lobby.SetWaitingForOpponent(true);
                await dispatcher.SendAsync(MessageTypes.QueueJoin, new QueueJoinRequest { loadout = loadout });
            }
            catch (System.Exception exception)
            {
                rankedQueueRequested = false;
                lobby.SetWaitingForOpponent(false);
                lobby.SetStatus($"Invio fallito: {exception.Message}");
            }
        }

        private async void CancelQueue()
        {
            // Il feedback deve essere immediato: non teniamo il pannello bloccato mentre
            // aspettiamo la rete e marchiamo come obsolete eventuali QueueStatus in volo.
            rankedQueueRequested = false;
            lobby.SetWaitingForOpponent(false);
            lobby.SetStatus("Ricerca annullata. Puoi continuare a navigare o riprovare.");

            if (client is { IsConnected: true })
            {
                try
                {
                    await dispatcher.SendAsync(MessageTypes.QueueLeave);
                }
                catch (System.Exception exception)
                {
                    lobby.SetStatus($"Annullamento ricerca fallito: {exception.Message}");
                    return;
                }
            }
        }

        /// <summary>Loadout confermato o salvato; null se va ancora composto.</summary>
        private PvpLoadoutDto CurrentLoadout()
        {
            PvpLoadoutDto loadout = confirmedLoadout ?? PvpLoadoutBuilderScreen.LoadSaved();
            if (loadout == null && cardDatabase == null)
                loadout = PvpQuickLoadout.Build(null); // senza database non c'è builder: fallback
            if (loadout == null)
                return null;
            confirmedLoadout = loadout;
            myLoadout = new List<LoadoutCardDto>(loadout.cards);
            return loadout;
        }

        private bool RequireLoadout(out PvpLoadoutDto loadout)
        {
            loadout = CurrentLoadout();
            if (loadout != null)
                return true;
            lobby.SetStatus("Prima componi il tuo loadout!");
            OpenLoadoutBuilder();
            return false;
        }

        private void OpenLoadoutBuilder()
        {
            if (builder != null)
                return;
            lobby.SetVisible(false);
            builder = new PvpLoadoutBuilderScreen(
                canvasRoot,
                cardDatabase,
                confirmed =>
                {
                    confirmedLoadout = confirmed;
                    myLoadout = new List<LoadoutCardDto>(confirmed.cards);
                    CloseLoadoutBuilder("Loadout pronto: crea una stanza o cerca un avversario.");
                },
                () => CloseLoadoutBuilder(null));
        }

        private void CloseLoadoutBuilder(string statusMessage)
        {
            builder?.Destroy();
            builder = null;
            lobby.SetVisible(true);
            if (!string.IsNullOrEmpty(statusMessage))
                lobby.SetStatus(statusMessage);
        }

        // --- Profilo / social ---

        private async void OpenProfile()
        {
            if (!await EnsureReadyAsync())
                return;
            if (profileScreen != null)
                return;
            lobby.SetVisible(false);
            profileScreen = new PvpProfileScreen(canvasRoot, myPlayerId, new PvpProfileScreen.Callbacks
            {
                OnClose = CloseProfile,
                OnRequestProfile = () => Send(MessageTypes.ProfileGet),
                OnRequestIcons = () => Send(MessageTypes.IconsList),
                OnRequestFriends = () => Send(MessageTypes.FriendsList),
                OnRequestLeaderboard = () => Send(MessageTypes.LeaderboardGet),
                OnRequestHallOfFameSeasons = () => Send(MessageTypes.HallOfFameSeasonsGet),
                OnRequestHallOfFame = seasonId =>
                    Send(MessageTypes.HallOfFameGet, new HallOfFameGetRequest { seasonId = seasonId }),
                OnRequestAchievements = () => Send(MessageTypes.AchievementsGet),
                OnSelectIcon = iconId =>
                    Send(MessageTypes.ProfileSetIcon, new SetIconRequest { iconId = iconId }),
                OnAddFriend = username =>
                    Send(MessageTypes.FriendAdd, new FriendAddRequest { username = username }),
                OnRespondFriend = (playerId, accept) =>
                    Send(MessageTypes.FriendRespond, new FriendRespondRequest { playerId = playerId, accept = accept }),
                OnRemoveFriend = playerId =>
                    Send(MessageTypes.FriendRemove, new FriendTargetRequest { playerId = playerId }),
                OnChallengeFriend = ChallengeFriend
            }, ResolveIconArtwork);
        }

        /// <summary>
        /// Sincronizza col server i mostri sconfitti in campagna (sblocco icone).
        /// Aspetta la conferma invece di sperarci: il server risponde già con
        /// campaign.kills_result, e con un requestId il rinvio dopo una caduta di rete
        /// viene riconosciuto invece di essere rieseguito. Il tracker locale è
        /// cumulativo, quindi un fallimento qui si ripara comunque al login successivo:
        /// niente di rumoroso da mostrare al giocatore, solo una riga di log.
        /// </summary>
        private async void ReportCampaignKills()
        {
            string[] families = PvpCampaignKillTracker.All();
            string[] bosses = PvpCampaignKillTracker.AllBosses();
            if (families.Length == 0 && bosses.Length == 0)
                return;
            if (dispatcher == null)
                return;

            try
            {
                await dispatcher.RequestAsync(
                    MessageTypes.CampaignReportKills,
                    new CampaignKillsRequest { monsters = families, bosses = bosses },
                    MessageTypes.CampaignKillsResult,
                    timeoutSeconds: 10f);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PvP] Sincronizzazione uccisioni campagna fallita: {exception.Message}");
            }
        }

        /// <summary>Mappa un id icona all'artwork di una carta: classe eroe o famiglia di mostri.</summary>
        private Sprite ResolveIconArtwork(string iconId)
        {
            if (cardDatabase == null || string.IsNullOrEmpty(iconId))
                return null;
            if (iconId.StartsWith("class-"))
            {
                string className = iconId.Substring("class-".Length);
                foreach (CardDefinition card in cardDatabase.Cards)
                    if (card.HasHeroClass && card.HeroClass.ToString().ToLowerInvariant() == className)
                        return card.Artwork;
            }
            else if (iconId.StartsWith("monster-"))
            {
                string family = iconId.Substring("monster-".Length);
                foreach (CardDefinition card in cardDatabase.Cards)
                    if (card.Id != null && card.Id.Contains($"-{family}-"))
                        return card.Artwork;
            }
            else if (iconId.StartsWith("boss-"))
            {
                foreach (CardDefinition card in cardDatabase.Cards)
                    if (string.Equals(card.Id, iconId, StringComparison.OrdinalIgnoreCase))
                        return card.Artwork;
            }
            else if (iconId.StartsWith("tier-"))
            {
                return PvpUiFactory.RankEmblem(iconId.Substring("tier-".Length));
            }
            return null;
        }

        private void CloseProfile()
        {
            profileScreen?.Destroy();
            profileScreen = null;
            lobby.SetVisible(true);
        }

        private void CreateLeaderboardScreen()
        {
            lobby.SetVisible(false);
            leaderboardScreen = new PvpLeaderboardScreen(
                canvasRoot,
                myPlayerId,
                new PvpLeaderboardScreen.Callbacks
                {
                    OnClose = Close,
                    OnRequestProfile = () => Send(MessageTypes.ProfileGet),
                    OnRequestAccountProgress = () => Send(MessageTypes.SinglePlayerProgressGet),
                    OnRequestLeaderboard = () => Send(MessageTypes.LeaderboardGet),
                    OnRequestHallOfFameSeasons = () => Send(MessageTypes.HallOfFameSeasonsGet),
                    OnRequestHallOfFame = seasonId =>
                        Send(MessageTypes.HallOfFameGet, new HallOfFameGetRequest { seasonId = seasonId })
                },
                ResolveIconArtwork);
        }

        private void ChallengeFriend(string playerId)
        {
            if (!RequireLoadout(out PvpLoadoutDto loadout))
            {
                profileScreen?.SetStatus("Prima componi il tuo loadout!");
                return;
            }
            Send(MessageTypes.FriendChallenge, new FriendChallengeRequest { playerId = playerId, loadout = loadout });
            profileScreen?.SetStatus("Sfida inviata: attendi che l'amico accetti.");
        }

        private void ShowMatchResult(MatchResultData result)
        {
            if (surrenderSent)
            {
                // Ci siamo arresi noi: il verdetto lo conosciamo già, e restare davanti
                // al riepilogo della propria resa non serve a niente. Si torna al menu.
                surrenderSent = false;
                LeaveToLobby();
                lobby.SetStatus(GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpResult.SurrenderLobbyStatus,
                    "Ti sei arreso: la vittoria è andata all'avversario."));
                return;
            }

            resultOverlay?.Destroy();
            resultOverlay = new PvpMatchResultOverlay(canvasRoot, result, () =>
            {
                resultOverlay?.Destroy();
                resultOverlay = null;
                LeaveToLobby();
            }, ClaimRankedExperienceAdMultiplier);
        }

		private async void ClaimRankedExperienceAdMultiplier(
			MatchResultData result, System.Action<bool> completed)
		{
			if (dispatcher == null || result == null
				|| string.IsNullOrWhiteSpace(result.accountExperienceRewardClaimId))
			{
				completed?.Invoke(false);
				return;
			}

			// L'EXP di base della partita e' gia' accreditata: il x3 e' l'extra che paga la
			// pubblicita'. Video chiuso a meta', niente moltiplicatore e niente chiamata al
			// server; rete assente, il x3 e' condonato (la regola sta in AdService).
			AccardND.Ads.AdResult ad = await AccardND.Ads.AdService.ShowAsync(
				AccardND.Ads.AdPlacement.PvpExperienceTriple,
				AccardND.Ads.AdRewardContext.ForClaim(result.accountExperienceRewardClaimId));
			if (!ad.Grants)
			{
				completed?.Invoke(false);
				return;
			}

			try
			{
				Envelope response = await dispatcher.RequestAsync(
					MessageTypes.SinglePlayerClaimAdMultiplier,
					new SinglePlayerAdMultiplierRequest
					{
						rewardClaimId = result.accountExperienceRewardClaimId,
						adImpressionId = ad.ImpressionId
					},
					MessageTypes.SinglePlayerRewardResult,
					timeoutSeconds: 12f);
				completed?.Invoke(response != null && response.type != MessageTypes.Error);
			}
			catch (System.Exception exception)
			{
				Debug.LogWarning($"[PvP] Triplicatore EXP non applicato: {exception.Message}");
				completed?.Invoke(false);
			}
		}

        private void ShowChallengePrompt(FriendChallengeReceived challenge)
        {
            DismissChallengePrompt();
            challengePrompt = PvpUiFactory.CreatePanel(canvasRoot, "Challenge", new Color(0.06f, 0.08f, 0.12f, 0.99f));
            PvpUiFactory.SetAnchors(challengePrompt, new Vector2(0.34f, 0.4f), new Vector2(0.66f, 0.62f));

            Text text = PvpUiFactory.CreateText(
                challengePrompt, "Text", $"{challenge.challengerName} ti sfida!", 26);
            PvpUiFactory.SetAnchors((RectTransform)text.transform, new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.92f));

            string code = challenge.roomCode;
            Button accept = PvpUiFactory.CreateButton(
                challengePrompt, "Accept", "ACCETTA", new Color(0.1f, 0.5f, 0.3f, 0.98f), () => AcceptChallenge(code), 22);
            PvpUiFactory.SetAnchors((RectTransform)accept.transform, new Vector2(0.08f, 0.1f), new Vector2(0.48f, 0.4f));

            Button decline = PvpUiFactory.CreateButton(
                challengePrompt, "Decline", "RIFIUTA", new Color(0.5f, 0.15f, 0.15f, 0.98f), DismissChallengePrompt, 22);
            PvpUiFactory.SetAnchors((RectTransform)decline.transform, new Vector2(0.52f, 0.1f), new Vector2(0.92f, 0.4f));
        }

        private async void AcceptChallenge(string code)
        {
            DismissChallengePrompt();
            if (!await EnsureReadyAsync())
                return;
            if (!RequireLoadout(out PvpLoadoutDto loadout))
                return;
            CloseProfile();
            try
            {
                await dispatcher.SendAsync(MessageTypes.RoomJoin, new JoinRoomRequest { code = code, loadout = loadout });
            }
            catch (System.Exception exception)
            {
                lobby.SetStatus($"Accettazione sfida fallita: {exception.Message}");
            }
        }

        private void DismissChallengePrompt()
        {
            if (challengePrompt != null)
                Destroy(challengePrompt.gameObject);
            challengePrompt = null;
        }

        private async void Send(string type, object payload = null)
        {
            if (dispatcher == null)
            {
                profileScreen?.SetStatus("Connessione al server persa.");
                return;
            }
            try
            {
                // Niente guardia su IsConnected: se il socket è giù il dispatcher
                // tiene il messaggio in coda e lo spedisce alla riconnessione. Solo
                // quelli che scadono con l'istante (una mossa, la coda) rifiutano.
                await dispatcher.SendAsync(type, payload);
            }
            catch (System.Exception exception)
            {
                profileScreen?.SetStatus("Connessione al server persa.");
                Debug.LogWarning($"[PvP] Invio {type} fallito: {exception.Message}");
            }
        }

        // --- IBattlePresentationActions ---

        public void Deploy(int handIndex) =>
            SendMatchAction(new MatchActionDto
            {
                action = MatchActionDto.Deploy,
                handIndex = handIndex
            });

        public void Attack(int enemySlot) =>
            SendMatchAction(new MatchActionDto
            {
                action = MatchActionDto.Attack,
                targetIsEnemy = true,
                targetSlot = enemySlot
            });

        public void UseAbility(bool targetIsEnemy, int targetSlot) =>
            SendMatchAction(new MatchActionDto
            {
                action = MatchActionDto.Ability,
                targetIsEnemy = targetIsEnemy,
                targetSlot = targetSlot
            });

        public void UseSupreme(bool targetIsEnemy, int targetSlot) =>
            SendMatchAction(new MatchActionDto
            {
                action = MatchActionDto.Supreme,
                targetIsEnemy = targetIsEnemy,
                targetSlot = targetSlot
            });

        public void Attach(int allySlot) =>
            SendMatchAction(new MatchActionDto
            {
                action = MatchActionDto.Attach,
                targetIsEnemy = false,
                targetSlot = allySlot
            });

        public void Pass() =>
            SendMatchAction(new MatchActionDto { action = MatchActionDto.Pass });

        public void SubmitDecisive(int[] loadoutIndices) =>
            SendMatchAction(new MatchActionDto
            {
                action = MatchActionDto.Decisive,
                decisiveIndices = loadoutIndices
            });

        /// <summary>
        /// Resa. Non passa dalle azioni di gioco: vale anche a partita in pausa, e la
        /// conferma è il risultato che il server manda subito dopo. Chi si arrende non
        /// si ferma a guardare il tabellone, torna alla lobby.
        /// </summary>
        public async void Surrender()
        {
            if (dispatcher == null || state == null || surrenderSent)
                return;
            surrenderSent = true;
            try
            {
                await dispatcher.SendAsync(MessageTypes.MatchForfeit);
            }
            catch (System.Exception exception)
            {
                surrenderSent = false;
                state?.AddNotice(GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpStatus.SurrenderNotSent,
                    "Resa non inviata: connessione persa."));
                stateDirty = true;
                Debug.LogWarning($"[PvP] Invio resa fallito: {exception.Message}");
            }
        }

        private async void SendMatchAction(MatchActionDto action)
        {
            if (dispatcher == null)
            {
                Debug.LogWarning("[PvP] Azione ignorata: connessione al server persa.");
                return;
            }
            try
            {
                battleSfx?.PlayButtonClick();
                Envelope response = await dispatcher.RequestAsync(
                    MessageTypes.MatchAction,
                    action,
                    MessageTypes.MatchActionAck,
                    timeoutSeconds: 12f);
                if (response?.type == MessageTypes.Error)
                {
                    var error = PvpServerClient.ParsePayload<ErrorMessage>(response);
                    string notice = LocalizedPvpActionError(error);
                    state?.AddNotice(notice);
                    stateDirty = true;
                }
            }
            catch (System.Exception exception)
            {
                // Senza questo avviso una mossa non confermata è indistinguibile da un
                // gioco che non risponde: il tavolo resta com'era e nessuno spiega perché.
                state?.AddNotice(exception is System.TimeoutException
                    ? GameText.GetOrFallbackSilent(
                        GameTextKeys.PvpStatus.ActionNotConfirmed,
                        "Mossa non confermata dal server: riprova.")
                    : GameText.GetOrFallbackSilent(
                        GameTextKeys.PvpStatus.ActionNotSent,
                        "Mossa non inviata: connessione persa."));
                stateDirty = true;
                Debug.LogWarning($"[PvP] Invio azione fallito: {exception.Message}");
            }
        }

        private static string LocalizedPvpActionError(ErrorMessage error)
        {
            return error?.code switch
            {
                PvpActionErrorCodes.AbilityRequiresAction => GameText.GetLocalizedFallback(
                    GameTextKeys.PvpError.AbilityRequiresAction,
                    "Dopo aver usato un'abilità devi attaccare o equipaggiarti.",
                    "After using an ability, you must attack or equip."),
                PvpActionErrorCodes.NotEnoughMana => GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpError.ManaInsufficientGeneric,
                    "Mana insufficiente per eseguire l'azione."),
                PvpActionErrorCodes.SupremeNotAvailable => GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpError.SupremeNotAvailable,
                    "La suprema non è ancora disponibile."),
                _ => error?.message ?? GameText.GetOrFallbackSilent(
                    GameTextKeys.PvpStatus.MoveRejected,
                    "Mossa rifiutata dal server.")
            };
        }

        public async void LeaveToLobby()
        {
            if (client is { IsConnected: true })
            {
                try
                {
                    await dispatcher.SendAsync(MessageTypes.RoomLeave);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[PvP] Uscita stanza fallita: {exception.Message}");
                }
            }
            matchView?.HidePvpMatch();
            state = null;
            lobby.ClearRoomCode();
            lobby.SetWaitingForOpponent(false);
            lobby.SetVisible(true);
        }

        public void CloseFromMainMenu()
        {
            Close(invokeCallback: false);
        }

        /// <summary>Chiude il PvP e notifica l'Hub, usato dal pulsante Home condiviso.</summary>
        public void CloseToHub()
        {
            Close(invokeCallback: true);
        }

        // --- Setup ---

        /// <summary>
        /// Nome da mostrare al server nel login UGS. Non e' piu' una credenziale:
        /// gli account si creano solo con Google, quindi qui serve solo un'etichetta
        /// leggibile finche' il giocatore non sceglie il nickname.
        /// </summary>
        private void EnsureDisplayName()
        {
            // In editor il suffisso distingue l'istanza dalla build affiancata nei test locali.
            string editorSuffix = Application.isEditor ? "-editor" : string.Empty;
            string savedNickname = SanitizeNickname(PlayerPrefs.GetString(NicknamePrefsKey, string.Empty));
            if (!string.IsNullOrWhiteSpace(savedNickname))
                username = savedNickname + editorSuffix;
            else if (string.IsNullOrWhiteSpace(username))
            {
                int hash = SystemInfo.deviceUniqueIdentifier.GetHashCode();
                username = $"giocatore-{(uint)hash % 100000:D5}{editorSuffix}";
            }
        }

        private void BuildCanvas()
        {
            configuration ??= Resources.Load<GameConfiguration>("GameConfiguration");
            if (configuration == null)
                configuration = ScriptableObject.CreateInstance<GameConfiguration>();
            cardDatabase ??= Resources.Load<CardDatabase>("CardDatabase");
            InitializeAudio();

            canvasObject = new GameObject("PvpCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // La classifica lanciata dall'Hub resta sotto il suo banner account
            // standard (sorting 901). Le altre schermate PvP continuano a coprire
            // integralmente l'Hub.
            canvas.sortingOrder = startInLeaderboard ? 900 : 950;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // Landscape/portrait ready: match e risoluzione seguono l'orientamento,
            // e le schermate vivono dentro la safe area (notch e barre di sistema).
            canvasObject.AddComponent<AdaptiveCanvasScaler>();
            var safeArea = new GameObject("Safe Area", typeof(RectTransform), typeof(SafeAreaRect));
            safeArea.transform.SetParent(canvasObject.transform, false);
            canvasRoot = safeArea.transform;

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void ShowReconnectOverlay(string message)
        {
            if (reconnectOverlay == null)
            {
                reconnectOverlay = PvpUiFactory.CreatePanel(
                    canvasRoot, "Match Reconnect Overlay", new Color(0f, 0f, 0f, 0.82f));
                PvpUiFactory.Stretch(reconnectOverlay);
                reconnectOverlayText = PvpUiFactory.CreateTitleText(
                    reconnectOverlay, "Reconnect Status", string.Empty, 34);
                reconnectOverlayText.alignment = TextAnchor.MiddleCenter;
                PvpUiFactory.SetAnchors(
                    (RectTransform)reconnectOverlayText.transform,
                    new Vector2(0.18f, 0.36f),
                    new Vector2(0.82f, 0.64f));
            }
            reconnectOverlay.gameObject.SetActive(true);
            reconnectOverlay.SetAsLastSibling();
            if (!string.IsNullOrEmpty(message))
                reconnectOverlayText.text = message;
        }

        private void UpdateReconnectOverlay()
        {
            if (reconnectOverlay == null || !reconnectOverlay.gameObject.activeSelf
                || string.IsNullOrEmpty(disconnectedOpponentName))
                return;
            int seconds = Mathf.Max(0, Mathf.CeilToInt(opponentReconnectDeadline - Time.unscaledTime));
            reconnectOverlayText.text =
                $"{disconnectedOpponentName.ToUpperInvariant()} È DISCONNESSO\n"
                + $"Attesa riconnessione: {seconds}s";
        }

        private void HideReconnectOverlay()
        {
            disconnectedOpponentName = null;
            opponentReconnectDeadline = 0f;
            reconnectOverlay?.gameObject.SetActive(false);
        }

        private void ShowLobby()
        {
            lobby = new PvpLobbyScreen(canvasRoot, username, new PvpLobbyScreen.Callbacks
            {
                OnClose = Close,
                OnCreateRoom = CreateRoom,
                OnJoinRoom = JoinRoom,
                OnQueue = JoinQueue,
                OnCancelQueue = CancelQueue,
                OnLeaveRoom = LeaveRoom,
                OnLoadout = OpenLoadoutBuilder,
                OnProfile = OpenProfile,
                OnSettings = OpenSettings,
                OnRefreshRooms = () => Send(MessageTypes.RoomsList)
            }, ResolveIconArtwork);
        }

        private void OpenSettings()
        {
            onSettings?.Invoke();
        }

        private void OpenNicknameDialog()
        {
            if (nicknameDialog != null)
            {
                nicknameDialog.gameObject.SetActive(true);
                nicknameInput?.ActivateInputField();
                return;
            }

            nicknameDialog = PvpUiFactory.CreatePanel(canvasRoot, "Nickname Dialog Overlay", new Color(0f, 0f, 0f, 0.72f));
            PvpUiFactory.Stretch(nicknameDialog);

            RectTransform dialog = PvpUiFactory.CreateSoftPanel(nicknameDialog, "Nickname Dialog", new Color(0.02f, 0.035f, 0.055f, 0.98f));
            PvpUiFactory.SetAnchors(dialog, new Vector2(0.3f, 0.34f), new Vector2(0.7f, 0.68f));

            Text title = PvpUiFactory.CreateTitleText(dialog, "Title", "CAMBIA NICKNAME", 26);
            title.color = PvpUiFactory.Gold;
            PvpUiFactory.SetAnchors((RectTransform)title.transform, new Vector2(0.06f, 0.75f), new Vector2(0.94f, 0.94f));

            Text hint = PvpUiFactory.CreateLabel(dialog, "Hint", "Il nome viene associato al tuo account di gioco.", 16, TextAnchor.MiddleCenter);
            PvpUiFactory.SetAnchors((RectTransform)hint.transform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.75f));

            RectTransform inputPanel = PvpUiFactory.CreateSoftPanel(dialog, "Nickname Input Panel", new Color(0.06f, 0.09f, 0.12f, 0.98f));
            PvpUiFactory.SetAnchors(inputPanel, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.58f));
            nicknameInput = inputPanel.gameObject.AddComponent<InputField>();
            nicknameInput.characterLimit = NicknameMaxLength;
            nicknameInput.text = StripEditorSuffix(username);
            nicknameInput.textComponent = PvpUiFactory.CreateText(inputPanel, "Input Text", nicknameInput.text, 22, TextAnchor.MiddleLeft, FontStyle.Normal);
            nicknameInput.textComponent.color = Color.white;
            PvpUiFactory.SetAnchors((RectTransform)nicknameInput.textComponent.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
            nicknameInput.placeholder = PvpUiFactory.CreateLabel(inputPanel, "Placeholder", "Nuovo nickname", 20, TextAnchor.MiddleLeft);
            PvpUiFactory.SetAnchors((RectTransform)nicknameInput.placeholder.transform, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));

            nicknameStatusText = PvpUiFactory.CreateLabel(dialog, "Status", string.Empty, 16, TextAnchor.MiddleCenter);
            nicknameStatusText.color = PvpUiFactory.Bad;
            PvpUiFactory.SetAnchors((RectTransform)nicknameStatusText.transform, new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.41f));

            Button cancel = PvpUiFactory.CreateButton(dialog, "Cancel", "ANNULLA", new Color(0.38f, 0.12f, 0.12f, 0.98f), CloseNicknameDialog, 18);
            PvpUiFactory.SetAnchors((RectTransform)cancel.transform, new Vector2(0.08f, 0.1f), new Vector2(0.44f, 0.26f));

            Button confirm = PvpUiFactory.CreateButton(dialog, "Confirm", "SALVA", new Color(0.08f, 0.38f, 0.32f, 0.98f), ApplyNicknameChange, 18);
            PvpUiFactory.SetAnchors((RectTransform)confirm.transform, new Vector2(0.56f, 0.1f), new Vector2(0.92f, 0.26f));

            nicknameInput.ActivateInputField();
        }

        private void CloseNicknameDialog()
        {
            if (nicknameRequired)
            {
                if (nicknameStatusText != null)
                    nicknameStatusText.text = "Devi scegliere un nickname per continuare.";
                return;
            }
            if (nicknameDialog != null)
                nicknameDialog.gameObject.SetActive(false);
        }

        private async void ApplyNicknameChange()
        {
            string newNickname = SanitizeNickname(nicknameInput != null ? nicknameInput.text : string.Empty);
            if (newNickname.Length < 3)
            {
                if (nicknameStatusText != null)
                    nicknameStatusText.text = "Usa almeno 3 caratteri.";
                return;
            }

            if (!await EnsureReadyAsync())
                return;
            if (nicknameStatusText != null)
                nicknameStatusText.text = "Salvataggio...";
            await dispatcher.SendAsync(MessageTypes.NicknameSet, new SetNicknameRequest
            {
                nickname = newNickname
            });
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

        private static string StripEditorSuffix(string value)
        {
            const string suffix = "-editor";
            if (Application.isEditor && !string.IsNullOrEmpty(value) && value.EndsWith(suffix, System.StringComparison.Ordinal))
                return value.Substring(0, value.Length - suffix.Length);
            return value ?? string.Empty;
        }

        private void CompleteAuthentication(AuthResponse auth, bool adoptConnection)
        {
            if (auth is not { ok: true })
                return;

            authenticated = true;
            myPlayerId = auth.playerId;
            username = StripEditorSuffix(auth.username);
            nicknameRequired = auth.requiresNickname;

            if (!string.IsNullOrWhiteSpace(username))
            {
                PlayerPrefs.SetString(NicknamePrefsKey, username);
                PlayerPrefs.SetString(PlayerHudNamePrefsKey, username);
                PlayerPrefs.Save();
            }

            lobby.SetPlayerName(string.IsNullOrWhiteSpace(username) ? "Guest" : username);
            SetConnectionStatus($"Account caricato: {username}.");
            leaderboardScreen?.SetIdentity(auth.playerId);

            if (adoptConnection)
            {
                AccountServerSession.Adopt(
                    client,
                    dispatcher,
                    auth,
                    serverUrl,
                    BuildGoogleRefreshRequestAsync);
                ownsConnection = false;
            }

            if (nicknameRequired)
            {
                SetConnectionStatus("Scegli un nickname univoco per continuare.");
                OpenNicknameDialog();
            }

            if (startInLeaderboard)
                leaderboardScreen?.RequestInitialData();
            else
            {
                Send(MessageTypes.ProfileGet);
                Send(MessageTypes.SinglePlayerProgressGet);
            }
            ReportCampaignKills();
        }

        private static async System.Threading.Tasks.Task<UgsLoginRequest> BuildGoogleRefreshRequestAsync()
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
                    googleIdToken = PvpUgsAuth.LastGoogleIdToken
                };
        }

        private static void CachePvpLeague(ProfileData data)
        {
            if (data == null || !data.ranked || data.placement)
            {
                PlayerPrefs.DeleteKey("AccardND.PvpTier");
                PlayerPrefs.DeleteKey("AccardND.PvpDivision");
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetString("AccardND.PvpTier", data.tier ?? string.Empty);
            PlayerPrefs.SetString("AccardND.PvpDivision", data.division ?? string.Empty);
            PlayerPrefs.Save();
        }

        private void Close()
        {
            Close(invokeCallback: true);
        }

        private void Close(bool invokeCallback)
        {
            matchView?.HidePvpMatch();
            state = null;
            DismissChallengePrompt();
            resultOverlay?.Destroy();
            resultOverlay = null;
            profileScreen?.Destroy();
            profileScreen = null;
            leaderboardScreen?.Destroy();
            leaderboardScreen = null;
            ReleaseConnection();
            if (canvasObject != null)
                Destroy(canvasObject);
            System.Action callback = invokeCallback ? onClosed : null;
            onClosed = null;
            Destroy(gameObject);
            callback?.Invoke();
        }

        private void ShowMatch()
        {
            CloseLoadoutBuilder(null);
            profileScreen?.Destroy();
            profileScreen = null;
            leaderboardScreen?.Destroy();
            leaderboardScreen = null;
            lobby.ClearRoomCode();
            lobby.SetWaitingForOpponent(false);
            lobby.SetVisible(false);
            matchView?.ShowPvpMatch(state, myLoadout, this);
        }

        private void OnDestroy()
        {
            ReleaseConnection();
        }

        private void ReleaseConnection()
        {
            if (dispatcher != null)
                dispatcher.UnhandledMessage -= HandleMessage;
            UnsubscribeConnectionEvents();
            if (ownsConnection)
                client?.Dispose();
            client = null;
            dispatcher = null;
            ownsConnection = false;
        }

        private void InitializeAudio()
        {
            if (battleSfx != null)
                return;

            battleSfx = new BattleSfxPlayer();
            battleSfx.Initialize(transform, "PvP Battle SFX Audio Source");
        }
    }
}
