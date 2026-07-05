using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    [Serializable]
    public class ActionAmountBadgeStyle
    {
        public const float DefaultBadgeWidth  = 90f;
        public const float DefaultBadgeHeight = 30f;
        public const float DefaultFontSize    = 14f;

        private static readonly Color DefaultTextColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Header("Colors")]
        public Color backgroundColor = BetDisplay.DefaultAmountBadgeColor;
        public Color textColor       = DefaultTextColor;

        [Header("Text")]
        public float fontSize = DefaultFontSize;

        [Header("Size")]
        public float badgeWidth  = DefaultBadgeWidth;
        public float badgeHeight = DefaultBadgeHeight;
        [Tooltip("When on, badge width follows the action button width.")]
        public bool matchButtonWidth = false;

        [Header("Position")]
        [Tooltip("Offset from the center of the below-button slot. Positive Y moves toward the button.")]
        public Vector2 anchoredOffset;

        public float BelowSlotHeight =>
            Mathf.Max(badgeHeight, RaiseInputBuilder.InputHeight) + RaiseInputBuilder.ColumnSpacing;
    }

    /// <summary>Dark rounded pill + gold amount under action-panel buttons.</summary>
    [ExecuteAlways]
    public class ActionAmountBadge : MonoBehaviour
    {
        public const float BadgeWidth  = ActionAmountBadgeStyle.DefaultBadgeWidth;
        public const float BadgeHeight = ActionAmountBadgeStyle.DefaultBadgeHeight;

        private static readonly NumberFormatInfo GermanNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0,
            NumberGroupSizes       = new[] { 3 }
        };

        [SerializeField] private ActionAmountBadgeStyle _style = new ActionAmountBadgeStyle();
        [SerializeField] private Image    _background;
        [SerializeField] private TMP_Text _amountText;

        public ActionAmountBadgeStyle Style => _style;

        public float LayoutHeight => _style.badgeHeight;

        public void Configure(ActionAmountBadgeStyle style)
        {
            if (style == null)
                return;

            _style = style;
            ApplyStyle();
        }

        public void ApplyLayout(float buttonWidth, float belowSlotHeight)
        {
            EnsureRefs();

            float width = _style.matchButtonWidth && buttonWidth > 0f
                ? buttonWidth
                : _style.badgeWidth;

            if (transform is not RectTransform rt)
                return;

            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(width, _style.badgeHeight);
            rt.anchoredPosition = new Vector2(
                _style.anchoredOffset.x,
                belowSlotHeight * 0.5f + _style.anchoredOffset.y);

            var element = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        public void SetAmount(int amount)
        {
            EnsureRefs();
            ApplyStyle();

            if (_amountText != null)
                _amountText.text = amount.ToString("N0", GermanNFI);

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public static ActionAmountBadge Ensure(Transform column)
        {
            if (column == null)
                return null;

            Transform existing = column.Find("AmountBadge");
            if (existing == null)
            {
                Transform spacer = column.Find(ActionPanelLayout.BelowSpacerName);
                if (spacer != null)
                    existing = spacer.Find("AmountBadge");
            }

            if (existing != null)
            {
                var badge = existing.GetComponent<ActionAmountBadge>();
                if (badge != null)
                {
                    if (existing.parent != column)
                    {
                        existing.SetParent(column, false);
                        existing.SetAsLastSibling();
                    }

                    return badge;
                }
            }

            var badgeGo = new GameObject("AmountBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(ActionAmountBadge));
            badgeGo.transform.SetParent(column, false);
            badgeGo.transform.SetAsLastSibling();

            var created = badgeGo.GetComponent<ActionAmountBadge>();
            created.Build();
            created.Hide();
            return created;
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            ApplyStyle();
        }

        private void Build()
        {
            EnsureRefs();
            ApplyStyle();
        }

        private void ApplyStyle()
        {
            EnsureRefs();

            Sprite sprite = BetDisplay.ResolveAmountBadgeSprite();
            if (_background != null)
            {
                _background.sprite        = sprite;
                _background.type          = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                _background.color         = _style.backgroundColor;
                _background.raycastTarget = false;
            }

            if (_amountText != null)
            {
                _amountText.alignment          = TextAlignmentOptions.Center;
                _amountText.fontStyle          = FontStyles.Bold;
                _amountText.enableWordWrapping = false;
                _amountText.overflowMode       = TextOverflowModes.Ellipsis;
                _amountText.raycastTarget      = false;
                _amountText.color              = _style.textColor;
                _amountText.fontSize           = _style.fontSize;
                PlayerHudLayout.ApplyStackAmountFontIfMissing(_amountText);
            }

        }

        private void EnsureRefs()
        {
            _background ??= GetComponent<Image>();

            if (_amountText == null)
            {
                Transform textT = transform.Find("AmountText");
                if (textT != null)
                    _amountText = textT.GetComponent<TMP_Text>();
            }

            if (_amountText != null)
                return;

            var textGo = new GameObject("AmountText", typeof(RectTransform));
            textGo.transform.SetParent(transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _amountText = textGo.AddComponent<TextMeshProUGUI>();
        }
    }
}
