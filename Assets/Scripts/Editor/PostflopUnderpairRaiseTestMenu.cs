using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PostflopUnderpairRaiseTestMenu
    {
        private const string ResultsPath = "Temp/PostflopUnderpairRaiseTestResults.txt";

        [MenuItem("Texas Hold'em/Run Postflop Underpair Raise Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PostflopUnderpairRaiseTestRunner.RunAllTests();
            Debug.Log($"[PostflopUnderpair] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = PostflopUnderpairRaiseTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PostflopUnderpair] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
