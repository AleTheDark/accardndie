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
