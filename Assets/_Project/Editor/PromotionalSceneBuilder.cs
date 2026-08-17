using System.IO;
using AccardND.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class PromotionalSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Scenes/Promotional/PromotionalTrailer.unity";

    static PromotionalSceneBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    private static void Rebuild()
    {
        Build(true);
    }

    private static void BuildIfMissing()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(ScenePath))
            Build(false);
    }

    private static void Build(bool openWhenDone)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "PromotionalTrailer";

        var root = new GameObject("PROMOTIONAL TRAILER - Press Play");
        SceneManager.MoveGameObjectToScene(root, scene);
        PromotionalSequenceController sequence = root.AddComponent<PromotionalSequenceController>();

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.008f, 0.018f);

        SerializedObject serialized = new SerializedObject(sequence);
        AssignSprites(serialized.FindProperty("backgrounds"), new[]
        {
            "Assets/_Project/Art/Scenarios/bg_loot.png",
            "Assets/_Project/Art/Scenarios/bg_loot.png",
            "Assets/_Project/Art/Scenarios/bg_loot.png"
        });
        AssignSprites(serialized.FindProperty("bosses"), new[]
        {
            "Assets/_Project/Art/Cards/Bosses/boss_medusa_card.png",
            "Assets/_Project/Art/Cards/Bosses/boss_trentor_card.png",
            "Assets/_Project/Art/Cards/Bosses/boss_bragus.png",
            "Assets/_Project/Art/Cards/Bosses/boss_palatir.png"
        });
        AssignSprites(serialized.FindProperty("heroes"), FindHeroArt());
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (openWhenDone)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("Promotional trailer scene created at " + ScenePath);
    }

    private static string[] FindHeroArt()
    {
        string[] candidates = Directory.GetFiles("Assets/_Project/Art/Cards", "*.png", SearchOption.AllDirectories);
        var result = new System.Collections.Generic.List<string>();
        foreach (string candidate in candidates)
        {
            string normalized = candidate.Replace('\\', '/');
            if (normalized.Contains("/Bosses/") || normalized.Contains("/Monsters/"))
                continue;
            result.Add(normalized);
            if (result.Count == 2) break;
        }
        if (result.Count < 2)
        {
            result.Clear();
            result.Add("Assets/_Project/Art/Cards/Monsters/10-champion-warrior.png");
            result.Add("Assets/_Project/Art/Cards/Monsters/10-champion-mage.png");
        }
        return result.ToArray();
    }

    private static void AssignSprites(SerializedProperty property, string[] paths)
    {
        property.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
    }
}
