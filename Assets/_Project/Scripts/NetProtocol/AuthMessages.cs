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

    [Serializable]
    public sealed class AuthResponse
    {
        public bool ok;
        public string error;
        public string token;
        public string playerId;
        public string username;
    }

    [Serializable]
    public sealed class ErrorMessage
    {
        public string code;
        public string message;
    }
}
