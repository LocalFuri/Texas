using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Regression tests for turn/river huge-call gate (weak underpairs vs shoves).</summary>
    public sealed class PostflopHugeCallGateTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Postflop Huge-Call Gate Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PostflopHugeCall] Running {cases.Count} scenario(s)...");

            foreach (HugeCallTestCase testCase in cases)
            {
                bool gateOk = BettingAdvisor.PassesHugeCallGate(
                    testCase.Phase,
                    testCase.CallAmount,
                    testCase.PlayerChips,
                    testCase.HoleCards,
                    testCase.Board,
                    out string blockReason);

                bool underpair = BettingAdvisor.IsPocketUnderpair(testCase.HoleCards, testCase.Board);
                HandRank made = BettingAdvisor.GetMadeHandRank(testCase.HoleCards, testCase.Board);

                // Equity high enough that pot-odds alone would Call.
                BettingAdvice advice = BettingAdvisor.Recommend(
                    equityPercent: 70f,
                    potBeforeAction: testCase.Pot,
                    callAmount: testCase.CallAmount,
                    canCheck: false,
                    canRaise: false,
                    canCall: true,
                    isPreflop: false,
                    preflopGroup: PreflopHandGroup.Weak,
                    preflopSeat: PreflopSeatBucket.Button,
                    facingRaise: true,
                    streetRaiseCount: 1,
                    playerChips: testCase.PlayerChips,
                    holeCards: testCase.HoleCards,
                    postflopPhase: testCase.Phase,
                    communityCards: testCase.Board);

                bool ok = gateOk == testCase.ExpectPassGate && advice == testCase.ExpectedAdvice;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopHugeCall] {testCase.Name}\n" +
                    $"  Hole: {testCase.HoleCards[0]} {testCase.HoleCards[1]} " +
                    $"Board: {FormatBoard(testCase.Board)} Phase={testCase.Phase}\n" +
                    $"  Made={made} underpair={underpair} call={testCase.CallAmount} chips={testCase.PlayerChips}\n" +
                    $"  Gate expectedPass={testCase.ExpectPassGate} actual={gateOk} block={blockReason ?? "(none)"}\n" +
                    $"  Advice expected={testCase.ExpectedAdvice} actual={advice}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopHugeCall] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<HugeCallTestCase> BuildTestCases()
        {
            // Victor 8♥8♣ on 2♦ 6♦ 10♦ J♣ facing near-stack shove → Fold (underpair, no strong draw).
            Card[] eights = { C(Suit.Hearts, Rank.Eight), C(Suit.Clubs, Rank.Eight) };
            Card[] board =
            {
                C(Suit.Diamonds, Rank.Two),
                C(Suit.Diamonds, Rank.Six),
                C(Suit.Diamonds, Rank.Ten),
                C(Suit.Clubs, Rank.Jack),
            };

            return new List<HugeCallTestCase>
            {
                new HugeCallTestCase(
                    "Victor 88 on 2d6dTdJc vs near-stack shove → Fold",
                    eights,
                    board,
                    GamePhase.Turn,
                    pot: 400,
                    callAmount: 850,
                    playerChips: 1000,
                    expectPassGate: false,
                    expectedAdvice: BettingAdvice.Fold),

                // Control: smaller call keeps pot-odds Call with same hand.
                new HugeCallTestCase(
                    "88 on same board vs small bet → Call (gate inactive)",
                    eights,
                    board,
                    GamePhase.Turn,
                    pot: 100,
                    callAmount: 20,
                    playerChips: 1000,
                    expectPassGate: true,
                    expectedAdvice: BettingAdvice.Call),
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

        private sealed class HugeCallTestCase
        {
            public string Name { get; }
            public Card[] HoleCards { get; }
            public Card[] Board { get; }
            public GamePhase Phase { get; }
            public int Pot { get; }
            public int CallAmount { get; }
            public int PlayerChips { get; }
            public bool ExpectPassGate { get; }
            public BettingAdvice ExpectedAdvice { get; }

            public HugeCallTestCase(
                string name,
                Card[] holeCards,
                Card[] board,
                GamePhase phase,
                int pot,
                int callAmount,
                int playerChips,
                bool expectPassGate,
                BettingAdvice expectedAdvice)
            {
                Name = name;
                HoleCards = holeCards;
                Board = board;
                Phase = phase;
                Pot = pot;
                CallAmount = callAmount;
                PlayerChips = playerChips;
                ExpectPassGate = expectPassGate;
                ExpectedAdvice = expectedAdvice;
            }
        }
    }
}
