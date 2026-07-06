using System;

namespace AccardND.NetProtocol
{
    /// <summary>Aggregati di un giocatore per uno scope (lifetime o stagione).</summary>
    [Serializable]
    public sealed class PlayerStatsDto
    {
        public int matches;
        public int wins;
        public int losses;
        public int forfeits;
        public int roundsWon;
        public int roundsLost;
        public int currentStreak;
        public int bestStreak;
        public int winRatePercent;
    }

    /// <summary>Risposta a stats.get: statistiche lifetime + stagione corrente.</summary>
    [Serializable]
    public sealed class StatsData
    {
        public string playerId;
        public string username;
        public int seasonId;
        public string seasonName;
        public PlayerStatsDto lifetime;
        public PlayerStatsDto season;
    }
}
