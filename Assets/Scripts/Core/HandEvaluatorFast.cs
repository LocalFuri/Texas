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
    }

    public static class HandEvaluatorFast
    {
        private static readonly int[] CountBuffer = new int[15];

        public static HandScore EvaluateSeven(Card h0, Card h1, Card b0, Card b1, Card b2, Card b3, Card b4)
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
            SortPairDesc(ref a, ref b);
            SortPairDesc(ref c, ref d);
            if (a < c) Swap(ref a, ref c);
            if (b < d) Swap(ref b, ref d);
            if (b < c) Swap(ref b, ref c);
            SortPairDesc(ref e, ref d);
            if (d < c) Swap(ref d, ref c);
            if (c < b) Swap(ref c, ref b);
            if (b < a) Swap(ref b, ref a);
        }

        private static void SortPairDesc(ref int x, ref int y)
        {
            if (x < y)
                Swap(ref x, ref y);
        }

        private static void Swap(ref int x, ref int y) => (x, y) = (y, x);
    }
}
