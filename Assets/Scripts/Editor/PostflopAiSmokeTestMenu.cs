using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PostflopAiSmokeTestMenu
    {
        private const string ResultsPath = "Temp/PostflopAiSmokeTestResults.txt";
        private const int DefaultDecisions = 10_000;

        [MenuItem("Texas Hold'em/Tests/Postflop/AI Smoke")]
        public static void RunFromMenu()
        {
            (bool ok, PostflopAiSmokeTestRunner.SmokeStats stats) =
                PostflopAiSmokeTestRunner.RunAllTests(DefaultDecisions);
            Debug.Log($"[PostflopSmoke] Menu complete: {(ok ? "PASS" : "FAIL")} " +
                      $"completed={stats.DecisionsCompleted}");
        }

        public static void RunFromBatch()
        {
            (bool ok, PostflopAiSmokeTestRunner.SmokeStats stats) =
                PostflopAiSmokeTestRunner.RunAllTests(DefaultDecisions);
            string line = ok
                ? $"PASS completed={stats.DecisionsCompleted} " +
                  $"fold={stats.Folds} check={stats.Checks} call={stats.Calls} raise={stats.Raises} " +
                  $"flop={stats.FlopDecisions} turn={stats.TurnDecisions} river={stats.RiverDecisions}"
                : $"FAIL completed={stats.DecisionsCompleted} exceptions={stats.Exceptions} " +
                  $"illegal={stats.IllegalActions} last={stats.LastError}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PostflopSmoke] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
