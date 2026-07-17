using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Regressions: underpair without FD/OESD must not raise facing a bet.</summary>
    public sealed class PostflopUnderpairRaiseTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Postflop Underpair Raise Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PostflopUnderpair] Running {cases.Count} scenario(s)...");

            foreach (UnderpairTestCase testCase in cases)
            {
                bool raiseBlocked = BettingAdvisor.IsWeakUnderpairRaiseBlocked(
                    testCase.HoleCards, testCase.Board, out string blockReason);
                bool isUnder = BettingAdvisor.IsPocketUnderpair(testCase.HoleCards, testCase.Board);

                BettingAdvice advice = BettingAdvisor.Recommend(
                    equityPercent: 90f,
                    potBeforeAction: testCase.Pot,
                    callAmount: testCase.CallAmount,
                    canCheck: false,
                    canRaise: true,
                    canCall: true,
                    isPreflop: false,
                    preflopGroup: PreflopHandGroup.Weak,
                    preflopSeat: PreflopSeatBucket.Button,
                    facingRaise: true,
                    streetRaiseCount: testCase.StreetRaiseCount,
                    playerChips: testCase.PlayerChips,
                    holeCards: testCase.HoleCards,
                    postflopPhase: testCase.Phase,
                    communityCards: testCase.Board);

                bool ok = advice == testCase.ExpectedAdvice
                          && raiseBlocked == testCase.ExpectRaiseBlocked;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopUnderpair] {testCase.Name}\n" +
                    $"  Hole: {testCase.HoleCards[0]} {testCase.HoleCards[1]} " +
                    $"Board: {FormatBoard(testCase.Board)} Phase={testCase.Phase}\n" +
                    $"  underpair={isUnder} raiseBlocked expected={testCase.ExpectRaiseBlocked} " +
                    $"actual={raiseBlocked} ({blockReason ?? "none"})\n" +
                    $"  Advice expected={testCase.ExpectedAdvice} actual={advice}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopUnderpair] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<UnderpairTestCase> BuildTestCases()
        {
            Card[] fours = { C(Suit.Spades, Rank.Four), C(Suit.Diamonds, Rank.Four) };
            Card[] flop =
            {
                C(Suit.Spades, Rank.Three),
                C(Suit.Hearts, Rank.Five),
                C(Suit.Diamonds, Rank.Ten),
            };
            Card[] turn =
            {
                C(Suit.Spades, Rank.Three),
                C(Suit.Hearts, Rank.Five),
                C(Suit.Diamonds, Rank.Ten),
                C(Suit.Hearts, Rank.Nine),
            };

            // Overpair: AA on 3s5h10d
            Card[] aces = { C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace) };

            return new List<UnderpairTestCase>
            {
                new UnderpairTestCase(
                    "1. 44 on 3s5hTd facing flop raise → Call (no reraise)",
                    fours, flop, GamePhase.Flop,
                    pot: 50, call: 15, chips: 1000, streetRaiseCount: 1,
                    expectRaiseBlocked: true, expectedAdvice: BettingAdvice.Call),

                new UnderpairTestCase(
                    "2. 44 on 3s5hTd9h facing turn bet → Fold (substantial, no draw)",
                    fours, turn, GamePhase.Turn,
                    pot: 80, call: 300, chips: 1000, streetRaiseCount: 1,
                    expectRaiseBlocked: true, expectedAdvice: BettingAdvice.Fold),

                new UnderpairTestCase(
                    "3. AA overpair on 3s5hTd may still Raise",
                    aces, flop, GamePhase.Flop,
                    pot: 50, call: 15, chips: 1000, streetRaiseCount: 1,
                    expectRaiseBlocked: false, expectedAdvice: BettingAdvice.Raise),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static string FormatBoard(IReadOnlyList<Card> board)
        {
            var parts = new List<string>(board.Count);
            foreach (Card c in board)
                parts.Add(c.ToString());
            return string.Join(" ", parts);
        }

        private sealed class UnderpairTestCase
        {
            public string Name { get; }
            public Card[] HoleCards { get; }
            public Card[] Board { get; }
            public GamePhase Phase { get; }
            public int Pot { get; }
            public int CallAmount { get; }
            public int PlayerChips { get; }
            public int StreetRaiseCount { get; }
            public bool ExpectRaiseBlocked { get; }
            public BettingAdvice ExpectedAdvice { get; }

            public UnderpairTestCase(
                string name,
                Card[] holeCards,
                Card[] board,
                GamePhase phase,
                int pot,
                int call,
                int chips,
                int streetRaiseCount,
                bool expectRaiseBlocked,
                BettingAdvice expectedAdvice)
            {
                Name = name;
                HoleCards = holeCards;
                Board = board;
                Phase = phase;
                Pot = pot;
                CallAmount = call;
                PlayerChips = chips;
                StreetRaiseCount = streetRaiseCount;
                ExpectRaiseBlocked = expectRaiseBlocked;
                ExpectedAdvice = expectedAdvice;
            }
        }
    }
}
