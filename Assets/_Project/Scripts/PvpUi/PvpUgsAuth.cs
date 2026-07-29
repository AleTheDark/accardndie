using System.Threading.Tasks;
using AccardND.Network;
#if UGS_AUTH
using System;
using System.Security.Cryptography;
using System.Text;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using System.Net;
#endif
#endif
#if UGS_AUTH && UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace AccardND.PvpUi
{
    /// <summary>
    /// Login anonimo con Unity Authentication. Compilato solo se il pacchetto
    /// com.unity.services.authentication è installato (define UGS_AUTH);
    /// altrimenti IsAvailable è false e si usa il fallback con password.
    /// Richiede il progetto collegato a Unity Cloud (Project Settings > Services).
    /// </summary>
    public static class PvpUgsAuth
    {
        /// <summary>Valori di <see cref="CurrentAuthMethod"/>, condivisi col server.</summary>
        public const string AuthMethodGoogle = "google";
        public const string AuthMethodGooglePlayGames = "google-play-games";
        public const string AuthMethodAnonymous = "anonymous";
        public const string AuthMethodUnknown = "unknown";

#if UGS_AUTH
#if UNITY_EDITOR
        public const string EditorGoogleClientIdPrefsKey = "AccardND.GoogleOAuth.EditorClientId";
        public const string EditorGoogleClientSecretPrefsKey = "AccardND.GoogleOAuth.EditorClientSecret";
        public const string EditorGoogleRedirectUri = "http://127.0.0.1:53682/oauth2callback/";
        public const string DefaultEditorGoogleClientId =
            "866249556431-mgdm97uvov7mjvect4bp453dpp2oe48u.apps.googleusercontent.com";
#endif

        public static bool IsAvailable => true;

        /// <summary>
        /// ID token dell'ultimo login Google interattivo, da mandare al server
        /// insieme al token UGS: il server ne verifica la firma e ne ricava la
        /// mail, che nel pannello admin dice a quale account Google corrisponde
        /// un giocatore. Resta null sui resume di sessione, dove Google non entra
        /// in gioco e la mail gia' salvata non va toccata.
        /// </summary>
        public static string LastGoogleIdToken { get; private set; }

        /// <summary>Ritorna (accessToken, provider) oppure (null, messaggio di errore).</summary>
        public static async Task<(string AccessToken, string Result)> SignInWithGoogleAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                // Permetti di passare da una sessione anonima salvata all'account
                // Google scelto esplicitamente nella schermata di login.
                if (AuthenticationService.Instance.IsSignedIn ||
                    AuthenticationService.Instance.SessionTokenExists)
                    AuthenticationService.Instance.SignOut(true);

#if UNITY_EDITOR
                return await SignInWithEditorGoogleAsync();
#elif UNITY_WEBGL
                return await SignInWithWebGoogleAsync();
#elif UNITY_ANDROID
                return await SignInWithBrokeredGoogleAsync();
#else
                return (null, "Login Google non disponibile su questa piattaforma.");
#endif
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PvP] Login Google fallito: {exception.Message}");
                return (null, exception.Message);
            }
        }

        public static Task<(string AccessToken, string Result)> SignInAsync() => SignInWithGoogleAsync();

        /// <summary>
        /// Come si e' autenticata davvero la sessione UGS corrente: "google",
        /// "google-play-games", "anonymous" o il TypeId dell'identita' collegata.
        /// Va letto dopo il login (anche dopo un resume di sessione, dove il
        /// risultato della SignIn dice solo "ugs-session") e mandato al server:
        /// e' l'unico modo per sapere nel pannello admin se dietro l'account
        /// esterno c'e' un Google vero o un ospite anonimo.
        /// </summary>
        public static string CurrentAuthMethod()
        {
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    return AuthMethodUnknown;

                var identities = AuthenticationService.Instance.PlayerInfo?.Identities;
                if (identities == null || identities.Count == 0)
                    return AuthMethodAnonymous;

                // TypeId sono gli id provider di UGS: "google.com" per il login
                // Google, "google-play-games" per Google Play Games.
                foreach (var identity in identities)
                {
                    string typeId = identity?.TypeId;
                    if (string.IsNullOrEmpty(typeId))
                        continue;
                    if (typeId.IndexOf("play-games", StringComparison.OrdinalIgnoreCase) >= 0)
                        return AuthMethodGooglePlayGames;
                    if (typeId.IndexOf("google", StringComparison.OrdinalIgnoreCase) >= 0)
                        return AuthMethodGoogle;
                    return typeId.ToLowerInvariant();
                }

                return AuthMethodAnonymous;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PvP] Metodo di accesso UGS non determinabile: {exception.Message}");
                return AuthMethodUnknown;
            }
        }

        /// <summary>
        /// Solo "google" vale come sessione buona. Google Play Games e' un provider
        /// UGS distinto: lo stesso account Google ci arrivava con un PlayerId diverso
        /// e sdoppiava il profilo, quindi le vecchie sessioni play-games vengono
        /// scartate e l'utente rifa' l'accesso con Google.
        /// </summary>
        public static bool IsCurrentSessionGoogle() => CurrentAuthMethod() == AuthMethodGoogle;

#if UNITY_EDITOR
        private static async Task<(string AccessToken, string Result)> SignInWithEditorGoogleAsync()
        {
            string clientId = UnityEditor.EditorPrefs
                .GetString(EditorGoogleClientIdPrefsKey, DefaultEditorGoogleClientId)
                .Trim();
            string clientSecret = UnityEditor.EditorPrefs.GetString(EditorGoogleClientSecretPrefsKey, string.Empty).Trim();
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return (null,
                    "OAuth Google Editor non configurato. Apri Tools > AccardND > Google OAuth Editor.");
            }

            string state = CreateUrlSafeRandom(32);
            string nonce = CreateUrlSafeRandom(32);
            string codeVerifier = CreateUrlSafeRandom(64);
            string codeChallenge = Sha256Base64Url(codeVerifier);

            using var listener = new HttpListener();
            listener.Prefixes.Add(EditorGoogleRedirectUri);
            try
            {
                listener.Start();
            }
            catch (HttpListenerException exception)
            {
                return (null,
                    $"Impossibile aprire il callback OAuth {EditorGoogleRedirectUri}: {exception.Message}");
            }

            string authorizationUrl =
                "https://accounts.google.com/o/oauth2/v2/auth"
                + "?client_id=" + Uri.EscapeDataString(clientId)
                + "&redirect_uri=" + Uri.EscapeDataString(EditorGoogleRedirectUri)
                + "&response_type=code"
                + "&scope=" + Uri.EscapeDataString("openid email profile")
                + "&state=" + Uri.EscapeDataString(state)
                + "&nonce=" + Uri.EscapeDataString(nonce)
                + "&code_challenge=" + Uri.EscapeDataString(codeChallenge)
                + "&code_challenge_method=S256"
                + "&prompt=select_account";

            Application.OpenURL(authorizationUrl);

            Task<HttpListenerContext> callbackTask = listener.GetContextAsync();
            Task completed = await Task.WhenAny(callbackTask, Task.Delay(TimeSpan.FromMinutes(2)));
            if (completed != callbackTask)
                return (null, "Timeout del login Google nell'Editor.");

            HttpListenerContext callback = await callbackTask;
            string error = callback.Request.QueryString["error"];
            string returnedState = callback.Request.QueryString["state"];
            string code = callback.Request.QueryString["code"];
            bool callbackIsValid =
                string.IsNullOrEmpty(error) &&
                string.Equals(state, returnedState, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(code);

            await ReplyToBrowserAsync(
                callback.Response,
                callbackIsValid
                    ? "Accesso completato. Puoi tornare a Unity."
                    : "Accesso non riuscito. Puoi tornare a Unity.");

            if (!string.IsNullOrEmpty(error))
                return (null, "Google OAuth: " + error);
            if (!string.Equals(state, returnedState, StringComparison.Ordinal))
                return (null, "Google OAuth ha restituito uno state non valido.");
            if (string.IsNullOrEmpty(code))
                return (null, "Google OAuth non ha restituito il codice di autorizzazione.");

            var form = new WWWForm();
            form.AddField("client_id", clientId);
            form.AddField("client_secret", clientSecret);
            form.AddField("code", code);
            form.AddField("code_verifier", codeVerifier);
            form.AddField("grant_type", "authorization_code");
            form.AddField("redirect_uri", EditorGoogleRedirectUri);

            using UnityWebRequest request =
                UnityWebRequest.Post("https://oauth2.googleapis.com/token", form);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
                await PvpAsync.NextFrameAsync();

            if (request.result != UnityWebRequest.Result.Success)
                return (null, $"Scambio token Google fallito: {request.downloadHandler.text}");

            GoogleTokenResponse tokenResponse =
                JsonUtility.FromJson<GoogleTokenResponse>(request.downloadHandler.text);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.id_token))
                return (null, "Google non ha restituito un ID token.");

            if (!IdTokenHasNonce(tokenResponse.id_token, nonce))
                return (null, "Google ha restituito un ID token con nonce non valido.");

            await AuthenticationService.Instance.SignInWithGoogleAsync(tokenResponse.id_token);
            LastGoogleIdToken = tokenResponse.id_token;
            return (AuthenticationService.Instance.AccessToken, "google-editor");
        }

        private static async Task ReplyToBrowserAsync(HttpListenerResponse response, string message)
        {
            string html =
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>AccardND</title></head>"
                + "<body style=\"font-family:sans-serif;text-align:center;padding:4rem;background:#130d25;color:#fff\">"
                + "<h1>AccardND</h1><p>" + WebUtility.HtmlEncode(message) + "</p></body></html>";
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            response.Close();
        }

        private static bool IdTokenHasNonce(string idToken, string expectedNonce)
        {
            string[] parts = idToken.Split('.');
            if (parts.Length != 3)
                return false;

            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                GoogleIdTokenPayload claims = JsonUtility.FromJson<GoogleIdTokenPayload>(json);
                return claims != null &&
                       string.Equals(claims.nonce, expectedNonce, StringComparison.Ordinal);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class GoogleTokenResponse
        {
            public string id_token;
        }

        [Serializable]
        private sealed class GoogleIdTokenPayload
        {
            public string nonce;
        }
#endif

        private static string CreateUrlSafeRandom(int byteCount)
        {
            byte[] bytes = new byte[byteCount];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Base64Url(bytes);
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string Sha256Base64Url(string value)
        {
            using SHA256 sha = SHA256.Create();
            return Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(value)));
        }

        /// <summary>
        /// Riprende una sessione già salvata senza login interattivo. Ritorna
        /// (accessToken, "ugs-session") se c'è una sessione valida, altrimenti
        /// (null, "no-session"). Il token di sessione UGS viene persistito in
        /// automatico (anche in WebGL, via IndexedDB), quindi basta riusarlo.
        /// </summary>
        public static async Task<(string AccessToken, string Result)> TryResumeSessionAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (AuthenticationService.Instance.IsSignedIn)
                    return (AuthenticationService.Instance.AccessToken, "ugs-session");

                // Con un session token in cache, SignInAnonymouslyAsync NON crea un
                // utente anonimo: riusa il token e ripristina il giocatore già loggato
                // (anche se era Google). È il meccanismo di resume ufficiale di UGS.
                if (AuthenticationService.Instance.SessionTokenExists)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    return (AuthenticationService.Instance.AccessToken, "ugs-session");
                }

                return (null, "no-session");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PvP] Ripristino sessione UGS fallito: {exception.Message}");
                return (null, exception.Message);
            }
        }

        /// <summary>
        /// Elimina la sessione UGS e il relativo token persistito, obbligando
        /// il prossimo accesso a passare nuovamente dal provider Google.
        /// </summary>
        public static void ForgetSession()
        {
            LastGoogleIdToken = null;
            if (AuthenticationService.Instance.IsSignedIn ||
                AuthenticationService.Instance.SessionTokenExists)
                AuthenticationService.Instance.SignOut(true);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int AccardNdGoogleSignInStart();
        [DllImport("__Internal")] private static extern int AccardNdGoogleSignInState(int id);
        [DllImport("__Internal")] private static extern IntPtr AccardNdGoogleSignInCredential(int id);
        [DllImport("__Internal")] private static extern IntPtr AccardNdGoogleSignInError(int id);
        [DllImport("__Internal")] private static extern void AccardNdGoogleSignInRelease(int id);

        private static async Task<(string AccessToken, string Result)> SignInWithWebGoogleAsync()
        {
            int requestId = AccardNdGoogleSignInStart();
            if (requestId < 0)
                return (null, "Bridge Google Web non disponibile.");

            try
            {
                float start = Time.realtimeSinceStartup;
                while (true)
                {
                    int state = AccardNdGoogleSignInState(requestId);
                    if (state == 1)
                    {
                        string idToken = PtrToString(AccardNdGoogleSignInCredential(requestId));
                        if (string.IsNullOrEmpty(idToken))
                            return (null, "ID token Google vuoto.");

                        await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
                        LastGoogleIdToken = idToken;
                        return (AuthenticationService.Instance.AccessToken, "google-web");
                    }

                    if (state == 2)
                        return (null, PtrToString(AccardNdGoogleSignInError(requestId)) ?? "Login Google annullato.");

                    if (Time.realtimeSinceStartup - start > 60f)
                        return (null, "Timeout login Google Web.");

                    await PvpAsync.NextFrameAsync();
                }
            }
            finally
            {
                AccardNdGoogleSignInRelease(requestId);
            }
        }

        private static string PtrToString(IntPtr ptr) =>
            ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) : null;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        // Stesso host del WebSocket di gioco (wss://accardndie.com/ws): se cambia
        // il dominio vanno aggiornati entrambi.
        private const string GoogleBrokerBaseUrl = "https://accardndie.com";

        // Il giro passa dal browser di sistema: serve tempo per scegliere
        // l'account, e nel frattempo l'app resta in background.
        private const float BrokerTimeoutSeconds = 300f;
        private const float BrokerPollSeconds = 2f;

        /// <summary>
        /// Login Google su Android nativo, mediato dal nostro server.
        ///
        /// L'app non parla direttamente con Google: un client OAuth "Android"
        /// produrrebbe un ID token con audience diversa da quella configurata sul
        /// provider Google di UGS, e Google non accetta redirect loopback per i
        /// client Android. Il broker sul server usa invece lo stesso Web Client ID
        /// del login web, quindi lo stesso account Google produce lo stesso
        /// PlayerId UGS del browser: niente profili sdoppiati tra APK e PWA.
        ///
        /// Flusso: begin (l'app manda l'hash di un verifier monouso e riceve
        /// requestId + URL) -> browser di sistema -> callback sul server, che
        /// scambia il codice -> l'app ritira l'ID token mostrando il verifier.
        /// </summary>
        private static async Task<(string AccessToken, string Result)> SignInWithBrokeredGoogleAsync()
        {
            string verifier = CreateUrlSafeRandom(48);
            (string requestId, string authorizeUrl, string beginError) =
                await BeginBrokeredGoogleAsync(Sha256Base64Url(verifier));
            if (beginError != null)
                return (null, beginError);

            Application.OpenURL(authorizeUrl);

            // L'app finisce in background mentre l'utente sceglie l'account: il
            // token resta in attesa sul server finche' non torna a ritirarlo.
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < BrokerTimeoutSeconds)
            {
                await WaitSecondsAsync(BrokerPollSeconds);

                (string idToken, string pollError, bool pending) =
                    await PollBrokeredGoogleAsync(requestId, verifier);
                if (pending)
                    continue;
                if (pollError != null)
                    return (null, pollError);

                await AuthenticationService.Instance.SignInWithGoogleAsync(idToken);
                LastGoogleIdToken = idToken;
                Debug.Log($"[Login][UGS] Google collegato al PlayerId {AuthenticationService.Instance.PlayerId}.");
                return (AuthenticationService.Instance.AccessToken, "google-android");
            }

            return (null, "Timeout del login Google: riprova.");
        }

        private static async Task<(string RequestId, string AuthorizeUrl, string Error)>
            BeginBrokeredGoogleAsync(string challenge)
        {
            string body = JsonUtility.ToJson(new BrokerBeginRequest { challenge = challenge });
            (string payload, string error) = await PostJsonAsync(GoogleBrokerBaseUrl + "/auth/google/begin", body);
            if (error != null)
                return (null, null, error);

            BrokerBeginResponse response = JsonUtility.FromJson<BrokerBeginResponse>(payload);
            if (response == null || string.IsNullOrEmpty(response.requestId) ||
                string.IsNullOrEmpty(response.authorizeUrl))
            {
                return (null, null, !string.IsNullOrEmpty(response?.error)
                    ? response.error
                    : "Il server non ha avviato il login Google.");
            }

            return (response.requestId, response.authorizeUrl, null);
        }

        private static async Task<(string IdToken, string Error, bool Pending)>
            PollBrokeredGoogleAsync(string requestId, string verifier)
        {
            string body = JsonUtility.ToJson(new BrokerTokenRequest
            {
                requestId = requestId,
                verifier = verifier
            });
            (string payload, string error) = await PostJsonAsync(GoogleBrokerBaseUrl + "/auth/google/token", body);
            // Un errore di rete durante l'attesa non e' definitivo: il browser
            // potrebbe non aver ancora finito. Si riprova al giro dopo.
            if (error != null)
                return (null, null, true);

            BrokerTokenResponse response = JsonUtility.FromJson<BrokerTokenResponse>(payload);
            if (response == null)
                return (null, "Risposta del server non leggibile.", false);
            if (response.status == "pending")
                return (null, null, true);
            if (response.status != "ready" || string.IsNullOrEmpty(response.idToken))
                return (null, string.IsNullOrEmpty(response.error) ? "Login Google non riuscito." : response.error, false);

            return (response.idToken, null, false);
        }

        private static async Task<(string Payload, string Error)> PostJsonAsync(string url, string json)
        {
            using UnityWebRequest request = new(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20
            };
            request.SetRequestHeader("Content-Type", "application/json");

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
                await PvpAsync.NextFrameAsync();

            return request.result == UnityWebRequest.Result.Success
                ? (request.downloadHandler.text, null)
                : (null, $"Server non raggiungibile ({request.error}).");
        }

        private static async Task WaitSecondsAsync(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                await PvpAsync.NextFrameAsync();
        }

        [Serializable]
        private sealed class BrokerBeginRequest
        {
            public string challenge;
        }

        [Serializable]
        private sealed class BrokerBeginResponse
        {
            public string requestId;
            public string authorizeUrl;
            public string error;
        }

        [Serializable]
        private sealed class BrokerTokenRequest
        {
            public string requestId;
            public string verifier;
        }

        [Serializable]
        private sealed class BrokerTokenResponse
        {
            public string status;
            public string idToken;
            public string error;
        }
#endif
#else
        public static bool IsAvailable => false;

        public static string LastGoogleIdToken => null;

        public static Task<(string AccessToken, string Result)> SignInAsync() =>
            Task.FromResult<(string, string)>((null, "Pacchetto Unity Authentication non installato."));

        public static Task<(string AccessToken, string Result)> SignInWithGoogleAsync() => SignInAsync();

        public static Task<(string AccessToken, string Result)> TryResumeSessionAsync() =>
            Task.FromResult<(string, string)>((null, "no-session"));

        public static string CurrentAuthMethod() => AuthMethodUnknown;

        public static bool IsCurrentSessionGoogle() => false;

        public static void ForgetSession()
        {
        }
#endif
    }
}
