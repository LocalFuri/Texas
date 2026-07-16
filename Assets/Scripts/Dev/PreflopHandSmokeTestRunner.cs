using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Play Mode / Dev smoke test: runs automated preflop hands through production
    /// <see cref="AIController"/> + <see cref="BettingManager"/> without changing AI logic.
    /// </summary>
    public sealed class PreflopHandSmokeTestRunner : MonoBehaviour
    {
        private const int DefaultHandCount = 10_000;
        private const int PlayerCount      = 6;
        private const int StartingChips    = 1000;
        private const int SmallBlind       = 10;
        private const int BigBlind         = 20;

        [SerializeField] private int _handCount = DefaultHandCount;

        [ContextMenu("Run Preflop Hand Smoke Test")]
        private void RunFromContextMenu() => RunAllTests(_handCount);

        /// <summary>
        /// Runs <paramref name="handCount"/> production-AI preflop hands.
        /// Returns (ok, stats). ok is false if any exception or illegal action occurred.
        /// </summary>
        public static (bool ok, SmokeStats stats) RunAllTests(int handCount = DefaultHandCount)
        {
            if (handCount < 1)
                handCount = DefaultHandCount;

            var stats = new SmokeStats { HandsTarget = handCount };

            Debug.Log($"[PreflopSmoke] Running {handCount} automated preflop hand(s)...");

            for (int hand = 0; hand < handCount; hand++)
            {
                stats.HandsPlayed++;

                try
                {
                    string failure = RunOneHand(hand, stats);
                    if (failure != null)
                    {
                        stats.IllegalActions++;
                        stats.LastError = $"hand={hand}: {failure}";
                        Debug.LogError($"[PreflopSmoke] Illegal action — {stats.LastError}");
                        continue;
                    }

                    stats.BettingRoundsCompleted++;
                }
                catch (Exception ex)
                {
                    stats.Exceptions++;
                    stats.LastError = $"hand={hand}: {ex.GetType().Name}: {ex.Message}";
                    Debug.LogError($"[PreflopSmoke] Exception — {stats.LastError}");
                }
            }

            PrintSummary(stats);

            bool ok = stats.Exceptions == 0 && stats.IllegalActions == 0;
            if (!ok)
                Debug.LogError("[PreflopSmoke] FAIL — exceptions or illegal actions detected.");
            else
                Debug.Log("[PreflopSmoke] PASS");

            return (ok, stats);
        }

        public static void PrintSummary(SmokeStats stats)
        {
            Debug.Log(
                "[PreflopSmoke] Summary\n" +
                $"  Hands played:              {stats.HandsPlayed}\n" +
                $"  Betting rounds completed:  {stats.BettingRoundsCompleted}\n" +
                $"  Exceptions:                {stats.Exceptions}\n" +
                $"  Illegal actions:           {stats.IllegalActions}\n" +
                $"  Actions Fold:              {stats.Folds}\n" +
                $"  Actions Call:              {stats.Calls}\n" +
                $"  Actions Raise:             {stats.Raises}");
        }

        /// <summary>Null on success; failure message otherwise.</summary>
        private static string RunOneHand(int handIndex, SmokeStats stats)
        {
            var players = new List<PlayerState>(PlayerCount);
            for (int i = 0; i < PlayerCount; i++)
                players.Add(new PlayerState($"SmokeBot{i}", PlayerType.AI, StartingChips));

            int dealer = handIndex % PlayerCount;
            int sbIndex = (dealer + 1) % PlayerCount;
            int bbIndex = (dealer + 2) % PlayerCount;
            int utgIndex = (bbIndex + 1) % PlayerCount;

            var betting = new BettingManager(SmallBlind, BigBlind);
            var board = new BoardManager();
            var ai = new AIController();

            betting.ResetRound();
            board.NewDeck();
            ai.ClearHandState();

            board.DealHoleCards(players, sbIndex);
            betting.PostSmallBlind(players[sbIndex]);
            betting.PostBigBlind(players[bbIndex]);

            return RunPreflopBettingRound(players, utgIndex, dealer, betting, board, ai, stats);
        }

        private static string RunPreflopBettingRound(
            List<PlayerState> players,
            int startIndex,
            int dealerIndex,
            BettingManager betting,
            BoardManager board,
            AIController ai,
            SmokeStats stats)
        {
            int n = players.Count;
            var hasActed = new bool[n];
            for (int i = 0; i < n; i++)
                hasActed[i] = players[i].HasFolded || players[i].IsAllIn;

            if (!AnyPlayerMustAct(players, hasActed))
                return null;

            int seatIndex = startIndex % n;
            int safetyLimit = n * n * 4;
            int iterations = 0;

            while (iterations++ < safetyLimit)
            {
                if (CountNonFolded(players) <= 1)
                    return null;

                if (IsBettingComplete(players, hasActed))
                    return null;

                int currentIndex = seatIndex % n;
                PlayerState player = players[currentIndex];

                if (player.HasFolded || player.IsAllIn)
                {
                    seatIndex++;
                    continue;
                }

                if (hasActed[currentIndex])
                {
                    seatIndex++;
                    continue;
                }

                if (player.HasFolded || player.IsAllIn)
                    return $"{player.Name} acted while folded/all-in";

                int playersBehind = 0;
                int callersBefore = 0;
                for (int i = 0; i < n; i++)
                {
                    if (i == currentIndex)
                        continue;
                    if (players[i].HasFolded || players[i].IsAllIn)
                        continue;
                    if (!hasActed[i])
                        playersBehind++;
                    else if (players[i].CurrentBet == betting.CurrentBet)
                        callersBefore++;
                }

                PlayerState shover = betting.LastAggressor;
                if (shover == null || shover == player || shover.HasFolded)
                {
                    shover = null;
                    int tableBet = betting.CurrentBet;
                    if (tableBet > 0)
                    {
                        foreach (PlayerState p in players)
                        {
                            if (p == null || p == player || p.HasFolded)
                                continue;
                            if (p.CurrentBet == tableBet)
                            {
                                shover = p;
                                break;
                            }
                        }
                    }
                }

                PreflopSeatBucket shovePosition = shover != null
                    ? PreflopStrategy.ResolveSeatBucket(players, dealerIndex, shover)
                    : PreflopSeatBucket.Button;

                PreflopSeatBucket seat = PreflopStrategy.ResolveSeatBucket(players, dealerIndex, player);

                int betBefore = betting.CurrentBet;
                int minRaiseBefore = betting.GetMinRaiseIncrement();
                int callAmount = betting.GetCallAmount(player);

                var (action, raise) = ai.DecideAction(
                    player,
                    board.CommunityCards,
                    betting,
                    players,
                    GamePhase.PreFlop,
                    betting.Pot,
                    betting.CurrentBet,
                    BigBlind,
                    betting.StreetRaiseCount,
                    seat,
                    testMode: false,
                    playersBehind,
                    shovePosition,
                    callersBefore);

                if (action == BettingAction.Call && callAmount > player.Chips)
                {
                    return $"{player.Name} Call callAmount={callAmount} > chips={player.Chips}";
                }

                if (!betting.ProcessAction(player, action, raise))
                {
                    return $"{player.Name} illegal action rejected: {action} raise={raise} " +
                           $"call={callAmount} chips={player.Chips} table={betting.CurrentBet}";
                }

                CountAction(stats, action, callAmount);

                hasActed[currentIndex] = true;

                if (betting.CurrentBet - betBefore >= minRaiseBefore)
                    ReopenActionForOthers(players, hasActed, currentIndex);

                seatIndex++;

                if (IsBettingComplete(players, hasActed))
                    return null;
            }

            return "betting round failed to complete (safety limit)";
        }

        private static void CountAction(SmokeStats stats, BettingAction action, int callAmount)
        {
            switch (action)
            {
                case BettingAction.Fold:
                    stats.Folds++;
                    break;
                case BettingAction.Call:
                    stats.Calls++;
                    break;
                case BettingAction.Raise:
                    stats.Raises++;
                    break;
                case BettingAction.AllIn:
                    // Jam into a bet counts as Call; open/overbet jam counts as Raise.
                    if (callAmount > 0)
                        stats.Calls++;
                    else
                        stats.Raises++;
                    break;
                // Check is legal but not part of Fold/Call/Raise summary.
            }
        }

        private static int CountNonFolded(IReadOnlyList<PlayerState> players)
        {
            int count = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].HasFolded)
                    count++;
            }

            return count;
        }

        private static bool IsBettingComplete(IReadOnlyList<PlayerState> players, bool[] hasActed)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].HasFolded || players[i].IsAllIn)
                    continue;
                if (!hasActed[i])
                    return false;
            }

            return true;
        }

        private static bool AnyPlayerMustAct(IReadOnlyList<PlayerState> players, bool[] hasActed)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].HasFolded || players[i].IsAllIn)
                    continue;
                if (!hasActed[i])
                    return true;
            }

            return false;
        }

        private static void ReopenActionForOthers(
            IReadOnlyList<PlayerState> players, bool[] hasActed, int aggressorIndex)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (i == aggressorIndex)
                    continue;
                if (players[i].HasFolded || players[i].IsAllIn)
                    continue;
                hasActed[i] = false;
            }
        }

        public sealed class SmokeStats
        {
            public int HandsTarget;
            public int HandsPlayed;
            public int BettingRoundsCompleted;
            public int Exceptions;
            public int IllegalActions;
            public int Folds;
            public int Calls;
            public int Raises;
            public string LastError;
        }
    }
}
