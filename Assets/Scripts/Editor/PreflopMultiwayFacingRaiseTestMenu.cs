using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PreflopMultiwayFacingRaiseTestMenu
    {
        private const string ResultsPath = "Temp/PreflopMultiwayFacingRaiseTestResults.txt";

        [MenuItem("Texas Hold'em/Run Preflop Multiway Facing-Raise Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PreflopMultiwayFacingRaiseTestRunner.RunAllTests();
            Debug.Log($"[PreflopMultiwayTest] Menu complete: {passed}/{total}");
        }

        /// <summary>Batchmode entry point.</summary>
        public static void RunFromBatch()
        {
            (int passed, int total) = PreflopMultiwayFacingRaiseTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PreflopMultiwayTest] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
