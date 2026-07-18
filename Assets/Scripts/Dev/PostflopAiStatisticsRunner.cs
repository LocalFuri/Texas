using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Statistics-only: plays complete production-AI hands and aggregates postflop decisions.
    /// Does not change AI logic or normal gameplay code.
    /// </summary>
    public sealed class PostflopAiStatisticsRunner : MonoBehaviour
    {
        private const int DefaultHandCount = 10_000;
        private const int PlayerCount      = 6;
        private const int StartingChips    = 1000;
        private const int SmallBlind       = 10;
        private const int BigBlind         = 20;

        [SerializeField] private int _handCount = DefaultHandCount;

        [ContextMenu("Run Postflop AI Statistics")]
        private void RunFromContextMenu() => RunAll(_handCount);

        /// <param name="handCount">Number of complete hands to simulate.</param>
        /// <param name="onHandFinished">
        /// Optional progress callback after each hand (completed or failed).
        /// Args: (handNumber1Based, totalHands). Editor menus may drive a progress bar here.
        /// </param>
        public static StatsResult RunAll(
            int handCount = DefaultHandCount,
            Action<int, int> onHandFinished = null)
        {
            if (handCount < 1)
                handCount = DefaultHandCount;

            var stats = new Stats();
            var sw = Stopwatch.StartNew();

            bool prevLogEnabled = Debug.unityLogger.logEnabled;
            LogType prevFilter = Debug.unityLogger.filterLogType;

            Debug.Log($"[PostflopStats] Running {handCount} complete production-AI hand(s)...");

            // Suppress verbose Debug.Log (AI / HandActionLog); keep Warning/Error for progress + failures.
            Debug.unityLogger.filterLogType = LogType.Warning;

            try
            {
                for (int hand = 0; hand < handCount; hand++)
                {
                    stats.HandsAttempted++;

                    try
                    {
                        string failure = RunOneHand(hand, stats);
                        if (failure != null)
                        {
                            stats.IllegalActions++;
                            stats.LastError = $"hand={hand}: {failure}";
                            Debug.LogError($"[PostflopStats] Illegal — {stats.LastError}");
                        }
                        else
                        {
                            stats.HandsCompleted++;
                            if (stats.HandsCompleted % 100 == 0)
                            {
                                Debug.LogWarning(
                                    $"[PostflopStats] Progress: {stats.HandsCompleted}/{handCount} hands completed");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        stats.Exceptions++;
                        stats.LastError = $"hand={hand}: {ex.GetType().Name}: {ex.Message}";
                        Debug.LogError($"[PostflopStats] Exception — {stats.LastError}");
                    }

                    onHandFinished?.Invoke(hand + 1, handCount);
                }
            }
            finally
            {
                Debug.unityLogger.filterLogType = prevFilter;
                Debug.unityLogger.logEnabled = prevLogEnabled;
            }

            sw.Stop();
            stats.ElapsedSeconds = sw.Elapsed.TotalSeconds;
            PrintSummary(stats);
            return new StatsResult(stats);
        }

        public static void PrintSummary(Stats stats)
        {
            double hps = stats.ElapsedSeconds > 0
                ? stats.HandsCompleted / stats.ElapsedSeconds
                : 0d;

            Debug.Log(
                "=== Postflop AI Statistics ===\n" +
                "\n" +
                $"Hands attempted: {stats.HandsAttempted}\n" +
                $"Hands completed: {stats.HandsCompleted}\n" +
                $"Postflop decisions: {stats.PostflopDecisions}\n" +
                "\n" +
                $"Check: {stats.Checks}\n" +
                $"Bet: {stats.Bets}\n" +
                $"Call: {stats.Calls}\n" +
                $"Raise: {stats.Raises}\n" +
                $"Fold: {stats.Folds}\n" +
                $"All-In: {stats.AllIns}\n" +
                "\n" +
                "Street distribution:\n" +
                $"Flop: {stats.FlopDecisions}\n" +
                $"Turn: {stats.TurnDecisions}\n" +
                $"River: {stats.RiverDecisions}\n" +
                "\n" +
                "Situation:\n" +
                $"Checked-to: {stats.CheckedTo}\n" +
                $"Facing-bet: {stats.FacingBet}\n" +
                "\n" +
                "Opponent ranges:\n" +
                $"Wide: {stats.RangeWide}\n" +
                $"Strong: {stats.RangeStrong}\n" +
                $"Strongest: {stats.RangeStrongest}\n" +
                "\n" +
                "Outcomes:\n" +
                $"Showdowns: {stats.Showdowns}\n" +
                $"Fold-outs: {stats.FoldOuts}\n" +
                $"Wins by showdown: {stats.WinsByShowdown}\n" +
                $"Wins by fold: {stats.WinsByFold}\n" +
                $"Split pots: {stats.SplitPots}\n" +
                "\n" +
                $"Exceptions: {stats.Exceptions}\n" +
                $"Illegal actions: {stats.IllegalActions}\n" +
                "\n" +
                $"Runtime: {stats.ElapsedSeconds:F2}s\n" +
                $"Hands/sec: {hps:F2}");
        }

        /// <summary>Null on success; failure message otherwise.</summary>
        private static string RunOneHand(int handIndex, Stats stats)
        {
            var players = new List<PlayerState>(PlayerCount);
            for (int i = 0; i < PlayerCount; i++)
                players.Add(new PlayerState($"StatsBot{i}", PlayerType.AI, StartingChips));

            int dealer  = handIndex % PlayerCount;
            int sbIndex = (dealer + 1) % PlayerCount;
            int bbIndex = (dealer + 2) % PlayerCount;
            int utgIndex = (bbIndex + 1) % PlayerCount;

            var betting = new BettingManager(SmallBlind, BigBlind);
            var board   = new BoardManager();
            var ai      = new AIController();

            betting.ResetRound();
            board.NewDeck();
            ai.ClearHandState();

            board.DealHoleCards(players, sbIndex);
            betting.PostSmallBlind(players[sbIndex]);
            betting.PostBigBlind(players[bbIndex]);

            string err = RunBettingRound(
                players, utgIndex, dealer, GamePhase.PreFlop, betting, board, ai, stats);
            if (err != null)
                return err;

            if (CountNonFolded(players) <= 1)
            {
                RecordFoldOut(stats);
                return null;
            }

            ResetBetsForNewPhase(players, betting);
            board.DealFlop();
            err = RunBettingRound(
                players, sbIndex, dealer, GamePhase.Flop, betting, board, ai, stats);
            if (err != null)
                return err;

            if (CountNonFolded(players) <= 1)
            {
                RecordFoldOut(stats);
                return null;
            }

            ResetBetsForNewPhase(players, betting);
            board.DealTurn();
            err = RunBettingRound(
                players, sbIndex, dealer, GamePhase.Turn, betting, board, ai, stats);
            if (err != null)
                return err;

            if (CountNonFolded(players) <= 1)
            {
                RecordFoldOut(stats);
                return null;
            }

            ResetBetsForNewPhase(players, betting);
            board.DealRiver();
            err = RunBettingRound(
                players, sbIndex, dealer, GamePhase.River, betting, board, ai, stats);
            if (err != null)
                return err;

            if (CountNonFolded(players) <= 1)
            {
                RecordFoldOut(stats);
                return null;
            }

            RecordShowdown(players, board, stats);
            return null;
        }

        private static void ResetBetsForNewPhase(List<PlayerState> players, BettingManager betting)
        {
            betting.ResetPhase();
            for (int i = 0; i < players.Count; i++)
                players[i].CurrentBet = 0;
        }

        private static string RunBettingRound(
            List<PlayerState> players,
            int startIndex,
            int dealerIndex,
            GamePhase phase,
            BettingManager betting,
            BoardManager board,
            AIController ai,
            Stats stats)
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
                int chipsBefore = player.Chips;
                bool isPostflop = phase != GamePhase.PreFlop;

                if (isPostflop)
                    CountOpponentRange(ai, callAmount, betting.StreetRaiseCount, player.Chips, stats);

                var (action, raise) = ai.DecideAction(
                    player,
                    board.CommunityCards,
                    betting,
                    players,
                    phase,
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

                int playerBetBefore = player.CurrentBet;
                if (!betting.ProcessAction(player, action, raise))
                {
                    return $"{player.Name} illegal action rejected: {action} raise={raise} " +
                           $"call={callAmount} chips={player.Chips} table={betting.CurrentBet}";
                }

                int displayAmount = GetActionDisplayAmount(player, action, raise, playerBetBefore);
                ai.RecordHandAction(
                    phase, player, action, displayAmount, betting.Pot, betting.StreetRaiseCount);

                if (phase == GamePhase.Flop && betting.CurrentBet > betBefore)
                    ai.NoteFlopAggression(player);

                if (isPostflop)
                    CountPostflopDecision(stats, phase, action, callAmount, chipsBefore, player);

                hasActed[currentIndex] = true;

                if (betting.CurrentBet - betBefore >= minRaiseBefore)
                    ReopenActionForOthers(players, hasActed, currentIndex);

                seatIndex++;

                if (IsBettingComplete(players, hasActed))
                    return null;
            }

            return $"{phase} betting round failed to complete (safety limit)";
        }

        private static void CountOpponentRange(
            AIController ai,
            int callAmount,
            int streetRaiseCount,
            int defenderChips,
            Stats stats)
        {
            int preflopRaiseCount = ResolvePreflopRaiseCount(ai);
            MonteCarloSimulator.DescribeOpponentRangeSelection(
                facingBet: callAmount > 0,
                streetRaiseCount: streetRaiseCount,
                callAmount: callAmount,
                defenderChips: defenderChips,
                out OpponentRangeStrength range,
                preflopRaiseCount);

            switch (range)
            {
                case OpponentRangeStrength.Wide:
                    stats.RangeWide++;
                    break;
                case OpponentRangeStrength.Strong:
                    stats.RangeStrong++;
                    break;
                case OpponentRangeStrength.Strongest:
                    stats.RangeStrongest++;
                    break;
            }
        }

        /// <summary>Same scan as <see cref="HandActionLog.ResolvePreflopRaiseCount"/> via public HandActions.</summary>
        private static int ResolvePreflopRaiseCount(AIController ai)
        {
            int max = 0;
            IReadOnlyList<HandActionEntry> entries = ai.HandActions;
            for (int i = 0; i < entries.Count; i++)
            {
                HandActionEntry e = entries[i];
                if (e.Street != GamePhase.PreFlop)
                    continue;
                if (e.StreetRaiseCount > max)
                    max = e.StreetRaiseCount;
            }

            return max;
        }

        private static void CountPostflopDecision(
            Stats stats,
            GamePhase phase,
            BettingAction action,
            int callAmountBefore,
            int chipsBefore,
            PlayerState player)
        {
            stats.PostflopDecisions++;

            switch (phase)
            {
                case GamePhase.Flop:  stats.FlopDecisions++; break;
                case GamePhase.Turn:  stats.TurnDecisions++; break;
                case GamePhase.River: stats.RiverDecisions++; break;
            }

            if (callAmountBefore > 0)
                stats.FacingBet++;
            else
                stats.CheckedTo++;

            bool isAllIn =
                action == BettingAction.AllIn
                || player.IsAllIn
                || (chipsBefore > 0 && player.Chips == 0);

            if (isAllIn)
            {
                stats.AllIns++;
                return;
            }

            switch (action)
            {
                case BettingAction.Check:
                    stats.Checks++;
                    break;
                case BettingAction.Call:
                    stats.Calls++;
                    break;
                case BettingAction.Fold:
                    stats.Folds++;
                    break;
                case BettingAction.Raise:
                    if (callAmountBefore <= 0)
                        stats.Bets++;
                    else
                        stats.Raises++;
                    break;
            }
        }

        private static void RecordFoldOut(Stats stats)
        {
            stats.FoldOuts++;
            stats.WinsByFold++;
        }

        private static void RecordShowdown(
            List<PlayerState> players,
            BoardManager board,
            Stats stats)
        {
            var contenders = new List<PlayerState>();
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].HasFolded)
                    contenders.Add(players[i]);
            }

            if (contenders.Count <= 1)
            {
                RecordFoldOut(stats);
                return;
            }

            stats.Showdowns++;

            WinningHandEvaluation bestEval = null;
            var evaluated = new List<(PlayerState player, WinningHandEvaluation evaluation)>();

            for (int i = 0; i < contenders.Count; i++)
            {
                PlayerState player = contenders[i];
                var cards = new List<Card>(player.HoleCards);
                cards.AddRange(board.CommunityCards);
                if (cards.Count < 5)
                    continue;

                WinningHandEvaluation evaluation = HandEvaluator.EvaluateBest(cards);
                evaluated.Add((player, evaluation));

                if (bestEval == null || evaluation.Result.CompareTo(bestEval.Result) > 0)
                    bestEval = evaluation;
            }

            var winners = new List<PlayerState>();
            if (bestEval != null)
            {
                for (int i = 0; i < evaluated.Count; i++)
                {
                    if (evaluated[i].evaluation.Result.CompareTo(bestEval.Result) == 0)
                        winners.Add(evaluated[i].player);
                }
            }
            else if (contenders.Count > 0)
            {
                winners.Add(contenders[0]);
            }

            if (winners.Count >= 2)
                stats.SplitPots++;
            else if (winners.Count == 1)
                stats.WinsByShowdown++;
        }

        private static int GetActionDisplayAmount(
            PlayerState player, BettingAction action, int raiseAmount, int playerBetBefore)
        {
            switch (action)
            {
                case BettingAction.Call:
                case BettingAction.AllIn:
                    return Mathf.Max(0, player.CurrentBet - playerBetBefore);
                case BettingAction.Raise:
                    return raiseAmount;
                default:
                    return 0;
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

        public readonly struct StatsResult
        {
            public Stats Stats { get; }
            public bool Ok => Stats.Exceptions == 0 && Stats.IllegalActions == 0;

            public StatsResult(Stats stats) => Stats = stats;
        }

        public sealed class Stats
        {
            public int HandsAttempted;
            public int HandsCompleted;
            public int PostflopDecisions;

            public int Checks;
            public int Bets;
            public int Calls;
            public int Raises;
            public int Folds;
            public int AllIns;

            public int FlopDecisions;
            public int TurnDecisions;
            public int RiverDecisions;

            public int CheckedTo;
            public int FacingBet;

            public int RangeWide;
            public int RangeStrong;
            public int RangeStrongest;

            public int Showdowns;
            public int FoldOuts;
            public int WinsByShowdown;
            public int WinsByFold;
            public int SplitPots;

            public int Exceptions;
            public int IllegalActions;
            public string LastError;
            public double ElapsedSeconds;
        }
    }
}
