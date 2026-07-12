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
        [Tooltip("Used only when no UIManager is present. Otherwise tune Winner Card Highlight on UIManager.")]
        [SerializeField, Min(0.1f)] private float _winnerPulseHz = 1f;
        [Tooltip("Minimum gold tint at the trough of each pulse (0 = white, 1 = full peak color).")]
        [SerializeField, Range(0f, 1f)] private float _winnerPulseMinBlend = 0.5f;
        [Tooltip("Maximum gold tint at the peak of each pulse.")]
        [SerializeField, Range(0f, 1f)] private float _winnerPulseMaxBlend = 0.85f;
        [Tooltip("Scale bump at pulse peak as a fraction of base size (0 = color-only pulse).")]
        [SerializeField, Range(0f, 0.12f)] private float _winnerPulseScale = 0f;
        [Tooltip("Vertical lift in pixels when this card is part of the winning hand.")]
        [SerializeField] private float _winnerLiftPx = 15f;
        [SerializeField] private Color _winnerHighlightPeakColor = new Color(1f, 0.82f, 0.22f, 1f);

        private Sprite    _faceSprite;
        private bool      _isFaceUp;
        private bool      _winnerHighlight;
        private Vector2   _baseAnchoredPosition;
        private bool      _hasBaseAnchoredPosition;
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

            CacheBaseAnchoredPosition();
        }

        private void CacheBaseAnchoredPosition()
        {
            if (transform is not RectTransform rt)
                return;

            _baseAnchoredPosition    = rt.anchoredPosition;
            _hasBaseAnchoredPosition = true;
        }

        /// <summary>Highlights this card when it is part of the winning five-card hand.</summary>
        public void SetWinnerHighlight(bool highlighted)
        {
            bool turningOn = highlighted && !_winnerHighlight;
            _winnerHighlight = highlighted;
            ApplyWinnerLift();

            if (!highlighted)
            {
                StopWinnerPulse();
                if (_cardBackground != null && _isFaceUp)
                    _cardBackground.color = Color.white;
                return;
            }

            if (turningOn || _winnerPulseCoroutine == null)
                StartWinnerPulse();
        }

        private void ApplyWinnerLift()
        {
            if (transform is not RectTransform rt)
                return;

            if (!_hasBaseAnchoredPosition)
                CacheBaseAnchoredPosition();

            rt.anchoredPosition = _winnerHighlight
                ? _baseAnchoredPosition + new Vector2(0f, ResolveWinnerLiftPx())
                : _baseAnchoredPosition;
        }

        private float ResolveWinnerPulseHz() =>
            UIManager.Instance != null ? UIManager.Instance.WinnerCardPulseHz : _winnerPulseHz;

        private float ResolveWinnerPulseMinBlend() =>
            UIManager.Instance != null ? UIManager.Instance.WinnerCardPulseMinBlend : _winnerPulseMinBlend;

        private float ResolveWinnerPulseMaxBlend() =>
            UIManager.Instance != null ? UIManager.Instance.WinnerCardPulseMaxBlend : _winnerPulseMaxBlend;

        private float ResolveWinnerLiftPx() =>
            UIManager.Instance != null ? UIManager.Instance.WinnerCardLiftPx : _winnerLiftPx;

        private Color ResolveWinnerPeakColor() =>
            UIManager.Instance != null ? UIManager.Instance.WinnerCardPeakColor : _winnerHighlightPeakColor;
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

        public Sprite CardBackSprite =>
            _spriteLibrary != null ? _spriteLibrary.CardBack : null;

        /// <summary>Canvas pixel size of this card slot.</summary>
        public Vector2 GetDisplaySize()
        {
            if (transform is not RectTransform rt)
                return Vector2.zero;

            Vector2 size = rt.rect.size;
            return size.sqrMagnitude > 0f ? size : rt.sizeDelta;
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

        /// <summary>Instantly shows the card face (no flip animation).</summary>
        public void FlipToFace(Card card, Action onComplete = null)
        {
            Show(card);
            onComplete?.Invoke();
        }

        /// <summary>Scale flip from face-down to the given card face.</summary>
        public IEnumerator AnimateFlipToFace(Card card)
        {
            if (card == null)
                yield break;

            if (_isFaceUp)
                yield break;

            if (_cardBackground == null)
            {
                Show(card);
                yield break;
            }

            StopWinnerPulse();
            StopFlip();
            gameObject.SetActive(true);
            ShowFaceDown();

            _faceSprite = _spriteLibrary != null ? _spriteLibrary.GetSprite(card) : null;
            yield return FlipRoutine(toFaceUp: true, onComplete: null);
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

        /// <summary>
        /// Flies the card from a table-centre canvas point to its home slot with GGPoker-style motion smears.
        /// Card stays face-down and upright during flight; reveals on landing when requested.
        /// </summary>
        public IEnumerator AnimateFlyInOnCanvas(
            RectTransform canvasRt,
            Vector2 startCanvasPos,
            Card card,
            bool revealFaceUpOnLand,
            float duration,
            float smearLength = 2f,
            float smearAlpha = 0.4f,
            float smearSpawnInterval = 0.02f,
            float smearFadeDuration = 0.18f)
        {
            if (canvasRt == null)
            {
                if (revealFaceUpOnLand && card != null)
                    Show(card);
                else
                    ShowFaceDown();
                yield break;
            }

            StopWinnerPulse();
            StopFlip();

            var rt = (RectTransform)transform;
            Transform homeParent    = rt.parent;
            int       homeSibling   = rt.GetSiblingIndex();
            Vector2   origAnchorMin = rt.anchorMin;
            Vector2   origAnchorMax = rt.anchorMax;
            Vector2   origPivot     = rt.pivot;
            Vector2   origAnchored  = rt.anchoredPosition;
            Vector3   origScale     = rt.localScale;
            Vector2   cardSize      = rt.rect.size.sqrMagnitude > 0f ? rt.rect.size : rt.sizeDelta;

            bool restoreInactive = !gameObject.activeSelf;
            if (restoreInactive)
                gameObject.SetActive(true);

            Vector2 endCanvasPos = CanvasPointFromRect(canvasRt, rt);
            Vector2 flyDir       = endCanvasPos - startCanvasPos;
            if (flyDir.sqrMagnitude > 0.0001f)
                flyDir.Normalize();
            else
                flyDir = Vector2.down;

            ShowFaceDown();

            rt.SetParent(canvasRt, worldPositionStays: false);
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = cardSize;
            rt.localEulerAngles = Vector3.zero;
            rt.anchoredPosition = startCanvasPos;
            rt.localScale       = Vector3.one;

            float flyDuration       = Mathf.Max(0.05f, duration);
            float elapsed           = 0f;
            float nextSmearSpawn    = 0f;
            bool  spawnSmears       = smearSpawnInterval > 0f && smearLength > 1f && smearAlpha > 0f;
            Sprite smearSprite      = _spriteLibrary != null ? _spriteLibrary.CardBack : null;
            float smearSpacing      = cardSize.y * 0.22f;

            while (elapsed < flyDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / flyDuration));
                rt.anchoredPosition = Vector2.Lerp(startCanvasPos, endCanvasPos, t);

                if (spawnSmears && smearSprite != null && elapsed >= nextSmearSpawn)
                {
                    SpawnDealMotionSmear(
                        canvasRt,
                        rt.anchoredPosition,
                        cardSize,
                        flyDir,
                        smearSpacing,
                        smearSprite,
                        smearLength,
                        smearAlpha,
                        smearFadeDuration);
                    nextSmearSpawn += smearSpawnInterval;
                }

                yield return null;
            }

            rt.SetParent(homeParent, worldPositionStays: false);
            rt.SetSiblingIndex(homeSibling);
            rt.anchorMin        = origAnchorMin;
            rt.anchorMax        = origAnchorMax;
            rt.pivot            = origPivot;
            rt.anchoredPosition = origAnchored;
            rt.localScale       = origScale;
            rt.localEulerAngles = Vector3.zero;

            if (revealFaceUpOnLand && card != null)
                Show(card);
            else
                ShowFaceDown();
        }

        /// <summary>Vertical elongated smear trailing behind the card along its flight path.</summary>
        private void SpawnDealMotionSmear(
            RectTransform canvasRt,
            Vector2 headPos,
            Vector2 cardSize,
            Vector2 flyDir,
            float smearSpacing,
            Sprite backSprite,
            float smearLength,
            float startAlpha,
            float fadeDuration)
        {
            var go = new GameObject("_DealSmear", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var smearRt = go.GetComponent<RectTransform>();
            smearRt.SetParent(canvasRt, false);
            smearRt.anchorMin        = new Vector2(0.5f, 0.5f);
            smearRt.anchorMax        = new Vector2(0.5f, 0.5f);
            smearRt.pivot            = new Vector2(0.5f, 0.5f);
            smearRt.localEulerAngles = Vector3.zero;
            smearRt.sizeDelta        = new Vector2(cardSize.x, cardSize.y * smearLength);
            smearRt.anchoredPosition = headPos - flyDir * smearSpacing;

            var img = go.GetComponent<Image>();
            img.sprite        = backSprite;
            img.raycastTarget = false;
            img.color         = new Color(1f, 1f, 1f, startAlpha);

            StartCoroutine(FadeDestroyDealSmear(smearRt, img, fadeDuration));
        }

        private static IEnumerator FadeDestroyDealSmear(RectTransform rt, Image img, float duration)
        {
            if (rt == null || img == null)
                yield break;

            float startAlpha = img.color.a;
            float elapsed    = 0f;
            duration         = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                img.color = new Color(1f, 1f, 1f, startAlpha * (1f - t));
                yield return null;
            }

            if (rt != null)
                Destroy(rt.gameObject);
        }

        /// <summary>
        /// Straight-line fly to the slot home position — no rotation, scale, or bounce (flop deal).
        /// </summary>
        public IEnumerator AnimateStraightFlyOnCanvas(
            RectTransform canvasRt,
            Vector2 startCanvasPos,
            Card card,
            float duration)
        {
            if (canvasRt == null)
            {
                if (card != null)
                    Show(card);
                yield break;
            }

            StopWinnerPulse();
            StopFlip();

            var rt = (RectTransform)transform;
            Transform homeParent    = rt.parent;
            int       homeSibling   = rt.GetSiblingIndex();
            Vector2   origAnchorMin = rt.anchorMin;
            Vector2   origAnchorMax = rt.anchorMax;
            Vector2   origPivot     = rt.pivot;
            Vector2   origAnchored  = rt.anchoredPosition;
            Vector3   origScale     = rt.localScale;
            Vector3   origRotation  = rt.localEulerAngles;
            Vector2   cardSize      = rt.rect.size.sqrMagnitude > 0f ? rt.rect.size : rt.sizeDelta;

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            Vector2 endCanvasPos = CanvasPointFromRect(canvasRt, rt);

            if (card != null)
                Show(card);

            rt.SetParent(canvasRt, worldPositionStays: false);
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = cardSize;
            rt.localEulerAngles = origRotation;
            rt.localScale       = origScale;
            rt.anchoredPosition = startCanvasPos;

            float flyDuration = Mathf.Max(0.05f, duration);
            float elapsed     = 0f;

            while (elapsed < flyDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t   = EaseOutQuad(Mathf.Clamp01(elapsed / flyDuration));
                rt.anchoredPosition = Vector2.Lerp(startCanvasPos, endCanvasPos, t);
                yield return null;
            }

            rt.SetParent(homeParent, worldPositionStays: false);
            rt.SetSiblingIndex(homeSibling);
            rt.anchorMin        = origAnchorMin;
            rt.anchorMax        = origAnchorMax;
            rt.pivot            = origPivot;
            rt.anchoredPosition = origAnchored;
            rt.localScale       = origScale;
            rt.localEulerAngles = origRotation;

            if (card != null)
                Show(card);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private static Vector2 CanvasPointFromRect(RectTransform canvasRt, RectTransform target)
        {
            if (canvasRt == null || target == null)
                return Vector2.zero;

            return canvasRt.InverseTransformPoint(target.TransformPoint(Vector3.zero));
        }

        private IEnumerator FlipRoutine(bool toFaceUp, Action onComplete)
        {
            var rt        = (RectTransform)transform;
            Vector3 orig  = rt.localScale;
            float   half  = flipDuration * 0.5f;
            float   elapsed;

            for (elapsed = 0f; elapsed < half; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / half);
                rt.localScale = new Vector3(orig.x * (1f - t), orig.y, orig.z);
                yield return null;
            }

            rt.localScale = new Vector3(0f, orig.y, orig.z);
            _isFaceUp = toFaceUp;
            _cardBackground.color  = Color.white;
            _cardBackground.sprite = toFaceUp ? _faceSprite : _spriteLibrary?.CardBack;

            for (elapsed = 0f; elapsed < half; elapsed += Time.unscaledDeltaTime)
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
            {
                if (_winnerPulseCoroutine == null)
                    StartWinnerPulse();
                return;
            }

            StopWinnerPulse();
            _cardBackground.color = Color.white;
        }

        private void StartWinnerPulse()
        {
            if (_winnerPulseCoroutine != null)
            {
                StopCoroutine(_winnerPulseCoroutine);
                _winnerPulseCoroutine = null;
            }

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
            float angularSpeed = Mathf.PI * 2f * ResolveWinnerPulseHz();
            Color peakColor    = ResolveWinnerPeakColor();
            var rt = transform as RectTransform;

            while (_winnerHighlight && _isFaceUp && _cardBackground != null)
            {
                float wave  = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * angularSpeed);
                float blend = Mathf.Lerp(ResolveWinnerPulseMinBlend(), ResolveWinnerPulseMaxBlend(), wave);
                _cardBackground.color = Color.Lerp(Color.white, peakColor, blend);

                if (rt != null && _winnerPulseScale > 0f)
                {
                    float scaleMul = 1f + _winnerPulseScale * wave;
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
