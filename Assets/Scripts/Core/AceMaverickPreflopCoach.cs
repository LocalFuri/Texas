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
                return FormatLimpedReason(advice, isPair, pairRank);

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
                    return $"Below {seat} opening range";
            }
        }

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
            Rank pairRank)
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
                    return $"Below {seat} opening range";
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
