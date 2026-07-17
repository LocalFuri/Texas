using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Developer-only fixed tests for <see cref="PostflopDrawDetector"/>.</summary>
    public sealed class PostflopDrawDetectorTestRunner : MonoBehaviour
    {
        private static readonly PostflopDrawFlags SemiBluffDraws =
            PostflopDrawFlags.FlushDraw | PostflopDrawFlags.OpenEndedStraightDraw;

        [ContextMenu("Run PostflopDrawDetector Tests")]
        private void RunTestsFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PostflopDrawTest] Running {cases.Count} scenario(s)...");

            foreach (DrawTestCase testCase in cases)
            {
                PostflopDrawFlags actual = PostflopDrawDetector.Detect(
                    testCase.HoleCards,
                    testCase.Board);

                HandRank made = BettingAdvisor.GetMadeHandRank(testCase.HoleCards, testCase.Board);
                bool drawsOk = actual == testCase.Expected;
                bool madeOk = !testCase.ExpectedMade.HasValue || made == testCase.ExpectedMade.Value;
                bool semiOk = true;
                if (testCase.ExpectNoSemiBluff)
                {
                    bool wouldSemiBluff = made < HandRank.ThreeOfAKind
                        && (actual & SemiBluffDraws) != 0;
                    semiOk = !wouldSemiBluff && made >= HandRank.ThreeOfAKind;
                }

                bool ok = drawsOk && madeOk && semiOk;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopDrawTest] {testCase.Name}\n" +
                    $"  Made={made} draws expected={FormatFlags(testCase.Expected)} actual={FormatFlags(actual)}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopDrawTest] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
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
                    PostflopDrawFlags.OpenEndedStraightDraw,
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

                // Four spades on board, no spade in hand → not a flush draw.
                new DrawTestCase(
                    "Four-flush board without hole suit → no FlushDraw",
                    PostflopDrawFlags.None,
                    Cards(
                        C(Suit.Hearts, Rank.Three),
                        C(Suit.Diamonds, Rank.Eight)),
                    Cards(
                        C(Suit.Spades, Rank.Two),
                        C(Suit.Spades, Rank.Five),
                        C(Suit.Spades, Rank.Nine),
                        C(Suit.Spades, Rank.King))),

                // AA on 2♠5♠3♠A♠ → Trips, no FD, no semi-bluff.
                new DrawTestCase(
                    "AA on 2s5s3sAs → Trips only, no FlushDraw, no Semi-bluff",
                    PostflopDrawFlags.None,
                    Cards(
                        C(Suit.Hearts, Rank.Ace),
                        C(Suit.Diamonds, Rank.Ace)),
                    Cards(
                        C(Suit.Spades, Rank.Two),
                        C(Suit.Spades, Rank.Five),
                        C(Suit.Spades, Rank.Three),
                        C(Suit.Spades, Rank.Ace)),
                    expectedMade: HandRank.ThreeOfAKind,
                    expectNoSemiBluff: true),

                // Trips on unpaired rainbow board still no draws / no semi-bluff.
                new DrawTestCase(
                    "Set on dry flop → Trips, no draws, no Semi-bluff",
                    PostflopDrawFlags.None,
                    Cards(
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Clubs, Rank.Seven)),
                    Cards(
                        C(Suit.Spades, Rank.Seven),
                        C(Suit.Diamonds, Rank.Two),
                        C(Suit.Hearts, Rank.King)),
                    expectedMade: HandRank.ThreeOfAKind,
                    expectNoSemiBluff: true),
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
            public HandRank? ExpectedMade { get; }
            public bool ExpectNoSemiBluff { get; }

            public DrawTestCase(
                string name,
                PostflopDrawFlags expected,
                Card[] holeCards,
                Card[] board,
                HandRank? expectedMade = null,
                bool expectNoSemiBluff = false)
            {
                Name = name;
                Expected = expected;
                HoleCards = holeCards;
                Board = board;
                ExpectedMade = expectedMade;
                ExpectNoSemiBluff = expectNoSemiBluff;
            }
        }
    }
}
