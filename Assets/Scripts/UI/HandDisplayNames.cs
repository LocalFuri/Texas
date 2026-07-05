namespace TexasHoldem
{
    /// <summary>Human-readable labels for evaluated poker hands.</summary>
    public static class HandDisplayNames
    {
        public static string Format(HandResult result)
        {
            if (result == null)
                return string.Empty;

            var tb = result.Tiebreakers;
            return result.Rank switch
            {
                HandRank.RoyalFlush    => "Royal Flush",
                HandRank.StraightFlush => $"Straight Flush, {RankName(tb[0])} high",
                HandRank.FourOfAKind   => $"Four of a Kind, {RankName(tb[0])}s",
                HandRank.FullHouse     => $"Full House, {RankName(tb[0])}s over {RankName(tb[1])}s",
                HandRank.Flush         => $"Flush, {RankName(tb[0])} high",
                HandRank.Straight      => $"Straight, {RankName(tb[0])} high",
                HandRank.ThreeOfAKind  => $"Three of a Kind, {RankName(tb[0])}s",
                HandRank.TwoPair       => $"Two Pair, {RankName(tb[0])}s and {RankName(tb[1])}s",
                HandRank.OnePair       => $"Pair of {RankName(tb[0])}s",
                HandRank.HighCard      => $"{RankName(tb[0])} High",
                _                      => result.Rank.ToString()
            };
        }

        public static string FormatWithWinner(string playerName, HandResult result)
        {
            string hand = Format(result);
            if (string.IsNullOrEmpty(hand))
                return playerName;

            return string.IsNullOrEmpty(playerName)
                ? hand
                : $"{playerName} — {hand}";
        }

        public static string RankName(int rankValue)
        {
            if (!System.Enum.IsDefined(typeof(Rank), rankValue))
                return rankValue.ToString();

            return ((Rank)rankValue) switch
            {
                Rank.Ace   => "Ace",
                Rank.King  => "King",
                Rank.Queen => "Queen",
                Rank.Jack  => "Jack",
                Rank.Ten   => "Ten",
                _          => rankValue.ToString()
            };
        }
    }
}
