using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Runs fixed postflop / board / logger Dev suites in one pass.
    /// Does not include smoke or statistics runners.
    /// </summary>
    public sealed class PostflopRegressionTestRunner : MonoBehaviour
    {
        [ContextMenu("Run All Postflop Dev Tests")]
        private void RunFromContextMenu() => RunAllSuites();

        /// <summary>Returns overall (passed, total) across all suites.</summary>
        public static (int passed, int total) RunAllSuites()
        {
            Debug.Log("[PostflopRegression] Running all postflop Dev suites...");

            int passed = 0;
            int total = 0;

            Accumulate(ref passed, ref total, "opponent-range-equity",
                PostflopOpponentRangeEquityTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "underpair-raise",
                PostflopUnderpairRaiseTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "huge-call-gate",
                PostflopHugeCallGateTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "raise-escalation",
                PostflopRaiseEscalationTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "dry-value-bet",
                PostflopDryValueBetTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "wet-board",
                PostflopWetBoardTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "multiway",
                PostflopMultiwayTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "logger-reason",
                PostflopLoggerReasonTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "draw-detector",
                PostflopDrawDetectorTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "board-texture",
                BoardTextureAnalyzerTestRunner.RunAllTests());
            Accumulate(ref passed, ref total, "hand-action-summary",
                HandActionSummaryTestRunner.RunAllTests());

            Debug.Log($"[PostflopRegression] Overall: {passed}/{total} passed.");
            return (passed, total);
        }

        private static void Accumulate(
            ref int passed, ref int total, string suiteName, (int passed, int total) result)
        {
            passed += result.passed;
            total += result.total;
            Debug.Log(
                $"[PostflopRegression] Suite={suiteName} " +
                $"passed={result.passed} failed={result.total - result.passed} total={result.total}");
        }
    }
}
