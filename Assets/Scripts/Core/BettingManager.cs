using System;
using UnityEngine;

namespace TexasHoldem
{
    public class BettingManager
    {
        public int Pot        { get; private set; }
        public int CurrentBet { get; private set; }
        public int SmallBlind { get; }
        public int BigBlind   { get; }

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
        }

        /// <summary>Resets only the current bet for a new betting phase.</summary>
        public void ResetPhase() => CurrentBet = 0;

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
                    return true;

                case BettingAction.Raise:
                    int totalAfterRaise = CurrentBet + raiseAmount;
                    PlaceBet(player, totalAfterRaise - player.CurrentBet);
                    CurrentBet = player.CurrentBet;
                    return true;

                case BettingAction.AllIn:
                    PlaceBet(player, player.Chips);
                    if (player.CurrentBet > CurrentBet)
                        CurrentBet = player.CurrentBet;
                    player.IsAllIn = true;
                    return true;

                default:
                    return false;
            }
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
    }
}
