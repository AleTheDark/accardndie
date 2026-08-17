using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    public readonly struct MedusaDefenseResult
    {
        public MedusaDefenseResult(int attackerTotal, int defenseRoll, int defenseTotal, int damage, int hitPointsBefore, int hitPointsAfter)
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

    public readonly struct MedusaPetrifyingGazeResult
    {
        public MedusaPetrifyingGazeResult(IReadOnlyList<VigorRollResult> medusaRolls, IReadOnlyList<int> targetRolls, IReadOnlyList<int> targetTotals, int medusaTotal, int alliesTotal)
        {
            MedusaRolls = medusaRolls ?? Array.Empty<VigorRollResult>();
            TargetRolls = targetRolls ?? Array.Empty<int>();
            TargetTotals = targetTotals ?? Array.Empty<int>();
            MedusaTotal = medusaTotal;
            AlliesTotal = alliesTotal;
        }

        public IReadOnlyList<VigorRollResult> MedusaRolls { get; }
        public IReadOnlyList<int> TargetRolls { get; }
        public IReadOnlyList<int> TargetTotals { get; }
        public int MedusaTotal { get; }
        public int AlliesTotal { get; }
        public bool PetrifiesTargets
        {
            get
            {
                for (int index = 0; index < TargetTotals.Count; index++)
                {
                    if (PetrifiesTarget(index))
                        return true;
                }
                return false;
            }
        }

        public bool PetrifiesTarget(int index)
        {
            return index >= 0 && index < TargetTotals.Count && MedusaTotal > TargetTotals[index];
        }
    }

    public readonly struct MedusaUnpetrifyResult
    {
        public MedusaUnpetrifyResult(int roll, int requiredRoll)
        {
            Roll = roll;
            RequiredRoll = requiredRoll;
        }

        public int Roll { get; }
        public int RequiredRoll { get; }
        public bool Freed => Roll > RequiredRoll;
    }

    public sealed class MedusaBoss
    {
        public const int CardStrength = 8;
        public const int DefaultHitPoints = 50;
        public const int DefaultVigorDieSides = 6;

        private readonly IRandomSource random;

        public MedusaBoss(IRandomSource random)
            : this(random, DefaultHitPoints)
        {
        }

        public MedusaBoss(IRandomSource random, int maxHitPoints)
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

        /// <summary>
        /// Rimette il boss come l'aveva lasciato una battaglia salvata a meta'.
        /// Vedi CampaignBattleSave: il boss e' l'unico avversario che porta stato oltre
        /// la propria pedina, e senza questo riprendere una run gli ridava tutti gli HP.
        /// </summary>
        public void Restore(int hitPoints)
        {
            HitPoints = Math.Clamp(hitPoints, 0, MaxHitPoints);
        }

        public MedusaDefenseResult DefendAgainst(int attackerTotal, int defenseDieSides)
        {
            if (attackerTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(attackerTotal));
            if (defenseDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(defenseDieSides));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Medusa cannot defend.");

            int hitPointsBefore = HitPoints;
            int defenseRoll = random.NextInclusive(1, defenseDieSides);
            int defenseTotal = CardStrength + defenseRoll;
            int damage = Math.Max(0, attackerTotal - defenseTotal);
            if (damage > 0)
                HitPoints = Math.Max(0, HitPoints - damage);

            return new MedusaDefenseResult(attackerTotal, defenseRoll, defenseTotal, damage, hitPointsBefore, HitPoints);
        }

        public MedusaDefenseResult ApplyResolvedDefense(int attackerTotal, int defenseRoll, int defenseTotal)
        {
            if (attackerTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(attackerTotal));
            if (defenseRoll < 1)
                throw new ArgumentOutOfRangeException(nameof(defenseRoll));
            if (defenseTotal < 1)
                throw new ArgumentOutOfRangeException(nameof(defenseTotal));
            if (IsDefeated)
                throw new InvalidOperationException("A defeated Medusa cannot defend.");

            int hitPointsBefore = HitPoints;
            int damage = Math.Max(0, attackerTotal - defenseTotal);
            if (damage > 0)
                HitPoints = Math.Max(0, HitPoints - damage);

            return new MedusaDefenseResult(attackerTotal, defenseRoll, defenseTotal, damage, hitPointsBefore, HitPoints);
        }

        public MedusaPetrifyingGazeResult PetrifyingGaze(
            IReadOnlyList<CombatCard> targets,
            IReadOnlyList<int> targetDefenseDieSides,
            int medusaDieSides)
        {
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));
            if (targetDefenseDieSides == null)
                throw new ArgumentNullException(nameof(targetDefenseDieSides));
            if (medusaDieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(medusaDieSides));
            if (targetDefenseDieSides.Count == 0)
                throw new ArgumentException("Medusa needs at least one target.", nameof(targetDefenseDieSides));
            if (targets.Count != targetDefenseDieSides.Count)
                throw new ArgumentException("Targets and defense dice must match.", nameof(targetDefenseDieSides));

            for (int index = 0; index < targetDefenseDieSides.Count; index++)
            {
                if (targets[index] == null)
                    throw new ArgumentNullException(nameof(targets));
                if (targetDefenseDieSides[index] < 2)
                    throw new ArgumentOutOfRangeException(nameof(targetDefenseDieSides));
            }

            // Come le Supreme AOE, Medusa effettua un unico tiro offensivo contro
            // la somma dei tiri difensivi del gruppo.
            VigorRollResult medusaRoll = RollVigor(medusaDieSides, MatchupResult.Neutral);
            VigorRollResult[] medusaRolls = { medusaRoll };
            int[] targetRolls = new int[targetDefenseDieSides.Count];
            int[] targetTotals = new int[targetDefenseDieSides.Count];
            int medusaTotal = CardStrength + medusaRoll.SelectedRoll;
            int alliesTotal = 0;
            for (int index = 0; index < targetDefenseDieSides.Count; index++)
            {
                int dieSides = targetDefenseDieSides[index];
                int roll = random.NextInclusive(1, dieSides);
                targetRolls[index] = roll;
                targetTotals[index] = targets[index].Strength + roll;
                alliesTotal += roll;
            }

            return new MedusaPetrifyingGazeResult(medusaRolls, targetRolls, targetTotals, medusaTotal, alliesTotal);
        }

        public MedusaUnpetrifyResult RollUnpetrify(int dieSides)
        {
            if (dieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(dieSides));

            return new MedusaUnpetrifyResult(random.NextInclusive(1, dieSides), UnpetrifyRequiredRoll(dieSides));
        }

        public static int UnpetrifyRequiredRoll(int dieSides)
        {
            if (dieSides < 2)
                throw new ArgumentOutOfRangeException(nameof(dieSides));

            return dieSides / 2;
        }

        private VigorRollResult RollVigor(int dieSides, MatchupResult matchup)
        {
            int first = random.NextInclusive(1, dieSides);
            if (matchup == MatchupResult.Neutral)
                return new VigorRollResult(dieSides, first, 0, false, first, matchup, VigorSelectionMode.Single);

            int second = random.NextInclusive(1, dieSides);
            int selected = matchup == MatchupResult.Advantage
                ? Math.Max(first, second)
                : Math.Min(first, second);
            VigorSelectionMode mode = matchup == MatchupResult.Advantage
                ? VigorSelectionMode.Highest
                : VigorSelectionMode.Lowest;
            return new VigorRollResult(dieSides, first, second, true, selected, matchup, mode);
        }
    }
}
