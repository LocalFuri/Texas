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
        private const int DefaultHandCount = 100;
        private const int PlayerCount      = 6;
        private const int StartingChips    = 1000;
        private const int SmallBlind       = 10;
        private const int BigBlind         = 20;

        [SerializeField] private int _handCount = DefaultHandCount;

        [ContextMenu("Run Preflop Hand Smoke Test")]
        private void RunFromContextMenu() => RunAllTests(_handCount);

        /// <summary>Returns (handsPassed, handCount). Logs first failure and aborts remaining hands.</summary>
        public static (int passed, int total) RunAllTests(int handCount = DefaultHandCount)
        {
            if (handCount < 1)
                handCount = DefaultHandCount;

            Debug.Log($"[PreflopSmoke] Running {handCount} automated preflop hand(s)...");

            int passed = 0;
            for (int hand = 0; hand < handCount; hand++)
            {
                try
                {
                    string failure = RunOneHand(hand);
                    if (failure != null)
                    {
                        Debug.LogError($"[PreflopSmoke] FAIL hand={hand}: {failure}");
                        Debug.Log($"[PreflopSmoke] Complete: {passed}/{handCount} passed (stopped on failure).");
                        return (passed, handCount);
                    }

                    passed++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PreflopSmoke] FAIL hand={hand}: exception — {ex}");
                    Debug.Log($"[PreflopSmoke] Complete: {passed}/{handCount} passed (stopped on exception).");
                    return (passed, handCount);
                }
            }

            Debug.Log($"[PreflopSmoke] Complete: {passed}/{handCount} passed.");
            return (passed, handCount);
        }

        /// <summary>Null on success; failure message otherwise.</summary>
        private static string RunOneHand(int handIndex)
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

            return RunPreflopBettingRound(players, utgIndex, dealer, betting, board, ai);
        }

        private static string RunPreflopBettingRound(
            List<PlayerState> players,
            int startIndex,
            int dealerIndex,
            BettingManager betting,
            BoardManager board,
            AIController ai)
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

                // Belt-and-suspenders: never decide for folded / all-in.
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

                hasActed[currentIndex] = true;

                if (betting.CurrentBet - betBefore >= minRaiseBefore)
                    ReopenActionForOthers(players, hasActed, currentIndex);

                seatIndex++;

                if (IsBettingComplete(players, hasActed))
                    return null;
            }

            return "betting round failed to complete (safety limit)";
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
    }
}
