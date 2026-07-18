namespace TexasHoldem
{
    /// <summary>
    /// Single per-turn trainer recommendation snapshot for HUD, AI Coach, and AI Review.
    /// Built once; consumers must not re-run <see cref="BettingAdvisor.Recommend"/>.
    /// </summary>
    public sealed class HumanTrainerAdvice
    {
        public int TurnId { get; set; }

        public int EquityPercent { get; set; }
        public float PotOddsPercent { get; set; }
        public string Position { get; set; }
        public string BoardTexture { get; set; }
        public string Street { get; set; }
        public PreflopHandGroup PreflopHandGroup { get; set; }

        public BettingAdvice Advice { get; set; }
        public string AdviceLabel { get; set; }

        public BettingAction RecommendedAction { get; set; }
        /// <summary>Raise increment above table bet (BettingManager meaning).</summary>
        public int RecommendedRaiseIncrement { get; set; }
        /// <summary>Table bet after a recommended raise/bet, when applicable.</summary>
        public int RecommendedTotalBet { get; set; }

        public int AmountToCall { get; set; }
        public int CurrentBet { get; set; }
        public int PotBeforeAction { get; set; }

        /// <summary>Fold / Check / Call / Bet / Raise / All-In.</summary>
        public string DecisionLabel { get; set; }

        /// <summary>Short explanation from existing trainer evaluation inputs only.</summary>
        public string Explanation { get; set; }
    }
}
