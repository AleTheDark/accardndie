using AccardND.GameData;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    [InitializeOnLoad]
    public static class GameConfigurationBuilder
    {
        public const string ConfigurationPath = "Assets/_Project/Resources/GameConfiguration.asset";

        static GameConfigurationBuilder()
        {
            EditorApplication.delayCall += () => EnsureConfiguration();
        }

        public static void OpenConfiguration()
        {
            GameConfiguration configuration = EnsureConfiguration();
            Selection.activeObject = configuration;
            EditorGUIUtility.PingObject(configuration);
        }

        private static void ValidateConfiguration()
        {
            GameConfiguration configuration = EnsureConfiguration();
            Debug.Log(
                $"[Accard N' Die] Configurazione valida — schema {configuration.SchemaVersion}, "
                + $"D{configuration.Gameplay.InitiativeDieSides} iniziativa, "
                + $"D{configuration.Gameplay.VigorDieSides} vigore.",
                configuration);
        }

        public static GameConfiguration EnsureConfiguration()
        {
            GameConfiguration configuration = AssetDatabase.LoadAssetAtPath<GameConfiguration>(ConfigurationPath);
            if (configuration == null)
            {
                configuration = ScriptableObject.CreateInstance<GameConfiguration>();
                AssetDatabase.CreateAsset(configuration, ConfigurationPath);
            }

            configuration.UpgradeIfNeeded();
            EditorUtility.SetDirty(configuration);
            AssetDatabase.SaveAssets();
            return configuration;
        }
    }
}
