using System.Collections.Generic;

namespace TexasHoldem
{
    /// <summary>
    /// Ace Maverick human-trainer-only preflop display/advice tweaks.
    /// Does not change shared <see cref="PreflopStrategy"/> or bot decisions.
    /// </summary>
    public static class AceMaverickPreflopCoach
    {
        /// <summary>
        /// Unopened Middle: keep shared Strong+ raises; upgrade Fold → Raise for a
        /// small 6-max allowlist. All other Playable hands stay Fold.
        /// </summary>
        public static BettingAdvice ApplyUnopenedMiddleAllowlist(
            BettingAdvice sharedAdvice,
            PreflopSeatBucket seat,
            bool facingRaise,
            int streetRaiseCount,
            int callersBefore,
            IReadOnlyList<Card> holeCards,
            bool canRaise)
        {
            if (seat != PreflopSeatBucket.Middle)
                return sharedAdvice;

            if (facingRaise || streetRaiseCount > 0 || callersBefore > 0)
                return sharedAdvice;

            if (!canRaise)
                return sharedAdvice;

            // Preserve existing Strong+ (and any other non-Fold shared open).
            if (sharedAdvice != BettingAdvice.Fold)
                return sharedAdvice;

            if (!IsMiddleUnopenedOpenAllowlisted(holeCards))
                return sharedAdvice;

            return BettingAdvice.Raise;
        }

        /// <summary>
        /// 77/66/55, A9s–A5s, KTs, QTs, 98s/87s/76s.
        /// </summary>
        public static bool IsMiddleUnopenedOpenAllowlisted(IReadOnlyList<Card> holeCards)
        {
            if (holeCards == null || holeCards.Count < 2)
                return false;

            Rank r0 = holeCards[0].Rank;
            Rank r1 = holeCards[1].Rank;
            bool suited = holeCards[0].Suit == holeCards[1].Suit;

            Rank hi = r0 >= r1 ? r0 : r1;
            Rank lo = r0 >= r1 ? r1 : r0;

            if (hi == lo)
            {
                return hi == Rank.Seven
                    || hi == Rank.Six
                    || hi == Rank.Five;
            }

            if (!suited)
                return false;

            if (hi == Rank.Ace && lo >= Rank.Five && lo <= Rank.Nine)
                return true;

            if (hi == Rank.King && lo == Rank.Ten)
                return true;

            if (hi == Rank.Queen && lo == Rank.Ten)
                return true;

            if (hi == Rank.Nine && lo == Rank.Eight)
                return true;

            if (hi == Rank.Eight && lo == Rank.Seven)
                return true;

            if (hi == Rank.Seven && lo == Rank.Six)
                return true;

            return false;
        }

        /// <summary>
        /// Short Ace Coach reason from an existing <see cref="HumanTrainerAdvice"/> snapshot.
        /// Display-only; does not recompute strategy.
        /// </summary>
        public static string FormatCoachReason(HumanTrainerAdvice advice)
        {
            if (advice == null || !advice.IsPreflop)
                return advice?.Explanation ?? string.Empty;

            bool unopened = !advice.FacingRaise
                && advice.StreetRaiseCount <= 0
                && advice.CallersBefore <= 0;

            TryParseHoleShape(advice.HoleCards, out bool isPair, out Rank pairRank,
                out bool suited, out Rank hi, out Rank lo);

            if (advice.FacingAllIn)
                return FormatFacingAllInReason(advice);

            if (unopened)
                return FormatUnopenedReason(advice, isPair, pairRank, suited, hi, lo);

            if (advice.FacingRaise || advice.StreetRaiseCount > 0 || advice.AmountToCall > 0)
                return FormatFacingRaiseReason(advice, isPair, pairRank);

            if (advice.CallersBefore > 0)
                return FormatLimpedReason(advice, isPair, pairRank, suited, hi, lo);

            return FormatUnopenedReason(advice, isPair, pairRank, suited, hi, lo);
        }

        private static string FormatFacingAllInReason(HumanTrainerAdvice advice)
        {
            switch (advice.RecommendedAction)
            {
                case BettingAction.Call:
                case BettingAction.AllIn:
                    if (advice.PreflopHandGroup == PreflopHandGroup.Premium)
                        return "Premium shove call";
                    return "Call with pot odds";

                case BettingAction.Fold:
                    return "Too weak to defend";

                default:
                    return "Premium shove call";
            }
        }

        private static string FormatUnopenedReason(
            HumanTrainerAdvice advice,
            bool isPair,
            Rank pairRank,
            bool suited,
            Rank hi,
            Rank lo)
        {
            string seat = FormatSeatShort(advice.Position);

            switch (advice.RecommendedAction)
            {
                case BettingAction.Raise:
                case BettingAction.AllIn:
                    return FormatUnopenedOpenReason(advice, seat, isPair, pairRank, suited, hi, lo);

                case BettingAction.Check:
                    if (seat == "BB")
                        return "Defend Big Blind";
                    return "No raise needed";

                case BettingAction.Call:
                    // Limped-in call shouldn't hit here often; keep short.
                    if (isPair && IsSetMinePair(pairRank))
                        return "Small pair for set mining";
                    return "Call with pot odds";

                default:
                    return FormatUnopenedFoldReason(advice, isPair, pairRank, suited, hi, lo);
            }
        }

        /// <summary>
        /// Teach why this hand folds unopened from this seat (display only).
        /// Deterministic: same hand + seat always yields the same text.
        /// </summary>
        private static string FormatUnopenedFoldReason(
            HumanTrainerAdvice advice,
            bool isPair,
            Rank pairRank,
            bool suited,
            Rank hi,
            Rank lo)
        {
            string hand = FormatHandCode(isPair, pairRank, suited, hi, lo);
            string seat = FormatSeatShort(advice.Position);
            FoldTheme theme = ClassifyUnopenedFoldTheme(isPair, pairRank, suited, hi, lo);
            return FormatPositionFoldExplanation(seat, hand, theme, suited, hi);
        }

        private enum FoldTheme
        {
            DominatedBroadway,
            DominatedKing,
            DominatedQueen,
            DominatedAce,
            SmallPair,
            SpeculativeSuited,
            WeakOffsuit,
            TooWeakForRange,
        }

        private static FoldTheme ClassifyUnopenedFoldTheme(
            bool isPair,
            Rank pairRank,
            bool suited,
            Rank hi,
            Rank lo)
        {
            if (isPair)
                return pairRank <= Rank.Eight ? FoldTheme.SmallPair : FoldTheme.TooWeakForRange;

            if (hi == Rank.Ace)
                return FoldTheme.DominatedAce;

            if (hi == Rank.King)
                return FoldTheme.DominatedKing;

            if (hi == Rank.Queen)
                return FoldTheme.DominatedQueen;

            // Offsuit Broadway / near-Broadway (JT+, T9o-style covered via hi>=T).
            if (!suited && hi >= Rank.Ten && lo >= Rank.Nine)
                return FoldTheme.DominatedBroadway;

            if (suited && (hi - lo) <= 2 && hi <= Rank.Jack)
                return FoldTheme.SpeculativeSuited;

            if (!suited)
                return FoldTheme.WeakOffsuit;

            return FoldTheme.TooWeakForRange;
        }

        private static string FormatPositionFoldExplanation(
            string seat,
            string hand,
            FoldTheme theme,
            bool suited,
            Rank hi)
        {
            string label = string.IsNullOrEmpty(hand) ? "This hand" : hand;
            int pick = StablePick(label, seat, theme);

            switch (seat)
            {
                case "EP":
                    return FormatEarlyPositionFold(label, theme, pick);
                case "MP":
                    return FormatMiddlePositionFold(label, theme, pick, hi);
                case "SB":
                    return FormatSmallBlindFold(label, theme, pick, suited, hi);
                case "CO":
                    return FormatCutoffFold(label, theme, pick);
                case "BTN":
                    return FormatButtonFold(label, theme, pick);
                case "BB":
                    return FormatBigBlindFold(label, theme, pick);
                default:
                    return FormatMiddlePositionFold(label, theme, pick, hi);
            }
        }

        private static string FormatEarlyPositionFold(string hand, FoldTheme theme, int pick)
        {
            switch (theme)
            {
                case FoldTheme.DominatedBroadway:
                case FoldTheme.DominatedQueen:
                    return Pick(pick,
                        hand + " is often dominated by stronger Broadway hands. Early Position requires a tighter opening range.",
                        hand + " performs much better from late position. Fold here.",
                        "Open a stronger range from Early Position. " + hand + " is too weak here.");

                case FoldTheme.DominatedKing:
                    return Pick(pick,
                        hand + " is often dominated by stronger kings. Early Position requires a tighter opening range.",
                        hand + " performs much better from late position. Fold here.",
                        "Open a stronger range from Early Position.");

                case FoldTheme.DominatedAce:
                    return Pick(pick,
                        hand + " is often dominated by stronger aces. Early Position requires a tighter opening range.",
                        hand + " has a weak kicker for Early Position. Fold here.",
                        "Open a stronger range from Early Position.");

                case FoldTheme.SmallPair:
                    return Pick(pick,
                        hand + " is too small to open from Early Position.",
                        hand + " plays better as a late-position set-mine. Fold here.",
                        "Early Position needs a tighter opening range than " + hand + ".");

                case FoldTheme.SpeculativeSuited:
                    return Pick(pick,
                        hand + " is too speculative to open from Early Position.",
                        hand + " has better implied odds from late position. Fold here.",
                        "Open a stronger range from Early Position.");

                case FoldTheme.WeakOffsuit:
                    return Pick(pick,
                        hand + " has poor postflop playability from Early Position.",
                        hand + " is often dominated and plays poorly multiway. Fold here.",
                        "Open a stronger range from Early Position.");

                default:
                    return Pick(pick,
                        hand + " is too weak for the Early Position opening range.",
                        "Open a stronger range from Early Position.",
                        hand + " performs much better from late position. Fold here.");
            }
        }

        private static string FormatMiddlePositionFold(string hand, FoldTheme theme, int pick, Rank hi)
        {
            switch (theme)
            {
                case FoldTheme.DominatedKing:
                    return Pick(pick,
                        hand + " is often dominated by stronger kings. It becomes more playable from the Cutoff or Button.",
                        "This hand is just below the Middle Position opening range.",
                        "Fold here and open it from a later seat.");

                case FoldTheme.DominatedBroadway:
                case FoldTheme.DominatedQueen:
                    return Pick(pick,
                        hand + " is often dominated by stronger Broadway hands. It becomes more playable from the Cutoff or Button.",
                        "This hand is just below the Middle Position opening range.",
                        "Fold here and open it from a later seat.");

                case FoldTheme.DominatedAce:
                    return Pick(pick,
                        hand + " is often dominated by stronger aces. Save it for a later seat.",
                        "This hand is just below the Middle Position opening range.",
                        "Fold here and open it from a later seat.");

                case FoldTheme.SmallPair:
                    return Pick(pick,
                        hand + " is just below the Middle Position opening range.",
                        hand + " is more profitable as a late-position set-mine. Fold here.",
                        "Fold here and open it from a later seat.");

                case FoldTheme.SpeculativeSuited:
                    return Pick(pick,
                        hand + " is more profitable from the Cutoff or Button.",
                        "This hand is just below the Middle Position opening range.",
                        "Fold here and open it from a later seat.");

                case FoldTheme.WeakOffsuit:
                    return Pick(pick,
                        hand + " is often dominated and too weak for Middle Position.",
                        "This hand is just below the Middle Position opening range.",
                        "Fold here and open it from a later seat.");

                default:
                    return Pick(pick,
                        "This hand is just below the Middle Position opening range.",
                        "Fold here and open it from a later seat.",
                        hand + " becomes more playable from the Cutoff or Button.");
            }
        }

        private static string FormatSmallBlindFold(
            string hand,
            FoldTheme theme,
            int pick,
            bool suited,
            Rank hi)
        {
            switch (theme)
            {
                case FoldTheme.DominatedKing:
                    return Pick(pick,
                        hand + " is dominated by stronger kings and plays poorly out of position.",
                        "Avoid entering the pot with a weak offsuit king from the Small Blind.",
                        hand + " has poor postflop playability out of position.");

                case FoldTheme.DominatedQueen:
                case FoldTheme.DominatedBroadway:
                    return Pick(pick,
                        hand + " is often dominated by stronger Broadway hands out of position.",
                        "Avoid opening weak Broadways from the Small Blind.",
                        hand + " has poor postflop playability out of position.");

                case FoldTheme.DominatedAce:
                    return Pick(pick,
                        hand + " is dominated by stronger aces and plays poorly out of position.",
                        "Avoid entering the pot with a weak ace from the Small Blind.",
                        hand + " has poor postflop playability out of position.");

                case FoldTheme.WeakOffsuit:
                    if (hi == Rank.King && !suited)
                    {
                        return Pick(pick,
                            hand + " is dominated by stronger kings and plays poorly out of position.",
                            "Avoid entering the pot with a weak offsuit king from the Small Blind.",
                            hand + " has poor postflop playability out of position.");
                    }

                    return Pick(pick,
                        hand + " has poor postflop playability out of position.",
                        "Avoid entering the pot with " + hand + " from the Small Blind.",
                        hand + " is too weak and dominated out of position.");

                case FoldTheme.SmallPair:
                    return Pick(pick,
                        hand + " plays poorly out of position from the Small Blind.",
                        "Set-mining is tougher out of position. Fold " + hand + " here.",
                        hand + " has poor postflop playability out of position.");

                case FoldTheme.SpeculativeSuited:
                    return Pick(pick,
                        hand + " has poor postflop playability out of position.",
                        "Avoid speculative suited hands from the Small Blind.",
                        hand + " is too weak to open from the Small Blind.");

                default:
                    return Pick(pick,
                        hand + " has poor postflop playability out of position.",
                        "Avoid entering the pot with " + hand + " from the Small Blind.",
                        hand + " is too weak for a Small Blind open.");
            }
        }

        private static string FormatCutoffFold(string hand, FoldTheme theme, int pick)
        {
            switch (theme)
            {
                case FoldTheme.DominatedAce:
                    return Pick(pick,
                        hand + " is often dominated by stronger aces. Too weak for a Cutoff open.",
                        hand + " is just below the Cutoff opening range.",
                        "Fold here; open a stronger ace from the Cutoff.");

                case FoldTheme.DominatedKing:
                case FoldTheme.DominatedQueen:
                case FoldTheme.DominatedBroadway:
                    return Pick(pick,
                        hand + " is often dominated by stronger Broadway hands.",
                        hand + " is just below the Cutoff opening range.",
                        "This hand is more profitable from the Button.");

                case FoldTheme.SmallPair:
                    return Pick(pick,
                        hand + " is just below the Cutoff opening range.",
                        hand + " is a thin set-mine from the Cutoff. Fold here.",
                        "Open a stronger range from the Cutoff.");

                default:
                    return Pick(pick,
                        hand + " is too weak for the Cutoff opening range.",
                        hand + " is more profitable from the Button.",
                        "Open a stronger range from the Cutoff.");
            }
        }

        private static string FormatButtonFold(string hand, FoldTheme theme, int pick)
        {
            // Rare unopened BTN folds (very weak hands).
            return Pick(pick,
                hand + " is too weak even for a Button open.",
                hand + " has poor postflop playability. Fold here.",
                hand + " is dominated and not worth opening from the Button.");
        }

        private static string FormatBigBlindFold(string hand, FoldTheme theme, int pick)
        {
            // Unopened BB fold is unusual; keep short and concrete.
            return Pick(pick,
                hand + " is too weak to continue.",
                hand + " has poor postflop playability.",
                hand + " is dominated in this spot.");
        }

        /// <summary>Stable 0..2 pick from hand + seat + theme (not random per frame).</summary>
        private static int StablePick(string hand, string seat, FoldTheme theme)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)theme;
                if (seat != null)
                {
                    for (int i = 0; i < seat.Length; i++)
                        hash = hash * 31 + seat[i];
                }

                if (hand != null)
                {
                    for (int i = 0; i < hand.Length; i++)
                        hash = hash * 31 + hand[i];
                }

                return (hash & 0x7fffffff) % 3;
            }
        }

        private static string Pick(int index, string a, string b, string c)
        {
            switch (index % 3)
            {
                case 0: return a;
                case 1: return b;
                default: return c;
            }
        }

        private static string FormatHandCode(bool isPair, Rank pairRank, bool suited, Rank hi, Rank lo)
        {
            if (isPair)
                return RankCode(pairRank) + RankCode(pairRank);

            string code = RankCode(hi) + RankCode(lo) + (suited ? "s" : "o");
            // Guard against failed parse (defaults can look like "22o").
            if (hi == Rank.Two && lo == Rank.Two && !isPair)
                return string.Empty;
            return code;
        }

        private static string RankCode(Rank rank) => rank switch
        {
            Rank.Ace   => "A",
            Rank.King  => "K",
            Rank.Queen => "Q",
            Rank.Jack  => "J",
            Rank.Ten   => "T",
            Rank.Nine  => "9",
            Rank.Eight => "8",
            Rank.Seven => "7",
            Rank.Six   => "6",
            Rank.Five  => "5",
            Rank.Four  => "4",
            Rank.Three => "3",
            Rank.Two   => "2",
            _          => "?",
        };

        private static string FormatUnopenedOpenReason(
            HumanTrainerAdvice advice,
            string seat,
            bool isPair,
            Rank pairRank,
            bool suited,
            Rank hi,
            Rank lo)
        {
            if (advice.PreflopHandGroup == PreflopHandGroup.Premium)
                return "Premium opening hand";

            if (isPair && IsSetMinePair(pairRank))
                return "Set-mine candidate";

            if (suited && hi == Rank.Ace && lo >= Rank.Five && lo <= Rank.Nine)
                return "Strong suited Ace";

            if (advice.PreflopHandGroup == PreflopHandGroup.Strong)
                return "Strong opening hand";

            if (seat == "BTN")
                return "Standard Button open";

            if (seat == "CO")
                return "Standard Cutoff open";

            if (seat == "SB")
                return "Standard SB open";

            return $"Standard {seat} open";
        }

        private static string FormatFacingRaiseReason(
            HumanTrainerAdvice advice,
            bool isPair,
            Rank pairRank)
        {
            string seat = FormatSeatShort(advice.Position);
            bool is3BetPlus = advice.StreetRaiseCount >= 1
                && (advice.RecommendedAction == BettingAction.Raise
                    || advice.RecommendedAction == BettingAction.AllIn);

            switch (advice.RecommendedAction)
            {
                case BettingAction.Raise:
                case BettingAction.AllIn:
                    if (advice.PreflopHandGroup == PreflopHandGroup.Premium)
                        return advice.StreetRaiseCount >= 2 ? "Premium 4-bet" : "Premium 3-bet";
                    if (advice.PreflopHandGroup == PreflopHandGroup.Strong)
                        return "Strong 3-bet";
                    return is3BetPlus ? "Value re-raise" : "Raise for value";

                case BettingAction.Call:
                    if (seat == "BB")
                        return "Defend Big Blind";
                    if (isPair && IsSetMinePair(pairRank))
                        return "Small pair for set mining";
                    if (advice.PotOddsPercent > 0f)
                        return "Call with pot odds";
                    return "Call with pot odds";

                case BettingAction.Check:
                    if (seat == "BB")
                        return "Defend Big Blind";
                    return "No raise needed";

                default:
                    if (advice.PreflopHandGroup == PreflopHandGroup.Weak)
                        return "Too weak to defend";
                    // Opener seat unknown on snapshot; keep concept-focused.
                    return "Fold versus raise";
            }
        }

        private static string FormatLimpedReason(
            HumanTrainerAdvice advice,
            bool isPair,
            Rank pairRank,
            bool suited,
            Rank hi,
            Rank lo)
        {
            string seat = FormatSeatShort(advice.Position);

            switch (advice.RecommendedAction)
            {
                case BettingAction.Raise:
                case BettingAction.AllIn:
                    if (advice.PreflopHandGroup == PreflopHandGroup.Premium)
                        return "Premium opening hand";
                    if (advice.PreflopHandGroup == PreflopHandGroup.Strong)
                        return "Strong opening hand";
                    return $"Standard {seat} open";

                case BettingAction.Call:
                    if (isPair && IsSetMinePair(pairRank))
                        return "Small pair for set mining";
                    return "Call with pot odds";

                case BettingAction.Check:
                    if (seat == "BB")
                        return "Defend Big Blind";
                    return "No raise needed";

                default:
                    return FormatUnopenedFoldReason(advice, isPair, pairRank, suited, hi, lo);
            }
        }

        private static bool IsSetMinePair(Rank rank) =>
            rank == Rank.Seven || rank == Rank.Six || rank == Rank.Five
            || rank == Rank.Four || rank == Rank.Three || rank == Rank.Two;

        private static string FormatSeatShort(string position)
        {
            if (string.IsNullOrEmpty(position))
                return "EP";

            switch (position)
            {
                case "BTN":
                case "Button":
                    return "BTN";
                case "SB":
                    return "SB";
                case "BB":
                    return "BB";
                case "EP":
                case "Early":
                    return "EP";
                case "MP":
                case "Middle":
                    return "MP";
                case "CO":
                case "Cutoff":
                    return "CO";
                default:
                    return position;
            }
        }

        /// <summary>
        /// Parse trainer hole-card display ("A♥ 8♥") for coaching tags only.
        /// </summary>
        private static bool TryParseHoleShape(
            string holeCards,
            out bool isPair,
            out Rank pairRank,
            out bool suited,
            out Rank hi,
            out Rank lo)
        {
            isPair = false;
            pairRank = Rank.Two;
            suited = false;
            hi = Rank.Two;
            lo = Rank.Two;

            if (string.IsNullOrEmpty(holeCards))
                return false;

            string[] parts = holeCards.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return false;

            if (!TryParseCardToken(parts[0], out Rank r0, out char s0)
                || !TryParseCardToken(parts[1], out Rank r1, out char s1))
                return false;

            suited = s0 == s1;
            hi = r0 >= r1 ? r0 : r1;
            lo = r0 >= r1 ? r1 : r0;
            isPair = r0 == r1;
            if (isPair)
                pairRank = r0;
            return true;
        }

        private static bool TryParseCardToken(string token, out Rank rank, out char suit)
        {
            rank = Rank.Two;
            suit = '\0';
            if (string.IsNullOrEmpty(token) || token.Length < 2)
                return false;

            suit = token[token.Length - 1];
            string rankPart = token.Substring(0, token.Length - 1);
            switch (rankPart)
            {
                case "A":  rank = Rank.Ace; return true;
                case "K":  rank = Rank.King; return true;
                case "Q":  rank = Rank.Queen; return true;
                case "J":  rank = Rank.Jack; return true;
                case "10": rank = Rank.Ten; return true;
                case "9":  rank = Rank.Nine; return true;
                case "8":  rank = Rank.Eight; return true;
                case "7":  rank = Rank.Seven; return true;
                case "6":  rank = Rank.Six; return true;
                case "5":  rank = Rank.Five; return true;
                case "4":  rank = Rank.Four; return true;
                case "3":  rank = Rank.Three; return true;
                case "2":  rank = Rank.Two; return true;
                default:   return false;
            }
        }
    }
}
