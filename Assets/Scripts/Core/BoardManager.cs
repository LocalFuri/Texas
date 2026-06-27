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

        /// <summary>Deals two hole cards to each player, one card at a time around the table.</summary>
        public void DealHoleCards(List<PlayerState> players)
        {
            foreach (var player in players)
                player.HoleCards.Clear();

            for (int round = 0; round < 2; round++)
                foreach (var player in players)
                    player.HoleCards.Add(_deck.Deal());
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
