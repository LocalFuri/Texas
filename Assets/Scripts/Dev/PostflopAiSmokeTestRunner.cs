using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Smoke test: randomized flop/turn/river decisions through production
    /// <see cref="AIController.DecideAction"/> without changing AI logic.
    /// </summary>
    public sealed class PostflopAiSmokeTestRunner : MonoBehaviour
    {
        private const int DefaultDecisionCount = 10_000;
        private const int StartingChips        = 1000;
        private const int SmallBlind           = 10;
        private const int BigBlind             = 20;

        [SerializeField] private int _decisionCount = DefaultDecisionCount;

        [ContextMenu("Run Postflop AI Smoke Test")]
        private void RunFromContextMenu() => RunAllTests(_decisionCount);

        public static (bool ok, SmokeStats stats) RunAllTests(int decisionCount = DefaultDecisionCount)
        {
            if (decisionCount < 1)
                decisionCount = DefaultDecisionCount;

            var stats = new SmokeStats { DecisionsTarget = decisionCount };
            var rng = new System.Random(42);

            Debug.Log($"[PostflopSmoke] Running {decisionCount} randomized postflop decision(s)...");

            for (int i = 0; i < decisionCount; i++)
            {
                try
                {
                    string failure = RunOneDecision(rng, stats);
                    if (failure != null)
                    {
                        stats.IllegalActions++;
                        stats.LastError = $"decision={i}: {failure}";
                        Debug.LogError($"[PostflopSmoke] Illegal — {stats.LastError}");
                        continue;
                    }

                    stats.DecisionsCompleted++;
                }
                catch (Exception ex)
                {
                    stats.Exceptions++;
                    stats.LastError = $"decision={i}: {ex.GetType().Name}: {ex.Message}";
                    Debug.LogError($"[PostflopSmoke] Exception — {stats.LastError}");
                }
            }

            PrintSummary(stats);

            bool ok = stats.Exceptions == 0 && stats.IllegalActions == 0;
            if (!ok)
                Debug.LogError("[PostflopSmoke] FAIL — exceptions or illegal actions detected.");
            else
                Debug.Log("[PostflopSmoke] PASS");

            return (ok, stats);
        }

        public static void PrintSummary(SmokeStats stats)
        {
            Debug.Log(
                "[PostflopSmoke] Summary\n" +
                $"  Decisions completed:  {stats.DecisionsCompleted}\n" +
                $"  Exceptions:           {stats.Exceptions}\n" +
                $"  Illegal actions:      {stats.IllegalActions}\n" +
                $"  Fold:                 {stats.Folds}\n" +
                $"  Check:                {stats.Checks}\n" +
                $"  Call:                 {stats.Calls}\n" +
                $"  Raise:                {stats.Raises}\n" +
                $"  By street Flop:       {stats.FlopDecisions}\n" +
                $"  By street Turn:       {stats.TurnDecisions}\n" +
                $"  By street River:      {stats.RiverDecisions}");
        }

        private static string RunOneDecision(System.Random rng, SmokeStats stats)
        {
            GamePhase street = PickStreet(rng);
            int boardCount = street == GamePhase.Flop ? 3
                : street == GamePhase.Turn ? 4
                : 5;

            int opponentCount = rng.Next(1, 4); // 1–3 opponents
            DealResult deal = DealUniqueCards(rng, boardCount, opponentCount);

            var hero = new PlayerState("Hero", PlayerType.AI, StartingChips);
            hero.HoleCards.Add(deal.Hero0);
            hero.HoleCards.Add(deal.Hero1);

            var players = new List<PlayerState> { hero };
            for (int o = 0; o < opponentCount; o++)
            {
                var opp = new PlayerState($"Opp{o}", PlayerType.AI, StartingChips);
                opp.HoleCards.Add(deal.OppHoles[o * 2]);
                opp.HoleCards.Add(deal.OppHoles[o * 2 + 1]);
                players.Add(opp);
            }

            var betting = new BettingManager(SmallBlind, BigBlind);
            betting.ResetRound();

            // Seed pot via blinds, then start a new street (postflop CurrentBet = 0).
            betting.PostSmallBlind(players[1]);
            betting.PostBigBlind(hero);
            betting.ResetPhase();
            foreach (PlayerState p in players)
                p.CurrentBet = 0;

            bool facingBet = rng.Next(0, 2) == 1;
            if (facingBet)
            {
                PlayerState villain = players[1];
                int minInc = betting.GetMinRaiseIncrement();
                int potNow = betting.Pot;
                int openSize = Mathf.Max(minInc, Mathf.RoundToInt(Mathf.Max(1, potNow) * 0.67f));
                openSize = Mathf.Min(openSize, villain.Chips);
                if (openSize < minInc)
                {
                    // Can't open legally — fall back to checked-to.
                    facingBet = false;
                }
                else if (!betting.ProcessAction(villain, BettingAction.Raise, openSize))
                {
                    return $"setup villain open rejected size={openSize} pot={potNow}";
                }
            }

            var ai = new AIController();
            ai.ClearHandState();
            if (street == GamePhase.Turn && rng.Next(0, 2) == 0)
                ai.NoteFlopAggression(hero);

            int potBefore = betting.Pot;
            int tableBet = betting.CurrentBet;
            int callAmount = betting.GetCallAmount(hero);
            int chipsBefore = hero.Chips;
            int maxRaiseInc = betting.GetMaxRaiseIncrement(hero);

            var (action, raiseAmount) = ai.DecideAction(
                hero,
                deal.Board,
                betting,
                players,
                street,
                potBefore,
                tableBet,
                BigBlind,
                betting.StreetRaiseCount,
                PreflopSeatBucket.Button,
                testMode: false);

            if (raiseAmount < 0)
                return $"negative raiseAmount={raiseAmount} action={action}";

            if (action == BettingAction.Raise)
            {
                if (raiseAmount > maxRaiseInc)
                {
                    return $"raise {raiseAmount} exceeds available stack increment {maxRaiseInc} " +
                           $"(chips={chipsBefore} call={callAmount})";
                }

                if (raiseAmount > chipsBefore)
                    return $"raise {raiseAmount} > chips {chipsBefore}";
            }

            if (action == BettingAction.Call && callAmount > chipsBefore)
                return $"Call callAmount={callAmount} > chips={chipsBefore}";

            if (action == BettingAction.AllIn)
            {
                // All-in must not claim more than remaining chips.
                if (chipsBefore <= 0)
                    return "AllIn with no chips";
            }

            if (!betting.ProcessAction(hero, action, raiseAmount))
            {
                return $"illegal action rejected: {action} raise={raiseAmount} " +
                       $"call={callAmount} chips={chipsBefore} table={tableBet}";
            }

            CountAction(stats, action, street);
            return null;
        }

        private static void CountAction(SmokeStats stats, BettingAction action, GamePhase street)
        {
            switch (street)
            {
                case GamePhase.Flop:  stats.FlopDecisions++; break;
                case GamePhase.Turn:  stats.TurnDecisions++; break;
                case GamePhase.River: stats.RiverDecisions++; break;
            }

            switch (action)
            {
                case BettingAction.Fold:  stats.Folds++; break;
                case BettingAction.Check: stats.Checks++; break;
                case BettingAction.Call:  stats.Calls++; break;
                case BettingAction.Raise: stats.Raises++; break;
                case BettingAction.AllIn:
                    // Treat jam as Raise for summary (aggressive commitment).
                    stats.Raises++;
                    break;
            }
        }

        private static GamePhase PickStreet(System.Random rng)
        {
            int roll = rng.Next(0, 3);
            if (roll == 0) return GamePhase.Flop;
            if (roll == 1) return GamePhase.Turn;
            return GamePhase.River;
        }

        private static DealResult DealUniqueCards(System.Random rng, int boardCount, int opponentCount)
        {
            var deck = new List<Card>(52);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    deck.Add(new Card(suit, rank));
            }

            // Fisher–Yates
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            int need = 2 + boardCount + opponentCount * 2;
            if (deck.Count < need)
                throw new InvalidOperationException("Deck too small for deal.");

            int idx = 0;
            Card hero0 = deck[idx++];
            Card hero1 = deck[idx++];

            var board = new List<Card>(boardCount);
            for (int b = 0; b < boardCount; b++)
                board.Add(deck[idx++]);

            var oppHoles = new List<Card>(opponentCount * 2);
            for (int o = 0; o < opponentCount * 2; o++)
                oppHoles.Add(deck[idx++]);

            return new DealResult(hero0, hero1, board, oppHoles);
        }

        private readonly struct DealResult
        {
            public Card Hero0 { get; }
            public Card Hero1 { get; }
            public IReadOnlyList<Card> Board { get; }
            public IReadOnlyList<Card> OppHoles { get; }

            public DealResult(Card hero0, Card hero1, IReadOnlyList<Card> board, IReadOnlyList<Card> oppHoles)
            {
                Hero0 = hero0;
                Hero1 = hero1;
                Board = board;
                OppHoles = oppHoles;
            }
        }

        public sealed class SmokeStats
        {
            public int DecisionsTarget;
            public int DecisionsCompleted;
            public int Exceptions;
            public int IllegalActions;
            public int Folds;
            public int Checks;
            public int Calls;
            public int Raises;
            public int FlopDecisions;
            public int TurnDecisions;
            public int RiverDecisions;
            public string LastError;
        }
    }
}
