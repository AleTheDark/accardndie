using UnityEditor;
using UnityEditor.SceneManagement;

namespace AccardND.EditorTools
{
    /// <summary>
    /// Apre il banco di prova del popup di recensione. Stessa forma di
    /// <see cref="BossDebugSceneMenu"/>: le scene di debug si aprono dal menu, non
    /// cercandole a mano nel Project.
    /// </summary>
    internal static class ReviewPromptDebugSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/Debug/ReviewPromptDebug.unity";

        [MenuItem("Accard N' Die/Debug/Popup recensione")]
        private static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
    }
}
