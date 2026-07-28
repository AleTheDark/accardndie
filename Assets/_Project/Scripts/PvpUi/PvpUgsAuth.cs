using System.Threading.Tasks;
using AccardND.Network;
#if UGS_AUTH
using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
#if UNITY_EDITOR
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.Networking;
#endif
#endif
#if UGS_AUTH && UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
#if UGS_AUTH && GPGS_AUTH
using GooglePlayGames;
using GooglePlayGames.BasicApi;
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
#if UGS_AUTH
#if UNITY_EDITOR
        public const string EditorGoogleClientIdPrefsKey = "AccardND.GoogleOAuth.EditorClientId";
        public const string EditorGoogleClientSecretPrefsKey = "AccardND.GoogleOAuth.EditorClientSecret";
        public const string EditorGoogleRedirectUri = "http://127.0.0.1:53682/oauth2callback/";
        public const string DefaultEditorGoogleClientId =
            "866249556431-mgdm97uvov7mjvect4bp453dpp2oe48u.apps.googleusercontent.com";
#endif

        public static bool IsAvailable => true;

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
#elif UNITY_WEBGL && !UNITY_EDITOR
                return await SignInWithWebGoogleAsync();
#elif UNITY_ANDROID && GPGS_AUTH
                return await SignInWithGooglePlayGamesAsync();
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

        public static async Task<(string AccessToken, string Result)> SignInAnonymouslyAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return (AuthenticationService.Instance.AccessToken,
                    "ugs-anonymous");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PvP] Login UGS fallito: {exception.Message}");
                return (null, exception.Message);
            }
        }

        public static Task<(string AccessToken, string Result)> SignInAsync() => SignInWithGoogleAsync();

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
            string codeChallenge;
            using (SHA256 sha = SHA256.Create())
                codeChallenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier)));

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

        private static string CreateUrlSafeRandom(int byteCount)
        {
            byte[] bytes = new byte[byteCount];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Base64Url(bytes);
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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

#if UNITY_ANDROID && GPGS_AUTH
        private static async Task<(string AccessToken, string Result)> SignInWithGooglePlayGamesAsync()
        {
            PlayGamesPlatform.Activate();

            SignInStatus status = await AuthenticateGooglePlayGamesAsync();
            if (status != SignInStatus.Success)
                return (null, status.ToString());

            string authCode = await RequestServerSideAccessAsync();
            if (string.IsNullOrEmpty(authCode))
                return (null, "Server auth code vuoto.");

            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(authCode);
            return (AuthenticationService.Instance.AccessToken, "google-play-games");
        }

        private static Task<SignInStatus> AuthenticateGooglePlayGamesAsync()
        {
            var completion = new TaskCompletionSource<SignInStatus>();
            PlayGamesPlatform.Instance.Authenticate(status => completion.TrySetResult(status));
            return completion.Task;
        }

        private static Task<string> RequestServerSideAccessAsync()
        {
            var completion = new TaskCompletionSource<string>();
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, code => completion.TrySetResult(code));
            return completion.Task;
        }
#endif
#else
        public static bool IsAvailable => false;

        public static Task<(string AccessToken, string Result)> SignInAsync() =>
            Task.FromResult<(string, string)>((null, "Pacchetto Unity Authentication non installato."));

        public static Task<(string AccessToken, string Result)> SignInWithGoogleAsync() => SignInAsync();

        public static Task<(string AccessToken, string Result)> SignInAnonymouslyAsync() => SignInAsync();

        public static Task<(string AccessToken, string Result)> TryResumeSessionAsync() =>
            Task.FromResult<(string, string)>((null, "no-session"));
#endif
    }
}
