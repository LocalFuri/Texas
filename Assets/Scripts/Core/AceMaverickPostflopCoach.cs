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
        /// Classification is display-only; advice is not recomputed.
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
            bool isBet = IsBetAction(advice);
            bool isRaiseFacing = IsRaiseFacingAction(advice);

            // Hand category from cards/draws first — then label may adjust for Call-only concepts.
            string handCategory = ResolveHandCategory(holeCards, communityCards, draws, advice);
            string concept = ResolveDisplayConcept(
                handCategory, advice.RecommendedAction, facingBet, isBet, isRaiseFacing, draws, texture);

            advice.HandCategory = concept;
            advice.BoardTexture = DescribeBoardConcept(texture);
            advice.Explanation = FormatExplanation(concept, advice.RecommendedAction, texture, draws, isBet);
        }

        /// <summary>One short coaching line already stored on the snapshot.</summary>
        public static string FormatCoachReason(HumanTrainerAdvice advice) =>
            advice?.Explanation ?? string.Empty;

        /// <summary>Builds "{concept} — {coaching}" matched to the recommended action.</summary>
        public static string FormatExplanation(
            string concept,
            BettingAction action,
            BoardTextureFlags texture,
            PostflopDrawFlags draws,
            bool isBet)
        {
            string coaching = ResolveCoaching(concept, action, texture, draws, isBet);
            if (string.IsNullOrEmpty(concept) || concept == "Unknown")
                return Capitalize(coaching);

            return concept + " — " + coaching;
        }

        // -------------------------------------------------------------------------
        // Hand category (from detectors only — not from recommendation)
        // -------------------------------------------------------------------------

        private static string ResolveHandCategory(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards,
            PostflopDrawFlags draws,
            HumanTrainerAdvice advice)
        {
            if (communityCards == null || communityCards.Count < 3
                || holeCards == null || holeCards.Count < 2)
            {
                return "Unknown";
            }

            HandRank made = BettingAdvisor.GetMadeHandRank(holeCards, communityCards);
            bool hasFd = (draws & PostflopDrawFlags.FlushDraw) != 0;
            bool hasOesd = (draws & PostflopDrawFlags.OpenEndedStraightDraw) != 0;
            bool hasGutshot = (draws & PostflopDrawFlags.GutshotStraightDraw) != 0;
            bool comboDraw = hasFd && (hasOesd || hasGutshot);
            bool river = IsRiver(advice);

            // Draws (only when detector found them). Prefer combo / specific draw names.
            if (comboDraw && made <= HandRank.OnePair)
                return "Combo draw";

            if (made == HandRank.HighCard)
            {
                if (hasFd && IsNutFlushDraw(holeCards, communityCards))
                    return "Nut flush draw";
                if (hasFd)
                    return "Flush draw";
                if (hasOesd)
                    return "Open-ended straight draw";
                if (hasGutshot)
                    return "Gutshot";

                // River fold air: teach as missed draw (no specific draw claimed).
                if (river
                    && (advice.RecommendedAction == BettingAction.Fold)
                    && (advice.AmountToCall > 0 || advice.FacingRaise || advice.FacingAllIn))
                {
                    return "Missed draw";
                }

                if (HasAceHigh(holeCards))
                    return "Ace high";
                return "High card";
            }

            // Pair + draw: keep the made-hand name when strong; otherwise name the draw.
            if (made == HandRank.OnePair)
            {
                string pairName = ResolvePairName(holeCards, communityCards);
                bool strongPair = pairName == "Overpair" || pairName == "Top pair";

                if (!strongPair)
                {
                    if (comboDraw)
                        return "Combo draw";
                    if (hasFd && IsNutFlushDraw(holeCards, communityCards))
                        return "Nut flush draw";
                    if (hasFd)
                        return "Flush draw";
                    if (hasOesd)
                        return "Open-ended straight draw";
                    if (hasGutshot)
                        return "Gutshot";
                }

                return pairName;
            }

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
                default: return "High card";
            }
        }

        private static string ResolvePairName(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards)
        {
            if (BettingAdvisor.IsOverpair(holeCards, communityCards))
                return "Overpair";
            if (IsTopPair(holeCards, communityCards))
                return "Top pair";
            if (IsMiddlePair(holeCards, communityCards))
                return "Middle pair";
            if (IsBottomPair(holeCards, communityCards))
                return "Bottom pair";
            if (BettingAdvisor.IsPocketUnderpair(holeCards, communityCards))
                return "Weak pair";

            // Board pair only (no hole pair) — Ace high is not a weak pair.
            if (HasAceHigh(holeCards))
                return "Ace high";

            return "Weak pair";
        }

        /// <summary>
        /// Display concept may use Call-only labels (Bluff catcher) or Fold-only (Missed draw).
        /// Never labels Raise/Bet as Bluff catcher.
        /// </summary>
        private static string ResolveDisplayConcept(
            string handCategory,
            BettingAction action,
            bool facingBet,
            bool isBet,
            bool isRaiseFacing,
            PostflopDrawFlags draws,
            BoardTextureFlags texture)
        {
            bool betting = isBet || isRaiseFacing
                || action == BettingAction.Raise
                || action == BettingAction.AllIn;

            // Bluff catcher is Call-only.
            if (action == BettingAction.Call
                && facingBet
                && IsWeakShowdownPair(handCategory)
                && draws == PostflopDrawFlags.None)
            {
                return "Bluff catcher";
            }

            // Never keep Bluff catcher / Missed draw on a bet/raise.
            if (betting)
            {
                if (handCategory == "Missed draw")
                    return HasDraw(draws) ? PrimaryDrawName(draws) : "Ace high";
                return handCategory;
            }

            // Board texture as concept only when hand is generic high-card check/call.
            if ((action == BettingAction.Check || action == BettingAction.Call)
                && (handCategory == "High card" || handCategory == "Ace high")
                && (texture & (BoardTextureFlags.Paired | BoardTextureFlags.TwoPair | BoardTextureFlags.Trips)) != 0)
            {
                return "Paired board";
            }

            return handCategory;
        }

        // -------------------------------------------------------------------------
        // Coaching phrases matched to RecommendedAction
        // -------------------------------------------------------------------------

        private static string ResolveCoaching(
            string concept,
            BettingAction action,
            BoardTextureFlags texture,
            PostflopDrawFlags draws,
            bool isBet)
        {
            switch (action)
            {
                case BettingAction.Check:
                    return ResolveCheckCoaching(concept);

                case BettingAction.Call:
                    return ResolveCallCoaching(concept);

                case BettingAction.Fold:
                    return ResolveFoldCoaching(concept);

                case BettingAction.Raise:
                case BettingAction.AllIn:
                    return ResolveBetOrRaiseCoaching(concept, texture, draws, isBet);

                default:
                    return "Check for pot control";
            }
        }

        private static string ResolveCheckCoaching(string concept)
        {
            if (concept == "Ace high" || concept == "High card" || IsDrawConcept(concept))
                return "Take the free card";

            if (IsWeakShowdownPair(concept)
                || concept == "Bluff catcher"
                || concept == "Paired board"
                || concept == "Middle pair"
                || concept == "Bottom pair"
                || concept == "Weak pair")
            {
                return "Check for pot control";
            }

            return "Check behind";
        }

        private static string ResolveCallCoaching(string concept)
        {
            if (concept == "Bluff catcher")
                return "call only with sufficient pot odds";

            if (IsDrawConcept(concept)
                && (concept == "Combo draw"
                    || concept == "Nut flush draw"
                    || concept == "Flush draw"
                    || concept == "Open-ended straight draw"
                    || concept == "Strong draw"))
            {
                return "Continue with a strong draw";
            }

            return "Call with sufficient pot odds";
        }

        private static string ResolveFoldCoaching(string concept)
        {
            if (concept == "Missed draw")
                return "Draw missed";

            if (IsWeakShowdownPair(concept)
                || concept == "Bluff catcher"
                || concept == "Ace high"
                || concept == "High card"
                || concept == "Paired board")
            {
                return "Hand too weak to continue";
            }

            return "Fold versus continued aggression";
        }

        /// <summary>
        /// BET (opening the betting) vs RAISE (facing a bet) use matching verbs.
        /// </summary>
        private static string ResolveBetOrRaiseCoaching(
            string concept,
            BoardTextureFlags texture,
            PostflopDrawFlags draws,
            bool isBet)
        {
            // Never attach value language to air / weak pairs / missed draws / draws.
            if (IsNonValueConcept(concept) || IsDrawConcept(concept) || HasDraw(draws))
                return isBet ? "Bet as a semi-bluff" : "Raise as a semi-bluff";

            bool wet = (texture & BoardTextureAnalyzer.WetFlags) != 0;
            if (wet && IsStrongMadeConcept(concept))
                return isBet ? "Bet to protect against draws" : "Raise to protect against draws";

            return isBet ? "Bet for value" : "Raise for value";
        }

        // -------------------------------------------------------------------------
        // Concept helpers
        // -------------------------------------------------------------------------

        private static bool IsDrawConcept(string concept) =>
            concept == "Combo draw"
            || concept == "Nut flush draw"
            || concept == "Flush draw"
            || concept == "Open-ended straight draw"
            || concept == "Gutshot"
            || concept == "Strong draw";

        private static bool IsWeakShowdownPair(string concept) =>
            concept == "Bottom pair"
            || concept == "Middle pair"
            || concept == "Weak pair"
            || concept == "Underpair";

        private static bool IsNonValueConcept(string concept) =>
            concept == "Ace high"
            || concept == "High card"
            || concept == "Missed draw"
            || concept == "Bluff catcher"
            || IsWeakShowdownPair(concept)
            || concept == "Paired board";

        private static bool IsStrongMadeConcept(string concept) =>
            concept == "Overpair"
            || concept == "Top pair"
            || concept == "Two pair"
            || concept == "Set"
            || concept == "Trips"
            || concept == "Straight"
            || concept == "Flush"
            || concept == "Full house"
            || concept == "Quads"
            || concept == "Straight flush"
            || concept == "Royal flush";

        private static bool HasDraw(PostflopDrawFlags draws) => draws != PostflopDrawFlags.None;

        private static string PrimaryDrawName(PostflopDrawFlags draws)
        {
            bool hasFd = (draws & PostflopDrawFlags.FlushDraw) != 0;
            bool hasOesd = (draws & PostflopDrawFlags.OpenEndedStraightDraw) != 0;
            bool hasGut = (draws & PostflopDrawFlags.GutshotStraightDraw) != 0;
            if (hasFd && (hasOesd || hasGut))
                return "Combo draw";
            if (hasFd)
                return "Flush draw";
            if (hasOesd)
                return "Open-ended straight draw";
            if (hasGut)
                return "Gutshot";
            return "High card";
        }

        private static bool IsBetAction(HumanTrainerAdvice advice) =>
            advice != null
            && (advice.RecommendedAction == BettingAction.Raise
                || advice.RecommendedAction == BettingAction.AllIn)
            && advice.AmountToCall <= 0
            && !advice.FacingRaise;

        private static bool IsRaiseFacingAction(HumanTrainerAdvice advice) =>
            advice != null
            && (advice.RecommendedAction == BettingAction.Raise
                || advice.RecommendedAction == BettingAction.AllIn)
            && (advice.AmountToCall > 0 || advice.FacingRaise || advice.FacingAllIn);

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

        private static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            if (char.IsUpper(text[0]))
                return text;
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
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
            if (!TryGetHoleBoardPairRank(holeCards, communityCards, out Rank pairRank))
                return false;
            if (!TryBoardHigh(communityCards, out Rank boardHigh)
                || !TryBoardLow(communityCards, out Rank boardLow))
            {
                return false;
            }

            return pairRank < boardHigh && pairRank > boardLow;
        }

        private static bool IsBottomPair(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards)
        {
            if (!TryGetHoleBoardPairRank(holeCards, communityCards, out Rank pairRank))
                return false;
            if (!TryBoardLow(communityCards, out Rank boardLow))
                return false;
            return pairRank == boardLow;
        }

        private static bool TryGetHoleBoardPairRank(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> communityCards,
            out Rank pairRank)
        {
            pairRank = Rank.Two;
            if (holeCards[0].Rank == holeCards[1].Rank)
                return false;

            for (int i = 0; i < communityCards.Count; i++)
            {
                Card c = communityCards[i];
                if (c == null)
                    continue;
                if (c.Rank == holeCards[0].Rank || c.Rank == holeCards[1].Rank)
                {
                    pairRank = c.Rank;
                    return true;
                }
            }

            return false;
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

        private static bool TryBoardLow(IReadOnlyList<Card> communityCards, out Rank boardLow)
        {
            boardLow = Rank.Ace;
            bool any = false;
            for (int i = 0; i < communityCards.Count; i++)
            {
                Card c = communityCards[i];
                if (c == null)
                    continue;
                any = true;
                if (c.Rank < boardLow)
                    boardLow = c.Rank;
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
