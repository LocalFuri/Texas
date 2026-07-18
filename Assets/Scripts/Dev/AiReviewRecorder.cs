using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Developer-only observer: records interesting human decisions during normal play
    /// as JSON Lines under <see cref="Application.persistentDataPath"/>.
    /// Does not change AI strategy or gameplay.
    /// </summary>
    public sealed class AiReviewRecorder : MonoBehaviour
    {
        private const string FileName = "TexasHoldem_AI_Review.jsonl";
        private const string LogPrefix = "[AiReview]";

        private GameManager _game;
        private UIManager _ui;
        private int _handNumber;
        private int _decisionIndexInHand;
        private int _sessionNetAtHandStart;
        private bool _hasTurnSnapshot;
        private TurnSnapshot _turnSnapshot;
        private readonly List<PendingDecision> _pending = new List<PendingDecision>(8);
        private bool _loggedPath;
        private bool _applicationQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<AiReviewRecorder>() != null)
                return;

            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.gameObject.AddComponent<AiReviewRecorder>();
        }

        private void OnEnable()
        {
            Application.quitting += OnApplicationQuitting;
            Bind(FindFirstObjectByType<GameManager>());
            LogPathOnce();
        }

        private void Start()
        {
            if (_game == null)
                Bind(FindFirstObjectByType<GameManager>());
            LogPathOnce();
        }

        private void OnDisable()
        {
            Application.quitting -= OnApplicationQuitting;
            if (!_applicationQuitting)
                FlushPendingIncomplete();
            Unbind();
        }

        private void OnApplicationQuitting()
        {
            _applicationQuitting = true;
            FlushPendingIncomplete();
        }

        private void Bind(GameManager game)
        {
            if (game == null || _game == game)
                return;

            Unbind();
            _game = game;
            _ui = UIManager.Instance != null
                ? UIManager.Instance
                : FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);

            _game.OnRoundStarting.AddListener(OnRoundStarting);
            _game.OnPlayerTurn.AddListener(OnPlayerTurn);
            _game.OnPlayerAction.AddListener(OnPlayerAction);
            _game.OnRoundEnded.AddListener(OnRoundEnded);
        }

        private void Unbind()
        {
            if (_game == null)
                return;

            _game.OnRoundStarting.RemoveListener(OnRoundStarting);
            _game.OnPlayerTurn.RemoveListener(OnPlayerTurn);
            _game.OnPlayerAction.RemoveListener(OnPlayerAction);
            _game.OnRoundEnded.RemoveListener(OnRoundEnded);
            _game = null;
            _ui = null;
        }

        private void LogPathOnce()
        {
            if (_loggedPath)
                return;

            _loggedPath = true;
            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "{0} Enabled. Output: {1}",
                LogPrefix,
                ResolveOutputPath());
        }

        private void OnRoundStarting()
        {
            // Drop unfinished decisions from a prior hand that never reached OnRoundEnded.
            _pending.Clear();
            _hasTurnSnapshot = false;
            _decisionIndexInHand = 0;
            _handNumber++;

            PlayerState human = FindHuman(_game?.Players);
            _sessionNetAtHandStart = human != null ? human.SessionNetProfit : 0;
        }

        private void OnPlayerTurn(PlayerState player)
        {
            _hasTurnSnapshot = false;
            if (_game == null || player == null || player.Type != PlayerType.Human)
                return;

            _turnSnapshot = CaptureTurnSnapshot(player);
            _hasTurnSnapshot = true;
        }

        private void OnPlayerAction(PlayerState player, BettingAction action, int amount)
        {
            if (_game == null || player == null || player.Type != PlayerType.Human)
                return;

            if (!_hasTurnSnapshot)
                return;

            TurnSnapshot snap = _turnSnapshot;
            _hasTurnSnapshot = false;

            if (!IsInteresting(snap, action))
                return;

            AiReviewDecisionDto record = CreatePendingRecord(snap, player, action, amount);
            _pending.Add(new PendingDecision(record));
            _decisionIndexInHand++;
        }

        private void OnRoundEnded()
        {
            if (_game == null || _pending.Count == 0)
            {
                _pending.Clear();
                return;
            }

            PlayerState human = FindHuman(_game.Players);
            int? heroProfitLoss = human != null
                ? human.SessionNetProfit - _sessionNetAtHandStart
                : (int?)null;

            string outcomeStatus = ResolveOutcomeStatus(
                _game.LastHandWasBbWalk,
                _game.LastShowdownHands);

            EnrichPendingWithOutcome(
                _pending,
                _game.LastRoundWinners,
                _game.LastShowdownHands,
                _game.LastShowdownEvaluations,
                _game.LastHandWasBbWalk,
                heroProfitLoss,
                outcomeStatus);

            int written = AppendPendingRecords(_pending);
            RemoveWritten(_pending);

            Debug.LogFormat(
                LogType.Log,
                LogOption.NoStacktrace,
                null,
                "{0} Hand {1}: wrote {2} decision(s).",
                LogPrefix,
                _handNumber,
                written);
        }

        private void FlushPendingIncomplete()
        {
            if (_pending.Count == 0)
                return;

            EnrichPendingWithOutcome(
                _pending,
                winners: null,
                showdownHands: null,
                showdownEvaluations: null,
                lastHandWasBbWalk: false,
                heroProfitLoss: null,
                outcomeStatus: "incomplete");

            int written = AppendPendingRecords(_pending);
            RemoveWritten(_pending);
            _hasTurnSnapshot = false;

            if (written > 0)
            {
                Debug.LogFormat(
                    LogType.Log,
                    LogOption.NoStacktrace,
                    null,
                    "{0} Flushed {1} incomplete decision(s).",
                    LogPrefix,
                    written);
            }
        }

        private static void RemoveWritten(List<PendingDecision> pending)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (pending[i].Written)
                    pending.RemoveAt(i);
            }
        }

        private TurnSnapshot CaptureTurnSnapshot(PlayerState human)
        {
            int equity = _ui != null ? _ui.CachedHumanEquityPercent : -1;
            float equityForAdvice = equity >= 0 ? equity : 0f;

            string adviceLabel = string.Empty;
            string recommendedAction = string.Empty;
            int recommendedRaiseAmount = 0;
            string trainerRecommendation = string.Empty;

            if (_game.TryResolveHumanTrainerAdvice(
                    human,
                    equityForAdvice,
                    out BettingAdvice advice,
                    out string label,
                    out BettingAction recAction,
                    out int raiseAmount))
            {
                adviceLabel = label ?? string.Empty;
                trainerRecommendation = advice.ToString();
                recommendedAction = recAction.ToString();
                recommendedRaiseAmount = raiseAmount;
            }

            bool facingRaise = _game.CurrentPhase == GamePhase.PreFlop
                ? (_game.CurrentBet > _game.BigBlindAmount || _game.StreetRaiseCount >= 1)
                : (_game.CurrentBet > 0 && _game.GetCallAmountFor(human) > 0);

            PreflopHandGroup preflopGroup = PreflopHandGroup.Weak;
            if (human.HoleCards != null && human.HoleCards.Count >= 2)
                preflopGroup = PreflopStrategy.ClassifyHand(human.HoleCards);

            string position = FormatSeat(_game.GetPreflopSeatBucket(human));
            string street = _game.CurrentPhase.ToString();
            int pot = _game.PotAmount;
            int amountToCall = _game.GetCallAmountFor(human);

            return new TurnSnapshot
            {
                HoleCards = FormatCards(human.HoleCards),
                Position = position,
                PreflopGroup = preflopGroup,
                Stacks = CaptureStacks(_game.Players),
                PotBeforeAction = pot,
                BoardCards = FormatCards(_game.CommunityCards),
                BettingHistory = CaptureHistory(_game.HandActions),
                Street = street,
                IsPreflop = _game.CurrentPhase == GamePhase.PreFlop,
                CurrentBet = _game.CurrentBet,
                AmountToCall = amountToCall,
                StreetRaiseCount = _game.StreetRaiseCount,
                FacingRaise = facingRaise,
                TrainerRecommendation = trainerRecommendation,
                RecommendedAction = recommendedAction,
                RecommendedAdviceLabel = adviceLabel,
                RecommendedRaiseAmount = recommendedRaiseAmount,
                CachedEquityPercent = equity,
                HeroStreetBetBefore = human.CurrentBet,
                TrainerInputs = new AiReviewTrainerInputsDto
                {
                    equity = equity,
                    potOdds = ComputePotOddsPercent(pot, amountToCall),
                    boardTexture = FormatBoardTexture(_game.CommunityCards),
                    street = street,
                    position = position,
                },
            };
        }

        private AiReviewDecisionDto CreatePendingRecord(
            TurnSnapshot snap,
            PlayerState human,
            BettingAction action,
            int amountReported)
        {
            return new AiReviewDecisionDto
            {
                schemaVersion = 1,
                utcTimestamp = DateTime.UtcNow.ToString("o"),
                handNumber = _handNumber,
                decisionIndexInHand = _decisionIndexInHand,
                holeCards = snap.HoleCards ?? new List<string>(),
                position = snap.Position ?? string.Empty,
                stacks = snap.Stacks ?? new List<AiReviewStackDto>(),
                potBeforeAction = snap.PotBeforeAction,
                boardCards = snap.BoardCards ?? new List<string>(),
                bettingHistory = snap.BettingHistory ?? new List<AiReviewHistoryDto>(),
                street = snap.Street ?? string.Empty,
                currentBet = snap.CurrentBet,
                amountToCallBeforeAction = snap.AmountToCall,
                streetRaiseCount = snap.StreetRaiseCount,
                facingRaise = snap.FacingRaise,
                trainerRecommendation = snap.TrainerRecommendation ?? string.Empty,
                recommendedAdviceLabel = snap.RecommendedAdviceLabel ?? string.Empty,
                recommendedAction = snap.RecommendedAction ?? string.Empty,
                recommendedBetOrRaiseAmount = snap.RecommendedRaiseAmount,
                trainerInputs = snap.TrainerInputs ?? new AiReviewTrainerInputsDto(),
                cachedHumanEquityPercent = snap.CachedEquityPercent,
                actualAction = action.ToString(),
                actionAmountReportedByEvent = amountReported,
                heroStreetBetBeforeAction = snap.HeroStreetBetBefore,
                heroStreetBetAfterAction = human.CurrentBet,
                totalStreetBetAfterAction = _game != null ? _game.CurrentBet : 0,
                winners = new List<string>(),
                showdown = null,
                lastHandWasBbWalk = false,
                heroProfitLossAvailable = false,
                heroProfitLoss = 0,
                outcomeStatus = string.Empty,
                preflopHandGroup = snap.PreflopGroup.ToString(),
            };
        }

        /// <summary>Same pot-odds percent the trainer uses: 100 * call / (pot + call).</summary>
        private static float ComputePotOddsPercent(int potBeforeAction, int callAmount)
        {
            if (callAmount <= 0)
                return 0f;

            int denominator = potBeforeAction + callAmount;
            if (denominator <= 0)
                return 0f;

            return 100f * callAmount / denominator;
        }

        private static string FormatBoardTexture(IReadOnlyList<Card> communityCards)
        {
            if (communityCards == null || communityCards.Count < 3)
                return string.Empty;

            BoardTextureFlags flags = BoardTextureAnalyzer.Analyze(communityCards);
            return flags == BoardTextureFlags.None ? "Dry" : flags.ToString();
        }

        private static bool IsInteresting(TurnSnapshot snap, BettingAction action)
        {
            if (!snap.IsPreflop)
                return true;

            if (snap.FacingRaise)
                return true;

            bool isWeak = snap.PreflopGroup == PreflopHandGroup.Weak;

            // Skip obvious weak unopened folds.
            if (action == BettingAction.Fold && isWeak)
                return false;

            // Skip weak BB free checks.
            if (action == BettingAction.Check && isWeak)
                return false;

            // Skip weak unopened opens.
            if ((action == BettingAction.Raise || action == BettingAction.AllIn) && isWeak)
                return false;

            // Keep non-weak opens, limps, non-weak folds/checks, etc.
            return true;
        }

        private static string ResolveOutcomeStatus(
            bool bbWalk,
            IReadOnlyList<(PlayerState Player, HandResult Result)> showdownHands)
        {
            if (bbWalk)
                return "walk";

            bool hasShowdown = showdownHands != null && showdownHands.Count > 0;
            return hasShowdown ? "completed" : "folded_no_showdown";
        }

        private static void EnrichPendingWithOutcome(
            List<PendingDecision> pending,
            IReadOnlyList<PlayerState> winners,
            IReadOnlyList<(PlayerState Player, HandResult Result)> showdownHands,
            IReadOnlyList<(PlayerState Player, WinningHandEvaluation Evaluation)> showdownEvaluations,
            bool lastHandWasBbWalk,
            int? heroProfitLoss,
            string outcomeStatus)
        {
            var winnerNames = new List<string>();
            if (winners != null)
            {
                for (int i = 0; i < winners.Count; i++)
                {
                    if (winners[i] != null)
                        winnerNames.Add(winners[i].Name ?? string.Empty);
                }
            }

            List<AiReviewShowdownDto> showdown = BuildShowdown(showdownHands, showdownEvaluations);

            for (int i = 0; i < pending.Count; i++)
            {
                PendingDecision item = pending[i];
                if (item.Written)
                    continue;

                AiReviewDecisionDto dto = item.Record;
                dto.winners = new List<string>(winnerNames);
                dto.showdown = showdown;
                dto.lastHandWasBbWalk = lastHandWasBbWalk;
                dto.outcomeStatus = outcomeStatus ?? string.Empty;

                if (heroProfitLoss.HasValue)
                {
                    dto.heroProfitLossAvailable = true;
                    dto.heroProfitLoss = heroProfitLoss.Value;
                }
                else
                {
                    dto.heroProfitLossAvailable = false;
                    dto.heroProfitLoss = 0;
                }
            }
        }

        private static List<AiReviewShowdownDto> BuildShowdown(
            IReadOnlyList<(PlayerState Player, HandResult Result)> showdownHands,
            IReadOnlyList<(PlayerState Player, WinningHandEvaluation Evaluation)> showdownEvaluations)
        {
            if (showdownHands == null || showdownHands.Count == 0)
                return null;

            var list = new List<AiReviewShowdownDto>(showdownHands.Count);
            for (int i = 0; i < showdownHands.Count; i++)
            {
                (PlayerState player, HandResult result) = showdownHands[i];
                string evalText = result != null ? HandDisplayNames.Format(result) : string.Empty;

                if (showdownEvaluations != null)
                {
                    for (int j = 0; j < showdownEvaluations.Count; j++)
                    {
                        if (showdownEvaluations[j].Player == player
                            && showdownEvaluations[j].Evaluation?.Result != null)
                        {
                            evalText = HandDisplayNames.Format(showdownEvaluations[j].Evaluation.Result);
                            break;
                        }
                    }
                }

                list.Add(new AiReviewShowdownDto
                {
                    playerName = player != null ? player.Name : string.Empty,
                    holeCards = FormatCards(player?.HoleCards),
                    evaluation = evalText ?? string.Empty,
                });
            }

            return list;
        }

        private int AppendPendingRecords(List<PendingDecision> pending)
        {
            if (pending == null || pending.Count == 0)
                return 0;

            var sb = new StringBuilder(1024);
            int count = 0;

            for (int i = 0; i < pending.Count; i++)
            {
                PendingDecision item = pending[i];
                if (item.Written || item.Record == null)
                    continue;

                try
                {
                    string json = JsonUtility.ToJson(item.Record);
                    if (string.IsNullOrEmpty(json))
                        continue;

                    // JsonUtility omits null reference fields; inject explicit JSON null.
                    json = EnsureChatgptReviewNull(json);

                    sb.Append(json);
                    sb.Append('\n');
                    item.Written = true;
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LogPrefix} Serialization failed: {ex.Message}");
                }
            }

            if (count == 0)
                return 0;

            try
            {
                string path = ResolveOutputPath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} File write failed: {ex.Message}");
                // Allow retry on a later flush by clearing Written flags we just set.
                for (int i = 0; i < pending.Count; i++)
                {
                    if (pending[i].Written)
                        pending[i].Written = false;
                }

                return 0;
            }

            return count;
        }

        private static string ResolveOutputPath() =>
            Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Ensures every JSON line includes <c>"chatgptReview":null</c>
        /// (JsonUtility cannot emit a null reference field).
        /// </summary>
        private static string EnsureChatgptReviewNull(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            if (json.IndexOf("\"chatgptReview\"", StringComparison.Ordinal) >= 0)
                return json;

            if (json[json.Length - 1] != '}')
                return json;

            return json.Substring(0, json.Length - 1) + ",\"chatgptReview\":null}";
        }

        private static List<AiReviewStackDto> CaptureStacks(IReadOnlyList<PlayerState> players)
        {
            var list = new List<AiReviewStackDto>();
            if (players == null)
                return list;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerState p = players[i];
                if (p == null)
                    continue;

                list.Add(new AiReviewStackDto
                {
                    playerName = p.Name ?? string.Empty,
                    chips = p.Chips,
                    handStartStack = p.HandStartStack,
                    isHuman = p.Type == PlayerType.Human,
                });
            }

            return list;
        }

        private static List<AiReviewHistoryDto> CaptureHistory(IReadOnlyList<HandActionEntry> actions)
        {
            var list = new List<AiReviewHistoryDto>();
            if (actions == null)
                return list;

            for (int i = 0; i < actions.Count; i++)
            {
                HandActionEntry e = actions[i];
                list.Add(new AiReviewHistoryDto
                {
                    street = e.Street.ToString(),
                    playerName = e.PlayerName ?? string.Empty,
                    action = e.Action.ToString(),
                    amount = e.Amount,
                    pot = e.Pot,
                    streetRaiseCount = e.StreetRaiseCount,
                });
            }

            return list;
        }

        private static List<string> FormatCards(IReadOnlyList<Card> cards)
        {
            var list = new List<string>();
            if (cards == null)
                return list;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                    list.Add(cards[i].ToString());
            }

            return list;
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

        private sealed class PendingDecision
        {
            public AiReviewDecisionDto Record;
            public bool Written;

            public PendingDecision(AiReviewDecisionDto record)
            {
                Record = record;
                Written = false;
            }
        }

        private struct TurnSnapshot
        {
            public List<string> HoleCards;
            public string Position;
            public PreflopHandGroup PreflopGroup;
            public List<AiReviewStackDto> Stacks;
            public int PotBeforeAction;
            public List<string> BoardCards;
            public List<AiReviewHistoryDto> BettingHistory;
            public string Street;
            public bool IsPreflop;
            public int CurrentBet;
            public int AmountToCall;
            public int StreetRaiseCount;
            public bool FacingRaise;
            public string TrainerRecommendation;
            public string RecommendedAction;
            public string RecommendedAdviceLabel;
            public int RecommendedRaiseAmount;
            public int CachedEquityPercent;
            public int HeroStreetBetBefore;
            public AiReviewTrainerInputsDto TrainerInputs;
        }

        [Serializable]
        private sealed class AiReviewDecisionDto
        {
            public int schemaVersion;
            public string utcTimestamp;
            public int handNumber;
            public int decisionIndexInHand;
            public List<string> holeCards;
            public string position;
            public List<AiReviewStackDto> stacks;
            public int potBeforeAction;
            public List<string> boardCards;
            public List<AiReviewHistoryDto> bettingHistory;
            public string street;
            public int currentBet;
            public int amountToCallBeforeAction;
            public int streetRaiseCount;
            public bool facingRaise;
            public string trainerRecommendation;
            public string recommendedAdviceLabel;
            public string recommendedAction;
            public int recommendedBetOrRaiseAmount;
            public AiReviewTrainerInputsDto trainerInputs;
            public int cachedHumanEquityPercent;
            public string actualAction;
            public int actionAmountReportedByEvent;
            public int heroStreetBetBeforeAction;
            public int heroStreetBetAfterAction;
            public int totalStreetBetAfterAction;
            public List<string> winners;
            public List<AiReviewShowdownDto> showdown;
            public bool lastHandWasBbWalk;
            public bool heroProfitLossAvailable;
            public int heroProfitLoss;
            public string outcomeStatus;
            public string preflopHandGroup;
        }

        [Serializable]
        private sealed class AiReviewTrainerInputsDto
        {
            public int equity;
            public float potOdds;
            public string boardTexture;
            public string street;
            public string position;
        }

        [Serializable]
        private sealed class AiReviewStackDto
        {
            public string playerName;
            public int chips;
            public int handStartStack;
            public bool isHuman;
        }

        [Serializable]
        private sealed class AiReviewHistoryDto
        {
            public string street;
            public string playerName;
            public string action;
            public int amount;
            public int pot;
            public int streetRaiseCount;
        }

        [Serializable]
        private sealed class AiReviewShowdownDto
        {
            public string playerName;
            public List<string> holeCards;
            public string evaluation;
        }
    }
}
