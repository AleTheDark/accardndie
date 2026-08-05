using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace AccardND.Localization
{
    /// <summary>
    /// Punto di accesso unico ai testi mostrati al giocatore.
    /// Le chiavi sono indipendenti dalla lingua e i valori vivono nelle String Table di Unity.
    /// </summary>
    public static class GameText
    {
        public const string TableName = "Game";
        private const string LocalePrefsKey = "AccardND.Locale";

        private static readonly HashSet<string> ReportedMissingKeys = new();

        public static event Action LocaleChanged;

        public static string CurrentLocaleCode =>
            LocalizationSettings.SelectedLocale?.Identifier.Code ?? string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;

            string savedLocale = PlayerPrefs.GetString(LocalePrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(savedLocale))
                TrySelectLocale(savedLocale, persist: false);
        }

        public static string Get(string key)
        {
            return Resolve(key, null, Array.Empty<object>());
        }

        public static string Format(string key, params object[] arguments)
        {
            return Resolve(key, null, arguments);
        }

        /// <summary>
        /// Consente una migrazione non distruttiva dei vecchi ScriptableObject: il testo
        /// serializzato rimane un fallback finché la relativa chiave non è nel catalogo.
        /// </summary>
        public static string GetOrFallback(string key, string fallback, params object[] arguments)
        {
            return Resolve(key, fallback, arguments, reportMissing: true);
        }

        public static string GetOrFallbackSilent(string key, string fallback, params object[] arguments)
        {
            return Resolve(key, fallback, arguments, reportMissing: false);
        }

        public static string GetLocalizedFallback(
            string key,
            string italianFallback,
            string englishFallback,
            params object[] arguments)
        {
            bool english = CurrentLocaleCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            string fallback = english ? englishFallback : italianFallback;
            return Resolve(key, fallback, arguments, reportMissing: false);
        }

        public static bool TrySelectLocale(string localeCode, bool persist = true)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
                return false;

            try
            {
                Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(localeCode));
                if (locale == null)
                    return false;

                LocalizationSettings.SelectedLocale = locale;
                if (persist)
                {
                    PlayerPrefs.SetString(LocalePrefsKey, locale.Identifier.Code);
                    PlayerPrefs.Save();
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Localization] Impossibile selezionare la locale '{localeCode}': {exception.Message}");
                return false;
            }
        }

        private static string Resolve(
            string key,
            string fallback,
            object[] arguments,
            bool reportMissing = true)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback ?? string.Empty;

            try
            {
                var operation = LocalizationSettings.StringDatabase.GetTableEntryAsync(
                    TableName,
                    key);
                var result = operation.WaitForCompletion();
                string value = result.Entry?.GetLocalizedString(arguments ?? Array.Empty<object>());

                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch (Exception exception)
            {
                if (reportMissing)
                    ReportMissingOnce(key, exception.Message);
                return FormatFallback(fallback, key, arguments);
            }

            if (reportMissing)
                ReportMissingOnce(key, null);
            return FormatFallback(fallback, key, arguments);
        }

        private static string FormatFallback(string fallback, string key, object[] arguments)
        {
            string value = string.IsNullOrEmpty(fallback) ? key : fallback;
            if (arguments == null || arguments.Length == 0)
                return value;

            try
            {
                return string.Format(value, arguments);
            }
            catch (FormatException)
            {
                return value;
            }
        }

        private static void ReportMissingOnce(string key, string error)
        {
            if (!Debug.isDebugBuild || !ReportedMissingKeys.Add(key))
                return;

            string suffix = string.IsNullOrWhiteSpace(error) ? string.Empty : $" ({error})";
            Debug.LogWarning($"[Localization] Chiave mancante: {key}{suffix}");
        }

        private static void HandleSelectedLocaleChanged(Locale _)
        {
            LocaleChanged?.Invoke();
        }
    }
}
