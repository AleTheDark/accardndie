using System;

namespace AccardND.GameCore
{
    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random random;

        public SeededRandomSource(int seed)
        {
            random = new Random(seed);
        }

        public int NextInclusive(int minimum, int maximum)
        {
            if (minimum > maximum)
                throw new ArgumentException("Minimum cannot be greater than maximum.");

            return random.Next(minimum, maximum + 1);
        }
    }
}
