namespace TexasHoldem.Dev
{
    /// <summary>
    /// Fixed decision spot for <see cref="StrategyValidationRunner"/>.
    /// Framework only — no predefined scenarios ship with this type.
    /// </summary>
    public sealed class StrategyValidationScenario
    {
        /// <summary>Optional label printed in the summary.</summary>
        public string Name;

        public Suit HoleSuit0;
        public Rank HoleRank0;
        public Suit HoleSuit1;
        public Rank HoleRank1;

        public PreflopSeatBucket HeroPosition = PreflopSeatBucket.Button;

        public int HeroStack = 1000;
        public int OpponentStack = 1000;

        public int SmallBlind = 10;
        public int BigBlind = 20;

        public int PlayerCount = 6;

        /// <summary>Forced hero action on the first preflop decision.</summary>
        public BettingAction HeroAction = BettingAction.Fold;

        /// <summary>
        /// Raise increment above the current table bet (same meaning as
        /// <see cref="BettingManager.ProcessAction"/> raiseAmount). Ignored unless
        /// <see cref="HeroAction"/> is <see cref="BettingAction.Raise"/>.
        /// Example: table bet 2, raise to total 5 → RaiseIncrement = 3.
        /// </summary>
        public int RaiseIncrement;

        /// <summary>
        /// When set, each hand i seeds <c>UnityEngine.Random</c> with
        /// <c>BaseSeed + i</c> before the hand starts. Null keeps existing RNG behavior.
        /// </summary>
        public int? BaseSeed;

        public string Validate()
        {
            if (PlayerCount < 2 || PlayerCount > 10)
                return $"PlayerCount must be 2–10 (was {PlayerCount}).";
            if (HeroStack <= 0)
                return "HeroStack must be > 0.";
            if (OpponentStack <= 0)
                return "OpponentStack must be > 0.";
            if (SmallBlind <= 0 || BigBlind <= 0)
                return "Blind levels must be > 0.";
            if (BigBlind < SmallBlind)
                return "BigBlind must be >= SmallBlind.";
            if (HoleSuit0 == HoleSuit1 && HoleRank0 == HoleRank1)
                return "Hero hole cards must be distinct.";

            switch (HeroAction)
            {
                case BettingAction.Fold:
                case BettingAction.Call:
                case BettingAction.AllIn:
                    break;
                case BettingAction.Raise:
                    if (RaiseIncrement <= 0)
                        return "RaiseIncrement must be > 0 when HeroAction is Raise.";
                    break;
                default:
                    return $"HeroAction {HeroAction} is not supported (use Fold, Call, Raise, AllIn).";
            }

            return null;
        }
    }
}
