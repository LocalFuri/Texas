using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public enum PreflopHandGroup
    {
        Premium,
        Strong,
        Playable,
        Weak,
    }

    public enum PreflopSeatBucket
    {
        Button,
        SmallBlind,
        BigBlind,
        Early,
        Middle,
        Cutoff,
    }

    /// <summary>Shared preflop hand chart and deterministic advice (mirrors bot tiers + position).</summary>
    public static class PreflopStrategy
    {
        private const int MaxStreetRaises = 4;

        private const float PlayableCallChipFraction = 0.20f;
        private const float StrongFoldChipFraction   = 0.35f;

        private static readonly PreflopHandGroup[,] PreflopChart = BuildPreflopChart();

        public static PreflopHandGroup ClassifyHand(IReadOnlyList<Card> holeCards)
        {
            if (holeCards == null || holeCards.Count < 2)
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

        public static PreflopSeatBucket ResolveSeatBucket(
            IReadOnlyList<PlayerState> activePlayers,
            int dealerIndexInActive,
            PlayerState hero)
        {
            if (activePlayers == null || hero == null || activePlayers.Count == 0)
                return PreflopSeatBucket.Early;

            int heroIndex = -1;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                if (activePlayers[i] == hero)
                {
                    heroIndex = i;
                    break;
                }
            }

            if (heroIndex < 0)
                return PreflopSeatBucket.Early;

            int n       = activePlayers.Count;
            int fromBtn = ((heroIndex - dealerIndexInActive) % n + n) % n;

            if (fromBtn == 0) return PreflopSeatBucket.Button;
            if (fromBtn == 1) return PreflopSeatBucket.SmallBlind;
            if (fromBtn == 2) return PreflopSeatBucket.BigBlind;
            if (n <= 4)       return PreflopSeatBucket.Early;
            if (fromBtn == 3) return PreflopSeatBucket.Early;
            if (fromBtn == n - 1) return PreflopSeatBucket.Cutoff;
            if (n >= 6 && fromBtn == n - 2) return PreflopSeatBucket.Middle;

            return PreflopSeatBucket.Middle;
        }

        public static BettingAdvice RecommendAdvice(
            PreflopHandGroup group,
            PreflopSeatBucket seat,
            bool facingRaise,
            int callAmount,
            int playerChips,
            bool canCheck,
            bool canRaise,
            bool canCall,
            int streetRaiseCount)
        {
            if (facingRaise)
                return RecommendFacingRaise(group, callAmount, playerChips, canRaise, canCall, streetRaiseCount);

            return RecommendUnopened(group, seat, callAmount, playerChips, canCheck, canRaise, canCall, streetRaiseCount);
        }

        private static BettingAdvice RecommendUnopened(
            PreflopHandGroup group,
            PreflopSeatBucket seat,
            int callAmount,
            int playerChips,
            bool canCheck,
            bool canRaise,
            bool canCall,
            int streetRaiseCount)
        {
            if (seat == PreflopSeatBucket.BigBlind && canCheck)
            {
                if (group >= PreflopHandGroup.Strong
                    && canRaise
                    && streetRaiseCount < MaxStreetRaises)
                {
                    return BettingAdvice.Raise;
                }

                return BettingAdvice.Check;
            }

            if (canRaise
                && streetRaiseCount < MaxStreetRaises
                && OpenTier(group) >= MinOpenTier(seat))
            {
                return BettingAdvice.Raise;
            }

            if (canCheck)
                return BettingAdvice.Check;

            if (group == PreflopHandGroup.Weak)
                return BettingAdvice.Fold;

            if (canCall && callAmount <= playerChips)
                return BettingAdvice.Call;

            return BettingAdvice.Fold;
        }

        private static BettingAdvice RecommendFacingRaise(
            PreflopHandGroup group,
            int callAmount,
            int playerChips,
            bool canRaise,
            bool canCall,
            int streetRaiseCount)
        {
            if (group == PreflopHandGroup.Weak)
                return BettingAdvice.Fold;

            if (streetRaiseCount >= MaxStreetRaises)
                return ResolveCallOrFoldAdvice(group, callAmount, playerChips, canCall);

            if (group == PreflopHandGroup.Premium
                && canRaise
                && streetRaiseCount < MaxStreetRaises)
            {
                return BettingAdvice.Raise;
            }

            return ResolveCallOrFoldAdvice(group, callAmount, playerChips, canCall);
        }

        private static BettingAdvice ResolveCallOrFoldAdvice(
            PreflopHandGroup group,
            int callAmount,
            int playerChips,
            bool canCall)
        {
            if (!canCall || callAmount > playerChips)
                return BettingAdvice.Fold;

            switch (group)
            {
                case PreflopHandGroup.Premium:
                    return BettingAdvice.Call;

                case PreflopHandGroup.Strong:
                    if (callAmount > playerChips * StrongFoldChipFraction)
                        return BettingAdvice.Fold;
                    return BettingAdvice.Call;

                case PreflopHandGroup.Playable:
                    if (callAmount <= playerChips * PlayableCallChipFraction)
                        return BettingAdvice.Call;
                    return BettingAdvice.Fold;

                default:
                    return BettingAdvice.Fold;
            }
        }

        private static int OpenTier(PreflopHandGroup group) =>
            group switch
            {
                PreflopHandGroup.Premium  => 3,
                PreflopHandGroup.Strong   => 2,
                PreflopHandGroup.Playable => 1,
                _                         => 0,
            };

        /// <summary>Minimum hand tier to open-raise from each seat (higher = tighter).</summary>
        private static int MinOpenTier(PreflopSeatBucket seat) =>
            seat switch
            {
                PreflopSeatBucket.Button     => 1,
                PreflopSeatBucket.Cutoff     => 1,
                PreflopSeatBucket.SmallBlind => 2,
                PreflopSeatBucket.Middle     => 2,
                PreflopSeatBucket.Early      => 2,
                PreflopSeatBucket.BigBlind   => 3,
                _                            => 2,
            };

        private static int RankIndex(Rank rank) => (int)rank - 2;

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

            Suited(12, 11, PreflopHandGroup.Premium);
            Suited(12, 10, PreflopHandGroup.Premium);
            Suited(12, 9, PreflopHandGroup.Strong);
            Suited(12, 8, PreflopHandGroup.Strong);

            return chart;
        }
    }
}
