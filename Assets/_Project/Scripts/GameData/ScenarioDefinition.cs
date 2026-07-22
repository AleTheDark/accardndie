using UnityEngine;

namespace AccardND.GameData
{
    [CreateAssetMenu(menuName = "Accard N' Die/Scenario Definition", fileName = "ScenarioDefinition")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite backgroundLandscape;
        [SerializeField] private RoomType roomType;
        [SerializeField] private RoomDifficulty difficulty;
        [SerializeField] private string bossId;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite Background => background;
        public Sprite BackgroundLandscape => backgroundLandscape;
        public RoomType RoomType => roomType;
        public RoomDifficulty Difficulty => difficulty;
        public string BossId => bossId;

#if UNITY_EDITOR
        public void ApplyImportedData(
            string importedId,
            string importedDisplayName,
            Sprite importedBackground,
            Sprite importedLandscapeBackground,
            RoomType importedRoomType,
            RoomDifficulty importedDifficulty,
            string importedBossId,
            bool initializeRules)
        {
            id = importedId;
            background = importedBackground;
            backgroundLandscape = importedLandscapeBackground;
            if (!initializeRules)
                return;

            displayName = importedDisplayName;
            roomType = importedRoomType;
            difficulty = importedDifficulty;
            bossId = importedBossId;
        }
#endif
    }
}
