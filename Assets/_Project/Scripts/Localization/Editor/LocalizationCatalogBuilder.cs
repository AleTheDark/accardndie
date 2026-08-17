using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string EnglishLocalePath = LocalesFolder + "/English (en).asset";
        private const string SpanishLocalePath = LocalesFolder + "/Spanish (es).asset";
        private const string GermanLocalePath = LocalesFolder + "/German (de).asset";
        private const string FrenchLocalePath = LocalesFolder + "/French (fr).asset";

        [MenuItem("AccardND/Localization/Rebuild Game Catalog")]
        public static void RebuildItalianCatalog()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(LocalesFolder);
            EnsureFolder(TablesFolder);

            EnsureLocalizationSettings();
            Locale italian = EnsureLocale("it", "Italian (it)", ItalianLocalePath);
            Locale english = EnsureLocale("en", "English (en)", EnglishLocalePath);
            Locale spanish = EnsureLocale("es", "Spanish (es)", SpanishLocalePath);
            Locale german = EnsureLocale("de", "German (de)", GermanLocalePath);
            Locale french = EnsureLocale("fr", "French (fr)", FrenchLocalePath);
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    GameText.TableName,
                    TablesFolder,
                    new List<Locale> { italian, english, spanish, german, french });
            }

            StringTable table = collection.GetTable(italian.Identifier) as StringTable;
            if (table == null)
                table = collection.AddNewTable(italian.Identifier) as StringTable;
            if (table == null)
                throw new InvalidOperationException("Impossibile creare la String Table italiana.");

            ValidateCatalog();
            foreach (ItalianTextEntry source in ItalianGameTextCatalog.Entries)
                ApplyEntry(table, source.Key, source.Text, source.IsSmart);

            EnsureEnglishTable(collection, italian, english);
            EnsureTranslationTable(collection, italian, spanish);
            EnsureTranslationTable(collection, italian, german);
            EnsureTranslationTable(collection, italian, french);
            ApplyTutorialClassCatalog(collection, italian, english, german, spanish, french);

            int dataEntryCount = AddSerializedDataEntries(table);
            int capturedUiEntryCount = AddCapturedRuntimeUiEntries(table);
            int prefabUiEntryCount = AddPrefabUiEntries(table);
            int staticUiEntryCount = AddStaticUiSourceEntries(table);

            ConfigureItalianAsStartupLocale(italian);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Localization] Catalogo italiano aggiornato: " +
                $"{ItalianGameTextCatalog.Entries.Count + dataEntryCount + capturedUiEntryCount + prefabUiEntryCount + staticUiEntryCount} chiavi " +
                $"({dataEntryCount} da carte e scenari, {capturedUiEntryCount} da UI catturata, " +
                $"{prefabUiEntryCount} da prefab UI, {staticUiEntryCount} da UI sorgente).");
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

        private static int AddStaticUiSourceEntries(StringTable table)
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            if (!Directory.Exists(scriptsRoot))
                return 0;

            var entries = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (path.IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\Tests\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string source = File.ReadAllText(path);
                foreach (Match match in Regex.Matches(source, "\"(?<value>(?:\\\\.|[^\"\\\\])*)\""))
                {
                    string text;
                    try
                    {
                        text = Regex.Unescape(match.Groups["value"].Value).TrimEnd();
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    if (LooksLikeStaticUiText(text))
                        entries.Add(text);
                }
            }

            foreach (string text in entries)
                ApplyEntry(table, GameText.AutoKey(text), text, text.Contains("{"));
            return entries.Count;
        }

        private static bool LooksLikeStaticUiText(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 600)
                return false;
            if (text.IndexOf('/') >= 0 || text.IndexOf('\\') >= 0 || text.Contains("://"))
                return false;
            if (!text.Any(char.IsLetter))
                return false;

            // Gli identificatori e le chiavi sono in minuscolo senza spazi; le etichette
            // UI possono invece essere parole singole, titoli o frasi.
            return !Regex.IsMatch(text, "^[a-z][a-z0-9_.-]*$");
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

        public static void EnableTranslationLocales()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(LocalesFolder);
            EnsureFolder(TablesFolder);

            EnsureLocalizationSettings();
            Locale italian = EnsureLocale("it", "Italian (it)", ItalianLocalePath);
            Locale english = EnsureLocale("en", "English (en)", EnglishLocalePath);
            Locale spanish = EnsureLocale("es", "Spanish (es)", SpanishLocalePath);
            Locale german = EnsureLocale("de", "German (de)", GermanLocalePath);
            Locale french = EnsureLocale("fr", "French (fr)", FrenchLocalePath);
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    GameText.TableName, TablesFolder, new List<Locale> { italian, english, spanish, german, french });
            }

            EnsureEnglishTable(collection, italian, english);
            EnsureTranslationTable(collection, italian, spanish);
            EnsureTranslationTable(collection, italian, german);
            EnsureTranslationTable(collection, italian, french);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Localization] Locale inglese, tedesca, spagnola e francese abilitate. Traduci Game_en, Game_de, Game_es e Game_fr per sostituire i testi iniziali.");
        }

        private static Locale EnsureLocale(string code, string assetName, string assetPath)
        {
            var identifier = new LocaleIdentifier(code);
            Locale locale = LocalizationEditorSettings.GetLocale(identifier);
            if (locale == null)
            {
                locale = Locale.CreateLocale(identifier);
                locale.name = assetName;
                AssetDatabase.CreateAsset(locale, assetPath);
                LocalizationEditorSettings.AddLocale(locale);
            }

            ILocalesProvider availableLocales =
                UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales;
            if (!availableLocales.Locales.Any(locale => locale.Identifier == identifier))
                availableLocales.AddLocale(locale);
            return locale;
        }

        private static void EnsureEnglishTable(StringTableCollection collection, Locale italian, Locale english)
        {
            EnsureTranslationTable(collection, italian, english);
        }

        private static void ApplyTutorialClassCatalog(
            StringTableCollection collection,
            Locale italian,
            Locale english,
            Locale german,
            Locale spanish,
            Locale french)
        {
            StringTable italianTable = collection.GetTable(italian.Identifier) as StringTable;
            StringTable englishTable = collection.GetTable(english.Identifier) as StringTable;
            if (italianTable == null || englishTable == null)
                throw new InvalidOperationException("String Table tutorial IT/EN non disponibili.");

            foreach (TutorialClassTextCatalog.Entry source in TutorialClassTextCatalog.Entries)
            {
                ApplyEntry(italianTable, source.Key, source.Italian, source.Italian.Contains("{"));
                ApplyEntry(englishTable, source.Key, source.English, source.English.Contains("{"));
                ApplyMigratedTutorialTranslation(collection, german, source);
                ApplyMigratedTutorialTranslation(collection, spanish, source);
                ApplyMigratedTutorialTranslation(collection, french, source);
            }
        }

        private static void ApplyMigratedTutorialTranslation(
            StringTableCollection collection,
            Locale locale,
            TutorialClassTextCatalog.Entry source)
        {
            StringTable table = collection.GetTable(locale.Identifier) as StringTable;
            if (table == null)
                return;

            // Recupera la traduzione già presente sotto la vecchia chiave auto.*. Se non
            // esiste ancora, l'inglese è un fallback leggibile e non una copia italiana.
            StringTableEntry legacy = table.GetEntry(GameText.AutoKey(source.Italian));
            string translated = !string.IsNullOrWhiteSpace(legacy?.Value)
                ? legacy.Value
                : source.English;
            ApplyEntry(table, source.Key, translated, translated.Contains("{"));
        }

        private static void EnsureTranslationTable(StringTableCollection collection, Locale italian, Locale locale)
        {
            StringTable translatedTable = collection.GetTable(locale.Identifier) as StringTable
                ?? collection.AddNewTable(locale.Identifier) as StringTable;
            if (translatedTable == null)
                throw new InvalidOperationException($"Impossibile creare la String Table '{locale.Identifier.Code}'.");

            StringTable italianTable = collection.GetTable(italian.Identifier) as StringTable;
            if (italianTable != null)
            {
                foreach (StringTableEntry source in italianTable.Values)
                {
                    if (translatedTable.GetEntry(source.Key) == null)
                        ApplyEntry(translatedTable, source.Key, source.Value, source.IsSmart);
                }
            }

            EditorUtility.SetDirty(translatedTable);
            EditorUtility.SetDirty(collection.SharedData);
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
