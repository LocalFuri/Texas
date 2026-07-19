using System.Collections.Generic;

namespace TexasHoldem
{
    /// <summary>
    /// Ace Maverick–only postflop Explanation formatting (display text).
    /// Does not change <see cref="BettingAdvisor"/>, Monte Carlo, or bot decisions.
    /// </summary>
    public static class AceMaverickPostflopCoach
    {
        /// <summary>
        /// Fills Ace coaching labels on the existing snapshot and sets a one-line Explanation.
        /// Classification uses the same analyzers as the rest of the trainer UI — no second Recommend.
        /// </summary>
        public static void ApplyToSnapshot(
            HumanTrainerAdvice advice,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            if (advice == null || advice.IsPreflop)
                return;

            BoardTextureFlags texture = BoardTextureAnalyzer.Analyze(communityCards);
            PostflopDrawFlags draws = PostflopDrawDetector.Detect(holeCards, communityCards);
            bool facingBet = advice.AmountToCall > 0 || advice.FacingRaise || advice.FacingAllIn;

            string concept = ResolveConcept(holeCards, communityCards, draws, texture, advice, facingBet);
            advice.HandCategory = concept;
            advice.BoardTexture = DescribeBoardConcept(texture);
            advice.Explanation = FormatExplanation(advice, concept, texture, draws, facingBet);
        }

        /// <summary>One short coaching line already stored on the snapshot.</summary>
        public static string FormatCoachReason(HumanTrainerAdvice advice) =>
            advice?.Explanation ?? string.Empty;

        /// <summary>
        /// Builds: "{concept} — {coaching}" from snapshot recommendation + concept.
        /// No equity percentages; no seat/tier debug labels.
        /// </summary>
        public static string FormatExplanation(
            HumanTrainerAdvice advice,
            string concept,
            BoardTextureFlags texture,
            PostflopDrawFlags draws,
            bool facingBet)
        {
            if (advice == null)
                return FallbackForAction(BettingAction.Check, facingBet: false);

            string coaching = ResolveCoaching(advice, concept, texture, draws, facingBet);
            if (string.IsNullOrEmpty(concept) || concept == "Unknown")
                return coaching;

            return concept + " — " + coaching;
        }

        private static string ResolveConcept(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards,
            PostflopDrawFlags draws,
            BoardTextureFlags texture,
            HumanTrainerAdvice advice,
            bool facingBet)
        {
            bool river = IsRiver(advice);
            HandRank made = BettingAdvisor.GetMadeHandRank(holeCards, communityCards);

            bool hasFd = (draws & PostflopDrawFlags.FlushDraw) != 0;
            bool hasOesd = (draws & PostflopDrawFlags.OpenEndedStraightDraw) != 0;
            bool hasGutshot = (draws & PostflopDrawFlags.GutshotStraightDraw) != 0;
            bool comboDraw = hasFd && (hasOesd || hasGutshot);
            bool strongDraw = hasFd || hasOesd;

            // Prefer the single most important teaching concept.
            if (comboDraw)
                return "Combo draw";

            if (hasFd && IsNutFlushDraw(holeCards, communityCards))
                return "Nut flush draw";

            if (strongDraw && made <= HandRank.OnePair)
            {
                // Pair + draw: still lead with the draw when it is the lesson.
                if (made == HandRank.HighCard)
                    return hasFd ? "Strong draw" : "Strong draw";
            }

            if (made == HandRank.HighCard)
            {
                if (hasFd)
                    return "Flush draw";
                if (hasOesd)
                    return "Open-ended straight draw";
                if (hasGutshot)
                    return "Gutshot";

                // River air vs a bet: teach as a missed draw without naming a specific draw type.
                if (river && facingBet && advice.RecommendedAction == BettingAction.Fold)
                    return "Missed draw";

                if (HasAceHigh(holeCards))
                    return "Ace high";
                return "High card";
            }

            switch (made)
            {
                case HandRank.RoyalFlush:
                case HandRank.StraightFlush:
                case HandRank.FourOfAKind:
                case HandRank.FullHouse:
                    return river ? "River value hand" : MadeLabel(made, holeCards, communityCards);
                case HandRank.Flush:
                case HandRank.Straight:
                    return river ? "River value hand" : MadeLabel(made, holeCards, communityCards);
                case HandRank.ThreeOfAKind:
                    return IsSet(holeCards, communityCards) ? "Set" : "Trips";
                case HandRank.TwoPair:
                    return river ? "River value hand" : "Two pair";
                case HandRank.OnePair:
                    return ResolvePairConcept(holeCards, communityCards, hasFd, hasOesd, facingBet, river);
                default:
                    break;
            }

            // Board-led concept only when hand is otherwise generic and board is the lesson.
            if ((texture & BoardTextureAnalyzer.WetFlags) != 0
                && advice.RecommendedAction == BettingAction.Raise
                && advice.AmountToCall <= 0)
            {
                return "Wet board";
            }

            if ((texture & (BoardTextureFlags.Paired | BoardTextureFlags.TwoPair | BoardTextureFlags.Trips)) != 0
                && (advice.RecommendedAction == BettingAction.Check
                    || advice.RecommendedAction == BettingAction.Call))
            {
                return "Paired board";
            }

            return "High card";
        }

        private static string ResolvePairConcept(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards,
            bool hasFd,
            bool hasOesd,
            bool facingBet,
            bool river)
        {
            if (hasFd && hasOesd)
                return "Combo draw";
            if (hasFd || hasOesd)
                return "Strong draw";

            if (BettingAdvisor.IsOverpair(holeCards, communityCards))
                return river ? "River value hand" : "Overpair";

            if (IsTopPair(holeCards, communityCards))
                return river ? "River value hand" : "Top pair";

            if (facingBet
                && (BettingAdvisor.IsPocketUnderpair(holeCards, communityCards)
                    || IsMiddlePair(holeCards, communityCards)
                    || IsWeakPair(holeCards, communityCards)))
            {
                return "Bluff catcher";
            }

            if (BettingAdvisor.IsPocketUnderpair(holeCards, communityCards)
                || IsMiddlePair(holeCards, communityCards)
                || IsWeakPair(holeCards, communityCards))
            {
                return "Medium pair";
            }

            return "Medium pair";
        }

        private static string MadeLabel(
            HandRank made,
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            switch (made)
            {
                case HandRank.RoyalFlush: return "Royal flush";
                case HandRank.StraightFlush: return "Straight flush";
                case HandRank.FourOfAKind: return "Quads";
                case HandRank.FullHouse: return "Full house";
                case HandRank.Flush: return "Flush";
                case HandRank.Straight: return "Straight";
                case HandRank.ThreeOfAKind:
                    return IsSet(holeCards, communityCards) ? "Set" : "Trips";
                case HandRank.TwoPair: return "Two pair";
                default: return "Value hand";
            }
        }

        private static string ResolveCoaching(
            HumanTrainerAdvice advice,
            string concept,
            BoardTextureFlags texture,
            PostflopDrawFlags draws,
            bool facingBet)
        {
            bool wet = (texture & BoardTextureAnalyzer.WetFlags) != 0;
            bool potOddsOk = advice.PotOddsPercent > 0f
                && advice.EquityPercent + 0.5f >= advice.PotOddsPercent;
            bool freeCard = advice.AmountToCall <= 0;
            bool river = IsRiver(advice);

            switch (advice.RecommendedAction)
            {
                case BettingAction.Check:
                    return ResolveCheckCoaching(concept, freeCard);

                case BettingAction.Call:
                    return ResolveCallCoaching(concept, potOddsOk);

                case BettingAction.Fold:
                    return ResolveFoldCoaching(concept, facingBet);

                case BettingAction.Raise:
                case BettingAction.AllIn:
                    return ResolveBetRaiseCoaching(concept, freeCard, wet, river, facingBet);

                default:
                    return FallbackForAction(advice.RecommendedAction, facingBet);
            }
        }

        private static string ResolveCheckCoaching(string concept, bool freeCard)
        {
            if (concept == "Ace high" || concept == "High card")
                return freeCard
                    ? "check and take the free card"
                    : "check and control the pot";

            if (concept == "Medium pair" || concept == "Paired board" || concept == "Bluff catcher")
                return "check for pot control";

            if (concept == "Strong draw" || concept == "Flush draw"
                || concept == "Nut flush draw" || concept == "Open-ended straight draw"
                || concept == "Gutshot" || concept == "Combo draw")
            {
                return "check behind to realize equity";
            }

            return "check and control the pot";
        }

        private static string ResolveCallCoaching(string concept, bool potOddsOk)
        {
            if (concept == "Nut flush draw")
                return potOddsOk
                    ? "call with sufficient pot odds"
                    : "call with sufficient pot odds";

            if (concept == "Strong draw" || concept == "Flush draw"
                || concept == "Open-ended straight draw" || concept == "Gutshot")
            {
                return "continue and realize equity";
            }

            if (concept == "Combo draw")
                return "continue and realize equity";

            if (concept == "Bluff catcher")
                return "call only with sufficient pot odds";

            if (concept == "Paired board" || concept == "Medium pair")
                return potOddsOk
                    ? "call with sufficient pot odds"
                    : "call with sufficient pot odds";

            return "call with sufficient pot odds";
        }

        private static string ResolveFoldCoaching(string concept, bool facingBet)
        {
            if (concept == "Missed draw")
                return "fold versus continued aggression";

            if (concept == "Bluff catcher" || concept == "Medium pair" || concept == "Ace high"
                || concept == "High card" || concept == "Gutshot")
            {
                return facingBet ? "fold versus the bet" : "fold versus the bet";
            }

            return "fold versus the bet";
        }

        private static string ResolveBetRaiseCoaching(
            string concept,
            bool freeCard,
            bool wet,
            bool river,
            bool facingBet)
        {
            if (!freeCard || facingBet)
            {
                if (concept == "Combo draw" || concept == "Strong draw" || concept == "Nut flush draw"
                    || concept == "Flush draw" || concept == "Open-ended straight draw")
                {
                    return concept == "Combo draw"
                        ? "aggressive semi-bluff"
                        : "continue and realize equity";
                }

                return "raise for value";
            }

            // Betting when checked to / opening the betting.
            if (concept == "Combo draw")
                return "aggressive semi-bluff";

            if (concept == "Strong draw" || concept == "Nut flush draw" || concept == "Flush draw"
                || concept == "Open-ended straight draw")
            {
                return "aggressive semi-bluff";
            }

            if (concept == "Wet board")
                return "protect against draws";

            if (concept == "Overpair")
                return wet
                    ? "bet for value and protection"
                    : "bet for value and protection";

            if (concept == "Top pair" || concept == "Two pair" || concept == "Set" || concept == "Trips"
                || concept == "Straight" || concept == "Flush" || concept == "Full house"
                || concept == "Quads" || concept == "River value hand")
            {
                if (wet)
                    return "bet for value and protection";
                if (river || concept == "River value hand")
                    return "bet against weaker calls";
                return "value bet against weaker hands";
            }

            if (concept == "Paired board")
                return "control the pot";

            return "bet for value";
        }

        private static string FallbackForAction(BettingAction action, bool facingBet)
        {
            switch (action)
            {
                case BettingAction.Check:
                    return "Check and control the pot";
                case BettingAction.Call:
                    return "Call with sufficient pot odds";
                case BettingAction.Fold:
                    return "Fold versus the bet";
                case BettingAction.Raise:
                case BettingAction.AllIn:
                    return facingBet ? "Raise for value" : "Bet for value";
                default:
                    return "Check and control the pot";
            }
        }

        private static string DescribeBoardConcept(BoardTextureFlags flags)
        {
            if (flags == BoardTextureFlags.None)
                return "Dry board";

            if ((flags & (BoardTextureFlags.Paired | BoardTextureFlags.TwoPair | BoardTextureFlags.Trips)) != 0)
                return "Paired board";

            if ((flags & BoardTextureAnalyzer.WetFlags) != 0)
                return "Wet board";

            return "Dry board";
        }

        private static bool IsRiver(HumanTrainerAdvice advice) =>
            advice != null
            && string.Equals(advice.Street, GamePhase.River.ToString(), System.StringComparison.Ordinal);

        private static bool IsSet(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards)
        {
            if (holeCards == null || holeCards.Count < 2 || holeCards[0] == null || holeCards[1] == null)
                return false;
            if (holeCards[0].Rank != holeCards[1].Rank)
                return false;

            Rank pair = holeCards[0].Rank;
            for (int i = 0; i < communityCards.Count; i++)
            {
                Card c = communityCards[i];
                if (c != null && c.Rank == pair)
                    return true;
            }

            return false;
        }

        private static bool IsTopPair(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards)
        {
            if (holeCards[0].Rank == holeCards[1].Rank)
                return false;
            if (!TryBoardHigh(communityCards, out Rank boardHigh))
                return false;
            return holeCards[0].Rank == boardHigh || holeCards[1].Rank == boardHigh;
        }

        private static bool IsMiddlePair(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards)
        {
            if (holeCards[0].Rank == holeCards[1].Rank)
                return false;

            Rank pairRank = Rank.Two;
            bool found = false;
            for (int i = 0; i < communityCards.Count; i++)
            {
                Card c = communityCards[i];
                if (c == null)
                    continue;
                if (c.Rank == holeCards[0].Rank || c.Rank == holeCards[1].Rank)
                {
                    pairRank = c.Rank;
                    found = true;
                    break;
                }
            }

            if (!found || !TryBoardHigh(communityCards, out Rank boardHigh))
                return false;

            Rank boardLow = boardHigh;
            for (int i = 0; i < communityCards.Count; i++)
            {
                Card c = communityCards[i];
                if (c != null && c.Rank < boardLow)
                    boardLow = c.Rank;
            }

            return pairRank < boardHigh && pairRank > boardLow;
        }

        private static bool IsWeakPair(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards)
        {
            if (BettingAdvisor.IsOverpair(holeCards, communityCards) || IsTopPair(holeCards, communityCards))
                return false;
            if (BettingAdvisor.IsPocketUnderpair(holeCards, communityCards) || IsMiddlePair(holeCards, communityCards))
                return false;
            return BettingAdvisor.GetMadeHandRank(holeCards, communityCards) == HandRank.OnePair;
        }

        private static bool TryBoardHigh(IReadOnlyList<Card> communityCards, out Rank boardHigh)
        {
            boardHigh = Rank.Two;
            bool any = false;
            for (int i = 0; i < communityCards.Count; i++)
            {
                Card c = communityCards[i];
                if (c == null)
                    continue;
                any = true;
                if (c.Rank > boardHigh)
                    boardHigh = c.Rank;
            }

            return any;
        }

        private static bool HasAceHigh(IReadOnlyList<Card> holeCards) =>
            holeCards != null && holeCards.Count >= 2
            && (holeCards[0].Rank == Rank.Ace || holeCards[1].Rank == Rank.Ace);

        private static bool IsNutFlushDraw(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards)
        {
            var holeSuits = new int[4];
            var boardSuits = new int[4];
            for (int i = 0; i < holeCards.Count; i++)
            {
                if (holeCards[i] != null)
                    holeSuits[(int)holeCards[i].Suit]++;
            }

            for (int i = 0; i < communityCards.Count; i++)
            {
                if (communityCards[i] != null)
                    boardSuits[(int)communityCards[i].Suit]++;
            }

            for (int s = 0; s < 4; s++)
            {
                if (holeSuits[s] + boardSuits[s] != 4 || holeSuits[s] < 1)
                    continue;

                for (int i = 0; i < holeCards.Count; i++)
                {
                    Card h = holeCards[i];
                    if (h != null && (int)h.Suit == s && h.Rank == Rank.Ace)
                        return true;
                }
            }

            return false;
        }
    }
}
