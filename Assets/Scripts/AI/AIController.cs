using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public class AIController
    {
        private const int MaxStreetRaises = 4;

        private const float PremiumOpenRaiseChance = 0.82f;
        private const float StrongOpenRaiseChance  = 0.58f;
        private const float PlayableOpenRaiseChance = 0.18f;

        private const float PremiumThreeBetChance = 0.28f;
        private const float StrongThreeBetChance  = 0.12f;

        private const float PlayableCallChipFraction = 0.20f;
        private const float StrongFoldChipFraction   = 0.35f;

        private const float PostflopValueBetChance   = 0.55f;
        private const float PostflopRaiseFacingBetChance = 0.22f;

        private enum RaiseSizeIntent
        {
            Open,
            ThreeBet,
            Continuation
        }

        /// <summary>Decides the AI's next betting action based on estimated hand strength.</summary>
        public (BettingAction action, int raiseAmount) DecideAction(
            PlayerState    player,
            List<Card>     communityCards,
            BettingManager betting,
            bool           testMode = false)
        {
            int  callAmount = betting.GetCallAmount(player);
            bool canCheck   = callAmount <= 0;

            if (testMode)
                return canCheck ? (BettingAction.Check, 0) : (BettingAction.Call, 0);

            if (communityCards.Count == 0 && player.HoleCards.Count >= 2)
            {
                PreflopHandGroup group = PreflopStrategy.ClassifyHand(player.HoleCards);
                return DecidePreflopAction(group, canCheck, callAmount, player.Chips, betting, player);
            }

            return DecidePostflopAction(player, communityCards, betting, canCheck, callAmount);
        }

        private static (BettingAction action, int raiseAmount) DecidePreflopAction(
            PreflopHandGroup group,
            bool canCheck,
            int callAmount,
            int playerChips,
            BettingManager betting,
            PlayerState player)
        {
            bool facingRaise = betting.CurrentBet > betting.BigBlind;

            if (facingRaise)
                return DecidePreflopFacingRaise(group, callAmount, playerChips, betting, player);

            return DecidePreflopUnopened(group, canCheck, callAmount, playerChips, betting, player);
        }

        private static (BettingAction action, int raiseAmount) DecidePreflopUnopened(
            PreflopHandGroup group,
            bool canCheck,
            int callAmount,
            int playerChips,
            BettingManager betting,
            PlayerState player)
        {
            float openChance = group switch
            {
                PreflopHandGroup.Premium  => PremiumOpenRaiseChance,
                PreflopHandGroup.Strong   => StrongOpenRaiseChance,
                PreflopHandGroup.Playable => PlayableOpenRaiseChance,
                _                         => 0f
            };

            if (openChance > 0f
                && betting.CanRaise(player)
                && betting.StreetRaiseCount < MaxStreetRaises
                && Random.value < openChance)
            {
                return SizedRaise(betting, player, RaiseSizeIntent.Open);
            }

            if (canCheck)
                return (BettingAction.Check, 0);

            if (group == PreflopHandGroup.Weak)
                return (BettingAction.Fold, 0);

            if (callAmount <= playerChips)
                return (BettingAction.Call, 0);

            return (BettingAction.Fold, 0);
        }

        private static (BettingAction action, int raiseAmount) DecidePreflopFacingRaise(
            PreflopHandGroup group,
            int callAmount,
            int playerChips,
            BettingManager betting,
            PlayerState player)
        {
            if (group == PreflopHandGroup.Weak)
                return (BettingAction.Fold, 0);

            if (betting.StreetRaiseCount >= MaxStreetRaises)
                return ResolveCallOrFold(group, callAmount, playerChips);

            float threeBetChance = group switch
            {
                PreflopHandGroup.Premium  => PremiumThreeBetChance,
                PreflopHandGroup.Strong   => StrongThreeBetChance,
                _                         => 0f
            };

            if (threeBetChance > 0f
                && betting.CanRaise(player)
                && Random.value < threeBetChance)
            {
                return SizedRaise(betting, player, RaiseSizeIntent.ThreeBet);
            }

            return ResolveCallOrFold(group, callAmount, playerChips);
        }

        private static (BettingAction action, int raiseAmount) ResolveCallOrFold(
            PreflopHandGroup group,
            int callAmount,
            int playerChips)
        {
            if (callAmount > playerChips)
                return (BettingAction.Fold, 0);

            switch (group)
            {
                case PreflopHandGroup.Premium:
                    return (BettingAction.Call, 0);

                case PreflopHandGroup.Strong:
                    if (callAmount > playerChips * StrongFoldChipFraction)
                        return (BettingAction.Fold, 0);
                    return (BettingAction.Call, 0);

                case PreflopHandGroup.Playable:
                    if (callAmount <= playerChips * PlayableCallChipFraction)
                        return (BettingAction.Call, 0);
                    return (BettingAction.Fold, 0);

                default:
                    return (BettingAction.Fold, 0);
            }
        }

        private static (BettingAction action, int raiseAmount) DecidePostflopAction(
            PlayerState player,
            List<Card> communityCards,
            BettingManager betting,
            bool canCheck,
            int callAmount)
        {
            var allCards = new List<Card>(player.HoleCards);
            allCards.AddRange(communityCards);

            HandResult hand = HandEvaluator.Evaluate(allCards);
            bool valueHand  = IsTopPairOrBetter(player.HoleCards, communityCards, hand);

            if (valueHand)
            {
                if (canCheck)
                {
                    if (betting.CanRaise(player)
                        && betting.StreetRaiseCount < MaxStreetRaises
                        && Random.value < PostflopValueBetChance)
                    {
                        return SizedRaise(betting, player, RaiseSizeIntent.Continuation);
                    }

                    return (BettingAction.Check, 0);
                }

                if (betting.CanRaise(player)
                    && betting.StreetRaiseCount < MaxStreetRaises
                    && Random.value < PostflopRaiseFacingBetChance)
                {
                    return SizedRaise(betting, player, RaiseSizeIntent.ThreeBet);
                }

                if (callAmount <= player.Chips)
                    return (BettingAction.Call, 0);

                return (BettingAction.Fold, 0);
            }

            float strength = (float)hand.Rank / (float)HandRank.RoyalFlush;

            if (strength > 0.4f)
            {
                if (canCheck)
                    return (BettingAction.Check, 0);

                if (callAmount <= player.Chips / 5)
                    return (BettingAction.Call, 0);

                return (BettingAction.Fold, 0);
            }

            return canCheck ? (BettingAction.Check, 0) : (BettingAction.Fold, 0);
        }

        private static (BettingAction action, int raiseAmount) SizedRaise(
            BettingManager betting,
            PlayerState player,
            RaiseSizeIntent intent)
        {
            int minRaise = betting.GetMinRaiseIncrement();
            int maxRaise = betting.GetMaxRaiseIncrement(player);
            if (maxRaise < minRaise)
                return (BettingAction.Call, 0);

            int increment = intent switch
            {
                RaiseSizeIntent.Open =>
                    Mathf.Max(minRaise, Mathf.RoundToInt(betting.BigBlind * 2.5f)),

                RaiseSizeIntent.ThreeBet =>
                    Mathf.Max(minRaise, betting.CurrentBet * 2),

                _ =>
                    Mathf.Max(minRaise, Mathf.RoundToInt(betting.BigBlind * 2.5f))
            };

            increment = Mathf.Clamp(increment, minRaise, maxRaise);
            return (BettingAction.Raise, increment);
        }

        private static bool IsTopPairOrBetter(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards,
            HandResult hand)
        {
            if (communityCards == null || communityCards.Count == 0)
                return false;

            if (hand.Rank >= HandRank.TwoPair)
                return true;

            if (hand.Rank != HandRank.OnePair || holeCards == null || holeCards.Count < 2)
                return false;

            int pairRank = hand.Tiebreakers[0];
            int maxBoardRank = (int)communityCards[0].Rank;
            foreach (Card boardCard in communityCards)
                maxBoardRank = Mathf.Max(maxBoardRank, (int)boardCard.Rank);

            bool holePairsRank = false;
            foreach (Card holeCard in holeCards)
            {
                if ((int)holeCard.Rank == pairRank)
                {
                    holePairsRank = true;
                    break;
                }
            }

            if (!holePairsRank)
                return false;

            if (holeCards[0].Rank == holeCards[1].Rank && pairRank > maxBoardRank)
                return true;

            return pairRank >= maxBoardRank;
        }
    }
}
