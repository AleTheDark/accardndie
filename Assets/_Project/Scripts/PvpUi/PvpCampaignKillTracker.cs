using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AccardND.PvpUi
{
    /// <summary>
    /// Tiene traccia in locale (PlayerPrefs) delle famiglie di mostri sconfitte in
    /// campagna, così il client PvP può sincronizzarle col server al login e sbloccare
    /// le icone corrispondenti. È cosmetico: la fiducia sul client è accettabile.
    /// </summary>
    public static class PvpCampaignKillTracker
    {
        private const string Key = "pvp-campaign-kills";
        private const string BossKey = "pvp-campaign-bosses";
        private const char Separator = ',';

        /// <summary>Registra la famiglia di un mostro sconfitto (es. "goblin").</summary>
        public static void RecordDefeat(string monsterFamily)
        {
            if (string.IsNullOrWhiteSpace(monsterFamily))
                return;
            string family = monsterFamily.Trim().ToLowerInvariant();
            HashSet<string> defeated = Load();
            if (defeated.Add(family))
                Save(defeated);
        }

        /// <summary>Estrae la famiglia da un id carta "&lt;valore&gt;-&lt;famiglia&gt;-&lt;classe&gt;" e la registra.</summary>
        public static void RecordDefeatFromCardId(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return;
            string[] parts = cardId.Split('-');
            if (parts.Length >= 2)
                RecordDefeat(parts[1]);
        }

        /// <summary>Tutte le famiglie sconfitte finora.</summary>
        public static string[] All() => Load().ToArray();

        /// <summary>Registra un boss sconfitto in campagna per gli sblocchi profilo.</summary>
        public static void RecordBossDefeat(string bossId)
        {
            if (string.IsNullOrWhiteSpace(bossId))
                return;
            string id = bossId.Trim().ToLowerInvariant();
            HashSet<string> defeated = Load(BossKey);
            if (defeated.Add(id))
                Save(BossKey, defeated);
        }

        /// <summary>Tutti i boss sconfitti finora.</summary>
        public static string[] AllBosses() => Load(BossKey).ToArray();

        private static HashSet<string> Load() => Load(Key);

        private static HashSet<string> Load(string key)
        {
            string raw = PlayerPrefs.GetString(key, string.Empty);
            return string.IsNullOrEmpty(raw)
                ? new HashSet<string>()
                : new HashSet<string>(raw.Split(Separator));
        }

        private static void Save(HashSet<string> defeated) => Save(Key, defeated);

        private static void Save(string key, HashSet<string> defeated)
        {
            PlayerPrefs.SetString(key, string.Join(Separator, defeated));
            PlayerPrefs.Save();
        }
    }
}
