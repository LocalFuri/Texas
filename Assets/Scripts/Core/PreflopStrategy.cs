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

    public enum EffectiveStackBand
    {
        Short,
        Medium,
        Deep,
    }

    /// <summary>Shared preflop hand chart and deterministic advice (mirrors bot tiers + position).</summary>
    public static class PreflopStrategy
    {
        private const int MaxStreetRaises = 4;

        private const float PlayableCallChipFraction = 0.20f;
        private const float StrongFoldChipFraction   = 0.35f;

        private const float SmallPairCallChipFraction = 0.12f;
        /// <summary>Post-call stack must exceed call × this ratio (~15 BB when call ≈ 3 BB).</summary>
        private const int SmallPairMinPostCallToCallRatio = 5;

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

        public static float ResolveEffectiveStackBB(int heroStackChips, int villainStackChips, int bigBlind)
        {
            if (bigBlind <= 0)
                return 0f;

            int effectiveChips = Mathf.Min(
                Mathf.Max(0, heroStackChips),
                Mathf.Max(0, villainStackChips));

            return effectiveChips / (float)bigBlind;
        }

        public static EffectiveStackBand ResolveEffectiveStackBand(
            int heroStackChips,
            int villainStackChips,
            int bigBlind)
        {
            float effectiveStackBB = ResolveEffectiveStackBB(heroStackChips, villainStackChips, bigBlind);
            if (effectiveStackBB <= 25f)
                return EffectiveStackBand.Short;
            if (effectiveStackBB <= 100f)
                return EffectiveStackBand.Medium;
            return EffectiveStackBand.Deep;
        }

        /// <summary>Shover total chips (stack + committed); falls back to <paramref name="tableCurrentBet"/>.</summary>
        public static int ResolveVillainTotalChipsForEffectiveStack(
            PlayerState hero,
            IReadOnlyList<PlayerState> allPlayers,
            int tableCurrentBet)
        {
            if (allPlayers != null && tableCurrentBet > 0)
            {
                foreach (PlayerState player in allPlayers)
                {
                    if (player == null || player == hero || player.HasFolded)
                        continue;

                    if (player.CurrentBet == tableCurrentBet)
                        return player.Chips + player.CurrentBet;
                }
            }

            return tableCurrentBet;
        }

        public static void LogEffectiveStack(
            PlayerState hero,
            IReadOnlyList<PlayerState> allPlayers,
            int tableCurrentBet,
            int bigBlind)
        {
            if (hero == null || bigBlind <= 0)
                return;

            int heroTotal    = hero.Chips + hero.CurrentBet;
            int villainTotal = ResolveVillainTotalChipsForEffectiveStack(hero, allPlayers, tableCurrentBet);
            float effectiveStackBB = ResolveEffectiveStackBB(heroTotal, villainTotal, bigBlind);
            EffectiveStackBand band = ResolveEffectiveStackBand(heroTotal, villainTotal, bigBlind);

            Debug.Log($"[EffectiveStack] EffectiveStackBB = {effectiveStackBB:0} Band = {band}");
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
            int potBeforeAction,
            int callAmount,
            int playerChips,
            bool canCheck,
            bool canRaise,
            bool canCall,
            int streetRaiseCount,
            IReadOnlyList<Card> holeCards = null)
        {
            if (facingRaise)
                return RecommendFacingRaise(
                    group, seat, potBeforeAction, callAmount, playerChips,
                    canRaise, canCall, streetRaiseCount, holeCards);

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
                    && streetRaiseCount < MaxStreetRaises
                    && playerChips > 0)
                {
                    return BettingAdvice.Raise;
                }

                return BettingAdvice.Check;
            }

            if (streetRaiseCount < MaxStreetRaises
                && OpenTier(group) >= MinOpenTier(seat)
                && playerChips > 0)
            {
                return BettingAdvice.Raise;
            }

            // Unopened pots: never limp. Below open tier (or unable to raise) → fold,
            // except a free check (BB option) is handled above / here.
            if (canCheck)
                return BettingAdvice.Check;

            return BettingAdvice.Fold;
        }

        private static BettingAdvice RecommendFacingRaise(
            PreflopHandGroup group,
            PreflopSeatBucket seat,
            int potBeforeAction,
            int callAmount,
            int playerChips,
            bool canRaise,
            bool canCall,
            int streetRaiseCount,
            IReadOnlyList<Card> holeCards)
        {
            if (group == PreflopHandGroup.Weak)
            {
                LogPreflopDecision(holeCards, group, FacingAllInHandTier.None, callAmount, playerChips,
                    potBeforeAction, IsFacingAllIn(callAmount, playerChips), BettingAdvice.Fold,
                    "RecommendFacingRaise:Weak");
                return BettingAdvice.Fold;
            }

            if (streetRaiseCount >= MaxStreetRaises)
                return ResolveCallOrFoldAdvice(group, potBeforeAction, callAmount, playerChips, canCall, streetRaiseCount, holeCards);

            bool facingAllIn = IsFacingAllIn(callAmount, playerChips);
            if (group == PreflopHandGroup.Premium
                && canRaise
                && !facingAllIn
                && streetRaiseCount < MaxStreetRaises)
            {
                // 3-bet any Premium vs a single open; 4-bet only QQ+/AK vs a 3-bet.
                bool isThreeBetSpot = streetRaiseCount == 1;
                bool isFourBetSpot  = streetRaiseCount == 2 && IsPremiumFourBetHand(holeCards);

                if (isThreeBetSpot || isFourBetSpot)
                {
                    LogPreflopDecision(holeCards, group, ClassifyFacingAllInHand(holeCards), callAmount, playerChips,
                        potBeforeAction, facingAllIn, BettingAdvice.Raise,
                        isThreeBetSpot
                            ? "RecommendFacingRaise:Premium3Bet"
                            : "RecommendFacingRaise:Premium4Bet");
                    return BettingAdvice.Raise;
                }
            }

            // Strong: 3-bet only vs a single open from BTN / CO / BB.
            if (group == PreflopHandGroup.Strong
                && canRaise
                && !facingAllIn
                && streetRaiseCount == 1
                && (seat == PreflopSeatBucket.Button
                    || seat == PreflopSeatBucket.Cutoff
                    || seat == PreflopSeatBucket.BigBlind))
            {
                LogPreflopDecision(holeCards, group, ClassifyFacingAllInHand(holeCards), callAmount, playerChips,
                    potBeforeAction, facingAllIn, BettingAdvice.Raise,
                    "RecommendFacingRaise:Strong3Bet");
                return BettingAdvice.Raise;
            }

            return ResolveCallOrFoldAdvice(group, potBeforeAction, callAmount, playerChips, canCall, streetRaiseCount, holeCards);
        }

        private static bool IsFacingAllIn(int callAmount, int playerChips) =>
            playerChips > 0 && callAmount >= Mathf.CeilToInt(playerChips * 0.85f);

        /// <summary>Hands that 4-bet vs a 3-bet (QQ+ and AK). JJ / AQs use call/fold instead.</summary>
        private static bool IsPremiumFourBetHand(IReadOnlyList<Card> holeCards)
        {
            if (holeCards == null || holeCards.Count < 2)
                return false;

            Rank r0 = holeCards[0].Rank;
            Rank r1 = holeCards[1].Rank;
            Rank hi = (Rank)Mathf.Max((int)r0, (int)r1);
            Rank lo = (Rank)Mathf.Min((int)r0, (int)r1);

            if (hi == lo && hi >= Rank.Queen)
                return true;

            return hi == Rank.Ace && lo == Rank.King;
        }

        private enum FacingAllInHandTier
        {
            None,
            Premium,
            Strong,
            Fold,
        }

        private static FacingAllInHandTier ClassifyFacingAllInHand(IReadOnlyList<Card> holeCards)
        {
            if (holeCards == null || holeCards.Count < 2)
                return FacingAllInHandTier.None;

            Rank r0 = holeCards[0].Rank;
            Rank r1 = holeCards[1].Rank;

            Rank hi = (Rank)Mathf.Max((int)r0, (int)r1);
            Rank lo = (Rank)Mathf.Min((int)r0, (int)r1);
            bool isPair = hi == lo;

            if ((isPair && (hi == Rank.Ace || hi == Rank.King || hi == Rank.Queen || hi == Rank.Jack))
                || (hi == Rank.Ace && lo == Rank.King))
            {
                return FacingAllInHandTier.Premium;
            }

            if ((isPair && (hi == Rank.Ten || hi == Rank.Nine))
                || (hi == Rank.Ace && lo == Rank.Queen)
                || (hi == Rank.Ace && lo == Rank.Jack))
            {
                return FacingAllInHandTier.Strong;
            }

            return FacingAllInHandTier.Fold;
        }

        private static BettingAdvice ResolveFacingAllInAdvice(IReadOnlyList<Card> holeCards)
        {
            switch (ClassifyFacingAllInHand(holeCards))
            {
                case FacingAllInHandTier.Premium:
                case FacingAllInHandTier.Strong:
                    return BettingAdvice.Call;
                default:
                    return BettingAdvice.Fold;
            }
        }

        private static void LogPreflopDecision(
            IReadOnlyList<Card> holeCards,
            PreflopHandGroup group,
            FacingAllInHandTier handTier,
            int callAmount,
            int playerChips,
            int potBeforeAction,
            bool facingAllIn,
            BettingAdvice advice,
            string returnPath)
        {
            string cards = holeCards == null || holeCards.Count < 2
                ? "(none)"
                : $"{holeCards[0]} {holeCards[1]}";

            Debug.Log(
                $"[PreflopDebug] holeCards={cards} group={group} handTier={handTier} " +
                $"callAmount={callAmount} playerChips={playerChips} potBeforeAction={potBeforeAction} " +
                $"facingAllIn={facingAllIn} advice={advice} path={returnPath}");
        }

        private static BettingAdvice ResolveCallOrFoldAdvice(
            PreflopHandGroup group,
            int potBeforeAction,
            int callAmount,
            int playerChips,
            bool canCall,
            int streetRaiseCount,
            IReadOnlyList<Card> holeCards)
        {
            bool facingAllIn = IsFacingAllIn(callAmount, playerChips);
            FacingAllInHandTier handTier = ClassifyFacingAllInHand(holeCards);

            // Dedicated facing-all-in rule runs before chip-cap guards so short-stack calls
            // (callAmount > playerChips) still reach the hand matcher and map to AllIn in ResolveAction.
            if (facingAllIn)
            {
                BettingAdvice advice = ResolveFacingAllInAdvice(holeCards);
                LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                    facingAllIn, advice,
                    handTier == FacingAllInHandTier.None
                        ? "ResolveCallOrFoldAdvice:FacingAllIn:MissingHoleCards"
                        : advice == BettingAdvice.Call
                            ? $"ResolveCallOrFoldAdvice:FacingAllIn:{handTier}"
                            : "ResolveCallOrFoldAdvice:FacingAllIn:Fold");
                return advice;
            }

            if (!canCall || callAmount > playerChips)
            {
                LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                    facingAllIn, BettingAdvice.Fold,
                    !canCall
                        ? "ResolveCallOrFoldAdvice:!canCall"
                        : "ResolveCallOrFoldAdvice:callAmount>playerChips");
                return BettingAdvice.Fold;
            }

            if (IsSmallPocketPair(holeCards))
            {
                return ResolveSmallPairFacingRaiseAdvice(
                    group, handTier, potBeforeAction, callAmount, playerChips, streetRaiseCount, holeCards);
            }

            switch (group)
            {
                case PreflopHandGroup.Premium:
                    LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                        facingAllIn, BettingAdvice.Call, "ResolveCallOrFoldAdvice:Premium");
                    return BettingAdvice.Call;

                case PreflopHandGroup.Strong:
                    if (callAmount > playerChips * StrongFoldChipFraction)
                    {
                        LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                            facingAllIn, BettingAdvice.Fold, "ResolveCallOrFoldAdvice:Strong:TooLarge");
                        return BettingAdvice.Fold;
                    }

                    LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                        facingAllIn, BettingAdvice.Call, "ResolveCallOrFoldAdvice:Strong");
                    return BettingAdvice.Call;

                case PreflopHandGroup.Playable:
                    if (callAmount <= playerChips * PlayableCallChipFraction)
                    {
                        LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                            facingAllIn, BettingAdvice.Call, "ResolveCallOrFoldAdvice:Playable");
                        return BettingAdvice.Call;
                    }

                    LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                        facingAllIn, BettingAdvice.Fold, "ResolveCallOrFoldAdvice:Playable:TooLarge");
                    return BettingAdvice.Fold;

                default:
                    LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                        facingAllIn, BettingAdvice.Fold, "ResolveCallOrFoldAdvice:Default");
                    return BettingAdvice.Fold;
            }
        }

        private static bool IsSmallPocketPair(IReadOnlyList<Card> holeCards)
        {
            if (holeCards == null || holeCards.Count < 2)
                return false;

            if (holeCards[0].Rank != holeCards[1].Rank)
                return false;

            Rank rank = holeCards[0].Rank;
            return rank >= Rank.Two && rank <= Rank.Six;
        }

        private static BettingAdvice ResolveSmallPairFacingRaiseAdvice(
            PreflopHandGroup group,
            FacingAllInHandTier handTier,
            int potBeforeAction,
            int callAmount,
            int playerChips,
            int streetRaiseCount,
            IReadOnlyList<Card> holeCards)
        {
            const bool facingAllIn = false;

            if (streetRaiseCount >= 2)
            {
                LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                    facingAllIn, BettingAdvice.Fold, "ResolveSmallPairFacingRaise:Vs3BetPlus");
                return BettingAdvice.Fold;
            }

            if (callAmount > playerChips * SmallPairCallChipFraction)
            {
                LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                    facingAllIn, BettingAdvice.Fold, "ResolveSmallPairFacingRaise:CallTooLarge");
                return BettingAdvice.Fold;
            }

            int postCallStack = playerChips - callAmount;
            if (postCallStack <= callAmount * SmallPairMinPostCallToCallRatio)
            {
                LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                    facingAllIn, BettingAdvice.Fold, "ResolveSmallPairFacingRaise:InsufficientImpliedOdds");
                return BettingAdvice.Fold;
            }

            LogPreflopDecision(holeCards, group, handTier, callAmount, playerChips, potBeforeAction,
                facingAllIn, BettingAdvice.Call, "ResolveSmallPairFacingRaise:SetMine");
            return BettingAdvice.Call;
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
