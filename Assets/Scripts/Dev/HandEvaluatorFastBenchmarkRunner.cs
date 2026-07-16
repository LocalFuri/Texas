using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TexasHoldem.Dev
{
    /// <summary>Times best-of-21 reference vs direct EvaluateSeven on a fixed hand set.</summary>
    public sealed class HandEvaluatorFastBenchmarkRunner : MonoBehaviour
    {
        private const int HandCount   = 200_000;
        private const int WarmupHands = 10_000;
        private const int RandomSeed  = 20260716;

        [ContextMenu("Run HandEvaluatorFast Benchmark")]
        private void RunFromContextMenu() => RunBenchmark();

        public static void RunBenchmark()
        {
            Debug.Log($"[HandEvalFastBench] Generating {HandCount} hands (seed={RandomSeed})...");
            Card[] hands = GenerateHands(HandCount, RandomSeed);

            // Warmup (untimed).
            for (int i = 0; i < WarmupHands; i++)
            {
                int o = i * 7;
                HandEvaluatorFast.EvaluateSevenReference(
                    hands[o], hands[o + 1], hands[o + 2], hands[o + 3],
                    hands[o + 4], hands[o + 5], hands[o + 6]);
                HandEvaluatorFast.EvaluateSeven(
                    hands[o], hands[o + 1], hands[o + 2], hands[o + 3],
                    hands[o + 4], hands[o + 5], hands[o + 6]);
            }

            long sink = 0;

            var swRef = Stopwatch.StartNew();
            for (int i = 0; i < HandCount; i++)
            {
                int o = i * 7;
                HandScore s = HandEvaluatorFast.EvaluateSevenReference(
                    hands[o], hands[o + 1], hands[o + 2], hands[o + 3],
                    hands[o + 4], hands[o + 5], hands[o + 6]);
                sink += (int)s.Rank + s.K0;
            }
            swRef.Stop();

            var swNew = Stopwatch.StartNew();
            for (int i = 0; i < HandCount; i++)
            {
                int o = i * 7;
                HandScore s = HandEvaluatorFast.EvaluateSeven(
                    hands[o], hands[o + 1], hands[o + 2], hands[o + 3],
                    hands[o + 4], hands[o + 5], hands[o + 6]);
                sink += (int)s.Rank + s.K0;
            }
            swNew.Stop();

            double refEps = HandCount / swRef.Elapsed.TotalSeconds;
            double newEps = HandCount / swNew.Elapsed.TotalSeconds;
            double speedup = newEps / Math.Max(refEps, 1e-9);

            Debug.Log(
                "[HandEvalFastBench] Summary\n" +
                $"  Hands:              {HandCount}\n" +
                $"  Reference ms:       {swRef.Elapsed.TotalMilliseconds:F2}\n" +
                $"  Direct ms:          {swNew.Elapsed.TotalMilliseconds:F2}\n" +
                $"  Reference evals/s:  {refEps:N0}\n" +
                $"  Direct evals/s:     {newEps:N0}\n" +
                $"  Speedup:            {speedup:F2}x\n" +
                $"  Sink:               {sink}");
        }

        private static Card[] GenerateHands(int handCount, int seed)
        {
            var rng = new System.Random(seed);
            var deck = new List<Card>(52);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    deck.Add(new Card(suit, rank));
            }

            var hands = new Card[handCount * 7];
            for (int h = 0; h < handCount; h++)
            {
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(0, i + 1);
                    (deck[i], deck[j]) = (deck[j], deck[i]);
                }

                int o = h * 7;
                for (int k = 0; k < 7; k++)
                    hands[o + k] = deck[k];
            }

            return hands;
        }
    }
}
