using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class HandEvaluatorShowdownTestMenu
    {
        private const string ResultsPath = "Temp/HandEvaluatorShowdownTestResults.txt";

        [MenuItem("Texas Hold'em/Run HandEvaluator Showdown Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = HandEvaluatorShowdownTestRunner.RunAllTests();
            Debug.Log($"[HandEvalShowdown] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = HandEvaluatorShowdownTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[HandEvalShowdown] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
