using System;
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

        public static MonteCarloResult Simulate(
            IReadOnlyList<Card> heroHoleCards,
            IReadOnlyList<Card> communityCards,
            int activeOpponentCount,
            int simulationCount = DefaultSimulationCount)
        {
            ValidateInputs(heroHoleCards, communityCards, activeOpponentCount, simulationCount);

            var known = new List<Card>(7);
            known.AddRange(heroHoleCards);
            known.AddRange(communityCards);

            int boardCardsNeeded = 5 - communityCards.Count;
            int cardsNeeded      = activeOpponentCount * 2 + boardCardsNeeded;

            int wins      = 0;
            int ties      = 0;
            int losses    = 0;
            double equitySum = 0d;

            for (int sim = 0; sim < simulationCount; sim++)
            {
                List<Card> remaining = BuildRemainingDeck(known);
                if (remaining.Count < cardsNeeded)
                    continue;

                Shuffle(remaining);

                int index = 0;
                var opponentHoles = new List<Card>[activeOpponentCount];
                for (int o = 0; o < activeOpponentCount; o++)
                {
                    opponentHoles[o] = new List<Card>(2)
                    {
                        remaining[index++],
                        remaining[index++]
                    };
                }

                var board = new List<Card>(5);
                board.AddRange(communityCards);
                while (board.Count < 5)
                    board.Add(remaining[index++]);

                HandResult heroResult = EvaluateHand(heroHoleCards, board);

                var allResults = new List<HandResult>(1 + activeOpponentCount) { heroResult };
                for (int o = 0; o < activeOpponentCount; o++)
                    allResults.Add(EvaluateHand(opponentHoles[o], board));

                HandResult bestResult = heroResult;
                foreach (HandResult result in allResults)
                {
                    if (result.CompareTo(bestResult) > 0)
                        bestResult = result;
                }

                int playersAtBest = 0;
                foreach (HandResult result in allResults)
                {
                    if (result.CompareTo(bestResult) == 0)
                        playersAtBest++;
                }

                if (heroResult.CompareTo(bestResult) < 0)
                {
                    losses++;
                    continue;
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

            float n = simulationCount;
            return new MonteCarloResult(
                (float)(equitySum / n * 100d),
                wins / n * 100f,
                ties / n * 100f,
                simulationCount);
        }

        private static HandResult EvaluateHand(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> board)
        {
            var cards = new List<Card>(7);
            cards.AddRange(holeCards);
            cards.AddRange(board);
            return HandEvaluator.Evaluate(cards);
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

        private static List<Card> BuildRemainingDeck(IReadOnlyList<Card> known)
        {
            var knownSet = new HashSet<(Suit, Rank)>();
            foreach (Card card in known)
                knownSet.Add((card.Suit, card.Rank));

            var remaining = new List<Card>(52 - known.Count);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                {
                    if (!knownSet.Contains((suit, rank)))
                        remaining.Add(new Card(suit, rank));
                }
            }

            return remaining;
        }

        private static void Shuffle(List<Card> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
