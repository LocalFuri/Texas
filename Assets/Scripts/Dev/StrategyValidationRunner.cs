using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Developer-only: repeats one fixed hero decision while opponents (and hero after the
    /// forced act) use production <see cref="AIController.DecideAction"/>. Headless — does not
    /// touch gameplay, AI strategy, or Monte Carlo.
    /// </summary>
    public static class StrategyValidationRunner
    {
        public const int DefaultIterations = 10_000;

        /// <param name="scenario">Fixed hero spot (required).</param>
        /// <param name="iterations">Hands to simulate (default 10,000).</param>
        /// <param name="onHandFinished">Optional progress callback (handNumber1Based, total).</param>
        public static StrategyValidationResult Run(
            StrategyValidationScenario scenario,
            int iterations = DefaultIterations,
            Action<int, int> onHandFinished = null)
        {
            var stats = new StrategyValidationStats();

            if (scenario == null)
            {
                stats.LastError = "Scenario is null.";
                stats.Exceptions = 1;
                Debug.LogError("[StrategyValidation] " + stats.LastError);
                return new StrategyValidationResult(stats);
            }

            string validationError = scenario.Validate();
            if (validationError != null)
            {
                stats.ScenarioName = scenario.Name;
                stats.LastError = validationError;
                stats.Exceptions = 1;
                Debug.LogError("[StrategyValidation] " + validationError);
                return new StrategyValidationResult(stats);
            }

            if (iterations < 1)
                iterations = DefaultIterations;

            stats.ScenarioName = scenario.Name;
            var sw = Stopwatch.StartNew();

            bool prevLogEnabled = Debug.unityLogger.logEnabled;
            LogType prevFilter = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Warning;

            Debug.LogWarning(
                $"[StrategyValidation] Running {iterations} hand(s)" +
                (string.IsNullOrEmpty(scenario.Name) ? "..." : $" for '{scenario.Name}'..."));

            try
            {
                for (int hand = 0; hand < iterations; hand++)
                {
                    if (scenario.BaseSeed.HasValue)
                        UnityEngine.Random.InitState(scenario.BaseSeed.Value + hand);

                    stats.HandsAttempted++;

                    try
                    {
                        string failure = RunOneHand(scenario, stats);
                        if (failure != null)
                        {
                            stats.IllegalActions++;
                            stats.LastError = $"hand={hand}: {failure}";
                            Debug.LogError($"[StrategyValidation] Illegal — {stats.LastError}");
                        }
                    }
                    catch (Exception ex)
                    {
                        stats.Exceptions++;
                        stats.LastError = $"hand={hand}: {ex.GetType().Name}: {ex.Message}";
                        Debug.LogError($"[StrategyValidation] Exception — {stats.LastError}");
                    }

                    onHandFinished?.Invoke(hand + 1, iterations);
                }
            }
            finally
            {
                Debug.unityLogger.filterLogType = prevFilter;
                Debug.unityLogger.logEnabled = prevLogEnabled;
            }

            sw.Stop();
            stats.ElapsedSeconds = sw.Elapsed.TotalSeconds;

            string summary = stats.BuildSummaryText(scenario.BigBlind);
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", summary);
            return new StrategyValidationResult(stats);
        }

        /// <summary>Null on success; failure message otherwise.</summary>
        private static string RunOneHand(StrategyValidationScenario scenario, StrategyValidationStats stats)
        {
            int n = scenario.PlayerCount;
            int heroIndex = 0;

            var players = new List<PlayerState>(n);
            players.Add(new PlayerState("Hero", PlayerType.AI, scenario.HeroStack));
            for (int i = 1; i < n; i++)
                players.Add(new PlayerState($"Villain{i}", PlayerType.AI, scenario.OpponentStack));

            PlayerState hero = players[heroIndex];
            int dealer = DealerIndexForHeroSeat(scenario.HeroPosition, heroIndex, n);
            int sbIndex = (dealer + 1) % n;
            int bbIndex = (dealer + 2) % n;
            int utgIndex = (bbIndex + 1) % n;

            var betting = new BettingManager(scenario.SmallBlind, scenario.BigBlind);
            var board = new BoardManager();
            var ai = new AIController();

            betting.ResetRound();
            board.NewDeck();
            ai.ClearHandState();

            string dealError = DealHolesWithFixedHero(board, players, hero, scenario);
            if (dealError != null)
                return dealError;

            int heroChipsAtStart = hero.Chips;

            betting.PostSmallBlind(players[sbIndex]);
            betting.PostBigBlind(players[bbIndex]);

            bool heroForcePending = true;
            bool heroReachedShowdown = false;

            string err = RunBettingRound(
                players, utgIndex, dealer, GamePhase.PreFlop, betting, board, ai,
                scenario, hero, ref heroForcePending, stats);
            if (err != null)
                return err;

            if (heroForcePending)
                return "Hero never received a preflop decision for the forced action.";

            if (CountNonFolded(players) > 1)
            {
                ResetBetsForNewPhase(players, betting);
                board.DealFlop();
                err = RunBettingRound(
                    players, sbIndex, dealer, GamePhase.Flop, betting, board, ai,
                    scenario, hero, ref heroForcePending, stats);
                if (err != null)
                    return err;
            }

            if (CountNonFolded(players) > 1)
            {
                ResetBetsForNewPhase(players, betting);
                board.DealTurn();
                err = RunBettingRound(
                    players, sbIndex, dealer, GamePhase.Turn, betting, board, ai,
                    scenario, hero, ref heroForcePending, stats);
                if (err != null)
                    return err;
            }

            if (CountNonFolded(players) > 1)
            {
                ResetBetsForNewPhase(players, betting);
                board.DealRiver();
                err = RunBettingRound(
                    players, sbIndex, dealer, GamePhase.River, betting, board, ai,
                    scenario, hero, ref heroForcePending, stats);
                if (err != null)
                    return err;
            }

            List<PlayerState> contenders = CollectNonFolded(players);
            if (contenders.Count == 0)
                return "No contenders left to award pot.";

            if (contenders.Count == 1)
            {
                betting.ReturnUncalledBet(contenders[0], players);
                PotAward.Split(betting.Pot, contenders);
            }
            else
            {
                List<PlayerState> winners = ResolveShowdownWinners(contenders, board);
                if (winners.Count == 0)
                    return "Showdown produced no winners.";

                winners = PotAward.OrderWinnersClockwiseFromDealer(winners, players, dealer);
                PotAward.Split(betting.Pot, winners);
                if (!hero.HasFolded)
                    heroReachedShowdown = true;
            }

            int delta = hero.Chips - heroChipsAtStart;
            stats.HandsPlayed++;
            stats.TotalProfitLoss += delta;
            if (delta > 0)
                stats.Wins++;
            if (heroReachedShowdown)
                stats.Showdowns++;

            return null;
        }

        private static string DealHolesWithFixedHero(
            BoardManager board,
            List<PlayerState> players,
            PlayerState hero,
            StrategyValidationScenario scenario)
        {
            for (int i = 0; i < players.Count; i++)
                players[i].HoleCards.Clear();

            try
            {
                board.AssignHoleCards(
                    hero,
                    scenario.HoleSuit0, scenario.HoleRank0,
                    scenario.HoleSuit1, scenario.HoleRank1);
            }
            catch (Exception ex)
            {
                return $"Failed to assign hero hole cards: {ex.Message}";
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == hero)
                    continue;

                board.DealHoleCardTo(players[i]);
                board.DealHoleCardTo(players[i]);
            }

            return null;
        }

        private static string RunBettingRound(
            List<PlayerState> players,
            int startIndex,
            int dealerIndex,
            GamePhase phase,
            BettingManager betting,
            BoardManager board,
            AIController ai,
            StrategyValidationScenario scenario,
            PlayerState hero,
            ref bool heroForcePending,
            StrategyValidationStats stats)
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
                int streetRaiseCountBefore = betting.StreetRaiseCount;

                BettingAction action;
                int raise;

                bool forceThisAct = player == hero
                    && heroForcePending
                    && phase == GamePhase.PreFlop;

                if (forceThisAct)
                {
                    action = scenario.HeroAction;
                    raise = scenario.HeroAction == BettingAction.Raise ? scenario.RaiseIncrement : 0;
                    RecordForcedAction(stats, action, streetRaiseCountBefore);
                    heroForcePending = false;
                }
                else
                {
                    (action, raise) = ai.DecideAction(
                        player,
                        board.CommunityCards,
                        betting,
                        players,
                        phase,
                        betting.Pot,
                        betting.CurrentBet,
                        scenario.BigBlind,
                        betting.StreetRaiseCount,
                        seat,
                        testMode: false,
                        playersBehind,
                        shovePosition,
                        callersBefore);
                }

                int playerBetBefore = player.CurrentBet;
                if (!betting.ProcessAction(player, action, raise))
                {
                    return $"{player.Name} illegal action rejected: {action} raise={raise} " +
                           $"call={betting.GetCallAmount(player)} chips={player.Chips} " +
                           $"table={betting.CurrentBet} forced={forceThisAct}";
                }

                int displayAmount = GetActionDisplayAmount(player, action, raise, playerBetBefore);
                ai.RecordHandAction(
                    phase, player, action, displayAmount, betting.Pot, betting.StreetRaiseCount);

                if (phase == GamePhase.Flop && betting.CurrentBet > betBefore)
                    ai.NoteFlopAggression(player);

                hasActed[currentIndex] = true;

                if (betting.CurrentBet - betBefore >= minRaiseBefore)
                    ReopenActionForOthers(players, hasActed, currentIndex);

                seatIndex++;

                if (IsBettingComplete(players, hasActed))
                    return null;
            }

            return $"{phase} betting round failed to complete (safety limit)";
        }

        private static void RecordForcedAction(
            StrategyValidationStats stats,
            BettingAction action,
            int streetRaiseCountBefore)
        {
            stats.ForcedActions++;

            switch (action)
            {
                case BettingAction.Fold:
                    stats.ForcedFolds++;
                    break;
                case BettingAction.Call:
                    stats.ForcedCalls++;
                    break;
                case BettingAction.Raise:
                case BettingAction.AllIn:
                    if (streetRaiseCountBefore >= 1)
                        stats.ForcedThreeBets++;
                    break;
            }
        }

        private static List<PlayerState> ResolveShowdownWinners(
            List<PlayerState> contenders,
            BoardManager board)
        {
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

            return winners;
        }

        /// <summary>
        /// Chooses dealer so <see cref="PreflopStrategy.ResolveSeatBucket"/> returns
        /// <paramref name="heroSeat"/> for the hero at <paramref name="heroIndex"/>.
        /// </summary>
        private static int DealerIndexForHeroSeat(PreflopSeatBucket heroSeat, int heroIndex, int n)
        {
            int fromBtn = heroSeat switch
            {
                PreflopSeatBucket.Button     => 0,
                PreflopSeatBucket.SmallBlind => 1,
                PreflopSeatBucket.BigBlind   => 2,
                PreflopSeatBucket.Early      => 3,
                PreflopSeatBucket.Cutoff     => n - 1,
                PreflopSeatBucket.Middle     => n >= 6 ? n - 2 : Math.Max(3, n / 2),
                _                            => 0,
            };

            if (fromBtn < 0 || fromBtn >= n)
                fromBtn = 0;

            return ((heroIndex - fromBtn) % n + n) % n;
        }

        private static void ResetBetsForNewPhase(List<PlayerState> players, BettingManager betting)
        {
            betting.ResetPhase();
            for (int i = 0; i < players.Count; i++)
                players[i].CurrentBet = 0;
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

        private static List<PlayerState> CollectNonFolded(IReadOnlyList<PlayerState> players)
        {
            var list = new List<PlayerState>();
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].HasFolded)
                    list.Add(players[i]);
            }

            return list;
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
