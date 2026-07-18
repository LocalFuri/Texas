using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class HandEvaluatorFastTestMenu
    {
        private const string CorrectnessResults = "Temp/HandEvaluatorFastCorrectnessResults.txt";
        private const string BenchmarkResults   = "Temp/HandEvaluatorFastBenchmarkResults.txt";

        [MenuItem("Texas Hold'em/Tests/Evaluator/Fast Correctness")]
        public static void RunCorrectnessFromMenu()
        {
            bool ok = HandEvaluatorFastCorrectnessTestRunner.RunAllTests();
            Debug.Log($"[HandEvalFast] Menu correctness: {(ok ? "PASS" : "FAIL")}");
        }

        [MenuItem("Texas Hold'em/Benchmarks/HandEvaluatorFast")]
        public static void RunBenchmarkFromMenu()
        {
            HandEvaluatorFastBenchmarkRunner.RunBenchmark();
        }

        public static void RunCorrectnessFromBatch()
        {
            bool ok = HandEvaluatorFastCorrectnessTestRunner.RunAllTests();
            string line = ok ? "PASS" : "FAIL";
            Directory.CreateDirectory(Path.GetDirectoryName(CorrectnessResults) ?? "Temp");
            File.WriteAllText(CorrectnessResults, line + "\n");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void RunBenchmarkFromBatch()
        {
            HandEvaluatorFastBenchmarkRunner.RunBenchmark();
            File.WriteAllText(BenchmarkResults, "DONE\n");
            EditorApplication.Exit(0);
        }
    }
}
