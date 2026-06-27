namespace TexasHoldem
{
    public enum GamePhase
    {
        WaitingToStart,
        PreFlop,
        Flop,
        Turn,
        River,
        Showdown,
        RoundOver,
        GameOver
    }

    public enum BettingAction
    {
        Fold,
        Check,
        Call,
        Raise,
        AllIn
    }
}
