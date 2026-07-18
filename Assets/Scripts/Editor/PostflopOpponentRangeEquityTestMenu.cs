using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PostflopOpponentRangeEquityTestMenu
    {
        private const string ResultsPath = "Temp/PostflopOpponentRangeEquityTestResults.txt";

        [MenuItem("Texas Hold'em/Tests/Postflop/Opponent-Range Equity")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PostflopOpponentRangeEquityTestRunner.RunAllTests();
            Debug.Log($"[PostflopOppRange] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = PostflopOpponentRangeEquityTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PostflopOppRange] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
