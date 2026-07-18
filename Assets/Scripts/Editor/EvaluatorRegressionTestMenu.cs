using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class EvaluatorRegressionTestMenu
    {
        private const string ResultsPath = "Temp/EvaluatorRegressionTestResults.txt";

        [MenuItem("Texas Hold'em/Tests/Run All Evaluator Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = EvaluatorRegressionTestRunner.RunAllSuites();
            Debug.Log($"[EvaluatorRegression] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = EvaluatorRegressionTestRunner.RunAllSuites();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[EvaluatorRegression] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
