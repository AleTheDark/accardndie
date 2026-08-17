namespace AccardND.NetProtocol
{
    /// <summary>
    /// Gli id dei prodotti a valuta reale e cosa concede ciascuno. Sta in NetProtocol perche'
    /// client e server devono leggere la stessa lista: il client la usa per chiedere il
    /// prodotto allo store, il server per decidere cosa sbloccare quando la ricevuta e' buona.
    /// Un id qui deve coincidere carattere per carattere con quello su Play Console.
    /// </summary>
    public static class IapCatalog
    {
        public const string NoAdsId = "no_ads";
        public const string ClassesId = "all_classes";
        public const string ClassesSupremeId = "all_classes_supreme";
        public const string SupremeUpgradeId = "supreme_upgrade";

        public static readonly string[] All =
        {
            NoAdsId, ClassesId, ClassesSupremeId, SupremeUpgradeId
        };

        public static bool IsKnown(string productId)
        {
            for (int index = 0; index < All.Length; index++)
                if (All[index] == productId)
                    return true;
            return false;
        }

        /// <summary>Sblocca tutte le classi del Santuario.</summary>
        public static bool GrantsClasses(string productId) =>
            productId == ClassesId || productId == ClassesSupremeId;

        /// <summary>Sblocca tutte le abilita' supreme.</summary>
        public static bool GrantsSupreme(string productId) =>
            productId == ClassesSupremeId || productId == SupremeUpgradeId;

        /// <summary>Spegne la pubblicita' e condona i cancelli pubblicitari.</summary>
        public static bool GrantsNoAds(string productId) => productId == NoAdsId;
    }
}
