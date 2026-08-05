using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace AccardND.Localization.Editor
{
    public static class LocalizationCatalogBuilder
    {
        private const string RootFolder = "Assets/_Project/Localization";
        private const string LocalesFolder = RootFolder + "/Locales";
        private const string TablesFolder = RootFolder + "/Tables";
        private const string SettingsPath = RootFolder + "/Localization Settings.asset";
        private const string ItalianLocalePath = LocalesFolder + "/Italian (it).asset";

        [MenuItem("Accard N' Die/Localization/Rebuild Italian Catalog", priority = 80)]
        public static void RebuildItalianCatalog()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(LocalesFolder);
            EnsureFolder(TablesFolder);

            EnsureLocalizationSettings();
            Locale italian = EnsureItalianLocale();
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    GameText.TableName,
                    TablesFolder,
                    new List<Locale> { italian });
            }

            StringTable table = collection.GetTable(italian.Identifier) as StringTable;
            if (table == null)
                table = collection.AddNewTable(italian.Identifier) as StringTable;
            if (table == null)
                throw new InvalidOperationException("Impossibile creare la String Table italiana.");

            ValidateCatalog();
            foreach (ItalianTextEntry source in ItalianGameTextCatalog.Entries)
                ApplyEntry(table, source.Key, source.Text, source.IsSmart);

            int dataEntryCount = AddSerializedDataEntries(table);
            int capturedUiEntryCount = AddCapturedRuntimeUiEntries(table);
            int prefabUiEntryCount = AddPrefabUiEntries(table);

            ConfigureItalianAsStartupLocale(italian);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Localization] Catalogo italiano aggiornato: " +
                $"{ItalianGameTextCatalog.Entries.Count + dataEntryCount + capturedUiEntryCount + prefabUiEntryCount} chiavi " +
                $"({dataEntryCount} da carte e scenari, {capturedUiEntryCount} da UI catturata, " +
                $"{prefabUiEntryCount} da prefab UI).");
        }

        private static UnityEngine.Localization.Settings.LocalizationSettings EnsureLocalizationSettings()
        {
            UnityEngine.Localization.Settings.LocalizationSettings settings =
                LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings != null && !string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(settings)))
                return settings;

            settings = ScriptableObject.CreateInstance<
                UnityEngine.Localization.Settings.LocalizationSettings>();
            settings.name = "Localization Settings";
            AssetDatabase.CreateAsset(settings, SettingsPath);
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            return settings;
        }

        private static int AddPrefabUiEntries(StringTable table)
        {
            const string prefabFolder = "Assets/_Project/Resources/UI/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
                return 0;

            int count = 0;
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
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(fallback))
                        continue;

                    ApplyEntry(table, key, fallback, fallback.Contains("{"));
                    count++;
                }
            }

            return count;
        }

        private static int AddCapturedRuntimeUiEntries(StringTable table)
        {
            const string overrideAssetPath = "Assets/_Project/Resources/EditableTextOverrides.asset";
            ScriptableObject database = AssetDatabase.LoadAssetAtPath<ScriptableObject>(overrideAssetPath);
            if (database == null)
                return 0;

            var serialized = new SerializedObject(database);
            SerializedProperty entries = serialized.FindProperty("entries");
            if (entries == null || !entries.isArray)
                return 0;

            int count = 0;
            for (int index = 0; index < entries.arraySize; index++)
            {
                SerializedProperty item = entries.GetArrayElementAtIndex(index);
                string bindingKey = item.FindPropertyRelative("Key")?.stringValue;
                string text = item.FindPropertyRelative("Text")?.stringValue;
                if (string.IsNullOrWhiteSpace(bindingKey) || string.IsNullOrWhiteSpace(text))
                    continue;

                ApplyEntry(table, GameTextKeys.RuntimeUi(bindingKey), text, text.Contains("{"));
                count++;
            }

            return count;
        }

        private static int AddSerializedDataEntries(StringTable table)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:CardDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                    continue;

                var serialized = new SerializedObject(asset);
                string id = ReadString(serialized, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                string displayName = NormalizeDisplayName(ReadString(serialized, "displayName"), id);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    ApplyEntry(table, GameTextKeys.Data.CardName(id), displayName, false);
                    count++;
                }

                string rules = ReadString(serialized, "rulesText");
                if (!string.IsNullOrWhiteSpace(rules))
                {
                    ApplyEntry(table, GameTextKeys.Data.CardRules(id), rules, rules.Contains("{"));
                    count++;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ScenarioDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                    continue;

                var serialized = new SerializedObject(asset);
                string id = ReadString(serialized, "id");
                string displayName = ReadString(serialized, "displayName");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
                    continue;

                ApplyEntry(table, GameTextKeys.Data.ScenarioName(id), displayName, false);
                count++;
            }

            return count;
        }

        private static string ReadString(SerializedObject serialized, string propertyName)
        {
            return serialized.FindProperty(propertyName)?.stringValue ?? string.Empty;
        }

        private static string NormalizeDisplayName(string rawDisplayName, string fallbackId)
        {
            string source = !string.IsNullOrWhiteSpace(rawDisplayName)
                ? rawDisplayName
                : CreatureNameFromId(fallbackId);
            if (string.IsNullOrWhiteSpace(source))
                return string.Empty;

            string[] words = source.Trim()
                .Replace("_", " ")
                .Replace("-", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < words.Length; index++)
            {
                string word = words[index].ToLowerInvariant();
                words[index] = char.ToUpperInvariant(word[0]) + word.Substring(1);
            }

            return string.Join(" ", words);
        }

        private static string CreatureNameFromId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;
            string[] parts = id.Split('-');
            return parts.Length > 1 ? parts[1] : id;
        }

        private static void ApplyEntry(StringTable table, string key, string text, bool isSmart)
        {
            StringTableEntry entry = table.GetEntry(key) ?? table.AddEntry(key, text);
            entry.Value = text;
            entry.IsSmart = isSmart;
        }

        private static Locale EnsureItalianLocale()
        {
            var identifier = new LocaleIdentifier("it");
            Locale italian = LocalizationEditorSettings.GetLocale(identifier);
            if (italian == null)
            {
                italian = Locale.CreateLocale(identifier);
                italian.name = "Italian (it)";
                AssetDatabase.CreateAsset(italian, ItalianLocalePath);
                LocalizationEditorSettings.AddLocale(italian);
            }

            ILocalesProvider availableLocales =
                UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales;
            if (!availableLocales.Locales.Any(locale => locale.Identifier == identifier))
                availableLocales.AddLocale(italian);
            return italian;
        }

        private static void ConfigureItalianAsStartupLocale(Locale italian)
        {
            UnityEngine.Localization.Settings.LocalizationSettings settings =
                LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
                return;

            List<IStartupLocaleSelector> selectors =
                UnityEngine.Localization.Settings.LocalizationSettings.StartupLocaleSelectors;
            SpecificLocaleSelector specific = selectors.OfType<SpecificLocaleSelector>().FirstOrDefault();
            if (specific == null)
                selectors.Add(new SpecificLocaleSelector { LocaleId = italian.Identifier });
            else
                specific.LocaleId = italian.Identifier;

            UnityEngine.Localization.Settings.LocalizationSettings.ProjectLocale = italian;
            ConfigureSerializedSettings(settings, italian);
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureSerializedSettings(
            UnityEngine.Localization.Settings.LocalizationSettings settings,
            Locale italian)
        {
            var serialized = new SerializedObject(settings);
            serialized.Update();

            SerializedProperty projectLocale = serialized.FindProperty("m_ProjectLocaleIdentifier");
            SerializedProperty projectLocaleCode = projectLocale?.FindPropertyRelative("m_Code");
            if (projectLocaleCode != null)
                projectLocaleCode.stringValue = italian.Identifier.Code;

            SerializedProperty startupSelectors = serialized.FindProperty("m_StartupSelectors");
            if (startupSelectors != null && startupSelectors.isArray)
            {
                for (int index = 0; index < startupSelectors.arraySize; index++)
                {
                    SerializedProperty selector = startupSelectors.GetArrayElementAtIndex(index);
                    if (!(selector.managedReferenceFullTypename ?? string.Empty).Contains("SpecificLocaleSelector"))
                        continue;

                    SerializedProperty localeId = selector.FindPropertyRelative("m_LocaleId");
                    SerializedProperty localeCode = localeId?.FindPropertyRelative("m_Code");
                    if (localeCode != null)
                        localeCode.stringValue = italian.Identifier.Code;
                }
            }

            SerializedProperty availableLocales = serialized.FindProperty("m_AvailableLocales");
            SerializedProperty locales = availableLocales?.FindPropertyRelative("m_Locales");
            if (locales != null && locales.isArray)
            {
                bool alreadyPresent = false;
                for (int index = 0; index < locales.arraySize; index++)
                {
                    if (locales.GetArrayElementAtIndex(index).objectReferenceValue == italian)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    int index = locales.arraySize;
                    locales.InsertArrayElementAtIndex(index);
                    locales.GetArrayElementAtIndex(index).objectReferenceValue = italian;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateCatalog()
        {
            string duplicate = ItalianGameTextCatalog.Entries
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrEmpty(duplicate))
                throw new InvalidOperationException($"Chiave di localizzazione duplicata: {duplicate}");

            bool hasInvalidEntry = ItalianGameTextCatalog.Entries.Any(
                entry => string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Text));
            if (hasInvalidEntry)
                throw new InvalidOperationException("Il catalogo contiene una chiave o un testo vuoto.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
