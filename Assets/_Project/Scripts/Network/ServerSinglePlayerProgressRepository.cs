using System;
using System.Threading.Tasks;
using AccardND.GameData;
using AccardND.NetProtocol;

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
        public int GetCounter(string key) => cache.GetCounter(key);

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

        /// <summary>
        /// Segnala al server che il boss finale di un capitolo e stato sconfitto: il server
        /// segna il capitolo completato e concede quello successivo. La cache viene sostituita
        /// col nuovo stato autoritativo.
        /// </summary>
        public async Task ClearChapterAsync(string bossId)
        {
            SinglePlayerProgressSave snapshot = await server.ClearChapterAsync(bossId);
            cache.ApplyAuthoritative(snapshot);
            IsSynced = true;
        }

        /// <summary>
        /// Catalogo del Santuario valutato dal server. Non tocca la cache: e' una fotografia
        /// per la UI, mentre lo stato autoritativo continua ad arrivare dagli altri messaggi.
        /// </summary>
        public async Task<SanctuaryData> GetSanctuaryAsync()
        {
            SanctuaryData data = await server.GetSanctuaryAsync();
            IsSynced = true;
            return data;
        }

        /// <summary>
        /// Compra un consumabile. Il miele cambia lato server, quindi la cache di
        /// progressione va poi riallineata con <see cref="RefreshAsync"/>.
        /// </summary>
        public async Task<SanctuaryData> BuySanctuaryItemAsync(string itemId, string offerId = null)
        {
            SanctuaryData data = await server.BuySanctuaryItemAsync(itemId, offerId);
            IsSynced = true;
            return data;
        }

        /// <summary>
        /// Entitlement degli acquisti a valuta reale. Come il Santuario, e' una fotografia
        /// per la UI: gli sblocchi veri arrivano dalla progressione autoritativa.
        /// </summary>
        public async Task<IapEntitlementsData> GetEntitlementsAsync()
        {
            IapEntitlementsData data = await server.GetEntitlementsAsync();
            IsSynced = true;
            return data;
        }

        /// <summary>
        /// Riscatta una ricevuta. Se il server concede, gli sblocchi sono gia' applicati
        /// lato server: la cache di progressione va riallineata dal chiamante.
        /// </summary>
        public async Task<IapRedeemResult> RedeemPurchaseAsync(string productId, string receipt)
        {
            IapRedeemResult result = await server.RedeemPurchaseAsync(productId, receipt);
            IsSynced = true;
            return result;
        }

        /// <summary>Sostituisce la bisaccia scelta per la prossima run.</summary>
        public async Task<SanctuaryData> SetSanctuaryBagAsync(string[] itemIds)
        {
            SanctuaryData data = await server.SetSanctuaryBagAsync(itemIds);
            IsSynced = true;
            return data;
        }

        /// <summary>
        /// L'albero dei talenti valutato dal server. Come il Santuario non tocca la cache:
        /// e' una fotografia per la UI.
        /// </summary>
        public async Task<TalentData> GetTalentsAsync()
        {
            TalentData data = await server.GetTalentsAsync();
            IsSynced = true;
            return data;
        }

        /// <summary>
        /// Compra un rango di talento. Spende punti e cambia il pacchetto modificatori,
        /// quindi la cache di progressione va poi riallineata con <see cref="RefreshAsync"/>.
        /// </summary>
        public async Task<TalentData> BuyTalentAsync(string talentId)
        {
            TalentData data = await server.BuyTalentAsync(talentId);
            IsSynced = true;
            return data;
        }

        /// <summary>
        /// Bacheca della taverna valutata dal server. Come il Santuario non tocca la cache:
        /// e' una fotografia per la UI.
        /// </summary>
        public async Task<TavernData> GetTavernAsync()
        {
            TavernData data = await server.GetTavernAsync();
            IsSynced = true;
            return data;
        }

        /// <summary>
        /// Riscuote una quest della taverna. Concede miele, quindi la cache di progressione
        /// va poi riallineata con <see cref="RefreshAsync"/>.
        /// </summary>
        public async Task<TavernData> ClaimTavernQuestAsync(string questId, int rewardMultiplier = 1)
        {
            TavernData data = await server.ClaimTavernQuestAsync(questId, rewardMultiplier);
            IsSynced = true;
            return data;
        }

        /// <summary>Riscuote il premio di giornata. Vale la stessa nota sul miele.</summary>
        public async Task<TavernData> ClaimTavernBonusAsync()
        {
            TavernData data = await server.ClaimTavernBonusAsync();
            IsSynced = true;
            return data;
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
        /// Chiude un modulo del tutorial progressivo. Cosa concede lo decide il catalogo del
        /// server: qui si manda solo l'id del modulo e si rispecchia lo stato che torna.
        /// </summary>
        public async Task<SinglePlayerRewardOutcome> ClaimTutorialModuleRewardAsync(
            string moduleId, string moduleRunId)
        {
            SinglePlayerRewardOutcome outcome =
                await server.ClaimTutorialModuleRewardAsync(moduleId, moduleRunId);
            cache.ApplyAuthoritative(outcome.Progress);
            IsSynced = true;
            return outcome;
        }

        /// <summary>
        /// Segnala l'inizio di una run di campagna. Non tocca la cache: non e' progressione,
        /// e' la riga dello storico che la reward di fine run andra' a chiudere. Non lancia:
        /// una run non deve fermarsi perche' il server non ha preso nota dell'avvio.
        /// </summary>
        public async Task NotifyRunStartedAsync(string runId, string mode, string chapterId, string stageId)
        {
            try
            {
                await server.NotifyRunStartedAsync(runId, mode, chapterId, stageId);
                IsSynced = true;
            }
            catch (Exception)
            {
                IsSynced = false;
            }
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

        /// <summary>
        /// Le reward che aspettano ancora il video del triplicatore. Come Santuario e taverna
        /// non tocca la cache: e' una fotografia per la UI del profilo.
        /// </summary>
        public async Task<SinglePlayerPendingAdRewardsData> GetPendingAdRewardsAsync()
        {
            SinglePlayerPendingAdRewardsData data = await server.GetPendingAdRewardsAsync();
            IsSynced = true;
            return data;
        }

        public async Task DismissPendingAdRewardAsync(string rewardClaimId)
        {
            await server.DismissPendingAdRewardAsync(rewardClaimId);
            IsSynced = true;
        }

        /// <summary>Applica il triplicatore pubblicitario a una reward gia concessa.</summary>
        public async Task<SinglePlayerRewardOutcome> ClaimAdMultiplierAsync(string rewardClaimId, string adImpressionId)
        {
            SinglePlayerRewardOutcome outcome = await server.ClaimAdMultiplierAsync(rewardClaimId, adImpressionId);
            cache.ApplyAuthoritative(outcome.Progress);
            IsSynced = true;
            return outcome;
        }

        public async Task<SinglePlayerRewardOutcome> ClaimLevelRewardsAsync()
        {
            SinglePlayerRewardOutcome outcome = await server.ClaimLevelRewardsAsync();
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
        public int AddAccountExperience(int amount) => throw ServerAuthorityViolation();
        public bool TrySpendHoney(int amount) => throw ServerAuthorityViolation();
        public void SetTutorialCompleted(bool completed = true) => throw ServerAuthorityViolation();
        public void SetHardcoreUnlocked(bool unlocked = true) => throw ServerAuthorityViolation();
        public void Unlock(SinglePlayerUnlockType type, string id) => throw ServerAuthorityViolation();

        private static NotSupportedException ServerAuthorityViolation() => new NotSupportedException(
            "Progressione autoritativa lato server: usa RefreshAsync/PurchaseUnlockAsync, "
            + "non i mutatori locali di miele/unlock.");
    }
}
