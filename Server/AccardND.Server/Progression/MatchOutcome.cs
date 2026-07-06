using AccardND.Server.Accounts;

namespace AccardND.Server.Progression;

/// <summary>
/// Esito di una partita da registrare. Winner: 0 = PlayerA, 1 = PlayerB, -1 = nessuno.
/// EndedReason: 'normal' | 'forfeit' | 'timeout' | 'disconnect'.
/// </summary>
public sealed record MatchOutcome(
    AccountIdentity PlayerA,
    AccountIdentity PlayerB,
    int Winner,
    int ScoreA,
    int ScoreB,
    bool Ranked,
    string EndedReason,
    string RoomCode,
    DateTime StartedAt,
    DateTime EndedAt);
