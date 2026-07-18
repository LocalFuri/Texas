using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TexasHoldem
{
    /// <summary>
    /// How tightly to sample unknown opponent holes from betting aggression.
    /// Wide ≈ check/call; Strong ≈ bet/raise; Strongest ≈ re-raise / near-stack shove.
    /// </summary>
    public enum OpponentRangeStrength
    {
        Wide      = 0,
        Strong    = 1,
        Strongest = 2,
    }

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
    /// Opponent holes can be rejection-sampled to a betting-implied range.
    /// </summary>
    public static class MonteCarloSimulator
    {
        public const int DefaultSimulationCount = 10_000;
        /// <summary>Above default sim count so 10k runs without a trailing frame yield.</summary>
        public const int DefaultSimsPerFrame    = 15_000;

        private const int MaxOpponentHandAttempts = 24;
        private const float NearStackCallFraction = 0.5f;

        private static readonly int[] PerformanceBenchmarkSimulationCounts = { 10_000, 100_000, 1_000_000 };
        private const int PerformanceBenchmarkOpponentCount = 2;

        /// <summary>
        /// Same near-stack gate used by <see cref="ResolveOpponentRange"/> (≥50% of defender stack).
        /// </summary>
        public static bool IsNearStackCall(int callAmount, int defenderChips) =>
            defenderChips > 0
            && callAmount > 0
            && callAmount >= Mathf.CeilToInt(defenderChips * NearStackCallFraction);

        /// <summary>
        /// Maps facing-bet context to an opponent range tier, floored by preflop aggression.
        /// Street: check → Wide; bet/raise → Strong; re-raise or ≥50% stack call → Strongest.
        /// Preflop floor: 3-bet pot → Strong; 4-bet+ pot → Strongest. Never widens below that floor.
        /// </summary>
        /// <param name="preflopRaiseCount">
        /// Raises completed preflop (open=1, 3-bet pot=2, 4-bet pot=3).
        /// </param>
        public static OpponentRangeStrength ResolveOpponentRange(
            bool facingBet,
            int streetRaiseCount,
            int callAmount,
            int defenderChips,
            int preflopRaiseCount = 0)
        {
            OpponentRangeStrength street = ResolveStreetOpponentRange(
                facingBet, streetRaiseCount, callAmount, defenderChips);
            OpponentRangeStrength floor = ResolvePreflopRangeFloor(preflopRaiseCount);
            return street >= floor ? street : floor;
        }

        /// <summary>Preflop pot-type floor only (Wide / Strong / Strongest).</summary>
        public static OpponentRangeStrength ResolvePreflopRangeFloor(int preflopRaiseCount)
        {
            if (preflopRaiseCount >= 3)
                return OpponentRangeStrength.Strongest;
            if (preflopRaiseCount >= 2)
                return OpponentRangeStrength.Strong;
            return OpponentRangeStrength.Wide;
        }

        private static OpponentRangeStrength ResolveStreetOpponentRange(
            bool facingBet,
            int streetRaiseCount,
            int callAmount,
            int defenderChips)
        {
            if (!facingBet || callAmount <= 0)
                return OpponentRangeStrength.Wide;

            bool nearStack = IsNearStackCall(callAmount, defenderChips);

            if (streetRaiseCount >= 2 || nearStack)
                return OpponentRangeStrength.Strongest;

            return OpponentRangeStrength.Strong;
        }

        /// <summary>Logging helper: same tier as <see cref="ResolveOpponentRange"/> plus a short why string.</summary>
        public static string DescribeOpponentRangeSelection(
            bool facingBet,
            int streetRaiseCount,
            int callAmount,
            int defenderChips,
            out OpponentRangeStrength range,
            int preflopRaiseCount = 0)
        {
            OpponentRangeStrength street = ResolveStreetOpponentRange(
                facingBet, streetRaiseCount, callAmount, defenderChips);
            OpponentRangeStrength floor = ResolvePreflopRangeFloor(preflopRaiseCount);
            range = street >= floor ? street : floor;

            string streetWhy;
            if (!facingBet || callAmount <= 0)
                streetWhy = "check/call (no bet faced)";
            else if (streetRaiseCount >= 2)
                streetWhy = $"re-raise (streetRaiseCount={streetRaiseCount})";
            else if (IsNearStackCall(callAmount, defenderChips))
            {
                streetWhy =
                    $"near-stack shove/call call={callAmount} ≥50% stack chips={defenderChips}";
            }
            else
            {
                streetWhy = $"bet/raise (streetRaiseCount={streetRaiseCount})";
            }

            if (floor > street)
            {
                string pot =
                    floor == OpponentRangeStrength.Strongest ? "4-bet+ pot" :
                    floor == OpponentRangeStrength.Strong ? "3-bet pot" : "preflop";
                return $"{streetWhy}; floored to {floor} by {pot} (preflopRaises={preflopRaiseCount})";
            }

            if (preflopRaiseCount >= 2)
                return $"{streetWhy}; preflopRaises={preflopRaiseCount} floor={floor}";

            return streetWhy;
        }

        public static MonteCarloResult Simulate(
            IReadOnlyList<Card> heroHoleCards,
            IReadOnlyList<Card> communityCards,
            int activeOpponentCount,
            int simulationCount = DefaultSimulationCount,
            OpponentRangeStrength opponentRange = OpponentRangeStrength.Wide)
        {
            ValidateInputs(heroHoleCards, communityCards, activeOpponentCount, simulationCount);

            var workspace = new SimulationWorkspace();
            workspace.Initialize(heroHoleCards, communityCards);

            int wins   = 0;
            int ties   = 0;
            int losses = 0;
            double equitySum = 0d;

            for (int sim = 0; sim < simulationCount; sim++)
            {
                workspace.RunOneSimulation(
                    activeOpponentCount, opponentRange,
                    ref wins, ref ties, ref losses, ref equitySum);
            }

            return BuildResult(wins, ties, losses, equitySum, simulationCount);
        }

        /// <summary>Spreads simulation work across frames so the main thread stays responsive.</summary>
        public static IEnumerator SimulateOverFrames(
            IReadOnlyList<Card> heroHoleCards,
            IReadOnlyList<Card> communityCards,
            int activeOpponentCount,
            Action<MonteCarloResult> onComplete,
            int simulationCount = DefaultSimulationCount,
            int simsPerFrame = DefaultSimsPerFrame,
            bool logPerformance = false,
            string performanceLogContext = null,
            OpponentRangeStrength opponentRange = OpponentRangeStrength.Wide)
        {
            if (onComplete == null)
                throw new ArgumentNullException(nameof(onComplete));

            var stopwatch = Stopwatch.StartNew();
            int startFrame = Time.frameCount;

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
                workspace.RunOneSimulation(
                    activeOpponentCount, opponentRange,
                    ref wins, ref ties, ref losses, ref equitySum);

                if ((sim + 1) % batch == 0)
                    yield return null;
            }

            stopwatch.Stop();
            int renderedFrames = Time.frameCount - startFrame + 1;

            if (logPerformance)
            {
                string prefix = string.IsNullOrEmpty(performanceLogContext)
                    ? "[MonteCarlo]"
                    : $"[MonteCarlo] {performanceLogContext}";

                Debug.Log(
                    $"{prefix} simulations={simulationCount:N0} " +
                    $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:F2} " +
                    $"renderedFrames={renderedFrames} " +
                    $"simsPerFrame={batch} " +
                    $"activeOpponents={activeOpponentCount} " +
                    $"range={opponentRange}");
            }

            onComplete(BuildResult(wins, ties, losses, equitySum, simulationCount));
        }

        /// <summary>Runs 10k / 100k / 1M simulation timing passes and logs results to the Console.</summary>
        public static IEnumerator RunPerformanceBenchmark()
        {
            var heroHoleCards = new Card[]
            {
                new Card(Suit.Spades, Rank.Ace),
                new Card(Suit.Hearts, Rank.King)
            };

            var communityCards = new Card[]
            {
                new Card(Suit.Diamonds, Rank.Ten),
                new Card(Suit.Clubs, Rank.Jack),
                new Card(Suit.Hearts, Rank.Two)
            };

            Debug.Log(
                $"[MonteCarlo] Starting performance benchmark " +
                $"(simsPerFrame={DefaultSimsPerFrame}, activeOpponents={PerformanceBenchmarkOpponentCount}).");

            foreach (int simulationCount in PerformanceBenchmarkSimulationCounts)
            {
                MonteCarloResult result = default;

                yield return SimulateOverFrames(
                    heroHoleCards,
                    communityCards,
                    PerformanceBenchmarkOpponentCount,
                    r => result = r,
                    simulationCount,
                    DefaultSimsPerFrame,
                    logPerformance: true);

                Debug.Log(
                    $"[MonteCarlo] benchmark result equity={result.EquityPercent:F2}% " +
                    $"simulations={result.Simulations:N0}");
            }

            Debug.Log("[MonteCarlo] Performance benchmark complete.");
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

        /// <summary>
        /// Dev/test: % of unknown hole combos that pass Strong before vs after wet turn/river tightening.
        /// Legacy = OnePair+ or any FD/OESD on hole+board ranks (pre-tightening Strong).
        /// </summary>
        public static void MeasureStrongAcceptanceBeforeAfter(
            IReadOnlyList<Card> heroHoleCards,
            IReadOnlyList<Card> communityCards,
            out float legacyStrongPercent,
            out float currentStrongPercent)
        {
            ValidateInputs(heroHoleCards, communityCards, activeOpponentCount: 1, simulationCount: 1);
            var workspace = new SimulationWorkspace();
            workspace.Initialize(heroHoleCards, communityCards);
            legacyStrongPercent  = workspace.MeasureAcceptancePercent(
                OpponentRangeStrength.Strong, legacyStrong: true);
            currentStrongPercent = workspace.MeasureAcceptancePercent(
                OpponentRangeStrength.Strong, legacyStrong: false);
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
            private readonly int[] _rankCounts = new int[15];
            private readonly int[] _suitCounts = new int[4];
            private readonly bool[] _rankPresent = new bool[15];

            private Card _hero0;
            private Card _hero1;
            private int  _deckCount;
            private int  _boardKnown;
            /// <summary>Turn/river paired or connected: use tightened Strong filter.</summary>
            private bool _tightenStrongOnPairedOrConnected;

            public void Initialize(IReadOnlyList<Card> heroHoleCards, IReadOnlyList<Card> communityCards)
            {
                _hero0 = heroHoleCards[0];
                _hero1 = heroHoleCards[1];

                _boardKnown = communityCards.Count;
                for (int i = 0; i < _boardKnown; i++)
                    _board[i] = communityCards[i];

                _tightenStrongOnPairedOrConnected = ShouldTightenStrongRange(communityCards);

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

            private static bool ShouldTightenStrongRange(IReadOnlyList<Card> communityCards)
            {
                if (communityCards == null || communityCards.Count < 4)
                    return false;

                BoardTextureFlags flags = BoardTextureAnalyzer.Analyze(communityCards);
                bool paired = (flags & (BoardTextureFlags.Paired
                    | BoardTextureFlags.TwoPair
                    | BoardTextureFlags.Trips)) != 0;
                bool connected = (flags & (BoardTextureFlags.Connected
                    | BoardTextureFlags.FourStraight)) != 0;
                return paired || connected;
            }

            /// <summary>
            /// Fraction of unknown hole combos (deck after hero+board removed) that pass the filter.
            /// </summary>
            public float MeasureAcceptancePercent(OpponentRangeStrength range, bool legacyStrong)
            {
                int total = 0;
                int fit   = 0;

                for (int i = 0; i < _deckCount; i++)
                {
                    for (int j = i + 1; j < _deckCount; j++)
                    {
                        total++;
                        bool accepts = legacyStrong
                            ? FitsLegacyStrong(_deck[i], _deck[j])
                            : FitsRange(_deck[i], _deck[j], range);

                        if (accepts)
                            fit++;
                    }
                }

                return total == 0 ? 0f : 100f * fit / total;
            }

            public void RunOneSimulation(
                int activeOpponentCount,
                OpponentRangeStrength opponentRange,
                ref int wins,
                ref int ties,
                ref int losses,
                ref double equitySum)
            {
                int cardsNeeded = activeOpponentCount * 2 + (5 - _boardKnown);
                if (_deckCount < cardsNeeded)
                    return;

                int remaining = _deckCount;

                for (int o = 0; o < activeOpponentCount; o++)
                {
                    DealOpponentHole(ref remaining, opponentRange, o);
                }

                for (int i = _boardKnown; i < 5; i++)
                {
                    int pick = UnityEngine.Random.Range(0, remaining);
                    _board[i] = _deck[pick];
                    SwapDeck(pick, remaining - 1);
                    remaining--;
                }

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
                        bestScore     = score;
                        playersAtBest = 1;
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

            private void DealOpponentHole(
                ref int remaining,
                OpponentRangeStrength opponentRange,
                int opponentIndex)
            {
                if (remaining < 2)
                    return;

                if (opponentRange == OpponentRangeStrength.Wide || _boardKnown < 3)
                {
                    TakeTwoRandomHoles(ref remaining, opponentIndex);
                    return;
                }

                for (int attempt = 0; attempt < MaxOpponentHandAttempts; attempt++)
                {
                    int i = UnityEngine.Random.Range(0, remaining);
                    int j = UnityEngine.Random.Range(0, remaining - 1);
                    if (j >= i)
                        j++;

                    Card a = _deck[i];
                    Card b = _deck[j];
                    if (!FitsRange(a, b, opponentRange))
                        continue;

                    int hi = i > j ? i : j;
                    int lo = i > j ? j : i;
                    SwapDeck(hi, remaining - 1);
                    SwapDeck(lo, remaining - 2);

                    _opponentHoleA[opponentIndex] = _deck[remaining - 1];
                    _opponentHoleB[opponentIndex] = _deck[remaining - 2];
                    remaining -= 2;
                    return;
                }

                TakeTwoRandomHoles(ref remaining, opponentIndex);
            }

            private void TakeTwoRandomHoles(ref int remaining, int opponentIndex)
            {
                int i = UnityEngine.Random.Range(0, remaining);
                SwapDeck(i, remaining - 1);
                _opponentHoleA[opponentIndex] = _deck[remaining - 1];
                remaining--;

                int j = UnityEngine.Random.Range(0, remaining);
                SwapDeck(j, remaining - 1);
                _opponentHoleB[opponentIndex] = _deck[remaining - 1];
                remaining--;
            }

            private void SwapDeck(int a, int b)
            {
                if (a == b)
                    return;
                (_deck[a], _deck[b]) = (_deck[b], _deck[a]);
            }

            /// <summary>
            /// Wide: any hand.
            /// Strong (flop / dry): OnePair+ / FD / OESD.
            /// Strong (turn/river paired or connected): TwoPair+, hole-contributed OnePair,
            ///   credible FD (hole suit), or FD+OESD combo — not pure board-pair or bare OESD.
            /// Strongest: TwoPair+, top pair / overpair, made flush-straight, or FD/OESD.
            /// </summary>
            private bool FitsRange(Card h0, Card h1, OpponentRangeStrength range)
            {
                ClassifyHoleOnBoard(h0, h1,
                    out HandRank made,
                    out bool topPairOrOverpair,
                    out bool strongDraw,
                    out _,
                    out bool madeFlush);

                if (range == OpponentRangeStrength.Strongest)
                {
                    return made >= HandRank.TwoPair
                        || topPairOrOverpair
                        || strongDraw;
                }

                if (range == OpponentRangeStrength.Wide)
                    return true;

                // Strong
                if (!_tightenStrongOnPairedOrConnected)
                    return made >= HandRank.OnePair || strongDraw;

                if (made >= HandRank.TwoPair)
                    return true;

                if (made == HandRank.OnePair && HoleContributesPair(h0, h1))
                    return true;

                // Draws only before river; bare OESDs never qualify on these boards.
                if (_boardKnown < 5 && HasCredibleFlushDraw(h0, h1, madeFlush))
                    return true;

                return false;
            }

            /// <summary>Pre-tightening Strong: OnePair+ or any hole+board FD/OESD.</summary>
            private bool FitsLegacyStrong(Card h0, Card h1)
            {
                ClassifyHoleOnBoard(h0, h1,
                    out HandRank made,
                    out _,
                    out bool strongDraw,
                    out _,
                    out _);
                return made >= HandRank.OnePair || strongDraw;
            }

            private bool HoleContributesPair(Card h0, Card h1)
            {
                if (h0.Rank == h1.Rank)
                    return true;

                for (int i = 0; i < _boardKnown; i++)
                {
                    Rank boardRank = _board[i].Rank;
                    if (h0.Rank == boardRank || h1.Rank == boardRank)
                        return true;
                }

                return false;
            }

            /// <summary>Exactly 4 to a suit with ≥1 hole card of that suit (not a made flush).</summary>
            private bool HasCredibleFlushDraw(Card h0, Card h1, bool madeFlush)
            {
                if (madeFlush)
                    return false;

                Array.Clear(_suitCounts, 0, _suitCounts.Length);
                for (int i = 0; i < _boardKnown; i++)
                    _suitCounts[(int)_board[i].Suit]++;

                int s0 = (int)h0.Suit;
                int s1 = (int)h1.Suit;
                _suitCounts[s0]++;
                _suitCounts[s1]++;

                for (int s = 0; s < 4; s++)
                {
                    if (_suitCounts[s] != 4)
                        continue;

                    int holeOfSuit = (s0 == s ? 1 : 0) + (s1 == s ? 1 : 0);
                    if (holeOfSuit >= 1)
                        return true;
                }

                return false;
            }

            private void ClassifyHoleOnBoard(
                Card h0,
                Card h1,
                out HandRank made,
                out bool topPairOrOverpair,
                out bool strongDraw,
                out bool openEnded,
                out bool madeFlush)
            {
                Array.Clear(_rankCounts, 0, _rankCounts.Length);
                Array.Clear(_suitCounts, 0, _suitCounts.Length);
                Array.Clear(_rankPresent, 0, _rankPresent.Length);

                void Add(Card c)
                {
                    int r = (int)c.Rank;
                    _rankCounts[r]++;
                    _suitCounts[(int)c.Suit]++;
                    _rankPresent[r] = true;
                }

                Add(h0);
                Add(h1);
                for (int i = 0; i < _boardKnown; i++)
                    Add(_board[i]);

                int quads = 0, trips = 0, pairs = 0;
                for (int r = (int)Rank.Two; r <= (int)Rank.Ace; r++)
                {
                    int c = _rankCounts[r];
                    if (c >= 4) quads++;
                    else if (c == 3) trips++;
                    else if (c == 2) pairs++;
                }

                madeFlush = false;
                bool flushDraw = false;
                for (int s = 0; s < 4; s++)
                {
                    if (_suitCounts[s] >= 5)
                        madeFlush = true;
                    else if (_suitCounts[s] == 4)
                        flushDraw = true;
                }

                bool madeStraight = HasStraight(_rankPresent);
                openEnded = !madeStraight && HasOpenEndedStraightDraw(_rankPresent);

                if (quads > 0)
                    made = HandRank.FourOfAKind;
                else if (trips > 0 && pairs > 0)
                    made = HandRank.FullHouse;
                else if (madeFlush && madeStraight)
                    made = HandRank.StraightFlush;
                else if (madeFlush)
                    made = HandRank.Flush;
                else if (madeStraight)
                    made = HandRank.Straight;
                else if (trips > 0)
                    made = HandRank.ThreeOfAKind;
                else if (pairs >= 2)
                    made = HandRank.TwoPair;
                else if (pairs == 1)
                    made = HandRank.OnePair;
                else
                    made = HandRank.HighCard;

                // Legacy / Strongest draw signal (may include bare OESD / board-only FD).
                strongDraw = (!madeFlush && flushDraw) || openEnded;

                topPairOrOverpair = false;
                if (made == HandRank.OnePair)
                {
                    int maxBoard = (int)_board[0].Rank;
                    for (int i = 1; i < _boardKnown; i++)
                    {
                        int br = (int)_board[i].Rank;
                        if (br > maxBoard)
                            maxBoard = br;
                    }

                    if (h0.Rank == h1.Rank && (int)h0.Rank > maxBoard)
                        topPairOrOverpair = true;
                    else if ((int)h0.Rank == maxBoard || (int)h1.Rank == maxBoard)
                        topPairOrOverpair = true;
                }
            }

            private static bool HasStraight(bool[] present)
            {
                // A-5 wheel
                if (present[14] && present[2] && present[3] && present[4] && present[5])
                    return true;

                int run = 0;
                for (int r = 2; r <= 14; r++)
                {
                    if (present[r])
                    {
                        run++;
                        if (run >= 5)
                            return true;
                    }
                    else
                    {
                        run = 0;
                    }
                }

                return false;
            }

            private static bool HasOpenEndedStraightDraw(bool[] present)
            {
                // Four consecutive ranks with both ends live (same idea as PostflopDrawDetector).
                for (int start = (int)Rank.Two; start <= (int)Rank.Jack; start++)
                {
                    bool run = true;
                    for (int i = 0; i < 4; i++)
                    {
                        if (!present[start + i])
                        {
                            run = false;
                            break;
                        }
                    }

                    if (!run)
                        continue;

                    int lowOut = start == (int)Rank.Two ? (int)Rank.Ace : start - 1;
                    int highOut = start + 4;
                    if (highOut > (int)Rank.Ace)
                        continue;

                    if (CompletesStraight(present, lowOut) && CompletesStraight(present, highOut))
                        return true;
                }

                return false;
            }

            private static bool CompletesStraight(bool[] present, int added)
            {
                if (added < (int)Rank.Two || added > (int)Rank.Ace)
                    return false;

                // Temporary add
                bool had = present[added];
                present[added] = true;
                bool made = HasStraight(present);
                present[added] = had;
                return made;
            }
        }
    }
}
