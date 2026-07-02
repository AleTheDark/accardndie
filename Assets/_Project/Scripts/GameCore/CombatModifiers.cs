namespace AccardND.GameCore
{
    public enum CombatCertainty
    {
        Impossible,
        RollRequired,
        Guaranteed
    }

    public readonly struct CombatModifiers
    {
        public CombatModifiers(
            bool sumAttackerVigor,
            bool defenderAdvantage,
            bool rerollAttackerOnes = false,
            bool rerollAttackerTwos = false,
            int attackerFlatBonus = 0,
            int defenderFlatBonus = 0,
            bool neutralizeAttackerMatchup = false,
            bool forceAttackerAdvantage = false)
        {
            SumAttackerVigor = sumAttackerVigor;
            DefenderAdvantage = defenderAdvantage;
            RerollAttackerOnes = rerollAttackerOnes;
            RerollAttackerTwos = rerollAttackerTwos;
            AttackerFlatBonus = attackerFlatBonus;
            DefenderFlatBonus = defenderFlatBonus;
            NeutralizeAttackerMatchup = neutralizeAttackerMatchup;
            ForceAttackerAdvantage = forceAttackerAdvantage;
        }

        public bool SumAttackerVigor { get; }
        public bool DefenderAdvantage { get; }
        public bool RerollAttackerOnes { get; }
        public bool RerollAttackerTwos { get; }
        public int AttackerFlatBonus { get; }
        public int DefenderFlatBonus { get; }
        public bool NeutralizeAttackerMatchup { get; }
        public bool ForceAttackerAdvantage { get; }

        public static CombatModifiers None => new(false, false);
    }

    public static class CombatCertaintyCalculator
    {
        public static CombatCertainty Evaluate(
            CombatCard attacker,
            CombatCard defender,
            int attackerDieSides,
            int defenderDieSides,
            CombatModifiers modifiers)
        {
            int attackerMinimumVigor = modifiers.SumAttackerVigor ? 2 : 1;
            int attackerMaximumVigor = modifiers.SumAttackerVigor
                ? attackerDieSides * 2
                : attackerDieSides;
            int defenderMinimumVigor = 1;
            int defenderMaximumVigor = defenderDieSides;

            int attackerMinimum = attacker.Strength + attackerMinimumVigor + modifiers.AttackerFlatBonus;
            int attackerMaximum = attacker.Strength + attackerMaximumVigor + modifiers.AttackerFlatBonus;
            int defenderMinimum = defender.Strength + defenderMinimumVigor + modifiers.DefenderFlatBonus;
            int defenderMaximum = defender.Strength + defenderMaximumVigor + modifiers.DefenderFlatBonus;

            if (attackerMaximum <= defenderMinimum)
                return CombatCertainty.Impossible;
            if (attackerMinimum > defenderMaximum)
                return CombatCertainty.Guaranteed;
            return CombatCertainty.RollRequired;
        }
    }
}
