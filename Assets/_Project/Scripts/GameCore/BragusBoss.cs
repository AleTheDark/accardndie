using System;

namespace AccardND.GameCore
{
    public readonly struct BragusDefenseResult
    {
        public BragusDefenseResult(int attackerTotal, int defenseRoll, int defenseTotal, int damage, int hitPointsBefore, int hitPointsAfter, int counterRoll, int counterTotal, int targetDefenseTotal)
        {
            AttackerTotal = attackerTotal;
            DefenseRoll = defenseRoll;
            DefenseTotal = defenseTotal;
            Damage = damage;
            HitPointsBefore = hitPointsBefore;
            HitPointsAfter = hitPointsAfter;
            CounterRoll = counterRoll;
            CounterTotal = counterTotal;
            TargetDefenseTotal = targetDefenseTotal;
        }

        public int AttackerTotal { get; }
        public int DefenseRoll { get; }
        public int DefenseTotal { get; }
        public int Damage { get; }
        public int HitPointsBefore { get; }
        public int HitPointsAfter { get; }
        public int CounterRoll { get; }
        public int CounterTotal { get; }
        public int TargetDefenseTotal { get; }
        public bool Counterattacks => Damage == 0;
        public bool CounterDefeatsAttacker => Counterattacks && CounterTotal > TargetDefenseTotal;
    }

    public sealed class BragusBoss
    {
        public const int CardStrength = 10;
        public const int DefaultHitPoints = 45;
        public const int DefaultVigorDieSides = 8;

        private readonly IRandomSource random;

        public BragusBoss(IRandomSource random)
            : this(random, DefaultHitPoints)
        {
        }

        public BragusBoss(IRandomSource random, int maxHitPoints)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (maxHitPoints < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));

            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
        }

        public int MaxHitPoints { get; }
        public int HitPoints { get; private set; }
        public bool IsDefeated => HitPoints <= 0;

        public BragusDefenseResult ApplyResolvedDefense(
            int attackerTotal,
            int defenseRoll,
            int defenseTotal,
            CombatCard attacker,
            int attackerDefenseStrength,
            int attackerDefenseDieSides,
            bool counterTargetDefenseAdvantage = false)
        {
            if (attackerTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(attackerTotal));
            if (defenseRoll < 1)
                throw new ArgumentOutOfRangeException(nameof(defenseRoll));
            if (defenseTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(defenseTotal));
            if (attacker == null)
                throw new ArgumentNullException(nameof(attacker));
            if (attackerDefenseStrength < 0)
                throw new ArgumentOutOfRangeException(nameof(attackerDefenseStrength));
            if (attackerDefenseDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(attackerDefenseDieSides));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Bragus cannot defend.");

            int hitPointsBefore = HitPoints;
            int damage = Math.Max(0, attackerTotal - defenseTotal);
            if (damage > 0)
                HitPoints = Math.Max(0, HitPoints - damage);

            int counterRoll = 0;
            int counterTotal = 0;
            int targetDefenseTotal = 0;
            if (damage == 0)
            {
                counterRoll = RollVigor(DefaultVigorDieSides, CounterattackMatchupAgainst(attacker.HeroClass));
                int targetRoll = random.NextInclusive(1, attackerDefenseDieSides);
                if (counterTargetDefenseAdvantage)
                {
                    int secondTargetRoll = random.NextInclusive(1, attackerDefenseDieSides);
                    targetRoll = Math.Max(targetRoll, secondTargetRoll);
                }
                counterTotal = CardStrength + counterRoll;
                targetDefenseTotal = attackerDefenseStrength + targetRoll;
            }

            return new BragusDefenseResult(
                attackerTotal,
                defenseRoll,
                defenseTotal,
                damage,
                hitPointsBefore,
                HitPoints,
                counterRoll,
                counterTotal,
                targetDefenseTotal);
        }

        private int RollVigor(int dieSides, MatchupResult matchup)
        {
            int first = random.NextInclusive(1, dieSides);
            if (matchup == MatchupResult.Neutral)
                return first;

            int second = random.NextInclusive(1, dieSides);
            return matchup == MatchupResult.Advantage ? Math.Max(first, second) : Math.Min(first, second);
        }

        private static MatchupResult CounterattackMatchupAgainst(HeroClass targetClass)
        {
            return HeroClassFamily.Of(targetClass) == ClassFamily.Cunning
                ? MatchupResult.Disadvantage
                : ClassMatchup.Compare(HeroClass.Barbarian, targetClass);
        }
    }
}
