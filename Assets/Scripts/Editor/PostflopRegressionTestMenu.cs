using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PostflopRegressionTestMenu
    {
        private const string ResultsPath = "Temp/PostflopRegressionTestResults.txt";

        [MenuItem("Texas Hold'em/Tests/Run All Postflop Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PostflopRegressionTestRunner.RunAllSuites();
            Debug.Log($"[PostflopRegression] Menu complete: {passed}/{total}");
        }

        [MenuItem("Texas Hold'em/Tests/Postflop/Dry Value-Bet")]
        public static void RunDryValueBet() => LogSuite(
            "DryValue", PostflopDryValueBetTestRunner.RunAllTests());

        [MenuItem("Texas Hold'em/Tests/Postflop/Wet Board")]
        public static void RunWetBoard() => LogSuite(
            "WetBoard", PostflopWetBoardTestRunner.RunAllTests());

        [MenuItem("Texas Hold'em/Tests/Postflop/Multiway")]
        public static void RunMultiway() => LogSuite(
            "Multiway", PostflopMultiwayTestRunner.RunAllTests());

        [MenuItem("Texas Hold'em/Tests/Postflop/Logger Reason")]
        public static void RunLoggerReason() => LogSuite(
            "LoggerReason", PostflopLoggerReasonTestRunner.RunAllTests());

        [MenuItem("Texas Hold'em/Tests/Postflop/Draw Detector")]
        public static void RunDrawDetector() => LogSuite(
            "DrawDetector", PostflopDrawDetectorTestRunner.RunAllTests());

        [MenuItem("Texas Hold'em/Tests/Postflop/Board Texture")]
        public static void RunBoardTexture() => LogSuite(
            "BoardTexture", BoardTextureAnalyzerTestRunner.RunAllTests());

        [MenuItem("Texas Hold'em/Tests/Postflop/Hand Action Summary")]
        public static void RunHandActionSummary() => LogSuite(
            "HandSummary", HandActionSummaryTestRunner.RunAllTests());

        public static void RunFromBatch()
        {
            (int passed, int total) = PostflopRegressionTestRunner.RunAllSuites();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PostflopRegression] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }

        private static void LogSuite(string label, (int passed, int total) result)
            => Debug.Log($"[{label}] Menu complete: {result.passed}/{result.total}");
    }
}
