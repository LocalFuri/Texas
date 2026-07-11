using System.Collections.Generic;

namespace TexasHoldem
{
    public class BoardManager
    {
        private readonly Deck _deck = new Deck();

        public List<Card> CommunityCards { get; } = new List<Card>();

        /// <summary>Prepares a freshly shuffled deck and clears the board.</summary>
        public void NewDeck()
        {
            _deck.Initialize();
            _deck.Shuffle();
            CommunityCards.Clear();
        }

        /// <summary>Clears hole cards for every seated player in the list.</summary>
        public void ClearHoleCards(IEnumerable<PlayerState> players)
        {
            foreach (var player in players)
                player.HoleCards.Clear();
        }

        /// <summary>Deals one hole card to a player from the deck.</summary>
        public Card DealHoleCardTo(PlayerState player)
        {
            Card card = _deck.Deal();
            player.HoleCards.Add(card);
            return card;
        }

        /// <summary>
        /// Deals two hole cards to each player, one card at a time clockwise from
        /// <paramref name="startIndex"/> (small blind in active-player order).
        /// </summary>
        public void DealHoleCards(List<PlayerState> players, int startIndex = 0)
        {
            ClearHoleCards(players);

            int count = players.Count;
            if (count == 0)
                return;

            startIndex = ((startIndex % count) + count) % count;

            for (int round = 0; round < 2; round++)
            {
                for (int i = 0; i < count; i++)
                    DealHoleCardTo(players[(startIndex + i) % count]);
            }
        }

        /// <summary>Burns one card then reveals the three flop cards.</summary>
        public void DealFlop()
        {
            _deck.Deal(); // burn
            for (int i = 0; i < 3; i++)
                CommunityCards.Add(_deck.Deal());
        }

        /// <summary>Burns one card then reveals the turn.</summary>
        public void DealTurn()
        {
            _deck.Deal(); // burn
            CommunityCards.Add(_deck.Deal());
        }

        /// <summary>Burns one card then reveals the river.</summary>
        public void DealRiver()
        {
            _deck.Deal(); // burn
            CommunityCards.Add(_deck.Deal());
        }
    }
}
