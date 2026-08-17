using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AccardND.Iap
{
    /// <summary>
    /// Il punto da cui il gioco chiede un acquisto a valuta reale. Ha la stessa forma di
    /// AdService: sopra c'e' la UI, che conosce solo <see cref="IapProduct"/>; sotto c'e' un
    /// <see cref="IIapProvider"/>, che conosce un SDK.
    ///
    /// Quello che questo strato NON fa e' altrettanto importante: non decide cosa possiede il
    /// giocatore. Gli entitlement li dice il server dopo aver verificato la ricevuta - qui si
    /// tiene solo l'ultima risposta ricevuta, per disegnare la UI e spegnere la pubblicita'.
    /// Un client che si dichiara proprietario di qualcosa non sblocca niente.
    /// </summary>
    public static class IapService
    {
        private static IIapProvider provider;
        private static Task<bool> initialization;
        private static IapEntitlements entitlements = IapEntitlements.None;

        /// <summary>Aggancio per il diario di gioco (AppendLog). Facoltativo.</summary>
        public static Action<string> Log;

        /// <summary>
        /// E' arrivata una ricevuta che nessuno aveva chiesto: un acquisto interrotto in una
        /// sessione precedente, o comprato su un altro dispositivo. Chi ascolta la manda al
        /// server, che concede quello che manca.
        /// </summary>
        public static event Action<IapReceipt> PurchaseRecovered;

        /// <summary>Gli entitlement sono cambiati: la UI si ridisegna, la pubblicita' si spegne.</summary>
        public static event Action EntitlementsChanged;

        public static string ActiveProviderId => provider?.ProviderId ?? "nessuno";

        /// <summary>Su questa piattaforma si puo' comprare.</summary>
        public static bool IsStoreAvailable => provider != null && provider.IsAvailable;

        /// <summary>L'ultima verita' arrivata dal server.</summary>
        public static IapEntitlements Entitlements => entitlements;

        public static IReadOnlyList<IapOffer> Offers =>
            provider?.Offers ?? Array.Empty<IapOffer>();

        /// <summary>
        /// Prepara lo store. Si puo' chiamare quante volte si vuole: la connessione si fa una
        /// volta sola e le chiamate successive aspettano la stessa.
        /// </summary>
        public static Task<bool> InitializeAsync()
        {
            if (initialization != null)
                return initialization;
            provider ??= CreateProvider();
            initialization = provider.InitializeAsync();
            return initialization;
        }

        /// <summary>
        /// Apre il pagamento. La ricevuta che torna non ha ancora sbloccato niente: va passata
        /// al server, e solo se il server concede si chiama <see cref="Confirm"/>.
        /// </summary>
        public static async Task<IapPurchaseResult> PurchaseAsync(IapProduct product)
        {
            if (!await InitializeAsync())
                return provider?.IsAvailable == true
                    ? IapPurchaseResult.Failed("Lo store non risponde.")
                    : IapPurchaseResult.Unavailable("Acquisto disponibile solo nell'app Android.");
            try
            {
                return await provider.PurchaseAsync(product);
            }
            catch (Exception exception)
            {
                Log?.Invoke("IAP - acquisto fallito: " + exception.Message);
                return IapPurchaseResult.Failed(exception.Message);
            }
        }

        /// <summary>Ricevute gia' possedute da questo account Google, per il ripristino.</summary>
        public static async Task<IReadOnlyList<IapReceipt>> FetchOwnedAsync()
        {
            if (!await InitializeAsync())
                return Array.Empty<IapReceipt>();
            return await provider.FetchOwnedAsync();
        }

        /// <summary>Da chiamare solo dopo che il server ha concesso lo sblocco.</summary>
        public static void Confirm(IapProduct product) => provider?.Confirm(product);

        /// <summary>La risposta del server: e' l'unica sorgente di verita' sul posseduto.</summary>
        public static void ApplyEntitlements(IapEntitlements value)
        {
            IapEntitlements next = value ?? IapEntitlements.None;
            if (entitlements.Equals(next))
                return;
            entitlements = next;
            EntitlementsChanged?.Invoke();
        }

        internal static void NotifyRecoveredPurchase(IapReceipt receipt)
        {
            if (receipt != null && receipt.IsUsable)
                PurchaseRecovered?.Invoke(receipt);
        }

        private static IIapProvider CreateProvider()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new GooglePlayIapProvider();
#else
            return new UnavailableIapProvider(Application.isEditor ? "editor" : "nessuno_store");
#endif
        }
    }

    /// <summary>
    /// Cosa possiede l'account, come lo racconta il server. E' un valore, non uno stato che
    /// qualcuno modifica un pezzo alla volta: arriva intero a ogni risposta.
    /// </summary>
    public sealed class IapEntitlements : IEquatable<IapEntitlements>
    {
        public static readonly IapEntitlements None = new(false, false, false);

        public IapEntitlements(bool noAds, bool allClasses, bool allSupreme)
        {
            NoAds = noAds;
            AllClasses = allClasses;
            AllSupreme = allSupreme;
        }

        public bool NoAds { get; }
        public bool AllClasses { get; }
        public bool AllSupreme { get; }

        /// <summary>
        /// Il prodotto e' gia' coperto da quello che l'account possiede. Il pacchetto
        /// classi+supreme e' coperto solo se ci sono entrambe: chi ha solo le classi lo vede
        /// ancora acquistabile (o meglio, vede l'upgrade).
        /// </summary>
        public bool Owns(IapProduct product)
        {
            switch (product)
            {
                case IapProduct.NoAds: return NoAds;
                case IapProduct.Classes: return AllClasses;
                case IapProduct.ClassesSupreme: return AllClasses && AllSupreme;
                case IapProduct.SupremeUpgrade: return AllSupreme;
                default: return false;
            }
        }

        /// <summary>
        /// L'upgrade "solo supreme" ha senso di esistere soltanto per chi ha gia' le classi:
        /// a chiunque altro il negozio mostra i due pacchetti interi.
        /// </summary>
        public bool ShowsSupremeUpgrade => AllClasses && !AllSupreme;

        public bool Equals(IapEntitlements other) =>
            other != null && NoAds == other.NoAds && AllClasses == other.AllClasses && AllSupreme == other.AllSupreme;

        public override bool Equals(object obj) => Equals(obj as IapEntitlements);

        public override int GetHashCode() =>
            (NoAds ? 1 : 0) | (AllClasses ? 2 : 0) | (AllSupreme ? 4 : 0);
    }
}
