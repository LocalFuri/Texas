using UnityEngine;

namespace TexasHoldem
{
    public enum BettingAdvice
    {
        None,
        Fold,
        Call,
        Check,
        Raise,
    }

    /// <summary>Pot-odds hint from Monte Carlo equity vs current action (human turn only).</summary>
    public static class BettingAdvisor
    {
        public const string LabelFold  = "FOLD";
        public const string LabelCall  = "CALL";
        public const string LabelCheck = "CHECK";
        public const string LabelRaise = "RAISE";

        private const float EdgeMargin  = 3f;
        private const float RaiseEdge   = 15f;
        private const float StrongRaise = 65f;

        public static string LabelFor(BettingAdvice advice) =>
            advice switch
            {
                BettingAdvice.Fold  => LabelFold,
                BettingAdvice.Call  => LabelCall,
                BettingAdvice.Check => LabelCheck,
                BettingAdvice.Raise => LabelRaise,
                _                   => string.Empty,
            };

        public static Color ColorFor(BettingAdvice advice) =>
            advice switch
            {
                BettingAdvice.Fold  => new Color(1f, 0.38f, 0.38f, 1f),
                BettingAdvice.Call  => new Color(0.45f, 0.95f, 0.55f, 1f),
                BettingAdvice.Check => new Color(0.78f, 0.86f, 0.96f, 1f),
                BettingAdvice.Raise => new Color(0.45f, 0.78f, 1f, 1f),
                _                   => UiColors.PotGold,
            };

        public static Color ColorForLabel(string label)
        {
            if (label == LabelFold)  return ColorFor(BettingAdvice.Fold);
            if (label == LabelCall)  return ColorFor(BettingAdvice.Call);
            if (label == LabelCheck) return ColorFor(BettingAdvice.Check);
            if (label == LabelRaise) return ColorFor(BettingAdvice.Raise);
            return UiColors.PotGold;
        }

        public static BettingAdvice Recommend(
            float equityPercent,
            int potBeforeAction,
            int callAmount,
            bool canCheck,
            bool canRaise,
            bool canCall)
        {
            equityPercent = Mathf.Clamp(equityPercent, 0f, 100f);

            if (canCheck)
            {
                if (equityPercent >= StrongRaise && canRaise)
                    return BettingAdvice.Raise;

                return BettingAdvice.Check;
            }

            if (!canCall || callAmount <= 0)
                return BettingAdvice.Check;

            int denominator = potBeforeAction + callAmount;
            if (denominator <= 0)
                return equityPercent >= 50f ? BettingAdvice.Call : BettingAdvice.Fold;

            float needed = 100f * callAmount / denominator;

            if (equityPercent >= needed + RaiseEdge && canRaise)
                return BettingAdvice.Raise;

            if (equityPercent >= needed + EdgeMargin)
                return BettingAdvice.Call;

            return BettingAdvice.Fold;
        }
    }
}
