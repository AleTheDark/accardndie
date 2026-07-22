using System;
using System.Threading.Tasks;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Connessione headless (senza UI) al server per la progressione single player.
    /// Si autentica come account ospite legato al dispositivo (come il fallback del PvP) e
    /// costruisce un <see cref="ServerSinglePlayerProgressRepository"/> sulla connessione autenticata.
    /// Pensato per essere aggiunto via AddComponent dal controller single player: se il server
    /// non e raggiungibile o rifiuta il login, restituisce null e il controller resta sul locale.
    /// </summary>
    public sealed class SinglePlayerServerLink : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "wss://accardndie.com/ws";

        private PvpServerClient client;
        private PvpServerMessageDispatcher dispatcher;
        private ServerSinglePlayerProgressRepository repository;
        private Task<ServerSinglePlayerProgressRepository> ensureTask;
        private bool authenticated;

        public bool IsReady => authenticated && repository != null && client is { IsConnected: true };

        /// <summary>Imposta l'URL server prima della prima connessione (default = produzione).</summary>
        public void ConfigureUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
                serverUrl = url;
        }

        /// <summary>
        /// Assicura connessione + autenticazione e restituisce il repository autoritativo, oppure
        /// null se il server non e raggiungibile o il login e rifiutato. Idempotente: chiamate
        /// concorrenti condividono lo stesso task.
        /// </summary>
        public Task<ServerSinglePlayerProgressRepository> EnsureRepositoryAsync()
        {
            if (IsReady)
                return Task.FromResult(repository);
            return ensureTask ??= ConnectAndAuthenticateAsync();
        }

        private async Task<ServerSinglePlayerProgressRepository> ConnectAndAuthenticateAsync()
        {
            try
            {
                client ??= new PvpServerClient();
                dispatcher ??= new PvpServerMessageDispatcher(client);
                if (!client.IsConnected)
                    await client.ConnectAsync(serverUrl);

                (string username, string password) = GuestCredentials.Derive();
                if (!await AuthenticateGuestAsync(username, password))
                    return null;

                authenticated = true;
                repository = new ServerSinglePlayerProgressRepository(
                    new ServerSinglePlayerProgressClient(dispatcher));
                await repository.RefreshAsync();
                return repository;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SP] Connessione progressione non riuscita: {exception.Message}");
                return null;
            }
            finally
            {
                ensureTask = null;
            }
        }

        private async Task<bool> AuthenticateGuestAsync(string username, string password)
        {
            AuthResponse response = await SendAuthAsync(
                MessageTypes.AuthRegister, new RegisterRequest { username = username, password = password });
            if (response is { ok: true })
                return true;

            // Account gia esistente (o registrazione disattivata): tenta il login.
            response = await SendAuthAsync(
                MessageTypes.AuthLogin, new LoginRequest { username = username, password = password });
            return response is { ok: true };
        }

        private async Task<AuthResponse> SendAuthAsync(string type, object payload)
        {
            Envelope envelope = await dispatcher.RequestAsync(type, payload, MessageTypes.AuthResponse);
            return envelope.type == MessageTypes.Error
                ? new AuthResponse { ok = false }
                : PvpServerClient.ParsePayload<AuthResponse>(envelope);
        }

        private void Update()
        {
            dispatcher?.Pump();
        }

        /// <summary>
        /// Chiude la connessione. Usato quando si entra in PvP per evitare due socket sullo stesso
        /// account ospite; alla successiva richiesta il link si riconnette da capo.
        /// </summary>
        public void Shutdown()
        {
            authenticated = false;
            repository = null;
            ensureTask = null;
            client?.Dispose();
            client = null;
            dispatcher = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
