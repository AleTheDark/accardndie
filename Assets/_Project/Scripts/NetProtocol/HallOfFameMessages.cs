using System;

namespace AccardND.NetProtocol
{
    /// <summary>Una stagione conclusa presente nella Hall of Fame (per il selettore).</summary>
    [Serializable]
    public sealed class HallOfFameSeasonDto
    {
        public int seasonId;
        public string name;
        public string startedAt;
        public string endedAt;
        public int participants;
    }

    /// <summary>Elenco delle stagioni archiviate (risposta a halloffame.seasons.get).</summary>
    [Serializable]
    public sealed class HallOfFameSeasonsData
    {
        public HallOfFameSeasonDto[] seasons;
    }

    /// <summary>Richiesta della classifica finale di una stagione (0 = più recente).</summary>
    [Serializable]
    public sealed class HallOfFameGetRequest
    {
        public int seasonId;
    }

    [Serializable]
    public sealed class HallOfFameEntry
    {
        public int rank;
        public string playerId;
        public string username;
        public string tier;
        public string division;
        public int finalMmr;
        public int wins;
        public int losses;
    }

    /// <summary>Classifica finale di una stagione (risposta a halloffame.get).</summary>
    [Serializable]
    public sealed class HallOfFameData
    {
        public int seasonId;
        public string seasonName;
        public HallOfFameEntry[] entries;
        public HallOfFameEntry you;   // piazzamento personale se fuori dai primi mostrati (può essere null)
    }
}
