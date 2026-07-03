using System;
using System.Collections.Generic;
using AccardND.GameData;
using AccardND.Presentation;
using UnityEngine;

namespace AccardND.Battlefield
{
    public enum BattlefieldSide
    {
        Top,
        Bottom
    }

    public readonly struct BattlefieldCardKey : IEquatable<BattlefieldCardKey>
    {
        public BattlefieldCardKey(BattlefieldSide side, int slot)
        {
            Side = side;
            Slot = slot;
        }

        public BattlefieldSide Side { get; }
        public int Slot { get; }

        public bool Equals(BattlefieldCardKey other) => Side == other.Side && Slot == other.Slot;
        public override bool Equals(object obj) => obj is BattlefieldCardKey other && Equals(other);
        public override int GetHashCode() => ((int)Side * 397) ^ Slot;
    }

    public sealed class BattlefieldCardViewState
    {
        public BattlefieldCardKey Key { get; set; }
        public CardDefinition Definition { get; set; }
        public int Strength { get; set; }
        public int Lives { get; set; }
        public int MaximumLives { get; set; } = 2;
        public bool Eliminated { get; set; }
        public bool ActiveTurn { get; set; }
        public bool Clickable { get; set; }
        public bool Inspectable { get; set; }
        public bool Selected { get; set; }
        public bool PlayerOwned { get; set; }
        public bool PlayEnterAnimation { get; set; }
        public string EmptyLabel { get; set; } = "-";
        public IReadOnlyList<PrototypeCardView.StatusToken> Statuses { get; set; } =
            Array.Empty<PrototypeCardView.StatusToken>();
    }

    public sealed class BattlefieldViewState
    {
        public int FormationSize { get; set; } = 3;
        public IReadOnlyList<BattlefieldCardViewState> TopCards { get; set; } =
            Array.Empty<BattlefieldCardViewState>();
        public IReadOnlyList<BattlefieldCardViewState> BottomCards { get; set; } =
            Array.Empty<BattlefieldCardViewState>();
    }
}
