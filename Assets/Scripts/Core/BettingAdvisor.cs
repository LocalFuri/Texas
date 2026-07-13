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

    /// <summary>Pot-odds and preflop chart hints for the human HUD (human turn only).</summary>
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
            bool canCall,
            bool isPreflop,
            PreflopHandGroup preflopGroup,
            PreflopSeatBucket preflopSeat,
            bool facingRaise,
            int streetRaiseCount,
            int playerChips)
        {
            if (isPreflop)
            {
                return PreflopStrategy.RecommendAdvice(
                    preflopGroup,
                    preflopSeat,
                    facingRaise,
                    callAmount,
                    playerChips,
                    canCheck,
                    canRaise,
                    canCall,
                    streetRaiseCount);
            }

            return RecommendPostflop(equityPercent, potBeforeAction, callAmount, canCheck, canRaise, canCall);
        }

        private static BettingAdvice RecommendPostflop(
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

        /// <summary>Maps HUD advice into a legal table action (same rules the human follows).</summary>
        public static (BettingAction action, int raiseAmount) ResolveAction(
            BettingAdvice advice,
            BettingManager betting,
            PlayerState player,
            bool isPreflop = false,
            int streetRaiseCount = 0)
        {
            if (betting == null || player == null)
                return (BettingAction.Fold, 0);

            int callAmount = betting.GetCallAmount(player);

            switch (advice)
            {
                case BettingAdvice.Fold:
                    return (BettingAction.Fold, 0);

                case BettingAdvice.Check:
                    return callAmount <= 0
                        ? (BettingAction.Check, 0)
                        : ResolveAction(BettingAdvice.Fold, betting, player);

                case BettingAdvice.Call:
                    if (callAmount <= 0)
                        return (BettingAction.Check, 0);

                    if (callAmount >= player.Chips)
                        return (BettingAction.AllIn, 0);

                    if (callAmount > player.Chips)
                        return (BettingAction.Fold, 0);

                    return (BettingAction.Call, 0);

                case BettingAdvice.Raise:
                    if (!betting.CanRaise(player))
                        return ResolveActionWhenCannotRaise(callAmount, betting, player);

                    int minIncrement = betting.GetMinRaiseIncrement();
                    int maxIncrement = betting.GetMaxRaiseIncrement(player);
                    if (maxIncrement < minIncrement)
                        return ResolveActionWhenCannotRaise(callAmount, betting, player);

                    int increment = isPreflop
                        ? ResolvePreflopRaiseIncrement(betting, minIncrement, streetRaiseCount)
                        : minIncrement;
                    if (callAmount + increment >= player.Chips)
                        return (BettingAction.AllIn, 0);

                    increment = Mathf.Clamp(increment, minIncrement, maxIncrement);
                    return (BettingAction.Raise, increment);

                default:
                    return callAmount <= 0
                        ? (BettingAction.Check, 0)
                        : (BettingAction.Fold, 0);
            }
        }

        private static int ResolvePreflopRaiseIncrement(
            BettingManager betting,
            int minIncrement,
            int streetRaiseCount)
        {
            int currentBet = betting.CurrentBet;
            int bigBlind   = betting.BigBlind;

            // Targets are TOTAL bet sizes. Convert to increment above current bet.
            int targetTotal;
            if (streetRaiseCount <= 0)
            {
                // Open raise: target 2.5× BB total.
                targetTotal = Mathf.RoundToInt(bigBlind * 2.5f);
            }
            else if (streetRaiseCount == 1)
            {
                // Facing one raise: target 3× current table bet total.
                targetTotal = currentBet * 3;
            }
            else
            {
                // Facing 2+ raises: target 2.5× current table bet total.
                targetTotal = Mathf.RoundToInt(currentBet * 2.5f);
            }

            int increment = targetTotal - currentBet;
            return Mathf.Max(minIncrement, increment);
        }

        private static (BettingAction action, int raiseAmount) ResolveActionWhenCannotRaise(
            int callAmount,
            BettingManager betting,
            PlayerState player)
        {
            if (callAmount <= 0)
                return (BettingAction.Check, 0);

            if (callAmount >= player.Chips)
                return (BettingAction.AllIn, 0);

            if (callAmount < player.Chips)
                return (BettingAction.Call, 0);

            return (BettingAction.Fold, 0);
        }
    }
}
