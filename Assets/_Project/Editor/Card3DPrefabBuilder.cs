using System.IO;
using AccardND.Presentation;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    [InitializeOnLoad]
    public static class Card3DPrefabBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs";
        private const string PrefabPath = PrefabFolder + "/Card3D.prefab";

        static Card3DPrefabBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("Accard N' Die/Rebuild Card 3D Prefab", priority = 35)]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            Directory.CreateDirectory(PrefabFolder);
            var root = new GameObject("Card3D", typeof(Card3DView));
            root.GetComponent<Card3DView>().RebuildGeometry();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Accard N' Die] Prefab carta 3D creato: {PrefabPath}");
        }

        private static void BuildIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                Rebuild();
        }
    }
}
