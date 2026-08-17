using System.Collections.Generic;

namespace AccardND.Iap
{
    /// <summary>Com'e' finita una richiesta d'acquisto.</summary>
    public enum IapOutcome
    {
        /// <summary>Lo store ha accettato il pagamento: c'e' una ricevuta da far validare.</summary>
        Purchased,

        /// <summary>Il giocatore ha chiuso il dialogo. Non e' un errore, non si mostra un allarme.</summary>
        Cancelled,

        /// <summary>Lo store ha rifiutato o e' andato storto qualcosa.</summary>
        Failed,

        /// <summary>Non c'e' nessuno store su questa piattaforma (web, editor, build senza IAP).</summary>
        Unavailable,

        /// <summary>
        /// Pagamento differito (approvazione di un genitore, contanti in cartoleria). Non c'e'
        /// niente da concedere adesso: arrivera' da solo al prossimo avvio.
        /// </summary>
        Deferred
    }

    /// <summary>
    /// Una ricevuta da mandare al server. Non concediamo niente sulla parola dello store:
    /// il contenuto di <see cref="Receipt"/> viene verificato sul server prima di sbloccare.
    /// </summary>
    public sealed class IapReceipt
    {
        public IapReceipt(IapProduct product, string receipt)
        {
            Product = product;
            Receipt = receipt ?? string.Empty;
        }

        public IapProduct Product { get; }

        /// <summary>Ricevuta unificata di Unity IAP, in JSON (Store / TransactionID / Payload).</summary>
        public string Receipt { get; }

        public bool IsUsable => !string.IsNullOrEmpty(Receipt);
    }

    public sealed class IapPurchaseResult
    {
        private IapPurchaseResult(IapOutcome outcome, IapReceipt receipt, string message)
        {
            Outcome = outcome;
            Receipt = receipt;
            Message = message ?? string.Empty;
        }

        public IapOutcome Outcome { get; }
        public IapReceipt Receipt { get; }
        public string Message { get; }

        public bool HasReceipt => Outcome == IapOutcome.Purchased && Receipt != null && Receipt.IsUsable;

        public static IapPurchaseResult Purchased(IapReceipt receipt) =>
            new(IapOutcome.Purchased, receipt, string.Empty);

        public static IapPurchaseResult Cancelled() =>
            new(IapOutcome.Cancelled, null, string.Empty);

        public static IapPurchaseResult Failed(string message) =>
            new(IapOutcome.Failed, null, message);

        public static IapPurchaseResult Unavailable(string message) =>
            new(IapOutcome.Unavailable, null, message);

        public static IapPurchaseResult Deferred() =>
            new(IapOutcome.Deferred, null, string.Empty);
    }

    /// <summary>Prodotto come lo conosce lo store adesso: prezzo locale e acquistabilita'.</summary>
    public sealed class IapOffer
    {
        public IapOffer(IapProduct product, string price, bool purchasable)
        {
            Product = product;
            Price = string.IsNullOrEmpty(price) ? IapProducts.FallbackPrice(product) : price;
            Purchasable = purchasable;
        }

        public IapProduct Product { get; }

        /// <summary>Prezzo gia' localizzato dallo store, o il segnaposto se non ha risposto.</summary>
        public string Price { get; }

        /// <summary>Lo store e' pronto e il prodotto esiste: si puo' premere il pulsante.</summary>
        public bool Purchasable { get; }
    }

    public static class IapOfferExtensions
    {
        public static IapOffer Find(this IReadOnlyList<IapOffer> offers, IapProduct product)
        {
            if (offers == null)
                return null;
            for (int index = 0; index < offers.Count; index++)
                if (offers[index].Product == product)
                    return offers[index];
            return null;
        }
    }
}
