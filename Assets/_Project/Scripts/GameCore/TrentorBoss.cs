using System;

namespace AccardND.GameCore
{
    public readonly struct TrentorDefenseResult
    {
        public TrentorDefenseResult(int attackerTotal, int defenseRoll, int defenseTotal, int damage, int hitPointsBefore, int hitPointsAfter)
        {
            AttackerTotal = attackerTotal;
            DefenseRoll = defenseRoll;
            DefenseTotal = defenseTotal;
            Damage = damage;
            HitPointsBefore = hitPointsBefore;
            HitPointsAfter = hitPointsAfter;
        }

        public int AttackerTotal { get; }
        public int DefenseRoll { get; }
        public int DefenseTotal { get; }
        public int Damage { get; }
        public int HitPointsBefore { get; }
        public int HitPointsAfter { get; }
    }

    public readonly struct TrentorAttackResult
    {
        public TrentorAttackResult(int vigorRoll, int attackTotal, int targetVigorRoll, int targetDefenseTotal, bool markedTargetBonus, bool rootsApplied)
        {
            VigorRoll = vigorRoll;
            AttackTotal = attackTotal;
            TargetVigorRoll = targetVigorRoll;
            TargetDefenseTotal = targetDefenseTotal;
            MarkedTargetBonus = markedTargetBonus;
            RootsApplied = rootsApplied;
        }

        public int VigorRoll { get; }
        public int AttackTotal { get; }
        public int TargetVigorRoll { get; }
        public int TargetDefenseTotal { get; }
        public bool MarkedTargetBonus { get; }
        public bool RootsApplied { get; }
        public bool TargetIsDefeated => AttackTotal > TargetDefenseTotal;
    }

    public sealed class TrentorBoss
    {
        public const int CardStrength = 12;
        public const int DefaultHitPoints = 40;
        public const int DefaultVigorDieSides = 8;
        public const int MarkedTargetAttackBonus = 2;
        public const int RootsVigorPenaltySteps = 1;
        public const int RootsEveryTurns = 2;

        private readonly IRandomSource random;

        public TrentorBoss(IRandomSource random)
            : this(random, DefaultHitPoints)
        {
        }

        public TrentorBoss(IRandomSource random, int maxHitPoints)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (maxHitPoints < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));

            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
        }

        public int MaxHitPoints { get; }
        public int HitPoints { get; private set; }
        public int TurnsTaken { get; private set; }
        public bool IsDefeated => HitPoints <= 0;

        public TrentorDefenseResult ApplyResolvedDefense(int attackerTotal, int defenseRoll, int defenseTotal)
        {
            if (attackerTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(attackerTotal));
            if (defenseRoll < 1)
                throw new ArgumentOutOfRangeException(nameof(defenseRoll));
            if (defenseTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(defenseTotal));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Trentor cannot defend.");

            int hitPointsBefore = HitPoints;
            int damage = Math.Max(0, attackerTotal - defenseTotal);
            if (damage > 0)
                HitPoints = Math.Max(0, HitPoints - damage);

            return new TrentorDefenseResult(attackerTotal, defenseRoll, defenseTotal, damage, hitPointsBefore, HitPoints);
        }

        public TrentorAttackResult Attack(CombatCard target, int targetDefenseDieSides, bool targetIsMarked)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetDefenseDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(targetDefenseDieSides));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Trentor cannot attack.");

            TurnsTaken++;
            int attackRoll = RollVigor(DefaultVigorDieSides, ClassMatchup.Compare(HeroClass.Hunter, target.HeroClass));
            int targetRoll = random.NextInclusive(1, targetDefenseDieSides);
            int markedBonus = targetIsMarked ? MarkedTargetAttackBonus : 0;
            int attackTotal = CardStrength + attackRoll + markedBonus;
            int targetDefenseTotal = target.Strength + targetRoll;
            bool rootsApplied = TurnsTaken % RootsEveryTurns == 0;
            return new TrentorAttackResult(attackRoll, attackTotal, targetRoll, targetDefenseTotal, targetIsMarked, rootsApplied);
        }

        private int RollVigor(int dieSides, MatchupResult matchup)
        {
            int first = random.NextInclusive(1, dieSides);
            if (matchup == MatchupResult.Neutral)
                return first;

            int second = random.NextInclusive(1, dieSides);
            return matchup == MatchupResult.Advantage ? Math.Max(first, second) : Math.Min(first, second);
        }
    }
}
