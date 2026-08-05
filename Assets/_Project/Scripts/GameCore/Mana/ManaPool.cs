using System;
using System.Collections.Generic;

namespace AccardND.GameCore.Mana
{
    /// <summary>
    /// Riserva di mana di un giocatore. E' globale: tutte le pedine attingono alla stessa
    /// cassa. Tiene anche il contatore che alimenta l'unico sovrapprezzo del gioco:
    /// le supreme gia' usate per classe nel round.
    /// </summary>
    public sealed class ManaPool
    {
        private readonly ManaRules rules;
        private readonly Dictionary<HeroClass, int> supremeUsesThisRound = new();
        private int current;

        public ManaPool(ManaRules rules = null)
        {
            this.rules = rules ?? ManaRules.CreateDefault();
            current = this.rules.RunStart;
        }

        public ManaRules Rules => rules;

        public int Current => current;

        /// <summary>Riserva a inizio run di campagna o inizio match.</summary>
        public void StartRun()
        {
            current = rules.RunStart;
            supremeUsesThisRound.Clear();
        }

        /// <summary>
        /// Inizio stanza o round: il mana persiste, ma risale al pavimento se e' sceso sotto.
        /// Il contatore di ripetizione delle supreme si azzera.
        /// </summary>
        public void StartRound()
        {
            if (current < rules.RoundFloor)
                current = rules.RoundFloor;
            supremeUsesThisRound.Clear();
        }

        /// <summary>
        /// Costo della prima abilita'. E' fisso: nessuna azione, di questa o di un'altra
        /// pedina, lo fa salire. L'unica escalation del gioco e' quella delle supreme.
        /// </summary>
        public int CostOfPrimary(HeroClass heroClass) =>
            AbilityManaCosts.Primary(heroClass);

        /// <summary>
        /// Costo effettivo della suprema: base + una tacca per ogni suprema della stessa
        /// classe gia' usata nel round. Vedi Docs/mana-design.md.
        /// </summary>
        public int CostOfSupreme(HeroClass heroClass) =>
            AbilityManaCosts.Supreme(heroClass)
            + SupremeRepeatSurcharge(heroClass);

        public int SupremeUses(HeroClass heroClass) =>
            supremeUsesThisRound.TryGetValue(heroClass, out int uses) ? uses : 0;

        public bool CanAfford(int cost) => cost <= current;

        /// <summary>
        /// Paga il costo. Il mana resta speso anche se l'effetto poi fallisce: e' voluto.
        /// </summary>
        public void Spend(int cost)
        {
            if (cost < 0)
                throw new ArgumentOutOfRangeException(nameof(cost));
            if (cost > current)
                throw new InvalidOperationException($"Mana insufficiente: servono {cost}, disponibili {current}.");
            current -= cost;
        }

        /// <summary>Registra l'uso di una suprema ai fini del sovrapprezzo di ripetizione.</summary>
        public void RegisterSupremeUse(HeroClass heroClass)
        {
            supremeUsesThisRound[heroClass] = SupremeUses(heroClass) + 1;
        }

        /// <summary>Guadagno di mana, sempre limitato dal tetto. Restituisce quanto e' stato effettivamente aggiunto.</summary>
        public int Gain(int amount)
        {
            if (amount <= 0)
                return 0;
            int before = current;
            current = Math.Min(rules.Maximum, current + amount);
            return current - before;
        }

        /// <summary>
        /// Riserva del Paladino: porta il mana alla soglia solo se e' sotto, senza mai superarla.
        /// Restituisce quanto e' stato aggiunto (0 se eri gia' sopra).
        /// </summary>
        public int RaiseTo(int threshold)
        {
            int target = Math.Min(threshold, rules.Maximum);
            if (current >= target)
                return 0;
            int gained = target - current;
            current = target;
            return gained;
        }

        /// <summary>Ripristino da salvataggio (campagna) o da stato sincronizzato dal server.</summary>
        public void Restore(int value)
        {
            current = Math.Clamp(value, 0, rules.Maximum);
        }

        private int SupremeRepeatSurcharge(HeroClass heroClass) =>
            SupremeUses(heroClass) * rules.SupremeRepeatSurcharge;
    }
}
