using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Tappable Check / Fold / Raise / All-In menu on the human seat, positioned above the player name.
    /// </summary>
    public class SeatActionMenu : MonoBehaviour
    {
        private const string HudFontResourcesPath = "Fonts & Materials/LiberationSans SDF";

        private static readonly Color MenuBgColor = new Color(0.08f, 0.08f, 0.10f, 1f);

        private static readonly Color FoldBorderColor  = new Color(1.00f, 0.15f, 0.15f, 1f);
        private static readonly Color CheckBorderColor = new Color(0.10f, 0.88f, 0.30f, 1f);
        private static readonly Color AllInBorderColor = new Color(0.85f, 0.10f, 0.85f, 1f);

        private static readonly ColorBlock ButtonColors = new ColorBlock
        {
            normalColor      = Color.white,
            highlightedColor = new Color(1.0f, 1.0f, 1.0f, 1.15f),
            pressedColor     = new Color(0.65f, 0.65f, 0.65f, 1.0f),
            disabledColor    = new Color(0.35f, 0.35f, 0.35f, 0.55f),
            selectedColor    = Color.white,
            colorMultiplier  = 1f,
            fadeDuration     = 0.06f,
        };

        [SerializeField] private Image         _background;
        [SerializeField] private Button        _foldButton;
        [SerializeField] private Button        _checkCallButton;
        [SerializeField] private Button        _raiseButton;
        [SerializeField] private Button        _allInButton;
        [SerializeField] private TMP_Text      _foldLabel;
        [SerializeField] private TMP_Text      _checkCallLabel;
        [SerializeField] private TMP_Text      _raiseLabel;
        [SerializeField] private TMP_Text      _allInLabel;
        [SerializeField] private TMP_InputField _raiseInput;
        [SerializeField] private GameObject    _raiseInputRow;

        public event Action FoldClicked;
        public event Action CheckCallClicked;
        public event Action RaiseClicked;
        public event Action AllInClicked;

        private bool            _listenersBound;
        private RectTransform   _actionsColumn;
        private RectTransform   _menuRect;

        private void Awake()
        {
            ResolveReferences();
            ApplyDefaultLabelStyle();
            EnsureButtonGraphics();
            EnsureListeners();
        }

        private void EnsureListeners()
        {
            if (_listenersBound) return;
            _listenersBound = true;

            _foldButton?.onClick.AddListener(() => FoldClicked?.Invoke());
            _checkCallButton?.onClick.AddListener(() => CheckCallClicked?.Invoke());
            _raiseButton?.onClick.AddListener(() => RaiseClicked?.Invoke());
            _allInButton?.onClick.AddListener(() => AllInClicked?.Invoke());
        }

        private void ResolveReferences()
        {
            _menuRect ??= (RectTransform)transform;

            Transform actions = transform.Find("ActionsColumn");
            if (actions != null)
                _actionsColumn = (RectTransform)actions;

            if (_foldButton == null)
                _foldButton = FindButton(actions, "FoldButton");
            if (_checkCallButton == null)
                _checkCallButton = FindButton(actions, "CheckCallButton");
            if (_raiseButton == null)
                _raiseButton = FindButton(actions, "RaiseButton");
            if (_allInButton == null)
                _allInButton = FindButton(actions, "AllInButton");
            if (_background == null)
                _background = transform.Find("Background")?.GetComponent<Image>();
            if (_raiseInputRow == null)
            {
                Transform row = transform.Find("ActionsColumn/RaiseInputRow")
                           ?? transform.Find("RaiseInputRow");
                if (row != null) _raiseInputRow = row.gameObject;
            }
            if (_raiseInput == null)
                _raiseInput = transform.Find("ActionsColumn/RaiseInputRow/RaiseInput")?.GetComponent<TMP_InputField>()
                           ?? transform.Find("RaiseInputRow/RaiseInput")?.GetComponent<TMP_InputField>()
                           ?? GetComponentInChildren<TMP_InputField>(true);

            _foldLabel      ??= _foldButton?.GetComponentInChildren<TMP_Text>(true);
            _checkCallLabel ??= _checkCallButton?.GetComponentInChildren<TMP_Text>(true);
            _raiseLabel     ??= _raiseButton?.GetComponentInChildren<TMP_Text>(true);
            _allInLabel     ??= _allInButton?.GetComponentInChildren<TMP_Text>(true);
        }

        private void ApplyDefaultLabelStyle()
        {
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(HudFontResourcesPath);
            StyleLabel(_foldLabel,      new Color(1f, 0f, 0f, 1f),  font, 15f);
            StyleLabel(_checkCallLabel, new Color(0f, 1f, 0f, 1f),  font, 15f);
            StyleLabel(_raiseLabel,     ButtonLabelStyle.RaiseText,  font, 15f);
            StyleLabel(_allInLabel,     new Color(1f, 0f, 1f, 1f),  font, 15f);
        }

        private static void StyleLabel(TMP_Text label, Color color, TMP_FontAsset font, float size)
        {
            if (label == null) return;
            if (font != null) label.font = font;
            ButtonLabelStyle.Apply(label, color, size);
            label.raycastTarget = false;
        }

        private Button FindButton(Transform actionsColumn, string name)
        {
            if (actionsColumn != null)
            {
                Button nested = actionsColumn.Find(name)?.GetComponent<Button>();
                if (nested != null) return nested;
            }

            return transform.Find(name)?.GetComponent<Button>();
        }

        /// <summary>Shows only the actions valid for the current human decision.</summary>
        public void Configure(bool showFold, bool showCheckCall, bool showRaise, bool showAllIn,
                              bool checkCallInteractable, bool raiseInteractable, bool allInInteractable)
        {
            ResolveReferences();
            EnsureListeners();

            bool visible = showFold || showCheckCall || showRaise || showAllIn;
            gameObject.SetActive(visible);
            if (!visible) return;

            if (_background != null)
                _background.enabled = false;

            transform.SetAsLastSibling();

            SetButtonVisible(_foldButton, showFold);
            SetButtonVisible(_checkCallButton, showCheckCall);
            SetButtonVisible(_raiseButton, showRaise);
            SetButtonVisible(_allInButton, showAllIn);
            SetRaiseInputVisible(showRaise);

            if (_foldButton != null)
                _foldButton.interactable = showFold;

            if (_checkCallButton != null)
                _checkCallButton.interactable = checkCallInteractable;

            if (_raiseButton != null)
                _raiseButton.interactable = raiseInteractable;

            if (_allInButton != null)
                _allInButton.interactable = allInInteractable;

            if (_raiseInput != null)
                _raiseInput.interactable = raiseInteractable;

            EnsureLayoutRows();
            RebuildLayout();
        }

        public void SetCheckCallLabel(string text)
        {
            if (_checkCallLabel != null)
                _checkCallLabel.text = text;
        }

        public void SetRaiseInputText(string text)
        {
            if (_raiseInput != null)
                _raiseInput.text = text;
        }

        public string GetRaiseInputText() => _raiseInput != null ? _raiseInput.text : string.Empty;

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RebuildLayout()
        {
            if (_actionsColumn != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_actionsColumn);
            if (_menuRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_menuRect);
        }

        private void EnsureLayoutRows()
        {
            const float rowH   = 24f;
            const float inputH = 22f;
            ApplyLayoutRow(_foldButton?.transform, rowH);
            ApplyLayoutRow(_checkCallButton?.transform, rowH);
            ApplyLayoutRow(_raiseButton?.transform, rowH);
            ApplyLayoutRow(_allInButton?.transform, rowH);
            if (_raiseInputRow != null)
                ApplyLayoutRow(_raiseInputRow.transform, inputH);
        }

        private void EnsureButtonGraphics()
        {
            SetupButtonGraphic(_foldButton);
            SetupButtonGraphic(_checkCallButton);
            SetupButtonGraphic(_raiseButton);
            SetupButtonGraphic(_allInButton);
        }

        /// <summary>Wires sprite-swap graphics on seat menu buttons (menu stays hidden during play).</summary>
        private static void SetupButtonGraphic(Button button)
        {
            if (button == null) return;

            ActionBadgeUtility.RestoreSpriteButton(button);
            button.colors = ButtonColors;

            if (button.GetComponent<ButtonHoverFix>() == null)
                button.gameObject.AddComponent<ButtonHoverFix>();
        }

        private static void ApplyLayoutRow(Transform row, float height)
        {
            if (row == null) return;
            var rt = (RectTransform)row;
            rt.anchorMin        = new Vector2(0f, 0.5f);
            rt.anchorMax        = new Vector2(1f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private void SetRaiseInputVisible(bool visible)
        {
            if (_raiseInputRow != null)
                _raiseInputRow.SetActive(visible);
            else if (_raiseInput != null)
                _raiseInput.gameObject.SetActive(visible);
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
                button.gameObject.SetActive(visible);
        }
    }
}
