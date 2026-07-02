using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccardND.GameData
{
    [CreateAssetMenu(menuName = "Accard N' Die/Card Database", fileName = "CardDatabase")]
    public sealed class CardDatabase : ScriptableObject
    {
        [SerializeField] private CardDefinition[] cards = Array.Empty<CardDefinition>();

        public IReadOnlyList<CardDefinition> Cards => cards;

        public CardDefinition FindById(string cardId)
        {
            foreach (CardDefinition card in cards)
            {
                if (card != null && string.Equals(card.Id, cardId, StringComparison.OrdinalIgnoreCase))
                    return card;
            }

            return null;
        }

#if UNITY_EDITOR
        public void SetCards(CardDefinition[] definitions)
        {
            cards = definitions ?? Array.Empty<CardDefinition>();
        }
#endif
    }
}
