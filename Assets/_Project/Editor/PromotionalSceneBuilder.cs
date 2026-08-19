using System.IO;
using AccardND.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Costruisce la scena di cattura del trailer descritta in
/// Docs/trailer-lancio.md §5. La scena e' volutamente vuota: un root con
/// <see cref="PromotionalSequenceController"/> e una camera.
///
/// Non ci sono riferimenti serializzati da assegnare: pedine, carte, fondali e
/// suoni li carica la sequenza a runtime da Resources (CardDatabase,
/// GameConfiguration, Backgrounds, SFX), gli stessi che usa la partita. Cosi'
/// il trailer non puo' andare fuori sincrono col gioco: se cambia una carta o
/// un VFX, cambia anche qui.
/// </summary>
[InitializeOnLoad]
internal static class PromotionalSceneBuilder
{
    private const string ScenePath = "Assets/_Project/Scenes/Promotional/PromotionalTrailer.unity";

    static PromotionalSceneBuilder()
    {
        EditorApplication.delayCall += BuildIfMissing;
    }

    [MenuItem("AccardND/Trailer/Apri scena trailer")]
    public static void Open()
    {
        if (!File.Exists(ScenePath))
            Build(false);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    [MenuItem("AccardND/Trailer/Ricostruisci scena trailer")]
    public static void Rebuild()
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
        root.AddComponent<PromotionalSequenceController>();

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.005f, 0.008f, 0.018f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (openWhenDone)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log($"[Trailer] Scena di cattura pronta: {ScenePath}");
    }
}
