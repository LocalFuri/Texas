using System;
using UnityEngine.Events;

namespace TexasHoldem
{
    [Serializable]
    public class PlayerActionEvent : UnityEvent<PlayerState, BettingAction, int> { }
}
