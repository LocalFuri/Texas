using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Developer-only fixed tests for multiway preflop facing-raise tightening.</summary>
    public sealed class PreflopMultiwayFacingRaiseTestRunner : MonoBehaviour
    {
        private const int StackChips   = 1000;
        private const int NormalCall   = 100;  // well below 85% → not facing-all-in
        private const int ShoveCall    = 850;  // >= 85% → facing-all-in

        [ContextMenu("Run Preflop Multiway Facing-Raise Tests")]
        private void RunTestsFromContextMenu() => RunAllTests();

        /// <summary>Returns (passed, total). Also logs each case.</summary>
        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PreflopMultiwayTest] Running {cases.Count} scenario(s)...");

            foreach (MultiwayTestCase testCase in cases)
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
                    shovePosition: testCase.ShovePosition,
                    callersBefore: testCase.CallersBefore);

                bool ok = actual == testCase.Expected;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PreflopMultiwayTest] {testCase.Name}\n" +
                    $"  Hole: {testCase.HoleCards[0]} {testCase.HoleCards[1]} " +
                    $"group={testCase.Group} call={testCase.CallAmount} " +
                    $"callersBefore={testCase.CallersBefore} behind={testCase.PlayersBehind}\n" +
                    $"  Expected: {testCase.Expected}\n" +
                    $"  Actual:   {actual}\n" +
                    $"  Result:   {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PreflopMultiwayTest] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<MultiwayTestCase> BuildTestCases()
        {
            Card[] tt = Cards(C(Suit.Spades, Rank.Ten), C(Suit.Hearts, Rank.Ten));
            Card[] aa = Cards(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace));

            return new List<MultiwayTestCase>
            {
                new MultiwayTestCase(
                    "Strong with 0 callers before → Raise (existing 3-bet)",
                    PreflopHandGroup.Strong, tt, NormalCall,
                    callersBefore: 0, playersBehind: 0,
                    shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Raise),

                new MultiwayTestCase(
                    "Strong with 1 caller before → Raise (existing 3-bet)",
                    PreflopHandGroup.Strong, tt, NormalCall,
                    callersBefore: 1, playersBehind: 0,
                    shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Raise),

                new MultiwayTestCase(
                    "Strong with 2 callers before → Fold",
                    PreflopHandGroup.Strong, tt, NormalCall,
                    callersBefore: 2, playersBehind: 0,
                    shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Fold),

                new MultiwayTestCase(
                    "Strong with 3 callers before → Fold",
                    PreflopHandGroup.Strong, tt, NormalCall,
                    callersBefore: 3, playersBehind: 0,
                    shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Fold),

                new MultiwayTestCase(
                    "Premium with 2+ callers before → Raise (unchanged)",
                    PreflopHandGroup.Premium, aa, NormalCall,
                    callersBefore: 2, playersBehind: 0,
                    shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Raise),

                new MultiwayTestCase(
                    "Facing-all-in Strong BTN, 0 behind, 2 callers → Call (all-in unchanged)",
                    PreflopHandGroup.Strong, tt, ShoveCall,
                    callersBefore: 2, playersBehind: 0,
                    shove: PreflopSeatBucket.Button,
                    expected: BettingAdvice.Call),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static Card[] Cards(params Card[] cards) => cards;

        private sealed class MultiwayTestCase
        {
            public string Name { get; }
            public PreflopHandGroup Group { get; }
            public Card[] HoleCards { get; }
            public int CallAmount { get; }
            public int CallersBefore { get; }
            public int PlayersBehind { get; }
            public PreflopSeatBucket ShovePosition { get; }
            public BettingAdvice Expected { get; }

            public MultiwayTestCase(
                string name,
                PreflopHandGroup group,
                Card[] holeCards,
                int callAmount,
                int callersBefore,
                int playersBehind,
                PreflopSeatBucket shove,
                BettingAdvice expected)
            {
                Name           = name;
                Group          = group;
                HoleCards      = holeCards;
                CallAmount     = callAmount;
                CallersBefore  = callersBefore;
                PlayersBehind  = playersBehind;
                ShovePosition  = shove;
                Expected       = expected;
            }
        }
    }
}
