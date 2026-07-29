using System;
using System.Threading.Tasks;
using UnityEngine;

namespace AccardND.Network
{
    /// <summary>
    /// Aggancio headless (senza UI) alla progressione single player autoritativa.
    /// Riusa la sessione account aperta dalla schermata di login e ci costruisce sopra
    /// un <see cref="ServerSinglePlayerProgressRepository"/>.
    /// Pensato per essere aggiunto via AddComponent dal controller single player: se non
    /// c'e' una sessione, restituisce null e il controller resta sulla progressione locale.
    /// Non apre piu' una connessione per conto suo ne' crea account ospite: gli account
    /// nascono soltanto dal login Google.
    /// </summary>
    public sealed class SinglePlayerServerLink : MonoBehaviour
    {
        private PvpServerClient client;
        private PvpServerMessageDispatcher dispatcher;
        private ServerSinglePlayerProgressRepository repository;
        private Task<ServerSinglePlayerProgressRepository> ensureTask;
        private bool authenticated;

        public bool IsReady => authenticated && repository != null && client is { IsConnected: true };
        public event Action Reconnected;

        /// <summary>
        /// Restituisce il repository autoritativo, oppure null se non c'e' una sessione
        /// account attiva. Idempotente: chiamate concorrenti condividono lo stesso task.
        /// </summary>
        public Task<ServerSinglePlayerProgressRepository> EnsureRepositoryAsync()
        {
            if (IsReady)
                return Task.FromResult(repository);
            return ensureTask ??= ConnectAndAuthenticateAsync();
        }

        /// <summary>Quanto si aspetta che una riconnessione in corso vada a buon fine prima di ripiegare sul locale.</summary>
        private const float ReconnectWaitSeconds = 12f;

        private async Task<ServerSinglePlayerProgressRepository> ConnectAndAuthenticateAsync()
        {
            try
            {
                // Un blip di rete non deve buttare la campagna in modalita' locale: se
                // la sessione si sta riaprendo da sola, conviene darle il tempo di
                // farlo invece di dichiarare il server assente.
                float deadline = Time.realtimeSinceStartup + ReconnectWaitSeconds;
                while (!AccountServerSession.IsReady
                       && AccountServerSession.IsReconnecting
                       && Time.realtimeSinceStartup < deadline)
                {
                    await PvpAsync.NextFrameAsync();
                }

                if (AccountServerSession.TryGet(
                        out PvpServerClient sharedClient,
                        out PvpServerMessageDispatcher sharedDispatcher,
                        out _))
                {
                    client = sharedClient;
                    dispatcher = sharedDispatcher;
                    authenticated = true;
                    var progressClient = new ServerSinglePlayerProgressClient(dispatcher);
                    await progressClient.PendingMutationsReplayed;
                    repository = new ServerSinglePlayerProgressRepository(
                        progressClient);
                    await repository.RefreshAsync();
                    return repository;
                }

                // Nessuna sessione: non si crea piu' un account ospite legato al
                // dispositivo. Gli account nascono solo dal login Google, quindi
                // senza sessione la progressione resta locale.
                return null;
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

        private void Update()
        {
            dispatcher?.Pump();
        }

        private async void HandleAccountReconnected()
        {
            if (repository == null)
                return;
            try
            {
                await repository.RefreshAsync();
                Reconnected?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SP] Aggiornamento dopo riconnessione fallito: {exception.Message}");
            }
        }

        private void OnEnable() => AccountServerSession.Reconnected += HandleAccountReconnected;

        private void OnDisable() => AccountServerSession.Reconnected -= HandleAccountReconnected;

        /// <summary>
        /// Rilascia il repository usato dall'Hub. La sessione account condivisa resta aperta
        /// e puo' essere riusata immediatamente dal PvP o dalla richiesta successiva.
        /// </summary>
        public void Shutdown()
        {
            // La connessione e' sempre quella condivisa: qui non si chiude mai,
            // resta disponibile per il PvP o per la richiesta successiva.
            authenticated = false;
            repository = null;
            ensureTask = null;
            client = null;
            dispatcher = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
