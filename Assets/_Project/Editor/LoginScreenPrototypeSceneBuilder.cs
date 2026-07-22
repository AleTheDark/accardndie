using AccardND.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AccardND.EditorTools
{
    public static class LoginScreenPrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/LoginScreenPrototype.unity";

        [InitializeOnLoadMethod]
        private static void CreateOnceAfterImport()
        {
            if (!System.IO.File.Exists(ScenePath))
                EditorApplication.delayCall += Rebuild;
        }

        [MenuItem("AccardND/UI/Rebuild Login Prototype Scene")]
        public static void Rebuild()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject root = new("Login Screen Prototype", typeof(LoginScreenPrototype));
            Undo.RegisterCreatedObjectUndo(root, "Create login prototype");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[Login Prototype] Scena creata: {ScenePath}");
        }
    }
}
