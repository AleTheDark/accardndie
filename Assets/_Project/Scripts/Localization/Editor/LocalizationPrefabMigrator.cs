using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.Localization.Editor
{
    public static class LocalizationPrefabMigrator
    {
        private const string PrefabFolder = "Assets/_Project/Resources/UI/Prefabs";

        public static void MigrateUiPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                Debug.Log("[Localization] Nessuna cartella di prefab UI da migrare.");
                return;
            }

            int prefabCount = 0;
            int bindingCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    foreach (Text text in root.GetComponentsInChildren<Text>(true))
                    {
                        if (text == null || string.IsNullOrWhiteSpace(text.text))
                            continue;

                        LocalizedTextBinding binding = text.GetComponent<LocalizedTextBinding>();
                        if (binding == null)
                        {
                            binding = text.gameObject.AddComponent<LocalizedTextBinding>();
                            changed = true;
                        }

                        string key = GameTextKeys.RuntimeUi(
                            $"prefab/{root.name}/{BuildRelativePath(root.transform, text.transform)}");
                        if (binding.Key != key)
                            changed = true;
                        binding.Configure(key, text.text);
                        EditorUtility.SetDirty(binding);
                        bindingCount++;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabCount++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[Localization] Migrazione prefab completata: {bindingCount} testi collegati " +
                $"in {prefabCount} prefab modificati.");
        }

        private static string BuildRelativePath(Transform root, Transform target)
        {
            var segments = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                segments.Add($"{current.name}_{current.GetSiblingIndex()}");
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
