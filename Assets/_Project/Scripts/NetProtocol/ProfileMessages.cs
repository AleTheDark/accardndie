using System;

namespace AccardND.NetProtocol
{
    /// <summary>Profilo del giocatore (risposta a profile.get).</summary>
    [Serializable]
    public sealed class ProfileData
    {
        public string playerId;
        public string username;
        public string selectedIconId;
        public string bio;

        // Riepilogo ranked (MMR resta nascosto).
        public bool ranked;
        public bool placement;
        public int placementRemaining;
        public string tier;
        public string division;
        public int leaguePoints;

        // Riepilogo statistiche lifetime.
        public int wins;
        public int losses;
        public int winRatePercent;
        public int currentStreak;
        public int bestStreak;
        public int roundsWon;
        public int roundsLost;
        public int forfeits;

        public int iconsUnlocked;
        public int iconsTotal;
        public int seasonId;
        public string seasonName;
    }

    [Serializable]
    public sealed class SetIconRequest
    {
        public string iconId;
    }

    [Serializable]
    public sealed class IconDto
    {
        public string iconId;
        public string name;
        public string source;     // free | tier | achievement | halloffame | campaign
        public string unlockRef;
        public bool unlocked;
    }

    /// <summary>Catalogo icone con stato di sblocco (risposta a icons.list).</summary>
    [Serializable]
    public sealed class IconsData
    {
        public string selectedIconId;
        public IconDto[] icons;
    }

    /// <summary>Mostri sconfitti in campagna da sincronizzare col profilo.</summary>
    [Serializable]
    public sealed class CampaignKillsRequest
    {
        public string[] monsters;
        public string[] bosses;
    }

    /// <summary>Icone appena sbloccate grazie ai mostri riportati.</summary>
    [Serializable]
    public sealed class CampaignKillsResult
    {
        public string[] newlyUnlocked;
    }
}
