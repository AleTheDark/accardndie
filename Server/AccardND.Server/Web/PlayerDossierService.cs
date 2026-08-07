using AccardND.NetProtocol;
using AccardND.Server.Accounts;
using AccardND.Server.Progression;

namespace AccardND.Server.Web;

/// <summary>
/// Tutto quello che il sito sa di un giocatore, raccolto in una volta sola.
///
/// Nessun conto nuovo: sono le stesse risposte che il gioco chiede via WebSocket
/// (profile.get, stats.get, singleplayer.progress, achievements.get). E' voluto -
/// una pagina web che ricalcolasse le statistiche a modo suo comincerebbe prima o
/// poi a dire numeri diversi da quelli in partita, e allora sarebbero due verita'.
/// </summary>
public sealed record PlayerDossier(
    AccountIdentity Identity,
    string DisplayName,
    ProfileData Profile,
    StatsData Stats,
    SinglePlayerProgressData Progress,
    AchievementsData Achievements);

public sealed class PlayerDossierService
{
    private readonly AccountService accounts;
    private readonly ProfileService profiles;
    private readonly StatsService stats;
    private readonly SinglePlayerProgressService singlePlayer;
    private readonly AchievementService achievements;

    public PlayerDossierService(
        AccountService accounts,
        ProfileService profiles,
        StatsService stats,
        SinglePlayerProgressService singlePlayer,
        AchievementService achievements)
    {
        this.accounts = accounts;
        this.profiles = profiles;
        this.stats = stats;
        this.singlePlayer = singlePlayer;
        this.achievements = achievements;
    }

    /// <summary>
    /// Null se l'account non esiste piu' (cancellato mentre la sessione era aperta).
    /// Il chiamante DEVE aver gia' verificato che questo player_id sia fra quelli
    /// della sessione: qui non c'e' nessun controllo di appartenenza.
    /// </summary>
    public PlayerDossier Build(string playerId, string displayName)
    {
        (AccountIdentity identity, _) = accounts.Reload(new AccountIdentity(playerId, null));
        if (identity == null)
            return null;

        return new PlayerDossier(
            identity,
            string.IsNullOrWhiteSpace(displayName) ? identity.Username : displayName,
            profiles.GetProfile(identity),
            stats.GetStats(identity),
            singlePlayer.GetProgress(identity),
            achievements.GetAchievements(identity));
    }
}
