using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace TexasHoldem
{
    public class GameManager : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField] private int   _startingChips  = 1000;
        [SerializeField] private int   _smallBlind     = 10;
        [SerializeField] private int   _bigBlind       = 20;
        [SerializeField] private int   _aiPlayerCount  = 3;
        [SerializeField] private float _aiActionDelay  = 1.0f;
        [SerializeField] private float _humanThinkTime = 15f;
        [SerializeField] private float _dealDelay      = 0.4f;

        [Header("Table")]
        [SerializeField] private TableLayoutManager _tableLayout;
        [SerializeField] private UIManager          _uiManager;

        [Header("Button Row")]
        [SerializeField] private float _buttonWidth    = 120f;
        [SerializeField] private float _buttonHeight   = 50f;
        [SerializeField] private float _buttonFontSize = 40f;

        [Header("Card Layout")]
        [Tooltip("Pixel gap between the two hole cards (synced to TableLayoutManager on validate).")]
        [SerializeField] private float _cardGap = 16f;

        [Header("Community Card Animation")]
        [SerializeField] private float _communityFlipDuration = 0.35f;
        [SerializeField] private float _communityFlipGap      = 0.1f;

        [Header("Round End")]
        [Tooltip("How long the winner celebration plays before the next round begins.")]
        [SerializeField, Min(0f)] private float _roundEndPauseSecs = 2.5f;

        [Header("Round Start")]
        [Tooltip("Pause before the dealer button appears at the start of each hand.")]
        [SerializeField, Min(0f)] private float _dealerButtonDelay = 0.4f;
        [Tooltip("Pause between dealer button, small blind, and big blind.")]
        [SerializeField, Min(0f)] private float _blindPostDelay = 0.5f;

        [Header("Rake")]
        [SerializeField] private PokerRakeSettings _rake = new PokerRakeSettings();

        [Header("Events")]
        public UnityEvent<GamePhase>        OnPhaseChanged;
        public UnityEvent<List<PlayerState>> OnPlayersUpdated;
        public UnityEvent                   OnRoundStarting;
        public UnityEvent<int>              OnDealerButtonPlaced;
        public UnityEvent<List<Card>>       OnCommunityCardsUpdated;
        public UnityEvent<string>           OnGameMessage;
        public UnityEvent<PlayerState>      OnPlayerTurn;
        public PlayerActionEvent            OnPlayerAction;
        public UnityEvent<PlayerState>      OnWinnerDetermined;
        public UnityEvent                   OnRoundEnded;

        public List<PlayerState> Players      { get; private set; } = new List<PlayerState>();
        public GamePhase         CurrentPhase { get; private set; } = GamePhase.WaitingToStart;
        public int               DealerIndex  { get; private set; } = 0;
        public int               PotAmount    => _bettingManager?.Pot ?? 0;
        public int               CurrentBet   => _bettingManager?.CurrentBet ?? 0;
        public int               BigBlindAmount => _bigBlind;
        public bool              IsAwaitingHumanInput => _awaitingHumanInput;
        public float             ButtonWidth    => _buttonWidth;
        public float             ButtonHeight   => _buttonHeight;
        public float             ButtonFontSize => _buttonFontSize;
        public float             AiActionDelay          => _aiActionDelay;

        /// <summary>Sets bot action delay in seconds (0 = instant).</summary>
        public void SetAiActionDelay(float seconds)
        {
            _aiActionDelay = Mathf.Max(0f, seconds);
        }
        public float             HumanThinkTime         => _humanThinkTime;
        public float             CardGap                 => _cardGap;
        public float             CommunityFlipDuration   => _communityFlipDuration;
        public float             CommunityFlipGap        => _communityFlipGap;
        public float             RoundEndPauseSecs       => _roundEndPauseSecs;
        public bool              AwaitingWinnerDismiss   => _awaitingWinnerDismiss;

        /// <summary>Continues past the showdown result after the human presses Space.</summary>
        public void AcknowledgeWinnerDismiss()
        {
            _awaitingWinnerDismiss = false;
        }
        public float             DealerButtonDelay       => _dealerButtonDelay;
        public float             BlindPostDelay          => _blindPostDelay;
        public int               LastPotAwarded          { get; private set; }
        public int               LastGrossPot            { get; private set; }
        public int               LastRakeAmount          { get; private set; }
        public string            LastRakeDisplayText     { get; private set; }
        public IReadOnlyList<PlayerState> LastRoundWinners { get; private set; }
        public WinningHandEvaluation LastWinningHand     { get; private set; }
        public IReadOnlyList<(PlayerState Player, HandResult Result)> LastShowdownHands { get; private set; }
        public IReadOnlyList<(PlayerState Player, WinningHandEvaluation Evaluation)> LastShowdownEvaluations { get; private set; }
        public IReadOnlyList<Card> CommunityCards        => _boardManager?.CommunityCards;

        /// <summary>Seat index (0-based) of the current dealer among <see cref="Players"/>.</summary>
        public int GetDealerSeatIndex()
        {
            if (Players == null || Players.Count == 0)
                return -1;

            var active = Players.Where(p => p.Chips > 0).ToList();
            if (active.Count == 0)
                return -1;

            int dealerActiveIndex = ((DealerIndex % active.Count) + active.Count) % active.Count;
            return Players.IndexOf(active[dealerActiveIndex]);
        }

        /// <summary>Wait time for staggered community-card flip animations.</summary>
        public float GetCommunityRevealDuration(int newCardCount)
        {
            if (newCardCount <= 0) return _dealDelay;
            return newCardCount * _communityFlipDuration
                 + Mathf.Max(0, newCardCount - 1) * _communityFlipGap;
        }

        private BettingManager _bettingManager;
        private BoardManager   _boardManager;
        private AIController   _aiController;

        private bool          _awaitingHumanInput;
        private bool          _awaitingWinnerDismiss;
        private BettingAction _humanAction;
        private int           _humanRaiseAmount;

        private void Awake()
        {
            ButtonLabelStyle.RegisterFontSizeProvider(() => _buttonFontSize);
            _bettingManager = new BettingManager(_smallBlind, _bigBlind);
            _boardManager   = new BoardManager();
            _aiController   = new AIController();

            if (_tableLayout == null)
                _tableLayout = FindObjectOfType<TableLayoutManager>(includeInactive: true);

            OnPhaseChanged          ??= new UnityEvent<GamePhase>();
            OnPlayersUpdated        ??= new UnityEvent<List<PlayerState>>();
            OnCommunityCardsUpdated ??= new UnityEvent<List<Card>>();
            OnGameMessage           ??= new UnityEvent<string>();
            OnPlayerTurn            ??= new UnityEvent<PlayerState>();
            OnPlayerAction          ??= new PlayerActionEvent();
            OnWinnerDetermined      ??= new UnityEvent<PlayerState>();
            OnRoundEnded            ??= new UnityEvent();
            OnRoundStarting         ??= new UnityEvent();
            OnDealerButtonPlaced    ??= new UnityEvent<int>();
        }

        private void Start()
        {
            InitializePlayers();
        }

        /// <summary>Called by the UI Start button to begin the game loop.</summary>
        public void StartGame()
        {
            StartCoroutine(GameLoop());
        }

        /// <summary>Stops play and returns to the pre-start state (Start button only).</summary>
        public void ResetToStartScreen()
        {
            StopAllCoroutines();
            _awaitingHumanInput   = false;
            _awaitingWinnerDismiss = false;
            _humanAction        = default;
            _humanRaiseAmount   = 0;
            DealerIndex         = 0;
            InitializePlayers();
            _bettingManager.ResetRound();
            _boardManager.NewDeck();
            SetPhase(GamePhase.WaitingToStart);
            OnCommunityCardsUpdated?.Invoke(_boardManager.CommunityCards);
            NotifyPlayersUpdated();
            OnGameMessage?.Invoke(string.Empty);
        }

        private void InitializePlayers()
        {
            _tableLayout?.SyncPlayerDisplayNames();

            Players.Clear();

            int seatCount = _tableLayout != null ? TableLayoutManager.SeatCount : 1 + _aiPlayerCount;
            int playerCount = Mathf.Clamp(1 + _aiPlayerCount, 1, seatCount);

            for (int i = 0; i < playerCount; i++)
            {
                string name = _tableLayout != null
                    ? _tableLayout.GetPlayerName(i)
                    : (i == 0 ? "You" : $"Bot {i}");
                PlayerType type = i == 0 ? PlayerType.Human : PlayerType.AI;
                Players.Add(new PlayerState(name, type, _startingChips));
            }
        }

        private IEnumerator GameLoop()
        {
            while (true)
            {
                var eligible = Players.Where(p => p.Chips > 0).ToList();
                if (eligible.Count < 2)
                {
                    string winner = eligible.Count == 1 ? eligible[0].Name : "Nobody";
                    SetPhase(GamePhase.GameOver);
                    OnGameMessage?.Invoke($"Game Over! {winner} wins the game!");
                    yield break;
                }

                yield return StartCoroutine(PlayRound());
            }
        }

        private IEnumerator PlayRound()
        {
            foreach (var p in Players) p.ResetForNewRound();
            _bettingManager.ResetRound();
            _boardManager.NewDeck();

            // Reset the board display at the start of every round.
            OnCommunityCardsUpdated?.Invoke(_boardManager.CommunityCards);

            var active   = Players.Where(p => p.Chips > 0).ToList();
            ApplyGodModeIfEnabled(active);
            int sbIndex  = (DealerIndex + 1) % active.Count;
            int bbIndex  = (DealerIndex + 2) % active.Count;

            SetPhase(GamePhase.RoundOver);
            OnRoundStarting?.Invoke();
            NotifyPlayersUpdated();

            yield return DelaySeconds(_dealerButtonDelay);
            OnDealerButtonPlaced?.Invoke(GetDealerSeatIndex());
            NotifyPlayersUpdated();

            yield return DelaySeconds(_blindPostDelay);
            _bettingManager.PostSmallBlind(active[sbIndex]);
            OnGameMessage?.Invoke($"{active[sbIndex].Name} posts small blind (${_smallBlind}).");
            NotifyPlayersUpdated();

            yield return DelaySeconds(_blindPostDelay);
            _bettingManager.PostBigBlind(active[bbIndex]);
            OnGameMessage?.Invoke($"{active[bbIndex].Name} posts big blind (${_bigBlind}).");
            NotifyPlayersUpdated();

            _boardManager.DealHoleCards(active);
            NotifyPlayersUpdated();
            yield return DelaySeconds(GetCommunityRevealDuration(2));

            // ── Pre-Flop ──────────────────────────────────────────────────
            SetPhase(GamePhase.PreFlop);
            int utgIndex = (bbIndex + 1) % active.Count;
            yield return StartCoroutine(BettingRound(active, utgIndex));
            if (GetNonFolded(active).Count <= 1) { yield return StartCoroutine(EndRound(active)); yield break; }

            int boardCount = 0;

            // ── Flop ──────────────────────────────────────────────────────
            yield return CollectStreetBetsBeforeNextStreet(active);
            _boardManager.DealFlop();
            var flop = _boardManager.CommunityCards;
            SetPhase(GamePhase.Flop);
            OnCommunityCardsUpdated?.Invoke(flop);
            yield return DelaySeconds(GetCommunityRevealDuration(flop.Count - boardCount));
            yield return null; // extra frame so UI reveal coroutine finishes updating _revealedCommunityCount
            boardCount = flop.Count;
            yield return StartCoroutine(BettingRound(active, sbIndex));
            if (GetNonFolded(active).Count <= 1) { yield return StartCoroutine(EndRound(active)); yield break; }

            // ── Turn ──────────────────────────────────────────────────────
            yield return CollectStreetBetsBeforeNextStreet(active);
            _boardManager.DealTurn();
            var board4 = _boardManager.CommunityCards;
            SetPhase(GamePhase.Turn);
            OnCommunityCardsUpdated?.Invoke(board4);
            yield return DelaySeconds(GetCommunityRevealDuration(board4.Count - boardCount));
            yield return null; // extra frame so UI reveal coroutine finishes updating _revealedCommunityCount
            boardCount = board4.Count;
            yield return StartCoroutine(BettingRound(active, sbIndex));
            if (GetNonFolded(active).Count <= 1) { yield return StartCoroutine(EndRound(active)); yield break; }

            // ── River ─────────────────────────────────────────────────────
            yield return CollectStreetBetsBeforeNextStreet(active);
            _boardManager.DealRiver();
            var board5 = _boardManager.CommunityCards;
            SetPhase(GamePhase.River);
            OnCommunityCardsUpdated?.Invoke(board5);
            yield return DelaySeconds(GetCommunityRevealDuration(board5.Count - boardCount));
            yield return null; // extra frame so UI reveal coroutine finishes updating _revealedCommunityCount
            yield return StartCoroutine(BettingRound(active, sbIndex));

            yield return StartCoroutine(EndRound(active));
        }

        private IEnumerator BettingRound(List<PlayerState> players, int startIndex)
        {
            int playersRemainingToAct = CountPlayersWhoCanAct(players);
            if (playersRemainingToAct <= 0)
                yield break;

            int seatIndex   = startIndex % players.Count;
            int safetyLimit = players.Count * players.Count * 4;
            int iterations  = 0;

            while (playersRemainingToAct > 0 && iterations++ < safetyLimit)
            {
                if (GetNonFolded(players).Count <= 1)
                    break;

                int currentIndex = seatIndex % players.Count;
                var player       = players[currentIndex];

                if (player.HasFolded || player.IsAllIn)
                {
                    seatIndex++;
                    continue;
                }

                yield return WaitWhileOptionsMenuOpen();

                int  betBeforeAction = _bettingManager.CurrentBet;
                bool actionApplied   = false;

                _awaitingHumanInput = player.Type == PlayerType.Human;
                OnPlayerTurn?.Invoke(player);

                if (player.Type == PlayerType.AI)
                {
                    yield return DelayForAiAction();
                    var (action, raise) = _aiController.DecideAction(
                        player, _boardManager.CommunityCards, _bettingManager, IsTestMode);
                    int playerBetBefore = player.CurrentBet;
                    if (_bettingManager.ProcessAction(player, action, raise))
                    {
                        actionApplied = true;
                        int displayAmount = GetActionDisplayAmount(player, action, raise, playerBetBefore);
                        OnPlayerAction?.Invoke(player, action, displayAmount);
                        string detail = action == BettingAction.Raise ? $" +${raise}" : "";
                        OnGameMessage?.Invoke($"{player.Name}: {action}{detail}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[GameManager] Rejected {player.Name} action {action} (raise={raise}, " +
                            $"bet={player.CurrentBet}, table={_bettingManager.CurrentBet}).");
                    }
                }
                else
                {
                    yield return new WaitUntil(() => !_awaitingHumanInput && !IsOptionsMenuOpen);

                    int humanBetBefore = player.CurrentBet;
                    if (_bettingManager.ProcessAction(player, _humanAction, _humanRaiseAmount))
                    {
                        actionApplied = true;
                        int displayAmount = GetActionDisplayAmount(player, _humanAction, _humanRaiseAmount, humanBetBefore);
                        OnPlayerAction?.Invoke(player, _humanAction, displayAmount);
                        string detail = _humanAction == BettingAction.Raise ? $" +${_humanRaiseAmount}" : "";
                        OnGameMessage?.Invoke($"You: {_humanAction}{detail}");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[GameManager] Rejected human action {_humanAction} (raise={_humanRaiseAmount}, " +
                            $"bet={player.CurrentBet}, table={_bettingManager.CurrentBet}).");
                    }
                }

                if (!actionApplied)
                    continue;

                NotifyPlayersUpdated();

                if (player.Type == PlayerType.AI && _aiActionDelay <= 0f)
                    yield return null;

                seatIndex++;

                if (_bettingManager.CurrentBet > betBeforeAction)
                    playersRemainingToAct = CountOtherPlayersWhoCanAct(players, currentIndex);
                else
                    playersRemainingToAct--;
            }
        }

        private static int CountPlayersWhoCanAct(IReadOnlyList<PlayerState> players)
            => players.Count(p => !p.HasFolded && !p.IsAllIn);

        private static int CountOtherPlayersWhoCanAct(IReadOnlyList<PlayerState> players, int excludeIndex)
        {
            int count = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (i == excludeIndex)
                    continue;
                if (!players[i].HasFolded && !players[i].IsAllIn)
                    count++;
            }

            return count;
        }

        private IEnumerator EndRound(List<PlayerState> active)
        {
            yield return CollectStreetBetsBeforeNextStreet(active);

            SetPhase(GamePhase.Showdown);

            var contenders = GetNonFolded(active);
            if (contenders.Count == 0)
                yield break;

            RoundWinners roundWinners = ResolveRoundWinners(contenders);
            LastWinningHand   = roundWinners.BestEvaluation;
            LastRoundWinners  = roundWinners.Players;
            LastShowdownHands       = roundWinners.ShowdownHands;
            LastShowdownEvaluations = roundWinners.ShowdownEvaluations;

            LastGrossPot   = _bettingManager.Pot;
            bool flopDealt = _boardManager.CommunityCards.Count >= 3;
            RakeResult rakeResult = _rake.Evaluate(LastGrossPot, _bigBlind, flopDealt);
            LastRakeAmount      = rakeResult.Amount;
            LastRakeDisplayText = _rake.FormatDisplay(rakeResult);
            LastPotAwarded = LastGrossPot - LastRakeAmount;

            PotAward.Split(LastPotAwarded, roundWinners.Players);

            OnGameMessage?.Invoke(BuildRoundEndMessage(
                roundWinners.Players, LastPotAwarded, LastRakeAmount, LastRakeDisplayText));
            OnWinnerDetermined?.Invoke(roundWinners.Players[0]);
            NotifyPlayersUpdated();

            _awaitingWinnerDismiss = true;
            yield return new WaitUntil(() => !_awaitingWinnerDismiss);

            OnRoundEnded?.Invoke();
            DealerIndex = (DealerIndex + 1) % active.Count;
            SetPhase(GamePhase.RoundOver);
        }

        private struct RoundWinners
        {
            public List<PlayerState> Players;
            public WinningHandEvaluation BestEvaluation;
            public List<(PlayerState Player, HandResult Result)> ShowdownHands;
            public List<(PlayerState Player, WinningHandEvaluation Evaluation)> ShowdownEvaluations;

            public RoundWinners(
                List<PlayerState> players,
                WinningHandEvaluation bestEvaluation,
                List<(PlayerState Player, HandResult Result)> showdownHands,
                List<(PlayerState Player, WinningHandEvaluation Evaluation)> showdownEvaluations)
            {
                Players              = players;
                BestEvaluation       = bestEvaluation;
                ShowdownHands        = showdownHands;
                ShowdownEvaluations  = showdownEvaluations;
            }
        }

        private RoundWinners ResolveRoundWinners(List<PlayerState> contenders)
        {
            WinningHandEvaluation bestEval = null;
            var evaluated = new List<(PlayerState player, WinningHandEvaluation evaluation)>();

            foreach (PlayerState player in contenders)
            {
                var cards = new List<Card>(player.HoleCards);
                cards.AddRange(_boardManager.CommunityCards);
                if (cards.Count < 5)
                    continue;

                WinningHandEvaluation evaluation = HandEvaluator.EvaluateBest(cards);
                evaluated.Add((player, evaluation));

                if (bestEval == null || evaluation.Result.CompareTo(bestEval.Result) > 0)
                    bestEval = evaluation;
            }

            var showdownHands = new List<(PlayerState Player, HandResult Result)>(evaluated.Count);
            var showdownEvaluations = new List<(PlayerState Player, WinningHandEvaluation Evaluation)>(evaluated.Count);
            foreach ((PlayerState player, WinningHandEvaluation evaluation) in evaluated)
            {
                showdownHands.Add((player, evaluation.Result));
                showdownEvaluations.Add((player, evaluation));
            }

            var winners = new List<PlayerState>();
            if (bestEval != null)
            {
                foreach ((PlayerState player, WinningHandEvaluation evaluation) in evaluated)
                {
                    if (evaluation.Result.CompareTo(bestEval.Result) == 0)
                        winners.Add(player);
                }
            }
            else
            {
                winners.Add(contenders[0]);
            }

            return new RoundWinners(winners, bestEval, showdownHands, showdownEvaluations);
        }

        private static string BuildRoundEndMessage(
            IReadOnlyList<PlayerState> winners, int netPot, int rakeAmount, string rakeDisplay)
        {
            if (winners == null || winners.Count == 0)
                return string.Empty;

            string names = winners.Count == 1
                ? winners[0].Name
                : string.Join(" & ", winners.Select(p => p.Name));

            string rakeNote = rakeAmount > 0 && !string.IsNullOrEmpty(rakeDisplay)
                ? $" (rake {rakeDisplay})"
                : string.Empty;

            if (winners.Count == 1)
                return $"{names} wins the pot of ${netPot}{rakeNote}!";

            return $"{names} split the pot of ${netPot}{rakeNote}!";
        }

        /// <summary>Called by the UI to submit the human player's chosen betting action.</summary>
        public void SubmitPlayerAction(BettingAction action, int raiseAmount = 0)
        {
            if (!_awaitingHumanInput) return;
            _humanAction        = action;
            _humanRaiseAmount   = raiseAmount;
            _awaitingHumanInput = false;
        }

        private void ResetBetsForNewPhase(List<PlayerState> players)
        {
            _bettingManager.ResetPhase();
            foreach (var p in players) p.CurrentBet = 0;
        }

        private IEnumerator CollectStreetBetsBeforeNextStreet(List<PlayerState> active)
        {
            UIManager ui = ResolveUiManager();
            if (ui != null)
                yield return ui.CollectStreetBetsToPot();

            ResetBetsForNewPhase(active);
        }

        private UIManager ResolveUiManager()
        {
            if (_uiManager == null)
                _uiManager = FindFirstObjectByType<UIManager>();

            return _uiManager;
        }

        private static int GetActionDisplayAmount(
            PlayerState player, BettingAction action, int raiseAmount, int playerBetBefore)
        {
            switch (action)
            {
                case BettingAction.Call:
                case BettingAction.AllIn:
                    return Mathf.Max(0, player.CurrentBet - playerBetBefore);
                case BettingAction.Raise:
                    return raiseAmount;
                default:
                    return 0;
            }
        }

        private List<PlayerState> GetNonFolded(List<PlayerState> players)
            => players.Where(p => !p.HasFolded).ToList();

        private static bool IsTestMode =>
            OptionsMenu.Instance != null && OptionsMenu.Instance.TestMode;

        private static bool IsOptionsMenuOpen =>
            OptionsMenu.Instance != null && OptionsMenu.Instance.IsOpen;

        private IEnumerator WaitWhileOptionsMenuOpen()
        {
            yield return new WaitUntil(() => !IsOptionsMenuOpen);
        }

        private IEnumerator DelayForAiAction()
        {
            float elapsed = 0f;
            while (true)
            {
                float target = _aiActionDelay;
                if (target <= 0f)
                    yield break;
                if (elapsed >= target)
                    yield break;

                if (!IsOptionsMenuOpen)
                    elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator DelaySeconds(float seconds)
        {
            if (seconds <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!IsOptionsMenuOpen)
                    elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void ApplyGodModeIfEnabled(List<PlayerState> players)
        {
            if (OptionsMenu.Instance == null || !OptionsMenu.Instance.GodMode)
                return;

            foreach (PlayerState player in players)
            {
                if (player.Type == PlayerType.Human && player.Chips < 100_000)
                    player.Chips = 100_000;
            }
        }

        private void NotifyPlayersUpdated() => OnPlayersUpdated?.Invoke(Players);

        private void SetPhase(GamePhase phase)
        {
            CurrentPhase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            ButtonLabelStyle.RegisterFontSizeProvider(() => _buttonFontSize);

            var buttonRow = GameObject.Find("ButtonRow")?.GetComponent<ButtonRowFontSize>();
            if (buttonRow != null)
                buttonRow.Apply();
            else
                FindObjectOfType<UIManager>(includeInactive: true)?.ApplyButtonRowSize();

            var tableLayout = FindObjectOfType<TableLayoutManager>(includeInactive: true);
            tableLayout?.SetCardLayout(120f, _cardGap);
        }
#endif
    }
}

