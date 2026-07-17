using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Observational 10k-hand preflop validation — does not change AI logic.
    /// Writes JSON report for the validation canvas.
    /// </summary>
    public static class PreflopValidationReportRunner
    {
        private const int DefaultHandCount = 10_000;
        private const int PlayerCount = 6;
        private const int StartingChips = 1000;
        private const int SmallBlind = 10;
        private const int BigBlind = 20;
        private const int MaxSuspicious = 20;

        public static string RunAndWriteReport(int handCount = DefaultHandCount, string outputPath = null)
        {
            if (handCount < 1)
                handCount = DefaultHandCount;

            var report = new ReportData { Hands = handCount, SeedNote = "Sequential dealer rotation; BoardManager shuffle" };
            var rngNote = new System.Random(42); // reserved for future; BoardManager uses Unity Random
            UnityEngine.Random.InitState(42);

            for (int hand = 0; hand < handCount; hand++)
                RunOneHand(hand, report);

            string json = report.ToJson();
            string path = outputPath
                ?? Path.Combine(Environment.CurrentDirectory, "preflop-validation-report.json");
            File.WriteAllText(path, json, Encoding.UTF8);
            Debug.Log($"[PreflopValidation] Wrote {path}");
            PrintConsoleSummary(report);
            return path;
        }

        private static void RunOneHand(int handIndex, ReportData report)
        {
            var players = new List<PlayerState>(PlayerCount);
            for (int i = 0; i < PlayerCount; i++)
                players.Add(new PlayerState($"Bot{i}", PlayerType.AI, StartingChips));

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

            for (int i = 0; i < PlayerCount; i++)
            {
                PreflopHandGroup g = PreflopStrategy.ClassifyHand(players[i].HoleCards);
                report.TierCounts[(int)g]++;
                report.TierTotal++;
            }

            var history = new List<string>(24);
            history.Add($"Hand#{handIndex} dealer=Bot{dealer}");

            RunPreflopBettingRound(
                players, utgIndex, dealer, betting, board, ai, report, history);
        }

        private static void RunPreflopBettingRound(
            List<PlayerState> players,
            int startIndex,
            int dealerIndex,
            BettingManager betting,
            BoardManager board,
            AIController ai,
            ReportData report,
            List<string> history)
        {
            int n = players.Count;
            var hasActed = new bool[n];
            for (int i = 0; i < n; i++)
                hasActed[i] = players[i].HasFolded || players[i].IsAllIn;

            int seatIndex = startIndex % n;
            int safetyLimit = n * n * 4;
            int iterations = 0;

            while (iterations++ < safetyLimit)
            {
                if (CountNonFolded(players) <= 1)
                    return;
                if (IsBettingComplete(players, hasActed))
                    return;

                int currentIndex = seatIndex % n;
                PlayerState player = players[currentIndex];

                if (player.HasFolded || player.IsAllIn || hasActed[currentIndex])
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
                string seatKey = SeatKey(seat);
                SeatStats seatStats = report.Seats[seatKey];

                int betBefore = betting.CurrentBet;
                int minRaiseBefore = betting.GetMinRaiseIncrement();
                int callAmount = betting.GetCallAmount(player);
                int srcBefore = betting.StreetRaiseCount;
                bool canCheck = callAmount <= 0;
                bool facingRaise = betting.CurrentBet > BigBlind;
                bool facingAllIn = player.Chips > 0
                    && callAmount >= Mathf.CeilToInt(player.Chips * 0.85f);

                PreflopHandGroup group = PreflopStrategy.ClassifyHand(player.HoleCards);

                seatStats.Decisions++;

                // Unopened pot: CurrentBet is still the BB (no raise yet).
                bool openOpportunity = srcBefore == 0 && !facingRaise;
                if (openOpportunity)
                    seatStats.OpenOpps++;

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

                if (!betting.ProcessAction(player, action, raise))
                {
                    report.IllegalActions++;
                    seatIndex++;
                    continue;
                }

                BettingAction counted = action;
                if (action == BettingAction.AllIn)
                    counted = callAmount > 0 ? BettingAction.Call : BettingAction.Raise;
                else if (action == BettingAction.Check)
                    counted = BettingAction.Check;

                switch (counted)
                {
                    case BettingAction.Fold: seatStats.Folds++; break;
                    case BettingAction.Call: seatStats.Calls++; break;
                    case BettingAction.Raise: seatStats.Raises++; break;
                    case BettingAction.Check: seatStats.Checks++; break;
                }

                string hole = FormatHole(player);
                history.Add($"{seatKey} {player.Name} {hole} {group} SRC={srcBefore} → {action}" +
                    (raise > 0 ? $" +{raise}" : ""));

                if (openOpportunity && (counted == BettingAction.Raise || action == BettingAction.AllIn && callAmount <= 0))
                    seatStats.OpenRaises++;

                if (counted == BettingAction.Raise || (action == BettingAction.AllIn && callAmount <= 0))
                {
                    if (srcBefore == 1)
                    {
                        seatStats.ThreeBets++;
                        report.ThreeBetsTotal++;
                    }
                    else if (srcBefore == 2)
                    {
                        seatStats.FourBets++;
                        report.FourBetsTotal++;
                    }
                }

                if (srcBefore == 1 && facingRaise)
                    seatStats.ThreeBetOppsFacing++;

                if (srcBefore == 2 && facingRaise)
                    report.FourBetOpportunities++;

                if (facingAllIn)
                {
                    report.AllInFacingDecisions++;
                    if (counted == BettingAction.Call || action == BettingAction.AllIn)
                        report.AllInCalls++;
                }

                TryRecordSuspicious(
                    report, history, seatKey, player, hole, group, action, counted,
                    srcBefore, facingRaise, facingAllIn, callAmount, openOpportunity);

                hasActed[currentIndex] = true;
                if (betting.CurrentBet - betBefore >= minRaiseBefore)
                    ReopenActionForOthers(players, hasActed, currentIndex);

                seatIndex++;
                if (IsBettingComplete(players, hasActed))
                    return;
            }
        }

        private static void TryRecordSuspicious(
            ReportData report,
            List<string> history,
            string seatKey,
            PlayerState player,
            string hole,
            PreflopHandGroup group,
            BettingAction action,
            BettingAction counted,
            int srcBefore,
            bool facingRaise,
            bool facingAllIn,
            int callAmount,
            bool openOpportunity)
        {
            if (report.Suspicious.Count >= MaxSuspicious)
                return;

            string why = null;
            if (group == PreflopHandGroup.Weak && counted == BettingAction.Raise)
                why = "Weak hand raised";
            else if (openOpportunity && seatKey == "UTG" && group != PreflopHandGroup.Premium
                     && counted == BettingAction.Raise)
                why = "UTG open without Premium";
            else if (openOpportunity && seatKey == "MP" && group == PreflopHandGroup.Playable
                     && counted == BettingAction.Raise)
                why = "MP open with Playable (below Strong+)";
            else if (group == PreflopHandGroup.Premium && counted == BettingAction.Fold
                     && facingRaise && !facingAllIn && srcBefore == 1)
                why = "Premium folded to a single raise (non-all-in)";
            else if (facingAllIn && group == PreflopHandGroup.Weak
                     && (counted == BettingAction.Call || action == BettingAction.AllIn))
                why = "Weak called facing all-in";
            else if (group == PreflopHandGroup.Playable && counted == BettingAction.Raise
                     && srcBefore >= 1)
                why = "Playable raised facing a raise (3-bet+)";

            if (why == null)
                return;

            report.Suspicious.Add(new SuspiciousSpot
            {
                Why = why,
                Seat = seatKey,
                Hole = hole,
                Tier = group.ToString(),
                Action = action.ToString(),
                History = string.Join(" | ", history),
            });
        }

        private static string SeatKey(PreflopSeatBucket seat) =>
            seat switch
            {
                PreflopSeatBucket.Early => "UTG",
                PreflopSeatBucket.Middle => "MP",
                PreflopSeatBucket.Cutoff => "CO",
                PreflopSeatBucket.Button => "BTN",
                PreflopSeatBucket.SmallBlind => "SB",
                PreflopSeatBucket.BigBlind => "BB",
                _ => seat.ToString(),
            };

        private static string FormatHole(PlayerState p)
        {
            if (p?.HoleCards == null || p.HoleCards.Count < 2)
                return "??";
            return $"{p.HoleCards[0]} {p.HoleCards[1]}";
        }

        private static void PrintConsoleSummary(ReportData r)
        {
            Debug.Log($"[PreflopValidation] Hands={r.Hands} illegal={r.IllegalActions}");
            Debug.Log(
                $"[PreflopValidation] Tiers P={r.TierCounts[0]} S={r.TierCounts[1]} " +
                $"Pl={r.TierCounts[2]} W={r.TierCounts[3]} / {r.TierTotal}");
            Debug.Log(
                $"[PreflopValidation] 3bets={r.ThreeBetsTotal} 4bets={r.FourBetsTotal} " +
                $"allInCall={r.AllInCalls}/{r.AllInFacingDecisions}");
            foreach (string k in ReportData.SeatOrder)
            {
                SeatStats s = r.Seats[k];
                float openPct = s.OpenOpps > 0 ? 100f * s.OpenRaises / s.OpenOpps : 0f;
                float d = Math.Max(1, s.Decisions);
                Debug.Log(
                    $"[PreflopValidation] {k}: open={openPct:F1}% " +
                    $"F={100f * s.Folds / d:F1}% C={100f * s.Calls / d:F1}% " +
                    $"R={100f * s.Raises / d:F1}% 3bet={s.ThreeBets}/{s.ThreeBetOppsFacing}");
            }
        }

        private static int CountNonFolded(IReadOnlyList<PlayerState> players)
        {
            int c = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].HasFolded)
                    c++;
            }
            return c;
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

        private sealed class SeatStats
        {
            public int Decisions;
            public int Folds, Calls, Raises, Checks;
            public int OpenOpps, OpenRaises;
            public int ThreeBets, ThreeBetOppsFacing;
            public int FourBets;
        }

        private sealed class SuspiciousSpot
        {
            public string Why, Seat, Hole, Tier, Action, History;
        }

        private sealed class ReportData
        {
            public static readonly string[] SeatOrder = { "UTG", "MP", "CO", "BTN", "SB", "BB" };

            public int Hands;
            public string SeedNote;
            public int IllegalActions;
            public int[] TierCounts = new int[4];
            public int TierTotal;
            public int ThreeBetsTotal;
            public int FourBetsTotal;
            public int FourBetOpportunities;
            public int AllInCalls;
            public int AllInFacingDecisions;
            public Dictionary<string, SeatStats> Seats = CreateSeats();
            public List<SuspiciousSpot> Suspicious = new List<SuspiciousSpot>(MaxSuspicious);

            private static Dictionary<string, SeatStats> CreateSeats()
            {
                var d = new Dictionary<string, SeatStats>();
                foreach (string k in SeatOrder)
                    d[k] = new SeatStats();
                return d;
            }

            public string ToJson()
            {
                var sb = new StringBuilder(8000);
                sb.Append("{\n");
                sb.Append($"  \"hands\": {Hands},\n");
                sb.Append($"  \"illegalActions\": {IllegalActions},\n");
                sb.Append($"  \"threeBetsTotal\": {ThreeBetsTotal},\n");
                sb.Append($"  \"fourBetsTotal\": {FourBetsTotal},\n");
                sb.Append($"  \"fourBetOpportunities\": {FourBetOpportunities},\n");
                sb.Append($"  \"allInCalls\": {AllInCalls},\n");
                sb.Append($"  \"allInFacingDecisions\": {AllInFacingDecisions},\n");
                sb.Append("  \"tiers\": {");
                sb.Append($"\"Premium\": {TierCounts[0]}, \"Strong\": {TierCounts[1]}, ");
                sb.Append($"\"Playable\": {TierCounts[2]}, \"Weak\": {TierCounts[3]}, \"total\": {TierTotal}");
                sb.Append("},\n  \"seats\": {\n");
                for (int i = 0; i < SeatOrder.Length; i++)
                {
                    string k = SeatOrder[i];
                    SeatStats s = Seats[k];
                    int d = Math.Max(1, s.Decisions);
                    double openPct = s.OpenOpps > 0 ? 100.0 * s.OpenRaises / s.OpenOpps : 0;
                    double threePct = s.ThreeBetOppsFacing > 0
                        ? 100.0 * s.ThreeBets / s.ThreeBetOppsFacing : 0;
                    sb.Append($"    \"{k}\": {{");
                    sb.Append($"\"decisions\": {s.Decisions}, ");
                    sb.Append($"\"openOpps\": {s.OpenOpps}, \"openRaises\": {s.OpenRaises}, \"openPct\": {openPct.ToString("F2", CultureInfo.InvariantCulture)}, ");
                    sb.Append($"\"foldPct\": {(100.0 * s.Folds / d).ToString("F2", CultureInfo.InvariantCulture)}, \"callPct\": {(100.0 * s.Calls / d).ToString("F2", CultureInfo.InvariantCulture)}, ");
                    sb.Append($"\"raisePct\": {(100.0 * s.Raises / d).ToString("F2", CultureInfo.InvariantCulture)}, \"checkPct\": {(100.0 * s.Checks / d).ToString("F2", CultureInfo.InvariantCulture)}, ");
                    sb.Append($"\"threeBets\": {s.ThreeBets}, \"threeBetFacingOpps\": {s.ThreeBetOppsFacing}, ");
                    sb.Append($"\"threeBetPct\": {threePct.ToString("F2", CultureInfo.InvariantCulture)}, \"fourBets\": {s.FourBets}");
                    sb.Append("}");
                    sb.Append(i < SeatOrder.Length - 1 ? ",\n" : "\n");
                }
                sb.Append("  },\n  \"suspicious\": [\n");
                for (int i = 0; i < Suspicious.Count; i++)
                {
                    SuspiciousSpot sp = Suspicious[i];
                    sb.Append("    {");
                    sb.Append($"\"why\": {JsonStr(sp.Why)}, \"seat\": {JsonStr(sp.Seat)}, ");
                    sb.Append($"\"hole\": {JsonStr(sp.Hole)}, \"tier\": {JsonStr(sp.Tier)}, ");
                    sb.Append($"\"action\": {JsonStr(sp.Action)}, \"history\": {JsonStr(sp.History)}");
                    sb.Append("}");
                    sb.Append(i < Suspicious.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ]\n}\n");
                return sb.ToString();
            }

            private static string JsonStr(string s)
            {
                if (s == null)
                    return "null";
                return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
            }
        }
    }
}
