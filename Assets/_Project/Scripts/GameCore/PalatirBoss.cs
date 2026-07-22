using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    public readonly struct PalatirDefenseResult
    {
        public PalatirDefenseResult(
            int attackerTotal,
            int defenderRoll,
            int defenderTotal,
            ClassFamily attackerFamily,
            ClassFamily? targetedShield,
            bool shieldWasBroken,
            int damage,
            int hitPointsBefore,
            int hitPointsAfter)
        {
            AttackerTotal = attackerTotal;
            DefenderRoll = defenderRoll;
            DefenderTotal = defenderTotal;
            AttackerFamily = attackerFamily;
            TargetedShield = targetedShield;
            ShieldWasBroken = shieldWasBroken;
            Damage = damage;
            HitPointsBefore = hitPointsBefore;
            HitPointsAfter = hitPointsAfter;
        }

        public int AttackerTotal { get; }
        public int DefenderRoll { get; }
        public int DefenderTotal { get; }
        public ClassFamily AttackerFamily { get; }
        public ClassFamily? TargetedShield { get; }
        public bool ShieldWasBroken { get; }
        public int Damage { get; }
        public int HitPointsBefore { get; }
        public int HitPointsAfter { get; }
        public bool WasBlockedByShields => Damage == 0 && TargetedShield.HasValue;
    }

    public readonly struct PalatirAttackResult
    {
        public PalatirAttackResult(
            CombatCard target,
            int vigorRoll,
            int attackTotal,
            int targetVigorRoll,
            int targetDefenseTotal)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            VigorRoll = vigorRoll;
            AttackTotal = attackTotal;
            TargetVigorRoll = targetVigorRoll;
            TargetDefenseTotal = targetDefenseTotal;
        }

        public CombatCard Target { get; }
        public int VigorRoll { get; }
        public int AttackTotal { get; }
        public int TargetVigorRoll { get; }
        public int TargetDefenseTotal { get; }
        public bool TargetIsDefeated => AttackTotal > TargetDefenseTotal;
    }

    public sealed class PalatirBoss
    {
        public const int DefaultHitPoints = 45;
        public const int CardStrength = 7;
        public const int DefaultVigorDieSides = 10;

        private readonly IRandomSource random;
        private readonly HashSet<ClassFamily> activeShields = new()
        {
            ClassFamily.Might,
            ClassFamily.Cunning,
            ClassFamily.Magic
        };

        public PalatirBoss(IRandomSource random)
            : this(random, DefaultHitPoints)
        {
        }

        public PalatirBoss(IRandomSource random, int maxHitPoints)
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
        public bool HasActiveShields => activeShields.Count > 0;
        public IReadOnlyCollection<ClassFamily> ActiveShields => activeShields;

        public PalatirDefenseResult ApplyResolvedDefense(
            CombatCard attacker,
            int attackerTotal,
            int defenderRoll,
            int defenderTotal)
        {
            if (attacker == null)
                throw new ArgumentNullException(nameof(attacker));
            if (attackerTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(attackerTotal));
            if (defenderTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(defenderTotal));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Palatir cannot defend.");

            int before = HitPoints;
            ClassFamily attackerFamily = HeroClassFamily.Of(attacker.HeroClass);
            ClassFamily? targetedShield = ShieldBrokenBy(attackerFamily);
            bool hit = attackerTotal > defenderTotal;
            bool shieldWasBroken = false;
            int damage = 0;

            if (HasActiveShields)
            {
                if (hit && targetedShield.HasValue && activeShields.Remove(targetedShield.Value))
                    shieldWasBroken = true;
            }
            else
            {
                damage = Math.Max(0, attackerTotal - defenderTotal);
                if (damage > 0)
                    HitPoints = Math.Max(0, HitPoints - damage);
            }

            return new PalatirDefenseResult(
                attackerTotal,
                defenderRoll,
                defenderTotal,
                attackerFamily,
                targetedShield,
                shieldWasBroken,
                damage,
                before,
                HitPoints);
        }

        public PalatirAttackResult Attack(CombatCard target, int targetVigorDieSides)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetVigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(targetVigorDieSides));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Palatir cannot attack.");

            int vigorRoll = random.NextInclusive(1, DefaultVigorDieSides);
            int targetVigorRoll = random.NextInclusive(1, targetVigorDieSides);
            return new PalatirAttackResult(
                target,
                vigorRoll,
                CardStrength + vigorRoll,
                targetVigorRoll,
                target.Strength + targetVigorRoll);
        }

        public static ClassFamily ShieldBrokenBy(ClassFamily attackerFamily)
        {
            return attackerFamily switch
            {
                ClassFamily.Might => ClassFamily.Cunning,
                ClassFamily.Cunning => ClassFamily.Magic,
                ClassFamily.Magic => ClassFamily.Might,
                _ => ClassFamily.Might
            };
        }

        public static int SelectCosmicTarget(
            IReadOnlyList<CombatCard> targets,
            IReadOnlyList<int> initiatives = null)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            if (targets.Count == 0)
                throw new ArgumentException("Palatir needs at least one target.", nameof(targets));
            if (initiatives != null && initiatives.Count != targets.Count)
                throw new ArgumentException("Initiatives must match target count.", nameof(initiatives));

            int selectedIndex = -1;
            for (int index = 0; index < targets.Count; index++)
            {
                CombatCard target = targets[index];
                if (target == null)
                    continue;

                ClassFamily targetFamily = HeroClassFamily.Of(target.HeroClass);
                ClassFamily selectedFamily = selectedIndex < 0
                    ? default
                    : HeroClassFamily.Of(targets[selectedIndex].HeroClass);

                if (selectedIndex < 0
                    || (targetFamily == ClassFamily.Magic && selectedFamily != ClassFamily.Magic)
                    || (targetFamily == selectedFamily
                        && (target.Strength > targets[selectedIndex].Strength
                            || (target.Strength == targets[selectedIndex].Strength
                                && initiatives != null
                                && initiatives[index] > initiatives[selectedIndex]))))
                {
                    selectedIndex = index;
                }
            }

            if (selectedIndex < 0)
                throw new ArgumentException("Palatir needs at least one non-null target.", nameof(targets));
            return selectedIndex;
        }
    }
}
