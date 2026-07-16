using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PreflopFacingAllInTestMenu
    {
        private const string ResultsPath = "Temp/PreflopFacingAllInTestResults.txt";

        [MenuItem("Texas Hold'em/Run Preflop Facing-All-In Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PreflopFacingAllInTestRunner.RunAllTests();
            Debug.Log($"[PreflopFacingAllInTest] Menu complete: {passed}/{total}");
        }

        /// <summary>Batchmode: Unity -batchmode -quit -projectPath ... -executeMethod TexasHoldem.PreflopFacingAllInTestMenu.RunFromBatch</summary>
        public static void RunFromBatch()
        {
            (int passed, int total) = PreflopFacingAllInTestRunner.RunAllTests();
            string line = $"PASS {passed}/{total}";
            if (passed != total)
                line = $"FAIL {passed}/{total}";

            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PreflopFacingAllInTest] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
