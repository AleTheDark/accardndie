#if UNITY_EDITOR
using AccardND.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class LevelUpTalentTourDebugSceneBuilder
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string DebugFolder = "Assets/_Project/Scenes/Debug";
    private const string DebugScenePath = DebugFolder + "/LevelUpTalentTourDebug.unity";

    [MenuItem("AccardND/Debug/Create Level-Up Talent Tour Scene")]
    internal static void CreateScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
            AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
        if (!AssetDatabase.IsValidFolder(DebugFolder))
            AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Debug");

        var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameObject existing = GameObject.Find("Level-Up Talent Tour Debug Launcher");
        if (existing == null)
            new GameObject("Level-Up Talent Tour Debug Launcher", typeof(LevelUpTalentTourDebugLauncher));

        EditorSceneManager.SaveScene(scene, DebugScenePath, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LEVEL-UP TOUR DEBUG] Scena creata: {DebugScenePath}");
    }
}
#endif
