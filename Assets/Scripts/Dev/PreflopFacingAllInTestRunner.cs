using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Developer-only fixed tests for preflop facing-all-in call/fold decisions.</summary>
    public sealed class PreflopFacingAllInTestRunner : MonoBehaviour
    {
        private const int StackChips = 1000;
        private const int ShoveCall  = 850; // >= 85% → IsFacingAllIn
        private const int AboveCapCall = 400; // 40% → Strong chip-cap fold when not allowlisted

        [ContextMenu("Run Preflop Facing-All-In Tests")]
        private void RunTestsFromContextMenu() => RunAllTests();

        /// <summary>Returns (passed, total). Also logs each case.</summary>
        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PreflopFacingAllInTest] Running {cases.Count} scenario(s)...");

            foreach (FacingAllInTestCase testCase in cases)
            {
                BettingAdvice actual = PreflopStrategy.RecommendAdvice(
                    testCase.Group,
                    PreflopSeatBucket.Button,
                    facingRaise: true,
                    potBeforeAction: 200,
                    callAmount: testCase.CallAmount,
                    playerChips: StackChips,
                    canCheck: false,
                    canRaise: true,
                    canCall: true,
                    streetRaiseCount: 1,
                    holeCards: testCase.HoleCards,
                    playersBehind: testCase.PlayersBehind,
                    shovePosition: testCase.ShovePosition);

                bool ok = actual == testCase.Expected;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PreflopFacingAllInTest] {testCase.Name}\n" +
                    $"  Hole: {testCase.HoleCards[0]} {testCase.HoleCards[1]} " +
                    $"group={testCase.Group} call={testCase.CallAmount} " +
                    $"behind={testCase.PlayersBehind} shove={testCase.ShovePosition}\n" +
                    $"  Expected: {testCase.Expected}\n" +
                    $"  Actual:   {actual}\n" +
                    $"  Result:   {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PreflopFacingAllInTest] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<FacingAllInTestCase> BuildTestCases()
        {
            Card[] tt = Cards(C(Suit.Spades, Rank.Ten), C(Suit.Hearts, Rank.Ten));
            Card[] aa = Cards(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace));
            Card[] eights = Cards(C(Suit.Clubs, Rank.Eight), C(Suit.Diamonds, Rank.Eight));

            return new List<FacingAllInTestCase>
            {
                new FacingAllInTestCase(
                    "Strong vs Early shove → Fold",
                    PreflopHandGroup.Strong, tt, ShoveCall,
                    playersBehind: 0, shove: PreflopSeatBucket.Early,
                    expected: BettingAdvice.Fold),

                new FacingAllInTestCase(
                    "Strong vs Middle shove → Fold",
                    PreflopHandGroup.Strong, tt, ShoveCall,
                    playersBehind: 0, shove: PreflopSeatBucket.Middle,
                    expected: BettingAdvice.Fold),

                new FacingAllInTestCase(
                    "Strong vs BTN shove, 0 behind → Call",
                    PreflopHandGroup.Strong, tt, ShoveCall,
                    playersBehind: 0, shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Call),

                new FacingAllInTestCase(
                    "Strong vs BTN shove, 2 behind → Fold",
                    PreflopHandGroup.Strong, tt, ShoveCall,
                    playersBehind: 2, shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Fold),

                new FacingAllInTestCase(
                    "Premium vs Early shove, 0 behind → Call",
                    PreflopHandGroup.Premium, aa, ShoveCall,
                    playersBehind: 0, shove: PreflopSeatBucket.Early,
                    expected: BettingAdvice.Call),

                new FacingAllInTestCase(
                    "Premium vs Middle shove, 2 behind → Call",
                    PreflopHandGroup.Premium, aa, ShoveCall,
                    playersBehind: 2, shove: PreflopSeatBucket.Middle,
                    expected: BettingAdvice.Call),

                new FacingAllInTestCase(
                    "Premium vs BTN shove, 2 behind → Call",
                    PreflopHandGroup.Premium, aa, ShoveCall,
                    playersBehind: 2, shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Call),

                new FacingAllInTestCase(
                    "Non-allowlisted Strong (88) above 35% cap → Fold",
                    PreflopHandGroup.Strong, eights, AboveCapCall,
                    playersBehind: 0, shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Fold),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static Card[] Cards(params Card[] cards) => cards;

        private sealed class FacingAllInTestCase
        {
            public string Name { get; }
            public PreflopHandGroup Group { get; }
            public Card[] HoleCards { get; }
            public int CallAmount { get; }
            public int PlayersBehind { get; }
            public PreflopSeatBucket ShovePosition { get; }
            public BettingAdvice Expected { get; }

            public FacingAllInTestCase(
                string name,
                PreflopHandGroup group,
                Card[] holeCards,
                int callAmount,
                int playersBehind,
                PreflopSeatBucket shove,
                BettingAdvice expected)
            {
                Name          = name;
                Group         = group;
                HoleCards     = holeCards;
                CallAmount    = callAmount;
                PlayersBehind = playersBehind;
                ShovePosition = shove;
                Expected      = expected;
            }
        }
    }
}
