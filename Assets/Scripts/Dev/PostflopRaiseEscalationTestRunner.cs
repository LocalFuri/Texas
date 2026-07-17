using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Regression tests for postflop facing-bet raise escalation caps (Hand #16 style).
    /// Does not change production AI beyond exercising <see cref="BettingAdvisor"/>.
    /// </summary>
    public sealed class PostflopRaiseEscalationTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Postflop Raise Escalation Tests")]
        private void RunFromContextMenu() => RunAllTests();

        /// <summary>Returns (passed, total).</summary>
        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildTestCases();
            int passed = 0;

            Debug.Log($"[PostflopEscalation] Running {cases.Count} scenario(s)...");

            foreach (EscalationTestCase testCase in cases)
            {
                bool canEscalate = BettingAdvisor.CanEscalateFacingRaise(
                    testCase.HoleCards,
                    testCase.Board,
                    testCase.StreetRaiseCount,
                    out string blockReason);

                HandRank made = BettingAdvisor.GetMadeHandRank(testCase.HoleCards, testCase.Board);
                PostflopDrawFlags draws = PostflopDrawDetector.Detect(testCase.HoleCards, testCase.Board);

                // High equity so pot-odds alone would raise; caps must still apply.
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
                    playerChips: 1000,
                    holeCards: testCase.HoleCards,
                    postflopPhase: GamePhase.Flop,
                    communityCards: testCase.Board);

                bool escalateOk = canEscalate == testCase.ExpectCanEscalate;
                bool adviceOk = advice == testCase.ExpectedAdvice;
                bool ok = escalateOk && adviceOk;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopEscalation] {testCase.Name}\n" +
                    $"  Hole: {testCase.HoleCards[0]} {testCase.HoleCards[1]} " +
                    $"Board: {FormatBoard(testCase.Board)}\n" +
                    $"  Made={made} Draws={FormatDraws(draws)} StreetRaiseCount={testCase.StreetRaiseCount}\n" +
                    $"  CanEscalate expected={testCase.ExpectCanEscalate} actual={canEscalate} " +
                    $"block={blockReason ?? "(none)"}\n" +
                    $"  Advice expected={testCase.ExpectedAdvice} actual={advice}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopEscalation] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<EscalationTestCase> BuildTestCases()
        {
            // 1) AJ on K72 dry — High Card must not re-raise (any StreetRaiseCount >= 1).
            Card[] aj = { C(Suit.Clubs, Rank.Ace), C(Suit.Diamonds, Rank.Jack) };
            Card[] k72 =
            {
                C(Suit.Hearts, Rank.Two),
                C(Suit.Diamonds, Rank.King),
                C(Suit.Clubs, Rank.Seven),
            };

            // 2) 56 on 247 — OESD (3-4-5-6), High Card + draw: raise once (SRC=1), not again (SRC=2).
            Card[] fiveSix = { C(Suit.Hearts, Rank.Five), C(Suit.Spades, Rank.Six) };
            Card[] board247 =
            {
                C(Suit.Clubs, Rank.Two),
                C(Suit.Diamonds, Rank.Four),
                C(Suit.Hearts, Rank.Seven),
            };

            // 3) 76 on 247 — top pair Sevens; stop escalating at StreetRaiseCount >= 3.
            Card[] sevenSix = { C(Suit.Spades, Rank.Seven), C(Suit.Clubs, Rank.Six) };

            // 4) Two pair (77 on K72 with pocket? Better: A7 on K7x → two pair? 
            //    Use 7♠ 2♦ on K♦ 7♣ 2♥ → two pair Sevens and Twos.
            Card[] twoPairHole = { C(Suit.Spades, Rank.Seven), C(Suit.Diamonds, Rank.Two) };
            Card[] twoPairBoard =
            {
                C(Suit.Diamonds, Rank.King),
                C(Suit.Clubs, Rank.Seven),
                C(Suit.Hearts, Rank.Two),
            };

            // Trips for StreetRaiseCount >= 4 gate: 7♠ 7♥ on K♦ 7♣ 2♥
            Card[] tripsHole = { C(Suit.Spades, Rank.Seven), C(Suit.Hearts, Rank.Seven) };
            Card[] tripsBoard =
            {
                C(Suit.Diamonds, Rank.King),
                C(Suit.Clubs, Rank.Seven),
                C(Suit.Hearts, Rank.Two),
            };

            return new List<EscalationTestCase>
            {
                new EscalationTestCase(
                    "1. AJ on K72 HighCard never re-raise (SRC=1)",
                    aj, k72, streetRaiseCount: 1, pot: 50, call: 15,
                    expectCanEscalate: false, expectedAdvice: BettingAdvice.Call),

                new EscalationTestCase(
                    "1b. AJ on K72 HighCard never re-raise (SRC=3)",
                    aj, k72, streetRaiseCount: 3, pot: 200, call: 80,
                    expectCanEscalate: false, expectedAdvice: BettingAdvice.Call),

                new EscalationTestCase(
                    "2. 56 on 247 OESD may raise once (SRC=1)",
                    fiveSix, board247, streetRaiseCount: 1, pot: 50, call: 15,
                    expectCanEscalate: true, expectedAdvice: BettingAdvice.Raise),

                new EscalationTestCase(
                    "2b. 56 on 247 OESD must not keep reraising (SRC=2)",
                    fiveSix, board247, streetRaiseCount: 2, pot: 100, call: 40,
                    expectCanEscalate: false, expectedAdvice: BettingAdvice.Call),

                new EscalationTestCase(
                    "3. 76 on 247 top pair may raise early (SRC=2)",
                    sevenSix, board247, streetRaiseCount: 2, pot: 100, call: 40,
                    expectCanEscalate: true, expectedAdvice: BettingAdvice.Raise),

                new EscalationTestCase(
                    "3b. 76 on 247 top pair stops at SRC>=3",
                    sevenSix, board247, streetRaiseCount: 3, pot: 200, call: 80,
                    expectCanEscalate: false, expectedAdvice: BettingAdvice.Call),

                new EscalationTestCase(
                    "4. Two pair may raise (SRC=3)",
                    twoPairHole, twoPairBoard, streetRaiseCount: 3, pot: 200, call: 80,
                    expectCanEscalate: true, expectedAdvice: BettingAdvice.Raise),

                new EscalationTestCase(
                    "4b. Two pair blocked at SRC>=4 (needs Trips+)",
                    twoPairHole, twoPairBoard, streetRaiseCount: 4, pot: 400, call: 150,
                    expectCanEscalate: false, expectedAdvice: BettingAdvice.Call),

                new EscalationTestCase(
                    "4c. Trips may raise at SRC>=4",
                    tripsHole, tripsBoard, streetRaiseCount: 4, pot: 400, call: 150,
                    expectCanEscalate: true, expectedAdvice: BettingAdvice.Raise),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static string FormatBoard(IReadOnlyList<Card> board)
        {
            if (board == null || board.Count == 0)
                return "(none)";
            var parts = new List<string>(board.Count);
            foreach (Card c in board)
                parts.Add(c.ToString());
            return string.Join(" ", parts);
        }

        private static string FormatDraws(PostflopDrawFlags flags) =>
            flags == PostflopDrawFlags.None ? "None" : flags.ToString();

        private sealed class EscalationTestCase
        {
            public string Name { get; }
            public Card[] HoleCards { get; }
            public Card[] Board { get; }
            public int StreetRaiseCount { get; }
            public int Pot { get; }
            public int CallAmount { get; }
            public bool ExpectCanEscalate { get; }
            public BettingAdvice ExpectedAdvice { get; }

            public EscalationTestCase(
                string name,
                Card[] holeCards,
                Card[] board,
                int streetRaiseCount,
                int pot,
                int call,
                bool expectCanEscalate,
                BettingAdvice expectedAdvice)
            {
                Name = name;
                HoleCards = holeCards;
                Board = board;
                StreetRaiseCount = streetRaiseCount;
                Pot = pot;
                CallAmount = call;
                ExpectCanEscalate = expectCanEscalate;
                ExpectedAdvice = expectedAdvice;
            }
        }
    }
}
