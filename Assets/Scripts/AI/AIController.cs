using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public class AIController
    {
        private const float AggressionThreshold = 0.35f;

        /// <summary>Decides the AI's next betting action based on estimated hand strength.</summary>
        public (BettingAction action, int raiseAmount) DecideAction(
            PlayerState    player,
            List<Card>     communityCards,
            BettingManager betting,
            bool           testMode = false)
        {
            int  callAmount = betting.CurrentBet - player.CurrentBet;
            bool canCheck   = callAmount <= 0;

            if (testMode)
                return canCheck ? (BettingAction.Check, 0) : (BettingAction.Call, 0);

            var allCards = new List<Card>(player.HoleCards);
            allCards.AddRange(communityCards);

            float strength = allCards.Count >= 5
                ? EvaluateHandStrength(allCards)
                : EstimateHoleCardStrength(player.HoleCards);

            if (strength > 0.75f)
            {
                if (Random.value < AggressionThreshold)
                {
                    int raise = Mathf.Clamp(betting.BigBlind * 2, betting.BigBlind, player.Chips);
                    return (BettingAction.Raise, raise);
                }
                return canCheck ? (BettingAction.Check, 0) : (BettingAction.Call, 0);
            }

            if (strength > 0.4f)
            {
                if (canCheck) return (BettingAction.Check, 0);
                if (callAmount <= player.Chips / 4) return (BettingAction.Call, 0);
                return (BettingAction.Fold, 0);
            }

            return canCheck ? (BettingAction.Check, 0) : (BettingAction.Fold, 0);
        }

        private float EvaluateHandStrength(List<Card> cards)
        {
            var result = HandEvaluator.Evaluate(cards);
            return (float)result.Rank / (float)HandRank.RoyalFlush;
        }

        private float EstimateHoleCardStrength(List<Card> holeCards)
        {
            if (holeCards.Count < 2) return 0f;

            Card a    = holeCards[0];
            Card b    = holeCards[1];
            int  high = Mathf.Max((int)a.Rank, (int)b.Rank);

            float score = (high - 2) / 12f;
            if (a.Rank == b.Rank)                          score = Mathf.Clamp01(score + 0.30f);
            if (a.Suit == b.Suit)                          score = Mathf.Clamp01(score + 0.10f);
            if (Mathf.Abs((int)a.Rank - (int)b.Rank) <= 2) score = Mathf.Clamp01(score + 0.05f);
            return score;
        }
    }
}
