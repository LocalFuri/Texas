using System.IO;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class PostflopAiStatisticsMenu
    {
        private const string ResultsPath = "Temp/PostflopAiStatisticsResults.txt";

        private const int Hands100   = 100;
        private const int Hands1000  = 1_000;
        private const int Hands10000 = 10_000;

        [MenuItem("Texas Hold'em/Postflop AI Statistics/Run 100 Hands")]
        public static void Run100() => RunWithProgress(Hands100);

        [MenuItem("Texas Hold'em/Postflop AI Statistics/Run 1,000 Hands")]
        public static void Run1000() => RunWithProgress(Hands1000);

        [MenuItem("Texas Hold'em/Postflop AI Statistics/Run 10,000 Hands")]
        public static void Run10000() => RunWithProgress(Hands10000);

        public static void RunFromBatch() => RunBatchAndExit(Hands10000);

        public static void RunFromBatchSmoke() => RunBatchAndExit(2);

        private static void RunWithProgress(int handCount)
        {
            try
            {
                EditorUtility.DisplayProgressBar(
                    "Postflop AI Statistics",
                    $"Hand 0 of {handCount}",
                    0f);

                PostflopAiStatisticsRunner.StatsResult result =
                    PostflopAiStatisticsRunner.RunAll(
                        handCount,
                        onHandFinished: (handNumber, total) =>
                        {
                            EditorUtility.DisplayProgressBar(
                                "Postflop AI Statistics",
                                $"Hand {handNumber} of {total}",
                                (float)handNumber / total);
                        });

                Debug.Log(
                    $"[PostflopStats] Menu complete: " +
                    $"ok={result.Ok} completed={result.Stats.HandsCompleted}/{result.Stats.HandsAttempted}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void RunBatchAndExit(int handCount)
        {
            try
            {
                EditorUtility.DisplayProgressBar(
                    "Postflop AI Statistics",
                    $"Hand 0 of {handCount}",
                    0f);

                PostflopAiStatisticsRunner.StatsResult result =
                    PostflopAiStatisticsRunner.RunAll(
                        handCount,
                        onHandFinished: (handNumber, total) =>
                        {
                            EditorUtility.DisplayProgressBar(
                                "Postflop AI Statistics",
                                $"Hand {handNumber} of {total}",
                                (float)handNumber / total);
                        });

                StatsToFile(result);
                EditorApplication.Exit(result.Ok ? 0 : 1);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void StatsToFile(PostflopAiStatisticsRunner.StatsResult result)
        {
            var s = result.Stats;
            string line = result.Ok
                ? $"PASS attempted={s.HandsAttempted} completed={s.HandsCompleted} " +
                  $"postflop={s.PostflopDecisions} exceptions={s.Exceptions} illegal={s.IllegalActions}"
                : $"FAIL attempted={s.HandsAttempted} completed={s.HandsCompleted} " +
                  $"exceptions={s.Exceptions} illegal={s.IllegalActions} last={s.LastError}";
            Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath) ?? "Temp");
            File.WriteAllText(ResultsPath, line + "\n");
            Debug.Log($"[PostflopStats] {line} (wrote {ResultsPath})");
        }
    }
}
