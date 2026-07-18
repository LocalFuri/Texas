using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Developer-only fixed tests for <see cref="BoardTextureAnalyzer"/>.</summary>
    public sealed class BoardTextureAnalyzerTestRunner : MonoBehaviour
    {
        [ContextMenu("Run BoardTextureAnalyzer Tests")]
        private void RunTestsFromContextMenu() => RunAllTests();

        /// <summary>Returns (passed, total). Also logs each case.</summary>
        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[BoardTextureTest] Running {cases.Count} scenario(s)...");

            foreach (TextureTestCase testCase in cases)
            {
                BoardTextureFlags actual = BoardTextureAnalyzer.Analyze(testCase.Board);
                bool ok = actual == testCase.Expected;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[BoardTextureTest] {testCase.Name}\n" +
                    $"  Expected: {FormatFlags(testCase.Expected)}\n" +
                    $"  Actual:   {FormatFlags(actual)}\n" +
                    $"  Result:   {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[BoardTextureTest] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<TextureTestCase> BuildTestCases()
        {
            return new List<TextureTestCase>
            {
                new TextureTestCase(
                    "Too few cards",
                    BoardTextureFlags.None,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King))),

                new TextureTestCase(
                    "Unpaired dry flop",
                    BoardTextureFlags.None,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.Seven))),

                new TextureTestCase(
                    "Broadway connected flop",
                    BoardTextureFlags.Connected,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.Queen))),

                new TextureTestCase(
                    "Single pair",
                    BoardTextureFlags.Paired,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.Seven),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Spades, Rank.Two))),

                new TextureTestCase(
                    "Two pair board",
                    BoardTextureFlags.TwoPair,
                    Cards(
                        C(Suit.Spades, Rank.King),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.Seven),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Spades, Rank.Two))),

                new TextureTestCase(
                    "Trips on board",
                    BoardTextureFlags.Trips,
                    Cards(
                        C(Suit.Spades, Rank.King),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.King),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Spades, Rank.Two))),

                new TextureTestCase(
                    "Board full house",
                    BoardTextureFlags.Trips,
                    Cards(
                        C(Suit.Spades, Rank.King),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.King),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Diamonds, Rank.Seven))),

                new TextureTestCase(
                    "Three-flush flop",
                    BoardTextureFlags.ThreeFlush,
                    Cards(
                        C(Suit.Hearts, Rank.Ace),
                        C(Suit.Hearts, Rank.King),
                        C(Suit.Hearts, Rank.Seven))),

                new TextureTestCase(
                    "Four-flush river",
                    BoardTextureFlags.FourFlush,
                    Cards(
                        C(Suit.Hearts, Rank.Ace),
                        C(Suit.Hearts, Rank.King),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Hearts, Rank.Two),
                        C(Suit.Diamonds, Rank.Three))),

                new TextureTestCase(
                    "Monotone river",
                    BoardTextureFlags.FourFlush,
                    Cards(
                        C(Suit.Hearts, Rank.Ace),
                        C(Suit.Hearts, Rank.King),
                        C(Suit.Hearts, Rank.Seven),
                        C(Suit.Hearts, Rank.Two),
                        C(Suit.Hearts, Rank.Three))),

                new TextureTestCase(
                    "Rainbow dry",
                    BoardTextureFlags.None,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.Seven),
                        C(Suit.Hearts, Rank.Two),
                        C(Suit.Diamonds, Rank.Three))),

                new TextureTestCase(
                    "Connected mid flop",
                    BoardTextureFlags.Connected,
                    Cards(
                        C(Suit.Spades, Rank.Nine),
                        C(Suit.Diamonds, Rank.Ten),
                        C(Suit.Hearts, Rank.Jack))),

                new TextureTestCase(
                    "Wheel connected",
                    BoardTextureFlags.Connected,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.Two),
                        C(Suit.Hearts, Rank.Three))),

                new TextureTestCase(
                    "Full wheel connected",
                    BoardTextureFlags.Connected,
                    Cards(
                        C(Suit.Spades, Rank.Five),
                        C(Suit.Diamonds, Rank.Four),
                        C(Suit.Hearts, Rank.Three),
                        C(Suit.Clubs, Rank.Two),
                        C(Suit.Spades, Rank.Ace))),

                new TextureTestCase(
                    "Paired and connected",
                    BoardTextureFlags.Paired | BoardTextureFlags.Connected,
                    Cards(
                        C(Suit.Spades, Rank.Queen),
                        C(Suit.Diamonds, Rank.Jack),
                        C(Suit.Hearts, Rank.Ten),
                        C(Suit.Clubs, Rank.Two),
                        C(Suit.Diamonds, Rank.Two))),

                new TextureTestCase(
                    "Four-straight river",
                    BoardTextureFlags.Connected | BoardTextureFlags.FourStraight,
                    Cards(
                        C(Suit.Spades, Rank.Six),
                        C(Suit.Diamonds, Rank.Seven),
                        C(Suit.Hearts, Rank.Eight),
                        C(Suit.Clubs, Rank.Nine),
                        C(Suit.Diamonds, Rank.Two))),

                new TextureTestCase(
                    "Made straight river",
                    BoardTextureFlags.Connected,
                    Cards(
                        C(Suit.Spades, Rank.Six),
                        C(Suit.Diamonds, Rank.Seven),
                        C(Suit.Hearts, Rank.Eight),
                        C(Suit.Clubs, Rank.Nine),
                        C(Suit.Diamonds, Rank.Ten))),

                new TextureTestCase(
                    "Broadway four-straight turn",
                    BoardTextureFlags.Connected | BoardTextureFlags.FourStraight,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.Queen),
                        C(Suit.Hearts, Rank.Jack))),

                new TextureTestCase(
                    "Dry Broadway disconnected kicker",
                    BoardTextureFlags.None,
                    Cards(
                        C(Suit.Spades, Rank.Ace),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Clubs, Rank.Seven))),

                new TextureTestCase(
                    "Combined paired three-flush connected",
                    BoardTextureFlags.Paired | BoardTextureFlags.ThreeFlush | BoardTextureFlags.Connected,
                    Cards(
                        C(Suit.Spades, Rank.King),
                        C(Suit.Diamonds, Rank.King),
                        C(Suit.Hearts, Rank.Nine),
                        C(Suit.Hearts, Rank.Eight),
                        C(Suit.Hearts, Rank.Seven))),
            };
        }

        private static string FormatFlags(BoardTextureFlags flags) =>
            flags == BoardTextureFlags.None ? "None" : flags.ToString();

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static Card[] Cards(params Card[] cards) => cards;

        private sealed class TextureTestCase
        {
            public string Name { get; }
            public BoardTextureFlags Expected { get; }
            public Card[] Board { get; }

            public TextureTestCase(string name, BoardTextureFlags expected, Card[] board)
            {
                Name     = name;
                Expected = expected;
                Board    = board;
            }
        }
    }
}
