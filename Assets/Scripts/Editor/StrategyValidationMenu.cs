using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    /// <summary>
    /// Editor entry points for the Strategy Validation framework.
    /// </summary>
    public static class StrategyValidationMenu
    {
        private const int CompareIterations = 10_000;
        private const int CompareBaseSeed = 88001;
        private const string CompareResultsPath = "Temp/StrategyValidationCompare88Utg.txt";

        /// <summary>
        /// Raise-to-5 with BB=2: BettingManager raise is an increment above CurrentBet,
        /// so RaiseIncrement = 5 - 2 = 3.
        /// </summary>
        private const int RaiseToFiveIncrement = 3;

        [MenuItem("Texas Hold'em/Statistics/Strategy Validation/Framework Info")]
        public static void ShowFrameworkInfo()
        {
            Debug.LogWarning(
                "[StrategyValidation] Framework ready.\n" +
                "Define a StrategyValidationScenario and call:\n" +
                "  StrategyValidationRunner.Run(scenario, iterations: 10000);\n" +
                "Optional BaseSeed: per-hand InitState(BaseSeed + handIndex).\n" +
                "Default iterations: " + StrategyValidationRunner.DefaultIterations + "\n" +
                "Menu: Compare 88 UTG (Fold vs Raise-to-5).");
        }

        [MenuItem("Texas Hold'em/Statistics/Strategy Validation/Compare 88 UTG")]
        public static void Compare88Utg() => RunCompare88Utg(writeResultsFile: false, exitEditor: false);

        /// <summary>Batch entry: run comparison, write Temp results, exit Editor.</summary>
        public static void RunCompare88UtgFromBatch() =>
            RunCompare88Utg(writeResultsFile: true, exitEditor: true);

        private static void RunCompare88Utg(bool writeResultsFile, bool exitEditor)
        {
            int exitCode = 1;
            try
            {
                EditorUtility.DisplayProgressBar("Strategy Validation", "Fold scenario...", 0f);

                StrategyValidationScenario foldScenario = Build88UtgScenario(
                    name: "88 UTG Fold",
                    action: BettingAction.Fold,
                    raiseIncrement: 0);

                StrategyValidationResult foldResult = StrategyValidationRunner.Run(
                    foldScenario,
                    CompareIterations,
                    onHandFinished: (hand, total) =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "Strategy Validation — 88 UTG Fold",
                            $"Hand {hand} of {total}",
                            0.5f * hand / total);
                    });

                EditorUtility.DisplayProgressBar("Strategy Validation", "Raise scenario...", 0.5f);

                StrategyValidationScenario raiseScenario = Build88UtgScenario(
                    name: "88 UTG Raise to 5",
                    action: BettingAction.Raise,
                    raiseIncrement: RaiseToFiveIncrement);

                StrategyValidationResult raiseResult = StrategyValidationRunner.Run(
                    raiseScenario,
                    CompareIterations,
                    onHandFinished: (hand, total) =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "Strategy Validation — 88 UTG Raise to 5",
                            $"Hand {hand} of {total}",
                            0.5f + 0.5f * hand / total);
                    });

                string report = BuildComparisonReport(
                    foldResult.Stats, raiseResult.Stats, foldScenario.BigBlind);
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", report);

                if (writeResultsFile)
                {
                    string dir = Path.GetDirectoryName(CompareResultsPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(CompareResultsPath, report);
                    Debug.LogWarning($"[StrategyValidation] Wrote {CompareResultsPath}");
                }

                exitCode = foldResult.Ok && raiseResult.Ok ? 0 : 1;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (exitEditor)
                    EditorApplication.Exit(exitCode);
            }
        }

        private static StrategyValidationScenario Build88UtgScenario(
            string name,
            BettingAction action,
            int raiseIncrement)
        {
            return new StrategyValidationScenario
            {
                Name = name,
                HoleSuit0 = Suit.Spades,
                HoleRank0 = Rank.Eight,
                HoleSuit1 = Suit.Diamonds,
                HoleRank1 = Rank.Eight,
                HeroPosition = PreflopSeatBucket.Early,
                HeroStack = 200,
                OpponentStack = 200,
                SmallBlind = 1,
                BigBlind = 2,
                PlayerCount = 6,
                HeroAction = action,
                RaiseIncrement = raiseIncrement,
                BaseSeed = CompareBaseSeed,
            };
        }

        private static string BuildComparisonReport(
            StrategyValidationStats fold,
            StrategyValidationStats raise,
            int bigBlind)
        {
            if (fold == null || raise == null)
                return "[StrategyValidation] Comparison aborted — missing stats.\n";

            double foldEv = fold.HeroEv;
            double raiseEv = raise.HeroEv;
            double foldBb100 = fold.BbPer100(bigBlind);
            double raiseBb100 = raise.BbPer100(bigBlind);
            double evDelta = raiseEv - foldEv;
            double bbDelta = raiseBb100 - foldBb100;

            string better;
            if (evDelta > 0d)
                better = "Raise";
            else if (evDelta < 0d)
                better = "Fold";
            else
                better = "Tie (equal EV)";

            var sb = new StringBuilder(1024);
            sb.AppendLine("=== Strategy Validation Comparison: 88 UTG ===");
            sb.AppendLine();
            sb.AppendLine("--- Fold summary ---");
            sb.Append(fold.BuildSummaryText(bigBlind));
            sb.AppendLine();
            sb.AppendLine("--- Raise summary ---");
            sb.Append(raise.BuildSummaryText(bigBlind));
            sb.AppendLine();
            sb.AppendLine("--- Comparison ---");
            sb.Append("Fold EV per hand: ").Append(foldEv.ToString("F4")).AppendLine();
            sb.Append("Raise EV per hand: ").Append(raiseEv.ToString("F4")).AppendLine();
            sb.Append("Raise EV − Fold EV: ").Append(evDelta.ToString("F4")).AppendLine();
            sb.Append("Fold BB/100: ").Append(foldBb100.ToString("F2")).AppendLine();
            sb.Append("Raise BB/100: ").Append(raiseBb100.ToString("F2")).AppendLine();
            sb.Append("Raise BB/100 − Fold BB/100: ").Append(bbDelta.ToString("F2")).AppendLine();
            sb.Append("Better-performing action: ").Append(better).AppendLine();
            sb.Append("Completed hands (Fold): ").Append(fold.HandsPlayed).AppendLine();
            sb.Append("Completed hands (Raise): ").Append(raise.HandsPlayed).AppendLine();
            sb.Append("Exceptions (Fold / Raise): ")
                .Append(fold.Exceptions).Append(" / ").Append(raise.Exceptions).AppendLine();
            sb.Append("Illegal actions (Fold / Raise): ")
                .Append(fold.IllegalActions).Append(" / ").Append(raise.IllegalActions).AppendLine();

            if (fold.TotalProfitLoss != 0)
            {
                sb.AppendLine();
                sb.AppendLine(
                    "*** WARNING: UTG Fold produced non-zero TotalProfitLoss = " +
                    fold.TotalProfitLoss +
                    ". Expected 0 (hero has not posted a blind). " +
                    "Possible seat-mapping, settlement, or accounting bug. ***");
            }

            return sb.ToString();
        }
    }
}
