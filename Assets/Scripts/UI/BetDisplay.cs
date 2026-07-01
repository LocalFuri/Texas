using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Displays a player's current round bet as a chip stack + amount badge.
    /// Layout (managed by TableLayoutManager + PlayerSeatLayout):
    ///   BetAnchor — gap below avatar bottom, then chips, then amount (centred on avatar X)
    ///   ChipStack — centred above amount badge
    ///   AmountBadge — below chip stack
    ///
    /// When the bet increases, a chip graphic flies from the player's avatar to ChipStack.
    /// </summary>
    public class BetDisplay : MonoBehaviour
    {
        private static readonly NumberFormatInfo GermanNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0,
            NumberGroupSizes       = new[] { 3 }
        };

        [SerializeField] private TMP_Text       _amountText;
        [SerializeField] private ChipStackView    _chipStackView;
        [SerializeField] private RectTransform    _chipStackRoot;

        private const float AnimDuration = 0.45f;
        private const float ChipAnimSize = 34f * 1.25f;

        private static readonly AnimationCurve EaseCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly Color ChipAnimFallbackColor = new Color(0.12f, 0.42f, 0.19f, 1f);

        private Canvas    _rootCanvas;
        private Coroutine _chipCoroutine;

        private void Awake()
        {
            if (_chipStackView == null)
                _chipStackView = GetComponentInChildren<ChipStackView>(true);
            if (_chipStackRoot == null && _chipStackView != null)
                _chipStackRoot = _chipStackView.StackRoot;

            Canvas c = GetComponentInParent<Canvas>();
            while (c != null && !c.isRootCanvas)
                c = c.transform.parent != null
                    ? c.transform.parent.GetComponentInParent<Canvas>()
                    : null;
            _rootCanvas = c;

            if (_amountText == null)
                _amountText = transform.Find("AmountBadge/AmountText")?.GetComponent<TMP_Text>();
        }

        public void ShowBet(int amount, RectTransform fromRect = null)
        {
            if (_amountText != null)
                _amountText.text = amount.ToString("N0", GermanNFI) + " \u20AC";

            _chipStackView?.SetAmount(amount);
            gameObject.SetActive(true);

            if (fromRect != null && _rootCanvas != null)
            {
                if (_chipCoroutine != null) StopCoroutine(_chipCoroutine);
                _chipCoroutine = StartCoroutine(AnimateChip(fromRect, amount));
            }
        }

        public void HideBet()
        {
            if (_chipCoroutine != null)
            {
                StopCoroutine(_chipCoroutine);
                _chipCoroutine = null;
            }
            _chipStackView?.Clear();
            gameObject.SetActive(false);
        }

        private IEnumerator AnimateChip(RectTransform origin, int amount)
        {
            RectTransform target = _chipStackRoot != null
                ? _chipStackRoot
                : _chipStackView != null
                    ? _chipStackView.StackRoot
                    : (RectTransform)transform;

            Sprite chipSprite = _chipStackView != null
                ? _chipStackView.SpriteForAmount(amount)
                : null;

            var chipGo  = new GameObject("_ChipAnim", typeof(RectTransform), typeof(Image));
            var chipImg = chipGo.GetComponent<Image>();
            chipImg.sprite         = chipSprite;
            chipImg.color          = chipSprite != null ? Color.white : ChipAnimFallbackColor;
            chipImg.raycastTarget  = false;
            chipImg.preserveAspect = true;

            var chipRt = (RectTransform)chipGo.transform;
            chipRt.SetParent((RectTransform)_rootCanvas.transform, false);
            chipRt.sizeDelta  = new Vector2(ChipAnimSize, ChipAnimSize);
            chipRt.anchorMin  = new Vector2(0.5f, 0.5f);
            chipRt.anchorMax  = new Vector2(0.5f, 0.5f);
            chipRt.pivot      = new Vector2(0.5f, 0.5f);

            Vector2 startPos        = ToCanvasLocal(origin);
            Vector2 endPos          = ToCanvasLocal(target);
            chipRt.anchoredPosition = startPos;

            float elapsed = 0f;
            while (elapsed < AnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseCurve.Evaluate(Mathf.Clamp01(elapsed / AnimDuration));
                chipRt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                chipRt.localScale       = Vector3.Lerp(Vector3.one * 1.4f, Vector3.one * 0.8f, t);

                float alpha = chipSprite != null
                    ? Mathf.Lerp(1f, 0f, t * t)
                    : Mathf.Lerp(1f, 0f, t * t);
                chipImg.color = new Color(chipImg.color.r, chipImg.color.g, chipImg.color.b, alpha);
                yield return null;
            }

            if (chipGo != null) Destroy(chipGo);
            _chipCoroutine = null;
        }

        private Vector2 ToCanvasLocal(RectTransform rt)
        {
            var canvasRt = (RectTransform)_rootCanvas.transform;
            return canvasRt.InverseTransformPoint(rt.TransformPoint(Vector3.zero));
        }
    }
}
