using System;
using System.Threading.Tasks;
using AccardND.GameData;

namespace AccardND.Network
{
    /// <summary>
    /// Repository di progressione single player con il server come fonte autoritativa.
    /// Le letture sincrone (usate dalla UI) provengono da una cache locale che contiene
    /// l'ultima istantanea nota; ogni mutazione passa dal server e, in caso di successo,
    /// sostituisce la cache con il nuovo stato autoritativo.
    ///
    /// I mutatori locali dell'interfaccia lanciano di proposito: in modalità autoritativa
    /// il client non può modificare miele/unlock/tutorial da solo. Chi la usa deve chiamare
    /// <see cref="RefreshAsync"/> e <see cref="PurchaseUnlockAsync"/>.
    /// </summary>
    public sealed class ServerSinglePlayerProgressRepository : ISinglePlayerProgressRepository
    {
        private readonly IServerSinglePlayerProgressClient server;
        private readonly ISinglePlayerProgressRepository cache;

        public ServerSinglePlayerProgressRepository(IServerSinglePlayerProgressClient server)
            : this(server, new LocalSinglePlayerProgressRepository())
        {
        }

        public ServerSinglePlayerProgressRepository(
            IServerSinglePlayerProgressClient server,
            ISinglePlayerProgressRepository cache)
        {
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>True se l'ultima comunicazione col server è andata a buon fine.</summary>
        public bool IsSynced { get; private set; }

        // --- Letture: cache locale (ultima istantanea autoritativa nota) ---
        public SinglePlayerProgressSave Progress => cache.Progress;
        public int Honey => cache.Honey;
        public bool TutorialCompleted => cache.TutorialCompleted;
        public bool HardcoreUnlocked => cache.HardcoreUnlocked;
        public bool IsUnlocked(SinglePlayerUnlockType type, string id) => cache.IsUnlocked(type, id);

        /// <summary>
        /// Carica lo stato autoritativo dal server e aggiorna la cache locale.
        /// Restituisce false se il server non è raggiungibile: in tal caso la cache
        /// (l'ultima istantanea nota) viene conservata per l'uso offline.
        /// </summary>
        public async Task<bool> RefreshAsync()
        {
            try
            {
                SinglePlayerProgressSave snapshot = await server.LoadProgressAsync();
                cache.ApplyAuthoritative(snapshot);
                IsSynced = true;
                return true;
            }
            catch (Exception)
            {
                IsSynced = false;
                return false;
            }
        }

        /// <summary>
        /// Chiede al server l'acquisto di un unlock. In caso di successo la cache viene
        /// sostituita con il nuovo stato autoritativo. Se il server rifiuta (miele
        /// insufficiente, unlock non valido, offline) l'eccezione viene propagata così che
        /// il chiamante possa mostrarne il messaggio; la cache resta invariata.
        /// </summary>
        public async Task PurchaseUnlockAsync(SinglePlayerUnlockType type, string id)
        {
            SinglePlayerProgressSave snapshot = await server.PurchaseUnlockAsync(type, id);
            cache.ApplyAuthoritative(snapshot);
            IsSynced = true;
        }

        /// <summary>Acquista lo sblocco della modalita Hardcore (flag server "mode"/"hardcore").</summary>
        public async Task PurchaseHardcoreAsync()
        {
            SinglePlayerProgressSave snapshot = await server.PurchaseHardcoreAsync();
            cache.ApplyAuthoritative(snapshot);
            IsSynced = true;
        }

        /// <summary>
        /// Riscatta la ricompensa di completamento tutorial (importo deciso dal server,
        /// idempotente). Aggiorna la cache col nuovo stato e restituisce l'esito.
        /// </summary>
        public async Task<SinglePlayerRewardOutcome> ClaimTutorialRewardAsync(string tutorialRunId)
        {
            SinglePlayerRewardOutcome outcome = await server.ClaimTutorialRewardAsync(tutorialRunId);
            cache.ApplyAuthoritative(outcome.Progress);
            IsSynced = true;
            return outcome;
        }

        /// <summary>
        /// Riscatta la ricompensa alla morte: il server calcola il miele dal sommario (con cap)
        /// e restituisce anche il rewardClaimId, da passare a <see cref="ClaimAdMultiplierAsync"/>
        /// se il player guarda la pubblicita per triplicare.
        /// </summary>
        public async Task<SinglePlayerRewardOutcome> ClaimDeathRewardAsync(DeathRewardSummary summary)
        {
            SinglePlayerRewardOutcome outcome = await server.ClaimDeathRewardAsync(summary);
            cache.ApplyAuthoritative(outcome.Progress);
            IsSynced = true;
            return outcome;
        }

        /// <summary>Applica il triplicatore pubblicitario a una reward gia concessa.</summary>
        public async Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId)
        {
            SinglePlayerRewardOutcome outcome = await server.ClaimAdMultiplierAsync(rewardClaimId, adImpressionId);
            cache.ApplyAuthoritative(outcome.Progress);
            IsSynced = true;
            return outcome;
        }

        public void ApplyAuthoritative(SinglePlayerProgressSave snapshot)
        {
            cache.ApplyAuthoritative(snapshot);
            IsSynced = true;
        }

        public void Clear()
        {
            cache.Clear();
            IsSynced = false;
        }

        // --- Mutatori locali non consentiti: la progressione è autoritativa sul server ---
        public void AddHoney(int amount) => throw ServerAuthorityViolation();
        public bool TrySpendHoney(int amount) => throw ServerAuthorityViolation();
        public void SetTutorialCompleted(bool completed = true) => throw ServerAuthorityViolation();
        public void SetHardcoreUnlocked(bool unlocked = true) => throw ServerAuthorityViolation();
        public void Unlock(SinglePlayerUnlockType type, string id) => throw ServerAuthorityViolation();

        private static NotSupportedException ServerAuthorityViolation() => new NotSupportedException(
            "Progressione autoritativa lato server: usa RefreshAsync/PurchaseUnlockAsync, "
            + "non i mutatori locali di miele/unlock.");
    }
}
