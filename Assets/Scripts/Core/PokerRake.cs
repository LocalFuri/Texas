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
                return string.Empty;

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

    /// <summary>Splits a net pot evenly among winners (remainder chips go to earliest seats in list).</summary>
    public static class PotAward
    {
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
