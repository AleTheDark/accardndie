using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace AccardND.Localization.Editor
{
    public static class LocalizationCatalogValidator
    {
        [MenuItem("Accard N' Die/Localization/Validate Catalog", priority = 81)]
        public static void ValidateCatalog()
        {
            Locale italian = LocalizationEditorSettings.GetLocale(new LocaleIdentifier("it"));
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
            StringTable table = italian == null ? null : collection?.GetTable(italian.Identifier) as StringTable;
            if (table == null)
                throw new InvalidOperationException("La String Table italiana 'Game' non esiste.");

            var expectedKeys = new HashSet<string>(StringComparer.Ordinal);
            CollectConstantKeys(typeof(GameTextKeys), expectedKeys);
            foreach (ItalianTextEntry entry in ItalianGameTextCatalog.Entries)
                expectedKeys.Add(entry.Key);
            CollectDataKeys("t:CardDefinition", "id", expectedKeys, includeRules: true);
            CollectDataKeys("t:ScenarioDefinition", "id", expectedKeys, includeRules: false);
            CollectCapturedRuntimeUiKeys(expectedKeys);
            CollectPrefabUiKeys(expectedKeys);

            List<string> missing = expectedKeys
                .Where(key => table.GetEntry(key) == null)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "Chiavi mancanti nel catalogo italiano:\n" + string.Join("\n", missing));

            List<string> empty = expectedKeys
                .Where(key => string.IsNullOrWhiteSpace(table.GetEntry(key)?.Value))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            if (empty.Count > 0)
                throw new InvalidOperationException(
                    "Valori italiani vuoti:\n" + string.Join("\n", empty));

            LocalizationSettings.SelectedLocale = italian;
            string resolvedTitle = GameText.Get(GameTextKeys.Login.Title);
            string resolvedWelcome = GameText.Format(GameTextKeys.Login.Welcome, "Ada");
            string resolvedConsumable = GameText.Format(
                GameTextKeys.Consumables.SecondChanceUsed,
                3);
            string resolvedCombat = GameText.Format(
                GameTextKeys.Combat.ImpossibleAttackDetailed,
                "A",
                10,
                2,
                string.Empty,
                "D6",
                "B",
                5,
                3,
                string.Empty,
                4);
            if (resolvedTitle != "ACCEDI" ||
                resolvedWelcome != "Benvenuto, Ada." ||
                resolvedConsumable != "Seconda Chance: 3 carte tornano dal cimitero al mazzo." ||
                resolvedCombat != "0%: A arriva al massimo a 10 (2 + D6), mentre B parte da 5 (3 + D4:1).")
                throw new InvalidOperationException(
                    "Risoluzione runtime non valida: " +
                    $"'{resolvedTitle}' / '{resolvedWelcome}' / " +
                    $"'{resolvedConsumable}' / '{resolvedCombat}'.");

            Debug.Log(
                $"[Localization] Catalogo valido: {expectedKeys.Count} chiavi controllate e " +
                "risoluzione runtime verificata.");
        }

        private static void CollectConstantKeys(Type type, ISet<string> keys)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
                {
                    string key = field.GetRawConstantValue() as string;
                    if (!string.IsNullOrWhiteSpace(key))
                        keys.Add(key);
                }
            }

            foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
                CollectConstantKeys(nested, keys);
        }

        private static void CollectDataKeys(
            string filter,
            string idProperty,
            ISet<string> keys,
            bool includeRules)
        {
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                    continue;

                var serialized = new SerializedObject(asset);
                string id = serialized.FindProperty(idProperty)?.stringValue;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (filter.Contains("CardDefinition", StringComparison.Ordinal))
                {
                    keys.Add(GameTextKeys.Data.CardName(id));
                    if (includeRules &&
                        !string.IsNullOrWhiteSpace(serialized.FindProperty("rulesText")?.stringValue))
                        keys.Add(GameTextKeys.Data.CardRules(id));
                }
                else
                {
                    keys.Add(GameTextKeys.Data.ScenarioName(id));
                }
            }
        }

        private static void CollectCapturedRuntimeUiKeys(ISet<string> keys)
        {
            const string overrideAssetPath = "Assets/_Project/Resources/EditableTextOverrides.asset";
            ScriptableObject database = AssetDatabase.LoadAssetAtPath<ScriptableObject>(overrideAssetPath);
            if (database == null)
                return;

            var serialized = new SerializedObject(database);
            SerializedProperty entries = serialized.FindProperty("entries");
            if (entries == null || !entries.isArray)
                return;

            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty item = entries.GetArrayElementAtIndex(index);
                string bindingKey = item.FindPropertyRelative("Key")?.stringValue;
                string text = item.FindPropertyRelative("Text")?.stringValue;
                if (!string.IsNullOrWhiteSpace(bindingKey) && !string.IsNullOrWhiteSpace(text))
                    keys.Add(GameTextKeys.RuntimeUi(bindingKey));
            }
        }

        private static void CollectPrefabUiKeys(ISet<string> keys)
        {
            const string prefabFolder = "Assets/_Project/Resources/UI/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
                return;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder }))
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (root == null)
                    continue;

                foreach (LocalizedTextBinding binding in root.GetComponentsInChildren<LocalizedTextBinding>(true))
                {
                    var serialized = new SerializedObject(binding);
                    string key = serialized.FindProperty("key")?.stringValue;
                    string fallback = serialized.FindProperty("sourceFallback")?.stringValue;
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(fallback))
                        keys.Add(key);
                }
            }
        }
    }
}
