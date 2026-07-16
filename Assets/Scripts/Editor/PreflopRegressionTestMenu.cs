using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PreflopRegressionTestMenu
    {
        private const string ResultsPath = "Temp/PreflopRegressionTestResults.txt";

        [MenuItem("Texas Hold'em/Run All Preflop Dev Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PreflopRegressionTestRunner.RunAllSuites();
            Debug.Log($"[PreflopRegression] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = PreflopRegressionTestRunner.RunAllSuites();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PreflopRegression] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
