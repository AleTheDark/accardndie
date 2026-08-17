using System.Linq;
using AccardND.GameCore;
using AccardND.GameData;
using AccardND.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SupremeDebugSceneBuilder
{
    [MenuItem("Accard N' Die/Debug/Build Supreme Debug Scene")]
    public static void Build()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
        var root=new GameObject("SupremeDebugScene",typeof(AudioSource),typeof(SupremeDebugScene));
        var audioSource=root.GetComponent<AudioSource>();
        audioSource.playOnAwake=false;
        audioSource.spatialBlend=0f;
        var cards=AssetDatabase.FindAssets("t:CardDefinition",new[]{"Assets/_Project/Data/Cards/Monster"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c=>c!=null&&c.HasHeroClass).GroupBy(c=>c.HeroClass).Select(g=>g.OrderByDescending(c=>c.Strength).First()).ToArray();
        var so=new SerializedObject(root.GetComponent<SupremeDebugScene>());var p=so.FindProperty("classCards");p.arraySize=cards.Length;
        for(int i=0;i<cards.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=cards[i];so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Debug/SupremeDebugScene.unity");
        Selection.activeGameObject=root;Debug.Log($"SupremeDebugScene creata con {cards.Length} classi.");
    }
}
