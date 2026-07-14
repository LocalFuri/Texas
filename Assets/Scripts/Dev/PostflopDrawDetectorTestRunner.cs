using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Developer-only fixed tests for <see cref="PostflopDrawDetector"/>.</summary>
    public sealed class PostflopDrawDetectorTestRunner : MonoBehaviour
    {
        [ContextMenu("Run PostflopDrawDetector Tests")]
        private void RunTestsFromContextMenu() => RunAllTests();

        public void RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PostflopDrawTest] Running {cases.Count} scenario(s)...");

            foreach (DrawTestCase testCase in cases)
            {
                PostflopDrawFlags actual = PostflopDrawDetector.Detect(
                    testCase.HoleCards,
                    testCase.Board);

                bool ok = actual == testCase.Expected;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopDrawTest] {testCase.Name}\n" +
                    $"  Expected: {FormatFlags(testCase.Expected)}\n" +
                    $"  Actual:   {FormatFlags(actual)}\n" +
                    $"  Result:   {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopDrawTest] Complete: {passed}/{cases.Count} passed.");
        }

        private static List<DrawTestCase> BuildTestCases()
        {
            return new List<DrawTestCase>
            {
                new DrawTestCase(
                    "Flush draw",
                    PostflopDrawFlags.FlushDraw,
                    Cards(
                        C(Suit.Hearts, Rank.Ace),
                        C(Suit.Hearts, Rank.King)),
                    Cards(
                        C(Suit.Hearts, Rank.Nine),
                        C(Suit.Hearts, Rank.Four),
                        C(Suit.Clubs, Rank.Two))),

                new DrawTestCase(
                    "Open-ended straight draw",
                    PostflopDrawFlags.OpenEndedStraightDraw | PostflopDrawFlags.GutshotStraightDraw,
                    Cards(
                        C(Suit.Clubs, Rank.Nine),
                        C(Suit.Clubs, Rank.Ten)),
                    Cards(
                        C(Suit.Diamonds, Rank.Eight),
                        C(Suit.Spades, Rank.Seven),
                        C(Suit.Hearts, Rank.Two))),

                new DrawTestCase(
                    "Gutshot straight draw",
                    PostflopDrawFlags.GutshotStraightDraw,
                    Cards(
                        C(Suit.Diamonds, Rank.Jack),
                        C(Suit.Clubs, Rank.Queen)),
                    Cards(
                        C(Suit.Spades, Rank.Nine),
                        C(Suit.Hearts, Rank.Eight),
                        C(Suit.Diamonds, Rank.Two))),

                new DrawTestCase(
                    "No draw",
                    PostflopDrawFlags.None,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King)),
                    Cards(
                        C(Suit.Spades, Rank.Two),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Diamonds, Rank.Jack))),

                new DrawTestCase(
                    "Made straight",
                    PostflopDrawFlags.None,
                    Cards(
                        C(Suit.Clubs, Rank.Nine),
                        C(Suit.Diamonds, Rank.Ten)),
                    Cards(
                        C(Suit.Spades, Rank.Eight),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Diamonds, Rank.Six))),

                new DrawTestCase(
                    "Made flush",
                    PostflopDrawFlags.None,
                    Cards(
                        C(Suit.Hearts, Rank.Ace),
                        C(Suit.Hearts, Rank.King)),
                    Cards(
                        C(Suit.Hearts, Rank.Queen),
                        C(Suit.Hearts, Rank.Jack),
                        C(Suit.Hearts, Rank.Nine))),
            };
        }

        private static string FormatFlags(PostflopDrawFlags flags) =>
            flags == PostflopDrawFlags.None ? "None" : flags.ToString();

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static Card[] Cards(params Card[] cards) => cards;

        private sealed class DrawTestCase
        {
            public string Name { get; }
            public PostflopDrawFlags Expected { get; }
            public Card[] HoleCards { get; }
            public Card[] Board { get; }

            public DrawTestCase(
                string name,
                PostflopDrawFlags expected,
                Card[] holeCards,
                Card[] board)
            {
                Name       = name;
                Expected   = expected;
                HoleCards  = holeCards;
                Board      = board;
            }
        }
    }
}
