using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Parity tests: direct <see cref="HandEvaluatorFast.EvaluateSeven"/> vs best-of-21 reference.</summary>
    public sealed class HandEvaluatorFastCorrectnessTestRunner : MonoBehaviour
    {
        private const int RandomHandCount = 100_000;
        private const int RandomSeed      = 20260716;

        [ContextMenu("Run HandEvaluatorFast Correctness Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static bool RunAllTests()
        {
            Debug.Log("[HandEvalFast] Correctness starting...");

            if (!RunEdgeCases(out string edgeFail))
            {
                Debug.LogError($"[HandEvalFast] FAIL edge case: {edgeFail}");
                return false;
            }

            if (!RunRandomParity(RandomHandCount, RandomSeed, out string randomFail))
            {
                Debug.LogError($"[HandEvalFast] FAIL random: {randomFail}");
                return false;
            }

            Debug.Log($"[HandEvalFast] PASS edge cases + {RandomHandCount} random hands (seed={RandomSeed}).");
            return true;
        }

        private static bool RunEdgeCases(out string failure)
        {
            failure = null;

            // Wheel straight A-2-3-4-5 (mixed suits) + junk.
            if (!Expect(
                    "wheel straight",
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Two),
                    C(Suit.Diamonds, Rank.Three), C(Suit.Clubs, Rank.Four),
                    C(Suit.Spades, Rank.Five), C(Suit.Hearts, Rank.King),
                    C(Suit.Diamonds, Rank.Queen),
                    out failure))
                return false;

            // Straight flush (not royal): 5-6-7-8-9 hearts + offsuits.
            if (!Expect(
                    "straight flush",
                    C(Suit.Hearts, Rank.Five), C(Suit.Hearts, Rank.Six),
                    C(Suit.Hearts, Rank.Seven), C(Suit.Hearts, Rank.Eight),
                    C(Suit.Hearts, Rank.Nine), C(Suit.Spades, Rank.Ace),
                    C(Suit.Clubs, Rank.Ace),
                    out failure))
                return false;

            // Two triplets → full house (AAA KKK 2).
            if (!Expect(
                    "two triplets full house",
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace),
                    C(Suit.Diamonds, Rank.Ace), C(Suit.Clubs, Rank.King),
                    C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.King),
                    C(Suit.Diamonds, Rank.Two),
                    out failure))
                return false;

            // Three pairs → best two pair + kicker.
            if (!Expect(
                    "three pairs",
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace),
                    C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.King),
                    C(Suit.Spades, Rank.Queen), C(Suit.Hearts, Rank.Queen),
                    C(Suit.Diamonds, Rank.Jack),
                    out failure))
                return false;

            // Quads with kicker.
            if (!Expect(
                    "quads with kicker",
                    C(Suit.Spades, Rank.Nine), C(Suit.Hearts, Rank.Nine),
                    C(Suit.Diamonds, Rank.Nine), C(Suit.Clubs, Rank.Nine),
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Two),
                    C(Suit.Diamonds, Rank.Three),
                    out failure))
                return false;

            // Seven-card flush (7 hearts) — top five hearts.
            if (!Expect(
                    "seven-card flush",
                    C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.King),
                    C(Suit.Hearts, Rank.Queen), C(Suit.Hearts, Rank.Jack),
                    C(Suit.Hearts, Rank.Nine), C(Suit.Hearts, Rank.Five),
                    C(Suit.Hearts, Rank.Two),
                    out failure))
                return false;

            return true;
        }

        private static bool Expect(
            string name,
            Card c0, Card c1, Card c2, Card c3, Card c4, Card c5, Card c6,
            out string failure)
        {
            HandScore direct = HandEvaluatorFast.EvaluateSeven(c0, c1, c2, c3, c4, c5, c6);
            HandScore reference = HandEvaluatorFast.EvaluateSevenReference(c0, c1, c2, c3, c4, c5, c6);
            if (direct.CompareTo(reference) != 0)
            {
                failure =
                    $"{name}: cards={Format7(c0, c1, c2, c3, c4, c5, c6)} " +
                    $"direct={direct} reference={reference}";
                return false;
            }

            failure = null;
            return true;
        }

        private static bool RunRandomParity(int count, int seed, out string failure)
        {
            var rng = new System.Random(seed);
            var deck = new List<Card>(52);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    deck.Add(new Card(suit, rank));
            }

            for (int i = 0; i < count; i++)
            {
                Shuffle(deck, rng);
                Card c0 = deck[0], c1 = deck[1], c2 = deck[2], c3 = deck[3];
                Card c4 = deck[4], c5 = deck[5], c6 = deck[6];

                HandScore direct = HandEvaluatorFast.EvaluateSeven(c0, c1, c2, c3, c4, c5, c6);
                HandScore reference = HandEvaluatorFast.EvaluateSevenReference(c0, c1, c2, c3, c4, c5, c6);
                if (direct.CompareTo(reference) != 0)
                {
                    failure =
                        $"i={i} cards={Format7(c0, c1, c2, c3, c4, c5, c6)} " +
                        $"direct={direct} reference={reference}";
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private static void Shuffle(List<Card> deck, System.Random rng)
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static string Format7(
            Card c0, Card c1, Card c2, Card c3, Card c4, Card c5, Card c6) =>
            $"{c0} {c1} {c2} {c3} {c4} {c5} {c6}";
    }
}
