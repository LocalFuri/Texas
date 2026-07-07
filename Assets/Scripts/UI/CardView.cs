using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    public class CardView : MonoBehaviour
    {
        private static readonly Color EmptySlotColor = new Color(0f, 0f, 0f, 0.30f);

        [SerializeField] private CardSpriteLibrary _spriteLibrary;
        [SerializeField] private Image             _cardBackground;
        [SerializeField] private GameObject        _faceDownOverlay;

        [Header("Flip Animation")]
        [SerializeField] private float flipDuration = 0.35f;

        [Header("Winner Highlight")]
        [Tooltip("Pulse rate in Hz.")]
        [SerializeField, Min(0.1f)] private float _winnerPulseHz = 3.5f;
        [Tooltip("Minimum gold tint at the trough of each pulse (0 = white, 1 = full peak color).")]
        [SerializeField, Range(0f, 1f)] private float _winnerPulseMinBlend = 0.25f;
        [Tooltip("Maximum gold tint at the peak of each pulse.")]
        [SerializeField, Range(0f, 1f)] private float _winnerPulseMaxBlend = 1f;
        [Tooltip("Scale bump at pulse peak as a fraction of base size (0.05 = 5%).")]
        [SerializeField, Range(0f, 0.12f)] private float _winnerPulseScale = 0.05f;
        [SerializeField] private Color _winnerHighlightPeakColor = new Color(1f, 0.82f, 0.22f, 1f);

        private Sprite    _faceSprite;
        private bool      _isFaceUp;
        private bool      _winnerHighlight;
        private Vector3   _winnerPulseBaseScale = Vector3.one;
        private Coroutine _flipCoroutine;
        private Coroutine _winnerPulseCoroutine;

        private void Awake()
        {
            var rankText = transform.Find("RankText");
            if (rankText != null) rankText.gameObject.SetActive(false);

            var suitText = transform.Find("SuitText");
            if (suitText != null) suitText.gameObject.SetActive(false);

            if (GetComponent<RectMask2D>() == null)
                gameObject.AddComponent<RectMask2D>();
        }

        /// <summary>Highlights this card when it is part of the winning five-card hand.</summary>
        public void SetWinnerHighlight(bool highlighted)
        {
            _winnerHighlight = highlighted;
            ApplyFaceColor();
        }
        /// <summary>Displays the card face-up using the sprite from the library.</summary>
        public void Show(Card card)
        {
            StopFlip();
            gameObject.SetActive(true);
            if (_faceDownOverlay != null) _faceDownOverlay.SetActive(false);

            if (_cardBackground == null)
            {
                Debug.LogWarning($"[CardView] _cardBackground is null on '{gameObject.name}'.");
                return;
            }

            _faceSprite = _spriteLibrary != null ? _spriteLibrary.GetSprite(card) : null;
            _isFaceUp    = true;
            ApplyFaceColor();
            _cardBackground.sprite = _faceSprite;
            ResetScale();
        }

        /// <summary>Displays the real card back sprite from the library (face-down).</summary>
        public void ShowFaceDown()
        {
            StopWinnerPulse();
            StopFlip();
            gameObject.SetActive(true);
            if (_faceDownOverlay != null) _faceDownOverlay.SetActive(false);

            if (_cardBackground != null && _spriteLibrary != null)
            {
                _isFaceUp = false;
                _cardBackground.color  = Color.white;
                _cardBackground.sprite = _spriteLibrary.CardBack;
            }

            ResetScale();
        }

        /// <summary>Flips from face-down to the given card face (BlackJack-style scale animation).</summary>
        public void FlipToFace(Card card, Action onComplete = null)
        {
            if (_cardBackground == null)
            {
                Show(card);
                onComplete?.Invoke();
                return;
            }

            _faceSprite = _spriteLibrary != null ? _spriteLibrary.GetSprite(card) : null;
            gameObject.SetActive(true);
            if (_faceDownOverlay != null) _faceDownOverlay.SetActive(false);

            if (!_isFaceUp && _spriteLibrary?.CardBack != null)
                _cardBackground.sprite = _spriteLibrary.CardBack;

            StopFlip();
            _flipCoroutine = StartCoroutine(FlipRoutine(true, onComplete));
        }

        /// <summary>Shows the slot as a dark empty placeholder — visible but no card assigned.</summary>
        public void ShowEmpty()
        {
            StopWinnerPulse();
            StopFlip();
            gameObject.SetActive(true);
            if (_faceDownOverlay != null) _faceDownOverlay.SetActive(false);

            if (_cardBackground != null)
            {
                _isFaceUp = false;
                _cardBackground.sprite = null;
                _cardBackground.color  = EmptySlotColor;
            }

            ResetScale();
        }

        /// <summary>Hides the card slot entirely (use for hole cards, not community slots).</summary>
        public void Hide()
        {
            StopWinnerPulse();
            StopFlip();
            gameObject.SetActive(false);
        }

        public void SetFlipDuration(float duration) => flipDuration = duration;

        /// <summary>Stops an in-flight flip and restores scale (e.g. when UI refresh is cancelled).</summary>
        public void CancelFlip()
        {
            StopFlip();
        }

        private IEnumerator FlipRoutine(bool toFaceUp, Action onComplete)
        {
            var rt        = (RectTransform)transform;
            Vector3 orig  = rt.localScale;
            float   half  = flipDuration * 0.5f;
            float   elapsed;

            for (elapsed = 0f; elapsed < half; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / half);
                rt.localScale = new Vector3(orig.x * (1f - t), orig.y, orig.z);
                yield return null;
            }

            rt.localScale = new Vector3(0f, orig.y, orig.z);
            _isFaceUp = toFaceUp;
            _cardBackground.color  = Color.white;
            _cardBackground.sprite = toFaceUp ? _faceSprite : _spriteLibrary?.CardBack;

            for (elapsed = 0f; elapsed < half; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / half);
                rt.localScale = new Vector3(orig.x * t, orig.y, orig.z);
                yield return null;
            }

            rt.localScale = orig;
            _flipCoroutine  = null;
            ApplyFaceColor();
            onComplete?.Invoke();
        }

        private void ApplyFaceColor()
        {
            if (_cardBackground == null || !_isFaceUp)
            {
                StopWinnerPulse();
                return;
            }

            if (_winnerHighlight)
                StartWinnerPulse();
            else
                StopWinnerPulse();
        }

        private void StartWinnerPulse()
        {
            if (_winnerPulseCoroutine != null)
                return;

            if (!isActiveAndEnabled)
                return;

            CacheWinnerPulseBaseScale();
            _winnerPulseCoroutine = StartCoroutine(RunWinnerPulse());
        }

        private void CacheWinnerPulseBaseScale()
        {
            if (transform is not RectTransform rt)
            {
                _winnerPulseBaseScale = Vector3.one;
                return;
            }

            _winnerPulseBaseScale = rt.localScale;
            if (_winnerPulseBaseScale.x == 0f)
                _winnerPulseBaseScale = Vector3.one;
        }

        private void StopWinnerPulse()
        {
            if (_winnerPulseCoroutine != null)
            {
                StopCoroutine(_winnerPulseCoroutine);
                _winnerPulseCoroutine = null;
            }

            if (transform is RectTransform rt)
                rt.localScale = _winnerPulseBaseScale;

            if (_cardBackground != null && _isFaceUp && !_winnerHighlight)
                _cardBackground.color = Color.white;
        }

        private IEnumerator RunWinnerPulse()
        {
            float angularSpeed = Mathf.PI * 2f * _winnerPulseHz;
            var rt = transform as RectTransform;

            while (_winnerHighlight && _isFaceUp && _cardBackground != null)
            {
                float wave   = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * angularSpeed);
                float shaped = wave * wave;
                float blend  = Mathf.Lerp(_winnerPulseMinBlend, _winnerPulseMaxBlend, shaped);
                _cardBackground.color = Color.Lerp(Color.white, _winnerHighlightPeakColor, blend);

                if (rt != null && _winnerPulseScale > 0f)
                {
                    float scaleMul = 1f + _winnerPulseScale * shaped;
                    rt.localScale = new Vector3(
                        _winnerPulseBaseScale.x * scaleMul,
                        _winnerPulseBaseScale.y * scaleMul,
                        _winnerPulseBaseScale.z);
                }

                yield return null;
            }

            _winnerPulseCoroutine = null;

            if (rt != null)
                rt.localScale = _winnerPulseBaseScale;

            if (_cardBackground != null && _isFaceUp)
                _cardBackground.color = _winnerHighlight ? _winnerHighlightPeakColor : Color.white;
        }

        private void StopFlip()
        {
            if (_flipCoroutine == null) return;
            StopCoroutine(_flipCoroutine);
            _flipCoroutine = null;
            ResetScale();
        }

        private void ResetScale()
        {
            var rt = transform as RectTransform;
            if (rt == null) return;

            Vector3 scale = rt.localScale;
            if (scale.x == 0f)
                rt.localScale = Vector3.one;

            if (_winnerPulseCoroutine == null)
                _winnerPulseBaseScale = rt.localScale;
        }
    }
}
