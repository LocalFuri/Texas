using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public class AIController
    {
        private const float AggressionThreshold   = 0.35f;
        private const float PremiumRaiseChance    = 0.75f;
        private const float StrongRaiseChance       = 0.50f;
        private const float PlayableCallChipFraction = 0.25f;

        private static readonly PreflopHandGroup[,] PreflopChart = BuildPreflopChart();

        private enum PreflopHandGroup
        {
            Premium,
            Strong,
            Playable,
            Weak
        }

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

            if (communityCards.Count == 0 && player.HoleCards.Count >= 2)
            {
                PreflopHandGroup group = ClassifyPreflopHand(player.HoleCards);
                return DecidePreflopAction(group, canCheck, callAmount, player.Chips, betting, player);
            }

            var allCards = new List<Card>(player.HoleCards);
            allCards.AddRange(communityCards);

            float strength = EvaluateHandStrength(allCards);

            if (strength > 0.75f)
            {
                if (betting.CanRaise(player) && Random.value < AggressionThreshold)
                {
                    int minRaise = betting.GetMinRaiseIncrement();
                    int maxRaise = betting.GetMaxRaiseIncrement(player);
                    int raise    = Mathf.Clamp(minRaise, minRaise, maxRaise);
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

        private static (BettingAction action, int raiseAmount) DecidePreflopAction(
            PreflopHandGroup group,
            bool canCheck,
            int callAmount,
            int playerChips,
            BettingManager betting,
            PlayerState player)
        {
            BettingAction passive = canCheck ? BettingAction.Check : BettingAction.Call;

            switch (group)
            {
                case PreflopHandGroup.Premium:
                    if (betting.CanRaise(player) && Random.value < PremiumRaiseChance)
                        return MinRaise(betting, player);
                    return (passive, 0);

                case PreflopHandGroup.Strong:
                    if (betting.CanRaise(player) && Random.value < StrongRaiseChance)
                        return MinRaise(betting, player);
                    return (passive, 0);

                case PreflopHandGroup.Playable:
                    if (canCheck)
                        return (BettingAction.Check, 0);
                    if (callAmount <= playerChips * PlayableCallChipFraction)
                        return (BettingAction.Call, 0);
                    return (BettingAction.Fold, 0);

                default:
                    return canCheck ? (BettingAction.Check, 0) : (BettingAction.Fold, 0);
            }
        }

        private static (BettingAction action, int raiseAmount) MinRaise(
            BettingManager betting, PlayerState player)
        {
            int minRaise = betting.GetMinRaiseIncrement();
            int maxRaise = betting.GetMaxRaiseIncrement(player);
            int raise    = Mathf.Clamp(minRaise, minRaise, maxRaise);
            return (BettingAction.Raise, raise);
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

        private float EvaluateHandStrength(List<Card> cards)
        {
            var result = HandEvaluator.Evaluate(cards);
            return (float)result.Rank / (float)HandRank.RoyalFlush;
        }
    }
}
