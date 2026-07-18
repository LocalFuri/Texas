using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PostflopHugeCallGateTestMenu
    {
        private const string ResultsPath = "Temp/PostflopHugeCallGateTestResults.txt";

        [MenuItem("Texas Hold'em/Tests/Postflop/Huge-Call Gate")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PostflopHugeCallGateTestRunner.RunAllTests();
            Debug.Log($"[PostflopHugeCall] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = PostflopHugeCallGateTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PostflopHugeCall] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
