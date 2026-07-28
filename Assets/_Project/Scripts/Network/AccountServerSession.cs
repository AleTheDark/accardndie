using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Sessione server unica dell'account. La connessione autenticata nella schermata
    /// di login sopravvive al cambio scena ed e' condivisa da Hub, progressione e PvP.
    /// </summary>
    public static class AccountServerSession
    {
        private static PvpServerClient client;
        private static PvpServerMessageDispatcher dispatcher;
        private static AuthResponse identity;

        public static bool IsReady =>
            client is { IsConnected: true } &&
            dispatcher != null &&
            identity is { ok: true };

        public static bool TryGet(
            out PvpServerClient sharedClient,
            out PvpServerMessageDispatcher sharedDispatcher,
            out AuthResponse sharedIdentity)
        {
            sharedClient = client;
            sharedDispatcher = dispatcher;
            sharedIdentity = identity;
            return IsReady;
        }

        public static void Adopt(
            PvpServerClient authenticatedClient,
            PvpServerMessageDispatcher authenticatedDispatcher,
            AuthResponse authenticatedIdentity)
        {
            if (authenticatedClient == null)
                throw new System.ArgumentNullException(nameof(authenticatedClient));
            if (authenticatedDispatcher == null)
                throw new System.ArgumentNullException(nameof(authenticatedDispatcher));
            if (authenticatedIdentity is not { ok: true })
                throw new System.ArgumentException("Identita' server non autenticata.", nameof(authenticatedIdentity));

            if (client != null && !ReferenceEquals(client, authenticatedClient))
                client.Dispose();

            client = authenticatedClient;
            dispatcher = authenticatedDispatcher;
            identity = authenticatedIdentity;
        }

        public static void UpdateIdentity(string playerId, string username, bool requiresNickname = false)
        {
            if (identity == null)
                return;

            if (!string.IsNullOrWhiteSpace(playerId))
                identity.playerId = playerId;
            if (!string.IsNullOrWhiteSpace(username))
                identity.username = username.Trim();
            identity.requiresNickname = requiresNickname;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            client?.Dispose();
            client = null;
            dispatcher = null;
            identity = null;
        }
    }
}
