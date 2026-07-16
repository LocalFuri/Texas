using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PreflopUnopenedRangeTestMenu
    {
        private const string ResultsPath = "Temp/PreflopUnopenedRangeTestResults.txt";

        [MenuItem("Texas Hold'em/Run Preflop Unopened Range Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PreflopUnopenedRangeTestRunner.RunAllTests();
            Debug.Log($"[PreflopUnopenedTest] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = PreflopUnopenedRangeTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PreflopUnopenedTest] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
