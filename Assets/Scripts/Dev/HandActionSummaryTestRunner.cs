using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Hand Summary must show the deciding player's action, not a later Fold by someone else.</summary>
    public sealed class HandActionSummaryTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Hand Action Summary Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            int passed = 0;
            const int total = 2;

            Debug.Log("[HandSummary] Running Hand Summary regression(s)...");

            if (RunRaiseSurvivesLaterFold())
                passed++;
            if (RunCallSurvivesLaterFold())
                passed++;

            Debug.Log($"[HandSummary] Complete: {passed}/{total} passed.");
            return (passed, total);
        }

        private static bool RunRaiseSurvivesLaterFold()
        {
            var log = new HandActionLog();
            log.Record(GamePhase.PreFlop, Named("Hero"), BettingAction.Raise, 60, 90, 1);
            log.Record(GamePhase.PreFlop, Named("Villain"), BettingAction.Fold, 0, 90, 1);

            string summary = log.FormatStreetSummary("Hero", GamePhase.Flop, BettingAction.Check);
            bool ok = summary.Contains("Preflop: Raise")
                && !summary.Contains("Preflop: Fold");

            Debug.Log(
                $"[HandSummary] Preflop Raise survives later Fold\n" +
                $"  summary:\n{summary}\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }

        private static bool RunCallSurvivesLaterFold()
        {
            var log = new HandActionLog();
            log.Record(GamePhase.PreFlop, Named("Hero"), BettingAction.Call, 20, 40, 0);
            log.Record(GamePhase.PreFlop, Named("Villain"), BettingAction.Fold, 0, 40, 0);

            string summary = log.FormatStreetSummary("Hero");
            bool ok = summary.Contains("Preflop: Call")
                && !summary.Contains("Preflop: Fold");

            Debug.Log(
                $"[HandSummary] Preflop Call survives later Fold\n" +
                $"  summary:\n{summary}\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");
            return ok;
        }

        private static PlayerState Named(string name) =>
            new PlayerState(name, PlayerType.AI, 1000);
    }
}
