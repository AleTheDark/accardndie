using System.IO;
using AccardND.Battlefield;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    public static class EditableRuntimeTextSaver
    {
        private const string ResourceFolder = "Assets/_Project/Resources";
        private const string AssetPath = ResourceFolder + "/EditableTextOverrides.asset";

        [MenuItem("Accard N' Die/Text Overrides/Save From Open Scenes", priority = 80)]
        public static void SaveFromOpenScenes()
        {
            EditableTextOverrideDatabase database = LoadOrCreateDatabase();
            EditableRuntimeText[] bindings = Object.FindObjectsByType<EditableRuntimeText>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (EditableRuntimeText binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.Key))
                    continue;

                EditableTextOverride entry = database.GetOrCreate(binding.Key);
                entry.Text = binding.GetComponent<UnityEngine.UI.Text>()?.text ?? binding.DefaultText;

                if (binding.transform is RectTransform rect)
                {
                    entry.OverrideLayout = true;
                    entry.AnchorMin = rect.anchorMin;
                    entry.AnchorMax = rect.anchorMax;
                    entry.OffsetMin = rect.offsetMin;
                    entry.OffsetMax = rect.offsetMax;
                    entry.Pivot = rect.pivot;
                    entry.LocalScale = rect.localScale;
                    entry.LocalEulerAngles = rect.localEulerAngles;
                }
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Editable Text] Saved {bindings.Length} text layout binding(s) to {AssetPath}.");
        }

        private static EditableTextOverrideDatabase LoadOrCreateDatabase()
        {
            EditableTextOverrideDatabase database = AssetDatabase.LoadAssetAtPath<EditableTextOverrideDatabase>(AssetPath);
            if (database != null)
                return database;

            if (!AssetDatabase.IsValidFolder(ResourceFolder))
                Directory.CreateDirectory(ResourceFolder);

            database = ScriptableObject.CreateInstance<EditableTextOverrideDatabase>();
            AssetDatabase.CreateAsset(database, AssetPath);
            AssetDatabase.SaveAssets();
            return database;
        }
    }
}
