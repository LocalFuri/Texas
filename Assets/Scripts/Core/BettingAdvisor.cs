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

        private const float EdgeMargin            = 3f;
        private const float RaiseEdge             = 15f;
        private const float RiverRaiseEdge        = 25f;
        private const float RiverRaiseEquityFloor = 80f;
        private const float StrongRaise           = 65f;
        private const float RiverThinValueBet     = 55f;

        private const float PostflopBetPotFraction      = 0.67f;
        private const float RiverThinBetPotFraction     = 0.33f;
        private const float PostflopRaisePotFraction    = 0.75f;

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
            int playerChips,
            System.Collections.Generic.IReadOnlyList<Card> holeCards = null,
            GamePhase postflopPhase = GamePhase.Flop)
        {
            if (isPreflop)
            {
                return PreflopStrategy.RecommendAdvice(
                    preflopGroup,
                    preflopSeat,
                    facingRaise,
                    potBeforeAction,
                    callAmount,
                    playerChips,
                    canCheck,
                    canRaise,
                    canCall,
                    streetRaiseCount,
                    holeCards);
            }

            return RecommendPostflop(
                equityPercent, potBeforeAction, callAmount, canCheck, canRaise, canCall, postflopPhase);
        }

        private static BettingAdvice RecommendPostflop(
            float equityPercent,
            int potBeforeAction,
            int callAmount,
            bool canCheck,
            bool canRaise,
            bool canCall,
            GamePhase postflopPhase)
        {
            equityPercent = Mathf.Clamp(equityPercent, 0f, 100f);

            if (canCheck)
            {
                if (!canRaise)
                    return BettingAdvice.Check;

                if (postflopPhase == GamePhase.River)
                {
                    if (equityPercent >= StrongRaise)
                        return BettingAdvice.Raise;

                    // Former thin-value band (55–65%): check instead of betting.
                    if (equityPercent >= RiverThinValueBet)
                    {
                        Debug.Log(
                            $"[PostflopAI] RiverThinCheck equity={equityPercent:F1}% " +
                            $"(former thin-value band {RiverThinValueBet:F0}–{StrongRaise:F0}%)");
                    }

                    return BettingAdvice.Check;
                }

                if (equityPercent >= StrongRaise)
                    return BettingAdvice.Raise;

                return BettingAdvice.Check;
            }

            if (!canCall || callAmount <= 0)
                return BettingAdvice.Check;

            int denominator = potBeforeAction + callAmount;
            if (denominator <= 0)
                return equityPercent >= 50f ? BettingAdvice.Call : BettingAdvice.Fold;

            float needed = 100f * callAmount / denominator;
            float raiseEdge = postflopPhase == GamePhase.River ? RiverRaiseEdge : RaiseEdge;

            if (equityPercent >= needed + raiseEdge && canRaise)
            {
                if (postflopPhase == GamePhase.River && equityPercent < RiverRaiseEquityFloor)
                {
                    Debug.Log(
                        $"[PostflopAI] RiverRaiseFloorBlock equity={equityPercent:F1}% " +
                        $"(needed+{RiverRaiseEdge:F0}%={needed + RiverRaiseEdge:F1}%, " +
                        $"floor={RiverRaiseEquityFloor:F0}%) → Call");
                    return BettingAdvice.Call;
                }

                return BettingAdvice.Raise;
            }

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
            int streetRaiseCount = 0,
            int potBeforeAction = 0,
            float equityPercent = 0f,
            GamePhase postflopPhase = GamePhase.Flop)
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

                    // Preflop: don't leave a crumb when already committing ~85%+ of stack.
                    if (callAmount >= player.Chips
                        || (isPreflop && callAmount >= Mathf.CeilToInt(player.Chips * 0.85f)))
                        return (BettingAction.AllIn, 0);

                    return (BettingAction.Call, 0);

                case BettingAdvice.Raise:
                    if (!betting.CanRaise(player))
                        return ResolveActionWhenCannotRaise(callAmount, player, isPreflop);

                    int minIncrement = betting.GetMinRaiseIncrement();
                    int maxIncrement = betting.GetMaxRaiseIncrement(player);
                    if (maxIncrement < minIncrement)
                        return ResolveActionWhenCannotRaise(callAmount, player, isPreflop);

                    int increment = isPreflop
                        ? ResolvePreflopRaiseIncrement(betting, minIncrement, streetRaiseCount)
                        : ResolvePostflopRaiseIncrement(
                            betting, potBeforeAction, callAmount, minIncrement, maxIncrement,
                            equityPercent, postflopPhase);
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

        private static int ResolvePostflopRaiseIncrement(
            BettingManager betting,
            int potBeforeAction,
            int callAmount,
            int minIncrement,
            int maxIncrement,
            float equityPercent,
            GamePhase postflopPhase)
        {
            int currentBet = betting.CurrentBet;
            int targetTotal;

            if (callAmount <= 0)
            {
                float betPotFraction = PostflopBetPotFraction;
                if (postflopPhase == GamePhase.River
                    && equityPercent >= RiverThinValueBet
                    && equityPercent < StrongRaise)
                {
                    betPotFraction = RiverThinBetPotFraction;
                }

                targetTotal = potBeforeAction > 0
                    ? Mathf.RoundToInt(potBeforeAction * betPotFraction)
                    : currentBet + minIncrement;
            }
            else
            {
                int potAfterCall = potBeforeAction + callAmount;
                targetTotal = callAmount + Mathf.RoundToInt(potAfterCall * PostflopRaisePotFraction);
            }

            if (targetTotal <= currentBet)
                targetTotal = currentBet + minIncrement;

            int increment = targetTotal - currentBet;
            return Mathf.Clamp(Mathf.Max(increment, minIncrement), minIncrement, maxIncrement);
        }

        private static (BettingAction action, int raiseAmount) ResolveActionWhenCannotRaise(
            int callAmount,
            PlayerState player,
            bool isPreflop)
        {
            if (player.Chips <= 0)
                return callAmount <= 0 ? (BettingAction.Check, 0) : (BettingAction.Fold, 0);

            // Preflop: intended raise but cannot meet min → jam (never flat leftover chips).
            if (isPreflop)
                return (BettingAction.AllIn, 0);

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
