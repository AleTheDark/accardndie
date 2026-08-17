using System;
using System.Collections.Generic;

namespace AccardND.GameData
{
    /// <summary>
    /// Applica alla run i modificatori dei talenti. E' il solo posto dove i valori del
    /// pacchetto diventano numeri di gioco: il controller di battaglia chiama questi metodi
    /// e non sa nulla di ranghi o di percentuali.
    ///
    /// Tutto e' statico e senza stato per poter essere verificato senza scena e senza rete,
    /// che e' l'unico modo pratico di controllare un bilanciamento fatto di sconti.
    ///
    /// Regola che vale per l'intero file: nessun metodo qui dentro aumenta l'esperienza
    /// guadagnata nella run. Il ramo Maestria fa salire di livello prima abbassando le
    /// <em>soglie</em>, e l'esperienza incassata resta identica a quella di chi non ha
    /// nessun talento. Serve a tenere chiuso l'anello exp -> livelli -> punti -> exp: vedi
    /// Docs/talenti-design.md.
    /// </summary>
    public static class TalentRunModifiers
    {
        /// <summary>Un pacchetto vuoto, per quando la progressione non e' ancora arrivata.</summary>
        public static readonly TalentLoadoutSave None = new TalentLoadoutSave();

        /// <summary>
        /// Le soglie di livello della run, gia' scontate. L'ordine e' quello della
        /// configurazione: indice 0 e' la soglia per il secondo livello.
        ///
        /// Lo sconto e' uno solo e uguale su tutte le soglie. Prima ce n'erano quattro, uno
        /// generale e tre mirati su livelli specifici: sommati portavano il d20 troppo
        /// avanti nella run e il resto della campagna diventava una discesa. Il ramo
        /// Maestria adesso ha un solo nodo di progressione, e gli altri tre agiscono sul
        /// combattimento.
        ///
        /// Il risultato non scende mai sotto 1: una soglia a zero farebbe salire di livello
        /// all'infinito al primo punto esperienza.
        /// </summary>
        public static int[] ApplyLevelThresholds(IReadOnlyList<int> thresholds, TalentLoadoutSave loadout)
        {
            if (thresholds == null)
                return Array.Empty<int>();

            var applied = new int[thresholds.Count];
            int discount = (loadout ?? None).masteryThresholdPercent;

            for (int index = 0; index < thresholds.Count; index++)
                applied[index] = ApplyDiscount(thresholds[index], discount);

            return applied;
        }

        /// <summary>Essenza con cui si entra nella forgia.</summary>
        public static int StartingEssence(int configuredEssence, TalentLoadoutSave loadout) =>
            Math.Max(1, configuredEssence + Math.Max(0, (loadout ?? None).startingEssence));

        /// <summary>Oro in tasca alla prima stanza.</summary>
        public static int StartingGold(TalentLoadoutSave loadout) =>
            Math.Max(0, (loadout ?? None).startingGold);

        /// <summary>Prezzo del mercante dopo lo sconto del ramo Borsa.</summary>
        public static int MerchantCost(int baseCost, TalentLoadoutSave loadout) =>
            ApplyDiscount(baseCost, (loadout ?? None).merchantDiscountPercent);

        /// <summary>Costo di recupero dopo lo sconto del ramo Occasioni.</summary>
        public static int RecoveryCost(int baseCost, TalentLoadoutSave loadout) =>
            ApplyDiscount(baseCost, (loadout ?? None).recoveryDiscountPercent);

        /// <summary>
        /// Bonus di iniziativa della pedina in posizione <paramref name="slot"/> nella
        /// formazione. Si somma all'ordinamento, mai al numero mostrato sul dado: mentire sul
        /// risultato di un dado e' il modo piu' rapido per far sospettare che il gioco bari.
        /// </summary>
        public static int InitiativeBonus(int slot, TalentLoadoutSave loadout) =>
            (loadout ?? None).InitiativeBonusFor(slot);

        /// <summary>
        /// Quanti consumabili consegna una stanza bottino: uno di base, piu' quelli del
        /// Cercatore.
        /// </summary>
        public static int LootItemCount(TalentLoadoutSave loadout) =>
            1 + Math.Max(0, (loadout ?? None).extraLootItems);

        /// <summary>
        /// "Concentrazione": il mana recuperato entrando in una stanza nuova. Si somma a
        /// quello che il giocatore si porta dietro, e la riserva lo taglia comunque al
        /// proprio tetto: il talento accelera il recupero, non alza il massimo.
        /// </summary>
        public static int RoomChangeMana(TalentLoadoutSave loadout) =>
            Math.Max(0, (loadout ?? None).roomChangeMana);

        /// <summary>
        /// "Riserva": il tetto della riserva di mana del giocatore, partendo da quello di
        /// configurazione. Il bonus e' limitato a +2, il massimo che il nodo puo' dare: un
        /// pacchetto corrotto o manomesso non deve poter regalare una riserva infinita.
        /// </summary>
        public static int MaximumMana(int configuredMaximum, TalentLoadoutSave loadout) =>
            configuredMaximum + Math.Clamp((loadout ?? None).bonusMaximumMana, 0, 2);

        /// <summary>
        /// "Trance": se la prima abilita' di classe della stanza e' gratis. E' una dotazione,
        /// non un consumo - chi tiene il conto di quella gia' usata e' il controller.
        /// </summary>
        public static bool FirstAbilityFreeEachRoom(TalentLoadoutSave loadout) =>
            (loadout ?? None).firstAbilityFreeEachRoom;

        /// <summary>
        /// Applica uno sconto percentuale arrotondando per eccesso il costo che resta: uno
        /// sconto non deve mai regalare piu' di quanto dice, e il minimo resta 1 perche' un
        /// prezzo a zero non e' uno sconto, e' un altro talento.
        /// </summary>
        private static int ApplyDiscount(int baseValue, int discountPercent)
        {
            if (baseValue <= 0)
                return 0;

            int discount = Math.Clamp(discountPercent, 0, 90);
            if (discount == 0)
                return baseValue;

            int remaining = 100 - discount;
            return Math.Max(1, (baseValue * remaining + 99) / 100);
        }
    }
}
