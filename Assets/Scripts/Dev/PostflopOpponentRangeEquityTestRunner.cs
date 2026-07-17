using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Regression: same hand/board — equity vs turn shove range is lower than vs passive check/call.
    /// </summary>
    public sealed class PostflopOpponentRangeEquityTestRunner : MonoBehaviour
    {
        private const int SimulationCount = 10_000;
        private const float MinEquityGapPercent = 3f;

        [ContextMenu("Run Postflop Opponent-Range Equity Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            int passed = 0;
            const int total = 2;

            Debug.Log("[PostflopOppRange] Running opponent-range equity regression(s)...");

            if (RunResolveMappingCase())
                passed++;
            if (RunShoveVsPassiveEquityCase())
                passed++;

            Debug.Log($"[PostflopOppRange] Complete: {passed}/{total} passed.");
            return (passed, total);
        }

        private static bool RunResolveMappingCase()
        {
            bool ok =
                MonteCarloSimulator.ResolveOpponentRange(false, 0, 0, 1000)
                    == OpponentRangeStrength.Wide
                && MonteCarloSimulator.ResolveOpponentRange(true, 1, 100, 1000)
                    == OpponentRangeStrength.Strong
                && MonteCarloSimulator.ResolveOpponentRange(true, 2, 100, 1000)
                    == OpponentRangeStrength.Strongest
                && MonteCarloSimulator.ResolveOpponentRange(true, 1, 850, 1000)
                    == OpponentRangeStrength.Strongest;

            Debug.Log(
                $"[PostflopOppRange] ResolveOpponentRange mapping\n" +
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
    }
}
