using System;

namespace AccardND.GameCore
{
    public readonly struct SeraphelDefenseResult
    {
        public SeraphelDefenseResult(int damage, int before, int after, bool phaseChanged)
        {
            Damage = damage;
            HitPointsBefore = before;
            HitPointsAfter = after;
            PhaseChanged = phaseChanged;
        }

        public int Damage { get; }
        public int HitPointsBefore { get; }
        public int HitPointsAfter { get; }
        public bool PhaseChanged { get; }
    }

    public readonly struct SeraphelAttackResult
    {
        public SeraphelAttackResult(int attackRoll, int attackTotal, int defenseRoll,
            int defenseTotal, int sealsBefore, int sealsApplied)
        {
            AttackRoll = attackRoll;
            AttackTotal = attackTotal;
            DefenseRoll = defenseRoll;
            DefenseTotal = defenseTotal;
            SealsBefore = sealsBefore;
            SealsApplied = sealsApplied;
        }

        public int AttackRoll { get; }
        public int AttackTotal { get; }
        public int DefenseRoll { get; }
        public int DefenseTotal { get; }
        public int SealsBefore { get; }
        public int SealsApplied { get; }
        public int SealDamageBonus => SealsBefore * SeraphelBoss.DamagePerSeal;
        public bool AttackSucceeded => AttackTotal > DefenseTotal;
    }

    /// <summary>Boss del capitolo 4. Accumula Sigilli di Luce ed esegue il bersaglio al terzo.</summary>
    public sealed class SeraphelBoss
    {
        public const int DefaultHitPoints = 80;
        public const int PhaseTwoThreshold = DefaultHitPoints / 2;
        public const int PhaseOneStrength = 8;
        public const int PhaseTwoStrength = 10;
        public const int PhaseOneVigorDieSides = 10;
        public const int PhaseTwoVigorDieSides = 12;
        public const int DamagePerSeal = 2;
        public const int SealExecutionThreshold = 3;
        public const int ManaHealingThreshold = 10;
        public const int PhaseOneManaHealingAmount = 4;
        public const int PhaseTwoManaHealingAmount = 8;

        private readonly IRandomSource random;
        private bool phaseTwoActivated;

        public SeraphelBoss(IRandomSource random) : this(random, DefaultHitPoints) { }

        public SeraphelBoss(IRandomSource random, int maxHitPoints) : this(random, maxHitPoints, maxHitPoints) { }

        public SeraphelBoss(IRandomSource random, int maxHitPoints, int hitPoints)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (maxHitPoints < 2) throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            if (hitPoints < 0 || hitPoints > maxHitPoints) throw new ArgumentOutOfRangeException(nameof(hitPoints));
            MaxHitPoints = maxHitPoints;
            HitPoints = hitPoints;
            phaseTwoActivated = hitPoints <= maxHitPoints / 2;
        }

        public int MaxHitPoints { get; }
        public int HitPoints { get; private set; }
        public bool IsPhaseTwo => phaseTwoActivated;

        /// <summary>
        /// Rimette il boss come l'aveva lasciato una battaglia salvata a meta'. La seconda
        /// fase si salva a parte invece di ricavarla dagli HP: una volta scattata resta
        /// scattata, anche se nel frattempo il boss e' stato curato sopra la meta'.
        /// </summary>
        public void Restore(int hitPoints, bool phaseTwo)
        {
            HitPoints = Math.Clamp(hitPoints, 0, MaxHitPoints);
            phaseTwoActivated = phaseTwo || HitPoints <= MaxHitPoints / 2;
        }
        public bool IsImmuneToDebuffs => IsPhaseTwo;
        public bool IsDefeated => HitPoints <= 0;
        public int Strength => IsPhaseTwo ? PhaseTwoStrength : PhaseOneStrength;
        public int VigorDieSides => IsPhaseTwo ? PhaseTwoVigorDieSides : PhaseOneVigorDieSides;
        public int SealsPerHit => IsPhaseTwo ? 2 : 1;
        public int ManaHealingAmount => IsPhaseTwo ? PhaseTwoManaHealingAmount : PhaseOneManaHealingAmount;

        public int Heal(int amount)
        {
            if (amount <= 0 || IsDefeated) return 0;
            int before = HitPoints;
            HitPoints = Math.Min(MaxHitPoints, HitPoints + amount);
            return HitPoints - before;
        }

        public SeraphelDefenseResult ApplyResolvedDefense(int attackerTotal, int defenseTotal)
        {
            if (IsDefeated) throw new InvalidOperationException("Seraphel is already defeated.");
            bool wasPhaseTwo = IsPhaseTwo;
            int before = HitPoints;
            int damage = Math.Max(0, attackerTotal - defenseTotal);
            HitPoints = Math.Max(0, HitPoints - damage);
            if (HitPoints <= MaxHitPoints / 2)
                phaseTwoActivated = true;
            return new SeraphelDefenseResult(damage, before, HitPoints, !wasPhaseTwo && IsPhaseTwo);
        }

        public SeraphelAttackResult Attack(CombatCard target, int targetDefenseDieSides, int seals,
            int attackDieSides = 0, int? targetEffectiveStrength = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (targetDefenseDieSides < 2) throw new ArgumentOutOfRangeException(nameof(targetDefenseDieSides));
            if (seals < 0) throw new ArgumentOutOfRangeException(nameof(seals));
            int resolvedAttackDieSides = attackDieSides > 0 ? attackDieSides : VigorDieSides;
            if (resolvedAttackDieSides < 2) throw new ArgumentOutOfRangeException(nameof(attackDieSides));
            int attackRoll = random.NextInclusive(1, resolvedAttackDieSides);
            int defenseRoll = random.NextInclusive(1, targetDefenseDieSides);
            int attackTotal = Strength + attackRoll + seals * DamagePerSeal;
            int defenseStrength = targetEffectiveStrength ?? target.Strength;
            int defenseTotal = defenseStrength + defenseRoll;
            int applied = attackTotal > defenseTotal ? SealsPerHit : 0;
            return new SeraphelAttackResult(attackRoll, attackTotal, defenseRoll, defenseTotal, seals, applied);
        }
    }
}
