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
    }
}
