using System;
using System.Collections.Generic;
using UnityEngine;

namespace TexasHoldem
{
    public class BettingManager
    {
        public int Pot        { get; private set; }
        public int CurrentBet { get; private set; }
        public int SmallBlind { get; }
        public int BigBlind   { get; }

        private PlayerState _lastAggressor;
        private bool        _lastRaiseWasCalled;
        private int         _streetRaiseCount;

        /// <summary>Raises made this betting street (open, 3-bet, 4-bet, …).</summary>
        public int StreetRaiseCount => _streetRaiseCount;

        public BettingManager(int smallBlind, int bigBlind)
        {
            SmallBlind = smallBlind;
            BigBlind   = bigBlind;
        }

        /// <summary>Resets pot and current bet for a new round.</summary>
        public void ResetRound()
        {
            Pot        = 0;
            CurrentBet = 0;
            ResetAggressionTracking();
        }

        /// <summary>Resets only the current bet for a new betting phase.</summary>
        public void ResetPhase()
        {
            CurrentBet = 0;
            ResetAggressionTracking();
        }

        private void ResetAggressionTracking()
        {
            _lastAggressor       = null;
            _lastRaiseWasCalled  = false;
            _streetRaiseCount    = 0;
        }

        /// <summary>Posts the small blind for the given player.</summary>
        public void PostSmallBlind(PlayerState player) => PlaceForcedBet(player, SmallBlind);

        /// <summary>Posts the big blind for the given player and sets the round's CurrentBet.</summary>
        public void PostBigBlind(PlayerState player)
        {
            PlaceForcedBet(player, BigBlind);
            CurrentBet = BigBlind;
        }

        /// <summary>Processes a betting action for a player. Returns false if invalid.</summary>
        public bool ProcessAction(PlayerState player, BettingAction action, int raiseAmount = 0)
        {
            switch (action)
            {
                case BettingAction.Fold:
                    player.HasFolded = true;
                    return true;

                case BettingAction.Check:
                    if (player.CurrentBet < CurrentBet)
                    {
                        Debug.LogWarning($"{player.Name} cannot check — must call {CurrentBet - player.CurrentBet}.");
                        return false;
                    }
                    return true;

                case BettingAction.Call:
                    PlaceBet(player, CurrentBet - player.CurrentBet);
                    NoteMatchedBet(player);
                    return true;

                case BettingAction.Raise:
                    if (!IsValidRaiseIncrement(player, raiseAmount))
                    {
                        Debug.LogWarning(
                            $"{player.Name} cannot raise by {raiseAmount} " +
                            $"(call={GetCallAmount(player)}, chips={player.Chips}, table={CurrentBet}).");
                        return false;
                    }

                    int totalAfterRaise = CurrentBet + raiseAmount;
                    PlaceBet(player, totalAfterRaise - player.CurrentBet);
                    CurrentBet = player.CurrentBet;
                    NoteRaise(player);
                    return true;

                case BettingAction.AllIn:
                    int tableBetBefore = CurrentBet;
                    PlaceBet(player, player.Chips);
                    if (player.CurrentBet > CurrentBet)
                    {
                        CurrentBet = player.CurrentBet;
                        NoteRaise(player);
                    }
                    else if (player.CurrentBet >= tableBetBefore && player.CurrentBet >= CurrentBet)
                        NoteMatchedBet(player);

                    player.IsAllIn = true;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Chips required to match the current table bet.</summary>
        public int GetCallAmount(PlayerState player) =>
            Math.Max(0, CurrentBet - player.CurrentBet);

        /// <summary>Minimum raise increment above <see cref="CurrentBet"/> (2× big blind).</summary>
        public int GetMinRaiseIncrement() => BigBlind * 2;

        /// <summary>Maximum raise increment the player can add after calling.</summary>
        public int GetMaxRaiseIncrement(PlayerState player) =>
            Math.Max(0, player.Chips - GetCallAmount(player));

        /// <summary>True when the player has enough chips to make a legal minimum raise.</summary>
        public bool CanRaise(PlayerState player) =>
            GetMaxRaiseIncrement(player) >= GetMinRaiseIncrement();

        /// <summary>Validates a raise increment (amount above <see cref="CurrentBet"/>).</summary>
        public bool IsValidRaiseIncrement(PlayerState player, int raiseAmount)
        {
            if (raiseAmount <= 0)
                return false;

            int minIncrement = GetMinRaiseIncrement();
            int maxIncrement = GetMaxRaiseIncrement(player);
            if (maxIncrement < minIncrement)
                return false;

            return raiseAmount >= minIncrement && raiseAmount <= maxIncrement;
        }

        private void PlaceForcedBet(PlayerState player, int amount)
        {
            amount         = Math.Min(amount, player.Chips);
            player.Chips  -= amount;
            player.CurrentBet += amount;
            Pot           += amount;
        }

        private void PlaceBet(PlayerState player, int amount)
        {
            amount         = Math.Min(Math.Max(amount, 0), player.Chips);
            player.Chips  -= amount;
            player.CurrentBet += amount;
            Pot           += amount;
            if (player.Chips == 0) player.IsAllIn = true;
        }

        private void NoteRaise(PlayerState aggressor)
        {
            _lastAggressor      = aggressor;
            _lastRaiseWasCalled = false;
            _streetRaiseCount++;
        }

        private void NoteMatchedBet(PlayerState player)
        {
            if (_lastAggressor != null
                && player != _lastAggressor
                && player.CurrentBet >= CurrentBet)
            {
                _lastRaiseWasCalled = true;
            }
        }

        /// <summary>
        /// Returns uncalled raise chips to the last aggressor when everyone folded without calling.
        /// SB/BB-only pots keep blinds collected; the full uncalled raise stack goes back to the raiser.
        /// </summary>
        public int ReturnUncalledBet(PlayerState winner, IReadOnlyList<PlayerState> players)
        {
            if (winner == null || winner.CurrentBet <= 0 || winner != _lastAggressor)
                return 0;

            foreach (PlayerState player in players)
            {
                if (player != winner && player.CurrentBet >= winner.CurrentBet)
                    return 0;
            }

            int maxOtherBet  = 0;
            int sumOtherBets = 0;
            foreach (PlayerState player in players)
            {
                if (player == winner)
                    continue;

                sumOtherBets += player.CurrentBet;
                if (player.CurrentBet > maxOtherBet)
                    maxOtherBet = player.CurrentBet;
            }

            int blindCap = SmallBlind + BigBlind;
            int returnAmount;
            if (!_lastRaiseWasCalled && sumOtherBets <= blindCap)
                returnAmount = winner.CurrentBet;
            else
                returnAmount = winner.CurrentBet - maxOtherBet;

            returnAmount = Math.Min(returnAmount, winner.CurrentBet);
            if (returnAmount <= 0)
                return 0;

            winner.Chips      += returnAmount;
            winner.CurrentBet -= returnAmount;
            Pot               -= returnAmount;
            return returnAmount;
        }
    }
}
