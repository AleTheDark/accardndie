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
        private static readonly List<KeyValuePair<string, string>> AutoFragments = new();
        private static string autoFragmentsLocaleCode;
        private static bool initializationRefreshQueued;

        public static event Action LocaleChanged;

        public static string CurrentLocaleCode =>
            LocalizationSettings.SelectedLocale?.Identifier.Code ?? string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;

            string savedLocale = PlayerPrefs.GetString(LocalePrefsKey, string.Empty);

            // Alcune schermate runtime nascono prima che Addressables abbia caricato la
            // String Table. Al termine del caricamento notifichiamo tutti i binding: così
            // non restano visibili fallback italiani o, peggio, chiavi come "common.mute".
            if (!initializationRefreshQueued)
            {
                initializationRefreshQueued = true;
                _ = RefreshAfterInitializationAsync(savedLocale);
            }
        }

        public static string Get(string key)
        {
            return Resolve(key, null, Array.Empty<object>());
        }

        /// <summary>
        /// Chiave stabile per i testi UI statici creati da codice. Le chiavi semantiche
        /// rimangono preferibili; questa copertura evita che un'etichetta dimenticata
        /// resti vincolata alla lingua del sorgente.
        /// </summary>
        public static string AutoKey(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                return string.Empty;

            sourceText = sourceText.TrimEnd();

            unchecked
            {
                uint hash = 2166136261;
                for (int index = 0; index < sourceText.Length; index++)
                {
                    hash ^= sourceText[index];
                    hash *= 16777619;
                }

                return $"auto.{hash:x8}";
            }
        }

        public static string GetAuto(string sourceText)
        {
            if (string.IsNullOrWhiteSpace(sourceText))
                return sourceText ?? string.Empty;

            string resolved = Resolve(AutoKey(sourceText), null, Array.Empty<object>(), reportMissing: false);
            if (!string.Equals(resolved, AutoKey(sourceText), StringComparison.Ordinal))
                return resolved;

            return ReplaceLocalizedFragments(sourceText);
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

        private static async Task RefreshAfterInitializationAsync(string savedLocale)
        {
            var initialization = LocalizationSettings.InitializationOperation;
            while (!initialization.IsDone)
                await Task.Yield();

            if (!string.IsNullOrWhiteSpace(savedLocale))
                TrySelectLocale(savedLocale, persist: false);

            // Carica la tabella soltanto dopo il ripristino della locale, così il
            // preload riguarda la lingua effettivamente selezionata.
            var table = LocalizationSettings.StringDatabase.GetTableAsync(TableName);
            while (!table.IsDone)
                await Task.Yield();

            autoFragmentsLocaleCode = null;
            AutoFragments.Clear();
            LocaleChanged?.Invoke();
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

        /// <summary>
        /// Localizza un testo legacy che non possiede ancora una chiave semantica propria.
        /// Mantiene in un solo punto la compatibilita' con le voci auto.* del catalogo.
        /// </summary>
        public static string GetAutoLocalizedFallback(string fallback, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(fallback))
                return string.Empty;

            return Resolve(AutoKey(fallback), fallback, arguments, reportMissing: false);
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

        /// <summary>
        /// Fallback localizzato completo per i testi migrati prima della sincronizzazione
        /// degli asset String Table. La chiave resta la fonte primaria quando è presente.
        /// </summary>
        public static string GetLocalizedFallback(
            string key,
            string italianFallback,
            string englishFallback,
            string germanFallback,
            string spanishFallback,
            string frenchFallback,
            params object[] arguments)
        {
            string fallback = CurrentLocaleCode.ToLowerInvariant() switch
            {
                "en" => englishFallback,
                "de" => germanFallback,
                "es" => spanishFallback,
                "fr" => frenchFallback,
                _ => italianFallback
            };
            return Resolve(key, fallback, arguments, reportMissing: false);
        }

        /// <summary>Risolve una chiave e i parametri ricevuti dal protocollo server.</summary>
        public static string GetRemote(string key, string fallback, string[] arguments = null)
        {
            if (TryGetRemoteFallbacks(key, out string italian, out string english, out string german, out string spanish, out string french))
                return GetLocalizedFallback(key, italian, english, german, spanish, french, ToObjects(arguments));

            if (arguments == null || arguments.Length == 0)
                return Resolve(key, fallback, Array.Empty<object>(), reportMissing: false);

            return Resolve(key, fallback, ToObjects(arguments), reportMissing: false);
        }

        private static object[] ToObjects(string[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return Array.Empty<object>();

            var values = new object[arguments.Length];
            for (int index = 0; index < arguments.Length; index++)
                values[index] = arguments[index] ?? string.Empty;
            return values;
        }

        // I testi inviati dal server non devono mai dipendere dalla lingua del server.
        // La tabella Unity resta la fonte primaria; questi fallback mantengono tradotte le
        // nuove chiavi anche prima della successiva sincronizzazione degli asset.
        private static bool TryGetRemoteFallbacks(
            string key,
            out string italian,
            out string english,
            out string german,
            out string spanish,
            out string french)
        {
            (italian, english, german, spanish, french) = key switch
            {
                GameTextKeys.Server.FriendInvalid => ("Amico non valido.", "Invalid friend.", "Ungültiger Freund.", "Amigo no válido.", "Ami invalide."),
                GameTextKeys.Server.FriendNotAdded => ("Non è tra i tuoi amici.", "This player is not on your friends list.", "Dieser Spieler ist nicht in deiner Freundesliste.", "Este jugador no está en tu lista de amigos.", "Ce joueur ne fait pas partie de vos amis."),
                GameTextKeys.Server.FriendOffline => ("L'amico non è online.", "Your friend is not online.", "Dein Freund ist nicht online.", "Tu amigo no está conectado.", "Votre ami n'est pas en ligne."),
                GameTextKeys.Server.FriendBusy => ("L'amico è già occupato.", "Your friend is already busy.", "Dein Freund ist bereits beschäftigt.", "Tu amigo ya está ocupado.", "Votre ami est déjà occupé."),
                "server.error.not_authenticated" => ("Sessione non autenticata.", "You are not signed in.", "Du bist nicht angemeldet.", "No has iniciado sesión.", "Vous n'êtes pas connecté."),
                "server.error.invalid_message" => ("Richiesta non valida.", "Invalid request.", "Ungültige Anfrage.", "Solicitud no válida.", "Demande non valide."),
                "server.error.server_error" => ("Il server non ha potuto completare la richiesta.", "The server could not complete the request.", "Der Server konnte die Anfrage nicht abschließen.", "El servidor no pudo completar la solicitud.", "Le serveur n'a pas pu traiter la demande."),
                "server.error.invalid_credentials" => ("Credenziali non valide.", "Invalid credentials.", "Ungültige Anmeldedaten.", "Credenciales no válidas.", "Identifiants invalides."),
                "server.error.client_outdated" => ("Versione del gioco non aggiornata.", "Your game version is out of date.", "Deine Spielversion ist nicht aktuell.", "Tu versión del juego está desactualizada.", "Votre version du jeu n'est pas à jour."),
                "server.error.username_taken" => ("Nickname non disponibile.", "Nickname is not available.", "Dieser Nickname ist nicht verfügbar.", "El apodo no está disponible.", "Ce pseudo n'est pas disponible."),
                "server.error.room_not_found" => ("Stanza non trovata.", "Room not found.", "Raum nicht gefunden.", "Sala no encontrada.", "Salon introuvable."),
                "server.error.room_full" => ("La stanza è piena.", "The room is full.", "Der Raum ist voll.", "La sala está llena.", "Le salon est plein."),
                "server.error.already_in_room" => ("Sei già in una stanza.", "You are already in a room.", "Du bist bereits in einem Raum.", "Ya estás en una sala.", "Vous êtes déjà dans un salon."),
                "server.error.invalid_loadout" => ("Mazzo non valido.", "Invalid loadout.", "Ungültiges Deck.", "Configuración no válida.", "Configuration invalide."),
                "server.error.invalid_action" => ("Azione non valida.", "Invalid action.", "Ungültige Aktion.", "Acción no válida.", "Action non valide."),
                "server.error.not_in_match" => ("Non sei in una partita.", "You are not in a match.", "Du bist in keinem Spiel.", "No estás en una partida.", "Vous n'êtes pas dans une partie."),
                "server.error.match_paused" => ("La partita è in pausa.", "The match is paused.", "Das Spiel ist pausiert.", "La partida está en pausa.", "La partie est en pause."),
                "server.error.invalid_progression_request" => ("Richiesta di progressione non valida.", "Invalid progression request.", "Ungültige Fortschrittsanfrage.", "Solicitud de progreso no válida.", "Demande de progression non valide."),
                "server.error.insufficient_honey" => ("Vasetti di miele insufficienti.", "Not enough honey jars.", "Nicht genug Honiggläser.", "No tienes suficientes tarros de miel.", "Vous n'avez pas assez de pots de miel."),
                "server.error.reward_claim_not_found" => ("Ricompensa non trovata.", "Reward not found.", "Belohnung nicht gefunden.", "Recompensa no encontrada.", "Récompense introuvable."),
                "server.error.ad_already_used" => ("Pubblicità già utilizzata.", "Advertisement already used.", "Werbung bereits verwendet.", "Anuncio ya utilizado.", "Publicité déjà utilisée."),
                "server.error.requirements_not_met" => ("Requisiti non soddisfatti.", "Requirements not met.", "Anforderungen nicht erfüllt.", "No cumples los requisitos.", "Conditions requises non remplies."),
                _ => default
            };
            return italian != null;
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

                if (!string.IsNullOrEmpty(value) && !IsUntranslatedItalianCopy(key, value, fallback))
                    return value;
            }
            catch (Exception exception)
            {
                if (reportMissing)
                    ReportMissingOnce(key, exception.Message);
                return FormatFallback(fallback, key, arguments);
            }

            // Una chiave semantica può arrivare nel codice prima della successiva
            // sincronizzazione delle tabelle Unity. Se lo stesso testo era già nel
            // catalogo automatico, usalo anche in questo intervallo.
            if (!string.IsNullOrWhiteSpace(fallback) && !key.StartsWith("auto.", StringComparison.Ordinal))
            {
                try
                {
                    string autoKey = AutoKey(fallback);
                    var autoOperation = LocalizationSettings.StringDatabase.GetTableEntryAsync(TableName, autoKey);
                    var autoResult = autoOperation.WaitForCompletion();
                    string autoValue = autoResult.Entry?.GetLocalizedString(arguments ?? Array.Empty<object>());
                    if (!string.IsNullOrEmpty(autoValue))
                        return autoValue;
                }
                catch (Exception)
                {
                    // Il fallback esplicito qui sotto rimane sempre sicuro.
                }
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

        private static bool IsUntranslatedItalianCopy(string key, string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(fallback) ||
                string.Equals(CurrentLocaleCode, "it", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                Locale italian = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier("it"));
                if (italian == null)
                    return false;

                var italianOperation = LocalizationSettings.StringDatabase.GetTableEntryAsync(TableName, key, italian);
                var italianResult = italianOperation.WaitForCompletion();
                string italianValue = italianResult.Entry?.Value;
                return !string.IsNullOrEmpty(italianValue) && string.Equals(value, italianValue, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
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
            autoFragmentsLocaleCode = null;
            AutoFragments.Clear();
            LocaleChanged?.Invoke();
        }

        private static string ReplaceLocalizedFragments(string sourceText)
        {
            if (string.Equals(CurrentLocaleCode, "it", StringComparison.OrdinalIgnoreCase))
                return sourceText;

            EnsureAutoFragments();
            string localized = sourceText;
            foreach (KeyValuePair<string, string> fragment in AutoFragments)
            {
                if (localized.Contains(fragment.Key, StringComparison.Ordinal))
                    localized = localized.Replace(fragment.Key, fragment.Value);
            }

            return localized;
        }

        private static void EnsureAutoFragments()
        {
            string localeCode = CurrentLocaleCode;
            if (string.Equals(autoFragmentsLocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
                return;

            AutoFragments.Clear();
            autoFragmentsLocaleCode = localeCode;
            try
            {
                Locale italian = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier("it"));
                var italianOperation = LocalizationSettings.StringDatabase.GetTableAsync(TableName, italian);
                var targetOperation = LocalizationSettings.StringDatabase.GetTableAsync(TableName);
                var italianTable = italianOperation.WaitForCompletion();
                var targetTable = targetOperation.WaitForCompletion();
                if (italianTable == null || targetTable == null)
                    return;

                foreach (var source in italianTable.Values)
                {
                    if (!source.Key.StartsWith("auto.", StringComparison.Ordinal) ||
                        source.Value.Length < 4 || source.Value.Contains("{") ||
                        (!source.Value.Contains(" ") && !source.Value.EndsWith(":")))
                        continue;

                    string translated = targetTable.GetEntry(source.Key)?.Value;
                    if (!string.IsNullOrWhiteSpace(translated) &&
                        !string.Equals(source.Value, translated, StringComparison.Ordinal))
                        AutoFragments.Add(new KeyValuePair<string, string>(source.Value, translated));
                }

                AutoFragments.Sort((left, right) => right.Key.Length.CompareTo(left.Key.Length));
            }
            catch (Exception)
            {
                AutoFragments.Clear();
            }
        }
    }
}
