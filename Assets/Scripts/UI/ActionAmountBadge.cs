using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Dark rounded pill + gold amount — matches seat BetDisplay AmountBadge.</summary>
    public class ActionAmountBadge : MonoBehaviour
    {
        public const float BadgeWidth  = 90f;
        public const float BadgeHeight = 30f;

        private static readonly NumberFormatInfo GermanNFI = new NumberFormatInfo
        {
            NumberGroupSeparator   = ".",
            NumberDecimalSeparator = ",",
            NumberDecimalDigits    = 0,
            NumberGroupSizes       = new[] { 3 }
        };

        [SerializeField] private Image    _background;
        [SerializeField] private TMP_Text _amountText;

        public void SetAmount(int amount)
        {
            EnsureRefs();
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
                    return badge;
            }

            Transform parent = ActionPanelLayout.EnsureBelowSpacer(column) ?? column;

            var badgeGo = new GameObject("AmountBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(ActionAmountBadge));
            badgeGo.transform.SetParent(parent, false);
            badgeGo.transform.SetAsFirstSibling();

            var created = badgeGo.GetComponent<ActionAmountBadge>();
            created.Build();
            created.Hide();
            return created;
        }

        private void Build()
        {
            EnsureRefs();

            Sprite sprite = BetDisplay.ResolveAmountBadgeSprite();
            if (_background != null)
            {
                _background.sprite        = sprite;
                _background.type          = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                _background.color         = BetDisplay.DefaultAmountBadgeColor;
                _background.raycastTarget = false;
            }

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(BadgeWidth, BadgeHeight);

            var element = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            element.minHeight       = BadgeHeight;
            element.preferredHeight = BadgeHeight;
            element.flexibleWidth   = 0f;
            element.flexibleHeight  = 0f;

            if (_amountText != null)
            {
                _amountText.alignment          = TextAlignmentOptions.Center;
                _amountText.fontStyle          = FontStyles.Bold;
                _amountText.enableWordWrapping = false;
                _amountText.overflowMode       = TextOverflowModes.Ellipsis;
                _amountText.raycastTarget      = false;
                _amountText.color              = UiColors.PotGold;
                _amountText.fontSize           = PlayerHudLayout.StackAmountFontSize;
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
