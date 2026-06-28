using System.Collections.Generic;

namespace TexasHoldem
{
    /// <summary>Greedy chip breakdown for bet stacks (largest denominations first).</summary>
    public static class ChipBreakdown
    {
        public static readonly int[] Denominations      = { 500, 100, 25, 5, 1 };
        public static readonly int[] StackDenominations = { 25, 5, 1 };

        public static List<int> BreakDown(int amount, int maxChips = 8)
            => BreakDown(amount, maxChips, Denominations);

        public static List<int> BreakDown(int amount, int maxChips, int[] denominations)
        {
            var result    = new List<int>();
            int remaining = amount;
            foreach (int d in denominations)
            {
                while (remaining >= d && result.Count < maxChips)
                {
                    result.Add(d);
                    remaining -= d;
                }
            }
            return result;
        }

        public static int LargestDenomination(int amount)
            => LargestDenomination(amount, Denominations);

        public static int LargestDenomination(int amount, int[] denominations)
        {
            if (amount <= 0) return 1;
            foreach (int d in denominations)
            {
                if (amount >= d)
                    return d;
            }
            return 1;
        }
    }
}
