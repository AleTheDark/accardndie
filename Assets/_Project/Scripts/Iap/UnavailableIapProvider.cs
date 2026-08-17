using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AccardND.Iap
{
    /// <summary>
    /// Nessuno store: web, PWA, editor, build desktop. Le tile premium restano visibili -
    /// chi gioca da browser deve poter vedere cosa c'e' nell'app Android, e chi ha gia'
    /// comprato deve vedere i suoi acquisti - ma non si compra niente da qui.
    /// </summary>
    public sealed class UnavailableIapProvider : IIapProvider
    {
        private readonly List<IapOffer> offers = new();

        public UnavailableIapProvider(string providerId)
        {
            ProviderId = providerId;
            foreach (IapProduct product in IapProducts.All)
                offers.Add(new IapOffer(product, IapProducts.FallbackPrice(product), purchasable: false));
        }

        public string ProviderId { get; }

        public bool IsAvailable => false;

        public IReadOnlyList<IapOffer> Offers => offers;

        public Task<bool> InitializeAsync() => Task.FromResult(false);

        public Task<IapPurchaseResult> PurchaseAsync(IapProduct product) =>
            Task.FromResult(IapPurchaseResult.Unavailable("Acquisto disponibile solo nell'app Android."));

        public Task<IReadOnlyList<IapReceipt>> FetchOwnedAsync() =>
            Task.FromResult((IReadOnlyList<IapReceipt>)Array.Empty<IapReceipt>());

        public void Confirm(IapProduct product)
        {
        }
    }
}
