using System;
using System.Collections.Generic;

namespace TexasHoldem
{
    /// <summary>Best 5-card hand chosen from a player's hole cards plus the board.</summary>
    public sealed class WinningHandEvaluation
    {
        public HandResult           Result    { get; }
        public IReadOnlyList<Card> BestCards { get; }

        public WinningHandEvaluation(HandResult result, List<Card> bestCards)
        {
            Result    = result;
            BestCards = bestCards;
        }

        /// <summary>
        /// Cards to glow at showdown — pair/trip/quad ranks only, plus kickers when they decided the pot.
        /// </summary>
        public static IReadOnlyList<Card> GetGlowCards(WinningHandEvaluation evaluation, bool kickerDecisive)
        {
            if (evaluation?.Result == null || evaluation.BestCards == null || evaluation.BestCards.Count == 0)
                return Array.Empty<Card>();

            HandResult result = evaluation.Result;
            IReadOnlyList<int> tb = result.Tiebreakers;
            if (tb == null || tb.Count == 0)
                return evaluation.BestCards;

            switch (result.Rank)
            {
                case HandRank.TwoPair:
                    if (kickerDecisive && tb.Count > 2)
                        return FilterByRanks(evaluation.BestCards, tb[0], tb[1], tb[2]);
                    return FilterByRanks(evaluation.BestCards, tb[0], tb[1]);

                case HandRank.OnePair:
                case HandRank.ThreeOfAKind:
                    if (kickerDecisive)
                        return evaluation.BestCards;
                    return FilterByRanks(evaluation.BestCards, tb[0]);

                case HandRank.FourOfAKind:
                    if (kickerDecisive && tb.Count > 1)
                        return FilterByRanks(evaluation.BestCards, tb[0], tb[1]);
                    return FilterByRanks(evaluation.BestCards, tb[0]);

                case HandRank.HighCard:
                    if (kickerDecisive)
                        return evaluation.BestCards;
                    return FilterByRanks(evaluation.BestCards, tb[0]);

                default:
                    return evaluation.BestCards;
            }
        }

        private static List<Card> FilterByRanks(IReadOnlyList<Card> cards, params int[] ranks)
        {
            var rankSet = new HashSet<int>(ranks);
            var matched = new List<Card>(cards.Count);

            foreach (Card card in cards)
            {
                if (card != null && rankSet.Contains((int)card.Rank))
                    matched.Add(card);
            }

            return matched;
        }

        public static bool ContainsCard(IReadOnlyList<Card> winningCards, Card card)
        {
            if (card == null || winningCards == null)
                return false;

            foreach (Card winning in winningCards)
            {
                if (winning != null && winning.Suit == card.Suit && winning.Rank == card.Rank)
                    return true;
            }

            return false;
        }
    }
}
