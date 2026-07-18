using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PreflopHandSmokeTestMenu
    {
        private const string ResultsPath = "Temp/PreflopHandSmokeTestResults.txt";
        private const int DefaultHands = 10_000;

        [MenuItem("Texas Hold'em/Tests/Preflop/Hand Smoke")]
        public static void RunFromMenu()
        {
            (bool ok, PreflopHandSmokeTestRunner.SmokeStats stats) =
                PreflopHandSmokeTestRunner.RunAllTests(DefaultHands);
            Debug.Log($"[PreflopSmoke] Menu complete: {(ok ? "PASS" : "FAIL")} hands={stats.HandsPlayed}");
        }

        public static void RunFromBatch()
        {
            (bool ok, PreflopHandSmokeTestRunner.SmokeStats stats) =
                PreflopHandSmokeTestRunner.RunAllTests(DefaultHands);
            string line = ok
                ? $"PASS hands={stats.HandsPlayed} completed={stats.BettingRoundsCompleted} " +
                  $"fold={stats.Folds} call={stats.Calls} raise={stats.Raises}"
                : $"FAIL hands={stats.HandsPlayed} exceptions={stats.Exceptions} " +
                  $"illegal={stats.IllegalActions} last={stats.LastError}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PreflopSmoke] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
