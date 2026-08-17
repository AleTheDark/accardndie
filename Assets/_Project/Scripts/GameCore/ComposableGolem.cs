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
            int basePower,
            int vigorDieSides,
            int powerBonus = 0)
        {
            if (basePower < 1)
                throw new ArgumentOutOfRangeException(nameof(basePower));
            if (vigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(vigorDieSides));
            if (powerBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(powerBonus));

            Form = form;
            BasePower = basePower;
            VigorDieSides = vigorDieSides;
            PowerBonus = powerBonus;
        }

        public ComposableGolemForm Form { get; }
        public int BasePower { get; }
        public int PowerBonus { get; }
        public int Power => BasePower + PowerBonus;
        public int VigorDieSides { get; }

        public ComposableGolemFormStats AddPowerBonus(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            return new ComposableGolemFormStats(Form, BasePower, VigorDieSides, PowerBonus + amount);
        }
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
            : this(form, form.VigorDieSides, attackerTotal, vigorRoll, defenseTotal, damage, healing, hitPointsBefore, hitPointsAfter)
        {
        }

        public ComposableGolemDefenseResult(
            ComposableGolemFormStats form,
            int vigorDieSides,
            int attackerTotal,
            int vigorRoll,
            int defenseTotal,
            int damage,
            int healing,
            int hitPointsBefore,
            int hitPointsAfter)
        {
            Form = form;
            VigorDieSides = vigorDieSides;
            AttackerTotal = attackerTotal;
            VigorRoll = vigorRoll;
            DefenseTotal = defenseTotal;
            Damage = damage;
            Healing = healing;
            HitPointsBefore = hitPointsBefore;
            HitPointsAfter = hitPointsAfter;
        }

        public ComposableGolemFormStats Form { get; }
        public int VigorDieSides { get; }
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
            : this(form, form.VigorDieSides, target, vigorRoll, attackTotal, targetVigorRoll, targetDefenseTotal)
        {
        }

        public ComposableGolemAttackResult(
            ComposableGolemFormStats form,
            int vigorDieSides,
            CombatCard target,
            int vigorRoll,
            int attackTotal,
            int targetVigorRoll,
            int targetDefenseTotal)
        {
            Form = form;
            VigorDieSides = vigorDieSides;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            VigorRoll = vigorRoll;
            AttackTotal = attackTotal;
            TargetVigorRoll = targetVigorRoll;
            TargetDefenseTotal = targetDefenseTotal;
        }

        public ComposableGolemFormStats Form { get; }
        public int VigorDieSides { get; }
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
            : this(random, maxHitPoints, maxHitPoints, roundsPerForm, forms)
        {
        }

        public ComposableGolem(
            IRandomSource random,
            int maxHitPoints,
            int currentHitPoints,
            int roundsPerForm,
            IReadOnlyList<ComposableGolemFormStats> forms)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (maxHitPoints < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHitPoints));
            if (currentHitPoints < 0 || currentHitPoints > maxHitPoints)
                throw new ArgumentOutOfRangeException(nameof(currentHitPoints));
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
            HitPoints = currentHitPoints;
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

        /// <summary>
        /// Rimette il golem come l'aveva lasciato una battaglia salvata a meta'. Gli HP e
        /// le forme (con i bonus di potenza accumulati) arrivano dal costruttore; qui
        /// restano il punto del ciclo e il dado d'iniziativa gia' tirato, che deve restare
        /// quello - e' il numero che il giocatore ha in campo davanti agli occhi.
        /// </summary>
        public void Restore(int activeForm, int roundsInActiveForm, int? initiative)
        {
            activeFormIndex = forms.Length == 0 ? 0 : ((activeForm % forms.Length) + forms.Length) % forms.Length;
            RoundsInActiveForm = Math.Clamp(roundsInActiveForm, 0, roundsPerForm);
            Initiative = initiative;
        }

        /// <summary>Indice della forma attiva: serve a salvarla e a ritrovarla identica.</summary>
        public int ActiveFormIndex => activeFormIndex;

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
            if (activeFormIndex == 0)
            {
                for (int index = 0; index < forms.Length; index++)
                    forms[index] = forms[index].AddPowerBonus(1);
            }
            return true;
        }

        public ComposableGolemDefenseResult DefendAgainst(int attackerTotal)
        {
            return DefendAgainst(attackerTotal, ActiveForm.VigorDieSides);
        }

        public ComposableGolemDefenseResult DefendAgainst(int attackerTotal, int vigorDieSides)
        {
            return DefendAgainst(attackerTotal, vigorDieSides, powerModifier: 0);
        }

        public ComposableGolemDefenseResult DefendAgainst(
            int attackerTotal,
            int vigorDieSides,
            int powerModifier)
        {
            if (vigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(vigorDieSides));

            return DefendAgainstRoll(
                attackerTotal,
                vigorDieSides,
                random.NextInclusive(1, vigorDieSides),
                powerModifier);
        }

        public ComposableGolemDefenseResult DefendAgainstRoll(
            int attackerTotal,
            int vigorDieSides,
            int vigorRoll,
            int powerModifier = 0)
        {
            if (attackerTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(attackerTotal));
            if (vigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(vigorDieSides));
            if (vigorRoll < 1 || vigorRoll > vigorDieSides)
                throw new ArgumentOutOfRangeException(nameof(vigorRoll));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated golem cannot defend.");

            ComposableGolemFormStats form = ActiveForm;
            int hitPointsBefore = HitPoints;
            int defenseTotal = form.Power + powerModifier + vigorRoll;
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
                vigorDieSides,
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
            return Attack(target, targetVigorDieSides, ActiveForm.VigorDieSides);
        }

        public ComposableGolemAttackResult Attack(
            CombatCard target,
            int targetVigorDieSides,
            int vigorDieSides)
        {
            return Attack(target, targetVigorDieSides, vigorDieSides, powerModifier: 0);
        }

        public ComposableGolemAttackResult Attack(
            CombatCard target,
            int targetVigorDieSides,
            int vigorDieSides,
            int powerModifier)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (targetVigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(targetVigorDieSides));
            if (vigorDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(vigorDieSides));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated golem cannot attack.");

            ComposableGolemFormStats form = ActiveForm;
            int vigorRoll = random.NextInclusive(1, vigorDieSides);
            int targetVigorRoll = random.NextInclusive(1, targetVigorDieSides);

            return new ComposableGolemAttackResult(
                form,
                vigorDieSides,
                target,
                vigorRoll,
                form.Power + powerModifier + vigorRoll,
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
            return new ComposableGolemFormStats(ComposableGolemForm.Iron, basePower: 8, vigorDieSides: 6);
        }

        public static ComposableGolemFormStats CreateCrystalStats()
        {
            return new ComposableGolemFormStats(ComposableGolemForm.Crystal, basePower: 6, vigorDieSides: 10);
        }

        public static ComposableGolemFormStats CreateGlassStats()
        {
            return new ComposableGolemFormStats(ComposableGolemForm.Glass, basePower: 5, vigorDieSides: 8);
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
