using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Bot decisions use the same preflop chart, position rules, and postflop pot-odds logic as the human HUD.
    /// </summary>
    public class AIController
    {
        /// <summary>Decides the AI's next betting action using <see cref="BettingAdvisor"/>.</summary>
        public (BettingAction action, int raiseAmount) DecideAction(
            PlayerState                 player,
            IReadOnlyList<Card>         communityCards,
            BettingManager              betting,
            IReadOnlyList<PlayerState>  allPlayers,
            GamePhase                   phase,
            int                         potAmount,
            int                         tableCurrentBet,
            int                         bigBlindAmount,
            int                         streetRaiseCount,
            PreflopSeatBucket           preflopSeat,
            bool                        testMode = false)
        {
            int  callAmount = betting.GetCallAmount(player);
            bool canCheck   = callAmount <= 0;
            bool canCall    = callAmount > 0 && player.Chips > 0;
            bool canRaise   = betting.CanRaise(player);
            bool isPreflop  = phase == GamePhase.PreFlop;
            bool facingRaise = tableCurrentBet > bigBlindAmount;

            if (testMode)
                return canCheck ? (BettingAction.Check, 0) : (BettingAction.Call, 0);

            PreflopHandGroup preflopGroup = PreflopHandGroup.Weak;
            float            equityPercent = 0f;

            if (player.HoleCards != null && player.HoleCards.Count >= 2)
            {
                preflopGroup = PreflopStrategy.ClassifyHand(player.HoleCards);

                if (!isPreflop)
                    equityPercent = EstimateEquityPercent(player, communityCards, allPlayers);
            }

            if (isPreflop)
                PreflopStrategy.LogEffectiveStack(player, allPlayers, tableCurrentBet, bigBlindAmount);

            BettingAdvice advice = BettingAdvisor.Recommend(
                equityPercent,
                potAmount,
                callAmount,
                canCheck,
                canRaise,
                canCall,
                isPreflop,
                preflopGroup,
                preflopSeat,
                facingRaise,
                streetRaiseCount,
                player.Chips,
                player.HoleCards,
                phase);

            advice = ApplyFlopOrTurnSemiBluffIfEligible(
                advice, phase, canCheck, canRaise, player.HoleCards, communityCards);

            (BettingAction action, int raiseAmount) resolved = BettingAdvisor.ResolveAction(
                advice, betting, player, isPreflop, streetRaiseCount, potAmount, equityPercent, phase);

            resolved = ApplyFlopOpenBetSizing(
                resolved, phase, callAmount, equityPercent, potAmount, betting, player,
                player.HoleCards, communityCards);

            if (!isPreflop)
                LogPostflopDecision(player, communityCards, equityPercent, resolved.action, resolved.raiseAmount);

            return resolved;
        }

        private const float FlopValueEquityThreshold = 65f;
        private const float FlopSmallBetPotFraction  = 0.33f;
        private const float FlopLargeBetPotFraction  = 0.67f;

        private static readonly BoardTextureFlags FlopWetTextureFlags =
            BoardTextureFlags.ThreeFlush
            | BoardTextureFlags.FourFlush
            | BoardTextureFlags.Connected
            | BoardTextureFlags.FourStraight;

        private static readonly PostflopDrawFlags FlopSemiBluffDraws =
            PostflopDrawFlags.FlushDraw | PostflopDrawFlags.OpenEndedStraightDraw;

        /// <summary>
        /// Flop open bets only: ~1/3 pot for semi-bluffs and dry value; ~2/3 pot for value on wetter flops.
        /// </summary>
        private static (BettingAction action, int raiseAmount) ApplyFlopOpenBetSizing(
            (BettingAction action, int raiseAmount) resolved,
            GamePhase phase,
            int callAmount,
            float equityPercent,
            int potAmount,
            BettingManager betting,
            PlayerState player,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            if (phase != GamePhase.Flop
                || callAmount > 0
                || resolved.action != BettingAction.Raise
                || betting == null
                || player == null)
            {
                return resolved;
            }

            int minIncrement = betting.GetMinRaiseIncrement();
            int maxIncrement = betting.GetMaxRaiseIncrement(player);
            if (maxIncrement < minIncrement || maxIncrement <= 0)
                return resolved;

            float potFraction = ResolveFlopOpenBetPotFraction(equityPercent, holeCards, communityCards);
            int targetTotal = potAmount > 0
                ? Mathf.RoundToInt(potAmount * potFraction)
                : minIncrement;

            int increment = Mathf.Max(targetTotal, minIncrement);
            increment = Mathf.Clamp(increment, minIncrement, maxIncrement);

            if (increment >= player.Chips)
                return (BettingAction.AllIn, 0);

            return (BettingAction.Raise, increment);
        }

        private static float ResolveFlopOpenBetPotFraction(
            float equityPercent,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            bool isValue = equityPercent >= FlopValueEquityThreshold;
            PostflopDrawFlags draws = PostflopDrawDetector.Detect(holeCards, communityCards);
            bool isSemiBluff = !isValue && (draws & FlopSemiBluffDraws) != 0;

            if (isSemiBluff)
                return FlopSmallBetPotFraction;

            BoardTextureFlags texture = BoardTextureAnalyzer.Analyze(communityCards);
            bool isWet = (texture & FlopWetTextureFlags) != 0;

            if (isValue && isWet)
                return FlopLargeBetPotFraction;

            // Dry value (and any other flop open) → small size.
            return FlopSmallBetPotFraction;
        }

        /// <summary>
        /// Flop/turn only, when checking is free: keep ≥65% value bets from the advisor, and also
        /// bet open-ended straight draws / flush draws (not gutshots). Turn uses existing ~2/3 pot sizing.
        /// </summary>
        private static BettingAdvice ApplyFlopOrTurnSemiBluffIfEligible(
            BettingAdvice advice,
            GamePhase phase,
            bool canCheck,
            bool canRaise,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            if ((phase != GamePhase.Flop && phase != GamePhase.Turn) || !canCheck || !canRaise)
                return advice;

            if (advice != BettingAdvice.Check)
                return advice;

            PostflopDrawFlags draws = PostflopDrawDetector.Detect(holeCards, communityCards);
            if ((draws & FlopSemiBluffDraws) != 0)
                return BettingAdvice.Raise;

            return advice;
        }

        private static float EstimateEquityPercent(
            PlayerState player,
            IReadOnlyList<Card> communityCards,
            IReadOnlyList<PlayerState> allPlayers)
        {
            int opponents = CountActiveOpponents(allPlayers, player);
            if (opponents <= 0)
                return 100f;

            var board = communityCards ?? System.Array.Empty<Card>();
            MonteCarloResult result = MonteCarloSimulator.Simulate(
                player.HoleCards,
                board,
                opponents,
                MonteCarloSimulator.DefaultSimulationCount);

            return result.EquityPercent;
        }

        private static int CountActiveOpponents(
            IReadOnlyList<PlayerState> allPlayers,
            PlayerState hero)
        {
            if (allPlayers == null || hero == null)
                return 0;

            int count = 0;
            foreach (PlayerState player in allPlayers)
            {
                if (player == null || player == hero || player.HasFolded)
                    continue;

                count++;
            }

            return count;
        }

        private static void LogPostflopDecision(
            PlayerState player,
            IReadOnlyList<Card> communityCards,
            float equityPercent,
            BettingAction action,
            int raiseAmount)
        {
            BoardTextureFlags texture = BoardTextureAnalyzer.Analyze(communityCards);

            PostflopDrawFlags draws = PostflopDrawFlags.None;
            if (player?.HoleCards != null && player.HoleCards.Count >= 2)
                draws = PostflopDrawDetector.Detect(player.HoleCards, communityCards);

            Debug.Log(
                $"[PostflopAI] player={player.Name} board={FormatBoard(communityCards)} " +
                $"texture={FormatTextureFlags(texture)} draws={FormatDrawFlags(draws)} " +
                $"equity={equityPercent:F1}% decision={FormatDecision(action, raiseAmount)}");
        }

        private static string FormatTextureFlags(BoardTextureFlags flags) =>
            flags == BoardTextureFlags.None ? "None" : flags.ToString();

        private static string FormatDrawFlags(PostflopDrawFlags flags) =>
            flags == PostflopDrawFlags.None ? "None" : flags.ToString();

        private static string FormatDecision(BettingAction action, int raiseAmount)
        {
            if (action == BettingAction.Raise && raiseAmount > 0)
                return $"{action} +${raiseAmount}";

            return action.ToString();
        }

        private static string FormatBoard(IReadOnlyList<Card> communityCards)
        {
            if (communityCards == null || communityCards.Count == 0)
                return "(none)";

            var parts = new List<string>(communityCards.Count);
            foreach (Card card in communityCards)
            {
                if (card != null)
                    parts.Add(card.ToString());
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "(none)";
        }
    }
}
