using System.Collections.Generic;

namespace TexasHoldem
{
    public enum PlayerType { Human, AI }

    public class PlayerState
    {
        public string     Name       { get; }
        public PlayerType Type       { get; }
        public int        Chips      { get; set; }
        public List<Card> HoleCards  { get; } = new List<Card>();
        public int        CurrentBet { get; set; }
        public bool       HasFolded  { get; set; }
        public bool       IsAllIn    { get; set; }

        public bool IsActive => !HasFolded && !IsAllIn && Chips > 0;

        public PlayerState(string name, PlayerType type, int startingChips)
        {
            Name  = name;
            Type  = type;
            Chips = startingChips;
        }

        /// <summary>Resets per-round state while preserving the chip count.</summary>
        public void ResetForNewRound()
        {
            HoleCards.Clear();
            CurrentBet = 0;
            HasFolded  = false;
            IsAllIn    = false;
        }
    }
}
