using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Regression: opponent-range mapping and equity vs Wide / Strong / Strongest.
    /// </summary>
    public sealed class PostflopOpponentRangeEquityTestRunner : MonoBehaviour
    {
        private const int SimulationCount = 10_000;
        private const float MinEquityGapPercent = 3f;
        private const float MinAqStrongVsWideGapPercent = 3f;

        [ContextMenu("Run Postflop Opponent-Range Equity Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            int passed = 0;
            const int total = 5;

            Debug.Log("[PostflopOppRange] Running opponent-range equity regression(s)...");

            if (RunResolveMappingCase())
                passed++;
            if (RunCheckedToFourBetPotUsesStrongest())
                passed++;
            if (RunCheckedToSingleRaisedKeepsWide())
                passed++;
            if (RunShoveVsPassiveEquityCase())
                passed++;
            if (RunAqPairedConnectedStrongTighterThanWide())
                passed++;

            Debug.Log($"[PostflopOppRange] Complete: {passed}/{total} passed.");
            return (passed, total);
        }

        private static bool RunResolveMappingCase()
        {
            bool checkWide = AssertRange(
                "check/call → Wide",
                facingBet: false, streetRaiseCount: 0, callAmount: 0, chips: 1000,
                preflopRaiseCount: 0,
                OpponentRangeStrength.Wide);

            bool betStrong = AssertRange(
                "bet/raise → Strong",
                facingBet: true, streetRaiseCount: 1, callAmount: 100, chips: 1000,
                preflopRaiseCount: 0,
                OpponentRangeStrength.Strong);

            bool reraiseStrongest = AssertRange(
                "re-raise → Strongest",
                facingBet: true, streetRaiseCount: 2, callAmount: 100, chips: 1000,
                preflopRaiseCount: 0,
                OpponentRangeStrength.Strongest);

            bool nearStackStrongest = AssertRange(
                "≥50% stack call → Strongest",
                facingBet: true, streetRaiseCount: 1, callAmount: 850, chips: 1000,
                preflopRaiseCount: 0,
                OpponentRangeStrength.Strongest);

            bool ok = checkWide && betStrong && reraiseStrongest && nearStackStrongest;
            Debug.Log(
                $"[PostflopOppRange] ResolveOpponentRange mapping (check/bet/re-raise/near-stack)\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }

        private static bool RunCheckedToFourBetPotUsesStrongest()
        {
            bool ok = AssertRange(
                "checked-to in 4-bet pot → Strongest (not Wide)",
                facingBet: false, streetRaiseCount: 0, callAmount: 0, chips: 1000,
                preflopRaiseCount: 3,
                OpponentRangeStrength.Strongest);
            Debug.Log(
                $"[PostflopOppRange] Checked-to 4-bet pot floor\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }

        private static bool RunCheckedToSingleRaisedKeepsWide()
        {
            bool ok = AssertRange(
                "checked-to in single-raised pot → Wide (unchanged)",
                facingBet: false, streetRaiseCount: 0, callAmount: 0, chips: 1000,
                preflopRaiseCount: 1,
                OpponentRangeStrength.Wide);
            Debug.Log(
                $"[PostflopOppRange] Checked-to single-raised pot\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }

        private static bool AssertRange(
            string label,
            bool facingBet,
            int streetRaiseCount,
            int callAmount,
            int chips,
            int preflopRaiseCount,
            OpponentRangeStrength expected)
        {
            string why = MonteCarloSimulator.DescribeOpponentRangeSelection(
                facingBet, streetRaiseCount, callAmount, chips,
                out OpponentRangeStrength actual, preflopRaiseCount);
            bool ok = actual == expected;
            Debug.Log(
                $"[PostflopOppRange] {label}\n" +
                $"  expected={expected} actual={actual} why={why}\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }

        private static bool RunShoveVsPassiveEquityCase()
        {
            // Mid-strength bluff-catcher on a dry K-high turn: equity drops vs shove-tight range.
            var hole = new[]
            {
                new Card(Suit.Hearts, Rank.Jack),
                new Card(Suit.Clubs, Rank.Ten),
            };
            var board = new[]
            {
                new Card(Suit.Spades, Rank.King),
                new Card(Suit.Hearts, Rank.Seven),
                new Card(Suit.Diamonds, Rank.Two),
                new Card(Suit.Clubs, Rank.Eight),
            };

            UnityEngine.Random.InitState(20260717);
            MonteCarloResult passive = MonteCarloSimulator.Simulate(
                hole, board, activeOpponentCount: 1, SimulationCount, OpponentRangeStrength.Wide);

            UnityEngine.Random.InitState(20260717);
            MonteCarloResult shove = MonteCarloSimulator.Simulate(
                hole, board, activeOpponentCount: 1, SimulationCount, OpponentRangeStrength.Strongest);

            float gap = passive.EquityPercent - shove.EquityPercent;
            bool ok = gap >= MinEquityGapPercent;

            Debug.Log(
                $"[PostflopOppRange] Turn shove range vs passive check/call (same hand/board)\n" +
                $"  Hole: {hole[0]} {hole[1]} Board: {board[0]} {board[1]} {board[2]} {board[3]}\n" +
                $"  Passive(Wide) equity={passive.EquityPercent:F2}%\n" +
                $"  Shove(Strongest) equity={shove.EquityPercent:F2}%\n" +
                $"  Gap={gap:F2}% (need ≥ {MinEquityGapPercent:F1}%)\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");

            return ok;
        }

        /// <summary>
        /// Paired+connected turn: tightened Strong must be materially worse for AQ than Wide.
        /// </summary>
        private static bool RunAqPairedConnectedStrongTighterThanWide()
        {
            var hole = new[]
            {
                new Card(Suit.Hearts, Rank.Ace),
                new Card(Suit.Hearts, Rank.Queen),
            };
            var board = new[]
            {
                new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Hearts, Rank.Seven),
                new Card(Suit.Diamonds, Rank.Eight),
                new Card(Suit.Clubs, Rank.Eight),
            };

            MonteCarloSimulator.MeasureStrongAcceptanceBeforeAfter(
                hole, board,
                out float legacyAcceptPercent,
                out float currentAcceptPercent);

            UnityEngine.Random.InitState(20260718);
            MonteCarloResult wide = MonteCarloSimulator.Simulate(
                hole, board, activeOpponentCount: 1, SimulationCount, OpponentRangeStrength.Wide);

            UnityEngine.Random.InitState(20260718);
            MonteCarloResult strong = MonteCarloSimulator.Simulate(
                hole, board, activeOpponentCount: 1, SimulationCount, OpponentRangeStrength.Strong);

            float equityGap = wide.EquityPercent - strong.EquityPercent;
            bool equityOk = equityGap >= MinAqStrongVsWideGapPercent;
            bool acceptOk = currentAcceptPercent + 5f < legacyAcceptPercent;
            bool ok = equityOk && acceptOk;

            Debug.Log(
                $"[PostflopOppRange] AQ on 9c7h8d8c — Strong tighter than Wide\n" +
                $"  Hole: {hole[0]} {hole[1]} Board: {board[0]} {board[1]} {board[2]} {board[3]}\n" +
                $"  Wide equity={wide.EquityPercent:F2}%\n" +
                $"  Strong equity={strong.EquityPercent:F2}%\n" +
                $"  Equity gap={equityGap:F2}% (need ≥ {MinAqStrongVsWideGapPercent:F1}%)\n" +
                $"  Strong accept% before (legacy)={legacyAcceptPercent:F2}%\n" +
                $"  Strong accept% after (tightened)={currentAcceptPercent:F2}%\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");

            return ok;
        }
    }
}
