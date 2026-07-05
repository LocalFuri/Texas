using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Builds a TMP_InputField for raise amount entry under the Raise button.</summary>
    public static class RaiseInputBuilder
    {
        public const string RaiseRowName    = "RaiseRow";
        public const string RaiseColumnName = "RaiseColumn";
        public const string RaiseInputName  = "RaiseInput";

        public const float InputHeight      = 28f;
        public const float ColumnSpacing    = 4f;

        /// <summary>Finds an existing raise input on the panel or creates one.</summary>
        public static TMP_InputField Ensure(Transform actionPanel, TMP_FontAsset font)
        {
            if (actionPanel == null) return null;

            Transform legacyRow = actionPanel.Find(RaiseRowName);
            if (legacyRow != null)
            {
                var existing = legacyRow.GetComponentInChildren<TMP_InputField>(true);
                if (existing != null)
                    return existing;
            }

            TMP_InputField underColumn = FindUnderRaiseColumn(actionPanel);
            if (underColumn != null)
                return underColumn;

            return CreateInputField(actionPanel, font);
        }

        public static TMP_InputField FindUnderRaiseColumn(Transform actionPanel)
        {
            if (actionPanel == null) return null;

            Transform column = actionPanel.Find($"ButtonRow/{RaiseColumnName}");
            return column != null
                ? column.GetComponentInChildren<TMP_InputField>(true)
                : null;
        }

        public static TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font)
        {
            var root = new GameObject(RaiseInputName, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var image = root.AddComponent<Image>();
            image.color         = Color.white;
            image.raycastTarget = true;

            var input = root.AddComponent<TMP_InputField>();
            input.contentType          = TMP_InputField.ContentType.IntegerNumber;
            input.lineType             = TMP_InputField.LineType.SingleLine;
            input.characterValidation  = TMP_InputField.CharacterValidation.Integer;

            var textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(root.transform, false);
            var textAreaRt = (RectTransform)textArea.transform;
            StretchFull(textAreaRt);
            textArea.AddComponent<RectMask2D>();

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textArea.transform, false);
            StretchFull((RectTransform)placeholderGo.transform);
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text               = "40";
            placeholder.font               = font;
            placeholder.fontSize           = 16f;
            placeholder.color              = new Color(1f, 1f, 1f, 0.4f);
            placeholder.fontStyle          = FontStyles.Italic;
            placeholder.alignment          = TextAlignmentOptions.Midline;
            placeholder.enableWordWrapping = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(textArea.transform, false);
            StretchFull((RectTransform)textGo.transform);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font               = font;
            text.fontSize           = 16f;
            text.color              = ButtonLabelStyle.RaiseText;
            text.alignment          = TextAlignmentOptions.Midline;
            text.enableWordWrapping = false;

            input.textViewport  = textAreaRt;
            input.textComponent = text;
            input.placeholder   = placeholder;
            input.targetGraphic = image;
            input.interactable  = true;
            input.readOnly      = false;

            ConfigureInputLayoutElement(input);

            if (font != null)
                ApplyTextStyle(input, font, ButtonLabelStyle.ActionButtonFontSize);

            return input;
        }

        public static void ApplyTextStyle(TMP_InputField input, TMP_FontAsset font, float fontSize)
        {
            if (input == null || font == null)
                return;

            var placeholderColor = new Color(
                ButtonLabelStyle.RaiseText.r,
                ButtonLabelStyle.RaiseText.g,
                ButtonLabelStyle.RaiseText.b,
                0.45f);

            if (input.textComponent != null)
            {
                input.textComponent.font = font;
                ButtonLabelStyle.Apply(input.textComponent, ButtonLabelStyle.RaiseText, fontSize);
                input.textComponent.alignment = TextAlignmentOptions.Midline;
            }

            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.font = font;
                ButtonLabelStyle.Apply(placeholder, placeholderColor, fontSize);
                placeholder.alignment = TextAlignmentOptions.Midline;
            }

            input.pointSize = fontSize;
            ApplySelectionStyle(input);
            ConfigureInputLayoutElement(input, ResolveInputHeight(fontSize));
        }

        public static void ApplySelectionStyle(TMP_InputField input)
        {
            if (input == null)
                return;

            input.selectionColor = new Color(
                ButtonLabelStyle.RaiseText.r,
                ButtonLabelStyle.RaiseText.g,
                ButtonLabelStyle.RaiseText.b,
                0.2f);
            input.caretColor = ButtonLabelStyle.RaiseText;
        }

        public static float ResolveInputHeight(float fontSize) =>
            Mathf.Max(InputHeight, fontSize + 6f);

        public static void ConfigureInputLayoutElement(TMP_InputField input, float height = 0f)
        {
            if (input == null) return;

            float resolvedHeight = height > 0f ? height : InputHeight;

            var element = input.GetComponent<LayoutElement>()
                       ?? input.gameObject.AddComponent<LayoutElement>();
            element.minHeight        = resolvedHeight;
            element.preferredHeight  = resolvedHeight;
            element.flexibleWidth    = 1f;
            element.flexibleHeight   = 0f;
        }

        public static void ApplyButtonBackground(TMP_InputField input, Button raiseButton)
        {
            if (input == null || raiseButton == null) return;

            var src = raiseButton.GetComponent<Image>();
            if (src == null || input.targetGraphic is not Image dst) return;

            dst.sprite          = src.sprite;
            dst.color           = src.color;
            dst.type            = src.type;
            dst.preserveAspect  = src.preserveAspect;
            dst.raycastTarget   = true;
        }

        /// <summary>Resets legacy scene rects so text/placeholder render inside the field.</summary>
        public static void NormalizeInputLayout(TMP_InputField input, float width = 0f, float fontSize = 0f)
        {
            if (input == null)
                return;

            float height = fontSize > 0f ? ResolveInputHeight(fontSize) : InputHeight;

            if (input.transform is RectTransform inputRt)
            {
                inputRt.anchorMin        = new Vector2(0.5f, 0.5f);
                inputRt.anchorMax        = new Vector2(0.5f, 0.5f);
                inputRt.pivot            = new Vector2(0.5f, 0.5f);
                inputRt.anchoredPosition = Vector2.zero;

                float resolvedWidth = width > 0f
                    ? width
                    : inputRt.sizeDelta.x > 0f ? inputRt.sizeDelta.x : 120f;

                inputRt.sizeDelta = new Vector2(resolvedWidth, height);
            }

            if (input.textViewport != null)
                StretchFull(input.textViewport);

            if (input.textComponent != null && input.textComponent.transform is RectTransform textRt)
                StretchFull(textRt);

            if (input.placeholder is TMP_Text placeholder && placeholder.transform is RectTransform placeholderRt)
                StretchFull(placeholderRt);

            ConfigureInputLayoutElement(input, height);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 2f);
            rt.offsetMax = new Vector2(-6f, -2f);
        }
    }
}
