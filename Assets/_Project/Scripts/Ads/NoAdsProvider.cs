using System.Threading.Tasks;

namespace AccardND.Ads
{
    /// <summary>
    /// La rete che non ha mai niente da mostrare. E' quello che gira nelle build vere finche'
    /// non c'e' un SDK collegato, ed e' voluto: meglio nessuna pubblicita' che una pubblicita'
    /// finta addosso a un giocatore.
    ///
    /// Non e' codice morto neanche dopo AdMob: sulle piattaforme dove la rete scelta non
    /// esiste (oggi il web, finche' l'adattatore H5 non c'e') questo resta il provider giusto.
    /// </summary>
    public sealed class NoAdsProvider : IAdProvider
    {
        public string ProviderId => "none";

        public Task<bool> InitializeAsync() => Task.FromResult(false);

        public bool IsReady(AdPlacement placement) => false;

        public void Warm(AdPlacement placement)
        {
        }

        public void Cool(AdPlacement placement)
        {
        }

        public Task<AdResult> ShowAsync(AdPlacement placement, AdRewardContext context) =>
            Task.FromResult(AdResult.Of(AdOutcome.NoFill));
    }
}
