using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    public enum FlashTrialResult
    {
        Perfect,
        Excellent,
        Good,
        Completed,
        Failed,
        Forfeited
    }

    public enum FlashTrialMemoryInputResult
    {
        AwaitingInput,
        RoundCompleted,
        Failed,
        Perfect
    }

    /// <summary>
    /// Logica pura del gioco di memoria della Prova Lampo. Non conosce UI, animazioni o
    /// coroutine: la stessa classe puo' essere usata dalla stanza reale e dalla scena debug.
    /// </summary>
    public sealed class FlashTrialMemoryGame
    {
        private static readonly HeroClass[] DefaultClasses =
        {
            HeroClass.Assassin,
            HeroClass.Warrior,
            HeroClass.Mage,
            HeroClass.Paladin,
            HeroClass.Rogue,
            HeroClass.Hunter,
            HeroClass.Barbarian,
            HeroClass.Necromancer,
            HeroClass.Priest
        };

        private readonly Random random;
        private readonly HeroClass[] availableClasses;
        private readonly List<HeroClass> sequence = new List<HeroClass>();
        private int inputIndex;

        public FlashTrialMemoryGame(int seed, int maximumLevels = 8, IReadOnlyList<HeroClass> classes = null)
        {
            if (maximumLevels < 1) throw new ArgumentOutOfRangeException(nameof(maximumLevels));
            MaximumLevels = maximumLevels;
            random = new Random(seed);

            IReadOnlyList<HeroClass> source = classes ?? DefaultClasses;
            if (source.Count == 0) throw new ArgumentException("Serve almeno una classe.", nameof(classes));
            availableClasses = new HeroClass[source.Count];
            for (int index = 0; index < source.Count; index++)
                availableClasses[index] = source[index];
        }

        public int MaximumLevels { get; }
        public int CompletedLevels { get; private set; }
        public int ExpectedInputIndex => inputIndex;
        public bool IsFinished { get; private set; }
        public IReadOnlyList<HeroClass> Sequence => sequence;

        public IReadOnlyList<HeroClass> BeginNextRound()
        {
            if (IsFinished) throw new InvalidOperationException("La prova e' gia' terminata.");
            if (sequence.Count > 0 && inputIndex < sequence.Count)
                throw new InvalidOperationException("Il round corrente non e' ancora completo.");
            if (sequence.Count >= MaximumLevels)
                throw new InvalidOperationException("E' gia' stato raggiunto il livello massimo.");

            sequence.Add(availableClasses[random.Next(availableClasses.Length)]);
            inputIndex = 0;
            return sequence;
        }

        public FlashTrialMemoryInputResult Submit(HeroClass selectedClass)
        {
            if (IsFinished) throw new InvalidOperationException("La prova e' gia' terminata.");
            if (sequence.Count == 0 || inputIndex >= sequence.Count)
                throw new InvalidOperationException("Nessun round attende input.");

            if (selectedClass != sequence[inputIndex])
            {
                IsFinished = true;
                return FlashTrialMemoryInputResult.Failed;
            }

            inputIndex++;
            if (inputIndex < sequence.Count)
                return FlashTrialMemoryInputResult.AwaitingInput;

            CompletedLevels = sequence.Count;
            if (CompletedLevels >= MaximumLevels)
            {
                IsFinished = true;
                return FlashTrialMemoryInputResult.Perfect;
            }
            return FlashTrialMemoryInputResult.RoundCompleted;
        }

        public FlashTrialResult FinishForInactivity()
        {
            IsFinished = true;
            return Evaluate(CompletedLevels, MaximumLevels);
        }

        public static FlashTrialResult Evaluate(int completedLevels, int maximumLevels = 8)
        {
            if (maximumLevels < 1) throw new ArgumentOutOfRangeException(nameof(maximumLevels));
            completedLevels = Math.Max(0, Math.Min(completedLevels, maximumLevels));
            if (completedLevels >= maximumLevels) return FlashTrialResult.Perfect;
            if (completedLevels >= 6) return FlashTrialResult.Excellent;
            if (completedLevels >= 4) return FlashTrialResult.Good;
            if (completedLevels >= 2) return FlashTrialResult.Completed;
            return FlashTrialResult.Failed;
        }
    }
}
