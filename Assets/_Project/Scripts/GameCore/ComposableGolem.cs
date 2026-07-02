using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    public enum ComposableGolemForm
    {
        Iron,
        Crystal,
        Glass
    }

    public readonly struct ComposableGolemFormStats
    {
        public ComposableGolemFormStats(
            ComposableGolemForm form,
            int attack,
            int defense,
            int vigorDieSides)
        {
            if (attack < 1)
                throw new ArgumentOutOfRangeException(nameof(attack));
            if (defense < 1)
                throw new ArgumentOutOfRangeException(nameof(defense));
            if (vigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(vigorDieSides));

            Form = form;
            Attack = attack;
            Defense = defense;
            VigorDieSides = vigorDieSides;
        }

        public ComposableGolemForm Form { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int VigorDieSides { get; }
    }

    public readonly struct ComposableGolemDefenseResult
    {
        public ComposableGolemDefenseResult(
            ComposableGolemFormStats form,
            int attackerTotal,
            int vigorRoll,
            int defenseTotal,
            int damage,
            int healing,
            int hitPointsBefore,
            int hitPointsAfter)
        {
            Form = form;
            AttackerTotal = attackerTotal;
            VigorRoll = vigorRoll;
            DefenseTotal = defenseTotal;
            Damage = damage;
            Healing = healing;
            HitPointsBefore = hitPointsBefore;
            HitPointsAfter = hitPointsAfter;
        }

        public ComposableGolemFormStats Form { get; }
        public int AttackerTotal { get; }
        public int VigorRoll { get; }
        public int DefenseTotal { get; }
        public int Damage { get; }
        public int Healing { get; }
        public int HitPointsBefore { get; }
        public int HitPointsAfter { get; }
    }

    public readonly struct ComposableGolemAttackResult
    {
        public ComposableGolemAttackResult(
            ComposableGolemFormStats form,
            CombatCard target,
            int vigorRoll,
            int attackTotal,
            int targetVigorRoll,
            int targetDefenseTotal)
        {
            Form = form;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            VigorRoll = vigorRoll;
            AttackTotal = attackTotal;
            TargetVigorRoll = targetVigorRoll;
            TargetDefenseTotal = targetDefenseTotal;
        }

        public ComposableGolemFormStats Form { get; }
        public CombatCard Target { get; }
        public int VigorRoll { get; }
        public int AttackTotal { get; }
        public int TargetVigorRoll { get; }
        public int TargetDefenseTotal { get; }
        public bool TargetIsDefeated => AttackTotal > TargetDefenseTotal;
    }

    public sealed class ComposableGolem
    {
        public const int DefaultHitPoints = 30;
        public const int DefaultRoundsPerForm = 2;

        private readonly IRandomSource random;
        private readonly ComposableGolemFormStats[] forms;
        private readonly int roundsPerForm;

        private int activeFormIndex;

        public ComposableGolem(IRandomSource random)
            : this(random, DefaultHitPoints, DefaultRoundsPerForm, CreateShuffledDefaultForms(random))
        {
        }

        public ComposableGolem(
            IRandomSource random,
            int maxHitPoints,
            int roundsPerForm,
            IReadOnlyList<ComposableGolemFormStats> forms)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (maxHitPoints < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            if (roundsPerForm < 1)
                throw new ArgumentOutOfRangeException(nameof(roundsPerForm));
            if (forms == null)
                throw new ArgumentNullException(nameof(forms));
            if (forms.Count == 0)
                throw new ArgumentException("The golem needs at least one form.", nameof(forms));

            this.forms = new ComposableGolemFormStats[forms.Count];
            for (int index = 0; index < forms.Count; index++)
                this.forms[index] = forms[index];

            MaxHitPoints = maxHitPoints;
            HitPoints = maxHitPoints;
            this.roundsPerForm = roundsPerForm;
        }

        public int MaxHitPoints { get; }
        public int HitPoints { get; private set; }
        public bool IsDefeated => HitPoints <= 0;
        public int? Initiative { get; private set; }
        public int RoundsInActiveForm { get; private set; }
        public ComposableGolemFormStats ActiveForm => forms[activeFormIndex];
        public ComposableGolemFormStats NextForm => forms[(activeFormIndex + 1) % forms.Length];
        public IReadOnlyList<ComposableGolemFormStats> Forms => forms;

        public int RollInitiative(int initiativeDieSides)
        {
            if (initiativeDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(initiativeDieSides));

            if (!Initiative.HasValue)
                Initiative = random.NextInclusive(1, initiativeDieSides);
            return Initiative.Value;
        }

        public bool EndRound()
        {
            RoundsInActiveForm++;
            if (RoundsInActiveForm < roundsPerForm)
                return false;

            RoundsInActiveForm = 0;
            activeFormIndex = (activeFormIndex + 1) % forms.Length;
            return true;
        }

        public ComposableGolemDefenseResult DefendAgainst(int attackerTotal)
        {
            if (attackerTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(attackerTotal));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated golem cannot defend.");

            ComposableGolemFormStats form = ActiveForm;
            int hitPointsBefore = HitPoints;
            int vigorRoll = random.NextInclusive(1, form.VigorDieSides);
            int defenseTotal = form.Defense + vigorRoll;
            int damage = Math.Max(0, attackerTotal - defenseTotal);
            int healing = form.Form == ComposableGolemForm.Glass
                ? Math.Max(0, defenseTotal - attackerTotal)
                : 0;

            if (damage > 0)
                HitPoints = Math.Max(0, HitPoints - damage);
            else if (healing > 0)
                HitPoints = Math.Min(MaxHitPoints, HitPoints + healing);

            return new ComposableGolemDefenseResult(
                form,
                attackerTotal,
                vigorRoll,
                defenseTotal,
                damage,
                healing,
                hitPointsBefore,
                HitPoints);
        }

        public ComposableGolemAttackResult Attack(CombatCard target, int targetVigorDieSides)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetVigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(targetVigorDieSides));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated golem cannot attack.");

            ComposableGolemFormStats form = ActiveForm;
            int vigorRoll = random.NextInclusive(1, form.VigorDieSides);
            int targetVigorRoll = random.NextInclusive(1, targetVigorDieSides);

            return new ComposableGolemAttackResult(
                form,
                target,
                vigorRoll,
                form.Attack + vigorRoll,
                targetVigorRoll,
                target.Strength + targetVigorRoll);
        }

        public static int SelectHighestStrengthTarget(
            IReadOnlyList<CombatCard> targets,
            IReadOnlyList<int> initiatives = null)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            if (targets.Count == 0)
                throw new ArgumentException("The golem needs at least one target.", nameof(targets));
            if (initiatives != null && initiatives.Count != targets.Count)
                throw new ArgumentException("Initiatives must match target count.", nameof(initiatives));

            int selectedIndex = -1;
            for (int index = 0; index < targets.Count; index++)
            {
                CombatCard target = targets[index];
                if (target == null)
                    continue;

                if (selectedIndex < 0
                    || target.Strength > targets[selectedIndex].Strength
                    || (target.Strength == targets[selectedIndex].Strength
                        && initiatives != null
                        && initiatives[index] > initiatives[selectedIndex]))
                {
                    selectedIndex = index;
                }
            }

            if (selectedIndex < 0)
                throw new ArgumentException("The golem needs at least one non-null target.", nameof(targets));
            return selectedIndex;
        }

        public static ComposableGolemFormStats CreateIronStats()
        {
            return new ComposableGolemFormStats(ComposableGolemForm.Iron, attack: 8, defense: 5, vigorDieSides: 6);
        }

        public static ComposableGolemFormStats CreateCrystalStats()
        {
            return new ComposableGolemFormStats(ComposableGolemForm.Crystal, attack: 6, defense: 4, vigorDieSides: 10);
        }

        public static ComposableGolemFormStats CreateGlassStats()
        {
            return new ComposableGolemFormStats(ComposableGolemForm.Glass, attack: 5, defense: 6, vigorDieSides: 8);
        }

        private static ComposableGolemFormStats[] CreateShuffledDefaultForms(IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            ComposableGolemFormStats[] result =
            {
                CreateIronStats(),
                CreateCrystalStats(),
                CreateGlassStats()
            };

            for (int index = result.Length - 1; index > 0; index--)
            {
                int otherIndex = random.NextInclusive(0, index);
                (result[index], result[otherIndex]) = (result[otherIndex], result[index]);
            }

            return result;
        }
    }
}
