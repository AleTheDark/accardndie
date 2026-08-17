using System;

namespace AccardND.NetProtocol
{
    /// <summary>
    /// Una ricevuta da riscattare. Il client non dice mai "ho comprato X": manda la ricevuta
    /// firmata dallo store e il server decide. Il productId serve solo a dare un messaggio
    /// d'errore sensato quando la ricevuta non e' verificabile: quello che vale davvero e'
    /// il prodotto scritto dentro la ricevuta.
    /// </summary>
    [Serializable]
    public sealed class IapRedeemRequest
    {
        public string productId;

        /// <summary>Ricevuta unificata di Unity IAP, in JSON (Store / TransactionID / Payload).</summary>
        public string receipt;
    }

    /// <summary>Cosa possiede l'account. E' la sola sorgente di verita' sugli acquisti.</summary>
    [Serializable]
    public sealed class IapEntitlementsData
    {
        public bool noAds;
        public bool allClasses;
        public bool allSupreme;

        /// <summary>Id dei prodotti riscattati, per disegnare le tile gia' possedute.</summary>
        public string[] productIds;
    }

    [Serializable]
    public sealed class IapRedeemResult
    {
        /// <summary>La ricevuta era valida e lo sblocco e' stato applicato (o lo era gia').</summary>
        public bool granted;

        public string productId;

        /// <summary>Chiave di testo per il client, mai una frase gia' localizzata.</summary>
        public string messageKey;

        public IapEntitlementsData entitlements;
    }
}
