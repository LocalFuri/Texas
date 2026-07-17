using System.Collections.Generic;

namespace TexasHoldem
{
    [System.Flags]
    public enum PostflopDrawFlags
    {
        None                  = 0,
        FlushDraw             = 1 << 0,
        OpenEndedStraightDraw = 1 << 1,
        GutshotStraightDraw   = 1 << 2,
    }

    /// <summary>Detects common postflop draws from hero hole cards plus the visible board.</summary>
    public static class PostflopDrawDetector
    {
        public static PostflopDrawFlags Detect(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            if (communityCards == null || communityCards.Count < 3)
                return PostflopDrawFlags.None;

            if (holeCards == null || holeCards.Count < 2)
                return PostflopDrawFlags.None;

            // Made Trips+ is never a draw / semi-bluff candidate.
            if (BettingAdvisor.GetMadeHandRank(holeCards, communityCards) >= HandRank.ThreeOfAKind)
                return PostflopDrawFlags.None;

            var ranks = new HashSet<int>();
            var holeSuits = new int[4];
            var boardSuits = new int[4];

            AddRank(holeCards[0], ranks);
            AddRank(holeCards[1], ranks);
            AddSuit(holeCards[0], holeSuits);
            AddSuit(holeCards[1], holeSuits);

            foreach (Card card in communityCards)
            {
                AddRank(card, ranks);
                AddSuit(card, boardSuits);
            }

            PostflopDrawFlags flags = PostflopDrawFlags.None;
            flags |= DetectFlushDraw(holeSuits, boardSuits);
            flags |= DetectStraightDraws(ranks);
            return flags;
        }

        private static void AddRank(Card card, HashSet<int> ranks)
        {
            if (card == null)
                return;
            ranks.Add((int)card.Rank);
        }

        private static void AddSuit(Card card, int[] suitCounts)
        {
            if (card == null)
                return;
            suitCounts[(int)card.Suit]++;
        }

        /// <summary>
        /// Flush draw only if hero holds ≥1 card of the suit and hole+board reach exactly 4.
        /// Four board cards of a suit with zero hole cards of that suit is not a flush draw.
        /// Five+ of a suit is a made flush (no draw).
        /// </summary>
        private static PostflopDrawFlags DetectFlushDraw(int[] holeSuits, int[] boardSuits)
        {
            bool madeFlush = false;
            bool flushDraw = false;

            for (int s = 0; s < 4; s++)
            {
                int hole = holeSuits[s];
                int board = boardSuits[s];
                int total = hole + board;

                if (total >= 5)
                    madeFlush = true;
                else if (total == 4 && hole >= 1)
                    flushDraw = true;
            }

            if (madeFlush)
                return PostflopDrawFlags.None;

            return flushDraw ? PostflopDrawFlags.FlushDraw : PostflopDrawFlags.None;
        }

        private static PostflopDrawFlags DetectStraightDraws(HashSet<int> ranks)
        {
            if (HasMadeStraight(ranks))
                return PostflopDrawFlags.None;

            PostflopDrawFlags flags = PostflopDrawFlags.None;

            if (HasOpenEndedStraightDraw(ranks))
                flags |= PostflopDrawFlags.OpenEndedStraightDraw;
            else if (HasGutshotStraightDraw(ranks))
                flags |= PostflopDrawFlags.GutshotStraightDraw;

            return flags;
        }

        private static bool HasMadeStraight(HashSet<int> ranks)
        {
            if (HasWheel(ranks))
                return true;

            for (int high = 6; high <= (int)Rank.Ace; high++)
            {
                if (HasStraightHigh(ranks, high))
                    return true;
            }

            return false;
        }

        private static bool HasWheel(HashSet<int> ranks) =>
            ranks.Contains((int)Rank.Ace)
            && ranks.Contains((int)Rank.Two)
            && ranks.Contains((int)Rank.Three)
            && ranks.Contains((int)Rank.Four)
            && ranks.Contains((int)Rank.Five);

        private static bool HasStraightHigh(HashSet<int> ranks, int high)
        {
            for (int rank = high - 4; rank <= high; rank++)
            {
                if (!ranks.Contains(rank))
                    return false;
            }

            return true;
        }

        private static bool HasOpenEndedStraightDraw(HashSet<int> ranks)
        {
            for (int start = (int)Rank.Two; start <= (int)Rank.Jack; start++)
            {
                if (!HasRankRun(ranks, start, 4))
                    continue;

                int lowOut  = start == (int)Rank.Two ? (int)Rank.Ace : start - 1;
                int highOut = start + 4;
                if (highOut > (int)Rank.Ace)
                    continue;

                if (CompletesStraightWhenAdded(ranks, lowOut)
                    && CompletesStraightWhenAdded(ranks, highOut))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasGutshotStraightDraw(HashSet<int> ranks)
        {
            if (CountStraightTemplateMatches(WheelTemplate, ranks) == 4)
                return true;

            for (int high = 6; high <= (int)Rank.Ace; high++)
            {
                if (CountStraightHighMatches(high, ranks) == 4)
                    return true;
            }

            return false;
        }

        private static bool HasRankRun(HashSet<int> ranks, int start, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (!ranks.Contains(start + i))
                    return false;
            }

            return true;
        }

        private static bool CompletesStraightWhenAdded(HashSet<int> ranks, int addedRank)
        {
            if (addedRank < (int)Rank.Two || addedRank > (int)Rank.Ace)
                return false;

            var withAdded = new HashSet<int>(ranks) { addedRank };
            return HasMadeStraight(withAdded);
        }

        private static int CountStraightHighMatches(int high, HashSet<int> ranks)
        {
            int count = 0;
            for (int rank = high - 4; rank <= high; rank++)
            {
                if (ranks.Contains(rank))
                    count++;
            }

            return count;
        }

        private static int CountStraightTemplateMatches(int[] template, HashSet<int> ranks)
        {
            int count = 0;
            foreach (int rank in template)
            {
                if (ranks.Contains(rank))
                    count++;
            }

            return count;
        }

        private static readonly int[] WheelTemplate =
        {
            (int)Rank.Ace,
            (int)Rank.Two,
            (int)Rank.Three,
            (int)Rank.Four,
            (int)Rank.Five,
        };
    }
}
