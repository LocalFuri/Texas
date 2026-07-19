using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class AceMaverickMpOpenAllowlistTestMenu
    {
        private const string ResultsPath = "Temp/AceMaverickMpOpenAllowlistTestResults.txt";

        [MenuItem("Texas Hold'em/Tests/Preflop/Ace Maverick MP Open Allowlist")]
        public static void RunFromMenu()
        {
            (int passed, int total) = AceMaverickMpOpenAllowlistTestRunner.RunAllTests();
            Debug.Log($"[AceMpOpenAllowlist] Menu complete: {passed}/{total}");
        }

        public static void RunFromBatch()
        {
            (int passed, int total) = AceMaverickMpOpenAllowlistTestRunner.RunAllTests();
            string line = passed == total ? $"PASS {passed}/{total}" : $"FAIL {passed}/{total}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[AceMpOpenAllowlist] {line} (wrote {ResultsPath})");
            EditorApplication.Exit(passed == total ? 0 : 1);
        }
    }
}
