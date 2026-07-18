using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class NetProfitRegressionTestMenu
    {
        private const string ResultsPath = "Temp/NetProfitRegressionTestResults.txt";

        [MenuItem("Texas Hold'em/Tests/Run Net Profit Regression")]
        public static void RunFromMenu()
        {
            (int passed, int total) = NetProfitRegressionTestRunner.RunAllTests();
            Debug.Log($"[NetProfit] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = NetProfitRegressionTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[NetProfit] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
