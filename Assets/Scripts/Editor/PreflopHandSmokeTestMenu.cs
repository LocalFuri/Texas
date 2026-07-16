using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PreflopHandSmokeTestMenu
    {
        private const string ResultsPath = "Temp/PreflopHandSmokeTestResults.txt";

        [MenuItem("Texas Hold'em/Run Preflop Hand Smoke Test")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PreflopHandSmokeTestRunner.RunAllTests();
            Debug.Log($"[PreflopSmoke] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = PreflopHandSmokeTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PreflopSmoke] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
