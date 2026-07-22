using System.Threading.Tasks;
using AccardND.Network;
#if UGS_AUTH
using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
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
        public static bool IsAvailable => true;

        /// <summary>Ritorna (accessToken, provider) oppure (null, messaggio di errore).</summary>
        public static async Task<(string AccessToken, string Result)> SignInWithGoogleAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

#if UNITY_WEBGL && !UNITY_EDITOR
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
