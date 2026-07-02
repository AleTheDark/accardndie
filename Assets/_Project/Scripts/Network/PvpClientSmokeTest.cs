using System.Collections.Generic;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Test manuale del flusso PvP: metti questo componente su un GameObject,
    /// avvia il server (dotnet run in Server/AccardND.Server) e premi Play.
    /// Con due istanze (una CreateRoom, una JoinRoom col codice loggato, oppure
    /// entrambe Queue) si arriva fino a match.start + mano privata.
    /// </summary>
    public sealed class PvpClientSmokeTest : MonoBehaviour
    {
        public enum LobbyAction
        {
            CreateRoom,
            JoinRoom,
            Queue
        }

        [SerializeField] private string serverUrl = "ws://localhost:5017/ws";
        [SerializeField] private string username = "tester1";
        [SerializeField] private string password = "password";
        [SerializeField] private LobbyAction action = LobbyAction.CreateRoom;
        [SerializeField] private string roomCodeToJoin = string.Empty;

        private PvpServerClient client;
        private bool loginAttempted;

        private async void Start()
        {
            client = new PvpServerClient();
            await client.ConnectAsync(serverUrl);
            Debug.Log($"[PvP] Connesso a {serverUrl}");
            await client.SendAsync(MessageTypes.AuthRegister, new RegisterRequest
            {
                username = username,
                password = password
            });
        }

        private async void Update()
        {
            if (client == null)
                return;

            while (client.TryDequeueMessage(out Envelope envelope))
            {
                Debug.Log($"[PvP] ← {envelope.type}: {envelope.payload}");
                switch (envelope.type)
                {
                    case MessageTypes.AuthResponse:
                        var auth = PvpServerClient.ParsePayload<AuthResponse>(envelope);
                        if (auth.ok)
                        {
                            Debug.Log($"[PvP] Autenticato come '{auth.username}'");
                            await client.SendAsync(MessageTypes.RulesGet);
                            await SendLobbyRequestAsync();
                        }
                        else if (!loginAttempted)
                        {
                            // L'account esiste già dai run precedenti: prova il login.
                            loginAttempted = true;
                            await client.SendAsync(MessageTypes.AuthLogin, new LoginRequest
                            {
                                username = username,
                                password = password
                            });
                        }
                        else
                        {
                            Debug.LogError($"[PvP] Autenticazione fallita: {auth.error}");
                        }
                        break;

                    case MessageTypes.RoomCreated:
                        var created = PvpServerClient.ParsePayload<RoomCreated>(envelope);
                        Debug.Log($"[PvP] Stanza creata, codice da condividere: {created.code}");
                        break;

                    case MessageTypes.MatchStart:
                        var start = PvpServerClient.ParsePayload<MatchStart>(envelope);
                        Debug.Log($"[PvP] Match contro '{start.opponentName}': iniziativa {start.yourInitiative} vs {start.opponentInitiative}, "
                            + (start.youDeployFirst ? "schieri per primo" : "schiera prima l'avversario"));
                        break;

                    case MessageTypes.MatchHand:
                        var hand = PvpServerClient.ParsePayload<MatchHand>(envelope);
                        Debug.Log($"[PvP] Mano round {hand.roundNumber}: {string.Join(", ", hand.handDefinitionIds)}");
                        break;
                }
            }
        }

        private async System.Threading.Tasks.Task SendLobbyRequestAsync()
        {
            PvpLoadoutDto loadout = BuildTestLoadout();
            switch (action)
            {
                case LobbyAction.CreateRoom:
                    await client.SendAsync(MessageTypes.RoomCreate, new CreateRoomRequest { loadout = loadout });
                    break;
                case LobbyAction.JoinRoom:
                    await client.SendAsync(MessageTypes.RoomJoin, new JoinRoomRequest
                    {
                        code = roomCodeToJoin,
                        loadout = loadout
                    });
                    break;
                case LobbyAction.Queue:
                    await client.SendAsync(MessageTypes.QueueJoin, new QueueJoinRequest { loadout = loadout });
                    break;
            }
        }

        private static PvpLoadoutDto BuildTestLoadout()
        {
            // Loadout minimo legale: 9 carte valore 2 (18 punti) + D3 gratis + bag D6+D8 (6 punti).
            var cards = new List<LoadoutCardDto>();
            for (int index = 0; index < 9; index++)
                cards.Add(new LoadoutCardDto { definitionId = $"2-goblin-test-{index}", value = 2 });
            return new PvpLoadoutDto
            {
                cards = cards.ToArray(),
                baseDieSides = 3,
                bagDiceSides = new[] { 6, 8 }
            };
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null;
        }
    }
}
