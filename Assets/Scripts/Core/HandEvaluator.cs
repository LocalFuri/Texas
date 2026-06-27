using System;
using System.Collections.Generic;
using System.Linq;

namespace TexasHoldem
{
    public enum HandRank
    {
        HighCard      = 0,
        OnePair       = 1,
        TwoPair       = 2,
        ThreeOfAKind  = 3,
        Straight      = 4,
        Flush         = 5,
        FullHouse     = 6,
        FourOfAKind   = 7,
        StraightFlush = 8,
        RoyalFlush    = 9
    }

    public class HandResult : IComparable<HandResult>
    {
        public HandRank          Rank        { get; }
        public IReadOnlyList<int> Tiebreakers { get; }

        public HandResult(HandRank rank, List<int> tiebreakers)
        {
            Rank        = rank;
            Tiebreakers = tiebreakers.AsReadOnly();
        }

        public int CompareTo(HandResult other)
        {
            if (other == null) return 1;
            if (Rank != other.Rank) return Rank.CompareTo(other.Rank);
            for (int i = 0; i < Math.Min(Tiebreakers.Count, other.Tiebreakers.Count); i++)
            {
                int cmp = Tiebreakers[i].CompareTo(other.Tiebreakers[i]);
                if (cmp != 0) return cmp;
            }
            return 0;
        }
    }

    public static class HandEvaluator
    {
        /// <summary>Evaluates the best 5-card poker hand from 5–7 provided cards.</summary>
        public static HandResult Evaluate(List<Card> cards)
        {
            if (cards.Count < 5)
                throw new ArgumentException("At least 5 cards are required for evaluation.");

            HandResult best = null;
            foreach (var combo in GetFiveCardCombinations(cards))
            {
                var result = EvaluateFive(combo);
                if (best == null || result.CompareTo(best) > 0)
                    best = result;
            }
            return best;
        }

        private static IEnumerable<List<Card>> GetFiveCardCombinations(List<Card> cards)
        {
            int n = cards.Count;
            for (int a = 0; a < n - 4; a++)
            for (int b = a + 1; b < n - 3; b++)
            for (int c = b + 1; c < n - 2; c++)
            for (int d = c + 1; d < n - 1; d++)
            for (int e = d + 1; e < n; e++)
                yield return new List<Card> { cards[a], cards[b], cards[c], cards[d], cards[e] };
        }

        private static HandResult EvaluateFive(List<Card> five)
        {
            var ranks   = five.Select(c => (int)c.Rank).OrderByDescending(r => r).ToList();
            bool isFlush    = five.Select(c => c.Suit).Distinct().Count() == 1;
            bool isStraight = IsStraight(ranks, out int straightHigh);

            if (isFlush && isStraight)
            {
                return straightHigh == (int)Rank.Ace
                    ? new HandResult(HandRank.RoyalFlush,    new List<int> { straightHigh })
                    : new HandResult(HandRank.StraightFlush, new List<int> { straightHigh });
            }

            var groups = ranks
                .GroupBy(r => r)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .ToList();

            var counts = groups.Select(g => g.Count()).ToList();

            if (counts[0] == 4)
                return new HandResult(HandRank.FourOfAKind,
                    new List<int> { groups[0].Key, groups[1].Key });

            if (counts[0] == 3 && counts[1] == 2)
                return new HandResult(HandRank.FullHouse,
                    new List<int> { groups[0].Key, groups[1].Key });

            if (isFlush)
                return new HandResult(HandRank.Flush, ranks);

            if (isStraight)
                return new HandResult(HandRank.Straight, new List<int> { straightHigh });

            if (counts[0] == 3)
            {
                var kickers = groups.Skip(1).Select(g => g.Key).OrderByDescending(r => r).ToList();
                return new HandResult(HandRank.ThreeOfAKind,
                    new List<int> { groups[0].Key }.Concat(kickers).ToList());
            }

            if (counts[0] == 2 && counts.Count > 1 && counts[1] == 2)
            {
                int highPair = Math.Max(groups[0].Key, groups[1].Key);
                int lowPair  = Math.Min(groups[0].Key, groups[1].Key);
                return new HandResult(HandRank.TwoPair,
                    new List<int> { highPair, lowPair, groups[2].Key });
            }

            if (counts[0] == 2)
            {
                var kickers = groups.Skip(1).Select(g => g.Key).OrderByDescending(r => r).ToList();
                return new HandResult(HandRank.OnePair,
                    new List<int> { groups[0].Key }.Concat(kickers).ToList());
            }

            return new HandResult(HandRank.HighCard, ranks);
        }

        private static bool IsStraight(List<int> ranksDesc, out int highCard)
        {
            highCard = ranksDesc[0];

            bool normal = true;
            for (int i = 0; i < ranksDesc.Count - 1; i++)
                if (ranksDesc[i] - ranksDesc[i + 1] != 1) { normal = false; break; }
            if (normal) return true;

            // Wheel: A-2-3-4-5
            if (ranksDesc[0] == (int)Rank.Ace)
            {
                var wheel = ranksDesc.Skip(1).Concat(new[] { 1 }).OrderByDescending(r => r).ToList();
                bool isWheel = true;
                for (int i = 0; i < wheel.Count - 1; i++)
                    if (wheel[i] - wheel[i + 1] != 1) { isWheel = false; break; }
                if (isWheel) { highCard = 5; return true; }
            }

            return false;
        }
    }
}
