using System;

namespace AccardND.NetProtocol
{
    /// <summary>
    /// Stato ranked del giocatore (risposta a ranked.get). L'MMR resta nascosto:
    /// il client vede solo tier, divisione e punti lega.
    /// </summary>
    [Serializable]
    public sealed class RankedData
    {
        public bool ranked;              // false = non ancora classificato in questa stagione
        public bool placement;           // true durante il piazzamento (tier ancora nascosto)
        public int placementRemaining;
        public string tier;
        public string division;
        public int leaguePoints;         // 0-100 nella divisione corrente
        public int seasonId;
        public string seasonName;
    }

    /// <summary>Riepilogo di fine partita, personalizzato per il destinatario.</summary>
    [Serializable]
    public sealed class MatchResultData
    {
        public bool youWon;
        public bool ranked;
        public string endedReason;       // normal | timeout | disconnect
        public int scoreYou;
        public int scoreOpponent;

        // Sezione ranked (valorizzata solo se ranked == true).
        public string tier;              // tier dopo la partita
        public string division;
        public int leaguePoints;
        public int lpDelta;              // variazione punti lega (può essere negativa)
        public bool promoted;
        public bool demoted;
        public bool placement;
        public int placementRemaining;

        // Achievement sbloccati con questa partita (nomi), indipendenti dal ranked.
        public string[] unlockedAchievements;
    }

    [Serializable]
    public sealed class LeaderboardEntry
    {
        public int rank;
        public string playerId;
        public string username;
        public string tier;
        public string division;
        public int leaguePoints;
        public bool placement;
    }

    [Serializable]
    public sealed class LeaderboardData
    {
        public int seasonId;
        public string seasonName;
        public LeaderboardEntry[] entries;
    }
}
