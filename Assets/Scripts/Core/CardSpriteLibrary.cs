using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    [Serializable]
    public struct CardSpriteEntry
    {
        public Suit suit;
        public Rank rank;
        public Sprite sprite;
    }

    /// <summary>Maps every Card to its face sprite and holds the shared card-back sprite.</summary>
    [CreateAssetMenu(fileName = "CardSpriteLibrary", menuName = "TexasHoldem/Card Sprite Library")]
    public class CardSpriteLibrary : ScriptableObject
    {
        [SerializeField] private List<CardSpriteEntry> _entries = new List<CardSpriteEntry>();
        [SerializeField] private Sprite _cardBack;

        public Sprite CardBack => _cardBack;

        /// <summary>Returns the face sprite for the given card, or null if not found.</summary>
        public Sprite GetSprite(Card card)
        {
            foreach (var entry in _entries)
                if (entry.suit == card.Suit && entry.rank == card.Rank)
                    return entry.sprite;

            Debug.LogWarning($"CardSpriteLibrary: No sprite found for {card.Rank} of {card.Suit}.");
            return null;
        }
    }
}
