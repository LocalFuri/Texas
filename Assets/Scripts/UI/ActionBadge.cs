using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>PNG action badge above the player name when a player acts or wins.</summary>
    public class ActionBadge : MonoBehaviour
    {
        public const float DisplayDurationSecs    = 3f;
        public const float BotDisplayDurationSecs = 1.5f;

        private static readonly NumberFormatInfo GermanNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0,
            NumberGroupSizes       = new[] { 3 }
        };

        [SerializeField] private Image _badgeImage;

        [Header("Layout (optional)")]
        [Tooltip("When enabled, uses Custom Position and Custom Height instead of auto card-centre layout.")]
        [SerializeField] private bool _useCustomLayout;
        [SerializeField] private Vector2 _customAnchoredPosition = new Vector2(0f, 55f);
        [Tooltip("Badge height in pixels; width follows sprite aspect ratio.")]
        [SerializeField] private float _customHeight = ActionBadgeSprites.DefaultBadgeHeight;

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
            _badgeImage ??= GetComponent<Image>();
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
            HideLabelChild();
            gameObject.SetActive(false);
        }

        /// <summary>Shows the winner badge for <paramref name="duration"/> seconds (0 = stay until hidden).</summary>
        public void ShowWin(int potAmount, float duration)
        {
            PresentSprite(ActionBadgeSprites.Winner, duration);
            SetWinAmountLabel(potAmount);
        }

        /// <summary>Shows the winner badge until <see cref="Hide"/> or the next hand.</summary>
        public void ShowWinPersistent(int potAmount = 0)
        {
            PresentSprite(ActionBadgeSprites.Winner, 0f);
            SetWinAmountLabel(potAmount);
        }

        private void PresentSprite(Sprite sprite, float duration)
        {
            if (sprite == null)
            {
                Debug.LogWarning("[ActionBadge] Missing badge sprite — run Texas Holdem → Create Action Badge Sprite Set.", this);
                return;
            }

            enabled = true;
            ActionBadgeUtility.Repair(gameObject, this);
            ResolveReferences();

            if (_badgeImage == null)
            {
                Debug.LogWarning("[ActionBadge] Badge Image missing after repair — run Texas Holdem → Repair Action Badges In Scene.", this);
                return;
            }

            _badgeImage.sprite         = sprite;
            _badgeImage.color          = Color.white;
            _badgeImage.preserveAspect = true;
            _badgeImage.enabled        = true;

            HideLabelChild();
            ApplyLayout(sprite);
            BringToFrontOfSeat();

            // Activate after setup — prefab starts inactive; never call Hide() from Awake (that races first Show).
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

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
        }

        /// <summary>Re-applies layout (e.g. after global offset changes in Play mode).</summary>
        public void RefreshLayout()
        {
            ResolveReferences();
            Sprite sprite = _badgeImage != null ? _badgeImage.sprite : null;
            sprite ??= ActionBadgeSprites.For(BettingAction.Check) ?? ActionBadgeSprites.Winner;
            ApplyLayout(sprite);
        }

        private void HideLabelChild()
        {
            Transform label = transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);
        }

        private void SetWinAmountLabel(int potAmount)
        {
            Transform labelT = transform.Find("Label");
            if (labelT == null)
                return;

            if (potAmount <= 0)
            {
                labelT.gameObject.SetActive(false);
                return;
            }

            TMP_Text text = ResolveWinAmountLabel(labelT);
            if (text == null)
                return;

            text.text               = potAmount.ToString("N0", GermanNFI);
            text.alignment          = TextAlignmentOptions.Center;
            text.fontStyle          = FontStyles.Bold;
            text.color              = Color.white;
            text.raycastTarget      = false;
            text.enableWordWrapping = false;

            RectTransform labelRt = labelT as RectTransform;
            if (labelRt != null)
            {
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
            }

            labelT.gameObject.SetActive(true);
            labelT.SetAsLastSibling();
        }

        private static TMP_Text ResolveWinAmountLabel(Transform labelT)
        {
            TMP_Text text = labelT.GetComponent<TMP_Text>();
            if (text != null)
                return text;

            text = labelT.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18f;
            return text;
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
