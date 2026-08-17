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

        /// <summary>
        /// Tenuto da parte per poter rigiocare l'outbox al ritorno della rete: il
        /// repository non lo espone, e le mutazioni rimaste su disco sono sue.
        /// </summary>
        private ServerSinglePlayerProgressClient progressClient;
        private Task<ServerSinglePlayerProgressRepository> ensureTask;
        private bool authenticated;

        public bool IsReady => authenticated && repository != null && client is { IsConnected: true };

        /// <summary>C'e' un tentativo di aggancio ancora in volo.</summary>
        public bool IsConnecting => ensureTask is { IsCompleted: false };

        public event Action Reconnected;

        /// <summary>
        /// Restituisce il repository autoritativo, oppure null se non c'e' una sessione
        /// account attiva. Idempotente: chiamate concorrenti condividono lo stesso task.
        /// </summary>
        public Task<ServerSinglePlayerProgressRepository> EnsureRepositoryAsync()
        {
            if (IsReady)
                return Task.FromResult(repository);
            // Si condivide soltanto un tentativo ancora in volo. Un tentativo concluso non
            // va mai riusato ne' tenuto: quello fallito e' nato con una sessione che nel
            // frattempo puo' essere tornata, e riproporlo voleva dire rispondere "server
            // assente" per sempre - fino a un giro dall'arena, l'unica schermata che
            // rilascia il link. Era esattamente il motivo per cui la taverna si apriva
            // vuota e si sbloccava solo passando dal PvP.
            if (ensureTask is { IsCompleted: false })
                return ensureTask;

            Task<ServerSinglePlayerProgressRepository> attempt = ConnectAndAuthenticateAsync();
            // Un tentativo che si conclude subito (nessuna sessione da adottare) non lascia
            // niente dietro di se': il prossimo che chiede riparte pulito.
            ensureTask = attempt.IsCompleted ? null : attempt;
            return attempt;
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
                await AccountServerSession.WaitUntilReadyAsync(ReconnectWaitSeconds);

                if (!AccountServerSession.TryGet(
                        out PvpServerClient sharedClient,
                        out PvpServerMessageDispatcher sharedDispatcher,
                        out _))
                {
                    // Nessuna sessione: non si crea piu' un account ospite legato al
                    // dispositivo. Gli account nascono solo dal login Google, quindi
                    // senza sessione la progressione resta locale.
                    return null;
                }

                client = sharedClient;
                // Il repository si ricostruisce solo se manca o se sotto e' cambiata la
                // sessione. Rifarlo a ogni caduta di rete vorrebbe dire ripagare a ogni
                // schermata il replay dell'outbox e una RefreshAsync in piu', quando il
                // socket e' semplicemente tornato quello di prima.
                if (repository == null || !ReferenceEquals(dispatcher, sharedDispatcher))
                {
                    dispatcher = sharedDispatcher;
                    progressClient = new ServerSinglePlayerProgressClient(dispatcher);
                    await progressClient.PendingMutationsReplayed;
                    repository = new ServerSinglePlayerProgressRepository(progressClient);
                    await repository.RefreshAsync();
                }
                authenticated = true;
                return repository;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SP] Connessione progressione non riuscita: {exception.Message}");
                return null;
            }
        }

        private void Update()
        {
            dispatcher?.Pump();
        }

        private async void HandleAccountReconnected()
        {
            try
            {
                // Il primo tentativo dell'Hub puo' avvenire prima che la sessione account
                // sia pronta. In quel caso non esiste ancora un repository: la notifica di
                // riconnessione deve crearlo, non essere ignorata, altrimenti il banner
                // continua a mostrare per tutta la sessione i valori locali predefiniti.
                if (repository == null)
                {
                    // EnsureRepositoryAsync costruisce il client, che rigioca l'outbox da se'.
                    if (await EnsureRepositoryAsync() == null)
                        return;
                }
                else
                {
                    // Prima le mutazioni rimaste su disco, poi la fotografia: al contrario
                    // il banner mostrerebbe uno stato vecchio di una reward appena versata.
                    if (progressClient != null)
                        await progressClient.ReplayPendingMutationsAsync();
                    await repository.RefreshAsync();
                }

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
            progressClient = null;
            ensureTask = null;
            client = null;
            dispatcher = null;
        }

        private void OnDestroy() => Shutdown();
    }
}
