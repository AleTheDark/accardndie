using System.Threading.Tasks;

namespace AccardND.Ads
{
    /// <summary>
    /// L'adattatore verso una rete pubblicitaria. E' l'unico punto che conosce un SDK:
    /// tutto il resto del gioco parla con <see cref="AdService"/> e non sa chi serve
    /// l'annuncio. Le implementazioni previste sono AdMob su Android, gli H5 Games Ads
    /// (o l'SDK di un portale) su WebGL, e le due finte che stanno in questa cartella.
    /// </summary>
    public interface IAdProvider
    {
        /// <summary>Nome corto della rete: entra nei log e, piu' avanti, nei claim lato server.</summary>
        string ProviderId { get; }

        /// <summary>
        /// Prepara l'SDK. Chiamata una volta sola da <see cref="AdService"/>, che aspetta
        /// l'esito prima del primo annuncio. Falso significa "questa rete non e' utilizzabile
        /// qui": il gioco prosegue senza pubblicita', non si blocca.
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// C'e' un annuncio caricato per questo placement. Serve a decidere se mostrare o no
        /// un bottone "guarda la pubblicita'": proporlo e poi non avere niente da far vedere
        /// e' peggio che non proporlo.
        /// </summary>
        bool IsReady(AdPlacement placement);

        /// <summary>
        /// Il giocatore sta entrando in un posto dove questo placement puo' scattare: prepara
        /// un annuncio, senza mostrarlo e senza far aspettare nessuno.
        ///
        /// E' l'unico modo in cui parte una richiesta ad AdMob, ed e' voluto. Caricare tutti i
        /// placement all'avvio costa una richiesta a testa per ogni sessione, comprese quelle
        /// dei posti dove il giocatore non mette piede: le reti misurano quante richieste
        /// diventano impressioni e strozzano chi chiede molto e mostra poco. In piu' un
        /// annuncio scade dopo circa un'ora, quindi quello caricato all'avvio spesso e' gia'
        /// da buttare quando servirebbe davvero.
        ///
        /// Va chiamata con un po' di anticipo sul momento dell'annuncio (l'apertura della
        /// schermata, l'inizio della run), perche' il caricamento richiede secondi e
        /// <see cref="IsReady"/> nel frattempo risponde no. Chiamarla due volte non raddoppia
        /// le richieste.
        /// </summary>
        void Warm(AdPlacement placement);

        /// <summary>
        /// Il giocatore e' uscito da quel posto: l'annuncio gia' caricato resta buono, ma dopo
        /// il prossimo show non se ne carica un altro. Senza questa, ogni annuncio mostrato ne
        /// lascia dietro uno pronto per un'occasione che non arrivera'.
        /// </summary>
        void Cool(AdPlacement placement);

        /// <summary>
        /// Mostra l'annuncio e restituisce l'esito quando si chiude. Non deve lanciare:
        /// un guasto della rete e' un <see cref="AdOutcome.Failed"/>, non un'eccezione che
        /// il gameplay deve intercettare.
        ///
        /// <paramref name="context"/> viaggia fino alla verifica lato server ed e' vuoto per
        /// gli interstitial, che non pagano niente.
        /// </summary>
        Task<AdResult> ShowAsync(AdPlacement placement, AdRewardContext context);
    }
}
