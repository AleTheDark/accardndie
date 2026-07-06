using System;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class AchievementDto
    {
        public string achievementId;
        public string name;
        public string description;
        public int progress;
        public int threshold;
        public bool unlocked;
    }

    /// <summary>Elenco achievement con progressi (risposta a achievements.get).</summary>
    [Serializable]
    public sealed class AchievementsData
    {
        public AchievementDto[] achievements;
    }
}
