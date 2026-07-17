using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Regression tests for turn/river huge-call gate:
    /// underpairs, and board-pair + pocket-pair Two Pair as bluff-catchers.
    /// </summary>
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
                bool boardPairPocket = BettingAdvisor.IsBoardPairPlusPocketPairTwoPair(
                    testCase.HoleCards, testCase.Board);

                // Equity high enough that pot-odds alone would Call (needed≈68% + 3% edge).
                BettingAdvice advice = BettingAdvisor.Recommend(
                    equityPercent: 80f,
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

                bool patternOk = !testCase.ExpectBoardPairPocket.HasValue
                    || boardPairPocket == testCase.ExpectBoardPairPocket.Value;
                bool ok = patternOk
                    && gateOk == testCase.ExpectPassGate
                    && advice == testCase.ExpectedAdvice;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopHugeCall] {testCase.Name}\n" +
                    $"  Hole: {testCase.HoleCards[0]} {testCase.HoleCards[1]} " +
                    $"Board: {FormatBoard(testCase.Board)} Phase={testCase.Phase}\n" +
                    $"  Made={made} underpair={underpair} boardPairPocket={boardPairPocket} " +
                    $"call={testCase.CallAmount} chips={testCase.PlayerChips}\n" +
                    $"  Gate expectedPass={testCase.ExpectPassGate} actual={gateOk} " +
                    $"block={blockReason ?? "(none)"}\n" +
                    $"  Advice expected={testCase.ExpectedAdvice} actual={advice}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopHugeCall] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<HugeCallTestCase> BuildTestCases()
        {
            // Underpair on unpaired board vs near-stack shove → Fold.
            Card[] underpairHole = { C(Suit.Hearts, Rank.Eight), C(Suit.Clubs, Rank.Eight) };
            Card[] unpairedBoard =
            {
                C(Suit.Diamonds, Rank.Two),
                C(Suit.Diamonds, Rank.Six),
                C(Suit.Diamonds, Rank.Ten),
                C(Suit.Clubs, Rank.Jack),
            };

            // Pattern: pocket pair + paired board → Two Pair bluff-catcher.
            Card[] pocketOnPairedBoard = { C(Suit.Hearts, Rank.Jack), C(Suit.Clubs, Rank.Jack) };
            Card[] pairedBoard =
            {
                C(Suit.Spades, Rank.Ace),
                C(Suit.Hearts, Rank.Ace),
                C(Suit.Diamonds, Rank.King),
                C(Suit.Clubs, Rank.Seven),
            };

            // Pattern: both hole cards pair distinct board ranks → genuine Two Pair.
            Card[] genuineTwoPairHole = { C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.Seven) };

            return new List<HugeCallTestCase>
            {
                new HugeCallTestCase(
                    "Underpair vs near-stack turn shove → Fold",
                    underpairHole,
                    unpairedBoard,
                    GamePhase.Turn,
                    pot: 400,
                    callAmount: 850,
                    playerChips: 1000,
                    expectPassGate: false,
                    expectedAdvice: BettingAdvice.Fold),

                new HugeCallTestCase(
                    "Underpair vs small bet → Call (gate inactive)",
                    underpairHole,
                    unpairedBoard,
                    GamePhase.Turn,
                    pot: 100,
                    callAmount: 20,
                    playerChips: 1000,
                    expectPassGate: true,
                    expectedAdvice: BettingAdvice.Call),

                new HugeCallTestCase(
                    "Board-pair + pocket-pair TwoPair vs near-stack shove → Fold",
                    pocketOnPairedBoard,
                    pairedBoard,
                    GamePhase.Turn,
                    pot: 400,
                    callAmount: 850,
                    playerChips: 1000,
                    expectPassGate: false,
                    expectedAdvice: BettingAdvice.Fold,
                    expectBoardPairPocket: true),

                new HugeCallTestCase(
                    "Genuine TwoPair vs near-stack shove → Call",
                    genuineTwoPairHole,
                    pairedBoard,
                    GamePhase.Turn,
                    pot: 400,
                    callAmount: 850,
                    playerChips: 1000,
                    expectPassGate: true,
                    expectedAdvice: BettingAdvice.Call,
                    expectBoardPairPocket: false),
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
            public bool? ExpectBoardPairPocket { get; }

            public HugeCallTestCase(
                string name,
                Card[] holeCards,
                Card[] board,
                GamePhase phase,
                int pot,
                int callAmount,
                int playerChips,
                bool expectPassGate,
                BettingAdvice expectedAdvice,
                bool? expectBoardPairPocket = null)
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
                ExpectBoardPairPocket = expectBoardPairPocket;
            }
        }
    }
}
