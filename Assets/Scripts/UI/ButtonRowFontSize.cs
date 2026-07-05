using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Applies GameManager button row width, height, and font size to every child button.</summary>
    [ExecuteAlways]
    public class ButtonRowFontSize : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private float minFontSize = 8f;

        private void OnValidate() => Apply();
        private void OnEnable() => Apply();

        public void Apply()
        {
            GameManager gm = ResolveGameManager();
            if (gm == null) return;

            ApplyWithDimensions(gm.ButtonWidth, gm.ButtonHeight, gm.ButtonFontSize);
        }

        /// <summary>Widens visible betting buttons so amount labels (e.g. Call 1.000) fit at full font size.</summary>
        public void FitActiveButtons(float baseWidth, float buttonHeight, float fontSize, IReadOnlyList<Button> buttons)
        {
            if (buttons == null || buttons.Count == 0)
            {
                ApplyWithDimensions(baseWidth, buttonHeight, fontSize);
                return;
            }

            const float horizontalPadding = 28f;
            float targetWidth = baseWidth;

            foreach (Button button in buttons)
            {
                if (button == null || !button.gameObject.activeSelf)
                    continue;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label == null || label.GetComponentInParent<ActionAmountBadge>() != null)
                    continue;

                label.enableAutoSizing = false;
                label.overflowMode     = TextOverflowModes.Overflow;
                label.fontSize         = fontSize;

                float textWidth = label.GetPreferredValues(label.text).x + horizontalPadding;
                if (textWidth > targetWidth)
                    targetWidth = textWidth;
            }

            ConfigureRowLayout(buttonHeight);

            foreach (Button button in buttons)
            {
                if (button == null || !button.gameObject.activeSelf)
                    continue;

                if (button.transform is RectTransform rt)
                {
                    ApplyButtonDimensions(rt, targetWidth, buttonHeight);
                    ApplyColumnWidth(button.transform.parent, targetWidth);
                }
            }

            foreach (Button button in buttons)
            {
                if (button == null || !button.gameObject.activeSelf)
                    continue;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null && label.GetComponentInParent<ActionAmountBadge>() == null)
                    label.fontSize = fontSize;
            }

            Canvas.ForceUpdateCanvases();
        }

        private void ApplyWithDimensions(float buttonWidth, float buttonHeight, float fontSize)
        {
            ConfigureRowLayout(buttonHeight);

            foreach (Transform child in transform)
            {
                if (child is not RectTransform rt)
                    continue;

                if (child.GetComponent<VerticalLayoutGroup>() != null)
                    continue;

                ApplyButtonDimensions(rt, buttonWidth, buttonHeight);
            }

            ApplyLabelFontSize(fontSize, buttonWidth);
        }

        private GameManager ResolveGameManager()
        {
            if (gameManager != null) return gameManager;

#if UNITY_2022_2_OR_NEWER
            return FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
#else
            return FindObjectOfType<GameManager>();
#endif
        }

        private void ConfigureRowLayout(float buttonHeight)
        {
            var rowElement = GetComponent<LayoutElement>();
            if (rowElement != null)
                rowElement.preferredHeight = buttonHeight;

            var hlg = GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) return;

            hlg.childAlignment         = TextAnchor.LowerCenter;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
        }

        private void ApplyButtonDimensions(float buttonWidth, float buttonHeight)
        {
            foreach (Transform child in transform)
            {
                if (child is not RectTransform rt)
                    continue;

                if (child.GetComponent<VerticalLayoutGroup>() != null)
                    continue;

                ApplyButtonDimensions(rt, buttonWidth, buttonHeight);
            }
        }

        private static void ApplyButtonDimensions(RectTransform rt, float buttonWidth, float buttonHeight)
        {
            if (rt == null)
                return;

            rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            var element = rt.GetComponent<LayoutElement>()
                       ?? rt.gameObject.AddComponent<LayoutElement>();

            element.ignoreLayout     = false;
            element.minWidth         = buttonWidth;
            element.preferredWidth   = buttonWidth;
            element.minHeight        = buttonHeight;
            element.preferredHeight  = buttonHeight;
            element.flexibleWidth    = -1f;
            element.flexibleHeight   = -1f;
        }

        private static void ApplyColumnWidth(Transform column, float buttonWidth)
        {
            if (column == null || buttonWidth <= 0f)
                return;

            if (column.GetComponent<VerticalLayoutGroup>() == null)
                return;

            var element = column.GetComponent<LayoutElement>()
                       ?? column.gameObject.AddComponent<LayoutElement>();

            element.minWidth       = buttonWidth;
            element.preferredWidth = buttonWidth;
        }

        private void ApplyLabelFontSize(float fontSize, float buttonWidth)
        {
            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(includeInactive: true);
            if (labels.Length == 0) return;

            foreach (TMP_Text label in labels)
            {
                if (label.GetComponentInParent<ActionAmountBadge>() != null)
                    continue;

                label.enableAutoSizing = false;
                label.overflowMode     = TextOverflowModes.Overflow;
                label.fontSize         = fontSize;
            }

            Canvas.ForceUpdateCanvases();

            float minScaleFactor = 1f;
            foreach (TMP_Text label in labels)
            {
                if (label.GetComponentInParent<ActionAmountBadge>() != null)
                    continue;

                float containerWidth = label.rectTransform.rect.width;
                if (containerWidth <= 0f) continue;

                float textWidth = label.GetPreferredValues().x;
                if (textWidth > containerWidth)
                {
                    float scale = containerWidth / textWidth;
                    if (scale < minScaleFactor)
                        minScaleFactor = scale;
                }
            }

            float finalSize = Mathf.Max(minFontSize, fontSize * minScaleFactor);
            foreach (TMP_Text label in labels)
            {
                if (label.GetComponentInParent<ActionAmountBadge>() != null)
                    continue;

                label.fontSize = finalSize;
            }
        }
    }
}
