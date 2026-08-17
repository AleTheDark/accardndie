using System;

namespace AccardND.NetProtocol
{
    /// <summary>
    /// Una prova richiesta per uno sblocco, gia' valutata sul giocatore. Il server manda
    /// descrizione e progresso insieme: il client non deve conoscere le regole per mostrarli.
    /// </summary>
    [Serializable]
    public sealed class SanctuaryRequirementData
    {
        public string description;
        public int current;
        public int threshold;
        public bool met;
    }

    /// <summary>
    /// Una voce del Santuario (classe, tecnica) con il suo stato per questo giocatore.
    /// </summary>
    [Serializable]
    public sealed class SanctuaryEntryData
    {
        public string type;
        public string id;
        public string name;
        public string description;
        public int honeyCost;
        /// <summary>Prezzo di una singola copia nel catalogo del negozio.</summary>
        public int copyCost;

        /// <summary>Gia' posseduta: la voce si mostra come ottenuta.</summary>
        public bool owned;

        /// <summary>
        /// Acquistabile in linea di principio. False per le classi starter (si ottengono col
        /// tutorial) e per le tecniche, che restano visibili ma bloccate finche' gli effetti
        /// non sono definiti.
        /// </summary>
        public bool available;

        /// <summary>Tutte le prove sono soddisfatte. Non tiene conto del miele disponibile.</summary>
        public bool requirementsMet;

        public SanctuaryRequirementData[] requirements;
    }

    /// <summary>
    /// Catalogo del Santuario con i progressi del giocatore (risposta a sanctuary.get).
    /// Il catalogo vive sul server: il client lo disegna e basta.
    /// </summary>
    [Serializable]
    public sealed class SanctuaryData
    {
        public int honey;
        public SanctuaryEntryData[] entries;

        /// <summary>
        /// Catalogo completo dei consumabili acquistabili al negozio. Non sono sblocchi del
        /// Santuario: ogni voce e' disponibile a tutti fin dall'inizio.
        /// </summary>
        public SanctuaryEntryData[] shopCatalog;

        /// <summary>Slot bisaccia disponibili (base piu' quelli acquistati).</summary>
        public int bagSlots;

        /// <summary>Consumabili posseduti con la quantita'.</summary>
        public SanctuaryStashData[] stash;

        /// <summary>Id dei consumabili scelti per la prossima run.</summary>
        public string[] bag;

        /// <summary>Offerte autoritative correnti del negozio.</summary>
        public ShopOfferData[] shopOffers;
    }

    /// <summary>Quantita' posseduta di un consumabile.</summary>
    [Serializable]
    public sealed class SanctuaryStashData
    {
        public string itemId;
        public int count;
    }

    /// <summary>Acquisto di un consumabile (si somma alla scorta, non e' uno sblocco).</summary>
    [Serializable]
    public sealed class SanctuaryBuyItemRequest
    {
        public string itemId;
        /// <summary>Vuoto per il catalogo; valorizzato per applicare un'offerta.</summary>
        public string offerId;
    }

    [Serializable]
    public sealed class ShopOfferData
    {
        public string offerId;
        public string itemId;
        public int regularCost;
        public int offerCost;
        public int remaining;
        public int discountPercent;
    }

    /// <summary>
    /// Scelta della bisaccia per la prossima run. Sostituisce l'intera selezione: il server
    /// valida numero di slot, duplicati e possesso.
    /// </summary>
    [Serializable]
    public sealed class SanctuarySetBagRequest
    {
        public string[] itemIds;
    }
}
