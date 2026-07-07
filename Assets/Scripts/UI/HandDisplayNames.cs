using System.Collections.Generic;

namespace TexasHoldem
{
    /// <summary>Human-readable labels for evaluated poker hands.</summary>
    public static class HandDisplayNames
    {
        /// <summary>
        /// True when a non-winning showdown player had the same hand rank, so kickers or high-card detail decided the pot.
        /// </summary>
        public static bool WasTiebreakerDecisive(
            HandResult winner,
            IReadOnlyList<PlayerState> winners,
            IReadOnlyList<(PlayerState Player, HandResult Result)> showdownHands)
        {
            if (winner == null || showdownHands == null || showdownHands.Count == 0)
                return false;

            var winnerSet = new HashSet<PlayerState>();
            if (winners != null)
            {
                foreach (PlayerState player in winners)
                {
                    if (player != null)
                        winnerSet.Add(player);
                }
            }

            foreach ((PlayerState player, HandResult result) in showdownHands)
            {
                if (player == null || result == null)
                    continue;
                if (winnerSet.Contains(player))
                    continue;
                if (result.Rank == winner.Rank)
                    return true;
            }

            return false;
        }

        public static string Format(HandResult result, bool tiebreakerDecisive = true)
        {
            if (result == null)
                return string.Empty;

            var tb = result.Tiebreakers;
            return result.Rank switch
            {
                HandRank.RoyalFlush => "Royal Flush",
                HandRank.StraightFlush => tiebreakerDecisive
                    ? $"Straight Flush, {RankName(tb[0])} high"
                    : "Straight Flush",
                HandRank.FourOfAKind => tiebreakerDecisive
                    ? $"Four of a Kind, {RankName(tb[0])}s{TopKickerLabel(tb, 1)}"
                    : $"Four of a Kind, {RankName(tb[0])}s",
                HandRank.FullHouse => $"Full House, {RankName(tb[0])}s over {RankName(tb[1])}s",
                HandRank.Flush => tiebreakerDecisive
                    ? $"Flush, {RankName(tb[0])} high"
                    : "Flush",
                HandRank.Straight => tiebreakerDecisive
                    ? $"Straight, {RankName(tb[0])} high"
                    : "Straight",
                HandRank.ThreeOfAKind => tiebreakerDecisive
                    ? $"Three of a Kind, {RankName(tb[0])}s{TopKickerLabel(tb, 1)}"
                    : $"Three of a Kind, {RankName(tb[0])}s",
                HandRank.TwoPair => tiebreakerDecisive
                    ? $"Two Pair, {RankName(tb[0])}s and {RankName(tb[1])}s{TopKickerLabel(tb, 2)}"
                    : $"Two Pair, {RankName(tb[0])}s and {RankName(tb[1])}s",
                HandRank.OnePair => tiebreakerDecisive
                    ? $"Pair of {RankName(tb[0])}s{TopKickerLabel(tb, 1)}"
                    : $"Pair of {RankName(tb[0])}s",
                HandRank.HighCard => tiebreakerDecisive
                    ? $"{RankName(tb[0])} High"
                    : "High Card",
                _ => result.Rank.ToString()
            };
        }

        public static string FormatWithWinner(string playerName, HandResult result, bool tiebreakerDecisive = true)
        {
            string hand = Format(result, tiebreakerDecisive);
            if (string.IsNullOrEmpty(hand))
                return playerName ?? string.Empty;

            if (string.IsNullOrEmpty(playerName))
                return hand;

            return $"{playerName} wins — {hand}";
        }

        public static string FormatWithWinners(
            IReadOnlyList<string> playerNames,
            HandResult result,
            bool tiebreakerDecisive = true)
        {
            if (playerNames == null || playerNames.Count == 0)
                return Format(result, tiebreakerDecisive);

            string hand = Format(result, tiebreakerDecisive);
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
