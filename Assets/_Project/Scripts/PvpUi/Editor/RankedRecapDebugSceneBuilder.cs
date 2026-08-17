#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AccardND.PvpUi.Editor
{
    internal static class RankedRecapDebugSceneBuilder
    {
        [MenuItem("AccardND/Debug/Create Ranked Recap Scene")]
        private static void CreateScene()
        {
            const string folder = "Assets/_Project/Scenes/Debug";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes")) AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Debug");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Main Camera", typeof(Camera)).tag = "MainCamera";
            new GameObject("Directional Light", typeof(Light));
            new GameObject("Ranked Recap Debug", typeof(RankedRecapDebugScene));
            EditorSceneManager.SaveScene(scene, folder + "/RankedRecapDebug.unity");
            Debug.Log("[Ranked Recap] Debug scene created.");
        }
    }
}
#endif
