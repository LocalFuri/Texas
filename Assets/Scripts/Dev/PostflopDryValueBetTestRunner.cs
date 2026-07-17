using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>Dry-board Overpair / TPTK value-bet when checked to below StrongRaise equity.</summary>
    public sealed class PostflopDryValueBetTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Postflop Dry Value-Bet Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            var cases = BuildCases();
            int passed = 0;

            Debug.Log($"[PostflopDryValue] Running {cases.Count} scenario(s)...");

            foreach (ValueCase c in cases)
            {
                bool classified = c.ExpectOverpair
                    ? BettingAdvisor.IsOverpair(c.Hole, c.Board)
                    : BettingAdvisor.IsTopPairTopKicker(c.Hole, c.Board);

                BettingAdvice advice = BettingAdvisor.Recommend(
                    equityPercent: c.Equity,
                    potBeforeAction: 100,
                    callAmount: 0,
                    canCheck: true,
                    canRaise: true,
                    canCall: false,
                    isPreflop: false,
                    preflopGroup: PreflopHandGroup.Premium,
                    preflopSeat: PreflopSeatBucket.Button,
                    facingRaise: false,
                    streetRaiseCount: 0,
                    playerChips: 1000,
                    holeCards: c.Hole,
                    postflopPhase: GamePhase.Flop,
                    communityCards: c.Board,
                    activeOpponentCount: 1);

                bool ok = classified && advice == BettingAdvice.Raise;
                if (ok)
                    passed++;

                Debug.Log(
                    $"[PostflopDryValue] {c.Name}\n" +
                    $"  classified={classified} equity={c.Equity:F0}% advice={advice} expected=Raise\n" +
                    $"  Result: {(ok ? "PASS" : "FAIL")}");
            }

            Debug.Log($"[PostflopDryValue] Complete: {passed}/{cases.Count} passed.");
            return (passed, cases.Count);
        }

        private static List<ValueCase> BuildCases()
        {
            // Dry: K♠ 7♥ 2♦
            Card[] dry =
            {
                C(Suit.Spades, Rank.King),
                C(Suit.Hearts, Rank.Seven),
                C(Suit.Diamonds, Rank.Two),
            };

            return new List<ValueCase>
            {
                new ValueCase(
                    "Dry Overpair checked to at 57% equity → Raise",
                    new[] { C(Suit.Hearts, Rank.Ace), C(Suit.Clubs, Rank.Ace) },
                    dry,
                    equity: 57f,
                    expectOverpair: true),

                new ValueCase(
                    "Dry TPTK checked to at 58% equity → Raise",
                    new[] { C(Suit.Hearts, Rank.King), C(Suit.Clubs, Rank.Ace) },
                    dry,
                    equity: 58f,
                    expectOverpair: false),
            };
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);

        private sealed class ValueCase
        {
            public string Name { get; }
            public Card[] Hole { get; }
            public Card[] Board { get; }
            public float Equity { get; }
            public bool ExpectOverpair { get; }

            public ValueCase(string name, Card[] hole, Card[] board, float equity, bool expectOverpair)
            {
                Name = name;
                Hole = hole;
                Board = board;
                Equity = equity;
                ExpectOverpair = expectOverpair;
            }
        }
    }
}
