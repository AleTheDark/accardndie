using System;

namespace AccardND.GameCore
{
    public sealed class CombatCard
    {
        public CombatCard(string id, string name, HeroClass heroClass, int strength)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A card must have an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A card must have a name.", nameof(name));
            if (strength < 1)
                throw new ArgumentOutOfRangeException(nameof(strength), "Strength must be positive.");

            Id = id;
            Name = name;
            HeroClass = heroClass;
            Strength = strength;
        }

        public string Id { get; }
        public string Name { get; }
        public HeroClass HeroClass { get; }
        public int Strength { get; }
    }
}
