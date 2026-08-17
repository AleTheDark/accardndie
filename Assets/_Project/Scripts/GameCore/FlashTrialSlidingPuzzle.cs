using System;
using System.Collections.Generic;

namespace AccardND.GameCore
{
    /// <summary>Modello puro dell'8-puzzle. Lo shuffle applica solo mosse valide.</summary>
    public sealed class FlashTrialSlidingPuzzle
    {
        private readonly Random random;
        private readonly int[] cells = { 0, 1, 2, 3, 4, 5, 6, 7, -1 };
        private int emptyIndex = 8;

        public FlashTrialSlidingPuzzle(int seed)
        {
            random = new Random(seed);
        }

        public IReadOnlyList<int> Cells => cells;
        public int EmptyIndex => emptyIndex;
        public int Moves { get; private set; }
        public bool IsSolved
        {
            get
            {
                for (int index = 0; index < 8; index++)
                    if (cells[index] != index) return false;
                return cells[8] == -1;
            }
        }

        public void Shuffle(int validMoves = 50)
        {
            if (validMoves < 1) throw new ArgumentOutOfRangeException(nameof(validMoves));
            int previousEmpty = -1;
            for (int move = 0; move < validMoves; move++)
            {
                List<int> candidates = AdjacentIndices(emptyIndex);
                if (candidates.Count > 1)
                    candidates.Remove(previousEmpty);
                int tileIndex = candidates[random.Next(candidates.Count)];
                previousEmpty = emptyIndex;
                SwapWithEmpty(tileIndex);
            }
            Moves = 0;
            if (IsSolved)
                Shuffle(validMoves + 1);
        }

        public bool TryMove(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= cells.Length || !IsAdjacent(cellIndex, emptyIndex))
                return false;
            SwapWithEmpty(cellIndex);
            Moves++;
            return true;
        }

        public static FlashTrialResult Evaluate(float seconds, int moves)
        {
            FlashTrialResult time = seconds <= 45f ? FlashTrialResult.Perfect
                : seconds <= 75f ? FlashTrialResult.Excellent
                : seconds <= 120f ? FlashTrialResult.Good
                : FlashTrialResult.Completed;
            FlashTrialResult movement = moves <= 60 ? FlashTrialResult.Perfect
                : moves <= 100 ? FlashTrialResult.Excellent
                : moves <= 150 ? FlashTrialResult.Good
                : FlashTrialResult.Completed;
            return (FlashTrialResult)Math.Max((int)time, (int)movement);
        }

        private void SwapWithEmpty(int tileIndex)
        {
            cells[emptyIndex] = cells[tileIndex];
            cells[tileIndex] = -1;
            emptyIndex = tileIndex;
        }

        private static List<int> AdjacentIndices(int index)
        {
            var result = new List<int>(4);
            int row = index / 3;
            int column = index % 3;
            if (row > 0) result.Add(index - 3);
            if (row < 2) result.Add(index + 3);
            if (column > 0) result.Add(index - 1);
            if (column < 2) result.Add(index + 1);
            return result;
        }

        private static bool IsAdjacent(int left, int right)
        {
            int rowDistance = Math.Abs(left / 3 - right / 3);
            int columnDistance = Math.Abs(left % 3 - right % 3);
            return rowDistance + columnDistance == 1;
        }
    }
}
