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

        private Sprite    _faceSprite;
        private bool      _isFaceUp;
        private bool      _winnerHighlight;
        private Coroutine _flipCoroutine;

        private static readonly Color WinnerHighlightColor = new Color(1f, 0.95f, 0.55f, 1f);

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
                return;

            _cardBackground.color = _winnerHighlight ? WinnerHighlightColor : Color.white;
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
        }
    }
}
