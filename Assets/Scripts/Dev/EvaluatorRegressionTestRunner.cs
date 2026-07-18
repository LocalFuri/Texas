using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Runs hand-evaluator correctness suites (showdown winners + Fast parity).
    /// Does not include benchmarks.
    /// </summary>
    public sealed class EvaluatorRegressionTestRunner : MonoBehaviour
    {
        [ContextMenu("Run All Evaluator Dev Tests")]
        private void RunFromContextMenu() => RunAllSuites();

        /// <summary>Returns overall (passed, total). Fast correctness contributes as 1 suite unit.</summary>
        public static (int passed, int total) RunAllSuites()
        {
            Debug.Log("[EvaluatorRegression] Running all evaluator Dev suites...");

            (int showPassed, int showTotal) = HandEvaluatorShowdownTestRunner.RunAllTests();
            Debug.Log(
                $"[EvaluatorRegression] Suite=showdown " +
                $"passed={showPassed} failed={showTotal - showPassed} total={showTotal}");

            bool fastOk = HandEvaluatorFastCorrectnessTestRunner.RunAllTests();
            int fastPassed = fastOk ? 1 : 0;
            Debug.Log(
                $"[EvaluatorRegression] Suite=fast-correctness " +
                $"passed={fastPassed} failed={1 - fastPassed} total=1");

            int passed = showPassed + fastPassed;
            int total = showTotal + 1;
            Debug.Log($"[EvaluatorRegression] Overall: {passed}/{total} passed.");
            return (passed, total);
        }
    }
}
