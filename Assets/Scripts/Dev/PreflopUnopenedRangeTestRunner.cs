using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Developer-only fixed tests for unopened-pot preflop opening ranges.</summary>
    public sealed class PreflopUnopenedRangeTestRunner : MonoBehaviour
    {
        private const int StackChips = 1000;
        private const int BlindCall  = 20; // match BB; not free to check

        [ContextMenu("Run Preflop Unopened Range Tests")]
        private void RunTestsFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PreflopUnopenedTest] Running {cases.Count} scenario(s)...");

            foreach (UnopenedTestCase testCase in cases)
            {
                BettingAdvice actual = PreflopStrategy.RecommendAdvice(
                    testCase.Group,
                    testCase.Seat,
                    facingRaise: false,
                    potBeforeAction: 30,
                    callAmount: testCase.CallAmount,
                    playerChips: StackChips,
                    canCheck: testCase.CanCheck,
                    canRaise: true,
                    canCall: testCase.CallAmount > 0,
                    streetRaiseCount: 0,
                    holeCards: testCase.HoleCards);

                bool ok = actual == testCase.Expected;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PreflopUnopenedTest] {testCase.Name}\n" +
                    $"  Seat={testCase.Seat} Tier={testCase.Group} canCheck={testCase.CanCheck}\n" +
                    $"  Expected: {testCase.Expected}\n" +
                    $"  Actual:   {actual}\n" +
                    $"  Result:   {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PreflopUnopenedTest] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<UnopenedTestCase> BuildTestCases()
        {
            Card[] aa = Cards(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace));       // Premium
            Card[] tt = Cards(C(Suit.Spades, Rank.Ten), C(Suit.Hearts, Rank.Ten));         // Strong
            Card[] eights = Cards(C(Suit.Clubs, Rank.Eight), C(Suit.Diamonds, Rank.Eight)); // Playable

            return new List<UnopenedTestCase>
            {
                new UnopenedTestCase(
                    "Early Premium → Raise",
                    PreflopHandGroup.Premium, PreflopSeatBucket.Early,
                    BlindCall, canCheck: false, expected: BettingAdvice.Raise, aa),

                new UnopenedTestCase(
                    "Early Strong → Fold",
                    PreflopHandGroup.Strong, PreflopSeatBucket.Early,
                    BlindCall, canCheck: false, expected: BettingAdvice.Fold, tt),

                new UnopenedTestCase(
                    "Middle Strong → Raise",
                    PreflopHandGroup.Strong, PreflopSeatBucket.Middle,
                    BlindCall, canCheck: false, expected: BettingAdvice.Raise, tt),

                new UnopenedTestCase(
                    "Middle Playable → Fold",
                    PreflopHandGroup.Playable, PreflopSeatBucket.Middle,
                    BlindCall, canCheck: false, expected: BettingAdvice.Fold, eights),

                new UnopenedTestCase(
                    "CO Playable → Raise",
                    PreflopHandGroup.Playable, PreflopSeatBucket.Cutoff,
                    BlindCall, canCheck: false, expected: BettingAdvice.Raise, eights),

                new UnopenedTestCase(
                    "BTN Playable → Raise",
                    PreflopHandGroup.Playable, PreflopSeatBucket.Button,
                    BlindCall, canCheck: false, expected: BettingAdvice.Raise, eights),

                new UnopenedTestCase(
                    "SB Strong → Raise",
                    PreflopHandGroup.Strong, PreflopSeatBucket.SmallBlind,
                    BlindCall, canCheck: false, expected: BettingAdvice.Raise, tt),

                new UnopenedTestCase(
                    "SB Playable → Fold",
                    PreflopHandGroup.Playable, PreflopSeatBucket.SmallBlind,
                    BlindCall, canCheck: false, expected: BettingAdvice.Fold, eights),

                // BB option unchanged: Premium/Strong raise; Playable checks
                new UnopenedTestCase(
                    "BB option Premium → Raise",
                    PreflopHandGroup.Premium, PreflopSeatBucket.BigBlind,
                    0, canCheck: true, expected: BettingAdvice.Raise, aa),

                new UnopenedTestCase(
                    "BB option Strong → Raise",
                    PreflopHandGroup.Strong, PreflopSeatBucket.BigBlind,
                    0, canCheck: true, expected: BettingAdvice.Raise, tt),

                new UnopenedTestCase(
                    "BB option Playable → Check",
                    PreflopHandGroup.Playable, PreflopSeatBucket.BigBlind,
                    0, canCheck: true, expected: BettingAdvice.Check, eights),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static Card[] Cards(params Card[] cards) => cards;

        private sealed class UnopenedTestCase
        {
            public string Name { get; }
            public PreflopHandGroup Group { get; }
            public PreflopSeatBucket Seat { get; }
            public int CallAmount { get; }
            public bool CanCheck { get; }
            public BettingAdvice Expected { get; }
            public Card[] HoleCards { get; }

            public UnopenedTestCase(
                string name,
                PreflopHandGroup group,
                PreflopSeatBucket seat,
                int callAmount,
                bool canCheck,
                BettingAdvice expected,
                Card[] holeCards)
            {
                Name       = name;
                Group      = group;
                Seat       = seat;
                CallAmount = callAmount;
                CanCheck   = canCheck;
                Expected   = expected;
                HoleCards  = holeCards;
            }
        }
    }
}
