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

            var ranks = new HashSet<int>();
            var suitCounts = new int[4];

            AddCard(holeCards[0], ranks, suitCounts);
            AddCard(holeCards[1], ranks, suitCounts);
            foreach (Card card in communityCards)
                AddCard(card, ranks, suitCounts);

            PostflopDrawFlags flags = PostflopDrawFlags.None;
            flags |= DetectFlushDraw(suitCounts);
            flags |= DetectStraightDraws(ranks);
            return flags;
        }

        private static void AddCard(Card card, HashSet<int> ranks, int[] suitCounts)
        {
            if (card == null)
                return;

            ranks.Add((int)card.Rank);
            suitCounts[(int)card.Suit]++;
        }

        private static PostflopDrawFlags DetectFlushDraw(int[] suitCounts)
        {
            bool madeFlush = false;
            bool flushDraw = false;

            for (int i = 0; i < suitCounts.Length; i++)
            {
                if (suitCounts[i] >= 5)
                    madeFlush = true;
                if (suitCounts[i] == 4)
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
