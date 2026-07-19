using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem.Dev
{
    /// <summary>
    /// Dev-only overlay: shows the shared <see cref="HumanTrainerAdvice"/> on the human turn.
    /// Does not compute recommendations — reads UIManager's single cached advice.
    /// </summary>
    public sealed class AiCoachMode : MonoBehaviour
    {
        public const string PrefsKey = "TexasHoldem.AiCoachMode.Enabled";
        private const string LogPrefix = "[AiCoach]";

        private const float FontSize = 19f;
        private const float LineSpacing = 108f;
        private const float PadX = 16f;
        private const float PadY = 14f;
        private const float PanelAlpha = 0.93f;

        private const string ColorCyan = "#00E5FF";
        private const string ColorRaise = "#3DDC64";
        private const string ColorCall = "#F0D040";
        private const string ColorFold = "#E84A4A";
        private const string ColorMuted = "#D0D4DA";

        private UIManager _ui;
        private GameManager _game;
        private Canvas _canvas;
        private RectTransform _panel;
        private Image _panelBg;
        private RectTransform _textRt;
        private TMP_Text _text;
        private bool _loggedEnabled;

        public static bool IsEnabled
        {
            get => PlayerPrefs.GetInt(PrefsKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<AiCoachMode>() != null)
                return;

            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
                gm.gameObject.AddComponent<AiCoachMode>();
        }

        private void OnEnable()
        {
            Bind();
            if (IsEnabled && !_loggedEnabled)
            {
                _loggedEnabled = true;
                Debug.LogWarning($"{LogPrefix} Enabled (Dev). Overlay uses shared TrainerAdvice.");
            }
        }

        private void Start() => Bind();

        private void OnDisable()
        {
            Unbind();
            SetOverlayVisible(false);
        }

        private void Bind()
        {
            if (_ui != null)
                return;

            _ui = UIManager.Instance != null
                ? UIManager.Instance
                : FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            _game = FindFirstObjectByType<GameManager>();

            if (_ui != null)
                _ui.OnHumanTrainerAdviceUpdated += OnAdviceUpdated;

            if (_game != null)
            {
                _game.OnPlayerTurn.AddListener(OnPlayerTurn);
                _game.OnPlayerAction.AddListener(OnPlayerAction);
                _game.OnRoundStarting.AddListener(OnRoundStarting);
            }

            RefreshFromCache();
        }

        private void Unbind()
        {
            if (_ui != null)
            {
                _ui.OnHumanTrainerAdviceUpdated -= OnAdviceUpdated;
                _ui = null;
            }

            if (_game != null)
            {
                _game.OnPlayerTurn.RemoveListener(OnPlayerTurn);
                _game.OnPlayerAction.RemoveListener(OnPlayerAction);
                _game.OnRoundStarting.RemoveListener(OnRoundStarting);
                _game = null;
            }
        }

        private void OnRoundStarting()
        {
            if (!IsEnabled)
                return;
            SetOverlayVisible(false);
        }

        private void OnPlayerTurn(PlayerState player)
        {
            if (!IsEnabled)
            {
                SetOverlayVisible(false);
                return;
            }

            if (player == null || player.Type != PlayerType.Human)
            {
                SetOverlayVisible(false);
                return;
            }

            RefreshFromCache();
        }

        private void OnPlayerAction(PlayerState player, BettingAction action, int amount)
        {
            if (player != null && player.Type == PlayerType.Human)
                SetOverlayVisible(false);
        }

        private void OnAdviceUpdated(HumanTrainerAdvice advice)
        {
            if (!IsEnabled)
            {
                SetOverlayVisible(false);
                return;
            }

            ShowAdvice(advice);
        }

        private void RefreshFromCache()
        {
            if (!IsEnabled || _ui == null)
            {
                SetOverlayVisible(false);
                return;
            }

            ShowAdvice(_ui.CurrentHumanTrainerAdvice);
        }

        private void ShowAdvice(HumanTrainerAdvice advice)
        {
            if (advice == null)
            {
                SetOverlayVisible(false);
                return;
            }

            EnsureOverlay();
            if (_text == null || _panel == null)
                return;

            ApplyOverlayStyle();

            var sb = new StringBuilder(384);
            sb.Append("<b>AI Coach</b>");
            if (advice.IsAceMaverick)
                sb.Append(" — Ace Maverick");
            sb.AppendLine();

            // Ace Maverick preflop: same data, sectioned for readability.
            if (advice.IsAceMaverick && advice.IsPreflop)
            {
                sb.AppendLine();
                sb.Append("<b>Hand</b>").AppendLine();
                sb.Append("Position: ").Append(Cyan(advice.Position ?? "?")).AppendLine();
                sb.Append("Hole cards: ").Append(Cyan(advice.HoleCards ?? "?")).AppendLine();

                sb.AppendLine();
                sb.Append("<b>Situation</b>").AppendLine();
                sb.Append("Players in pot: ").Append(advice.PlayersInPot);
                sb.Append(" (callers before ").Append(advice.CallersBefore).Append(')').AppendLine();
                sb.Append("Raise count: ").Append(advice.StreetRaiseCount).AppendLine();
                sb.Append("All-in: ").Append(advice.FacingAllIn ? "Yes" : "No").AppendLine();
                sb.Append("Stack depth: ").Append(advice.EffectiveStackBB.ToString("0"))
                    .Append("bb (").Append(advice.EffectiveStackBand ?? "?").Append(')').AppendLine();
                sb.Append("Call amount: ").Append(advice.AmountToCall).AppendLine();
                if (advice.FacingAllIn || advice.AmountToCall > 0)
                    sb.Append("Pot odds: ").Append(advice.PotOddsPercent.ToString("0.0")).Append('%').AppendLine();

                sb.AppendLine();
                sb.Append("<b>Decision</b>").AppendLine();
                sb.Append("Recommended: ").Append(ColoredRecommendation(advice)).AppendLine();
                sb.Append("Sizing: ").Append(FormatSizing(advice)).AppendLine();
                sb.Append("Confidence: ").Append(advice.ConfidencePercent).Append('%');
                _panel.sizeDelta = new Vector2(340f, 320f);
            }
            else
            {
                sb.AppendLine();
                sb.Append("<b>Decision</b>").AppendLine();
                sb.Append("Recommended: ").Append(ColoredRecommendation(advice));

                if (advice.RecommendedAction == BettingAction.Raise
                    || (advice.RecommendedAction == BettingAction.AllIn && advice.RecommendedRaiseIncrement > 0))
                {
                    sb.AppendLine();
                    sb.Append("Amount: ").Append(advice.RecommendedTotalBet);
                    if (advice.RecommendedRaiseIncrement > 0
                        && advice.RecommendedTotalBet != advice.RecommendedRaiseIncrement)
                    {
                        sb.Append(" (increment ").Append(advice.RecommendedRaiseIncrement).Append(')');
                    }
                }
                else if (advice.RecommendedAction == BettingAction.AllIn)
                {
                    sb.AppendLine();
                    sb.Append("Amount: All-In");
                }

                if (!string.IsNullOrEmpty(advice.Explanation))
                {
                    sb.AppendLine();
                    sb.Append("<color=").Append(ColorMuted).Append('>').Append(advice.Explanation).Append("</color>");
                }

                _panel.sizeDelta = new Vector2(320f, 150f);
            }

            _text.text = sb.ToString();
            SetOverlayVisible(true);
        }

        private static string Cyan(string value) =>
            $"<color={ColorCyan}>{value}</color>";

        private static string ColoredRecommendation(HumanTrainerAdvice advice)
        {
            string label = advice.DecisionLabel ?? advice.AdviceLabel ?? "?";
            if (advice.RecommendedAction == BettingAction.Raise && advice.RecommendedTotalBet > 0)
                label = $"{label} to {advice.RecommendedTotalBet}";
            else if (advice.RecommendedAction == BettingAction.AllIn
                     && label.IndexOf("All-In", System.StringComparison.OrdinalIgnoreCase) < 0)
                label = $"{label} (All-In)";

            string hex = RecommendationColorHex(advice.RecommendedAction, advice.DecisionLabel);
            return $"<b><color={hex}>{label}</color></b>";
        }

        private static string RecommendationColorHex(BettingAction action, string decisionLabel)
        {
            switch (action)
            {
                case BettingAction.Raise:
                    return ColorRaise;
                case BettingAction.Call:
                    return ColorCall;
                case BettingAction.Fold:
                    return ColorFold;
                case BettingAction.AllIn:
                    return ColorRaise;
                case BettingAction.Check:
                    return ColorMuted;
                default:
                    if (decisionLabel == "Bet")
                        return ColorRaise;
                    return ColorMuted;
            }
        }

        private static string FormatSizing(HumanTrainerAdvice advice)
        {
            if (advice == null)
                return "—";

            switch (advice.RecommendedAction)
            {
                case BettingAction.Raise:
                    return $"total {advice.RecommendedTotalBet} (inc {advice.RecommendedRaiseIncrement})";
                case BettingAction.AllIn:
                    return "All-In";
                case BettingAction.Call:
                    return $"call {advice.AmountToCall}";
                default:
                    return "—";
            }
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_panel != null)
                _panel.gameObject.SetActive(visible);
        }

        private void ApplyOverlayStyle()
        {
            if (_panelBg != null)
                _panelBg.color = new Color(0.04f, 0.06f, 0.10f, PanelAlpha);

            if (_textRt != null)
            {
                _textRt.offsetMin = new Vector2(PadX, PadY);
                _textRt.offsetMax = new Vector2(-PadX, -PadY);
            }

            if (_text != null)
            {
                _text.fontSize = FontSize;
                _text.lineSpacing = LineSpacing;
                _text.richText = true;
                _text.color = Color.white;
            }
        }

        private void EnsureOverlay()
        {
            if (_panel != null)
            {
                ApplyOverlayStyle();
                return;
            }

            Canvas parent = FindOverlayCanvas();
            if (parent == null)
                return;

            _canvas = parent;
            var go = new GameObject("AiCoachOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 1f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchoredPosition = new Vector2(16f, -16f);
            _panel.sizeDelta = new Vector2(340f, 320f);

            _panelBg = go.GetComponent<Image>();
            _panelBg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            _textRt = textGo.GetComponent<RectTransform>();
            _textRt.anchorMin = Vector2.zero;
            _textRt.anchorMax = Vector2.one;

            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = true;
            _text.raycastTarget = false;
            _text.richText = true;

            ApplyOverlayStyle();
            go.SetActive(false);
        }

        private static Canvas FindOverlayCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Canvas best = null;
            int bestOrder = int.MinValue;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                if (c == null || !c.isActiveAndEnabled)
                    continue;
                if (c.renderMode == RenderMode.WorldSpace)
                    continue;
                if (best == null || c.sortingOrder >= bestOrder)
                {
                    best = c;
                    bestOrder = c.sortingOrder;
                }
            }

            return best;
        }
    }
}
