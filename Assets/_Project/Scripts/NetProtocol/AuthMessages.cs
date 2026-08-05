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

        /// <summary>Versione della build che sta accedendo. Il server la confronta
        /// con la versione target e blocca i client vecchi.</summary>
        public string clientVersion;
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

        /// <summary>Come in <see cref="UgsLoginRequest.clientVersion"/>: anche il
        /// riaggancio passa dal controllo di versione.</summary>
        public string clientVersion;
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

        /// <summary>
        /// L'accesso è stato rifiutato perché la build è più vecchia di quella
        /// richiesta dal server. Non è un errore da riprovare: il client deve
        /// fermarsi sul login e chiedere l'aggiornamento.
        /// </summary>
        public bool requiresUpdate;

        /// <summary>Versione che il server pretende, da mostrare nell'avviso.</summary>
        public string requiredVersion;

        /// <summary>Dove si scarica la versione nuova (store o sito). Può essere vuoto.</summary>
        public string updateUrl;
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

    /// <summary>
    /// La sessione è stata chiusa dal server perché l'account ha fatto accesso da
    /// un'altra parte. Non è un errore da riprovare: il client avvisa e chiude.
    /// </summary>
    [Serializable]
    public sealed class SessionKickedMessage
    {
        public string message;
    }

    /// <summary>
    /// Vista minima su un payload di accesso: serve al server per leggere la
    /// versione del client da qualunque messaggio di autenticazione, senza dover
    /// sapere quale dei tipi di login sta arrivando.
    /// </summary>
    [Serializable]
    public sealed class ClientVersionInfo
    {
        public string clientVersion;
    }

    [Serializable]
    public sealed class ErrorMessage
    {
        public string code;
        public string message;
    }
}
