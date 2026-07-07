using System.Collections.Generic;

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
                HandRank.FourOfAKind   => $"Four of a Kind, {RankName(tb[0])}s{TopKickerLabel(tb, 1)}",
                HandRank.FullHouse     => $"Full House, {RankName(tb[0])}s over {RankName(tb[1])}s",
                HandRank.Flush         => $"Flush, {RankName(tb[0])} high",
                HandRank.Straight      => $"Straight, {RankName(tb[0])} high",
                HandRank.ThreeOfAKind  => $"Three of a Kind, {RankName(tb[0])}s{TopKickerLabel(tb, 1)}",
                HandRank.TwoPair       => $"Two Pair, {RankName(tb[0])}s and {RankName(tb[1])}s{TopKickerLabel(tb, 2)}",
                HandRank.OnePair       => $"Pair of {RankName(tb[0])}s{TopKickerLabel(tb, 1)}",
                HandRank.HighCard      => $"{RankName(tb[0])} High",
                _                      => result.Rank.ToString()
            };
        }

        public static string FormatWithWinner(string playerName, HandResult result)
        {
            string hand = Format(result);
            if (string.IsNullOrEmpty(hand))
                return playerName ?? string.Empty;

            if (string.IsNullOrEmpty(playerName))
                return hand;

            return $"{playerName} wins — {hand}";
        }

        public static string FormatWithWinners(IReadOnlyList<string> playerNames, HandResult result)
        {
            if (playerNames == null || playerNames.Count == 0)
                return Format(result);

            string hand = Format(result);
            string names = playerNames.Count == 1
                ? playerNames[0]
                : string.Join(" & ", playerNames);

            if (string.IsNullOrEmpty(hand))
                return names;

            string verb = playerNames.Count == 1 ? "wins" : "win";
            return $"{names} {verb} — {hand}";
        }

        public static string FormatFoldWin(IReadOnlyList<string> playerNames)
        {
            if (playerNames == null || playerNames.Count == 0)
                return string.Empty;

            if (playerNames.Count == 1)
                return $"{playerNames[0]} wins";

            return $"{string.Join(" & ", playerNames)} win";
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

        private static string TopKickerLabel(IReadOnlyList<int> tiebreakers, int kickerIndex)
        {
            if (tiebreakers == null || tiebreakers.Count <= kickerIndex)
                return string.Empty;

            return $", {RankName(tiebreakers[kickerIndex])} Kicker";
        }
    }
}
