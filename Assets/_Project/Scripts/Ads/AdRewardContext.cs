namespace AccardND.Ads
{
    /// <summary>
    /// Quello che la rete pubblicitaria deve riferire al nostro server quando il video e'
    /// stato guardato davvero.
    ///
    /// E' il pezzo che rende utile la verifica lato server (SSV): AdMob chiama un nostro
    /// endpoint a video completato e ci rimanda dentro questi due campi. Senza, la chiamata
    /// arriverebbe senza sapere a quale giocatore e a quale ricompensa si riferisce, e
    /// sarebbe carta straccia.
    ///
    /// Vale solo per i rewarded: un interstitial non paga niente, quindi non ha nessun
    /// contesto da trasportare.
    /// </summary>
    public readonly struct AdRewardContext
    {
        /// <summary>Il giocatore, come lo conosce il nostro server.</summary>
        public readonly string UserId;

        /// <summary>
        /// La ricompensa a cui l'annuncio si riferisce: e' il <c>rewardClaimId</c> che il
        /// server ha registrato a fine run o fine partita. Torna indietro nella callback SSV
        /// come <c>custom_data</c>, ed e' quello che permette al server di accreditare il
        /// moltiplicatore senza credere a una parola del client.
        /// </summary>
        public readonly string CustomData;

        public AdRewardContext(string userId, string customData)
        {
            UserId = userId ?? string.Empty;
            CustomData = customData ?? string.Empty;
        }

        /// <summary>Contesto vuoto: e' il caso degli interstitial.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(UserId) && string.IsNullOrEmpty(CustomData);

        /// <summary>Scorciatoia per il caso piu' comune: so a quale ricompensa punto, non chi la incassa.</summary>
        public static AdRewardContext ForClaim(string rewardClaimId) =>
            new AdRewardContext(null, rewardClaimId);
    }
}
