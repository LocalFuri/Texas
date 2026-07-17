using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Logger must report the call-gate that forced Fold, not a false "Equity below call threshold".
    /// </summary>
    public sealed class PostflopLoggerReasonTestRunner : MonoBehaviour
    {
        [ContextMenu("Run Postflop Logger Reason Tests")]
        private void RunFromContextMenu() => RunAllTests();

        public static (int passed, int total) RunAllTests()
        {
            int passed = 0;
            const int total = 2;

            Debug.Log("[PostflopLoggerReason] Running logger reason regression(s)...");

            if (RunGateFoldReasonCase())
                passed++;
            if (RunEquityFoldReasonCase())
                passed++;

            Debug.Log($"[PostflopLoggerReason] Complete: {passed}/{total} passed.");
            return (passed, total);
        }

        /// <summary>
        /// Equity above call threshold; wet bluff-catcher gate forces Fold.
        /// Logged reason must name that gate, not equity-below-threshold.
        /// </summary>
        private static bool RunGateFoldReasonCase()
        {
            Card[] hole = { C(Suit.Spades, Rank.King), C(Suit.Clubs, Rank.Nine) };
            Card[] board =
            {
                C(Suit.Hearts, Rank.King),
                C(Suit.Hearts, Rank.Seven),
                C(Suit.Hearts, Rank.Two),
                C(Suit.Clubs, Rank.Eight),
            };

            const float equity = 80f;
            const int pot = 400;
            const int call = 300;
            float needed = 100f * call / (pot + call);
            float callThreshold = needed + 3f;

            BettingAdvice advice = BettingAdvisor.Recommend(
                equityPercent: equity,
                potBeforeAction: pot,
                callAmount: call,
                canCheck: false,
                canRaise: true,
                canCall: true,
                isPreflop: false,
                preflopGroup: PreflopHandGroup.Weak,
                preflopSeat: PreflopSeatBucket.Button,
                facingRaise: true,
                streetRaiseCount: 1,
                playerChips: 1000,
                out string preservedReason,
                holeCards: hole,
                postflopPhase: GamePhase.Turn,
                communityCards: board,
                activeOpponentCount: 1);

            string logged = AIController.BuildPlainEnglishReason(
                afterAdvisor: advice,
                afterSemiBluff: advice,
                afterThinValue: advice,
                afterBarrel: advice,
                equityPercent: equity,
                potOddsPercent: needed,
                facingThreshold: callThreshold,
                canCheck: false,
                street: GamePhase.Turn,
                advisorDecisionReason: preservedReason);

            bool equityAbove = equity >= callThreshold;
            bool ok = advice == BettingAdvice.Fold
                && equityAbove
                && !string.IsNullOrEmpty(preservedReason)
                && preservedReason.IndexOf("Wet-board bluff-catcher", System.StringComparison.Ordinal) >= 0
                && logged == preservedReason
                && logged.IndexOf("Equity below call threshold", System.StringComparison.Ordinal) < 0;

            Debug.Log(
                $"[PostflopLoggerReason] Gate-forced Fold logs gate reason\n" +
                $"  equity={equity:F1}% callThreshold={callThreshold:F1}% advice={advice}\n" +
                $"  preserved={preservedReason ?? "(null)"}\n" +
                $"  logged={logged}\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");

            return ok;
        }

        /// <summary>True equity miss still logs Equity below call threshold.</summary>
        private static bool RunEquityFoldReasonCase()
        {
            Card[] hole = { C(Suit.Spades, Rank.King), C(Suit.Clubs, Rank.Nine) };
            Card[] board =
            {
                C(Suit.Spades, Rank.King),
                C(Suit.Hearts, Rank.Seven),
                C(Suit.Diamonds, Rank.Two),
            };

            const float equity = 10f;
            const int pot = 100;
            const int call = 40;
            float needed = 100f * call / (pot + call);
            float callThreshold = needed + 3f;

            BettingAdvice advice = BettingAdvisor.Recommend(
                equityPercent: equity,
                potBeforeAction: pot,
                callAmount: call,
                canCheck: false,
                canRaise: true,
                canCall: true,
                isPreflop: false,
                preflopGroup: PreflopHandGroup.Weak,
                preflopSeat: PreflopSeatBucket.Button,
                facingRaise: true,
                streetRaiseCount: 1,
                playerChips: 1000,
                out string preservedReason,
                holeCards: hole,
                postflopPhase: GamePhase.Flop,
                communityCards: board,
                activeOpponentCount: 1);

            string logged = AIController.BuildPlainEnglishReason(
                afterAdvisor: advice,
                afterSemiBluff: advice,
                afterThinValue: advice,
                afterBarrel: advice,
                equityPercent: equity,
                potOddsPercent: needed,
                facingThreshold: callThreshold,
                canCheck: false,
                street: GamePhase.Flop,
                advisorDecisionReason: preservedReason);

            bool ok = advice == BettingAdvice.Fold
                && string.IsNullOrEmpty(preservedReason)
                && logged.IndexOf("Equity below call threshold", System.StringComparison.Ordinal) >= 0;

            Debug.Log(
                $"[PostflopLoggerReason] Equity Fold still says below threshold\n" +
                $"  equity={equity:F1}% callThreshold={callThreshold:F1}% advice={advice}\n" +
                $"  preserved={preservedReason ?? "(null)"}\n" +
                $"  logged={logged}\n" +
                $"  Result: {(ok ? "PASS" : "FAIL")}");

            return ok;
        }

        private static Card C(Suit suit, Rank rank) => new Card(suit, rank);
    }
}
