using System;

namespace TexasHoldem
{
    /// <summary>Allocation-free hand strength for Monte Carlo hot paths.</summary>
    public readonly struct HandScore : IComparable<HandScore>
    {
        public HandRank Rank { get; }
        public int K0 { get; }
        public int K1 { get; }
        public int K2 { get; }
        public int K3 { get; }
        public int K4 { get; }

        public HandScore(HandRank rank, int k0, int k1 = 0, int k2 = 0, int k3 = 0, int k4 = 0)
        {
            Rank = rank;
            K0   = k0;
            K1   = k1;
            K2   = k2;
            K3   = k3;
            K4   = k4;
        }

        public int CompareTo(HandScore other)
        {
            if (Rank != other.Rank)
                return Rank.CompareTo(other.Rank);

            int cmp = K0.CompareTo(other.K0);
            if (cmp != 0) return cmp;
            cmp = K1.CompareTo(other.K1);
            if (cmp != 0) return cmp;
            cmp = K2.CompareTo(other.K2);
            if (cmp != 0) return cmp;
            cmp = K3.CompareTo(other.K3);
            if (cmp != 0) return cmp;
            return K4.CompareTo(other.K4);
        }

        public override string ToString() =>
            $"{Rank}({K0},{K1},{K2},{K3},{K4})";
    }

    public static class HandEvaluatorFast
    {
        private static readonly int[] CountBuffer = new int[15];

        private const int RankTwo = 2;
        private const int RankAce = 14;
        private const int WheelMask =
            (1 << 14) | (1 << 5) | (1 << 4) | (1 << 3) | (1 << 2);

        /// <summary>Best five-card hand from seven cards (direct, non-allocating).</summary>
        public static HandScore EvaluateSeven(Card h0, Card h1, Card b0, Card b1, Card b2, Card b3, Card b4)
        {
            Span<int> rankCount = stackalloc int[15];
            Span<int> suitCount = stackalloc int[4];
            Span<int> suitMask  = stackalloc int[4];
            int rankMask = 0;

            Accumulate(h0, rankCount, suitCount, suitMask, ref rankMask);
            Accumulate(h1, rankCount, suitCount, suitMask, ref rankMask);
            Accumulate(b0, rankCount, suitCount, suitMask, ref rankMask);
            Accumulate(b1, rankCount, suitCount, suitMask, ref rankMask);
            Accumulate(b2, rankCount, suitCount, suitMask, ref rankMask);
            Accumulate(b3, rankCount, suitCount, suitMask, ref rankMask);
            Accumulate(b4, rankCount, suitCount, suitMask, ref rankMask);

            int flushSuit = -1;
            for (int s = 0; s < 4; s++)
            {
                if (suitCount[s] >= 5)
                {
                    flushSuit = s;
                    break;
                }
            }

            if (flushSuit >= 0)
            {
                int flushBits = suitMask[flushSuit];
                if (TryStraightHighFromMask(flushBits, out int sfHigh))
                {
                    return sfHigh == RankAce
                        ? new HandScore(HandRank.RoyalFlush, sfHigh)
                        : new HandScore(HandRank.StraightFlush, sfHigh);
                }
            }

            int quad = 0;
            int trips1 = 0, trips2 = 0;
            int pair1 = 0, pair2 = 0, pair3 = 0;
            for (int rank = RankAce; rank >= RankTwo; rank--)
            {
                int c = rankCount[rank];
                if (c == 4)
                    quad = rank;
                else if (c == 3)
                {
                    if (trips1 == 0) trips1 = rank;
                    else if (trips2 == 0) trips2 = rank;
                }
                else if (c == 2)
                {
                    if (pair1 == 0) pair1 = rank;
                    else if (pair2 == 0) pair2 = rank;
                    else if (pair3 == 0) pair3 = rank;
                }
            }

            if (quad != 0)
            {
                int kicker = HighestRankExcept(rankCount, quad);
                return new HandScore(HandRank.FourOfAKind, quad, kicker);
            }

            // Full house: best trips + best remaining pair (pair or second trips).
            if (trips1 != 0)
            {
                int housePair = 0;
                if (trips2 != 0)
                    housePair = trips2;
                else if (pair1 != 0)
                    housePair = pair1;

                if (housePair != 0)
                    return new HandScore(HandRank.FullHouse, trips1, housePair);
            }

            if (flushSuit >= 0)
            {
                TopFiveRanks(suitMask[flushSuit], out int f0, out int f1, out int f2, out int f3, out int f4);
                return new HandScore(HandRank.Flush, f0, f1, f2, f3, f4);
            }

            if (TryStraightHighFromMask(rankMask, out int straightHigh))
                return new HandScore(HandRank.Straight, straightHigh);

            if (trips1 != 0)
            {
                HighestTwoExcept(rankCount, trips1, out int k1, out int k2);
                return new HandScore(HandRank.ThreeOfAKind, trips1, k1, k2);
            }

            if (pair1 != 0 && pair2 != 0)
            {
                // pair1 >= pair2 >= pair3 by scan order.
                int kicker = HighestRankExcept(rankCount, pair1, pair2);
                return new HandScore(HandRank.TwoPair, pair1, pair2, kicker);
            }

            if (pair1 != 0)
            {
                HighestThreeExcept(rankCount, pair1, out int k1, out int k2, out int k3);
                return new HandScore(HandRank.OnePair, pair1, k1, k2, k3);
            }

            TopFiveRanks(rankMask, out int h0r, out int h1r, out int h2r, out int h3r, out int h4r);
            return new HandScore(HandRank.HighCard, h0r, h1r, h2r, h3r, h4r);
        }

        /// <summary>Original best-of-21 evaluator (correctness / benchmark reference).</summary>
        internal static HandScore EvaluateSevenReference(
            Card h0, Card h1, Card b0, Card b1, Card b2, Card b3, Card b4)
        {
            HandScore best = default;
            bool      hasBest = false;

            for (int a = 0; a < 3; a++)
            for (int b = a + 1; b < 4; b++)
            for (int c = b + 1; c < 5; c++)
            for (int d = c + 1; d < 6; d++)
            for (int e = d + 1; e < 7; e++)
            {
                HandScore score = EvaluateFive(Pick(a, h0, h1, b0, b1, b2, b3, b4),
                    Pick(b, h0, h1, b0, b1, b2, b3, b4),
                    Pick(c, h0, h1, b0, b1, b2, b3, b4),
                    Pick(d, h0, h1, b0, b1, b2, b3, b4),
                    Pick(e, h0, h1, b0, b1, b2, b3, b4));

                if (!hasBest || score.CompareTo(best) > 0)
                {
                    best    = score;
                    hasBest = true;
                }
            }

            return best;
        }

        private static void Accumulate(
            Card card,
            Span<int> rankCount,
            Span<int> suitCount,
            Span<int> suitMask,
            ref int rankMask)
        {
            int rank = (int)card.Rank;
            int suit = (int)card.Suit;
            rankCount[rank]++;
            suitCount[suit]++;
            int bit = 1 << rank;
            suitMask[suit] |= bit;
            rankMask |= bit;
        }

        private static bool TryStraightHighFromMask(int mask, out int highCard)
        {
            for (int high = RankAce; high >= 6; high--)
            {
                int need = (1 << high) | (1 << (high - 1)) | (1 << (high - 2))
                         | (1 << (high - 3)) | (1 << (high - 4));
                if ((mask & need) == need)
                {
                    highCard = high;
                    return true;
                }
            }

            if ((mask & WheelMask) == WheelMask)
            {
                highCard = 5;
                return true;
            }

            highCard = 0;
            return false;
        }

        private static void TopFiveRanks(
            int mask, out int r0, out int r1, out int r2, out int r3, out int r4)
        {
            r0 = r1 = r2 = r3 = r4 = 0;
            int found = 0;
            for (int rank = RankAce; rank >= RankTwo && found < 5; rank--)
            {
                if ((mask & (1 << rank)) == 0)
                    continue;

                switch (found)
                {
                    case 0: r0 = rank; break;
                    case 1: r1 = rank; break;
                    case 2: r2 = rank; break;
                    case 3: r3 = rank; break;
                    case 4: r4 = rank; break;
                }

                found++;
            }
        }

        private static int HighestRankExcept(Span<int> rankCount, int excl0, int excl1 = 0)
        {
            for (int rank = RankAce; rank >= RankTwo; rank--)
            {
                if (rank == excl0 || rank == excl1)
                    continue;
                if (rankCount[rank] > 0)
                    return rank;
            }

            return 0;
        }

        private static void HighestTwoExcept(
            Span<int> rankCount, int excluded, out int k1, out int k2)
        {
            k1 = k2 = 0;
            int found = 0;
            for (int rank = RankAce; rank >= RankTwo && found < 2; rank--)
            {
                if (rank == excluded || rankCount[rank] == 0)
                    continue;

                if (found == 0) k1 = rank;
                else k2 = rank;
                found++;
            }
        }

        private static void HighestThreeExcept(
            Span<int> rankCount, int excluded, out int k1, out int k2, out int k3)
        {
            k1 = k2 = k3 = 0;
            int found = 0;
            for (int rank = RankAce; rank >= RankTwo && found < 3; rank--)
            {
                if (rank == excluded || rankCount[rank] == 0)
                    continue;

                if (found == 0) k1 = rank;
                else if (found == 1) k2 = rank;
                else k3 = rank;
                found++;
            }
        }

        private static Card Pick(int index, Card h0, Card h1, Card b0, Card b1, Card b2, Card b3, Card b4)
        {
            switch (index)
            {
                case 0: return h0;
                case 1: return h1;
                case 2: return b0;
                case 3: return b1;
                case 4: return b2;
                case 5: return b3;
                default: return b4;
            }
        }

        private static HandScore EvaluateFive(Card c0, Card c1, Card c2, Card c3, Card c4)
        {
            int r0 = (int)c0.Rank;
            int r1 = (int)c1.Rank;
            int r2 = (int)c2.Rank;
            int r3 = (int)c3.Rank;
            int r4 = (int)c4.Rank;

            SortDescending5(ref r0, ref r1, ref r2, ref r3, ref r4);

            bool flush = c0.Suit == c1.Suit
                      && c1.Suit == c2.Suit
                      && c2.Suit == c3.Suit
                      && c3.Suit == c4.Suit;

            if (TryStraightHigh(r0, r1, r2, r3, r4, out int straightHigh))
            {
                if (flush)
                {
                    return straightHigh == (int)Rank.Ace
                        ? new HandScore(HandRank.RoyalFlush, straightHigh)
                        : new HandScore(HandRank.StraightFlush, straightHigh);
                }

                return new HandScore(HandRank.Straight, straightHigh);
            }

            Array.Clear(CountBuffer, 0, CountBuffer.Length);
            CountBuffer[r0]++;
            CountBuffer[r1]++;
            CountBuffer[r2]++;
            CountBuffer[r3]++;
            CountBuffer[r4]++;

            int quad = 0, trips = 0, pairA = 0, pairB = 0;
            for (int rank = (int)Rank.Ace; rank >= (int)Rank.Two; rank--)
            {
                switch (CountBuffer[rank])
                {
                    case 4: quad = rank; break;
                    case 3 when trips == 0: trips = rank; break;
                    case 2:
                        if (pairA == 0) pairA = rank;
                        else pairB = rank;
                        break;
                }
            }

            if (quad != 0)
            {
                int kicker = FindHighestRank(CountBuffer, quad);
                return new HandScore(HandRank.FourOfAKind, quad, kicker);
            }

            if (trips != 0 && pairA != 0)
                return new HandScore(HandRank.FullHouse, trips, pairA);

            if (flush)
                return new HandScore(HandRank.Flush, r0, r1, r2, r3, r4);

            if (trips != 0)
            {
                CollectKickers(CountBuffer, trips, out int k1, out int k2);
                return new HandScore(HandRank.ThreeOfAKind, trips, k1, k2);
            }

            if (pairA != 0 && pairB != 0)
            {
                int highPair = Math.Max(pairA, pairB);
                int lowPair  = Math.Min(pairA, pairB);
                int kicker   = FindHighestRank(CountBuffer, highPair, lowPair);
                return new HandScore(HandRank.TwoPair, highPair, lowPair, kicker);
            }

            if (pairA != 0)
            {
                CollectKickers(CountBuffer, pairA, out int k1, out int k2, out int k3);
                return new HandScore(HandRank.OnePair, pairA, k1, k2, k3);
            }

            return new HandScore(HandRank.HighCard, r0, r1, r2, r3, r4);
        }

        private static bool TryStraightHigh(int r0, int r1, int r2, int r3, int r4, out int highCard)
        {
            highCard = r0;

            if (r0 - r4 == 4 && r0 - r3 == 3 && r0 - r2 == 2 && r0 - r1 == 1)
                return true;

            if (r0 == (int)Rank.Ace && r1 == 5 && r2 == 4 && r3 == 3 && r4 == 2)
            {
                highCard = 5;
                return true;
            }

            return false;
        }

        private static int FindHighestRank(int[] counts, params int[] excluded)
        {
            for (int rank = (int)Rank.Ace; rank >= (int)Rank.Two; rank--)
            {
                if (counts[rank] == 0)
                    continue;

                bool skip = false;
                for (int i = 0; i < excluded.Length; i++)
                {
                    if (excluded[i] == rank)
                    {
                        skip = true;
                        break;
                    }
                }

                if (!skip)
                    return rank;
            }

            return 0;
        }

        private static void CollectKickers(int[] counts, int excluded, out int k1, out int k2)
        {
            k1 = k2 = 0;
            int found = 0;
            for (int rank = (int)Rank.Ace; rank >= (int)Rank.Two && found < 2; rank--)
            {
                if (rank == excluded || counts[rank] == 0)
                    continue;

                if (found == 0) k1 = rank;
                else k2 = rank;
                found++;
            }
        }

        private static void CollectKickers(int[] counts, int excluded, out int k1, out int k2, out int k3)
        {
            k1 = k2 = k3 = 0;
            int found = 0;
            for (int rank = (int)Rank.Ace; rank >= (int)Rank.Two && found < 3; rank--)
            {
                if (rank == excluded || counts[rank] == 0)
                    continue;

                if (found == 0) k1 = rank;
                else if (found == 1) k2 = rank;
                else k3 = rank;
                found++;
            }
        }

        private static void SortDescending5(ref int a, ref int b, ref int c, ref int d, ref int e)
        {
            // Bubble/odd-even network — must fully sort for wheel (A,5,4,3,2) detection.
            SortPairDesc(ref a, ref b);
            SortPairDesc(ref b, ref c);
            SortPairDesc(ref c, ref d);
            SortPairDesc(ref d, ref e);
            SortPairDesc(ref a, ref b);
            SortPairDesc(ref b, ref c);
            SortPairDesc(ref c, ref d);
            SortPairDesc(ref a, ref b);
            SortPairDesc(ref b, ref c);
            SortPairDesc(ref a, ref b);
        }

        private static void SortPairDesc(ref int x, ref int y)
        {
            if (x < y)
                Swap(ref x, ref y);
        }

        private static void Swap(ref int x, ref int y) => (x, y) = (y, x);
    }
}
