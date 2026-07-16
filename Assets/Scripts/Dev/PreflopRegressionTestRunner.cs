using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Runs all preflop Dev test suites in one pass (facing-all-in, multiway, unopened).</summary>
    public sealed class PreflopRegressionTestRunner : MonoBehaviour
    {
        [ContextMenu("Run All Preflop Dev Tests")]
        private void RunTestsFromContextMenu() => RunAllSuites();

        /// <summary>Returns overall (passed, total) across all suites.</summary>
        public static (int passed, int total) RunAllSuites()
        {
            Debug.Log("[PreflopRegression] Running all preflop Dev suites...");

            (int allInPassed, int allInTotal) = PreflopFacingAllInTestRunner.RunAllTests();
            Debug.Log(
                $"[PreflopRegression] Suite=facing-all-in " +
                $"passed={allInPassed} failed={allInTotal - allInPassed} total={allInTotal}");

            (int multiPassed, int multiTotal) = PreflopMultiwayFacingRaiseTestRunner.RunAllTests();
            Debug.Log(
                $"[PreflopRegression] Suite=multiway-facing-raise " +
                $"passed={multiPassed} failed={multiTotal - multiPassed} total={multiTotal}");

            (int openPassed, int openTotal) = PreflopUnopenedRangeTestRunner.RunAllTests();
            Debug.Log(
                $"[PreflopRegression] Suite=unopened-ranges " +
                $"passed={openPassed} failed={openTotal - openPassed} total={openTotal}");

            int passed = allInPassed + multiPassed + openPassed;
            int total  = allInTotal + multiTotal + openTotal;
            Debug.Log($"[PreflopRegression] Overall: {passed}/{total} passed.");
            return (passed, total);
        }
    }
}
