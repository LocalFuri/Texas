using System.Text;

namespace TexasHoldem.Dev
{
    /// <summary>Aggregated results for a <see cref="StrategyValidationRunner"/> batch.</summary>
    public sealed class StrategyValidationStats
    {
        public string ScenarioName;

        public int HandsAttempted;
        public int HandsPlayed;

        public long TotalProfitLoss;
        public int Wins;
        public int Showdowns;

        public int ForcedFolds;
        public int ForcedCalls;
        public int ForcedThreeBets;
        public int ForcedActions;

        public int Exceptions;
        public int IllegalActions;
        public string LastError;

        public double ElapsedSeconds;

        public double HeroEv => HandsPlayed > 0 ? (double)TotalProfitLoss / HandsPlayed : 0d;

        public double BbPer100(int bigBlind) =>
            HandsPlayed > 0 && bigBlind > 0
                ? ((double)TotalProfitLoss / bigBlind) / HandsPlayed * 100d
                : 0d;

        public double WinPercent => HandsPlayed > 0 ? 100d * Wins / HandsPlayed : 0d;

        public double FoldedPreflopPercent =>
            ForcedActions > 0 ? 100d * ForcedFolds / ForcedActions : 0d;

        public double CalledPreflopPercent =>
            ForcedActions > 0 ? 100d * ForcedCalls / ForcedActions : 0d;

        public double ThreeBetPercent =>
            ForcedActions > 0 ? 100d * ForcedThreeBets / ForcedActions : 0d;

        public string BuildSummaryText(int bigBlind)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("=== Strategy Validation ===");
            if (!string.IsNullOrEmpty(ScenarioName))
                sb.Append("Scenario: ").Append(ScenarioName).AppendLine();
            sb.AppendLine();
            sb.Append("Hands played: ").Append(HandsPlayed).AppendLine();
            sb.Append("Hands attempted: ").Append(HandsAttempted).AppendLine();
            sb.Append("Hero EV: ").Append(HeroEv.ToString("F2")).AppendLine();
            sb.Append("Total profit/loss: ").Append(TotalProfitLoss).AppendLine();
            sb.Append("BB/100: ").Append(BbPer100(bigBlind).ToString("F2")).AppendLine();
            sb.Append("Win %: ").Append(WinPercent.ToString("F2")).AppendLine();
            sb.Append("Folded preflop %: ").Append(FoldedPreflopPercent.ToString("F2")).AppendLine();
            sb.Append("Called preflop %: ").Append(CalledPreflopPercent.ToString("F2")).AppendLine();
            sb.Append("3-bet %: ").Append(ThreeBetPercent.ToString("F2")).AppendLine();
            sb.Append("Showdowns: ").Append(Showdowns).AppendLine();
            sb.Append("Exceptions: ").Append(Exceptions).AppendLine();
            sb.Append("Illegal actions: ").Append(IllegalActions).AppendLine();
            if (!string.IsNullOrEmpty(LastError))
                sb.Append("Last error: ").Append(LastError).AppendLine();
            sb.Append("Runtime: ").Append(ElapsedSeconds.ToString("F2")).Append('s').AppendLine();
            return sb.ToString();
        }
    }

    public readonly struct StrategyValidationResult
    {
        public StrategyValidationStats Stats { get; }
        public bool Ok => Stats != null && Stats.Exceptions == 0 && Stats.IllegalActions == 0;

        public StrategyValidationResult(StrategyValidationStats stats) => Stats = stats;
    }
}
