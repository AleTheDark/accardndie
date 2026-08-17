namespace AccardND.Iap
{
    /// <summary>
    /// Le voci a valuta reale. Sono poche e non cambiano a runtime: il catalogo vero vive
    /// su Play Console, ma gli id devono coincidere carattere per carattere, quindi stanno
    /// qui in chiaro e non in un file di configurazione che qualcuno puo' disallineare.
    /// </summary>
    public enum IapProduct
    {
        /// <summary>Niente pubblicita' e nessun cancello pubblicitario sulle ricompense.</summary>
        NoAds,

        /// <summary>Tutte le classi del Santuario.</summary>
        Classes,

        /// <summary>Tutte le classi piu' tutte le abilita' supreme.</summary>
        ClassesSupreme,

        /// <summary>
        /// Le sole supreme, per chi ha gia' comprato le classi. Google non ha un percorso di
        /// upgrade per i prodotti una tantum: senza questo prodotto chi ha gia' speso 9,99
        /// dovrebbe ricomprare il pacchetto intero per avere le supreme.
        /// </summary>
        SupremeUpgrade
    }

    public static class IapProducts
    {
        // Gli id veri stanno in NetProtocol: li legge anche il server quando decide cosa
        // sbloccare, e due liste separate sarebbero due liste che prima o poi divergono.
        public const string NoAdsId = AccardND.NetProtocol.IapCatalog.NoAdsId;
        public const string ClassesId = AccardND.NetProtocol.IapCatalog.ClassesId;
        public const string ClassesSupremeId = AccardND.NetProtocol.IapCatalog.ClassesSupremeId;
        public const string SupremeUpgradeId = AccardND.NetProtocol.IapCatalog.SupremeUpgradeId;

        public static readonly IapProduct[] All =
        {
            IapProduct.NoAds, IapProduct.Classes, IapProduct.ClassesSupreme, IapProduct.SupremeUpgrade
        };

        /// <summary>Id del prodotto su Play Console. Cambiarlo significa creare un altro prodotto.</summary>
        public static string IdOf(IapProduct product)
        {
            switch (product)
            {
                case IapProduct.NoAds: return NoAdsId;
                case IapProduct.Classes: return ClassesId;
                case IapProduct.ClassesSupreme: return ClassesSupremeId;
                case IapProduct.SupremeUpgrade: return SupremeUpgradeId;
                default: return string.Empty;
            }
        }

        public static bool TryParse(string productId, out IapProduct product)
        {
            foreach (IapProduct candidate in All)
            {
                if (IdOf(candidate) == productId)
                {
                    product = candidate;
                    return true;
                }
            }
            product = IapProduct.NoAds;
            return false;
        }

        /// <summary>
        /// Prezzo mostrato finche' lo store non ha risposto, e su web dove non risponde mai.
        /// Quello vero arriva da Google gia' convertito nella valuta del giocatore: questo e'
        /// solo un segnaposto onesto per l'Italia, non una promessa di prezzo.
        /// </summary>
        public static string FallbackPrice(IapProduct product)
        {
            switch (product)
            {
                case IapProduct.NoAds: return "2,99 €";
                case IapProduct.Classes: return "9,99 €";
                case IapProduct.ClassesSupreme: return "14,99 €";
                case IapProduct.SupremeUpgrade: return "4,99 €";
                default: return string.Empty;
            }
        }
    }
}
