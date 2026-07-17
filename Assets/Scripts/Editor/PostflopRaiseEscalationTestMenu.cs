using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PostflopRaiseEscalationTestMenu
    {
        private const string ResultsPath = "Temp/PostflopRaiseEscalationTestResults.txt";

        [MenuItem("Texas Hold'em/Run Postflop Raise Escalation Tests")]
        public static void RunFromMenu()
        {
            (int passed, int total) = PostflopRaiseEscalationTestRunner.RunAllTests();
            Debug.Log($"[PostflopEscalation] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = PostflopRaiseEscalationTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PostflopEscalation] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
