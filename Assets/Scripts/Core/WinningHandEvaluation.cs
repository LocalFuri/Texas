using System.Collections.Generic;

namespace TexasHoldem
{
    /// <summary>Best 5-card hand chosen from a player's hole cards plus the board.</summary>
    public sealed class WinningHandEvaluation
    {
        public HandResult           Result    { get; }
        public IReadOnlyList<Card> BestCards { get; }

        public WinningHandEvaluation(HandResult result, List<Card> bestCards)
        {
            Result    = result;
            BestCards = bestCards;
        }

        public static bool ContainsCard(IReadOnlyList<Card> winningCards, Card card)
        {
            if (card == null || winningCards == null)
                return false;

            foreach (Card winning in winningCards)
            {
                if (winning != null && winning.Suit == card.Suit && winning.Rank == card.Rank)
                    return true;
            }

            return false;
        }
    }
}
