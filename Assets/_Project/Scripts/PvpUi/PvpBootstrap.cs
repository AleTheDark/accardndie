using System.Collections.Generic;
using AccardND.Battlefield;
using AccardND.GameData;
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
        [SerializeField] private string serverUrl = "ws://217.160.212.85:5017/ws";
        [SerializeField, Tooltip("Vuoto = account ospite legato al dispositivo.")]
        private string username = string.Empty;
        [SerializeField] private string password = string.Empty;
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private GameConfiguration configuration;

        private PvpServerClient client;
        private PvpClientMatchState state;
        private PvpLobbyScreen lobby;
        private PvpProfileScreen profileScreen;
        private PvpMatchResultOverlay resultOverlay;
        private RectTransform challengePrompt;
        private string myPlayerId = string.Empty;
        private PvpLoadoutBuilderScreen builder;
        private PvpLoadoutDto confirmedLoadout;
        private Transform canvasRoot;
        private GameObject canvasObject;
        private List<LoadoutCardDto> myLoadout;
        private IPvpMatchView matchView;
        private System.Action onClosed;
        private bool registerAttempted;
        private bool loginAttempted;
        private bool authenticated;
        private bool connecting;
        private bool stateDirty;
        private BattleSfxPlayer battleSfx;
        private readonly List<BattlePresentationEvent> pendingAnimationEvents = new();

        /// <summary>Da chiamare subito dopo AddComponent quando il PvP è lanciato dal menu di gioco.</summary>
        public void Configure(CardDatabase database, System.Action closedCallback, IPvpMatchView view = null)
        {
            cardDatabase = database;
            onClosed = closedCallback;
            matchView = view;
        }

        private async void Start()
        {
            EnsureGuestCredentials();
            BuildCanvas();
            ShowLobby();
            client = new PvpServerClient();
            await ConnectAndAuthenticateAsync();
        }

        private async System.Threading.Tasks.Task ConnectAndAuthenticateAsync()
        {
            if (connecting || client == null || client.IsConnected)
                return;
            connecting = true;
            try
            {
                lobby.SetStatus($"Connessione a {serverUrl}...");
                await client.ConnectAsync(serverUrl);
                lobby.SetStatus("Connesso. Autenticazione...");
                registerAttempted = false;
                loginAttempted = false;

                // Prima scelta: Unity Authentication (login anonimo, niente password).
                if (PvpUgsAuth.IsAvailable)
                {
                    (string accessToken, _) = await PvpUgsAuth.SignInAnonymouslyAsync();
                    if (accessToken != null)
                    {
                        await client.SendAsync(MessageTypes.AuthUgs, new UgsLoginRequest
                        {
                            accessToken = accessToken,
                            displayName = username
                        });
                        return;
                    }
                    lobby.SetStatus("Unity Auth non disponibile: uso l'account locale...");
                }

                registerAttempted = true;
                await client.SendAsync(MessageTypes.AuthRegister, new RegisterRequest
                {
                    username = username,
                    password = password
                });
            }
            catch (System.Exception exception)
            {
                lobby.SetStatus(
                    $"Server non raggiungibile ({exception.Message}).\n"
                    + "Avvia il server con: dotnet run in Server/AccardND.Server - poi premi di nuovo un pulsante.");
            }
            finally
            {
                connecting = false;
            }
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
            if (client == null)
                return;
            while (client.TryDequeueMessage(out Envelope envelope))
                HandleMessage(envelope);
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
                    {
                        authenticated = true;
                        myPlayerId = auth.playerId;
                        lobby.SetStatus($"Autenticato come {auth.username}. Scegli come giocare.");
                        ReportCampaignKills();
                    }
                    else if (!registerAttempted)
                    {
                        // L'auth UGS è stata rifiutata dal server: fallback all'account locale.
                        registerAttempted = true;
                        await client.SendAsync(MessageTypes.AuthRegister, new RegisterRequest
                        {
                            username = username,
                            password = password
                        });
                    }
                    else if (!loginAttempted)
                    {
                        loginAttempted = true;
                        await client.SendAsync(MessageTypes.AuthLogin, new LoginRequest
                        {
                            username = username,
                            password = password
                        });
                    }
                    else
                    {
                        lobby.SetStatus($"Autenticazione fallita: {auth.error}");
                    }
                    break;
                }

                case MessageTypes.RoomCreated:
                {
                    var created = PvpServerClient.ParsePayload<RoomCreated>(envelope);
                    lobby.ShowRoomCode(created.code);
                    lobby.SetStatus("Condividi il codice: il match parte quando l'avversario entra.");
                    break;
                }

                case MessageTypes.QueueStatus:
                    lobby.SetStatus("In coda: in attesa di un avversario...");
                    break;

                case MessageTypes.MatchFound:
                    lobby.SetStatus("Avversario trovato!");
                    break;

                case MessageTypes.MatchStart:
                {
                    state = new PvpClientMatchState();
                    state.Changed += () => stateDirty = true;
                    state.ApplyMatchStart(PvpServerClient.ParsePayload<MatchStart>(envelope));
                    Debug.Log($"[PvP] Match iniziato: sono G{state.MyIndex} contro '{state.OpponentName}'.");
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
                    profileScreen?.SetProfile(data);
                    profileScreen?.SyncSelectedIcon(data.selectedIconId);
                    break;
                }

                case MessageTypes.IconsData:
                    profileScreen?.SetIcons(PvpServerClient.ParsePayload<IconsData>(envelope));
                    break;

                case MessageTypes.FriendsData:
                    profileScreen?.SetFriends(PvpServerClient.ParsePayload<FriendsData>(envelope));
                    break;

                case MessageTypes.LeaderboardData:
                    profileScreen?.SetLeaderboard(PvpServerClient.ParsePayload<LeaderboardData>(envelope));
                    break;

                case MessageTypes.HallOfFameSeasonsData:
                    profileScreen?.SetHallOfFameSeasons(PvpServerClient.ParsePayload<HallOfFameSeasonsData>(envelope));
                    break;

                case MessageTypes.HallOfFameData:
                    profileScreen?.SetHallOfFame(PvpServerClient.ParsePayload<HallOfFameData>(envelope));
                    break;

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
                    else if (state == null)
                        lobby.SetStatus($"Errore: {error.message}");
                    break;
                }
            }
        }

        // --- Azioni lobby ---

        private async void CreateRoom()
        {
            if (!await EnsureReadyAsync())
                return;
            if (!RequireLoadout(out PvpLoadoutDto loadout))
                return;
            try
            {
                lobby.SetStatus("Creazione stanza...");
                await client.SendAsync(MessageTypes.RoomCreate, new CreateRoomRequest { loadout = loadout });
            }
            catch (System.Exception exception)
            {
                lobby.SetStatus($"Invio fallito: {exception.Message}");
            }
        }

        private async void JoinRoom()
        {
            if (!await EnsureReadyAsync())
                return;
            string code = lobby.TypedRoomCode;
            if (string.IsNullOrWhiteSpace(code) || code.Length < 6)
            {
                lobby.SetStatus("Componi il codice a 6 caratteri col tastierino.");
                return;
            }
            if (!RequireLoadout(out PvpLoadoutDto loadout))
                return;
            try
            {
                lobby.SetStatus($"Ingresso nella stanza {code}...");
                await client.SendAsync(MessageTypes.RoomJoin, new JoinRoomRequest
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

        private async void JoinQueue()
        {
            if (!await EnsureReadyAsync())
                return;
            if (!RequireLoadout(out PvpLoadoutDto loadout))
                return;
            try
            {
                lobby.SetStatus("Ingresso in coda...");
                await client.SendAsync(MessageTypes.QueueJoin, new QueueJoinRequest { loadout = loadout });
            }
            catch (System.Exception exception)
            {
                lobby.SetStatus($"Invio fallito: {exception.Message}");
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

        /// <summary>Sincronizza col server i mostri sconfitti in campagna (sblocco icone).</summary>
        private void ReportCampaignKills()
        {
            string[] families = PvpCampaignKillTracker.All();
            if (families.Length > 0)
                Send(MessageTypes.CampaignReportKills, new CampaignKillsRequest { monsters = families });
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
            return null;
        }

        private void CloseProfile()
        {
            profileScreen?.Destroy();
            profileScreen = null;
            lobby.SetVisible(true);
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
            resultOverlay?.Destroy();
            resultOverlay = new PvpMatchResultOverlay(canvasRoot, result, () =>
            {
                resultOverlay?.Destroy();
                resultOverlay = null;
                LeaveToLobby();
            });
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
                await client.SendAsync(MessageTypes.RoomJoin, new JoinRoomRequest { code = code, loadout = loadout });
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
            if (client is not { IsConnected: true })
            {
                profileScreen?.SetStatus("Connessione al server persa.");
                return;
            }
            try
            {
                await client.SendAsync(type, payload);
            }
            catch (System.Exception exception)
            {
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

        private async void SendMatchAction(MatchActionDto action)
        {
            if (client is not { IsConnected: true })
            {
                Debug.LogWarning("[PvP] Azione ignorata: connessione al server persa.");
                return;
            }
            try
            {
                battleSfx?.PlayButtonClick();
                await client.SendAsync(MessageTypes.MatchAction, action);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PvP] Invio azione fallito: {exception.Message}");
            }
        }

        public async void LeaveToLobby()
        {
            if (client is { IsConnected: true })
            {
                try
                {
                    await client.SendAsync(MessageTypes.RoomLeave);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[PvP] Uscita stanza fallita: {exception.Message}");
                }
            }
            matchView?.HidePvpMatch();
            state = null;
            lobby.SetVisible(true);
        }

        public void CloseFromMainMenu()
        {
            Close(invokeCallback: false);
        }

        // --- Setup ---

        private void EnsureGuestCredentials()
        {
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                return;
            // Account ospite stabile per dispositivo: nessun inserimento richiesto.
            // In editor il suffisso distingue l'istanza dalla build affiancata nei test locali.
            string device = SystemInfo.deviceUniqueIdentifier;
            int hash = device.GetHashCode();
            string editorSuffix = Application.isEditor ? "-editor" : string.Empty;
            username = $"ospite-{(uint)hash % 100000:D5}{editorSuffix}";
            password = $"dev-{device.Substring(0, Mathf.Min(12, device.Length))}";
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
            canvas.sortingOrder = 950;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot = canvasObject.transform;

            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void ShowLobby()
        {
            lobby = new PvpLobbyScreen(
                canvasRoot, username, CreateRoom, JoinRoom, JoinQueue, OpenLoadoutBuilder, OpenProfile, Close);
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
            client?.Dispose();
            client = null;
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
            lobby.SetVisible(false);
            matchView?.ShowPvpMatch(state, myLoadout, this);
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null;
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
