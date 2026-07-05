using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TexasHoldem
{
    /// <summary>
    /// Raise amount entry: auto select-all when idle, first key replaces, further keys append (8 then 0 → 80).
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class RaiseInputSelectAllOnClick : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private const float IdleSelectAllDelay = 0.3f;

        private TMP_InputField _input;
        private Coroutine      _selectCoroutine;
        private string         _textBeforeChange;
        private bool           _inMultiDigitBurst;
        private bool           _suppressValueChange;
        private bool           _listenersBound;
        private bool           _idleSelectApplied;
        private float          _lastEditTime;

        private void Awake()
        {
            _input = GetComponent<TMP_InputField>();
        }

        private void OnEnable()
        {
            if (_input == null)
                return;

            _input.onFocusSelectAll = true;
            BindListeners();
            ResetEntryState(_input.text);
        }

        private void Update()
        {
            if (_input == null || !_input.isFocused || !_input.interactable)
                return;

            if (Time.unscaledTime - _lastEditTime < IdleSelectAllDelay)
                return;

            if (_idleSelectApplied && HasFullSelection())
                return;

            RaiseInputBuilder.SelectAllText(_input);
            _inMultiDigitBurst  = false;
            _idleSelectApplied  = true;
            _textBeforeChange   = _input.text ?? string.Empty;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_input == null || !_input.interactable)
                return;

            ScheduleSelectAll();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_input == null || !_input.interactable)
                return;

            ScheduleSelectAll();
        }

        public void ResetEntryState(string text = null)
        {
            _inMultiDigitBurst = false;
            _idleSelectApplied = false;
            _textBeforeChange  = text ?? _input?.text ?? string.Empty;
            _lastEditTime      = Time.unscaledTime;
        }

        private void BindListeners()
        {
            if (_listenersBound || _input == null)
                return;

            _listenersBound = true;
            _input.onSelect.AddListener(OnInputSelected);
            _input.onValueChanged.AddListener(OnInputValueChanged);
        }

        private void OnInputSelected(string _)
        {
            _inMultiDigitBurst = false;
            _idleSelectApplied = false;
            ScheduleSelectAll();
        }

        private void OnInputValueChanged(string newValue)
        {
            newValue ??= string.Empty;

            if (_suppressValueChange)
            {
                _textBeforeChange = newValue;
                return;
            }

            MarkEdited();

            string oldValue = _textBeforeChange ?? string.Empty;
            bool isAppend     = newValue.Length == oldValue.Length + 1 && newValue.StartsWith(oldValue);

            if (!_inMultiDigitBurst &&
                isAppend &&
                char.IsDigit(newValue[^1]))
            {
                ApplyReplacementDigit(newValue[^1]);
                return;
            }

            _inMultiDigitBurst  = true;
            _textBeforeChange = newValue;
        }

        private void MarkEdited()
        {
            _lastEditTime      = Time.unscaledTime;
            _idleSelectApplied = false;
        }

        private void ApplyReplacementDigit(char digit)
        {
            _suppressValueChange = true;
            string replacement   = digit.ToString();
            _input.SetTextWithoutNotify(replacement);
            _input.caretPosition  = replacement.Length;
            _input.stringPosition = replacement.Length;
            _inMultiDigitBurst    = true;
            _textBeforeChange     = replacement;
            _suppressValueChange  = false;
            MarkEdited();
        }

        private void ScheduleSelectAll()
        {
            if (_selectCoroutine != null)
                StopCoroutine(_selectCoroutine);

            _selectCoroutine = StartCoroutine(SelectAllAfterInteraction());
        }

        private IEnumerator SelectAllAfterInteraction()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (_input == null || !_input.isFocused)
            {
                _selectCoroutine = null;
                yield break;
            }

            RaiseInputBuilder.SelectAllText(_input);
            _inMultiDigitBurst = false;
            _idleSelectApplied = true;
            _textBeforeChange  = _input.text ?? string.Empty;

            yield return null;

            if (_input != null && _input.isFocused && !HasFullSelection())
            {
                _input.DeactivateInputField();
                _input.ActivateInputField();
                RaiseInputBuilder.SelectAllText(_input);
            }

            _selectCoroutine = null;
        }

        private bool HasFullSelection()
        {
            if (_input == null)
                return false;

            int length = _input.text != null ? _input.text.Length : 0;
            return length > 0
                   && _input.selectionAnchorPosition == 0
                   && _input.selectionFocusPosition == length;
        }
    }
}
