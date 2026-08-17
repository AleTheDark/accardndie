using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace AccardND.Iap
{
    /// <summary>
    /// Google Play Billing attraverso Unity IAP 5. Tutto quello che sa di ordini, ricevute e
    /// callback dell'SDK sta qui dentro: sopra si vedono solo <see cref="IapProduct"/> e
    /// ricevute da far validare.
    ///
    /// Una regola sola guida questo file: non si conferma un ordine finche' il server non ha
    /// concesso lo sblocco. Confermare prima significa dire a Google "consegnato" mentre il
    /// giocatore non ha ancora niente; non confermare mai significa farsi rimborsare
    /// l'acquisto dopo tre giorni. Il punto giusto e' <see cref="Confirm"/>, chiamato dal
    /// negozio dopo la risposta del server.
    /// </summary>
    public sealed class GooglePlayIapProvider : IIapProvider
    {
        private const int ConnectTimeoutMilliseconds = 25000;
        private const int PurchaseTimeoutMilliseconds = 300000;

        private readonly Dictionary<string, PendingOrder> pendingOrders = new();
        private readonly Dictionary<string, string> ownedReceipts = new();
        private readonly List<IapOffer> offers = new();

        private StoreController controller;
        private TaskCompletionSource<bool> connection;
        private TaskCompletionSource<bool> productsFetch;
        private TaskCompletionSource<IReadOnlyList<IapReceipt>> purchasesFetch;
        private TaskCompletionSource<IapPurchaseResult> purchaseInFlight;
        private string purchaseInFlightProductId;
        private bool initialized;

        public string ProviderId => "google_play";

        public bool IsAvailable => initialized;

        public IReadOnlyList<IapOffer> Offers => offers;

        public async Task<bool> InitializeAsync()
        {
            if (initialized)
                return true;
            try
            {
                controller = UnityIAPServices.StoreController();
                controller.OnStoreConnected += OnStoreConnected;
                controller.OnStoreDisconnected += OnStoreDisconnected;
                controller.OnProductsFetched += OnProductsFetched;
                controller.OnProductsFetchFailed += OnProductsFetchFailed;
                controller.OnPurchasePending += OnPurchasePending;
                controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
                controller.OnPurchaseFailed += OnPurchaseFailed;
                controller.OnPurchaseDeferred += OnPurchaseDeferred;
                controller.OnPurchasesFetched += OnPurchasesFetched;
                controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;

                connection = NewSource<bool>();
                productsFetch = NewSource<bool>();
                await controller.Connect();
                if (!await WithTimeout(connection.Task, ConnectTimeoutMilliseconds, false))
                {
                    IapService.Log?.Invoke("IAP - lo store non si e' connesso.");
                    return false;
                }

                if (!await WithTimeout(productsFetch.Task, ConnectTimeoutMilliseconds, false))
                {
                    IapService.Log?.Invoke("IAP - prezzi non arrivati dallo store.");
                    return false;
                }

                initialized = true;
                return true;
            }
            catch (Exception exception)
            {
                IapService.Log?.Invoke("IAP - inizializzazione fallita: " + exception.Message);
                return false;
            }
        }

        public async Task<IapPurchaseResult> PurchaseAsync(IapProduct product)
        {
            string productId = IapProducts.IdOf(product);
            if (!initialized || controller == null)
                return IapPurchaseResult.Unavailable("Lo store non e' pronto.");

            Product storeProduct = FindProduct(productId);
            if (storeProduct == null)
                return IapPurchaseResult.Unavailable("Prodotto non trovato sullo store.");

            // Gia' posseduto e mai riscattato: non riaprire il pagamento, ridai la ricevuta.
            if (ownedReceipts.TryGetValue(productId, out string owned) && !string.IsNullOrEmpty(owned))
                return IapPurchaseResult.Purchased(new IapReceipt(product, owned));

            try
            {
                purchaseInFlightProductId = productId;
                purchaseInFlight = NewSource<IapPurchaseResult>();
                controller.PurchaseProduct(storeProduct);
                IapPurchaseResult result = await WithTimeout(
                    purchaseInFlight.Task,
                    PurchaseTimeoutMilliseconds,
                    IapPurchaseResult.Failed("Lo store non ha risposto."));
                return result;
            }
            catch (Exception exception)
            {
                return IapPurchaseResult.Failed(exception.Message);
            }
            finally
            {
                purchaseInFlight = null;
                purchaseInFlightProductId = null;
            }
        }

        public async Task<IReadOnlyList<IapReceipt>> FetchOwnedAsync()
        {
            if (!initialized || controller == null)
                return Array.Empty<IapReceipt>();
            try
            {
                purchasesFetch = NewSource<IReadOnlyList<IapReceipt>>();
                controller.FetchPurchases();
                return await WithTimeout(
                    purchasesFetch.Task,
                    ConnectTimeoutMilliseconds,
                    (IReadOnlyList<IapReceipt>)Array.Empty<IapReceipt>());
            }
            catch (Exception exception)
            {
                IapService.Log?.Invoke("IAP - ripristino fallito: " + exception.Message);
                return Array.Empty<IapReceipt>();
            }
            finally
            {
                purchasesFetch = null;
            }
        }

        public void Confirm(IapProduct product)
        {
            string productId = IapProducts.IdOf(product);
            if (controller == null || !pendingOrders.TryGetValue(productId, out PendingOrder order))
                return;
            pendingOrders.Remove(productId);
            try
            {
                controller.ConfirmPurchase(order);
            }
            catch (Exception exception)
            {
                IapService.Log?.Invoke("IAP - conferma fallita: " + exception.Message);
            }
        }

        private void OnStoreConnected()
        {
            connection?.TrySetResult(true);
            List<ProductDefinition> definitions = new();
            foreach (IapProduct product in IapProducts.All)
                definitions.Add(new ProductDefinition(IapProducts.IdOf(product), ProductType.NonConsumable));
            controller.FetchProducts(definitions);
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            initialized = false;
            IapService.Log?.Invoke("IAP - store disconnesso: " + description?.message);
            connection?.TrySetResult(false);
        }

        private void OnProductsFetched(List<Product> products)
        {
            offers.Clear();
            foreach (IapProduct product in IapProducts.All)
            {
                Product storeProduct = FindProduct(IapProducts.IdOf(product), products);
                offers.Add(new IapOffer(
                    product,
                    storeProduct?.metadata?.localizedPriceString,
                    storeProduct != null));
            }
            productsFetch?.TrySetResult(true);
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            IapService.Log?.Invoke("IAP - prezzi non recuperati: " + failure?.FailureReason);
            productsFetch?.TrySetResult(false);
        }

        private void OnPurchasePending(PendingOrder order)
        {
            string productId = ProductIdOf(order);
            if (string.IsNullOrEmpty(productId))
                return;
            pendingOrders[productId] = order;
            string receipt = order.Info?.Receipt ?? string.Empty;
            ownedReceipts[productId] = receipt;
            if (!IapProducts.TryParse(productId, out IapProduct product))
                return;

            // Un pendente che non abbiamo chiesto noi e' un acquisto rimasto a meta' in una
            // sessione precedente: non lo si butta, lo si consegna al giro di ripristino.
            if (purchaseInFlight != null && purchaseInFlightProductId == productId)
                purchaseInFlight.TrySetResult(IapPurchaseResult.Purchased(new IapReceipt(product, receipt)));
            else
                IapService.NotifyRecoveredPurchase(new IapReceipt(product, receipt));
        }

        private void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
                IapService.Log?.Invoke("IAP - conferma rifiutata: " + failed.FailureReason);
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            string productId = ProductIdOf(order);
            if (purchaseInFlight == null || purchaseInFlightProductId != productId)
                return;
            if (order.FailureReason == PurchaseFailureReason.UserCancelled)
            {
                purchaseInFlight.TrySetResult(IapPurchaseResult.Cancelled());
                return;
            }
            if (order.FailureReason == PurchaseFailureReason.DuplicateTransaction)
            {
                // Google dice "gia' comprato": non e' un errore da mostrare, e' un ripristino.
                purchaseInFlight.TrySetResult(IapPurchaseResult.Failed("already_owned"));
                return;
            }
            purchaseInFlight.TrySetResult(IapPurchaseResult.Failed(order.FailureReason.ToString()));
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            if (purchaseInFlight != null && purchaseInFlightProductId == ProductIdOf(order))
                purchaseInFlight.TrySetResult(IapPurchaseResult.Deferred());
        }

        private void OnPurchasesFetched(Orders orders)
        {
            List<IapReceipt> receipts = new();
            if (orders != null)
            {
                CollectReceipts(orders.ConfirmedOrders, receipts);
                CollectReceipts(orders.PendingOrders, receipts);
            }
            purchasesFetch?.TrySetResult(receipts);
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription description)
        {
            IapService.Log?.Invoke("IAP - elenco acquisti non recuperato: " + description?.message);
            purchasesFetch?.TrySetResult(Array.Empty<IapReceipt>());
        }

        private void CollectReceipts<TOrder>(IReadOnlyList<TOrder> orders, List<IapReceipt> receipts)
            where TOrder : Order
        {
            if (orders == null)
                return;
            foreach (TOrder order in orders)
            {
                string productId = ProductIdOf(order);
                if (!IapProducts.TryParse(productId, out IapProduct product))
                    continue;
                string receipt = order.Info?.Receipt ?? string.Empty;
                if (string.IsNullOrEmpty(receipt))
                    continue;
                ownedReceipts[productId] = receipt;
                if (order is PendingOrder pending)
                    pendingOrders[productId] = pending;
                receipts.Add(new IapReceipt(product, receipt));
            }
        }

        private Product FindProduct(string productId, IReadOnlyList<Product> source = null)
        {
            if (string.IsNullOrEmpty(productId))
                return null;
            if (source == null)
            {
                if (controller == null)
                    return null;
                source = controller.GetProducts();
            }
            foreach (Product candidate in source)
                if (candidate?.definition?.id == productId)
                    return candidate;
            return null;
        }

        private static string ProductIdOf(Order order)
        {
            if (order?.CartOrdered == null)
                return string.Empty;
            foreach (CartItem item in order.CartOrdered.Items())
                if (item?.Product?.definition?.id != null)
                    return item.Product.definition.id;
            return string.Empty;
        }

        private static TaskCompletionSource<T> NewSource<T>() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Nessuna attesa dell'SDK e' infinita: se lo store tace, il negozio deve poter dire
        /// "riprova" invece di restare girando su una rotella per sempre.
        /// </summary>
        private static async Task<T> WithTimeout<T>(Task<T> task, int milliseconds, T fallback)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(milliseconds));
            return completed == (Task)task ? task.Result : fallback;
        }
    }
}
