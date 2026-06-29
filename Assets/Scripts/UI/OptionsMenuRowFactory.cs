using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Creates options-menu row UI (shared by editor builder and runtime repair).</summary>
    public static class OptionsMenuRowFactory
    {
        public const float RowHeight    = 24f;
        private const float CheckboxSize = 15f;
        private static readonly Color LabelColor = new Color(0.902f, 0.839f, 0.306f, 1f);

        public static Slider CreateBotThinkSliderRow(Transform panel, float initialValue)
        {
            string labelText = OptionsMenuSliderStyle.BotThinkLabelText;
            var row = CreateRect("Row_" + labelText, panel);
            AddRowLayout(row);

            var iconGo = CreateRect("Icon", row.transform);
            var iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.minWidth = iconLe.preferredWidth = CheckboxSize;
            iconLe.minHeight = iconLe.preferredHeight = CheckboxSize;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite        = OptionsMenuToggleStyle.GetCheckboxSprite();
            iconImg.color         = Color.white;
            iconImg.raycastTarget = false;

            var labelGo = CreateRect("Label", row.transform);
            var labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.flexibleWidth   = 0f;
            labelLe.minWidth        = OptionsMenuSliderStyle.LabelWidth;
            labelLe.preferredWidth  = OptionsMenuSliderStyle.LabelWidth;
            labelLe.minHeight       = RowHeight;
            labelLe.preferredHeight = RowHeight;
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            StyleRowLabel(labelTmp, labelText, ResolveMenuFont(panel));

            var sliderGo = CreateRect("BotThinkSlider", row.transform);
            var sliderLe = sliderGo.AddComponent<LayoutElement>();
            sliderLe.flexibleWidth   = 1f;
            sliderLe.minWidth        = 72f;
            sliderLe.minHeight       = OptionsMenuSliderStyle.HandleHeight;
            sliderLe.preferredHeight = OptionsMenuSliderStyle.HandleHeight;

            var slider = sliderGo.AddComponent<Slider>();

            var bgGo = CreateRect("Background", sliderGo.transform);
            bgGo.AddComponent<Image>();

            var fillAreaGo = CreateRect("Fill Area", sliderGo.transform);
            Stretch((RectTransform)fillAreaGo.transform);

            var fillGo = CreateRect("Fill", fillAreaGo.transform);
            Stretch((RectTransform)fillGo.transform);
            fillGo.AddComponent<Image>();

            var handleAreaGo = CreateRect("Handle Slide Area", sliderGo.transform);
            Stretch((RectTransform)handleAreaGo.transform);

            var handleGo = CreateRect("Handle", handleAreaGo.transform);
            handleGo.AddComponent<Image>();

            var handleLabelGo = CreateRect("HandleLabel", handleGo.transform);
            Stretch((RectTransform)handleLabelGo.transform);
            var handleTmp = handleLabelGo.AddComponent<TextMeshProUGUI>();
            handleTmp.text               = OptionsMenuSliderStyle.FormatHandleValue(initialValue);
            handleTmp.raycastTarget      = false;
            handleTmp.enableWordWrapping = false;

            OptionsMenuSliderStyle.ApplyMenuFont(labelTmp, handleTmp);

            slider.fillRect     = (RectTransform)fillGo.transform;
            slider.handleRect   = (RectTransform)handleGo.transform;
            slider.direction    = Slider.Direction.LeftToRight;
            slider.minValue     = OptionsMenu.BotThinkMinSeconds;
            slider.maxValue     = OptionsMenu.BotThinkMaxSeconds;
            slider.wholeNumbers = false;
            slider.value        = Mathf.Clamp(initialValue, OptionsMenu.BotThinkMinSeconds, OptionsMenu.BotThinkMaxSeconds);

            OptionsMenuSliderStyle.Apply(slider, handleTmp);

            return slider;
        }

        private static void AddRowLayout(GameObject row)
        {
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.preferredHeight = RowHeight;
            rowLe.minHeight       = RowHeight;

            var hl                    = row.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment         = TextAnchor.MiddleLeft;
            hl.spacing                = 6f;
            hl.childControlWidth      = true;
            hl.childControlHeight     = true;
            hl.childForceExpandWidth  = false;
            hl.childForceExpandHeight = false;
        }

        private static void StyleRowLabel(TextMeshProUGUI tmp, string text, TMP_Text fontSource)
        {
            tmp.text               = text;
            tmp.fontSize           = OptionsMenu.MenuRowFontSize;
            tmp.fontStyle          = FontStyles.Normal;
            tmp.color              = LabelColor;
            tmp.alignment          = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            OptionsMenuSliderStyle.ApplyMenuFont(fontSource, tmp);
        }

        private static TMP_Text ResolveMenuFont(Transform panel)
        {
            foreach (Transform child in panel)
            {
                if (!child.name.StartsWith("Row_"))
                    continue;

                var label = child.Find("Label")?.GetComponent<TMP_Text>();
                if (label != null && label.font != null)
                    return label;
            }

            return null;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
