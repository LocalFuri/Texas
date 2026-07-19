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
        private const float PanelWidth = 320f;
        private const float MarginTop = 16f;
        /// <summary>Inset from the right edge (panel sits 40px in from the right).</summary>
        private const float MarginRight = 40f;

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
            AnchorTopRight();

            var sb = new StringBuilder(256);

            if (advice.IsAceMaverick && advice.IsPreflop)
            {
                sb.Append(FormatSpotLine(advice)).AppendLine();

                bool hasActionLine = TryFormatAceActionLine(advice, out string actionLine);
                if (hasActionLine)
                {
                    sb.AppendLine();
                    sb.Append(actionLine).AppendLine();
                }

                if (advice.AmountToCall > 0 || advice.FacingAllIn)
                {
                    if (!hasActionLine)
                        sb.AppendLine();
                    sb.Append("Pot Odds: ").Append(advice.PotOddsPercent.ToString("0")).Append('%').AppendLine();
                }

                sb.AppendLine();
                sb.Append("Confidence: ").Append(advice.ConfidencePercent).Append('%').AppendLine();
                sb.AppendLine();
                sb.Append("Reason: ").Append(advice.Explanation ?? string.Empty);
            }
            else
            {
                sb.Append("Confidence: ").Append(advice.ConfidencePercent).Append('%');

                if (!string.IsNullOrEmpty(advice.Explanation))
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.Append("Reason: ").Append("<color=").Append(ColorMuted).Append('>')
                        .Append(advice.Explanation).Append("</color>");
                }
            }

            _text.text = sb.ToString();
            FitPanelToContent();
            SetOverlayVisible(true);
        }

        /// <summary>
        /// Ace Coach call/open/raise line from snapshot fields only (display).
        /// Fold/Check → no line.
        /// </summary>
        private static bool TryFormatAceActionLine(HumanTrainerAdvice advice, out string line)
        {
            line = null;
            if (advice == null)
                return false;

            switch (advice.RecommendedAction)
            {
                case BettingAction.Call:
                    line = "Call: " + advice.AmountToCall;
                    return true;

                case BettingAction.Raise:
                {
                    bool unopened = !advice.FacingRaise && advice.StreetRaiseCount <= 0;
                    line = unopened
                        ? "Open to: " + advice.RecommendedTotalBet
                        : "Raise to: " + advice.RecommendedTotalBet;
                    return true;
                }

                default:
                    return false;
            }
        }

        /// <summary>Fixed width; height grows/shrinks with the TMP content + padding.</summary>
        private void FitPanelToContent()
        {
            if (_panel == null || _text == null)
                return;

            float contentWidth = PanelWidth - PadX * 2f;
            float contentHeight = _text.GetPreferredValues(_text.text, contentWidth, 0f).y;
            _panel.sizeDelta = new Vector2(PanelWidth, contentHeight + PadY * 2f);
        }

        private void AnchorTopRight()
        {
            if (_panel == null)
                return;

            _panel.anchorMin = new Vector2(1f, 1f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 1f);
            _panel.anchoredPosition = new Vector2(-MarginRight, -MarginTop);
        }

        /// <summary>Compact spot label from existing raise/caller counts (display only).</summary>
        private static string FormatSpotLine(HumanTrainerAdvice advice)
        {
            if (advice.FacingAllIn)
                return "All-in";

            int raises = advice.StreetRaiseCount;
            int callers = advice.CallersBefore;
            string callerWord = callers == 1 ? "Caller" : "Callers";

            if (raises <= 0 && !advice.FacingRaise)
                return callers > 0 ? $"Limped + {callers} {callerWord}" : "Unopened";

            if (raises == 1)
                return callers > 0 ? $"Raise + {callers} {callerWord}" : "Raise";

            if (raises == 2)
                return callers > 0 ? $"3-bet + {callers} {callerWord}" : "3-bet";

            return callers > 0 ? $"{raises} raises + {callers} {callerWord}" : $"{raises} raises";
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
                _text.enableWordWrapping = true;
                _text.overflowMode = TextOverflowModes.Overflow;
            }
        }

        private void EnsureOverlay()
        {
            if (_panel != null)
            {
                ApplyOverlayStyle();
                AnchorTopRight();
                return;
            }

            Canvas parent = FindOverlayCanvas();
            if (parent == null)
                return;

            _canvas = parent;
            var go = new GameObject("AiCoachOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.sizeDelta = new Vector2(PanelWidth, 0f);

            _panelBg = go.GetComponent<Image>();
            _panelBg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            _textRt = textGo.GetComponent<RectTransform>();
            _textRt.anchorMin = Vector2.zero;
            _textRt.anchorMax = Vector2.one;

            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.raycastTarget = false;
            _text.richText = true;

            ApplyOverlayStyle();
            AnchorTopRight();
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
