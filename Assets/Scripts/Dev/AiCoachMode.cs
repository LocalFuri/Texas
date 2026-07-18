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

        private UIManager _ui;
        private GameManager _game;
        private Canvas _canvas;
        private RectTransform _panel;
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
            if (_text == null)
                return;

            var sb = new StringBuilder(128);
            sb.Append("AI Coach\n");
            sb.Append("Decision: ").Append(advice.DecisionLabel ?? advice.AdviceLabel ?? "?");

            if (advice.RecommendedAction == BettingAction.Raise
                || (advice.RecommendedAction == BettingAction.AllIn && advice.RecommendedRaiseIncrement > 0))
            {
                sb.Append("\nAmount: ").Append(advice.RecommendedTotalBet);
                if (advice.RecommendedRaiseIncrement > 0
                    && advice.RecommendedTotalBet != advice.RecommendedRaiseIncrement)
                {
                    sb.Append(" (increment ").Append(advice.RecommendedRaiseIncrement).Append(')');
                }
            }
            else if (advice.RecommendedAction == BettingAction.AllIn)
            {
                sb.Append("\nAmount: All-In");
            }

            if (!string.IsNullOrEmpty(advice.Explanation))
                sb.Append("\n").Append(advice.Explanation);

            _text.text = sb.ToString();
            SetOverlayVisible(true);
        }

        private void SetOverlayVisible(bool visible)
        {
            if (_panel != null)
                _panel.gameObject.SetActive(visible);
        }

        private void EnsureOverlay()
        {
            if (_panel != null)
                return;

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
            _panel.sizeDelta = new Vector2(280f, 110f);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.82f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 8f);
            textRt.offsetMax = new Vector2(-10f, -8f);

            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 16f;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = true;
            _text.raycastTarget = false;

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
