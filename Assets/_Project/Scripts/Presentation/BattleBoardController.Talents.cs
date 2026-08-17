using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccardND.GameData;
using AccardND.NetProtocol;
using UnityEngine;

namespace AccardND.Presentation
{
/// <summary>
/// I talenti dal lato della run: da dove il controller prende il pacchetto di modificatori
/// e i pochi effetti che non passano da un calcolo puro.
///
/// Il pacchetto arriva gia' risolto dal server dentro la progressione, quindi qui non ci
/// sono ranghi, cancelli o prezzi: solo la lettura della cache e l'applicazione. Il resto
/// dei conti sta in <see cref="TalentRunModifiers"/>, che si verifica senza scena.
/// </summary>
public sealed partial class BattleBoardController
{
    /// <summary>
    /// Se il primo potenziamento gratuito del mercante e' ancora da usare in questa run.
    /// Vive sul controller e non sul pacchetto perche' e' consumo, non dotazione: il
    /// pacchetto dice cosa il giocatore possiede, questo cosa ha gia' speso.
    /// </summary>
    private bool freeMerchantUpgradeAvailable;

    /// <summary>Se il "Secondo fiato" ha ancora la sua pedina da salvare in questa run.</summary>
    private bool secondWindAvailable;

    // Gli id dei rami. Sono le stesse stringhe del TalentCatalog sul server: il client non
    // decide quali rami esistono, ma deve sapere dove disegnarli nel favo.
    private const string TalentBranchPurse = "purse";
    private const string TalentBranchInitiative = "initiative";
    private const string TalentBranchMastery = "mastery";
    private const string TalentBranchOccasion = "occasion";

    /// <summary>L'albero come lo ha valutato il server. Null finche' non e' arrivato.</summary>
    private TalentData talentData;

    private bool talentLoading;
    private bool talentBuying;
    private string selectedTalentBranch;
    private string selectedTalentId;
    private Vector2 talentHivePanPosition;
    private float talentHiveZoom = 1f;

    /// <summary>
    /// Chiede l'albero al server. Come le altre pagine del profilo non e' bloccante: se non
    /// risponde, la scheda lo dice e il resto del profilo resta quello che e'.
    /// </summary>
    private async void LoadTalentsFromServer()
    {
        if (talentLoading)
            return;

        talentLoading = true;
        RefreshProfileIfShowingTalents();
        try
        {
            // Il profilo puo' essere aperto prima che la progressione autoritativa sia
            // agganciata (primo ingresso nell'Hub, rientro da una caduta di rete):
            // qui si aspetta l'aggancio invece di lasciare la scheda vuota.
            if (serverProgress == null && !await EnsureServerProgressAsync())
                throw new InvalidOperationException("serve una connessione al server.");

            talentData = await serverProgress.GetTalentsAsync();
        }
        catch (Exception exception)
        {
            AppendLog("TALENTI - caricamento fallito: " + exception.Message);
        }
        finally
        {
            talentLoading = false;
            RefreshProfileIfShowingTalents();
        }
    }

    /// <summary>
    /// Compra un rango. La risposta del server e' il nuovo albero, quindi non si ricalcola
    /// niente in locale: si ridisegna con quello che e' tornato.
    ///
    /// Dopo l'acquisto la progressione va riallineata, perche' insieme ai punti e' cambiato
    /// anche il pacchetto di modificatori che la prossima run si portera' dietro.
    /// </summary>
    private async void BuyTalent(string talentId)
    {
        if (talentBuying || serverProgress == null || string.IsNullOrEmpty(talentId))
            return;

        talentBuying = true;
        RefreshProfileIfShowingTalents();
        try
        {
            talentData = await serverProgress.BuyTalentAsync(talentId);
            selectedTalentId = talentId;
			PlayTalentAcquiredSfx();
            await serverProgress.RefreshAsync();
            RefreshAccountBannerView();
        }
        catch (Exception exception)
        {
            AppendLog("TALENTI - acquisto rifiutato: " + exception.Message);
        }
        finally
        {
            talentBuying = false;
            RefreshProfileIfShowingTalents();
        }
    }

    /// <summary>
    /// Ridisegna solo se la scheda talenti e' quella aperta: una risposta che arriva dopo che
    /// il giocatore e' passato ai traguardi non deve riportarlo indietro.
    /// </summary>
    private void RefreshProfileIfShowingTalents()
    {
        if (profilePanel != null && profilePanel.activeSelf && profilePage == ProfilePage.Talents)
            RefreshProfile();
    }

    private void SelectTalentBranch(string branch)
    {
		if (!string.Equals(selectedTalentBranch, branch, StringComparison.Ordinal))
			talentHivePanPosition = Vector2.zero;
        selectedTalentBranch = branch;
        selectedTalentId = null;
        RefreshProfile();
    }

    private void SelectTalentNode(string talentId)
    {
        selectedTalentId = talentId;
        selectedTalentBranch = FindTalent(talentId)?.branch;
        RefreshProfile();
    }

    private TalentEntryData FindTalent(string talentId)
    {
        if (talentData?.talents == null || string.IsNullOrEmpty(talentId))
            return null;

        foreach (TalentEntryData entry in talentData.talents)
        {
            if (string.Equals(entry.id, talentId, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    private TalentBranchData FindTalentBranch(string branch)
    {
        if (talentData?.branches == null)
            return null;

        foreach (TalentBranchData entry in talentData.branches)
        {
            if (string.Equals(entry.id, branch, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    /// <summary>I nodi di un ramo, dal tier piu' basso al piu' alto.</summary>
    private List<TalentEntryData> TalentsOfBranch(string branch)
    {
        var nodes = new List<TalentEntryData>();
        if (talentData?.talents == null)
            return nodes;

        foreach (TalentEntryData entry in talentData.talents)
        {
            if (string.Equals(entry.branch, branch, StringComparison.Ordinal))
                nodes.Add(entry);
        }
        nodes.Sort((left, right) => left.tier.CompareTo(right.tier));
        return nodes;
    }

    /// <summary>
    /// Il percorso dell'icona di un nodo, ricavato dal suo id: <c>purse-travel-fund</c>
    /// diventa <c>UI/ProfileTalents/talent_icon_purse_travel_fund</c>.
    ///
    /// Derivarlo invece di tenere una tabella id → file significa che aggiungere un talento
    /// e' aggiungere una riga al catalogo sul server e un PNG nella cartella, e nient'altro:
    /// una tabella a mano sarebbe la prima cosa a restare indietro.
    /// </summary>
    private static string TalentIconResourcePath(string talentId)
    {
        if (string.IsNullOrEmpty(talentId))
            return null;

        // Il nome artistico originale e' rimasto "concentration", mentre l'id definitivo
        // del catalogo e' "focus". Manteniamo l'asset e il suo GUID senza duplicarlo.
        if (string.Equals(talentId, "mastery-focus", StringComparison.Ordinal))
            return "UI/ProfileTalents/talent_icon_mastery_concentration";

        return "UI/ProfileTalents/talent_icon_" + talentId.Replace('-', '_');
    }

    /// <summary>
    /// Punti talento non spesi, come li conosce la cache di progressione. Alimenta il badge
    /// sul bottone del profilo: e' l'unico richiamo che riporta il giocatore nell'albero.
    /// </summary>
    private int UnspentTalentPoints() =>
        singlePlayerProgressService?.Progress?.talentPoints ?? 0;

    /// <summary>
    /// Se il bonus dello "Sfidante" e' ancora da spendere in questo scontro. Si riarma a
    /// ogni combattimento contro un boss o un miniboss, non una volta per run: il nodo
    /// promette il <em>primo</em> tiro contro il boss, e i boss sono piu' di uno.
    /// </summary>
    private bool challengerBonusAvailable;

    /// <summary>
    /// I modificatori dei talenti per questa run. Mai null: senza progressione caricata la
    /// run parte come se il giocatore non avesse nessun talento, che e' l'unico
    /// comportamento accettabile quando la cache non c'e' ancora.
    /// </summary>
    private TalentLoadoutSave ActiveTalents =>
        singlePlayerProgressService?.Progress?.talentLoadout ?? TalentRunModifiers.None;

    /// <summary>
    /// Riarma i consumi legati ai talenti. Va chiamata dove nasce la run, insieme al reset
    /// della progressione: sono contatori di run, non di partita.
    /// </summary>
    private void ResetTalentRunState()
    {
        TalentLoadoutSave talents = ActiveTalents;
        freeMerchantUpgradeAvailable = talents.firstMerchantUpgradeFree;
        secondWindAvailable = talents.savesFirstFallenCard;
    }

    /// <summary>
    /// Rimette i consumi come li ha lasciati una run ripresa dal salvataggio. Va chiamata
    /// dopo <see cref="ResetTalentRunState"/>, che li riarma tutti.
    /// </summary>
    private void RestoreTalentRunState(bool merchantUpgradeUsed, bool secondWindUsed)
    {
        if (merchantUpgradeUsed)
            freeMerchantUpgradeAvailable = false;
        if (secondWindUsed)
            secondWindAvailable = false;
    }

    /// <summary>
    /// Consuma il potenziamento gratuito se e' ancora disponibile. Restituisce il prezzo da
    /// pagare davvero: zero la prima volta, il prezzo pieno da li' in poi.
    /// </summary>
    private int ConsumeMerchantUpgradeCost(int cost)
    {
        if (!freeMerchantUpgradeAvailable || cost <= 0)
            return cost;

        freeMerchantUpgradeAvailable = false;
        return 0;
    }

    /// <summary>
    /// Rimette il potenziamento gratuito nel taschino quando l'acquisto non e' andato in
    /// porto. Senza, un talento una-tantum si brucerebbe su un errore del mercante e il
    /// giocatore non avrebbe modo di recuperarlo per il resto della run.
    /// </summary>
    private void RestoreMerchantUpgradeCost(bool wasUsed)
    {
        if (wasUsed)
            freeMerchantUpgradeAvailable = true;
    }

    /// <summary>
    /// "Secondo fiato": la prima pedina che cade in una run non resta al cimitero, torna nel
    /// mazzo.
    ///
    /// Va chiamata subito dopo <c>CampaignDeckState.CompleteCombat</c>, che e' il solo punto
    /// in cui una carta sconfitta finisce al cimitero: intercettarla prima vorrebbe dire
    /// riscrivere la lista dei caduti, e quella lista serve intatta a chi conta le perdite.
    ///
    /// Quando cadono piu' pedine nello stesso scontro ne salva una sola, la prima della
    /// formazione: il talento promette una pedina, non uno scontro.
    /// </summary>
    private void ApplySecondWindTalent(IReadOnlyList<CampaignCardInstance> defeatedCards)
    {
        if (!secondWindAvailable || campaignDeck == null || defeatedCards == null)
            return;

        foreach (CampaignCardInstance card in defeatedCards)
        {
            // RecoverFromGraveyard rifiuta le carte che non sono al cimitero: e' anche il
            // controllo che evita di bruciare il talento su una pedina che e' finita altrove.
            if (card == null || !campaignDeck.RecoverFromGraveyard(card))
                continue;

            secondWindAvailable = false;
            AppendLog(
                $"SECONDO FIATO - {CardDisplayNames.MarketName(card.Definition)} " +
                "torna nel mazzo invece che al cimitero.");
            return;
        }
    }

    /// <summary>Riarma lo "Sfidante" all'inizio di ogni scontro.</summary>
    private void ArmChallengerBonus()
    {
        challengerBonusAvailable = ActiveTalents.bossVigorBonus > 0;
    }

    /// <summary>
    /// Il bonus dello "Sfidante" sull'attacco in corso, consumato se spetta. Vale solo per
    /// il primo attacco del giocatore contro un boss o un miniboss dello scontro.
    /// </summary>
    private int ConsumeChallengerBonus(BattleCardState attacker, BattleCardState defender)
    {
        if (!challengerBonusAvailable || attacker == null || !attacker.BelongsToPlayer)
            return 0;
        if (!IsBossOrMinibossProxy(defender))
            return 0;

        challengerBonusAvailable = false;
        return ActiveTalents.bossVigorBonus;
    }

    /// <summary>
    /// Se la pedina e' l'incarnazione di un boss o di un miniboss. Elenca tutte le proxy
    /// esistenti: un boss nuovo che non finisse qui dentro spegnerebbe lo "Sfidante" proprio
    /// nello scontro in cui il giocatore se lo aspetta di piu'.
    /// </summary>
    private bool IsBossOrMinibossProxy(BattleCardState card) =>
        card != null && (
            IsComposableGolemProxy(card) ||
            IsMedusaBossProxy(card) ||
            IsTrentorBossProxy(card) ||
            IsBragusBossProxy(card) ||
            IsPalatirBossProxy(card) ||
            IsSeraphelBossProxy(card));

    /// <summary>
    /// "Tempra del fabbro": a mazzo appena forgiato, alza di un punto forza N carte a caso.
    ///
    /// Le carte si estraggono senza rimpiazzo, quindi due ranghi toccano due pedine diverse:
    /// impilarli sulla stessa varrebbe come un potenziamento del mercante ed e' un'altra cosa.
    /// </summary>
    private void ApplyForgeTemperTalent()
    {
        int temperedCards = ActiveTalents.forgeTemperedCards;
        if (temperedCards <= 0 || campaignDeck == null)
            return;

        var candidates = new System.Collections.Generic.List<CampaignCardInstance>(campaignDeck.Cards);
        var tempered = new System.Collections.Generic.List<string>();
        while (tempered.Count < temperedCards && candidates.Count > 0)
        {
            int index = random.NextInclusive(0, candidates.Count - 1);
            CampaignCardInstance card = candidates[index];
            candidates.RemoveAt(index);
            if (campaignDeck.TryApplyForgeTemper(card))
                tempered.Add(CardDisplayNames.MarketName(card.Definition));
        }

        if (tempered.Count > 0)
            AppendLog($"TEMPRA DEL FABBRO - {string.Join(", ", tempered)} guadagna +1 Forza.");
    }

    /// <summary>
    /// Quale dei dadi d'iniziativa del giocatore tira questa carta: 0 e' il primo, ed e'
    /// quello che i tre nodi del ramo Iniziativa distinguono.
    /// -1 per le carte della CPU, che non hanno talenti.
    /// </summary>
    private int InitiativeSlotOf(BattleCardState card) =>
        card == null || !card.BelongsToPlayer ? -1 : playerCards.IndexOf(card);

    /// <summary>
    /// L'iniziativa che conta per l'ordine di turno: il tiro piu' il bonus dei talenti.
    ///
    /// Il bonus resta separato dal tiro perche' <c>RollUniqueInitiative</c> garantisce
    /// valori unici fra tutti i combattenti: sommarlo al numero estratto creerebbe collisioni
    /// con un'iniziativa gia' assegnata. Vuol dire anche che il dado a schermo continua a
    /// mostrare il risultato vero, che e' l'unico modo di non far sospettare che il gioco bari.
    ///
    /// Il bonus si legge dalla pedina e non piu' dalla sua posizione in fila: la fila e'
    /// l'ordine di schieramento, cioe' iniziativa crescente, quindi lo slot 0 era sempre
    /// il tiro piu' basso del giocatore. Il talento "1º dado" finiva addosso al dado
    /// peggiore e a battaglia iniziata ribaltava la timeline che il giocatore aveva
    /// appena visto. Adesso il bonus viene assegnato al tiro nel momento in cui esce,
    /// mostrato sul dado durante lo schieramento e portato qui dalla pedina.
    /// </summary>
    private int EffectiveInitiativeOf(BattleCardState card) =>
        card == null ? 0 : card.Initiative + card.InitiativeTalentBonus;

    /// <summary>Se questa pedina e' quella che "Apertura" manda per prima.</summary>
    private static bool OpensTheFight(BattleCardState card) =>
        card != null && card.OpensTheFight;

    /// <summary>
    /// Assegna a una pedina i talenti d'iniziativa in base allo slot del suo dado. La usa
    /// il tiro in battaglia (<c>RollInitiatives</c>), dove i dadi si tirano a formazione
    /// gia' schierata e lo slot coincide con la posizione in fila.
    /// </summary>
    private void ApplyInitiativeTalentsBySlot(BattleCardState card)
    {
        if (card == null)
            return;

        int slot = InitiativeSlotOf(card);
        card.InitiativeTalentBonus = slot < 0 ? 0 : TalentRunModifiers.InitiativeBonus(slot, ActiveTalents);
        card.OpensTheFight = slot == 0 && ActiveTalents.opensEveryFight;
    }

    /// <summary>
    /// Ordina la timeline dei turni: iniziativa effettiva decrescente, poi il tie-breaker.
    ///
    /// "Apertura" si infila prima di tutto il resto. Qui c'era "Colpo d'anticipo", che
    /// vinceva le parita' d'iniziativa: i tiri pero' sono estratti unici fra tutti i
    /// combattenti, quindi quel ramo non veniva percorso mai e il talento era inerte.
    /// </summary>
    private int CompareByInitiative(BattleCardState left, BattleCardState right)
    {
        bool leftOpens = OpensTheFight(left);
        bool rightOpens = OpensTheFight(right);
        if (leftOpens != rightOpens)
            return leftOpens ? -1 : 1;

        int compared = EffectiveInitiativeOf(right).CompareTo(EffectiveInitiativeOf(left));
        if (compared != 0)
            return compared;

        return right.TieBreaker.CompareTo(left.TieBreaker);
    }

    /// <summary>
    /// Mette a verbale l'ordine dei turni quando non coincide con quello dei numeri
    /// tirati: le pedine mostrano il tiro vero, quindi una che agisce fuori sequenza
    /// senza una riga di log si legge come un bug. Il riordino da talenti il giocatore
    /// l'ha gia' visto succedere sui dadi; qui restano i casi che nascono a battaglia
    /// iniziata, come gli assassini che agiscono per ultimi o il ri-tiro del golem.
    /// </summary>
    private void LogDeploymentTurnOrderReorder()
    {
        if (turnOrder.Count == 0)
            return;

        List<BattleCardState> rolledOrder = new List<BattleCardState>(turnOrder);
        rolledOrder.Sort((left, right) => right.Initiative.CompareTo(left.Initiative));
        if (rolledOrder.SequenceEqual(turnOrder))
            return;

        List<string> parts = new List<string>(turnOrder.Count);
        foreach (BattleCardState card in turnOrder)
        {
            string reason = OpensTheFight(card)
                ? " (Apertura)"
                : InitiativeBonusLogSuffix(card);
            parts.Add($"{card.Card.Name} {card.Initiative}{reason}");
        }
        AppendLog("ORDINE DEI TURNI - i talenti cambiano l'ordine dei dadi: " + string.Join(", ", parts));
    }

    /// <summary>
    /// Il pezzo di log che spiega il bonus di iniziativa, quando c'e'. Senza, il giocatore
    /// vedrebbe una pedina col dado piu' basso agire per prima e lo leggerebbe come un bug.
    /// </summary>
    private static string InitiativeBonusLogSuffix(BattleCardState card) =>
        card != null && card.InitiativeTalentBonus > 0
            ? $" (+{card.InitiativeTalentBonus} dai talenti)"
            : string.Empty;
}
}
