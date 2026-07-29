using System;

namespace AccardND.NetProtocol
{
    [Serializable]
    public sealed class RegisterRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public sealed class LoginRequest
    {
        public string username;
        public string password;
    }

    /// <summary>Login con token di Unity Authentication: il server lo valida
    /// contro le chiavi pubbliche di Unity, nessuna password coinvolta.</summary>
    [Serializable]
    public sealed class UgsLoginRequest
    {
        public string accessToken;
        public string displayName;

        /// <summary>Metodo di accesso dietro al token UGS ("google",
        /// "google-play-games", "anonymous"...). Serve solo a distinguere gli
        /// accessi nel pannello admin: il server lo usa se il token non porta
        /// gia' l'informazione, quindi non e' un dato di sicurezza.</summary>
        public string authMethod;

        /// <summary>ID token del login Google appena fatto, quando c'e' (sui resume
        /// di sessione no). Il server ne verifica la firma contro le chiavi di
        /// Google e ne estrae la mail, che serve solo al pannello admin per
        /// riconoscere a quale account Google corrisponde un giocatore.</summary>
        public string googleIdToken;
    }

    /// <summary>
    /// Riaggancio di una sessione dopo una caduta di rete: il token è quello emesso
    /// dal server all'ultimo login riuscito. Evita di ripassare da Google/UGS a ogni
    /// blip di rete, che è lento e può fallire proprio mentre la rete è instabile.
    /// </summary>
    [Serializable]
    public sealed class SessionResumeRequest
    {
        public string sessionToken;
    }

    [Serializable]
    public sealed class AuthResponse
    {
        public bool ok;
        public string error;
        public string token;
        public string playerId;
        public string username;
        public bool isNewAccount;
        public string authProvider;
        public bool requiresNickname;

        /// <summary>
        /// Token di sessione da rigiocare su <see cref="MessageTypes.AuthSession"/> per
        /// riconnettersi senza rifare il login. Vive solo in memoria sul client.
        /// </summary>
        public string sessionToken;
    }

    [Serializable]
    public sealed class SetNicknameRequest
    {
        public string nickname;
    }

    [Serializable]
    public sealed class NicknameResponse
    {
        public bool ok;
        public string error;
        public string nickname;
    }

    [Serializable]
    public sealed class ErrorMessage
    {
        public string code;
        public string message;
    }
}
