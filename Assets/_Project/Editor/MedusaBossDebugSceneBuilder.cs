using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AccardND.EditorTools
{
    public static class MedusaBossDebugSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MedusaBossDebug.unity";

        [InitializeOnLoadMethod]
        private static void CreateOnceAfterImport()
        {
            if (!System.IO.File.Exists(ScenePath))
                EditorApplication.delayCall += Rebuild;
        }

        [MenuItem("AccardND/Boss/Rebuild Medusa Debug Scene")]
        public static void Rebuild()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject root = new("Medusa Boss Debug");
            Undo.RegisterCreatedObjectUndo(root, "Create Medusa boss debug scene");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Medusa Debug] Scena creata: {ScenePath}");
        }
    }
}
