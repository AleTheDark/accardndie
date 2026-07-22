using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AccardND.EditorTools
{
    public static class PalatirBossDebugSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/PalatirBossDebug.unity";
        private const string TemplateScenePath = "Assets/Scenes/MainScene.unity";

        [InitializeOnLoadMethod]
        private static void CreateOnceAfterImport()
        {
            if (!System.IO.File.Exists(ScenePath))
                EditorApplication.delayCall += Rebuild;
        }

        [MenuItem("AccardND/Boss/Rebuild Palatir Debug Scene")]
        public static void Rebuild()
        {
            if (System.IO.File.Exists(TemplateScenePath))
            {
                System.IO.File.Copy(TemplateScenePath, ScenePath, overwrite: true);
                AssetDatabase.ImportAsset(ScenePath);
                Debug.Log($"[Palatir Debug] Scena ricreata da {TemplateScenePath}: {ScenePath}");
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject root = new("Palatir Boss Debug");
            Undo.RegisterCreatedObjectUndo(root, "Create Palatir boss debug scene");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Palatir Debug] Scena creata: {ScenePath}");
        }
    }
}
