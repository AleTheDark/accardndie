using System;
using AccardND.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class ProvaLampoDebugMenu
{
    [MenuItem("AccardND/Debug/Quick Challenge/Entrata reale casuale")]
    private static void OpenQuickChallengeRealDebugScene()
    {
        const string scenePath = "Assets/Scenes/Debug/QuickChallengeRealDebug.unity";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("AccardND/Debug/Quick Challenge/Stanza completa")]
    private static void OpenQuickChallengeRoomDebugScene()
    {
        const string scenePath = "Assets/Scenes/Debug/QuickChallengeRoomDebug.unity";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("AccardND/Debug/Prova Lampo/Sequenza delle classi")]
    private static void OpenMemoryDebugScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("Prova Lampo Memory Debug");
        root.AddComponent<ProvaLampoMemoryDebugScene>();
        Selection.activeGameObject = root;
        EditorApplication.isPlaying = true;
    }

    [MenuItem("AccardND/Debug/Prova Lampo/Quiz rapido")]
    private static void OpenQuizDebugScene()
    {
        OpenDebugScene("AccardND.Presentation.ProvaLampoQuizDebugScene", "Prova Lampo Quiz Debug");
    }

    [MenuItem("AccardND/Debug/Prova Lampo/Puzzle scorrevole 3x3")]
    private static void OpenPuzzleDebugScene()
    {
        OpenDebugScene("AccardND.Presentation.ProvaLampoPuzzleDebugScene", "Prova Lampo Puzzle Debug");
    }

    private static void OpenDebugScene(string componentTypeName, string rootName)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject(rootName);
        Type componentType = Type.GetType(componentTypeName + ", AccardND.Presentation");
        if (componentType == null)
        {
            Debug.LogError("Componente debug non trovato: " + componentTypeName);
            return;
        }
        root.AddComponent(componentType);
        Selection.activeGameObject = root;
        EditorApplication.isPlaying = true;
    }
}
