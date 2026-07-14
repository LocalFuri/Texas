using System;
using System.Collections.Generic;

namespace TexasHoldem
{
    [Flags]
    public enum BoardTextureFlags
    {
        None         = 0,
        Paired       = 1 << 0,
        TwoPair      = 1 << 1,
        Trips        = 1 << 2,
        ThreeFlush   = 1 << 3,
        FourFlush    = 1 << 4,
        Connected    = 1 << 5,
        FourStraight = 1 << 6,
    }

    /// <summary>Classifies community-card board texture (no hole cards).</summary>
    public static class BoardTextureAnalyzer
    {
        private static readonly int[] WheelTemplate =
        {
            (int)Rank.Ace,
            (int)Rank.Two,
            (int)Rank.Three,
            (int)Rank.Four,
            (int)Rank.Five,
        };

        public static BoardTextureFlags Analyze(IReadOnlyList<Card> communityCards)
        {
            if (communityCards == null)
                return BoardTextureFlags.None;

            var rankCounts = new int[15];
            var suitCounts = new int[4];
            var seen       = new HashSet<(Suit, Rank)>();
            int validCount = 0;

            foreach (Card card in communityCards)
            {
                if (card == null)
                    continue;

                if (!seen.Add((card.Suit, card.Rank)))
                    throw new ArgumentException("Duplicate community card.", nameof(communityCards));

                validCount++;
                rankCounts[(int)card.Rank]++;
                suitCounts[(int)card.Suit]++;
            }

            if (validCount < 3)
                return BoardTextureFlags.None;

            var uniqueRanks = new HashSet<int>();
            for (int rank = (int)Rank.Two; rank <= (int)Rank.Ace; rank++)
            {
                if (rankCounts[rank] > 0)
                    uniqueRanks.Add(rank);
            }

            BoardTextureFlags flags = BoardTextureFlags.None;
            flags |= ClassifyRankTexture(rankCounts);
            flags |= ClassifyFlushTexture(suitCounts);

            if (IsConnected(uniqueRanks))
                flags |= BoardTextureFlags.Connected;

            if (IsFourStraight(uniqueRanks))
                flags |= BoardTextureFlags.FourStraight;

            return flags;
        }

        private static BoardTextureFlags ClassifyRankTexture(int[] rankCounts)
        {
            int maxRankCount = 0;
            int pairRankCount = 0;

            for (int rank = (int)Rank.Two; rank <= (int)Rank.Ace; rank++)
            {
                int count = rankCounts[rank];
                if (count > maxRankCount)
                    maxRankCount = count;

                if (count == 2)
                    pairRankCount++;
            }

            if (maxRankCount >= 3)
                return BoardTextureFlags.Trips;

            if (pairRankCount >= 2)
                return BoardTextureFlags.TwoPair;

            if (pairRankCount == 1)
                return BoardTextureFlags.Paired;

            return BoardTextureFlags.None;
        }

        private static BoardTextureFlags ClassifyFlushTexture(int[] suitCounts)
        {
            int maxSuitCount = 0;
            for (int i = 0; i < suitCounts.Length; i++)
            {
                if (suitCounts[i] > maxSuitCount)
                    maxSuitCount = suitCounts[i];
            }

            if (maxSuitCount >= 4)
                return BoardTextureFlags.FourFlush;

            if (maxSuitCount == 3)
                return BoardTextureFlags.ThreeFlush;

            return BoardTextureFlags.None;
        }

        private static bool IsConnected(HashSet<int> uniqueRanks)
        {
            int runHigh = LongestConsecutiveRun(uniqueRanks);
            int runLow  = LongestConsecutiveRun(MapAceLow(uniqueRanks));
            return Math.Max(runHigh, runLow) >= 3;
        }

        private static HashSet<int> MapAceLow(HashSet<int> ranks)
        {
            var mapped = new HashSet<int>(ranks.Count);
            foreach (int rank in ranks)
                mapped.Add(rank == (int)Rank.Ace ? 1 : rank);

            return mapped;
        }

        private static int LongestConsecutiveRun(HashSet<int> ranks)
        {
            if (ranks.Count == 0)
                return 0;

            var sorted = new List<int>(ranks);
            sorted.Sort();

            int longest = 1;
            int current = 1;

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == sorted[i - 1] + 1)
                {
                    current++;
                }
                else
                {
                    if (current > longest)
                        longest = current;

                    current = 1;
                }
            }

            return Math.Max(longest, current);
        }

        private static bool IsFourStraight(HashSet<int> ranks)
        {
            if (HasMadeStraight(ranks))
                return false;

            if (CountStraightTemplateMatches(WheelTemplate, ranks) == 4)
                return true;

            for (int high = 6; high <= (int)Rank.Ace; high++)
            {
                if (CountStraightHighMatches(high, ranks) == 4)
                    return true;
            }

            return false;
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
    }
}
