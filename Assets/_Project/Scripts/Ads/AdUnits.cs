namespace AccardND.Ads
{
    /// <summary>
    /// La mappa fra i momenti di gioco e le unita' pubblicitarie di AdMob. E' l'unico posto
    /// da compilare quando si crea un'unita' nuova nella console: il gameplay continua a
    /// parlare di <see cref="AdPlacement"/> e non sa che AdMob esista.
    ///
    /// Gli id non sono segreti: finiscono nel binario dell'app e chiunque puo' leggerli da
    /// una build. Quello che va tenuto fuori dal repository e' semmai la chiave della
    /// verifica lato server (SSV), che non passa di qui.
    ///
    /// Regola sul quale id usare: sempre le unita' vere, anche nelle build di sviluppo.
    /// E' una scelta di chi tiene l'account, presa per poter giocare la build vera e vedere
    /// che gli annunci arrivino davvero. Per tornare alle unita' di prova basta il define
    /// ACCARDND_TEST_ADS, che resta il canale per collaudare senza generare impression.
    /// </summary>
    public static class AdUnits
    {
        /// <summary>
        /// Id dell'app Android su AdMob. Non si usa in codice: va messo nelle impostazioni
        /// del plugin Google Mobile Ads (che lo scrive nell'AndroidManifest come
        /// com.google.android.gms.ads.APPLICATION_ID). Sta qui perche' e' il riferimento con
        /// cui si controlla che le unita' sotto appartengano davvero a quest'app: il numero
        /// prima della tilde e' lo stesso che compare prima della barra negli ad unit id.
        /// </summary>
        public const string AndroidAppId = "ca-app-pub-3580486749764055~5129791296";

        /// <summary>
        /// I dispositivi su cui AdMob deve servire annunci di prova pur usando le unita'
        /// vere. E' il modo corretto di collaudare una build di release sul proprio telefono:
        /// senza, ogni annuncio che tocchi e' traffico non valido sul tuo account.
        ///
        /// L'id si legge in logcat al primo avvio dell'app con l'SDK, nella riga che dice
        /// "Use RequestConfiguration.Builder().setTestDeviceIds(Arrays.asList("..."))".
        /// </summary>
        public static readonly string[] TestDeviceIds = { };

        // Unita' di prova pubbliche di Google: identiche per tutti, servono proprio a essere
        // usate in sviluppo. Da riverificare sulla documentazione AdMob quando si integra
        // l'SDK: sono stabili da anni ma restano una cosa che decide Google, non noi.
        private const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
        private const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";

        /// <summary>
        /// L'unita' vera per un placement, o stringa vuota se non e' ancora stata creata
        /// nella console AdMob.
        /// </summary>
        private static string AndroidUnitFor(AdPlacement placement)
        {
            switch (placement)
            {
                case AdPlacement.TavernQuestClaim:
                    return "ca-app-pub-3580486749764055/3206601469";
                case AdPlacement.BagItemUsed:
                    return "ca-app-pub-3580486749764055/2137062379";
                case AdPlacement.TavernBonusClaim:
                    return "ca-app-pub-3580486749764055/8510899039";
                case AdPlacement.CampaignExperienceTriple:
                    return "ca-app-pub-3580486749764055/4363504532";
                case AdPlacement.PvpExperienceTriple:
                    return "ca-app-pub-3580486749764055/7336438226";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// L'unita' da chiedere ad AdMob adesso. Vuota significa "per questo placement non
        /// c'e' niente da mostrare": il provider rispondera' NoFill e il gioco andra' avanti
        /// senza pubblicita', che e' il comportamento giusto sia per un placement non ancora
        /// configurato sia per una piattaforma dove AdMob non arriva (il web).
        /// </summary>
        public static string For(AdPlacement placement)
        {
#if ACCARDND_TEST_ADS
            return AdPlacements.FormatOf(placement) == AdFormat.Rewarded
                ? TestRewarded
                : TestInterstitial;
#else
            // Unita' vere anche in build di sviluppo: serve poter giocare la build e vedere
            // gli annunci veri. In editor il provider e' quello finto e non arriva mai qui.
            return AndroidUnitFor(placement);
#endif
        }

        /// <summary>
        /// Il placement ha un'unita' vera configurata. Serve a distinguere, nei log e in una
        /// eventuale schermata di diagnostica, "AdMob non aveva annunci" da "questo placement
        /// non l'abbiamo ancora creato nella console".
        /// </summary>
        public static bool HasRealUnit(AdPlacement placement) =>
            !string.IsNullOrEmpty(AndroidUnitFor(placement));
    }
}
