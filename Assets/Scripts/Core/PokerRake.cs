using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    [Serializable]
    public class PokerRakeSettings
    {
        [Tooltip("When off, no rake is taken from any pot.")]
        public bool enabled = true;

        [Tooltip("Percent of the final pot taken as rake (e.g. 5 = 5%).")]
        [Range(0f, 100f)]
        public float percent = 5f;

        [Tooltip("Maximum rake in big-blind units (e.g. 3 = 3× BB cap).")]
        [Min(0)]
        public int capBigBlinds = 3;

        [Tooltip("When on, hands that end before the flop (3 board cards) are not raked.")]
        public bool noFlopNoRake = true;

        public RakeResult Evaluate(int grossPot, int bigBlind, bool flopWasDealt)
        {
            if (!enabled || grossPot <= 0)
                return default;

            if (noFlopNoRake && !flopWasDealt)
                return default;

            int uncapped = Mathf.FloorToInt(grossPot * (percent / 100f));
            int cap      = capBigBlinds * bigBlind;
            int amount   = Mathf.Clamp(uncapped, 0, cap);

            return new RakeResult
            {
                Amount    = amount,
                WasCapped = amount > 0 && uncapped > cap,
            };
        }

        public int Calculate(int grossPot, int bigBlind, bool flopWasDealt) =>
            Evaluate(grossPot, bigBlind, flopWasDealt).Amount;

        public string FormatDisplay(in RakeResult result)
        {
            if (result.Amount <= 0)
                return "No Rake";

            return result.WasCapped
                ? $"{result.Amount} (cap)"
                : $"{result.Amount} ({percent:g}%)";
        }
    }

    public struct RakeResult
    {
        public int  Amount;
        public bool WasCapped;
    }

    /// <summary>
    /// Splits a net pot among winners. Order winners with
    /// <see cref="OrderWinnersClockwiseFromDealer"/> first — remainder chips go to the
    /// first winner(s) in that list (WSOP Online: clockwise from the dealer).
    /// </summary>
    public static class PotAward
    {
        /// <summary>
        /// Orders winners clockwise from the dealer button (WSOP Online rule):
        /// the first winner after the button receives the first remainder chip.
        /// </summary>
        public static List<PlayerState> OrderWinnersClockwiseFromDealer(
            IReadOnlyList<PlayerState> winners,
            IReadOnlyList<PlayerState> activeHand,
            int dealerIndexInActive)
        {
            if (winners == null || winners.Count == 0)
                return new List<PlayerState>();

            if (winners.Count == 1 || activeHand == null || activeHand.Count == 0)
                return new List<PlayerState>(winners);

            var winnerSet = new HashSet<PlayerState>(winners);
            var ordered   = new List<PlayerState>(winners.Count);
            int n           = activeHand.Count;
            int dealerIndex = ((dealerIndexInActive % n) + n) % n;

            for (int i = 1; i <= n; i++)
            {
                PlayerState player = activeHand[(dealerIndex + i) % n];
                if (winnerSet.Contains(player))
                    ordered.Add(player);
            }

            foreach (PlayerState winner in winners)
            {
                if (!ordered.Contains(winner))
                    ordered.Add(winner);
            }

            return ordered;
        }

        /// <summary>Share for one winner after <see cref="Split"/> ordering (base + optional remainder chip).</summary>
        public static int ShareForWinnerIndex(int netPot, int winnerCount, int winnerIndexInAwardOrder)
        {
            if (netPot <= 0 || winnerCount <= 0)
                return 0;

            int share     = netPot / winnerCount;
            int remainder = netPot % winnerCount;
            return share + (winnerIndexInAwardOrder < remainder ? 1 : 0);
        }

        public static void Split(int netPot, IReadOnlyList<PlayerState> winners)
        {
            if (netPot <= 0 || winners == null || winners.Count == 0)
                return;

            int share     = netPot / winners.Count;
            int remainder = netPot % winners.Count;

            for (int i = 0; i < winners.Count; i++)
            {
                if (winners[i] == null)
                    continue;

                winners[i].Chips += share + (i < remainder ? 1 : 0);
            }
        }
    }
}
