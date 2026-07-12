using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public class AIController
    {
        private const int MaxStreetRaises = 4;

        // Open-raise frequencies (unopened pot / facing blinds only).
        private const float PremiumOpenRaiseChance = 0.82f;
        private const float StrongOpenRaiseChance  = 0.58f;
        private const float PlayableOpenRaiseChance = 0.18f;

        // Re-raise frequencies when already facing a raise above the big blind.
        private const float PremiumThreeBetChance = 0.28f;
        private const float StrongThreeBetChance  = 0.12f;

        private const float PlayableCallChipFraction = 0.20f;
        private const float StrongFoldChipFraction   = 0.35f;

        private const float PostflopValueBetChance   = 0.55f;
        private const float PostflopRaiseFacingBetChance = 0.22f;

        private static readonly PreflopHandGroup[,] PreflopChart = BuildPreflopChart();

        private enum PreflopHandGroup
        {
            Premium,
            Strong,
            Playable,
            Weak
        }

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
                PreflopHandGroup group = ClassifyPreflopHand(player.HoleCards);
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

        /// <summary>First-in or facing blinds/limpers only — open-raise or complete/check.</summary>
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

        /// <summary>Facing a raise — mostly call; selective 3-bets; fold weak holdings.</summary>
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

        /// <summary>True when the bot holds top pair, an overpair, or any made hand stronger than one pair.</summary>
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

        private static PreflopHandGroup ClassifyPreflopHand(IReadOnlyList<Card> holeCards)
        {
            if (holeCards.Count < 2)
                return PreflopHandGroup.Weak;

            int hi = RankIndex(holeCards[0].Rank);
            int lo = RankIndex(holeCards[1].Rank);
            if (lo > hi)
                (hi, lo) = (lo, hi);

            bool suited = holeCards[0].Suit == holeCards[1].Suit;
            return suited || hi == lo
                ? PreflopChart[hi, lo]
                : PreflopChart[lo, hi];
        }

        private static int RankIndex(Rank rank) => (int)rank - 2;

        /// <summary>
        /// 13×13 starting-hand chart (index 0 = Two, 12 = Ace).
        /// Pairs on diagonal; suited above diagonal [hi, lo]; offsuit below [lo, hi].
        /// </summary>
        private static PreflopHandGroup[,] BuildPreflopChart()
        {
            var chart = new PreflopHandGroup[13, 13];

            for (int i = 0; i < 13; i++)
            {
                for (int j = 0; j < 13; j++)
                    chart[i, j] = PreflopHandGroup.Weak;
            }

            void Pair(int idx, PreflopHandGroup group) => chart[idx, idx] = group;
            void Suited(int hi, int lo, PreflopHandGroup group) => chart[hi, lo] = group;
            void Offsuit(int hi, int lo, PreflopHandGroup group) => chart[lo, hi] = group;

            // Pairs
            Pair(12, PreflopHandGroup.Premium);
            Pair(11, PreflopHandGroup.Premium);
            Pair(10, PreflopHandGroup.Premium);
            Pair(9, PreflopHandGroup.Premium);
            Pair(8, PreflopHandGroup.Strong);
            Pair(7, PreflopHandGroup.Strong);
            Pair(6, PreflopHandGroup.Strong);
            Pair(5, PreflopHandGroup.Playable);
            Pair(4, PreflopHandGroup.Playable);
            Pair(3, PreflopHandGroup.Playable);
            Pair(2, PreflopHandGroup.Playable);
            Pair(1, PreflopHandGroup.Playable);
            Pair(0, PreflopHandGroup.Playable);

            // Suited
            Suited(12, 11, PreflopHandGroup.Premium);
            Suited(12, 10, PreflopHandGroup.Premium);
            Suited(12, 9, PreflopHandGroup.Strong);
            Suited(12, 8, PreflopHandGroup.Strong);
            Suited(11, 10, PreflopHandGroup.Strong);
            Suited(11, 9, PreflopHandGroup.Strong);
            Suited(10, 9, PreflopHandGroup.Strong);
            Suited(9, 8, PreflopHandGroup.Strong);

            for (int lo = 0; lo <= 7; lo++)
                Suited(12, lo, PreflopHandGroup.Playable);

            Suited(11, 8, PreflopHandGroup.Playable);
            Suited(11, 7, PreflopHandGroup.Playable);
            Suited(11, 6, PreflopHandGroup.Playable);
            Suited(10, 8, PreflopHandGroup.Playable);
            Suited(10, 7, PreflopHandGroup.Playable);
            Suited(9, 7, PreflopHandGroup.Playable);
            Suited(8, 7, PreflopHandGroup.Playable);
            Suited(7, 6, PreflopHandGroup.Playable);
            Suited(6, 5, PreflopHandGroup.Playable);
            Suited(5, 4, PreflopHandGroup.Playable);
            Suited(4, 3, PreflopHandGroup.Playable);
            Suited(3, 2, PreflopHandGroup.Playable);
            Suited(2, 1, PreflopHandGroup.Playable);

            // Offsuit
            Offsuit(12, 11, PreflopHandGroup.Premium);
            Offsuit(12, 10, PreflopHandGroup.Strong);
            Offsuit(11, 10, PreflopHandGroup.Strong);
            Offsuit(12, 9, PreflopHandGroup.Playable);
            Offsuit(12, 8, PreflopHandGroup.Playable);
            Offsuit(12, 7, PreflopHandGroup.Playable);
            Offsuit(11, 9, PreflopHandGroup.Playable);
            Offsuit(11, 8, PreflopHandGroup.Playable);
            Offsuit(10, 9, PreflopHandGroup.Playable);
            Offsuit(10, 8, PreflopHandGroup.Playable);
            Offsuit(9, 8, PreflopHandGroup.Playable);

            // Restore premium/strong suited aces overwritten by playable loop
            Suited(12, 11, PreflopHandGroup.Premium);
            Suited(12, 10, PreflopHandGroup.Premium);
            Suited(12, 9, PreflopHandGroup.Strong);
            Suited(12, 8, PreflopHandGroup.Strong);

            return chart;
        }
    }
}
