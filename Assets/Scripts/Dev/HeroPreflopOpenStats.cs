using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Statistics-only observer: records every human preflop open during normal play.
    /// Does not change AI or gameplay — listens to existing GameManager events.
    /// </summary>
    public sealed class HeroPreflopOpenStats : MonoBehaviour
    {
        private GameManager _game;
        private bool _pendingOpen;
        private PreflopSeatBucket _pendingSeat;
        private string _pendingCards = "??";
        private int _pendingRaiseSize;
        private int _sessionNetAtOpen;

        public int Opens { get; private set; }
        public int TotalProfitLoss { get; private set; }
        public float AverageProfitPerOpen => Opens > 0 ? (float)TotalProfitLoss / Opens : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<HeroPreflopOpenStats>() != null)
                return;

            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.gameObject.AddComponent<HeroPreflopOpenStats>();
        }

        private void OnEnable()
        {
            Bind(FindFirstObjectByType<GameManager>());
        }

        private void Start()
        {
            if (_game == null)
                Bind(FindFirstObjectByType<GameManager>());
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind(GameManager game)
        {
            if (game == null || _game == game)
                return;

            Unbind();
            _game = game;
            _game.OnPlayerAction.AddListener(OnPlayerAction);
            _game.OnRoundEnded.AddListener(OnRoundEnded);
            _game.OnRoundStarting.AddListener(OnRoundStarting);
        }

        private void Unbind()
        {
            if (_game == null)
                return;

            _game.OnPlayerAction.RemoveListener(OnPlayerAction);
            _game.OnRoundEnded.RemoveListener(OnRoundEnded);
            _game.OnRoundStarting.RemoveListener(OnRoundStarting);
            _game = null;
        }

        private void OnRoundStarting()
        {
            // New hand — drop an unfinished open (should not happen if OnRoundEnded ran).
            _pendingOpen = false;
        }

        private void OnPlayerAction(PlayerState player, BettingAction action, int amount)
        {
            if (_game == null || player == null || player.Type != PlayerType.Human)
                return;
            if (_game.CurrentPhase != GamePhase.PreFlop)
                return;
            if (action != BettingAction.Raise)
                return;
            // After ProcessAction, the first raise of the street has StreetRaiseCount == 1.
            if (_game.StreetRaiseCount != 1)
                return;

            _pendingOpen = true;
            _pendingSeat = _game.GetPreflopSeatBucket(player);
            _pendingCards = FormatHoleCards(player.HoleCards);
            _pendingRaiseSize = amount;
            _sessionNetAtOpen = player.SessionNetProfit;
        }

        private void OnRoundEnded()
        {
            if (!_pendingOpen || _game == null)
                return;

            PlayerState human = FindHuman(_game.Players);
            if (human == null)
            {
                _pendingOpen = false;
                return;
            }

            // SessionNetProfit was updated before OnRoundEnded; refill does not change it.
            int handProfitLoss = human.SessionNetProfit - _sessionNetAtOpen;

            Opens++;
            TotalProfitLoss += handProfitLoss;
            _pendingOpen = false;

            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[HeroOpen] {0} | {1} | raise {2} | P/L {3:+0;-0;0}\n" +
                "[HeroOpen] Totals: Opens={4} TotalP/L={5:+0;-0;0} Avg={6:+0.0;-0.0;0.0}",
                FormatSeat(_pendingSeat),
                _pendingCards,
                _pendingRaiseSize,
                handProfitLoss,
                Opens,
                TotalProfitLoss,
                AverageProfitPerOpen);
        }

        [ContextMenu("Print Hero Open Stats")]
        public void PrintTotals()
        {
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "[HeroOpen] Totals: Opens={0} TotalP/L={1:+0;-0;0} Avg={2:+0.0;-0.0;0.0}",
                Opens,
                TotalProfitLoss,
                AverageProfitPerOpen);
        }

        [ContextMenu("Reset Hero Open Stats")]
        public void ResetTotals()
        {
            Opens = 0;
            TotalProfitLoss = 0;
            _pendingOpen = false;
            Debug.Log("[HeroOpen] Totals reset.");
        }

        public string BuildSummaryText()
        {
            var sb = new StringBuilder(128);
            sb.Append("Opens: ").Append(Opens).AppendLine();
            sb.Append("Total profit/loss: ").Append(TotalProfitLoss).AppendLine();
            sb.Append("Average profit per open: ").Append(AverageProfitPerOpen.ToString("0.0"));
            return sb.ToString();
        }

        private static PlayerState FindHuman(IReadOnlyList<PlayerState> players)
        {
            if (players == null)
                return null;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].Type == PlayerType.Human)
                    return players[i];
            }
            return null;
        }

        private static string FormatHoleCards(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count < 2)
                return "??";
            return $"{cards[0]} {cards[1]}";
        }

        private static string FormatSeat(PreflopSeatBucket seat) =>
            seat switch
            {
                PreflopSeatBucket.Button     => "BTN",
                PreflopSeatBucket.SmallBlind => "SB",
                PreflopSeatBucket.BigBlind   => "BB",
                PreflopSeatBucket.Early      => "EP",
                PreflopSeatBucket.Middle     => "MP",
                PreflopSeatBucket.Cutoff     => "CO",
                _                            => seat.ToString(),
            };
    }
}
