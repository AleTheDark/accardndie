using System.Collections.Generic;
using AccardND.GameCore;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Auto-player di prova: connette, autentica, entra in stanza o coda e gioca
    /// l'intero match da solo (schiera la prima carta, attacca il primo slot vivo).
    /// Avvia il server con dotnet run in Server/AccardND.Server, poi due istanze
    /// di questo componente (CreateRoom + JoinRoom col codice, oppure due Queue).
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
        private int myIndex = -1;
        private readonly bool[,] enemyEliminated = new bool[2, 3];

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
                switch (envelope.type)
                {
                    case MessageTypes.AuthResponse:
                        var auth = PvpServerClient.ParsePayload<AuthResponse>(envelope);
                        if (auth.ok)
                        {
                            Debug.Log($"[PvP] Autenticato come '{auth.username}'");
                            await SendLobbyRequestAsync();
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
                            Debug.LogError($"[PvP] Autenticazione fallita: {auth.error}");
                        }
                        break;

                    case MessageTypes.RoomCreated:
                        var created = PvpServerClient.ParsePayload<RoomCreated>(envelope);
                        Debug.Log($"[PvP] Stanza creata, codice da condividere: {created.code}");
                        break;

                    case MessageTypes.MatchStart:
                        var start = PvpServerClient.ParsePayload<MatchStart>(envelope);
                        myIndex = start.yourPlayerIndex;
                        Debug.Log($"[PvP] Match contro '{start.opponentName}' - sei il giocatore {myIndex}");
                        break;

                    case MessageTypes.MatchHand:
                        var hand = PvpServerClient.ParsePayload<MatchHand>(envelope);
                        Debug.Log($"[PvP] Mano round {hand.roundNumber}: {string.Join(", ", hand.handDefinitionIds)}");
                        break;

                    case MessageTypes.MatchEvent:
                        await HandleMatchEventAsync(PvpServerClient.ParsePayload<MatchEventDto>(envelope));
                        break;

                    case MessageTypes.Error:
                        var error = PvpServerClient.ParsePayload<ErrorMessage>(envelope);
                        Debug.LogWarning($"[PvP] Errore server: {error.code} - {error.message}");
                        break;

                    default:
                        Debug.Log($"[PvP] <- {envelope.type}: {envelope.payload}");
                        break;
                }
            }
        }

        private async System.Threading.Tasks.Task HandleMatchEventAsync(MatchEventDto matchEvent)
        {
            switch (matchEvent.type)
            {
                case "RoundStarted":
                    Debug.Log($"[PvP] === ROUND {matchEvent.matchRound} - dado vigore D{matchEvent.vigorDieSides} ===");
                    for (int player = 0; player < 2; player++)
                        for (int slot = 0; slot < 3; slot++)
                            enemyEliminated[player, slot] = false;
                    break;

                case "DeploymentStarted":
                    Debug.Log($"[PvP] Iniziativa round: {matchEvent.rollPlayer0} vs {matchEvent.rollPlayer1} - schiera prima il giocatore {matchEvent.firstPlayer}");
                    break;

                case "DeployTurn":
                    if (matchEvent.player == myIndex)
                        await SendActionAsync(new MatchActionDto { action = MatchActionDto.Deploy, handIndex = 0 });
                    break;

                case "CardDeployed":
                    Debug.Log($"[PvP] G{matchEvent.player} schiera {matchEvent.cardName} (forza {matchEvent.strength}, {matchEvent.lives} vite) in slot {matchEvent.slot}");
                    break;

                case "BattleStarted":
                    Debug.Log($"[PvP] Battaglia! Aure: {(PvpAuraName(matchEvent.auraPlayer0))} vs {(PvpAuraName(matchEvent.auraPlayer1))}");
                    break;

                case "TurnStarted":
                    if (matchEvent.player == myIndex)
                    {
                        int target = FirstAliveEnemySlot();
                        await SendActionAsync(new MatchActionDto
                        {
                            action = MatchActionDto.Attack,
                            targetIsEnemy = true,
                            targetSlot = target
                        });
                    }
                    break;

                case "AttackResolved":
                    Debug.Log($"[PvP] G{matchEvent.player}s{matchEvent.slot} attacca G{matchEvent.targetPlayer}s{matchEvent.targetSlot}: "
                        + $"{matchEvent.attackerTotal} vs {matchEvent.defenderTotal} ({matchEvent.certainty})"
                        + (matchEvent.defenderLostLife ? $" - colpito, vite {matchEvent.defenderRemainingLives}" : " - difeso")
                        + (matchEvent.defenderEliminated ? " ELIMINATO" : string.Empty));
                    if (matchEvent.defenderEliminated)
                        enemyEliminated[matchEvent.targetPlayer, matchEvent.targetSlot] = true;
                    break;

                case "SpiritExpired":
                    enemyEliminated[matchEvent.player, matchEvent.slot] = true;
                    break;

                case "DecisiveSelectionStarted":
                    Debug.Log("[PvP] Round decisivo: scelgo le prime 3 carte del loadout.");
                    await SendActionAsync(new MatchActionDto
                    {
                        action = MatchActionDto.Decisive,
                        decisiveIndices = new[] { 0, 1, 2 }
                    });
                    break;

                case "RoundEnded":
                    Debug.Log($"[PvP] Round {matchEvent.matchRound} al giocatore {matchEvent.winner}. Parziale {matchEvent.winsPlayer0}-{matchEvent.winsPlayer1}");
                    break;

                case "MatchEnded":
                    Debug.Log($"[PvP] MATCH FINITO: vince il giocatore {matchEvent.winner} ({matchEvent.winsPlayer0}-{matchEvent.winsPlayer1})"
                        + (matchEvent.winner == myIndex ? " - HAI VINTO!" : " - hai perso."));
                    break;
            }
        }

        private int FirstAliveEnemySlot()
        {
            int enemy = 1 - myIndex;
            for (int slot = 0; slot < 3; slot++)
            {
                if (!enemyEliminated[enemy, slot])
                    return slot;
            }
            return 0;
        }

        private System.Threading.Tasks.Task SendActionAsync(MatchActionDto action) =>
            client.SendAsync(MessageTypes.MatchAction, action);

        private static string PvpAuraName(int aura) =>
            ((AccardND.GameCore.Pvp.PvpAuraType)aura).ToString();

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
            // 9 carte valore 2 di classi miste: 18 punti, D3 gratis, niente bag.
            var classes = new[]
            {
                HeroClass.Warrior, HeroClass.Rogue, HeroClass.Mage,
                HeroClass.Barbarian, HeroClass.Assassin, HeroClass.Priest,
                HeroClass.Paladin, HeroClass.Hunter, HeroClass.Necromancer
            };
            var cards = new List<LoadoutCardDto>();
            for (int index = 0; index < 9; index++)
                cards.Add(new LoadoutCardDto
                {
                    definitionId = $"2-goblin-{classes[index].ToString().ToLowerInvariant()}",
                    value = 2,
                    heroClass = (int)classes[index]
                });
            return new PvpLoadoutDto
            {
                cards = cards.ToArray(),
                baseDieSides = 3,
                bagDiceSides = new int[0]
            };
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null;
        }
    }
}
