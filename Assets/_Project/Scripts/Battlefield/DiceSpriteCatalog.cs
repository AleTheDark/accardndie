using System;
using UnityEngine;

namespace AccardND.Presentation
{
    [CreateAssetMenu(menuName = "Accard N' Die/Dice Sprite Catalog", fileName = "DiceSpriteCatalog")]
    public sealed class DiceSpriteCatalog : ScriptableObject
    {
        [SerializeField] private DiceAnimationSet[] dice = Array.Empty<DiceAnimationSet>();

        public Sprite[] FindFrames(int sides)
        {
            foreach (DiceAnimationSet set in dice)
            {
                if (set.Sides == sides)
                    return set.Frames;
            }

            return Array.Empty<Sprite>();
        }

        public Sprite FindResult(int sides, int result)
        {
            foreach (DiceAnimationSet set in dice)
            {
                if (set.Sides == sides)
                    return set.FindResult(result);
            }

            return null;
        }

#if UNITY_EDITOR
        public void SetDice(DiceAnimationSet[] sets)
        {
            dice = sets ?? Array.Empty<DiceAnimationSet>();
        }
#endif
    }

    [Serializable]
    public sealed class DiceAnimationSet
    {
        [SerializeField] private int sides;
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] results = Array.Empty<Sprite>();

        public DiceAnimationSet(int sides, Sprite[] frames, Sprite[] results)
        {
            this.sides = sides;
            this.frames = frames;
            this.results = results;
        }

        public int Sides => sides;
        public Sprite[] Frames => frames;

        public Sprite FindResult(int result)
        {
            int index = result - 1;
            return index >= 0 && index < results.Length ? results[index] : null;
        }
    }
}
