using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Ace Maverick human-trainer MP unopened allowlist (does not change shared bot opens).
    /// </summary>
    public sealed class AceMaverickMpOpenAllowlistTestRunner : MonoBehaviour
    {
        private const int StackChips = 1000;
        private const int BlindCall = 20;

        [ContextMenu("Run Ace Maverick MP Open Allowlist Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[AceMpOpenAllowlist] Running {cases.Count} scenario(s)...");

            foreach (TestCase testCase in cases)
            {
                PreflopHandGroup group = PreflopStrategy.ClassifyHand(testCase.HoleCards);

                BettingAdvice shared = PreflopStrategy.RecommendAdvice(
                    group,
                    PreflopSeatBucket.Middle,
                    facingRaise: false,
                    potBeforeAction: 30,
                    callAmount: BlindCall,
                    playerChips: StackChips,
                    canCheck: false,
                    canRaise: true,
                    canCall: true,
                    streetRaiseCount: 0,
                    holeCards: testCase.HoleCards);

                BettingAdvice aceAdvice = AceMaverickPreflopCoach.ApplyUnopenedMiddleAllowlist(
                    shared,
                    PreflopSeatBucket.Middle,
                    facingRaise: false,
                    streetRaiseCount: 0,
                    callersBefore: 0,
                    holeCards: testCase.HoleCards,
                    canRaise: true);

                bool ok = aceAdvice == testCase.ExpectedAce
                    && shared == testCase.ExpectedShared;

                if (ok)
                    passed++;

                Debug.Log(
                    $"[AceMpOpenAllowlist] {testCase.Name}\n" +
                    $"  Group={group}\n" +
                    $"  Shared expected/actual: {testCase.ExpectedShared}/{shared}\n" +
                    $"  Ace expected/actual:    {testCase.ExpectedAce}/{aceAdvice}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[AceMpOpenAllowlist] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<TestCase> BuildTestCases()
        {
            return new List<TestCase>
            {
                Case("MP A8s unopened → Ace Raise / shared Fold",
                    Cards(C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.Eight)),
                    BettingAdvice.Fold, BettingAdvice.Raise),

                Case("MP A5s unopened → Ace Raise / shared Fold",
                    Cards(C(Suit.Spades, Rank.Ace), C(Suit.Spades, Rank.Five)),
                    BettingAdvice.Fold, BettingAdvice.Raise),

                Case("MP 66 unopened → Ace Raise / shared Fold",
                    Cards(C(Suit.Clubs, Rank.Six), C(Suit.Diamonds, Rank.Six)),
                    BettingAdvice.Fold, BettingAdvice.Raise),

                Case("MP 87s unopened → Ace Raise / shared Fold",
                    Cards(C(Suit.Diamonds, Rank.Eight), C(Suit.Diamonds, Rank.Seven)),
                    BettingAdvice.Fold, BettingAdvice.Raise),

                Case("MP A2s unopened → Fold both",
                    Cards(C(Suit.Clubs, Rank.Ace), C(Suit.Clubs, Rank.Two)),
                    BettingAdvice.Fold, BettingAdvice.Fold),

                Case("MP K9s unopened → Fold both",
                    Cards(C(Suit.Hearts, Rank.King), C(Suit.Hearts, Rank.Nine)),
                    BettingAdvice.Fold, BettingAdvice.Fold),

                Case("MP A9o unopened → Fold both",
                    Cards(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Nine)),
                    BettingAdvice.Fold, BettingAdvice.Fold),

                Case("MP 44 unopened → Fold both",
                    Cards(C(Suit.Clubs, Rank.Four), C(Suit.Diamonds, Rank.Four)),
                    BettingAdvice.Fold, BettingAdvice.Fold),

                // Strong+ still Raise on shared path (allowlist must not interfere).
                Case("MP ATs unopened → Raise both (Strong)",
                    Cards(C(Suit.Spades, Rank.Ace), C(Suit.Spades, Rank.Ten)),
                    BettingAdvice.Raise, BettingAdvice.Raise),
            };
        }

        private static TestCase Case(
            string name,
            Card[] holeCards,
            BettingAdvice expectedShared,
            BettingAdvice expectedAce) =>
            new TestCase(name, holeCards, expectedShared, expectedAce);

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static Card[] Cards(params Card[] cards) => cards;

        private sealed class TestCase
        {
            public string Name { get; }
            public Card[] HoleCards { get; }
            public BettingAdvice ExpectedShared { get; }
            public BettingAdvice ExpectedAce { get; }

            public TestCase(
                string name,
                Card[] holeCards,
                BettingAdvice expectedShared,
                BettingAdvice expectedAce)
            {
                Name = name;
                HoleCards = holeCards;
                ExpectedShared = expectedShared;
                ExpectedAce = expectedAce;
            }
        }
    }
}
