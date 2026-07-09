using System.Collections.Generic;

namespace TexasHoldem
{
    /// <summary>Human-readable labels for evaluated poker hands.</summary>
    public static class HandDisplayNames
    {
        /// <summary>
        /// True when a non-winning showdown player tied the winner on the named part of the hand
        /// (e.g. same pair rank), so kicker or high-card detail actually decided the pot.
        /// </summary>
        public static bool WasTiebreakerDecisive(
            HandResult winner,
            IReadOnlyList<PlayerState> winners,
            IReadOnlyList<(PlayerState Player, HandResult Result)> showdownHands)
        {
            if (winner == null || showdownHands == null || showdownHands.Count == 0)
                return false;

            int decisivePrefix = DecisiveTiebreakerPrefixLength(winner.Rank);
            if (decisivePrefix <= 0)
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
                if (result.Rank != winner.Rank)
                    continue;
                if (TiebreakersMatchPrefix(winner.Tiebreakers, result.Tiebreakers, decisivePrefix))
                    return true;
            }

            return false;
        }

        /// <summary>Tiebreaker slots compared before kicker/high detail is shown.</summary>
        private static int DecisiveTiebreakerPrefixLength(HandRank rank) => rank switch
        {
            HandRank.OnePair       => 1,
            HandRank.TwoPair       => 2,
            HandRank.ThreeOfAKind  => 1,
            HandRank.FourOfAKind   => 1,
            HandRank.Straight      => 1,
            HandRank.StraightFlush => 1,
            HandRank.Flush         => 1,
            HandRank.HighCard      => 1,
            _                      => 0,
        };

        private static bool TiebreakersMatchPrefix(
            IReadOnlyList<int> a, IReadOnlyList<int> b, int length)
        {
            if (a == null || b == null)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (i >= a.Count || i >= b.Count || a[i] != b[i])
                    return false;
            }

            return true;
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
                    ? $"Four of a Kind, {RankName(tb[0])}s{KickersLabel(tb, 1, 1)}"
                    : $"Four of a Kind, {RankName(tb[0])}s",
                HandRank.FullHouse => $"Full House, {RankName(tb[0])}s over {RankName(tb[1])}s",
                HandRank.Flush => tiebreakerDecisive
                    ? $"Flush, {RankName(tb[0])} high"
                    : "Flush",
                HandRank.Straight => tiebreakerDecisive
                    ? $"Straight, {RankName(tb[0])} high"
                    : "Straight",
                HandRank.ThreeOfAKind => tiebreakerDecisive
                    ? $"Three of a Kind, {RankName(tb[0])}s{KickersLabel(tb, 1, 2)}"
                    : $"Three of a Kind, {RankName(tb[0])}s",
                HandRank.TwoPair => tiebreakerDecisive
                    ? $"Two Pair, {RankName(tb[0])}s and {RankName(tb[1])}s{KickersLabel(tb, 2, 1)}"
                    : $"Two Pair, {RankName(tb[0])}s and {RankName(tb[1])}s",
                HandRank.OnePair => tiebreakerDecisive
                    ? $"Pair of {RankName(tb[0])}s{KickersLabel(tb, 1, 3)}"
                    : $"Pair of {RankName(tb[0])}s",
                HandRank.HighCard => tiebreakerDecisive
                    ? $"{FormatRankList(tb, 0, 5)} High"
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

        public static string ShortRankName(int rankValue)
        {
            if (!System.Enum.IsDefined(typeof(Rank), rankValue))
                return rankValue.ToString();

            return ((Rank)rankValue) switch
            {
                Rank.Ace   => "A",
                Rank.King  => "K",
                Rank.Queen => "Q",
                Rank.Jack  => "J",
                Rank.Ten   => "10",
                _          => rankValue.ToString()
            };
        }

        private static string FormatRankList(IReadOnlyList<int> tiebreakers, int startIndex, int count)
        {
            if (tiebreakers == null || count <= 0)
                return string.Empty;

            var parts = new List<string>(count);
            for (int i = startIndex; i < startIndex + count && i < tiebreakers.Count; i++)
                parts.Add(ShortRankName(tiebreakers[i]));

            return parts.Count > 0 ? string.Join("-", parts) : string.Empty;
        }

        private static string KickersLabel(IReadOnlyList<int> tiebreakers, int startIndex, int count)
        {
            string ranks = FormatRankList(tiebreakers, startIndex, count);
            if (string.IsNullOrEmpty(ranks))
                return string.Empty;

            string suffix = count > 1 ? "Kickers" : "Kicker";
            return $", {ranks} {suffix}";
        }
    }
}
