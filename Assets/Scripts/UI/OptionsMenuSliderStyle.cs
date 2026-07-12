using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Reference-style slider: green fill, white value handle, black number.</summary>
    public static class OptionsMenuSliderStyle
    {
        public const string BotThinkLabelText = "Bots think";
        public const string EquitySimsLabelText = "Equity sims";
        public const float  TrackHeight       = 14f;
        public const float  HandleWidth       = 58f;
        public const float  HandleHeight      = 19f;
        public const float  LabelWidth        = 102f;
        public const float  SliderMinWidth    = 120f;
        public const float  LabelSliderGap    = 10f;
        public const float  TrackLeftInset    = 2f;

        public static float LabelFontSize => OptionsMenu.MenuRowFontSize;

        private static readonly Color FillColor      = new Color(0.275f, 0.557f, 0.286f, 1f);
        private static readonly Color HandleLabelColor = new Color(0f, 0f, 0f, 1f);

        public static void Apply(Slider slider, TMP_Text handleLabel = null)
        {
            if (slider == null)
                return;

            Sprite white = OptionsMenuToggleStyle.GetCheckboxSprite();
            TMP_Text rowLabel = slider.transform.parent?.Find("Label")?.GetComponent<TMP_Text>();

            Transform background = slider.transform.Find("Background");
            if (background != null)
            {
                var bgImg = background.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.sprite        = white;
                    bgImg.type          = Image.Type.Simple;
                    bgImg.color         = new Color(1f, 1f, 1f, 0f);
                    bgImg.raycastTarget = true;
                }

                SetCenteredBar((RectTransform)background, TrackHeight);
                InsetTrackLeft((RectTransform)background);
            }

            Transform fillArea = slider.transform.Find("Fill Area");
            if (fillArea is RectTransform fillAreaRt)
            {
                fillAreaRt.anchorMin = Vector2.zero;
                fillAreaRt.anchorMax = Vector2.one;
                fillAreaRt.offsetMin = new Vector2(TrackLeftInset, 0f);
                fillAreaRt.offsetMax = new Vector2(-HandleWidth * 0.5f, 0f);
            }

            Transform fill = slider.transform.Find("Fill Area/Fill");
            if (fill != null)
            {
                var fillImg = fill.GetComponent<Image>();
                if (fillImg != null)
                {
                    fillImg.sprite        = white;
                    fillImg.type          = Image.Type.Simple;
                    fillImg.color         = FillColor;
                    fillImg.raycastTarget = false;
                }
            }

            Transform handle = slider.transform.Find("Handle Slide Area/Handle");
            if (handle is RectTransform handleRt)
            {
                handleRt.sizeDelta        = new Vector2(HandleWidth, HandleHeight);
                handleRt.anchorMin        = new Vector2(0f, 0.5f);
                handleRt.anchorMax        = new Vector2(0f, 0.5f);
                handleRt.pivot            = new Vector2(0.5f, 0.5f);

                var handleImg = handle.GetComponent<Image>();
                if (handleImg != null)
                {
                    handleImg.sprite         = white;
                    handleImg.type           = Image.Type.Simple;
                    handleImg.color          = Color.white;
                    handleImg.raycastTarget  = true;
                    slider.targetGraphic     = handleImg;
                    slider.handleRect        = handleRt;
                }

                if (handleLabel == null)
                    handleLabel = handle.GetComponentInChildren<TMP_Text>(true);

                if (handleLabel != null)
                {
                    ApplyMenuFont(rowLabel, handleLabel, copySharedMaterial: false);
                    ApplyHandleLabelStyle(handleLabel);
                }
            }

            if (rowLabel != null)
            {
                rowLabel.fontSize     = LabelFontSize;
                rowLabel.fontStyle    = FontStyles.Normal;
                rowLabel.overflowMode = TextOverflowModes.Overflow;
            }

            var sliderLe = slider.GetComponent<LayoutElement>();
            if (sliderLe != null)
            {
                sliderLe.minHeight       = HandleHeight;
                sliderLe.preferredHeight = HandleHeight;
            }

            slider.transition = Selectable.Transition.None;
        }

        public static void ApplyHandleLabelStyle(TMP_Text handleLabel)
        {
            if (handleLabel == null)
                return;

            handleLabel.color              = HandleLabelColor;
            handleLabel.fontSize           = LabelFontSize;
            handleLabel.fontStyle          = FontStyles.Bold;
            handleLabel.alignment          = TextAlignmentOptions.Center;
            handleLabel.enableWordWrapping = false;
            handleLabel.overflowMode       = TextOverflowModes.Overflow;
            handleLabel.margin             = Vector4.zero;
        }

        public static void ApplyRowLabelStyle(TMP_Text tmp, TMP_Text fontSource = null)
        {
            if (tmp == null)
                return;

            tmp.text               = BotThinkLabelText;
            tmp.fontSize           = LabelFontSize;
            tmp.fontStyle          = FontStyles.Normal;
            tmp.color              = UiColors.PotGold;
            tmp.alignment          = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            ApplyMenuFont(fontSource, tmp);
        }

        public static void ApplyMenuFont(TMP_Text source, TMP_Text target, bool copySharedMaterial = true)
        {
            if (target == null)
                return;

            TMP_Text from = source;
            if (from == null || from.font == null)
                from = target.transform.root.GetComponentInChildren<TMP_Text>(true);

            if (from == null || from.font == null)
                return;

            target.font              = from.font;
            target.enableAutoSizing  = false;

            if (copySharedMaterial)
                target.fontSharedMaterial = from.fontSharedMaterial;
            else if (from.font.material != null)
                target.fontSharedMaterial = from.font.material;
        }

        public static string FormatHandleValueMs(int milliseconds)
        {
            if (milliseconds <= 0)
                return "0";

            return milliseconds + "ms";
        }

        public static string FormatHandleValueSims(int simulations)
        {
            if (simulations >= 1_000_000 && simulations % 1_000_000 == 0)
                return simulations / 1_000_000 + "M";

            if (simulations >= 10_000 && simulations % 1_000 == 0)
                return simulations / 1_000 + "k";

            return simulations.ToString();
        }

        public static void ApplyEquityRowLabelStyle(TMP_Text tmp, TMP_Text fontSource = null)
        {
            if (tmp == null)
                return;

            tmp.text               = EquitySimsLabelText;
            tmp.fontSize           = LabelFontSize;
            tmp.fontStyle          = FontStyles.Normal;
            tmp.color              = UiColors.PotGold;
            tmp.alignment          = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            ApplyMenuFont(fontSource, tmp);
        }

        private static void SetCenteredBar(RectTransform rt, float height)
        {
            rt.anchorMin        = new Vector2(0f, 0.5f);
            rt.anchorMax        = new Vector2(1f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void InsetTrackLeft(RectTransform rt)
        {
            rt.offsetMin = new Vector2(TrackLeftInset, rt.offsetMin.y);
        }
    }
}
