using AccardND.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace AccardND.EditorTools
{
    internal static class BossDebugSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/Debug/BossDebug.unity";
        private static void OpenBragus() => Open(BossDebugScenario.Bragus);

        [MenuItem("Accard N' Die/Debug/Boss/Jurinashor")]
        private static void OpenJurinashor() => Open(BossDebugScenario.Jurinashor);

        private static void OpenMedusa() => Open(BossDebugScenario.Medusa);

        private static void OpenPalatir() => Open(BossDebugScenario.Palatir);

        private static void OpenSeraphel() => Open(BossDebugScenario.Seraphel);

        private static void OpenTrentor() => Open(BossDebugScenario.Trentor);

        private static void Open(BossDebugScenario scenario)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BossDebugSelection.Current = scenario;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
    }
}
