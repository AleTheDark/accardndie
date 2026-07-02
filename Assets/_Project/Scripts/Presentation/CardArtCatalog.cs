using System;
using UnityEngine;

namespace AccardND.Presentation
{
    public sealed class CardArtCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] cards = Array.Empty<Sprite>();

        public Sprite Find(string cardName)
        {
            foreach (Sprite card in cards)
            {
                if (card != null && string.Equals(card.name, cardName, StringComparison.OrdinalIgnoreCase))
                    return card;
            }

            return null;
        }

#if UNITY_EDITOR
        public void SetCards(Sprite[] sprites)
        {
            cards = sprites ?? Array.Empty<Sprite>();
        }
#endif
    }
}
