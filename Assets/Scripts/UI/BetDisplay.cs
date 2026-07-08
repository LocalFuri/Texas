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
    /// When the bet increases and <see cref="UIManager"/> has Animate Bet Place enabled,
    /// a chip graphic flies from the player's avatar to ChipStack.
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

        public static readonly Color DefaultAmountBadgeColor = new Color(0.06f, 0.06f, 0.08f, 0.93f);

        [SerializeField] private TMP_Text       _amountText;
        [SerializeField] private ChipStackView    _chipStackView;
        [SerializeField] private RectTransform    _chipStackRoot;

        [Header("Amount Badge")]
        [Tooltip("Rounded pill behind the bet amount. Tint here — layout code does not overwrite this.")]
        [SerializeField] private Image _amountBadgeImage;
        [SerializeField] private Color _amountBadgeColor = DefaultAmountBadgeColor;

        private const float AnimDuration = 0.45f;

        private static float ChipAnimSize => ChipStackView.ResolveChipDisplaySize();

        private static readonly AnimationCurve EaseCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly Color ChipAnimFallbackColor = new Color(0.12f, 0.42f, 0.19f, 1f);

        private Canvas     _rootCanvas;
        private Coroutine  _chipCoroutine;
        private GameObject _chipAnimGo;

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

            ResolveAmountBadgeImage();
            ApplyAmountBadgeColor();
        }

        private void Start() => ApplyAmountBadgeColor();

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveAmountBadgeImage();
            ApplyAmountBadgeColor();
        }
#endif

        private void ResolveAmountBadgeImage()
        {
            if (_amountBadgeImage != null)
                return;

            Transform badge = transform.Find("AmountBadge");
            if (badge != null)
                _amountBadgeImage = badge.GetComponent<Image>();
        }

        private void ApplyAmountBadgeColor()
        {
            ResolveAmountBadgeImage();
            if (_amountBadgeImage != null)
                _amountBadgeImage.color = _amountBadgeColor;
        }

        public Sprite GetAmountBadgeSprite()
        {
            ResolveAmountBadgeImage();
            return _amountBadgeImage != null ? _amountBadgeImage.sprite : null;
        }

        /// <summary>RoundedRect sprite from any seat bet badge, for action-panel amount pills.</summary>
        public static Sprite ResolveAmountBadgeSprite()
        {
#if UNITY_2022_2_OR_NEWER
            BetDisplay[] displays = Object.FindObjectsByType<BetDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            BetDisplay[] displays = Object.FindObjectsOfType<BetDisplay>(true);
#endif
            foreach (BetDisplay display in displays)
            {
                Sprite sprite = display.GetAmountBadgeSprite();
                if (sprite != null)
                    return sprite;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Graphic/UI/RoundedRect.png");
#else
            return null;
#endif
        }

        public const float ChipFlyDuration = AnimDuration;

        public bool HasVisibleBet => gameObject.activeSelf;

        public RectTransform ChipStackOrigin =>
            _chipStackRoot != null
                ? _chipStackRoot
                : _chipStackView != null
                    ? _chipStackView.StackRoot
                    : (RectTransform)transform;

        public void ShowBet(int amount, RectTransform fromRect = null)
        {
            ApplyAmountBadgeColor();

            if (_amountText != null)
                _amountText.text = amount.ToString("N0", GermanNFI);

            _chipStackView?.SetExactAmount(amount);
            gameObject.SetActive(true);
            TableLayoutManager.SyncBetDisplayLayout(transform);

            if (fromRect != null && _rootCanvas != null)
            {
                CancelChipAnim();
                _chipCoroutine = StartCoroutine(AnimateChip(fromRect, amount));
            }
            else
                CancelChipAnim();
        }

        public void HideBet()
        {
            CancelChipAnim();
            _chipStackView?.Clear();
            gameObject.SetActive(false);
        }

        private void CancelChipAnim()
        {
            if (_chipCoroutine != null)
            {
                StopCoroutine(_chipCoroutine);
                _chipCoroutine = null;
            }

            if (_chipAnimGo != null)
            {
                Destroy(_chipAnimGo);
                _chipAnimGo = null;
            }
        }

        /// <summary>Flies a chip from the bet stack toward the pot at end of a betting street.</summary>
        public IEnumerator PlayCollectToPot(
            RectTransform potTarget, int amount, float flyDuration, float delay = 0f, bool useArc = true)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_rootCanvas == null || potTarget == null)
                yield break;

            RectTransform origin = ChipStackOrigin;
            Sprite chipSprite = _chipStackView != null
                ? _chipStackView.SpriteForAmount(amount)
                : null;

            var chipGo  = new GameObject("_ChipCollect", typeof(RectTransform), typeof(Image));
            var chipImg = chipGo.GetComponent<Image>();
            chipImg.sprite         = chipSprite;
            chipImg.color          = chipSprite != null ? Color.white : ChipAnimFallbackColor;
            chipImg.raycastTarget  = false;
            chipImg.preserveAspect = true;

            var chipRt = (RectTransform)chipGo.transform;
            chipRt.SetParent((RectTransform)_rootCanvas.transform, false);
            chipRt.sizeDelta = new Vector2(ChipAnimSize, ChipAnimSize);
            chipRt.anchorMin = new Vector2(0.5f, 0.5f);
            chipRt.anchorMax = new Vector2(0.5f, 0.5f);
            chipRt.pivot     = new Vector2(0.5f, 0.5f);

            Vector2 startPos = ToCanvasLocal(origin);
            Vector2 endPos   = ToCanvasLocal(potTarget);
            startPos += new Vector2(Random.Range(-8f, 8f), Random.Range(-4f, 4f));
            chipRt.anchoredPosition = startPos;

            float duration = Mathf.Max(0.05f, flyDuration);
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseCurve.Evaluate(Mathf.Clamp01(elapsed / duration));

                Vector2 pos = Vector2.Lerp(startPos, endPos, t);
                if (useArc)
                    pos.y += Mathf.Sin(t * Mathf.PI) * 40f;

                chipRt.anchoredPosition = pos;
                chipRt.localScale       = Vector3.Lerp(Vector3.one * 1.1f, Vector3.one * 0.75f, t);

                float alpha = t > 0.75f ? Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f) : 1f;
                chipImg.color = new Color(chipImg.color.r, chipImg.color.g, chipImg.color.b, alpha);
                yield return null;
            }

            if (chipGo != null)
                Destroy(chipGo);
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

            _chipAnimGo = chipGo;

            Vector2 startPos        = ToCanvasLocal(origin);
            Vector2 endPos          = ToCanvasLocal(target);
            chipRt.anchoredPosition = startPos;

            try
            {
                float elapsed = 0f;
                while (elapsed < AnimDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = EaseCurve.Evaluate(Mathf.Clamp01(elapsed / AnimDuration));
                    chipRt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                    chipRt.localScale       = Vector3.Lerp(Vector3.one * 1.4f, Vector3.one * 0.8f, t);

                    float alpha = Mathf.Lerp(1f, 0f, t * t);
                    chipImg.color = new Color(chipImg.color.r, chipImg.color.g, chipImg.color.b, alpha);
                    yield return null;
                }
            }
            finally
            {
                if (_chipAnimGo == chipGo)
                    _chipAnimGo = null;
                if (chipGo != null)
                    Destroy(chipGo);
                _chipCoroutine = null;
            }
        }

        private Vector2 ToCanvasLocal(RectTransform rt)
        {
            var canvasRt = (RectTransform)_rootCanvas.transform;
            return canvasRt.InverseTransformPoint(rt.TransformPoint(Vector3.zero));
        }
    }
}
