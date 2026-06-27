using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public class Deck
    {
        private readonly List<Card> _cards = new List<Card>();

        public int Count => _cards.Count;

        /// <summary>Initializes a standard 52-card deck.</summary>
        public void Initialize()
        {
            _cards.Clear();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    _cards.Add(new Card(suit, rank));
        }

        /// <summary>Shuffles the deck using the Fisher-Yates algorithm.</summary>
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        /// <summary>Deals and removes the top card from the deck.</summary>
        public Card Deal()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Cannot deal from an empty deck.");
            Card card = _cards[0];
            _cards.RemoveAt(0);
            return card;
        }
    }
}
