using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Wet-board policy: One Pair without FD/OESD never raises; bluff-catchers fold to substantial bets.
    /// Dry boards unchanged.
    /// </summary>
    public sealed class PostflopWetBoardTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Postflop Wet-Board Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildCases();
            int passed = 0;

            Debug.Log($"[PostflopWet] Running {cases.Count} scenario(s)...");

            foreach (WetCase c in cases)
            {
                bool wet = BoardTextureAnalyzer.IsWet(c.Board);
                HandRank made = BettingAdvisor.GetMadeHandRank(c.Hole, c.Board);

                BettingAdvice advice = BettingAdvisor.Recommend(
                    equityPercent: c.Equity,
                    potBeforeAction: c.Pot,
                    callAmount: c.CallAmount,
                    canCheck: c.CanCheck,
                    canRaise: true,
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
                    activeOpponentCount: 1);

                bool raiseBlocked = BettingAdvisor.IsWetOnePairRaiseBlocked(c.Hole, c.Board, out _);
                bool gateOk = !c.ExpectWetRaiseBlocked.HasValue
                    || raiseBlocked == c.ExpectWetRaiseBlocked.Value;
                bool adviceOk = advice == c.Expected;
                bool wetOk = wet == c.ExpectWet;
                bool ok = gateOk && adviceOk && wetOk;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopWet] {c.Name}\n" +
                    $"  wet={wet} made={made} advice={advice} expected={c.Expected}\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopWet] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<WetCase> BuildCases()
        {
            // Dry: K♠ 7♥ 2♦ — no wet flags.
            Card[] dryBoard =
            {
                C(Suit.Spades, Rank.King),
                C(Suit.Hearts, Rank.Seven),
                C(Suit.Diamonds, Rank.Two),
            };
            Card[] topPair = { C(Suit.Hearts, Rank.King), C(Suit.Clubs, Rank.Nine) };

            // Wet three-flush: K♥ 7♥ 2♥
            Card[] wetFlushBoard =
            {
                C(Suit.Hearts, Rank.King),
                C(Suit.Hearts, Rank.Seven),
                C(Suit.Hearts, Rank.Two),
            };
            // One pair, no heart → no FD (Trips+ N/A). K♠ 9♣ on hearts board = top pair, no FD.
            Card[] topPairNoFd = { C(Suit.Spades, Rank.King), C(Suit.Clubs, Rank.Nine) };

            // Wet turn for large-bet fold (add offsuit turn).
            Card[] wetFlushTurn =
            {
                C(Suit.Hearts, Rank.King),
                C(Suit.Hearts, Rank.Seven),
                C(Suit.Hearts, Rank.Two),
                C(Suit.Clubs, Rank.Eight),
            };

            // Facing: pot 100, call 40 → needed=28.6%; raise needs ~43.6%. Use equity 50% to clear raise on dry.
            // Wet bluff-catcher fold: call 300 / chips 1000 = 30% ≥ 25% substantial; equity 80% would call on pot odds.
            return new List<WetCase>
            {
                new WetCase(
                    "Dry One Pair facing bet can Raise",
                    topPair, dryBoard, GamePhase.Flop,
                    expectWet: false, equity: 50f, pot: 100, callAmount: 40, chips: 1000,
                    canCheck: false, streetRaiseCount: 1,
                    expected: BettingAdvice.Raise,
                    expectWetRaiseBlocked: false),

                new WetCase(
                    "Wet One Pair without FD/OESD does not Raise",
                    topPairNoFd, wetFlushBoard, GamePhase.Flop,
                    expectWet: true, equity: 50f, pot: 100, callAmount: 40, chips: 1000,
                    canCheck: false, streetRaiseCount: 1,
                    expected: BettingAdvice.Call,
                    expectWetRaiseBlocked: true),

                new WetCase(
                    "Wet bluff-catcher folds to substantial turn bet",
                    topPairNoFd, wetFlushTurn, GamePhase.Turn,
                    expectWet: true, equity: 80f, pot: 400, callAmount: 300, chips: 1000,
                    canCheck: false, streetRaiseCount: 1,
                    expected: BettingAdvice.Fold,
                    expectWetRaiseBlocked: true),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private sealed class WetCase
        {
            public string Name { get; }
            public Card[] Hole { get; }
            public Card[] Board { get; }
            public GamePhase Phase { get; }
            public bool ExpectWet { get; }
            public float Equity { get; }
            public int Pot { get; }
            public int CallAmount { get; }
            public int Chips { get; }
            public bool CanCheck { get; }
            public int StreetRaiseCount { get; }
            public BettingAdvice Expected { get; }
            public bool? ExpectWetRaiseBlocked { get; }

            public WetCase(
                string name,
                Card[] hole,
                Card[] board,
                GamePhase phase,
                bool expectWet,
                float equity,
                int pot,
                int callAmount,
                int chips,
                bool canCheck,
                int streetRaiseCount,
                BettingAdvice expected,
                bool? expectWetRaiseBlocked)
            {
                Name = name;
                Hole = hole;
                Board = board;
                Phase = phase;
                ExpectWet = expectWet;
                Equity = equity;
                Pot = pot;
                CallAmount = callAmount;
                Chips = chips;
                CanCheck = canCheck;
                StreetRaiseCount = streetRaiseCount;
                Expected = expected;
                ExpectWetRaiseBlocked = expectWetRaiseBlocked;
            }
        }
    }
}
