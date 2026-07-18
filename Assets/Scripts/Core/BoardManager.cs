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

        /// <summary>Debug: assign exact hole cards (removes them from the deck).</summary>
        public void AssignHoleCards(PlayerState player, Suit s0, Rank r0, Suit s1, Rank r1)
        {
            if (player == null)
                return;

            player.HoleCards.Clear();
            player.HoleCards.Add(_deck.Take(s0, r0));
            player.HoleCards.Add(_deck.Take(s1, r1));
        }

        /// <summary>Debug: burn one, then reveal three exact flop cards.</summary>
        public void DealScriptedFlop(Suit s0, Rank r0, Suit s1, Rank r1, Suit s2, Rank r2)
        {
            _deck.Deal(); // burn
            CommunityCards.Add(_deck.Take(s0, r0));
            CommunityCards.Add(_deck.Take(s1, r1));
            CommunityCards.Add(_deck.Take(s2, r2));
        }

        /// <summary>Debug: burn one, then reveal an exact turn card.</summary>
        public void DealScriptedTurn(Suit suit, Rank rank)
        {
            _deck.Deal(); // burn
            CommunityCards.Add(_deck.Take(suit, rank));
        }

        /// <summary>Debug: burn one, then reveal an exact river card.</summary>
        public void DealScriptedRiver(Suit suit, Rank rank)
        {
            _deck.Deal(); // burn
            CommunityCards.Add(_deck.Take(suit, rank));
        }
    }
}
