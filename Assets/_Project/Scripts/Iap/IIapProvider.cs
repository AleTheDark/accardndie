using System.Collections.Generic;
using System.Threading.Tasks;

namespace AccardND.Iap
{
    /// <summary>
    /// Un negozio di piattaforma. Sopra c'e' <see cref="IapService"/>, che conosce solo
    /// <see cref="IapProduct"/>; qui sotto ci sta l'SDK. Nessun metodo puo' lanciare: se
    /// lo store non risponde il gioco resta giocabile, semplicemente non si compra niente.
    /// </summary>
    public interface IIapProvider
    {
        /// <summary>Nome per i log e la schermata di diagnostica.</summary>
        string ProviderId { get; }

        /// <summary>Lo store esiste su questa piattaforma. Falso su web ed editor.</summary>
        bool IsAvailable { get; }

        /// <summary>Connette lo store e chiede i prezzi. Falso se non ci riesce.</summary>
        Task<bool> InitializeAsync();

        /// <summary>Prodotti come li conosce lo store adesso.</summary>
        IReadOnlyList<IapOffer> Offers { get; }

        /// <summary>
        /// Apre il dialogo di pagamento e aspetta l'esito. La ricevuta che torna non e'
        /// ancora spendibile: va validata sul server e poi confermata con
        /// <see cref="ConfirmAsync"/>, altrimenti Google rimborsa l'acquisto.
        /// </summary>
        Task<IapPurchaseResult> PurchaseAsync(IapProduct product);

        /// <summary>
        /// Acquisti gia' posseduti da questo account Google. E' il ripristino: serve al
        /// cambio dispositivo e a recuperare un acquisto interrotto a meta'.
        /// </summary>
        Task<IReadOnlyList<IapReceipt>> FetchOwnedAsync();

        /// <summary>
        /// Dice allo store che il contenuto e' stato consegnato. Da chiamare solo dopo che il
        /// server ha concesso lo sblocco: prima di questa chiamata l'ordine resta pendente e,
        /// se il gioco muore, ripartira' da solo al prossimo avvio.
        /// </summary>
        void Confirm(IapProduct product);
    }
}
