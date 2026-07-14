using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>PNG action badge above the player name when a player acts or wins.</summary>
    public class ActionBadge : MonoBehaviour
    {
        public const float DisplayDurationSecs    = 3f;
        public const float BotDisplayDurationSecs = 1.5f;

        private const string BadgeFaceName     = "BadgeFace";
        private const string WinnerShadowName  = "WinnerShadow";
        private const string WinnerGradientName = "WinnerGradient";

        [SerializeField] private Image _badgeImage;

        [Header("Layout (optional)")]
        [Tooltip("When enabled, uses Custom Position and Custom Height instead of auto card-centre layout.")]
        [SerializeField] private bool _useCustomLayout;
        [SerializeField] private Vector2 _customAnchoredPosition = new Vector2(0f, 55f);
        [Tooltip("Badge height in pixels; width follows sprite aspect ratio.")]
        [SerializeField] private float _customHeight = ActionBadgeSprites.DefaultBadgeHeight;

        [Header("Winner FX")]
        [SerializeField, Min(0.15f)] private float _winnerPopDuration = 0.18f;
        [SerializeField] private float _winnerPopScaleMin  = 0.95f;
        [SerializeField] private float _winnerPopScalePeak = 1.05f;
        [SerializeField] private Color _winnerGradientTop    = new Color(1f, 0.85f, 0.2f, 0.42f);
        [SerializeField] private Color _winnerGradientBottom = new Color(0.65f, 0.55f, 0.13f, 0.58f);
        [SerializeField] private Color _winnerShadowColor = new Color(0f, 0f, 0f, 0.25f);
        [SerializeField] private Vector2 _winnerShadowDistance = new Vector2(1f, -2f);
        [SerializeField] private float _winnerBackdropInset = 2f;

        private Image                     _winnerShadowImage;
        private ActionBadgeGradientImage  _winnerGradientImage;
        private Coroutine                 _winnerPopCoroutine;

        public bool UsesCustomLayout => _useCustomLayout;

        internal void ApplyCustomLayout(RectTransform rt, Sprite sprite)
        {
            if (rt == null)
                return;

            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = _customAnchoredPosition;
            rt.sizeDelta        = ActionBadgeSprites.SizeForSprite(sprite, _customHeight);
        }

        private void Awake()
        {
            ActionBadgeSprites.EnsureLoaded();
            ActionBadgeUtility.Repair(gameObject, this);
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            EnsureBadgeStructure();
            _badgeImage ??= transform.Find(BadgeFaceName)?.GetComponent<Image>();
        }

        internal void WireBadgeImage(Image image) => _badgeImage = image;

        /// <summary>Shows the badge for the given betting action.</summary>
        public void Show(BettingAction action, int amount = 0, float durationSecs = DisplayDurationSecs)
        {
            PresentSprite(ActionBadgeSprites.For(action), durationSecs);
        }

        /// <summary>Hides the badge immediately.</summary>
        public void Hide()
        {
            CancelInvoke(nameof(Hide));
            StopWinnerPop();
            ResetWinnerPresentation();
            HideLabelChild();
            gameObject.SetActive(false);
        }

        /// <summary>Shows the winner badge for <paramref name="duration"/> seconds (0 = stay until hidden).</summary>
        public void ShowWin(int potAmount, float duration)
        {
            PresentSprite(ActionBadgeSprites.Winner, duration);
        }

        /// <summary>Shows the winner badge until <see cref="Hide"/> or the next hand.</summary>
        public void ShowWinPersistent(int potAmount = 0)
        {
            PresentSprite(ActionBadgeSprites.Winner, 0f);
        }

        private void PresentSprite(Sprite sprite, float duration)
        {
            if (sprite == null)
            {
                Debug.LogWarning("[ActionBadge] Missing badge sprite — run Texas Holdem → Create Action Badge Sprite Set.", this);
                return;
            }

            enabled = true;
            ActionBadgeSprites.EnsureLoaded();
            ActionBadgeUtility.Repair(gameObject, this);
            ResolveReferences();

            if (_badgeImage == null)
            {
                Debug.LogWarning("[ActionBadge] Badge Image missing after repair — run Texas Holdem → Repair Action Badges In Scene.", this);
                return;
            }

            bool isWinner = sprite == ActionBadgeSprites.Winner;

            _badgeImage.sprite         = sprite;
            _badgeImage.color          = Color.white;
            _badgeImage.preserveAspect = true;
            _badgeImage.enabled        = true;

            HideLabelChild();
            ApplyLayout(sprite);
            SetWinnerFxVisible(isWinner);

            if (!isWinner)
                BringToFrontOfSeat();

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (isWinner)
                StartWinnerPop();
            else
                ResetWinnerPresentation();

            CancelInvoke(nameof(Hide));
            if (duration > 0f)
                Invoke(nameof(Hide), duration);
        }

        private void ApplyLayout(Sprite sprite)
        {
            RectTransform rt = transform as RectTransform;
            if (rt == null)
                return;

            if (_useCustomLayout)
            {
                ApplyCustomLayout(rt, sprite);
                ActionBadgeUtility.ApplyGlobalOffset(rt);
            }
            else
                ActionBadgeUtility.ApplyAutoLayoutRect(rt, sprite);

            SyncWinnerFxLayout();
        }

        /// <summary>Re-applies layout (e.g. after global offset changes in Play mode).</summary>
        public void RefreshLayout()
        {
            ResolveReferences();
            Sprite sprite = _badgeImage != null ? _badgeImage.sprite : null;
            sprite ??= ActionBadgeSprites.For(BettingAction.Check) ?? ActionBadgeSprites.Winner;
            ApplyLayout(sprite);
        }

        internal void EnsureBadgeStructure()
        {
            ActionBadgeUtility.EnsureBadgeFaceImage(gameObject);
            EnsureWinnerShadowLayer();
            EnsureWinnerGradientLayer();

            Transform face = transform.Find(BadgeFaceName);
            if (face != null)
            {
                face.SetAsLastSibling();
                int faceIndex = face.GetSiblingIndex();
                if (_winnerGradientImage != null)
                    _winnerGradientImage.transform.SetSiblingIndex(Mathf.Max(0, faceIndex - 1));
                if (_winnerShadowImage != null)
                    _winnerShadowImage.transform.SetSiblingIndex(Mathf.Max(0, faceIndex - 2));
            }

            _badgeImage = face != null ? face.GetComponent<Image>() : null;
        }

        private void EnsureWinnerShadowLayer()
        {
            Transform layer = transform.Find(WinnerShadowName);
            if (layer == null)
            {
                var layerGo = new GameObject(WinnerShadowName, typeof(RectTransform), typeof(CanvasRenderer));
                layerGo.transform.SetParent(transform, false);
                _winnerShadowImage = layerGo.AddComponent<Image>();
            }
            else
            {
                _winnerShadowImage = layer.GetComponent<Image>();
                if (_winnerShadowImage == null)
                    _winnerShadowImage = layer.gameObject.AddComponent<Image>();
            }

            ConfigureBackdropGraphic(_winnerShadowImage, sliced: true);
            _winnerShadowImage.color = _winnerShadowColor;
            _winnerShadowImage.gameObject.SetActive(false);
        }

        private void EnsureWinnerGradientLayer()
        {
            Transform gradientTransform = transform.Find(WinnerGradientName);
            if (gradientTransform == null)
            {
                var gradientGo = new GameObject(WinnerGradientName, typeof(RectTransform), typeof(CanvasRenderer));
                gradientGo.transform.SetParent(transform, false);
                _winnerGradientImage = gradientGo.AddComponent<ActionBadgeGradientImage>();
            }
            else
            {
                _winnerGradientImage = gradientTransform.GetComponent<ActionBadgeGradientImage>();
                if (_winnerGradientImage == null)
                {
                    Image legacy = gradientTransform.GetComponent<Image>();
                    if (legacy != null)
                        ActionBadgeUtility.DestroyGraphic(legacy);

                    _winnerGradientImage = gradientTransform.gameObject.AddComponent<ActionBadgeGradientImage>();
                }
            }

            ConfigureBackdropGraphic(_winnerGradientImage, sliced: true);
            _winnerGradientImage.SetColors(_winnerGradientTop, _winnerGradientBottom);

            Shadow gradientShadow = _winnerGradientImage.GetComponent<Shadow>();
            if (gradientShadow == null)
                gradientShadow = _winnerGradientImage.gameObject.AddComponent<Shadow>();

            gradientShadow.effectColor     = new Color(0f, 0f, 0f, _winnerShadowColor.a * 0.55f);
            gradientShadow.effectDistance  = _winnerShadowDistance * 0.75f;
            gradientShadow.useGraphicAlpha = true;

            _winnerGradientImage.gameObject.SetActive(false);
        }

        private void ConfigureBackdropGraphic(Image image, bool sliced)
        {
            if (image == null)
                return;

            Sprite backdrop = BetDisplay.ResolveAmountBadgeSprite();
            image.sprite         = backdrop;
            image.type           = sliced && backdrop != null ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget  = false;
            image.maskable       = true;
        }

        private void SyncWinnerFxLayout()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            SyncWinnerShadowLayout(root);
            ApplyInsetFill(_winnerGradientImage?.rectTransform, _winnerBackdropInset);
        }

        private void SyncWinnerShadowLayout(RectTransform root)
        {
            RectTransform shadowRt = _winnerShadowImage?.rectTransform;
            if (shadowRt == null)
                return;

            shadowRt.anchorMin        = new Vector2(0.5f, 0.5f);
            shadowRt.anchorMax        = new Vector2(0.5f, 0.5f);
            shadowRt.pivot            = new Vector2(0.5f, 0.5f);
            shadowRt.sizeDelta        = root.sizeDelta;
            shadowRt.anchoredPosition = _winnerShadowDistance;
            _winnerShadowImage.color  = _winnerShadowColor;
        }

        private static void ApplyInsetFill(RectTransform rt, float inset)
        {
            if (rt == null)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private void SetWinnerFxVisible(bool visible)
        {
            if (_winnerShadowImage != null)
                _winnerShadowImage.gameObject.SetActive(visible);

            if (_winnerGradientImage != null)
            {
                if (visible)
                    _winnerGradientImage.SetColors(_winnerGradientTop, _winnerGradientBottom);
                _winnerGradientImage.gameObject.SetActive(visible);
            }
        }

        private void StartWinnerPop()
        {
            StopWinnerPop();
            ResetLocalScale();
            _winnerPopCoroutine = StartCoroutine(AnimateWinnerPop());
        }

        private void StopWinnerPop()
        {
            if (_winnerPopCoroutine == null)
                return;

            StopCoroutine(_winnerPopCoroutine);
            _winnerPopCoroutine = null;
        }

        private void ResetWinnerPresentation()
        {
            StopWinnerPop();
            ResetLocalScale();
            SetWinnerFxVisible(false);
        }

        private void ResetLocalScale()
        {
            transform.localScale = Vector3.one;
        }

        private IEnumerator AnimateWinnerPop()
        {
            float halfDuration = _winnerPopDuration * 0.5f;
            yield return LerpScale(_winnerPopScaleMin, _winnerPopScalePeak, halfDuration);
            yield return LerpScale(_winnerPopScalePeak, 1f, halfDuration);
            transform.localScale = Vector3.one;
            _winnerPopCoroutine  = null;
        }

        private IEnumerator LerpScale(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                transform.localScale = Vector3.one * to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t     = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOut(t);
                float scale = Mathf.Lerp(from, to, eased);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }

            transform.localScale = Vector3.one * to;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private void HideLabelChild()
        {
            Transform label = transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);
        }

        /// <summary>Draws above cards, name, and bet chip display on the seat.</summary>
        public void BringToFront()
        {
            Transform parent = transform.parent;
            if (parent == null)
                return;

            transform.SetAsLastSibling();
        }

        private void BringToFrontOfSeat() => BringToFront();
    }
}
