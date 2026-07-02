using System.Security.Cryptography;
using AccardND.GameCore;
using AccardND.GameCore.Pvp;
using AccardND.NetProtocol;
using AccardND.Server.Rooms;
using AccardND.Server.Sessions;

namespace AccardND.Server.Match;

/// <summary>
/// Stato autoritativo di un match best-of-3. Tutta la casualità (iniziativa,
/// mani, e in futuro tiri di vigore) è generata qui, mai sul client.
/// </summary>
public sealed class MatchSession
{
    private sealed class PlayerState
    {
        public ClientConnection Connection;
        public PvpLoadout Loadout;
        public PvpFirstDeal FirstDeal;
    }

    private readonly ServerConfig config;
    private readonly IRandomSource random;
    private readonly PlayerState host = new();
    private readonly PlayerState guest = new();

    public MatchSession(Room room, ServerConfig config)
    {
        ArgumentNullException.ThrowIfNull(room);
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        random = new SeededRandomSource(RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue));
        host.Connection = room.Host;
        host.Loadout = room.HostLoadout;
        guest.Connection = room.Guest;
        guest.Loadout = room.GuestLoadout;
    }

    public int CurrentRound { get; private set; }

    /// <summary>Round 1: iniziativa contesa e mani private a entrambi i giocatori.</summary>
    public async Task StartAsync(CancellationToken cancellation)
    {
        CurrentRound = 1;
        PvpInitiativeResult initiative = PvpInitiative.RollOff(random, config.InitiativeDieSides);
        host.FirstDeal = PvpHandDealer.DealFirstHand(random, host.Loadout.Cards.Count, config.HandSize);
        guest.FirstDeal = PvpHandDealer.DealFirstHand(random, guest.Loadout.Cards.Count, config.HandSize);

        await SendRoundStartAsync(host, guest, initiative.FirstPlayerRoll, initiative.SecondPlayerRoll, cancellation);
        await SendRoundStartAsync(guest, host, initiative.SecondPlayerRoll, initiative.FirstPlayerRoll, cancellation);
    }

    private async Task SendRoundStartAsync(
        PlayerState player,
        PlayerState opponent,
        int playerRoll,
        int opponentRoll,
        CancellationToken cancellation)
    {
        await player.Connection.SendAsync(MessageTypes.MatchStart, new MatchStart
        {
            opponentName = opponent.Connection.Identity.Username,
            yourInitiative = playerRoll,
            opponentInitiative = opponentRoll,
            youDeployFirst = playerRoll > opponentRoll,
            roundNumber = CurrentRound
        }, cancellation);

        await player.Connection.SendAsync(MessageTypes.MatchHand, BuildHand(player), cancellation);
    }

    private MatchHand BuildHand(PlayerState player)
    {
        IReadOnlyList<int> indices = player.FirstDeal.HandIndices;
        var definitionIds = new string[indices.Count];
        for (int index = 0; index < indices.Count; index++)
            definitionIds[index] = player.Loadout.Cards[indices[index]].DefinitionId;

        return new MatchHand
        {
            roundNumber = CurrentRound,
            handIndices = indices.ToArray(),
            handDefinitionIds = definitionIds
        };
    }
}
