using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Debug options panel toggled with F1, F2, F5, O, or right mouse button.
    /// Pauses Texas Hold'em gameplay (time scale + game-loop waits) while open.
    /// </summary>
    public class OptionsMenu : MonoBehaviour
    {
        public const int OptionCount = 6;

        public const int BotThinkSliderRowIndex = 1;
        public const float BotThinkMinSeconds   = 0f;
        public const float BotThinkMaxSeconds   = 30f;

        /// <summary>Shared label size for all options-menu rows (Restart, toggles, bot slider).</summary>
        public const float MenuRowFontSize = 20f;

        /// <summary>Wide enough for a long bot-think slider track (reference layout).</summary>
        public const float PanelWidth = 380f;
        private const float RowHeight    = 20f;
        private const float CheckboxSize = 15f;
        private const float PanelPadding = 10f;
        private const float NearPanelPaddingPx = 24f;

        // Reference panel ~#052c05
        private static readonly Color PanelBg    = new Color(0.020f, 0.173f, 0.020f, 1f);
        // Reference label ~#e6d64e
        private static readonly Color LabelColor = new Color(0.902f, 0.839f, 0.306f, 1f);

        // ── Singleton ─────────────────────────────────────────────────────
        public static OptionsMenu Instance { get; private set; }

        // ── Option State (readable by other systems) ───────────────────────
        // Index 0 is Restart (action only). Index 1 is bot-think slider (not a toggle).
        public bool ShowBotCards       => _toggleStates[2];
        public bool TestMode           => _toggleStates[3];
        public bool GodMode            => _toggleStates[4];
        public bool AutoAdvance        => _toggleStates[5];
        public bool IsOpen             => _isOpen;

        public event Action OnOptionsChanged;

        // ── Serialized references (wired by OptionsMenuBuilder) ───────────
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Toggle[]    _toggles = new Toggle[OptionCount];
        [SerializeField] private Slider      _botThinkSlider;

        [Header("Layout")]
        [Tooltip("AnchoredPosition of the panel (canvas center anchor).")]
        [SerializeField] private float _panelPosX;
        [SerializeField] private float _panelPosY = 157f;
        [Tooltip("Vertical gap between option rows (VerticalLayoutGroup spacing).")]
        [SerializeField] private float _rowSpacing = 3f;

        private Vector2 PanelAnchoredPosition => new Vector2(_panelPosX, _panelPosY);

        private float ComputePanelHeight()
            => PanelPadding * 2f
               + OptionCount * RowHeight
               + (OptionCount - 1) * _rowSpacing;

        [Header("Audio")]
        [SerializeField] private AudioClip _menuToggleClip;
        [SerializeField] [Range(0f, 1f)] private float _menuToggleVolume = 0.35f;

        // ── Private ───────────────────────────────────────────────────────
        private readonly bool[] _toggleStates = new bool[OptionCount];
        private bool            _isOpen;
        private float           _timeScaleBeforePause = 1f;
        private AudioSource     _audioSource;
        private Coroutine       _quitCoroutine;
        private TMP_Text        _botThinkHandleLabel;
        private bool            _botThinkSliderBound;

        // ── Unity Callbacks ───────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveCanvasGroup();
            ResolveAudioSource();

            for (int i = 0; i < _toggles.Length; i++)
            {
                int idx = i;
                if (_toggles[idx] != null)
                    _toggles[idx].onValueChanged.AddListener(v => OnToggleChanged(idx, v));
            }

            SetVisible(false);
            BindBotThinkSlider();
        }

        private void Start()
        {
            OptionsMenuToggleStyle.ResetRuntimeSprites();
            EnsureBotThinkSliderRow();
            ApplyCompactLayout();
            ApplyToggleStyles();
            ApplySliderStyles();
            SyncBotThinkSliderFromGame();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                EnsureBotThinkSliderRow();
            ApplyCompactLayout();
            ApplySliderStyles();
        }
#endif

        private void OnDestroy()
        {
            if (_isOpen)
                Time.timeScale = _timeScaleBeforePause;

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (WasToggleKeyPressed())
            {
                SetVisible(!_isOpen);
                return;
            }

            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_quitCoroutine == null)
                    _quitCoroutine = StartCoroutine(QuitAfterEscapeSound());
                return;
            }

            if (!Input.GetMouseButtonDown(1))
                return;

            if (_isOpen)
            {
                SetVisible(false);
                return;
            }

            if (IsCursorNearPanel())
                SetVisible(true);
        }

        // ── Visibility ────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_isOpen == visible)
                return;

            _isOpen = visible;

            if (visible)
            {
                _timeScaleBeforePause = Time.timeScale;
                Time.timeScale         = 0f;
            }
            else
            {
                Time.timeScale = _timeScaleBeforePause;
            }

            ResolveCanvasGroup();

            gameObject.SetActive(true);

            if (visible)
                transform.SetAsLastSibling();

            _canvasGroup.alpha          = visible ? 1f : 0f;
            _canvasGroup.interactable   = visible;
            _canvasGroup.blocksRaycasts = visible;

            PlayMenuToggleSound();
        }

        private void ResolveAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake      = false;
            _audioSource.ignoreListenerPause = true;
        }

        private void PlayMenuToggleSound()
        {
            if (_menuToggleClip == null || _audioSource == null)
                return;

            _audioSource.PlayOneShot(_menuToggleClip, _menuToggleVolume);
        }

        private void ResolveCanvasGroup()
        {
            if (_canvasGroup != null)
                return;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private static bool WasToggleKeyPressed()
        {
            // O is a reliable backup when the Unity Editor captures F1 for Help.
            return Input.GetKeyDown(KeyCode.F1)
                || Input.GetKeyDown(KeyCode.F2)
                || Input.GetKeyDown(KeyCode.F5)
                || Input.GetKeyDown(KeyCode.O);
        }

        private IEnumerator QuitAfterEscapeSound()
        {
            PlayMenuToggleSound();

            float delay = _menuToggleClip != null ? _menuToggleClip.length : 0f;
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            _quitCoroutine = null;
            QuitApplication();
        }

        private void QuitApplication()
        {
            if (_isOpen)
                Time.timeScale = _timeScaleBeforePause;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool IsCursorNearPanel()
        {
            var rt = transform as RectTransform;
            if (rt == null)
                return false;

            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            rect.xMin -= NearPanelPaddingPx;
            rect.yMin -= NearPanelPaddingPx;
            rect.xMax += NearPanelPaddingPx;
            rect.yMax += NearPanelPaddingPx;

            return rect.Contains(Input.mousePosition);
        }

        // ── Toggle Callbacks ──────────────────────────────────────────────

        private void OnToggleChanged(int index, bool value)
        {
            if (index < 0 || index >= _toggleStates.Length)
                return;

            if (index == 0)
            {
                if (value)
                    PerformRestart();

                _toggleStates[0] = false;
                if (_toggles[0] != null)
                    _toggles[0].SetIsOnWithoutNotify(false);
                return;
            }

            _toggleStates[index] = value;

            if (index == 4 && value)
                ApplyGodModeChips();

            OnOptionsChanged?.Invoke();
        }

        private void PerformRestart()
        {
            if (_isOpen)
                SetVisible(false);

            UIManager ui = FindObjectOfType<UIManager>();
            GameManager gm = FindObjectOfType<GameManager>();
            ui?.ResetToStartScreen();
            gm?.ResetToStartScreen();
        }

        private static void ApplyGodModeChips()
        {
            var gm = FindObjectOfType<GameManager>();
            if (gm?.Players == null) return;

            foreach (PlayerState player in gm.Players)
            {
                if (player.Type != PlayerType.Human) continue;
                player.Chips = Mathf.Max(player.Chips, 100_000);
            }

            gm.OnPlayersUpdated?.Invoke(gm.Players);
        }

        private void BindBotThinkSlider()
        {
            if (_botThinkSlider == null || _botThinkSliderBound)
                return;

            _botThinkSlider.onValueChanged.AddListener(OnBotThinkSliderChanged);
            _botThinkSliderBound = true;
        }

        private void EnsureBotThinkSliderRow()
        {
            string expectedRowName = "Row_" + OptionsMenuSliderStyle.BotThinkLabelText;

            var botThinkRows = new System.Collections.Generic.List<Transform>();
            Transform legacyNonSliderRow = null;

            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Row_"))
                    continue;

                if (child.Find("BotThinkSlider") != null)
                    botThinkRows.Add(child);
                else if (IsLegacyBotThinkRowName(child.name) && legacyNonSliderRow == null)
                    legacyNonSliderRow = child;
            }

            float initial = BotThinkDefaultSeconds;
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
                initial = gm.AiActionDelay;
            if (_botThinkSlider != null)
                initial = _botThinkSlider.value;

            Transform keepRow = null;
            if (_botThinkSlider != null)
            {
                Transform parent = _botThinkSlider.transform.parent;
                if (parent != null && parent.Find("BotThinkSlider") == _botThinkSlider.transform)
                    keepRow = parent;
            }

            if (keepRow == null && botThinkRows.Count > 0)
                keepRow = botThinkRows[0];

            foreach (Transform row in botThinkRows)
            {
                if (row != keepRow)
                    DestroyObject(row.gameObject);
            }

            if (legacyNonSliderRow != null)
                DestroyObject(legacyNonSliderRow.gameObject);

            if (keepRow == null)
            {
                int insertAt = Mathf.Min(BotThinkSliderRowIndex, transform.childCount);
                _botThinkSlider = OptionsMenuRowFactory.CreateBotThinkSliderRow(transform, initial);
                keepRow = _botThinkSlider.transform.parent;
                keepRow.SetSiblingIndex(insertAt);
            }
            else
            {
                _botThinkSlider = keepRow.GetComponentInChildren<Slider>(true);
                if (keepRow.name != expectedRowName)
                {
                    int insertIndex = keepRow.GetSiblingIndex();
                    float value     = _botThinkSlider != null ? _botThinkSlider.value : initial;
                    DestroyObject(keepRow.gameObject);
                    _botThinkSlider      = null;
                    _botThinkHandleLabel = null;
                    _botThinkSliderBound = false;
                    _botThinkSlider = OptionsMenuRowFactory.CreateBotThinkSliderRow(transform, value);
                    keepRow = _botThinkSlider.transform.parent;
                    keepRow.SetSiblingIndex(insertIndex);
                }
            }

            keepRow.SetSiblingIndex(BotThinkSliderRowIndex);

            _botThinkSlider.minValue = BotThinkMinSeconds;
            _botThinkSlider.maxValue = BotThinkMaxSeconds;

            if (_toggles != null && BotThinkSliderRowIndex < _toggles.Length)
                _toggles[BotThinkSliderRowIndex] = null;

            _botThinkHandleLabel = null;
            _botThinkSliderBound = false;
            BindBotThinkSlider();

            ResolveBotThinkHandleLabel();
            OptionsMenuSliderStyle.Apply(_botThinkSlider, _botThinkHandleLabel);
            UpdateBotThinkHandleLabel(_botThinkSlider.value);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static bool IsLegacyBotThinkRowName(string rowName)
        {
            return rowName == "Row_Autoplay at max speed"
                || rowName == "Row_Bot think time"
                || rowName == "Row_Bot think";
        }

        private void DestroyObject(UnityEngine.Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(obj);
                return;
            }
#endif
            Destroy(obj);
        }

        private void SyncBotThinkSliderFromGame()
        {
            if (_botThinkSlider == null)
                return;

            ResolveBotThinkHandleLabel();

            float delay = BotThinkDefaultSeconds;
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
                delay = gm.AiActionDelay;

            delay = Mathf.Clamp(delay, BotThinkMinSeconds, BotThinkMaxSeconds);
            _botThinkSlider.SetValueWithoutNotify(delay);
            UpdateBotThinkHandleLabel(delay);
        }

        private const float BotThinkDefaultSeconds = 1f;

        private void OnBotThinkSliderChanged(float value)
        {
            float delay = Mathf.Clamp(value, BotThinkMinSeconds, BotThinkMaxSeconds);
            UpdateBotThinkHandleLabel(delay);

            GameManager gm = FindObjectOfType<GameManager>();
            gm?.SetAiActionDelay(delay);
        }

        private void ResolveBotThinkHandleLabel()
        {
            if (_botThinkHandleLabel != null || _botThinkSlider == null)
                return;

            Transform handle = _botThinkSlider.transform.Find("Handle Slide Area/Handle/HandleLabel");
            if (handle != null)
                _botThinkHandleLabel = handle.GetComponent<TMP_Text>();
        }

        private void UpdateBotThinkHandleLabel(float seconds)
        {
            ResolveBotThinkHandleLabel();
            if (_botThinkHandleLabel != null)
                _botThinkHandleLabel.text = OptionsMenuSliderStyle.FormatHandleValue(seconds);
        }

        private void ApplySliderStyles()
        {
            if (_botThinkSlider == null)
                return;

            ResolveBotThinkHandleLabel();
            OptionsMenuSliderStyle.Apply(_botThinkSlider, _botThinkHandleLabel);
        }

        private static void ApplyBotThinkIcon(Image icon)
        {
            if (icon == null)
                return;

            icon.sprite         = OptionsMenuToggleStyle.GetCheckboxSprite();
            icon.color          = Color.white;
            icon.raycastTarget  = false;
        }
        private TMP_Text FindMenuFontReference()
        {
            if (_toggles == null)
                return null;

            foreach (Toggle toggle in _toggles)
            {
                if (toggle == null)
                    continue;

                Transform label = toggle.transform.parent?.Find("Label");
                if (label == null)
                    continue;

                var tmp = label.GetComponent<TMP_Text>();
                if (tmp != null && tmp.font != null)
                    return tmp;
            }

            return null;
        }

        private void ApplyToggleStyles()
        {
            if (_toggles == null)
                return;

            foreach (Toggle toggle in _toggles)
            {
                if (toggle != null)
                    OptionsMenuToggleStyle.Apply(toggle);
            }
        }

        /// <summary>Repairs legacy scene layout to match the compact debug checklist reference.</summary>
        private void ApplyCompactLayout()
        {
            transform.SetAsLastSibling();

            var rt = transform as RectTransform;
            if (rt != null)
            {
                float panelHeight = ComputePanelHeight();

                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = PanelAnchoredPosition;
                rt.sizeDelta        = new Vector2(PanelWidth, panelHeight);
            }

            var bg = GetComponent<Image>();
            if (bg != null)
            {
                bg.color         = PanelBg;
                bg.raycastTarget = true;
                OptionsMenuToggleStyle.EnsurePanelBackgroundSprite(bg);
            }

            var vl = GetComponent<VerticalLayoutGroup>();
            if (vl != null)
            {
                int pad = (int)PanelPadding;
                vl.padding                = new RectOffset(pad, pad, pad, pad);
                vl.spacing                = _rowSpacing;
                vl.childAlignment         = TextAnchor.UpperLeft;
                vl.childControlWidth      = true;
                vl.childControlHeight     = true;
                vl.childForceExpandWidth  = false;
                vl.childForceExpandHeight = false;
            }

            foreach (Transform child in transform)
            {
                if (child.name == "Title" || child.name == "Divider")
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                if (!child.name.StartsWith("Row_"))
                    continue;

                var rowLe = child.GetComponent<LayoutElement>();
                if (rowLe == null)
                    rowLe = child.gameObject.AddComponent<LayoutElement>();
                rowLe.preferredHeight = RowHeight;
                rowLe.minHeight       = RowHeight;

                var hl = child.GetComponent<HorizontalLayoutGroup>();
                if (hl != null)
                {
                    hl.spacing                = 8f;
                    hl.childAlignment         = TextAnchor.MiddleLeft;
                    hl.childControlWidth      = true;
                    hl.childControlHeight     = true;
                    hl.childForceExpandWidth  = false;
                    hl.childForceExpandHeight = false;
                }

                Transform sliderT = child.Find("BotThinkSlider");
                if (sliderT != null)
                {
                    rowLe.preferredHeight = OptionsMenuRowFactory.RowHeight;
                    rowLe.minHeight       = OptionsMenuRowFactory.RowHeight;

                    if (hl != null)
                        hl.spacing = OptionsMenuSliderStyle.LabelSliderGap;

                    Transform icon = child.Find("Icon");
                    if (icon != null)
                        ApplyBotThinkIcon(icon.GetComponent<Image>());

                    Transform sliderLabel = child.Find("Label");
                    if (sliderLabel != null)
                    {
                        var tmp = sliderLabel.GetComponent<TMP_Text>();
                        if (tmp != null)
                            OptionsMenuSliderStyle.ApplyRowLabelStyle(tmp, FindMenuFontReference());

                        var labelLe = sliderLabel.GetComponent<LayoutElement>();
                        if (labelLe == null)
                            labelLe = sliderLabel.gameObject.AddComponent<LayoutElement>();
                        labelLe.flexibleWidth     = 0f;
                        labelLe.minWidth          = OptionsMenuSliderStyle.LabelWidth;
                        labelLe.preferredWidth    = OptionsMenuSliderStyle.LabelWidth;
                        labelLe.minHeight         = OptionsMenuRowFactory.RowHeight;
                        labelLe.preferredHeight   = OptionsMenuRowFactory.RowHeight;
                    }

                    var slider = sliderT.GetComponent<Slider>();
                    if (slider != null)
                        OptionsMenuSliderStyle.Apply(slider, sliderT.GetComponentInChildren<TMP_Text>(true));

                    var sliderLe = sliderT.GetComponent<LayoutElement>();
                    if (sliderLe == null)
                        sliderLe = sliderT.gameObject.AddComponent<LayoutElement>();
                    sliderLe.flexibleWidth   = 1f;
                    sliderLe.minWidth        = OptionsMenuSliderStyle.SliderMinWidth;
                    sliderLe.minHeight       = OptionsMenuSliderStyle.HandleHeight;
                    sliderLe.preferredHeight = OptionsMenuSliderStyle.HandleHeight;

                    continue;
                }

                Transform toggle = child.Find("Toggle");
                if (toggle != null)
                {
                    var toggleLe = toggle.GetComponent<LayoutElement>();
                    if (toggleLe == null)
                        toggleLe = toggle.gameObject.AddComponent<LayoutElement>();
                    toggleLe.minWidth = toggleLe.preferredWidth = CheckboxSize;
                    toggleLe.minHeight = toggleLe.preferredHeight = CheckboxSize;

                    if (toggle is RectTransform toggleRt)
                        toggleRt.sizeDelta = new Vector2(CheckboxSize, CheckboxSize);
                }

                Transform label = child.Find("Label");
                if (label != null)
                {
                    var tmp = label.GetComponent<TMP_Text>();
                    if (tmp != null)
                    {
                        tmp.color              = LabelColor;
                        tmp.fontSize           = MenuRowFontSize;
                        tmp.fontStyle          = FontStyles.Normal;
                        tmp.enableWordWrapping = false;
                    }

                    var labelLe = label.GetComponent<LayoutElement>();
                    if (labelLe == null)
                        labelLe = label.gameObject.AddComponent<LayoutElement>();
                    labelLe.flexibleWidth     = 1f;
                    labelLe.minHeight         = RowHeight;
                    labelLe.preferredHeight   = RowHeight;
                }
            }

            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }
}
