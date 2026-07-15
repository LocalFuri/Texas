using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>One completed betting action in the current hand (bot AI analysis only).</summary>
    public readonly struct HandActionEntry
    {
        public GamePhase     Street           { get; }
        public string        PlayerName       { get; }
        public BettingAction Action           { get; }
        public int           Amount           { get; }
        public int           Pot              { get; }
        public int           StreetRaiseCount { get; }

        public HandActionEntry(
            GamePhase street,
            string playerName,
            BettingAction action,
            int amount,
            int pot,
            int streetRaiseCount)
        {
            Street           = street;
            PlayerName       = playerName ?? string.Empty;
            Action           = action;
            Amount           = amount;
            Pot              = pot;
            StreetRaiseCount = streetRaiseCount;
        }
    }

    /// <summary>
    /// Hand-scoped action history for bot AI analysis. Cleared each hand; does not affect play.
    /// </summary>
    public sealed class HandActionLog
    {
        private readonly List<HandActionEntry> _entries = new List<HandActionEntry>(32);

        public IReadOnlyList<HandActionEntry> Entries => _entries;

        public void Clear() => _entries.Clear();

        public void Record(
            GamePhase street,
            PlayerState player,
            BettingAction action,
            int amount,
            int pot,
            int streetRaiseCount)
        {
            string name = player != null ? player.Name : "(null)";
            var entry = new HandActionEntry(street, name, action, amount, pot, streetRaiseCount);
            _entries.Add(entry);

            Debug.Log(
                $"[HandActionLog] street={entry.Street} player={entry.PlayerName} " +
                $"action={entry.Action} amount={entry.Amount} pot={entry.Pot} " +
                $"streetRaiseCount={entry.StreetRaiseCount}");
        }
    }
}
