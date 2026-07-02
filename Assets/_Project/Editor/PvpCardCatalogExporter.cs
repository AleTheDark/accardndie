using System.Collections.Generic;
using System.IO;
using System.Text;
using AccardND.GameData;
using UnityEditor;
using UnityEngine;

namespace AccardND.Editor
{
    /// <summary>
    /// Esporta il catalogo carte schierabili per la validazione anti-cheat del
    /// server PvP. Da rilanciare ogni volta che si aggiungono o modificano carte.
    /// </summary>
    public static class PvpCardCatalogExporter
    {
        private const string OutputPath = "Server/AccardND.Server/cardcatalog.json";

        [MenuItem("Accard N' Die/Esporta catalogo carte PvP")]
        public static void Export()
        {
            var entries = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:CardDefinition"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
                if (definition == null
                    || definition.Category != CardCategory.Monster
                    || !definition.CanEnterCombat)
                    continue;
                entries.Add(
                    $"    {{ \"id\": \"{definition.Id}\", \"value\": {definition.Strength}, \"heroClass\": {(int)definition.HeroClass} }}");
            }
            entries.Sort(System.StringComparer.Ordinal);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"cards\": [");
            json.AppendLine(string.Join(",\n", entries));
            json.AppendLine("  ]");
            json.AppendLine("}");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputFile = Path.Combine(projectRoot, OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
            File.WriteAllText(outputFile, json.ToString());
            Debug.Log($"Catalogo PvP esportato: {entries.Count} carte in {outputFile}");
        }
    }
}
