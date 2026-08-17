using System;

namespace AccardND.GameCore
{
    public readonly struct JurinashorDefenseResult
    {
        public JurinashorDefenseResult(int damage, int hitPointsBefore, int hitPointsAfter, bool phaseChanged)
        {
            Damage = damage;
            HitPointsBefore = hitPointsBefore;
            HitPointsAfter = hitPointsAfter;
            PhaseChanged = phaseChanged;
        }

        public int Damage { get; }
        public int HitPointsBefore { get; }
        public int HitPointsAfter { get; }
        public bool PhaseChanged { get; }
    }

    public sealed class JurinashorBoss
    {
        public const int CardStrength = 8;
        public const int DefaultHitPoints = 30;
        public const int DefaultVigorDieSides = 10;

        public JurinashorBoss(int maxHitPoints = DefaultHitPoints)
        {
            if (maxHitPoints < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
        }

        public int MaxHitPoints { get; }
        public int HitPoints { get; private set; }
        public bool IsPhaseTwo { get; private set; }
        public bool IsImmuneToDebuffs => IsPhaseTwo;
        public bool IsDefeated => IsPhaseTwo && HitPoints <= 0;

		public void Restore(int hitPoints, bool phaseTwo)
		{
			IsPhaseTwo = phaseTwo;
			HitPoints = Math.Max(1, Math.Min(MaxHitPoints, hitPoints));
		}

        public JurinashorDefenseResult ApplyResolvedDefense(int attackerTotal, int defenseTotal)
        {
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Jurinashor cannot defend.");
            int before = HitPoints;
            int damage = Math.Min(before, Math.Max(0, attackerTotal - defenseTotal));
            HitPoints = Math.Max(0, HitPoints - damage);
            bool phaseChanged = false;
            if (!IsPhaseTwo && HitPoints <= 0)
            {
                IsPhaseTwo = true;
                HitPoints = MaxHitPoints;
                phaseChanged = true;
            }
            return new JurinashorDefenseResult(damage, before, HitPoints, phaseChanged);
        }
    }
}
