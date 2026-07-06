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

        private static HashSet<string> Load()
        {
            string raw = PlayerPrefs.GetString(Key, string.Empty);
            return string.IsNullOrEmpty(raw)
                ? new HashSet<string>()
                : new HashSet<string>(raw.Split(Separator));
        }

        private static void Save(HashSet<string> defeated)
        {
            PlayerPrefs.SetString(Key, string.Join(Separator, defeated));
            PlayerPrefs.Save();
        }
    }
}
