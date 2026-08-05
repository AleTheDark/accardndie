namespace AccardND.Ads
{
    /// <summary>
    /// Come si presenta l'annuncio. Non e' un dettaglio del provider: decide chi paga il
    /// prezzo dell'attesa. L'interstitial arriva senza che il giocatore l'abbia chiesto,
    /// quindi va tenuto a freno da regole di frequenza; il rewarded lo chiede lui in cambio
    /// di qualcosa, quindi non ha bisogno di tetti.
    /// </summary>
    public enum AdFormat
    {
        Interstitial,
        Rewarded
    }

    /// <summary>
    /// I punti del gioco in cui puo' partire una pubblicita'. Sono nomi di momenti di gioco,
    /// non di unita' pubblicitarie: quale ad unit corrisponda a ciascuno lo decide il
    /// provider, cosi' cambiare rete (AdMob, H5 sul web, un portale) non tocca il gameplay.
    /// </summary>
    public enum AdPlacement
    {
        /// <summary>Riscossione di una singola quest giornaliera in taverna.</summary>
        TavernQuestClaim,

        /// <summary>Riscossione del premio di giornata, quello da 50 vasetti.</summary>
        TavernBonusClaim,

        /// <summary>Triplicare l'EXP account a fine run di campagna.</summary>
        CampaignExperienceTriple,

        /// <summary>Triplicare l'EXP account a fine partita PvP.</summary>
        PvpExperienceTriple,

        /// <summary>Uso di un consumabile della bisaccia durante la run.</summary>
        BagItemUsed
    }

    public static class AdPlacements
    {
        public static AdFormat FormatOf(AdPlacement placement)
        {
            switch (placement)
            {
                case AdPlacement.TavernBonusClaim:
                case AdPlacement.CampaignExperienceTriple:
                case AdPlacement.PvpExperienceTriple:
                    return AdFormat.Rewarded;
                default:
                    return AdFormat.Interstitial;
            }
        }

        /// <summary>
        /// Il placement puo' ripetersi piu' volte nella stessa occasione in cui e' stato
        /// preparato. Serve al provider per decidere se, dopo uno show, vale la pena ricaricare
        /// subito: chi si ripete (le riscossioni della taverna, gli oggetti della bisaccia) va
        /// tenuto rifornito, chi capita una volta sola (il premio di giornata, il triplicatore
        /// di fine run o di fine partita) no, perche' quella ricarica sarebbe una richiesta ad
        /// AdMob che nessuno vedra' mai.
        /// </summary>
        public static bool MayRepeat(AdPlacement placement)
        {
            switch (placement)
            {
                case AdPlacement.TavernQuestClaim:
                case AdPlacement.BagItemUsed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Chiave stabile del placement. Finisce nei log e sara' la chiave con cui il provider
        /// sceglie l'ad unit: cambiarla significa cambiare anche la configurazione della rete
        /// pubblicitaria, quindi non e' un nome di comodo.
        /// </summary>
        public static string KeyOf(AdPlacement placement)
        {
            switch (placement)
            {
                case AdPlacement.TavernQuestClaim: return "tavern_quest_claim";
                case AdPlacement.TavernBonusClaim: return "tavern_bonus_claim";
                case AdPlacement.CampaignExperienceTriple: return "campaign_experience_triple";
                case AdPlacement.PvpExperienceTriple: return "pvp_experience_triple";
                case AdPlacement.BagItemUsed: return "bag_item_used";
                default: return "unknown";
            }
        }

        /// <summary>Etichetta leggibile, per i messaggi di prova e i log.</summary>
        public static string DescribeFor(AdPlacement placement)
        {
            switch (placement)
            {
                case AdPlacement.TavernQuestClaim: return "riscossione quest";
                case AdPlacement.TavernBonusClaim: return "premio di giornata";
                case AdPlacement.CampaignExperienceTriple: return "EXP tripla di campagna";
                case AdPlacement.PvpExperienceTriple: return "EXP tripla d'arena";
                case AdPlacement.BagItemUsed: return "uso di un oggetto";
                default: return placement.ToString();
            }
        }
    }
}
