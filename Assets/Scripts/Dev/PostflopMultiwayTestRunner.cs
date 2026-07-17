using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Multiway postflop: HU unchanged; 3+/4-way tighter raises, marginal calls, no thin/semi-bluff.
    /// </summary>
    public sealed class PostflopMultiwayTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Postflop Multiway Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildCases();
            int passed = 0;

            Debug.Log($"[PostflopMultiway] Running {cases.Count} scenario(s)...");

            foreach (MultiwayCase c in cases)
            {
                BettingAdvice advice = BettingAdvisor.Recommend(
                    equityPercent: c.Equity,
                    potBeforeAction: c.Pot,
                    callAmount: c.CallAmount,
                    canCheck: c.CanCheck,
                    canRaise: c.CanRaise,
                    canCall: c.CallAmount > 0,
                    isPreflop: false,
                    preflopGroup: PreflopHandGroup.Weak,
                    preflopSeat: PreflopSeatBucket.Button,
                    facingRaise: c.CallAmount > 0,
                    streetRaiseCount: c.StreetRaiseCount,
                    playerChips: c.Chips,
                    holeCards: c.Hole,
                    postflopPhase: c.Phase,
                    communityCards: c.Board,
                    activeOpponentCount: c.Opponents);

                // Overlays (semi-bluff / thin value) — only when checked-to Check from advisor.
                BettingAdvice afterOverlay = advice;
                if (c.ApplyOverlays)
                {
                    afterOverlay = ApplyOverlaysForTest(
                        advice, c.Phase, c.CanCheck, c.CanRaise, c.Equity, c.Opponents, c.Hole, c.Board);
                }

                BettingAdvice actual = c.ApplyOverlays ? afterOverlay : advice;
                bool ok = actual == c.Expected;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopMultiway] {c.Name}\n" +
                    $"  Opponents={c.Opponents} equity={c.Equity:F0}% call={c.CallAmount} pot={c.Pot}\n" +
                    $"  Advisor={advice} overlays={c.ApplyOverlays} actual={actual} expected={c.Expected}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopMultiway] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        /// <summary>Mirrors AIController multiway gates for thin value / semi-bluff without full DecideAction.</summary>
        private static BettingAdvice ApplyOverlaysForTest(
            BettingAdvice advice,
            GamePhase phase,
            bool canCheck,
            bool canRaise,
            float equity,
            int opponents,
            Card[] hole,
            Card[] board)
        {
            if (!canCheck || !canRaise || advice != BettingAdvice.Check)
                return advice;

            // Semi-bluff: disabled multiway (opponents >= 2).
            if (opponents < 2 && (phase == GamePhase.Flop || phase == GamePhase.Turn))
            {
                PostflopDrawFlags draws = PostflopDrawDetector.Detect(hole, board);
                if ((draws & (PostflopDrawFlags.FlushDraw | PostflopDrawFlags.OpenEndedStraightDraw)) != 0)
                    return BettingAdvice.Raise;
            }

            // Thin value: HU only, flop, dry, 52–65%.
            if (phase == GamePhase.Flop && opponents <= 1
                && equity >= 52f && equity < 65f)
            {
                BoardTextureFlags texture = BoardTextureAnalyzer.Analyze(board);
                BoardTextureFlags wet =
                    BoardTextureFlags.ThreeFlush
                    | BoardTextureFlags.FourFlush
                    | BoardTextureFlags.Connected
                    | BoardTextureFlags.FourStraight;
                if ((texture & wet) == 0)
                    return BettingAdvice.Raise;
            }

            return advice;
        }

        private static List<MultiwayCase> BuildCases()
        {
            // Top pair / one pair on dry flop for call tests.
            Card[] topPairHole = { C(Suit.Hearts, Rank.King), C(Suit.Clubs, Rank.Nine) };
            Card[] dryFlop =
            {
                C(Suit.Spades, Rank.King),
                C(Suit.Hearts, Rank.Seven),
                C(Suit.Diamonds, Rank.Two),
            };

            // Genuine two pair.
            Card[] twoPairHole = { C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.Seven) };

            // Flush draw for semi-bluff (e.g. A♥J♥ on K♥ 8♥ 2♣).
            Card[] fdHole = { C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.Jack) };
            Card[] fdFlop =
            {
                C(Suit.Hearts, Rank.King),
                C(Suit.Hearts, Rank.Eight),
                C(Suit.Clubs, Rank.Two),
            };

            // Pot 100, call 20 → needed = 16.67%. HU call needs ~19.7%; multiway one-pair ~24.7%.
            const int pot = 100;
            const int call = 20;
            const int chips = 1000;

            return new List<MultiwayCase>
            {
                // --- Heads-up unchanged ---
                new MultiwayCase(
                    "HU checked-to 70% → Raise (65% threshold)",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 1, equity: 70f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: false, expected: BettingAdvice.Raise),

                new MultiwayCase(
                    "HU one-pair call at 21% equity (needed~17%+3) → Call",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 1, equity: 21f, pot: pot, callAmount: call, chips: chips,
                    canCheck: false, canRaise: true, streetRaiseCount: 1,
                    applyOverlays: false, expected: BettingAdvice.Call),

                new MultiwayCase(
                    "HU FD semi-bluff overlay → Raise",
                    fdHole, fdFlop, GamePhase.Flop,
                    opponents: 1, equity: 40f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: true, expected: BettingAdvice.Raise),

                new MultiwayCase(
                    "HU thin value 55% dry → Raise",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 1, equity: 55f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: true, expected: BettingAdvice.Raise),

                // --- 3-way tighter ---
                new MultiwayCase(
                    "3-way checked-to 70% → Check (+10% raise floor)",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 2, equity: 70f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: false, expected: BettingAdvice.Check),

                new MultiwayCase(
                    "3-way checked-to 76% → Raise (clears 75%)",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 2, equity: 76f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: false, expected: BettingAdvice.Raise),

                new MultiwayCase(
                    "3-way one-pair 21% → Fold (+5% call margin)",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 2, equity: 21f, pot: pot, callAmount: call, chips: chips,
                    canCheck: false, canRaise: true, streetRaiseCount: 1,
                    applyOverlays: false, expected: BettingAdvice.Fold),

                new MultiwayCase(
                    "3-way two-pair 21% → Call (TwoPair+ call margin unchanged)",
                    twoPairHole, dryFlop, GamePhase.Flop,
                    opponents: 2, equity: 21f, pot: pot, callAmount: call, chips: chips,
                    canCheck: false, canRaise: true, streetRaiseCount: 1,
                    applyOverlays: false, expected: BettingAdvice.Call),

                new MultiwayCase(
                    "3-way FD no semi-bluff → Check",
                    fdHole, fdFlop, GamePhase.Flop,
                    opponents: 2, equity: 40f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: true, expected: BettingAdvice.Check),

                new MultiwayCase(
                    "3-way no thin value 55% → Check",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 2, equity: 55f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: true, expected: BettingAdvice.Check),

                // --- 4-way tighter (same gates as 3-way) ---
                new MultiwayCase(
                    "4-way checked-to 70% → Check",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 3, equity: 70f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: false, expected: BettingAdvice.Check),

                new MultiwayCase(
                    "4-way one-pair 21% → Fold",
                    topPairHole, dryFlop, GamePhase.Flop,
                    opponents: 3, equity: 21f, pot: pot, callAmount: call, chips: chips,
                    canCheck: false, canRaise: true, streetRaiseCount: 1,
                    applyOverlays: false, expected: BettingAdvice.Fold),

                new MultiwayCase(
                    "4-way FD no semi-bluff → Check",
                    fdHole, fdFlop, GamePhase.Flop,
                    opponents: 3, equity: 40f, pot: pot, callAmount: 0, chips: chips,
                    canCheck: true, canRaise: true, streetRaiseCount: 0,
                    applyOverlays: true, expected: BettingAdvice.Check),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private sealed class MultiwayCase
        {
            public string Name { get; }
            public Card[] Hole { get; }
            public Card[] Board { get; }
            public GamePhase Phase { get; }
            public int Opponents { get; }
            public float Equity { get; }
            public int Pot { get; }
            public int CallAmount { get; }
            public int Chips { get; }
            public bool CanCheck { get; }
            public bool CanRaise { get; }
            public int StreetRaiseCount { get; }
            public bool ApplyOverlays { get; }
            public BettingAdvice Expected { get; }

            public MultiwayCase(
                string name,
                Card[] hole,
                Card[] board,
                GamePhase phase,
                int opponents,
                float equity,
                int pot,
                int callAmount,
                int chips,
                bool canCheck,
                bool canRaise,
                int streetRaiseCount,
                bool applyOverlays,
                BettingAdvice expected)
            {
                Name = name;
                Hole = hole;
                Board = board;
                Phase = phase;
                Opponents = opponents;
                Equity = equity;
                Pot = pot;
                CallAmount = callAmount;
                Chips = chips;
                CanCheck = canCheck;
                CanRaise = canRaise;
                StreetRaiseCount = streetRaiseCount;
                ApplyOverlays = applyOverlays;
                Expected = expected;
            }
        }
    }
}
