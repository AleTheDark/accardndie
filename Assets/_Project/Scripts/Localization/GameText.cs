using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        /// Precarica locale e tabella senza bloccare il main thread. In WebGL
        /// WaitForCompletion non puo' far avanzare i download Addressables, quindi
        /// la UI deve attendere questa inizializzazione prima di chiedere i testi.
        /// </summary>
        public static async Task InitializeAsync()
        {
            var initialization = LocalizationSettings.InitializationOperation;
            while (!initialization.IsDone)
                await Task.Yield();

            var table = LocalizationSettings.StringDatabase.GetTableAsync(TableName);
            while (!table.IsDone)
                await Task.Yield();
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

        /// <summary>Una lingua installata nel progetto, già pronta da mostrare in un menu.</summary>
        public readonly struct LanguageOption
        {
            public LanguageOption(string code, string displayName)
            {
                Code = code;
                DisplayName = displayName;
            }

            public string Code { get; }

            public string DisplayName { get; }
        }

        /// <summary>
        /// Le lingue effettivamente disponibili, in ordine alfabetico. La UI non deve
        /// conoscere Unity.Localization: aggiungere una Locale al progetto basta perché
        /// compaia nel selettore, senza toccare il pannello delle opzioni.
        /// </summary>
        public static IReadOnlyList<LanguageOption> AvailableLanguages()
        {
            var options = new List<LanguageOption>();
            ILocalesProvider provider = LocalizationSettings.AvailableLocales;
            if (provider?.Locales == null)
                return options;

            foreach (Locale locale in provider.Locales)
            {
                if (locale == null)
                    continue;

                string code = locale.Identifier.Code;
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                options.Add(new LanguageOption(code, DescribeLocale(locale)));
            }

            options.Sort((left, right) => string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.CurrentCultureIgnoreCase));
            return options;
        }

        /// <summary>Nome della lingua attiva nella lingua stessa ("Italiano", "English").</summary>
        public static string CurrentLanguageName => DescribeLocale(LocalizationSettings.SelectedLocale);

        private static string DescribeLocale(Locale locale)
        {
            if (locale == null)
                return string.Empty;

            string name = null;
            try
            {
                // Una locale personalizzata può non avere una CultureInfo corrispondente.
                name = locale.Identifier.CultureInfo?.NativeName;
            }
            catch (Exception)
            {
                name = null;
            }

            if (string.IsNullOrWhiteSpace(name))
                name = locale.LocaleName;
            if (string.IsNullOrWhiteSpace(name))
                name = locale.Identifier.Code;

            return string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : char.ToUpperInvariant(name[0]) + name.Substring(1);
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
