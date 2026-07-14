using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Screen-space overlay for the post-win flow: collect pot chips, then auto-advance to the next hand.
    /// Works in player builds — uses mouse polling and on-screen feedback (no Console needed).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class WinnerDismissControls : MonoBehaviour
    {
        private const int CanvasSortOrder = 5000;

        private GameManager _gameManager;
        private UIManager   _uiManager;

        private Canvas          _canvas;
        private TMP_Text        _statusText;
        private TMP_Text        _feedbackText;
        private Button          _collectButton;
        private RectTransform   _collectButtonRt;
        private bool            _inputActive;
        private bool            _overlayVisible;
        private string          _feedbackMessage = string.Empty;

        public void Bind(GameManager gameManager, UIManager uiManager)
        {
            _gameManager = gameManager;
            _uiManager   = uiManager;
        }

        /// <summary>Starts polling keys; optionally shows the on-screen buttons.</summary>
        public void Begin(bool showOverlay)
        {
            ResolveUiManager();
            TearDownOverlay();
            BuildOverlay();

            ClearKeyboardFocus();
            _uiManager?.PrepareTableForWinnerDismissInput();

            _inputActive      = true;
            _overlayVisible   = showOverlay;
            _feedbackMessage  = "Tap Collect or press Backspace / B.";
            _canvas.gameObject.SetActive(showOverlay);
            Refresh();

            Debug.Log(
                $"[WinnerDismissControls] Winner pause active — overlay={showOverlay}.",
                this);
        }

        public void End()
        {
            _inputActive    = false;
            _overlayVisible = false;
            if (_canvas != null)
                _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_inputActive)
                return;

            Refresh();
            HandleMouseClicks();
            HandleKeyboard();
        }

        private void HandleMouseClicks()
        {
            if (!Input.GetMouseButtonDown(0))
                return;

            if (IsCollectPending() && HitTest(_collectButtonRt, Input.mousePosition))
            {
                SetFeedback("Collect clicked…");
                TryCollectOrAdvance();
            }
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Delete))
            {
                SetFeedback("Collect key pressed…");
                TryCollectOrAdvance();
            }
        }

        private static bool HitTest(RectTransform rt, Vector2 screenPoint)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);
        }

        private void Refresh()
        {
            if (!_overlayVisible)
                return;

            if (_statusText != null)
            {
                _statusText.text =
                    $"Winner pause — collect: {(IsCollectPending() ? "yes" : "no")}";
            }

            if (_feedbackText != null)
                _feedbackText.text = _feedbackMessage;

            if (_collectButton != null)
                _collectButton.interactable = IsCollectPending() || CanAdvance();
        }

        private void SetFeedback(string message)
        {
            _feedbackMessage = message;
            if (_feedbackText != null)
                _feedbackText.text = message;
        }

        private bool IsCollectPending()
        {
            ResolveUiManager();
            return _uiManager != null && _uiManager.WinnerPotCollectPending;
        }

        private bool CanAdvance()
            => _gameManager != null
                && (_uiManager == null || _uiManager.CanAdvancePastWinnerDismiss());

        private void TryCollectOrAdvance()
        {
            if (IsCollectPending())
            {
                TryCollect();
                return;
            }

            if (CanAdvance())
                TryAdvance();
        }

        private void TryCollect()
        {
            ResolveUiManager();
            if (_uiManager == null)
            {
                SetFeedback("ERROR: UI manager missing.");
                Debug.LogError("[WinnerDismissControls] Collect failed — UIManager not found.", this);
                return;
            }

            if (!_uiManager.TryCollectWinnerPot())
            {
                SetFeedback("Collect blocked — see Player.log.");
                Debug.LogWarning(
                    $"[WinnerDismissControls] Collect failed — pending={_uiManager.WinnerPotCollectPending}.",
                    this);
                return;
            }

            SetFeedback("Collecting pot to winner…");
            Debug.Log("[WinnerDismissControls] Collect started.", this);
        }

        private void TryAdvance()
        {
            if (_gameManager == null || !CanAdvance())
            {
                SetFeedback("Collect pot first.");
                return;
            }

            SetFeedback("Starting next hand…");
            _gameManager.AcknowledgeWinnerDismiss();
        }

        private void ResolveUiManager()
        {
            if (_uiManager != null)
                return;

            _uiManager = UIManager.Instance;

#if UNITY_2023_1_OR_NEWER
            if (_uiManager == null)
                _uiManager = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
#else
            if (_uiManager == null)
                _uiManager = FindObjectOfType<UIManager>(true);
#endif
        }

        private static void ClearKeyboardFocus()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void TearDownOverlay()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("WinnerDismissCanvas"))
                    Destroy(child.gameObject);
            }

            _canvas          = null;
            _statusText      = null;
            _feedbackText    = null;
            _collectButton   = null;
            _collectButtonRt = null;
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("WinnerDismissCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvasGo.transform, false);

            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin        = new Vector2(0.5f, 0f);
            panelRt.anchorMax        = new Vector2(0.5f, 0f);
            panelRt.pivot            = new Vector2(0.5f, 0f);
            panelRt.sizeDelta        = new Vector2(420f, 175f);
            panelRt.anchoredPosition = new Vector2(0f, 24f);

            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color         = new Color(0.05f, 0.08f, 0.12f, 0.92f);
            panelImage.raycastTarget = false;

            _statusText = CreateLabel(
                panelRt, "Status",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(380f, 32f), 18f);

            _feedbackText = CreateLabel(
                panelRt, "Feedback",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -42f), new Vector2(380f, 28f), 16f);
            _feedbackText.color = new Color(0.75f, 0.9f, 1f, 1f);

            _collectButton = CreateButton(
                panelRt, "Collect pot  (Backspace / B)", new Vector2(0f, 36f), TryCollectOrAdvance, out _collectButtonRt);

            canvasGo.SetActive(false);
        }

        private static TMP_Text CreateLabel(
            RectTransform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size,
            float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = size;
            rt.anchoredPosition = anchoredPos;

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font           = TMP_Settings.defaultFontAsset;
            text.alignment      = TextAlignmentOptions.Center;
            text.fontSize       = fontSize;
            text.color          = UiColors.PotGold;
            text.raycastTarget  = false;
            text.text           = string.Empty;
            return text;
        }

        private static Button CreateButton(
            RectTransform parent,
            string label,
            Vector2 anchoredPos,
            UnityEngine.Events.UnityAction onClick,
            out RectTransform buttonRt)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            buttonRt = (RectTransform)go.transform;
            buttonRt.anchorMin        = new Vector2(0.5f, 0f);
            buttonRt.anchorMax        = new Vector2(0.5f, 0f);
            buttonRt.pivot            = new Vector2(0.5f, 0f);
            buttonRt.sizeDelta        = new Vector2(280f, 52f);
            buttonRt.anchoredPosition = anchoredPos;

            var image = go.GetComponent<Image>();
            image.color         = new Color(0.18f, 0.35f, 0.55f, 1f);
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateLabel(buttonRt, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20f);
            var textRt = (RectTransform)text.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            text.text = label;

            return button;
        }
    }
}
