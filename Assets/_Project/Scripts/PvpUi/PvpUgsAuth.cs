using System.Threading.Tasks;
#if UGS_AUTH
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
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

        /// <summary>Ritorna (accessToken, playerId) oppure (null, messaggio di errore).</summary>
        public static async Task<(string AccessToken, string Result)> SignInAnonymouslyAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return (AuthenticationService.Instance.AccessToken,
                    AuthenticationService.Instance.PlayerId);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[PvP] Login UGS fallito: {exception.Message}");
                return (null, exception.Message);
            }
        }
#else
        public static bool IsAvailable => false;

        public static Task<(string AccessToken, string Result)> SignInAnonymouslyAsync() =>
            Task.FromResult<(string, string)>((null, "Pacchetto Unity Authentication non installato."));
#endif
    }
}
