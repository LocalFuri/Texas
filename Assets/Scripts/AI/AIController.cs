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
                player.HoleCards);

            return BettingAdvisor.ResolveAction(advice, betting, player, isPreflop, streetRaiseCount, potAmount);
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
    }
}
