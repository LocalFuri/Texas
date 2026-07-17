using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Regression tests for showdown winners via production <see cref="HandEvaluator"/>.
    /// Every case asserts an expected winner (A / B / split), not Fast↔reference parity.
    /// </summary>
    public sealed class HandEvaluatorShowdownTestRunner : MonoBehaviour
    {
        public const int DefaultRandomShowdowns = 50_000;
        public const int DefaultRandomSeed = 20260717;

        private enum WinnerExpect
        {
            A,
            B,
            Split,
        }

        [ContextMenu("Run HandEvaluator Showdown Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests(
            int randomShowdowns = DefaultRandomShowdowns,
            int randomSeed = DefaultRandomSeed)
        {
            var failures = new List<string>();
            int passed = 0;
            int total = 0;

            Debug.Log("[HandEvalShowdown] Starting curated + random winner assertions...");

            foreach (ShowdownCase testCase in BuildCuratedCases())
            {
                total++;
                if (TryAssertShowdown(testCase, out string detail))
                {
                    passed++;
                    Debug.Log($"[HandEvalShowdown] PASS  {testCase.Name}");
                }
                else
                {
                    failures.Add(detail);
                    Debug.LogError($"[HandEvalShowdown] FAIL  {detail}");
                }
            }

            foreach (RankCase rankCase in BuildRankCases())
            {
                total++;
                HandResult result = HandEvaluator.Evaluate(rankCase.Cards.ToList());
                bool ok = result.Rank == rankCase.ExpectedRank
                    && (!rankCase.ExpectedHigh.HasValue || result.Tiebreakers[0] == rankCase.ExpectedHigh.Value);
                if (ok)
                {
                    passed++;
                    Debug.Log($"[HandEvalShowdown] PASS  {rankCase.Name}");
                }
                else
                {
                    string detail =
                        $"{rankCase.Name}: got {result.Rank}[{string.Join(",", result.Tiebreakers)}] " +
                        $"expected {rankCase.ExpectedRank}" +
                        (rankCase.ExpectedHigh.HasValue ? $" high={rankCase.ExpectedHigh.Value}" : "");
                    failures.Add(detail);
                    Debug.LogError($"[HandEvalShowdown] FAIL  {detail}");
                }
            }

            int randomPassed = 0;
            int randomTotal = randomShowdowns;
            if (!RunRandomShowdowns(randomShowdowns, randomSeed, failures, out randomPassed, out var catHits))
            {
                // failures already populated
            }

            passed += randomPassed;
            total += randomTotal;

            Debug.Log(
                $"[HandEvalShowdown] Category hits (random hands A+B): " +
                string.Join(", ", catHits.Select((c, i) => $"{(HandRank)i}={c}")));
            Debug.Log(
                $"[HandEvalShowdown] Complete: {passed}/{total} passed" +
                (failures.Count > 0 ? $", {failures.Count} failure detail(s)." : "."));

            return (passed, total);
        }

        private static bool TryAssertShowdown(ShowdownCase testCase, out string detail)
        {
            HandResult a = HandEvaluator.Evaluate(Concat(testCase.HoleA, testCase.Board));
            HandResult b = HandEvaluator.Evaluate(Concat(testCase.HoleB, testCase.Board));
            int cmp = a.CompareTo(b);

            WinnerExpect actual =
                cmp > 0 ? WinnerExpect.A :
                cmp < 0 ? WinnerExpect.B :
                WinnerExpect.Split;

            if (actual == testCase.Expected)
            {
                detail = string.Empty;
                return true;
            }

            detail =
                $"{testCase.Name}: expected {testCase.Expected}, got {actual} " +
                $"A={a.Rank}[{string.Join(",", a.Tiebreakers)}] " +
                $"B={b.Rank}[{string.Join(",", b.Tiebreakers)}] " +
                $"board={FormatCards(testCase.Board)} " +
                $"A={FormatCards(testCase.HoleA)} B={FormatCards(testCase.HoleB)}";
            return false;
        }

        private static bool RunRandomShowdowns(
            int count,
            int seed,
            List<string> failures,
            out int passed,
            out int[] categoryHits)
        {
            passed = 0;
            categoryHits = new int[10];
            var rng = new System.Random(seed);
            var deck = BuildDeck();

            for (int i = 0; i < count; i++)
            {
                Shuffle(deck, rng);
                Card[] board = { deck[0], deck[1], deck[2], deck[3], deck[4] };
                Card[] holeA = { deck[5], deck[6] };
                Card[] holeB = { deck[7], deck[8] };

                HandResult a = HandEvaluator.Evaluate(Concat(holeA, board));
                HandResult b = HandEvaluator.Evaluate(Concat(holeB, board));
                HandResult oa = OracleBest(Concat(holeA, board));
                HandResult ob = OracleBest(Concat(holeB, board));

                categoryHits[(int)a.Rank]++;
                categoryHits[(int)b.Rank]++;

                if (!SameResult(a, oa) || !SameResult(b, ob))
                {
                    if (failures.Count < 40)
                    {
                        failures.Add(
                            $"random[{i}] strength mismatch " +
                            $"A prod={Fmt(a)} oracle={Fmt(oa)} B prod={Fmt(b)} oracle={Fmt(ob)}");
                    }

                    continue;
                }

                int cmp = a.CompareTo(b);
                int ocmp = oa.CompareTo(ob);
                if (Math.Sign(cmp) != Math.Sign(ocmp))
                {
                    if (failures.Count < 40)
                    {
                        failures.Add(
                            $"random[{i}] winner mismatch cmp={cmp} ocmp={ocmp} " +
                            $"A={Fmt(a)} B={Fmt(b)} board={FormatCards(board)} " +
                            $"A={FormatCards(holeA)} B={FormatCards(holeB)}");
                    }

                    continue;
                }

                // Assert expected winner from oracle: A / B / Split.
                WinnerExpect expected =
                    ocmp > 0 ? WinnerExpect.A :
                    ocmp < 0 ? WinnerExpect.B :
                    WinnerExpect.Split;
                WinnerExpect actual =
                    cmp > 0 ? WinnerExpect.A :
                    cmp < 0 ? WinnerExpect.B :
                    WinnerExpect.Split;

                if (actual != expected)
                {
                    if (failures.Count < 40)
                        failures.Add($"random[{i}] expected {expected}, got {actual}");
                    continue;
                }

                passed++;
            }

            return passed == count;
        }

        private static List<ShowdownCase> BuildCuratedCases()
        {
            var cases = new List<ShowdownCase>();

            // --- Category ladder: each beats all lower (pairwise vs High Card / prior) ---
            cases.Add(Sd(
                "OnePair beats HighCard",
                Board(C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Five), C(Suit.Diamonds, Rank.Nine),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Three)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Clubs, Rank.Ace)),
                Hole(C(Suit.Diamonds, Rank.Ace), C(Suit.Clubs, Rank.Queen)),
                WinnerExpect.A));

            cases.Add(Sd(
                "TwoPair beats OnePair",
                Board(C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Two), C(Suit.Diamonds, Rank.Nine),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Three)),
                Hole(C(Suit.Hearts, Rank.King), C(Suit.Clubs, Rank.Four)),
                Hole(C(Suit.Diamonds, Rank.Ace), C(Suit.Clubs, Rank.Queen)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Trips beats TwoPair",
                Board(C(Suit.Spades, Rank.Seven), C(Suit.Hearts, Rank.Seven), C(Suit.Diamonds, Rank.Ace),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Two)),
                Hole(C(Suit.Clubs, Rank.Seven), C(Suit.Diamonds, Rank.Three)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Clubs, Rank.King)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Straight beats Trips",
                Board(C(Suit.Spades, Rank.Nine), C(Suit.Hearts, Rank.Eight), C(Suit.Diamonds, Rank.Seven),
                    C(Suit.Clubs, Rank.Six), C(Suit.Spades, Rank.Two)),
                Hole(C(Suit.Hearts, Rank.Five), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.Two), C(Suit.Clubs, Rank.Two)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Flush beats Straight",
                Board(C(Suit.Hearts, Rank.Nine), C(Suit.Hearts, Rank.Five), C(Suit.Hearts, Rank.Three),
                    C(Suit.Clubs, Rank.Seven), C(Suit.Spades, Rank.Six)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.Two)),
                Hole(C(Suit.Diamonds, Rank.Eight), C(Suit.Clubs, Rank.Four)),
                WinnerExpect.A));

            cases.Add(Sd(
                "FullHouse beats Flush",
                Board(C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.King), C(Suit.Diamonds, Rank.King),
                    C(Suit.Hearts, Rank.Two), C(Suit.Hearts, Rank.Five)),
                Hole(C(Suit.Spades, Rank.Two), C(Suit.Diamonds, Rank.Three)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.Nine)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Quads beats FullHouse",
                Board(C(Suit.Spades, Rank.Eight), C(Suit.Hearts, Rank.Eight), C(Suit.Diamonds, Rank.Eight),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.King)),
                Hole(C(Suit.Clubs, Rank.Eight), C(Suit.Hearts, Rank.Two)),
                Hole(C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.Two)),
                WinnerExpect.A));

            cases.Add(Sd(
                "StraightFlush beats Quads",
                Board(C(Suit.Clubs, Rank.Five), C(Suit.Clubs, Rank.Six), C(Suit.Clubs, Rank.Seven),
                    C(Suit.Clubs, Rank.Eight), C(Suit.Spades, Rank.Eight)),
                Hole(C(Suit.Clubs, Rank.Nine), C(Suit.Hearts, Rank.Two)),
                Hole(C(Suit.Hearts, Rank.Eight), C(Suit.Diamonds, Rank.Eight)),
                WinnerExpect.A));

            cases.Add(Sd(
                "RoyalFlush beats StraightFlush",
                Board(C(Suit.Spades, Rank.Ten), C(Suit.Spades, Rank.Jack), C(Suit.Spades, Rank.Queen),
                    C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.Two)),
                Hole(C(Suit.Spades, Rank.Ace), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Spades, Rank.Nine), C(Suit.Diamonds, Rank.Three)),
                WinnerExpect.A));

            // --- Kickers ---
            cases.Add(Sd(
                "Pair kicker Queen beats Jack",
                Board(C(Suit.Spades, Rank.Nine), C(Suit.Hearts, Rank.Nine), C(Suit.Diamonds, Rank.Two),
                    C(Suit.Clubs, Rank.Three), C(Suit.Spades, Rank.Four)),
                Hole(C(Suit.Hearts, Rank.Queen), C(Suit.Clubs, Rank.Five)),
                Hole(C(Suit.Diamonds, Rank.Jack), C(Suit.Clubs, Rank.Six)),
                WinnerExpect.A));

            cases.Add(Sd(
                "TwoPair kicker Queen beats Jack",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.King),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Two)),
                Hole(C(Suit.Hearts, Rank.Queen), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.Jack), C(Suit.Clubs, Rank.Four)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Trips kickers AK beats AQ",
                Board(C(Suit.Spades, Rank.Seven), C(Suit.Hearts, Rank.Seven), C(Suit.Diamonds, Rank.Seven),
                    C(Suit.Clubs, Rank.Two), C(Suit.Spades, Rank.Three)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Clubs, Rank.King)),
                Hole(C(Suit.Diamonds, Rank.Ace), C(Suit.Clubs, Rank.Queen)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Quads Ace kicker beats King",
                Board(C(Suit.Spades, Rank.Eight), C(Suit.Hearts, Rank.Eight), C(Suit.Diamonds, Rank.Eight),
                    C(Suit.Clubs, Rank.Eight), C(Suit.Spades, Rank.Two)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.Four)),
                WinnerExpect.A));

            // --- Split / board plays ---
            cases.Add(Sd(
                "Board broadway split",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.King), C(Suit.Diamonds, Rank.Queen),
                    C(Suit.Clubs, Rank.Jack), C(Suit.Spades, Rank.Ten)),
                Hole(C(Suit.Hearts, Rank.Two), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.Four), C(Suit.Clubs, Rank.Five)),
                WinnerExpect.Split));

            cases.Add(Sd(
                "Board trips split",
                Board(C(Suit.Spades, Rank.Seven), C(Suit.Hearts, Rank.Seven), C(Suit.Diamonds, Rank.Seven),
                    C(Suit.Clubs, Rank.Ace), C(Suit.Spades, Rank.King)),
                Hole(C(Suit.Hearts, Rank.Two), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.Four), C(Suit.Clubs, Rank.Five)),
                WinnerExpect.Split));

            cases.Add(Sd(
                "Board two pair + board kicker split",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.King),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Queen)),
                Hole(C(Suit.Hearts, Rank.Two), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.Four), C(Suit.Clubs, Rank.Five)),
                WinnerExpect.Split));

            cases.Add(Sd(
                "Same board pair, AJ kicker beats 76",
                Board(C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Two), C(Suit.Diamonds, Rank.Nine),
                    C(Suit.Clubs, Rank.Five), C(Suit.Spades, Rank.Three)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Clubs, Rank.Jack)),
                Hole(C(Suit.Diamonds, Rank.Seven), C(Suit.Clubs, Rank.Six)),
                WinnerExpect.A));

            // --- Wheel ---
            cases.Add(Sd(
                "Six-high straight beats wheel",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Two), C(Suit.Diamonds, Rank.Three),
                    C(Suit.Clubs, Rank.Four), C(Suit.Spades, Rank.Five)),
                Hole(C(Suit.Hearts, Rank.Six), C(Suit.Clubs, Rank.King)),
                Hole(C(Suit.Diamonds, Rank.Seven), C(Suit.Clubs, Rank.Eight)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Wheel beats Ace-high no straight",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Two), C(Suit.Diamonds, Rank.Three),
                    C(Suit.Clubs, Rank.Four), C(Suit.Spades, Rank.Nine)),
                Hole(C(Suit.Hearts, Rank.Five), C(Suit.Clubs, Rank.King)),
                Hole(C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.Queen)),
                WinnerExpect.A));

            // --- Flush comparisons ---
            cases.Add(Sd(
                "Ace-high flush beats King-high flush",
                Board(C(Suit.Hearts, Rank.Two), C(Suit.Hearts, Rank.Five), C(Suit.Hearts, Rank.Nine),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Queen)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.Three)),
                Hole(C(Suit.Hearts, Rank.King), C(Suit.Hearts, Rank.Four)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Flush second card King beats Queen",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Spades, Rank.Five), C(Suit.Spades, Rank.Four),
                    C(Suit.Spades, Rank.Three), C(Suit.Hearts, Rank.Two)),
                Hole(C(Suit.Spades, Rank.King), C(Suit.Clubs, Rank.Nine)),
                Hole(C(Suit.Spades, Rank.Queen), C(Suit.Diamonds, Rank.Nine)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Flush A-Q-T-8 beats A-Q-T-7",
                Board(C(Suit.Diamonds, Rank.Ace), C(Suit.Diamonds, Rank.Queen), C(Suit.Diamonds, Rank.Ten),
                    C(Suit.Diamonds, Rank.Two), C(Suit.Spades, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.Eight), C(Suit.Hearts, Rank.Four)),
                Hole(C(Suit.Diamonds, Rank.Seven), C(Suit.Clubs, Rank.Four)),
                WinnerExpect.A));

            // --- Full house comparisons ---
            cases.Add(Sd(
                "AAAKK beats KKKAA (trips rank)",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.King),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.King)),
                Hole(C(Suit.Clubs, Rank.Ace), C(Suit.Hearts, Rank.Two)),
                Hole(C(Suit.Diamonds, Rank.Two), C(Suit.Clubs, Rank.Three)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Nines full of Aces beats nines full of Kings",
                Board(C(Suit.Spades, Rank.Nine), C(Suit.Hearts, Rank.Nine), C(Suit.Diamonds, Rank.Nine),
                    C(Suit.Clubs, Rank.Ace), C(Suit.Spades, Rank.Two)),
                Hole(C(Suit.Hearts, Rank.Ace), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.King)),
                WinnerExpect.A));

            cases.Add(Sd(
                "Kings full beats Queens full of Aces",
                Board(C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.King), C(Suit.Diamonds, Rank.Queen),
                    C(Suit.Clubs, Rank.Queen), C(Suit.Spades, Rank.Ace)),
                Hole(C(Suit.Clubs, Rank.King), C(Suit.Hearts, Rank.Two)),
                Hole(C(Suit.Hearts, Rank.Queen), C(Suit.Diamonds, Rank.Ace)),
                WinnerExpect.A));

            // --- Wheel SF vs 6-high SF ---
            cases.Add(Sd(
                "Six-high SF beats wheel SF",
                Board(C(Suit.Clubs, Rank.Ace), C(Suit.Clubs, Rank.Two), C(Suit.Clubs, Rank.Three),
                    C(Suit.Clubs, Rank.Four), C(Suit.Clubs, Rank.Five)),
                Hole(C(Suit.Clubs, Rank.Six), C(Suit.Hearts, Rank.King)),
                Hole(C(Suit.Hearts, Rank.Seven), C(Suit.Diamonds, Rank.Eight)),
                WinnerExpect.A));

            // --- High card kicker ---
            cases.Add(Sd(
                "HighCard AKQJ9 beats AKQJ8",
                Board(C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.King), C(Suit.Diamonds, Rank.Queen),
                    C(Suit.Clubs, Rank.Jack), C(Suit.Spades, Rank.Two)),
                Hole(C(Suit.Hearts, Rank.Nine), C(Suit.Clubs, Rank.Three)),
                Hole(C(Suit.Diamonds, Rank.Eight), C(Suit.Clubs, Rank.Four)),
                WinnerExpect.A));

            return cases;
        }

        private static List<RankCase> BuildRankCases()
        {
            return new List<RankCase>
            {
                new RankCase(
                    "Classify HighCard",
                    HandRank.HighCard,
                    null,
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.King), C(Suit.Diamonds, Rank.Queen),
                    C(Suit.Clubs, Rank.Jack), C(Suit.Spades, Rank.Nine), C(Suit.Hearts, Rank.Three),
                    C(Suit.Clubs, Rank.Two)),
                new RankCase(
                    "Classify OnePair",
                    HandRank.OnePair,
                    null,
                    C(Suit.Spades, Rank.Ten), C(Suit.Hearts, Rank.Ten), C(Suit.Diamonds, Rank.Ace),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Queen), C(Suit.Hearts, Rank.Three),
                    C(Suit.Clubs, Rank.Two)),
                new RankCase(
                    "Classify TwoPair",
                    HandRank.TwoPair,
                    null,
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.King),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Five), C(Suit.Hearts, Rank.Three),
                    C(Suit.Clubs, Rank.Two)),
                new RankCase(
                    "Classify Trips",
                    HandRank.ThreeOfAKind,
                    null,
                    C(Suit.Spades, Rank.Seven), C(Suit.Hearts, Rank.Seven), C(Suit.Diamonds, Rank.Seven),
                    C(Suit.Clubs, Rank.Ace), C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.Three),
                    C(Suit.Clubs, Rank.Two)),
                new RankCase(
                    "Classify Straight",
                    HandRank.Straight,
                    13,
                    C(Suit.Spades, Rank.Nine), C(Suit.Hearts, Rank.Ten), C(Suit.Diamonds, Rank.Jack),
                    C(Suit.Clubs, Rank.Queen), C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.Two),
                    C(Suit.Clubs, Rank.Three)),
                new RankCase(
                    "Classify Wheel straight high=5",
                    HandRank.Straight,
                    5,
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Two), C(Suit.Diamonds, Rank.Three),
                    C(Suit.Clubs, Rank.Four), C(Suit.Spades, Rank.Five), C(Suit.Hearts, Rank.King),
                    C(Suit.Clubs, Rank.Queen)),
                new RankCase(
                    "Classify Flush",
                    HandRank.Flush,
                    null,
                    C(Suit.Hearts, Rank.Ace), C(Suit.Hearts, Rank.Jack), C(Suit.Hearts, Rank.Nine),
                    C(Suit.Hearts, Rank.Five), C(Suit.Hearts, Rank.Three), C(Suit.Spades, Rank.King),
                    C(Suit.Clubs, Rank.King)),
                new RankCase(
                    "Classify FullHouse",
                    HandRank.FullHouse,
                    null,
                    C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.King), C(Suit.Diamonds, Rank.King),
                    C(Suit.Clubs, Rank.Two), C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Ace),
                    C(Suit.Clubs, Rank.Three)),
                new RankCase(
                    "Classify Quads",
                    HandRank.FourOfAKind,
                    null,
                    C(Suit.Spades, Rank.Eight), C(Suit.Hearts, Rank.Eight), C(Suit.Diamonds, Rank.Eight),
                    C(Suit.Clubs, Rank.Eight), C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Two),
                    C(Suit.Clubs, Rank.Three)),
                new RankCase(
                    "Classify StraightFlush",
                    HandRank.StraightFlush,
                    9,
                    C(Suit.Hearts, Rank.Five), C(Suit.Hearts, Rank.Six), C(Suit.Hearts, Rank.Seven),
                    C(Suit.Hearts, Rank.Eight), C(Suit.Hearts, Rank.Nine), C(Suit.Spades, Rank.Ace),
                    C(Suit.Clubs, Rank.King)),
                new RankCase(
                    "Classify Wheel SF not Royal",
                    HandRank.StraightFlush,
                    5,
                    C(Suit.Clubs, Rank.Ace), C(Suit.Clubs, Rank.Two), C(Suit.Clubs, Rank.Three),
                    C(Suit.Clubs, Rank.Four), C(Suit.Clubs, Rank.Five), C(Suit.Hearts, Rank.King),
                    C(Suit.Diamonds, Rank.King)),
                new RankCase(
                    "Classify RoyalFlush",
                    HandRank.RoyalFlush,
                    14,
                    C(Suit.Spades, Rank.Ten), C(Suit.Spades, Rank.Jack), C(Suit.Spades, Rank.Queen),
                    C(Suit.Spades, Rank.King), C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Two),
                    C(Suit.Clubs, Rank.Three)),
                new RankCase(
                    "Classify three-pair as TwoPair AAKKQ",
                    HandRank.TwoPair,
                    14,
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.King),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.Queen), C(Suit.Hearts, Rank.Queen),
                    C(Suit.Diamonds, Rank.Jack)),
                new RankCase(
                    "Classify two-trips as FullHouse AAAKK",
                    HandRank.FullHouse,
                    14,
                    C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.Ace),
                    C(Suit.Clubs, Rank.King), C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.King),
                    C(Suit.Diamonds, Rank.Two)),
            };
        }

        // --- Independent oracle (best of C(7,5)) for random winner expectations ---

        private static HandResult OracleBest(List<Card> seven)
        {
            HandResult best = null;
            int n = seven.Count;
            for (int a = 0; a < n - 4; a++)
            for (int b = a + 1; b < n - 3; b++)
            for (int c = b + 1; c < n - 2; c++)
            for (int d = c + 1; d < n - 1; d++)
            for (int e = d + 1; e < n; e++)
            {
                HandResult result = OracleFive(new[]
                {
                    seven[a], seven[b], seven[c], seven[d], seven[e],
                });
                if (best == null || result.CompareTo(best) > 0)
                    best = result;
            }

            return best;
        }

        private static HandResult OracleFive(Card[] five)
        {
            var ranks = five.Select(x => (int)x.Rank).OrderByDescending(x => x).ToList();
            bool flush = five.Select(x => x.Suit).Distinct().Count() == 1;
            bool straight = IsStraightOracle(ranks, out int sh);

            if (flush && straight)
            {
                return sh == (int)Rank.Ace
                    ? new HandResult(HandRank.RoyalFlush, new List<int> { sh })
                    : new HandResult(HandRank.StraightFlush, new List<int> { sh });
            }

            var groups = ranks
                .GroupBy(r => r)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .ToList();
            var counts = groups.Select(g => g.Count()).ToList();

            if (counts[0] == 4)
                return new HandResult(HandRank.FourOfAKind,
                    new List<int> { groups[0].Key, groups[1].Key });

            if (counts[0] == 3 && counts[1] == 2)
                return new HandResult(HandRank.FullHouse,
                    new List<int> { groups[0].Key, groups[1].Key });

            if (flush)
                return new HandResult(HandRank.Flush, ranks);

            if (straight)
                return new HandResult(HandRank.Straight, new List<int> { sh });

            if (counts[0] == 3)
            {
                var kickers = groups.Skip(1).Select(g => g.Key).OrderByDescending(r => r).ToList();
                return new HandResult(HandRank.ThreeOfAKind,
                    new List<int> { groups[0].Key }.Concat(kickers).ToList());
            }

            if (counts[0] == 2 && counts.Count > 1 && counts[1] == 2)
            {
                int hi = Math.Max(groups[0].Key, groups[1].Key);
                int lo = Math.Min(groups[0].Key, groups[1].Key);
                return new HandResult(HandRank.TwoPair,
                    new List<int> { hi, lo, groups[2].Key });
            }

            if (counts[0] == 2)
            {
                var kickers = groups.Skip(1).Select(g => g.Key).OrderByDescending(r => r).ToList();
                return new HandResult(HandRank.OnePair,
                    new List<int> { groups[0].Key }.Concat(kickers).ToList());
            }

            return new HandResult(HandRank.HighCard, ranks);
        }

        private static bool IsStraightOracle(List<int> ranksDesc, out int high)
        {
            high = ranksDesc[0];
            bool normal = true;
            for (int i = 0; i < ranksDesc.Count - 1; i++)
            {
                if (ranksDesc[i] - ranksDesc[i + 1] != 1)
                {
                    normal = false;
                    break;
                }
            }

            if (normal)
                return true;

            if (ranksDesc[0] == (int)Rank.Ace)
            {
                var rest = ranksDesc.Skip(1).OrderByDescending(x => x).ToList();
                if (rest.Count == 4 && rest[0] == 5 && rest[1] == 4 && rest[2] == 3 && rest[3] == 2)
                {
                    high = 5;
                    return true;
                }
            }

            return false;
        }

        private static bool SameResult(HandResult a, HandResult b)
        {
            if (a.Rank != b.Rank || a.Tiebreakers.Count != b.Tiebreakers.Count)
                return false;
            for (int i = 0; i < a.Tiebreakers.Count; i++)
            {
                if (a.Tiebreakers[i] != b.Tiebreakers[i])
                    return false;
            }

            return true;
        }

        private static List<Card> BuildDeck()
        {
            var deck = new List<Card>(52);
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    deck.Add(new Card(suit, rank));
            }

            return deck;
        }

        private static void Shuffle(List<Card> deck, System.Random rng)
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        private static List<Card> Concat(Card[] hole, Card[] board)
        {
            var cards = new List<Card>(hole.Length + board.Length);
            cards.AddRange(hole);
            cards.AddRange(board);
            return cards;
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private static Card[] Hole(Card a, Card b) => new[] { a, b };

        private static Card[] Board(Card a, Card b, Card c, Card d, Card e) =>
            new[] { a, b, c, d, e };

        private static ShowdownCase Sd(
            string name, Card[] board, Card[] holeA, Card[] holeB, WinnerExpect expected) =>
            new ShowdownCase(name, board, holeA, holeB, expected);

        private static string FormatCards(IReadOnlyList<Card> cards) =>
            string.Join(" ", cards.Select(c => c.ToString()));

        private static string Fmt(HandResult r) =>
            $"{r.Rank}[{string.Join(",", r.Tiebreakers)}]";

        private sealed class ShowdownCase
        {
            public string Name { get; }
            public Card[] Board { get; }
            public Card[] HoleA { get; }
            public Card[] HoleB { get; }
            public WinnerExpect Expected { get; }

            public ShowdownCase(
                string name, Card[] board, Card[] holeA, Card[] holeB, WinnerExpect expected)
            {
                Name = name;
                Board = board;
                HoleA = holeA;
                HoleB = holeB;
                Expected = expected;
            }
        }

        private sealed class RankCase
        {
            public string Name { get; }
            public HandRank ExpectedRank { get; }
            public int? ExpectedHigh { get; }
            public Card[] Cards { get; }

            public RankCase(
                string name,
                HandRank expectedRank,
                int? expectedHigh,
                params Card[] cards)
            {
                Name = name;
                ExpectedRank = expectedRank;
                ExpectedHigh = expectedHigh;
                Cards = cards;
            }
        }
    }
}
