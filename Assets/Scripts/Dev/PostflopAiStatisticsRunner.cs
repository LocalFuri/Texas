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

            // Always emit after log filter is restored; Warning stays visible if Log is filtered.
            PrintSummary(stats);
            return new StatsResult(stats);
        }

        public static void PrintSummary(Stats stats)
        {
            string summary = BuildSummaryText(stats);

            // One Warning with the full report. NoStacktrace keeps the body visible in the
            // Console detail pane (stack traces otherwise push multi-line text out of view).
            // filterLogType is already restored by RunAll before this runs.
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", summary);
        }

        /// <summary>Builds the complete statistics report (never empty beyond the header).</summary>
        public static string BuildSummaryText(Stats stats)
        {
            if (stats == null)
                return "=== Postflop AI Statistics ===\n\n(stats was null)";

            double hps = stats.ElapsedSeconds > 0
                ? stats.HandsCompleted / stats.ElapsedSeconds
                : 0d;

            int n = stats.HandsCompleted;

            var sb = new System.Text.StringBuilder(1024);
            sb.AppendLine("=== Postflop AI Statistics ===");
            sb.AppendLine();
            sb.Append("Hands attempted: ").Append(stats.HandsAttempted).AppendLine();
            sb.Append("Hands completed: ").Append(stats.HandsCompleted).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Postflop decisions:");
            sb.Append("  Check: ").Append(stats.Checks).AppendLine();
            sb.Append("  Bet: ").Append(stats.Bets).AppendLine();
            sb.Append("  Call: ").Append(stats.Calls).AppendLine();
            sb.Append("  Raise: ").Append(stats.Raises).AppendLine();
            sb.Append("  Fold: ").Append(stats.Folds).AppendLine();
            sb.Append("  All-In: ").Append(stats.AllIns).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Street:");
            sb.Append("  Flop: ").Append(stats.FlopDecisions).AppendLine();
            sb.Append("  Turn: ").Append(stats.TurnDecisions).AppendLine();
            sb.Append("  River: ").Append(stats.RiverDecisions).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Situation:");
            sb.Append("  Checked-to: ").Append(stats.CheckedTo).AppendLine();
            sb.Append("  Facing-bet: ").Append(stats.FacingBet).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Opponent ranges:");
            sb.Append("  Wide: ").Append(stats.RangeWide).AppendLine();
            sb.Append("  Strong: ").Append(stats.RangeStrong).AppendLine();
            sb.Append("  Strongest: ").Append(stats.RangeStrongest).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Outcomes:");
            sb.Append("  Showdowns: ").Append(stats.Showdowns).AppendLine();
            sb.Append("  Fold-outs: ").Append(stats.FoldOuts).AppendLine();
            sb.Append("  Wins by showdown: ").Append(stats.WinsByShowdown).AppendLine();
            sb.Append("  Wins by fold: ").Append(stats.WinsByFold).AppendLine();
            sb.Append("  Split pots: ").Append(stats.SplitPots).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Averages (per completed hand):");
            sb.Append("  Postflop decisions/hand: ").Append(Avg(stats.PostflopDecisions, n).ToString("F2")).AppendLine();
            sb.Append("  Betting rounds reached: ").Append(Avg(stats.SumBettingRoundsReached, n).ToString("F2")).AppendLine();
            sb.Append("  Players seeing flop: ").Append(Avg(stats.SumPlayersSeeingFlop, n).ToString("F2")).AppendLine();
            sb.Append("  Players seeing turn: ").Append(Avg(stats.SumPlayersSeeingTurn, n).ToString("F2")).AppendLine();
            sb.Append("  Players seeing river: ").Append(Avg(stats.SumPlayersSeeingRiver, n).ToString("F2")).AppendLine();
            sb.Append("  Players at showdown: ").Append(Avg(stats.SumPlayersAtShowdown, n).ToString("F2")).AppendLine();
            sb.AppendLine();
            AppendPlayerSurvival(sb, stats, n);
            sb.AppendLine("Fold analysis:");
            sb.AppendLine("  Street:");
            sb.Append("    Fold on flop: ").Append(stats.FoldsOnFlop).AppendLine();
            sb.Append("    Fold on turn: ").Append(stats.FoldsOnTurn).AppendLine();
            sb.Append("    Fold on river: ").Append(stats.FoldsOnRiver).AppendLine();
            sb.AppendLine("  Situation:");
            sb.Append("    Fold after facing bet: ").Append(stats.FoldsFacingBet).AppendLine();
            sb.Append("    Fold after facing raise: ").Append(stats.FoldsFacingRaise).AppendLine();
            sb.Append("    Fold versus all-in: ").Append(stats.FoldsVersusAllIn).AppendLine();
            sb.AppendLine("  Opponent range:");
            sb.Append("    Fold versus Wide: ").Append(stats.FoldsVersusWide).AppendLine();
            sb.Append("    Fold versus Strong: ").Append(stats.FoldsVersusStrong).AppendLine();
            sb.Append("    Fold versus Strongest: ").Append(stats.FoldsVersusStrongest).AppendLine();
            sb.Append("  Average pot size when folding: ").Append(
                stats.Folds > 0
                    ? (stats.SumPotAtFold / (double)stats.Folds).ToString("F1")
                    : "0.0").AppendLine();
            sb.AppendLine();
            sb.Append("Exceptions: ").Append(stats.Exceptions).AppendLine();
            sb.Append("Illegal actions: ").Append(stats.IllegalActions).AppendLine();
            sb.AppendLine();
            sb.Append("Runtime: ").Append(stats.ElapsedSeconds.ToString("F2")).Append('s').AppendLine();
            sb.Append("Hands/sec: ").Append(hps.ToString("F2")).AppendLine();

            return sb.ToString();
        }

        private static double Avg(int sum, int handsCompleted) =>
            handsCompleted > 0 ? (double)sum / handsCompleted : 0d;

        private static void AppendPlayerSurvival(System.Text.StringBuilder sb, Stats stats, int handsCompleted)
        {
            double startAvg = Avg(stats.SumPlayersAtHandStart, handsCompleted);
            // Percent of original seats (PlayerCount), not of average start — start is always 6.
            double denom = PlayerCount;

            sb.AppendLine("=== Player Survival ===");
            sb.AppendLine();
            AppendSurvivalLine(sb, "Hand start:", startAvg, denom);
            AppendSurvivalLine(sb, "After preflop:", Avg(stats.SumPlayersAfterPreflop, handsCompleted), denom);
            AppendSurvivalLine(sb, "Flop:", Avg(stats.SumPlayersSeeingFlop, handsCompleted), denom);
            AppendSurvivalLine(sb, "Turn:", Avg(stats.SumPlayersSeeingTurn, handsCompleted), denom);
            AppendSurvivalLine(sb, "River:", Avg(stats.SumPlayersSeeingRiver, handsCompleted), denom);
            AppendSurvivalLine(sb, "Showdown:", Avg(stats.SumPlayersAtShowdown, handsCompleted), denom);
            sb.AppendLine();
        }

        private static void AppendSurvivalLine(
            System.Text.StringBuilder sb, string label, double average, double originalPlayers)
        {
            double pct = originalPlayers > 0 ? 100.0 * average / originalPlayers : 0d;
            sb.Append("  ").Append(label.PadRight(16)).Append(' ')
                .Append(average.ToString("F2"))
                .Append(" (")
                .Append(pct.ToString("F1"))
                .Append("%)")
                .AppendLine();
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

            // Local until hand completes successfully (illegal mid-hand must not skew averages).
            int bettingRoundsReached = 1;
            int playersAtHandStart = players.Count;
            int playersAfterPreflop = 0;
            int playersSeeingFlop = 0;
            int playersSeeingTurn = 0;
            int playersSeeingRiver = 0;
            int playersAtShowdown = 0;

            string err = RunBettingRound(
                players, utgIndex, dealer, GamePhase.PreFlop, betting, board, ai, stats);
            if (err != null)
                return err;

            playersAfterPreflop = CountNonFolded(players);

            if (playersAfterPreflop <= 1)
            {
                RecordFoldOut(stats);
                CommitHandDepth(
                    stats, bettingRoundsReached, playersAtHandStart, playersAfterPreflop,
                    playersSeeingFlop, playersSeeingTurn, playersSeeingRiver, playersAtShowdown);
                return null;
            }

            ResetBetsForNewPhase(players, betting);
            board.DealFlop();
            bettingRoundsReached = 2;
            playersSeeingFlop = CountNonFolded(players);
            err = RunBettingRound(
                players, sbIndex, dealer, GamePhase.Flop, betting, board, ai, stats);
            if (err != null)
                return err;

            if (CountNonFolded(players) <= 1)
            {
                RecordFoldOut(stats);
                CommitHandDepth(
                    stats, bettingRoundsReached, playersAtHandStart, playersAfterPreflop,
                    playersSeeingFlop, playersSeeingTurn, playersSeeingRiver, playersAtShowdown);
                return null;
            }

            ResetBetsForNewPhase(players, betting);
            board.DealTurn();
            bettingRoundsReached = 3;
            playersSeeingTurn = CountNonFolded(players);
            err = RunBettingRound(
                players, sbIndex, dealer, GamePhase.Turn, betting, board, ai, stats);
            if (err != null)
                return err;

            if (CountNonFolded(players) <= 1)
            {
                RecordFoldOut(stats);
                CommitHandDepth(
                    stats, bettingRoundsReached, playersAtHandStart, playersAfterPreflop,
                    playersSeeingFlop, playersSeeingTurn, playersSeeingRiver, playersAtShowdown);
                return null;
            }

            ResetBetsForNewPhase(players, betting);
            board.DealRiver();
            bettingRoundsReached = 4;
            playersSeeingRiver = CountNonFolded(players);
            err = RunBettingRound(
                players, sbIndex, dealer, GamePhase.River, betting, board, ai, stats);
            if (err != null)
                return err;

            if (CountNonFolded(players) <= 1)
            {
                RecordFoldOut(stats);
                CommitHandDepth(
                    stats, bettingRoundsReached, playersAtHandStart, playersAfterPreflop,
                    playersSeeingFlop, playersSeeingTurn, playersSeeingRiver, playersAtShowdown);
                return null;
            }

            playersAtShowdown = CountNonFolded(players);
            RecordShowdown(players, board, stats);
            CommitHandDepth(
                stats, bettingRoundsReached, playersAtHandStart, playersAfterPreflop,
                playersSeeingFlop, playersSeeingTurn, playersSeeingRiver, playersAtShowdown);
            return null;
        }

        private static void CommitHandDepth(
            Stats stats,
            int bettingRoundsReached,
            int playersAtHandStart,
            int playersAfterPreflop,
            int playersSeeingFlop,
            int playersSeeingTurn,
            int playersSeeingRiver,
            int playersAtShowdown)
        {
            stats.SumBettingRoundsReached += bettingRoundsReached;
            stats.SumPlayersAtHandStart += playersAtHandStart;
            stats.SumPlayersAfterPreflop += playersAfterPreflop;
            stats.SumPlayersSeeingFlop += playersSeeingFlop;
            stats.SumPlayersSeeingTurn += playersSeeingTurn;
            stats.SumPlayersSeeingRiver += playersSeeingRiver;
            stats.SumPlayersAtShowdown += playersAtShowdown;
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
                int streetRaiseCount = betting.StreetRaiseCount;
                int potBefore = betting.Pot;
                bool isPostflop = phase != GamePhase.PreFlop;

                OpponentRangeStrength opponentRange = OpponentRangeStrength.Wide;
                if (isPostflop)
                    opponentRange = CountOpponentRange(ai, callAmount, streetRaiseCount, player.Chips, stats);

                var (action, raise) = ai.DecideAction(
                    player,
                    board.CommunityCards,
                    betting,
                    players,
                    phase,
                    betting.Pot,
                    betting.CurrentBet,
                    BigBlind,
                    streetRaiseCount,
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
                {
                    bool facingAllIn = IsFacingAllIn(players, player, betBefore);
                    CountPostflopDecision(
                        stats, phase, action, callAmount, chipsBefore, player,
                        streetRaiseCount, opponentRange, potBefore, facingAllIn);
                }

                hasActed[currentIndex] = true;

                if (betting.CurrentBet - betBefore >= minRaiseBefore)
                    ReopenActionForOthers(players, hasActed, currentIndex);

                seatIndex++;

                if (IsBettingComplete(players, hasActed))
                    return null;
            }

            return $"{phase} betting round failed to complete (safety limit)";
        }

        private static OpponentRangeStrength CountOpponentRange(
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

            return range;
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

        /// <summary>
        /// True when folding/calling would be against an all-in opponent who put money in this street.
        /// Uses table state before the current action is applied.
        /// </summary>
        private static bool IsFacingAllIn(
            IReadOnlyList<PlayerState> players,
            PlayerState hero,
            int tableBetBefore)
        {
            if (tableBetBefore <= 0 || players == null || hero == null)
                return false;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState p = players[i];
                if (p == null || p == hero || p.HasFolded)
                    continue;
                if (p.IsAllIn && p.CurrentBet > 0)
                    return true;
            }

            return false;
        }

        private static void CountPostflopDecision(
            Stats stats,
            GamePhase phase,
            BettingAction action,
            int callAmountBefore,
            int chipsBefore,
            PlayerState player,
            int streetRaiseCount,
            OpponentRangeStrength opponentRange,
            int potBefore,
            bool facingAllIn)
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
                    RecordFoldAnalysis(
                        stats, phase, callAmountBefore, streetRaiseCount, opponentRange, potBefore, facingAllIn);
                    break;
                case BettingAction.Raise:
                    if (callAmountBefore <= 0)
                        stats.Bets++;
                    else
                        stats.Raises++;
                    break;
            }
        }

        private static void RecordFoldAnalysis(
            Stats stats,
            GamePhase phase,
            int callAmountBefore,
            int streetRaiseCount,
            OpponentRangeStrength opponentRange,
            int potBefore,
            bool facingAllIn)
        {
            switch (phase)
            {
                case GamePhase.Flop:  stats.FoldsOnFlop++; break;
                case GamePhase.Turn:  stats.FoldsOnTurn++; break;
                case GamePhase.River: stats.FoldsOnRiver++; break;
            }

            // Situation buckets (independent — a fold can count in more than one).
            if (callAmountBefore > 0)
            {
                if (streetRaiseCount >= 2)
                    stats.FoldsFacingRaise++;
                else
                    stats.FoldsFacingBet++;
            }

            if (facingAllIn)
                stats.FoldsVersusAllIn++;

            switch (opponentRange)
            {
                case OpponentRangeStrength.Wide:
                    stats.FoldsVersusWide++;
                    break;
                case OpponentRangeStrength.Strong:
                    stats.FoldsVersusStrong++;
                    break;
                case OpponentRangeStrength.Strongest:
                    stats.FoldsVersusStrongest++;
                    break;
            }

            stats.SumPotAtFold += potBefore;
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

            /// <summary>Sum of betting rounds reached per hand (1=preflop … 4=river).</summary>
            public int SumBettingRoundsReached;
            public int SumPlayersAtHandStart;
            public int SumPlayersAfterPreflop;
            /// <summary>Sum of non-folded players when each street is dealt (0 if street not reached).</summary>
            public int SumPlayersSeeingFlop;
            public int SumPlayersSeeingTurn;
            public int SumPlayersSeeingRiver;
            /// <summary>Sum of contenders at showdown (0 on fold-outs).</summary>
            public int SumPlayersAtShowdown;

            public int FoldsOnFlop;
            public int FoldsOnTurn;
            public int FoldsOnRiver;
            public int FoldsFacingBet;
            public int FoldsFacingRaise;
            public int FoldsVersusAllIn;
            public int FoldsVersusWide;
            public int FoldsVersusStrong;
            public int FoldsVersusStrongest;
            public long SumPotAtFold;

            public int Exceptions;
            public int IllegalActions;
            public string LastError;
            public double ElapsedSeconds;
        }
    }
}
