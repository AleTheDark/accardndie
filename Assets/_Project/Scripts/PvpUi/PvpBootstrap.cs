using System.Collections.Generic;
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
    public sealed class PvpBootstrap : MonoBehaviour, IPvpMatchActions
    {
        [SerializeField] private string serverUrl = "ws://localhost:5017/ws";
        [SerializeField, Tooltip("Vuoto = account ospite legato al dispositivo.")]
        private string username = string.Empty;
        [SerializeField] private string password = string.Empty;
        [SerializeField] private CardDatabase cardDatabase;

        private PvpServerClient client;
        private PvpClientMatchState state;
        private PvpLobbyScreen lobby;
        private PvpMatchScreen match;
        private Transform canvasRoot;
        private GameObject canvasObject;
        private List<LoadoutCardDto> myLoadout;
        private System.Action onClosed;
        private bool loginAttempted;
        private bool stateDirty;

        /// <summary>Da chiamare subito dopo AddComponent quando il PvP è lanciato dal menu di gioco.</summary>
        public void Configure(CardDatabase database, System.Action closedCallback)
        {
            cardDatabase = database;
            onClosed = closedCallback;
        }

        private async void Start()
        {
            EnsureGuestCredentials();
            BuildCanvas();
            ShowLobby();

            client = new PvpServerClient();
            try
            {
                await client.ConnectAsync(serverUrl);
                lobby.SetStatus($"Connesso a {serverUrl}. Autenticazione...");
                await client.SendAsync(MessageTypes.AuthRegister, new RegisterRequest
                {
                    username = username,
                    password = password
                });
            }
            catch (System.Exception exception)
            {
                lobby.SetStatus($"Connessione fallita: {exception.Message}\nAvvia il server con dotnet run in Server/AccardND.Server.");
            }
        }

        private void Update()
        {
            if (client == null)
                return;
            while (client.TryDequeueMessage(out Envelope envelope))
                HandleMessage(envelope);
            if (stateDirty)
            {
                stateDirty = false;
                match?.Rebuild();
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
                        lobby.SetStatus($"Autenticato come {auth.username}. Scegli come giocare.");
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
                    ShowMatch();
                    break;
                }

                case MessageTypes.MatchHand:
                    state?.ApplyHand(PvpServerClient.ParsePayload<MatchHand>(envelope));
                    break;

                case MessageTypes.MatchEvent:
                    state?.Apply(PvpServerClient.ParsePayload<MatchEventDto>(envelope));
                    break;

                case MessageTypes.MatchOpponentLeft:
                    if (match != null)
                        lobby.SetStatus("L'avversario ha lasciato la partita.");
                    LeaveToLobby();
                    break;

                case MessageTypes.Error:
                {
                    var error = PvpServerClient.ParsePayload<ErrorMessage>(envelope);
                    Debug.LogWarning($"[PvP] {error.code}: {error.message}");
                    if (match == null)
                        lobby.SetStatus($"Errore: {error.message}");
                    break;
                }
            }
        }

        // --- Azioni lobby ---

        private async void CreateRoom()
        {
            PvpLoadoutDto loadout = CurrentLoadout();
            lobby.SetStatus("Creazione stanza...");
            await client.SendAsync(MessageTypes.RoomCreate, new CreateRoomRequest { loadout = loadout });
        }

        private async void JoinRoom()
        {
            string code = lobby.TypedRoomCode;
            if (string.IsNullOrWhiteSpace(code) || code.Length < 6)
            {
                lobby.SetStatus("Componi il codice a 6 caratteri col tastierino.");
                return;
            }
            lobby.SetStatus($"Ingresso nella stanza {code}...");
            await client.SendAsync(MessageTypes.RoomJoin, new JoinRoomRequest
            {
                code = code,
                loadout = CurrentLoadout()
            });
        }

        private async void JoinQueue()
        {
            lobby.SetStatus("Ingresso in coda...");
            await client.SendAsync(MessageTypes.QueueJoin, new QueueJoinRequest { loadout = CurrentLoadout() });
        }

        private PvpLoadoutDto CurrentLoadout()
        {
            PvpLoadoutDto loadout = PvpQuickLoadout.Build(cardDatabase);
            myLoadout = new List<LoadoutCardDto>(loadout.cards);
            return loadout;
        }

        // --- IPvpMatchActions ---

        public async void Deploy(int handIndex) =>
            await client.SendAsync(MessageTypes.MatchAction, new MatchActionDto
            {
                action = MatchActionDto.Deploy,
                handIndex = handIndex
            });

        public async void Attack(int enemySlot) =>
            await client.SendAsync(MessageTypes.MatchAction, new MatchActionDto
            {
                action = MatchActionDto.Attack,
                targetIsEnemy = true,
                targetSlot = enemySlot
            });

        public async void UseAbility(bool targetIsEnemy, int targetSlot) =>
            await client.SendAsync(MessageTypes.MatchAction, new MatchActionDto
            {
                action = MatchActionDto.Ability,
                targetIsEnemy = targetIsEnemy,
                targetSlot = targetSlot
            });

        public async void Attach(int allySlot) =>
            await client.SendAsync(MessageTypes.MatchAction, new MatchActionDto
            {
                action = MatchActionDto.Attach,
                targetIsEnemy = false,
                targetSlot = allySlot
            });

        public async void Pass() =>
            await client.SendAsync(MessageTypes.MatchAction, new MatchActionDto
            {
                action = MatchActionDto.Pass
            });

        public async void SubmitDecisive(int[] loadoutIndices) =>
            await client.SendAsync(MessageTypes.MatchAction, new MatchActionDto
            {
                action = MatchActionDto.Decisive,
                decisiveIndices = loadoutIndices
            });

        public void LeaveToLobby()
        {
            match?.Destroy();
            match = null;
            state = null;
            lobby.SetVisible(true);
        }

        // --- Setup ---

        private void EnsureGuestCredentials()
        {
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                return;
            // Account ospite stabile per dispositivo: nessun inserimento richiesto.
            string device = SystemInfo.deviceUniqueIdentifier;
            int hash = device.GetHashCode();
            username = $"ospite-{(uint)hash % 100000:D5}";
            password = $"dev-{device.Substring(0, Mathf.Min(12, device.Length))}";
        }

        private void BuildCanvas()
        {
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
            lobby = new PvpLobbyScreen(canvasRoot, username, CreateRoom, JoinRoom, JoinQueue, Close);
        }

        private void Close()
        {
            client?.Dispose();
            client = null;
            if (canvasObject != null)
                Destroy(canvasObject);
            System.Action callback = onClosed;
            onClosed = null;
            Destroy(gameObject);
            callback?.Invoke();
        }

        private void ShowMatch()
        {
            lobby.SetVisible(false);
            match?.Destroy();
            match = new PvpMatchScreen(canvasRoot, state, this, myLoadout ?? new List<LoadoutCardDto>());
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null;
        }
    }
}
