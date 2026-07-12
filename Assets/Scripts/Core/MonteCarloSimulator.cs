using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public readonly struct MonteCarloResult
    {
        public float EquityPercent { get; }
        public float WinPercent    { get; }
        public float TiePercent    { get; }
        public int   Simulations   { get; }

        public MonteCarloResult(float equityPercent, float winPercent, float tiePercent, int simulations)
        {
            EquityPercent = equityPercent;
            WinPercent    = winPercent;
            TiePercent    = tiePercent;
            Simulations   = simulations;
        }
    }

    /// <summary>
    /// Estimates Texas Hold'em equity via Monte Carlo simulation.
    /// Uses only the hero's hole cards, visible board, and active opponent count.
    /// </summary>
    public static class MonteCarloSimulator
    {
        public const int DefaultSimulationCount = 10_000;
        public const int DefaultSimsPerFrame    = 5_000;

        public static MonteCarloResult Simulate(
            IReadOnlyList<Card> heroHoleCards,
            IReadOnlyList<Card> communityCards,
            int activeOpponentCount,
            int simulationCount = DefaultSimulationCount)
        {
            ValidateInputs(heroHoleCards, communityCards, activeOpponentCount, simulationCount);

            var workspace = new SimulationWorkspace();
            workspace.Initialize(heroHoleCards, communityCards);

            int wins   = 0;
            int ties   = 0;
            int losses = 0;
            double equitySum = 0d;

            for (int sim = 0; sim < simulationCount; sim++)
                workspace.RunOneSimulation(activeOpponentCount, ref wins, ref ties, ref losses, ref equitySum);

            return BuildResult(wins, ties, losses, equitySum, simulationCount);
        }

        /// <summary>Spreads simulation work across frames so the main thread stays responsive.</summary>
        public static IEnumerator SimulateOverFrames(
            IReadOnlyList<Card> heroHoleCards,
            IReadOnlyList<Card> communityCards,
            int activeOpponentCount,
            Action<MonteCarloResult> onComplete,
            int simulationCount = DefaultSimulationCount,
            int simsPerFrame = DefaultSimsPerFrame)
        {
            if (onComplete == null)
                throw new ArgumentNullException(nameof(onComplete));

            ValidateInputs(heroHoleCards, communityCards, activeOpponentCount, simulationCount);

            var workspace = new SimulationWorkspace();
            workspace.Initialize(heroHoleCards, communityCards);

            int wins   = 0;
            int ties   = 0;
            int losses = 0;
            double equitySum = 0d;

            int batch = Mathf.Max(1, simsPerFrame);

            for (int sim = 0; sim < simulationCount; sim++)
            {
                workspace.RunOneSimulation(activeOpponentCount, ref wins, ref ties, ref losses, ref equitySum);

                if ((sim + 1) % batch == 0)
                    yield return null;
            }

            onComplete(BuildResult(wins, ties, losses, equitySum, simulationCount));
        }

        private static MonteCarloResult BuildResult(
            int wins, int ties, int losses, double equitySum, int simulationCount)
        {
            float n = simulationCount;
            return new MonteCarloResult(
                (float)(equitySum / n * 100d),
                wins / n * 100f,
                ties / n * 100f,
                simulationCount);
        }

        private static void ValidateInputs(
            IReadOnlyList<Card> heroHoleCards,
            IReadOnlyList<Card> communityCards,
            int activeOpponentCount,
            int simulationCount)
        {
            if (heroHoleCards == null || heroHoleCards.Count != 2)
                throw new ArgumentException("Hero must have exactly 2 hole cards.", nameof(heroHoleCards));

            if (communityCards == null || communityCards.Count > 5)
                throw new ArgumentException("Board must have 0–5 cards.", nameof(communityCards));

            if (activeOpponentCount < 0)
                throw new ArgumentException("Opponent count cannot be negative.", nameof(activeOpponentCount));

            if (simulationCount <= 0)
                throw new ArgumentException("Simulation count must be positive.", nameof(simulationCount));

            var seen = new HashSet<(Suit, Rank)>();
            foreach (Card card in heroHoleCards)
            {
                if (card == null || !seen.Add((card.Suit, card.Rank)))
                    throw new ArgumentException("Duplicate or null known card.", nameof(heroHoleCards));
            }

            foreach (Card card in communityCards)
            {
                if (card == null || !seen.Add((card.Suit, card.Rank)))
                    throw new ArgumentException("Duplicate or null known card.", nameof(communityCards));
            }

            int needed = activeOpponentCount * 2 + (5 - communityCards.Count);
            if (52 - seen.Count < needed)
                throw new ArgumentException("Not enough unknown cards for the simulation.");
        }

        /// <summary>Reused buffers: deck built once, partial shuffle per simulation, no LINQ hand eval.</summary>
        private sealed class SimulationWorkspace
        {
            private readonly Card[] _deck = new Card[52];
            private readonly Card[] _board = new Card[5];
            private readonly Card[] _opponentHoleA = new Card[8];
            private readonly Card[] _opponentHoleB = new Card[8];

            private Card _hero0;
            private Card _hero1;
            private int  _deckCount;
            private int  _boardKnown;
            private int  _cardsNeeded;

            public void Initialize(IReadOnlyList<Card> heroHoleCards, IReadOnlyList<Card> communityCards)
            {
                _hero0 = heroHoleCards[0];
                _hero1 = heroHoleCards[1];

                _boardKnown = communityCards.Count;
                for (int i = 0; i < _boardKnown; i++)
                    _board[i] = communityCards[i];

                var known = new HashSet<(Suit, Rank)>();
                known.Add((_hero0.Suit, _hero0.Rank));
                known.Add((_hero1.Suit, _hero1.Rank));
                for (int i = 0; i < _boardKnown; i++)
                    known.Add((_board[i].Suit, _board[i].Rank));

                _deckCount = 0;
                foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                {
                    foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    {
                        if (known.Contains((suit, rank)))
                            continue;

                        _deck[_deckCount++] = new Card(suit, rank);
                    }
                }
            }

            public void RunOneSimulation(
                int activeOpponentCount,
                ref int wins,
                ref int ties,
                ref int losses,
                ref double equitySum)
            {
                _cardsNeeded = activeOpponentCount * 2 + (5 - _boardKnown);
                if (_deckCount < _cardsNeeded)
                    return;

                PartialShuffle(_cardsNeeded);

                int index = 0;
                for (int o = 0; o < activeOpponentCount; o++)
                {
                    _opponentHoleA[o] = _deck[index++];
                    _opponentHoleB[o] = _deck[index++];
                }

                for (int i = _boardKnown; i < 5; i++)
                    _board[i] = _deck[index++];

                Card b0 = _board[0];
                Card b1 = _board[1];
                Card b2 = _board[2];
                Card b3 = _board[3];
                Card b4 = _board[4];

                HandScore heroScore = HandEvaluatorFast.EvaluateSeven(_hero0, _hero1, b0, b1, b2, b3, b4);
                HandScore bestScore = heroScore;
                int       playersAtBest = 1;

                for (int o = 0; o < activeOpponentCount; o++)
                {
                    HandScore score = HandEvaluatorFast.EvaluateSeven(
                        _opponentHoleA[o], _opponentHoleB[o], b0, b1, b2, b3, b4);

                    int cmp = score.CompareTo(bestScore);
                    if (cmp > 0)
                    {
                        bestScore      = score;
                        playersAtBest  = 1;
                    }
                    else if (cmp == 0)
                    {
                        playersAtBest++;
                    }
                }

                int heroCmp = heroScore.CompareTo(bestScore);
                if (heroCmp < 0)
                {
                    losses++;
                    return;
                }

                if (playersAtBest == 1)
                {
                    wins++;
                    equitySum += 1d;
                }
                else
                {
                    ties++;
                    equitySum += 1d / playersAtBest;
                }
            }

            private void PartialShuffle(int cardsNeeded)
            {
                for (int i = 0; i < cardsNeeded; i++)
                {
                    int j = UnityEngine.Random.Range(i, _deckCount);
                    (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
                }
            }
        }
    }
}
