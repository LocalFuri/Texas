using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Button row layout for the action panel.</summary>
    public static class ActionPanelLayout
    {
        private const string ButtonRowName = "ButtonRow";

        private const float ButtonRowHeight = 50f;
        private const float PanelHeight     = 94f;

        public static TMP_InputField Apply(
            GameObject actionPanel,
            Button startButton,
            Button foldButton,
            Button checkCallButton,
            Button raiseButton,
            TMP_FontAsset font = null)
        {
            if (actionPanel == null) return null;

            Transform panel = actionPanel.transform;
            Transform existingRow = panel.Find(ButtonRowName);
            if (existingRow != null)
            {
                var sizer = existingRow.GetComponent<ButtonRowFontSize>()
                            ?? existingRow.gameObject.AddComponent<ButtonRowFontSize>();
                sizer.Apply();
                EnsurePanelHeight(panel);
                return AttachRaiseInputToButton(raiseButton, panel, font);
            }

            var oldHorizontal = panel.GetComponent<HorizontalLayoutGroup>();
            RectOffset padding = oldHorizontal != null ? oldHorizontal.padding : new RectOffset(0, 0, 0, 0);

            if (oldHorizontal != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(oldHorizontal);
                else
#endif
                    Object.Destroy(oldHorizontal);
            }

            Transform buttonRow = CreateButtonRow(panel);

            Reparent(startButton, buttonRow);
            Reparent(foldButton, buttonRow);
            Reparent(checkCallButton, buttonRow);
            Reparent(raiseButton, buttonRow);

            ConfigureRowElement(buttonRow, ButtonRowHeight);

            var rowSizer = buttonRow.gameObject.GetComponent<ButtonRowFontSize>()
                        ?? buttonRow.gameObject.AddComponent<ButtonRowFontSize>();

            var vertical = panel.GetComponent<VerticalLayoutGroup>() ?? panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding                = padding;
            vertical.spacing                = 8f;
            vertical.childAlignment         = TextAnchor.MiddleCenter;
            vertical.childForceExpandWidth  = true;
            vertical.childForceExpandHeight = false;
            vertical.childControlWidth      = true;
            vertical.childControlHeight     = true;

            if (panel is RectTransform panelRect)
                EnsurePanelHeight(panel);

            rowSizer.Apply();

            return AttachRaiseInputToButton(raiseButton, panel, font);
        }

        /// <summary>Places the raise input directly under the Raise button in a vertical column.</summary>
        public static TMP_InputField AttachRaiseInputToButton(Button raiseButton, Transform actionPanel, TMP_FontAsset font)
        {
            if (raiseButton == null)
                return null;

            Transform buttonRow = raiseButton.transform.parent;
            if (buttonRow == null)
                buttonRow = actionPanel?.Find(ButtonRowName);

            if (buttonRow == null)
                return null;

            TMP_InputField input = RaiseInputBuilder.Ensure(actionPanel, font);
            if (input == null)
                input = RaiseInputBuilder.CreateInputField(buttonRow, font);

            Transform legacyRow = actionPanel?.Find(RaiseInputBuilder.RaiseRowName);
            if (legacyRow != null)
                legacyRow.gameObject.SetActive(false);

            Transform column = EnsureRaiseColumn(raiseButton, buttonRow);
            input.transform.SetParent(column, false);
            input.transform.SetSiblingIndex(1);

            RaiseInputBuilder.ConfigureInputLayoutElement(input);
            RaiseInputBuilder.ApplyButtonBackground(input, raiseButton);

            float buttonHeight = raiseButton.transform is RectTransform raiseRt
                ? raiseRt.sizeDelta.y
                : ButtonRowHeight;
            SyncRaiseColumn(raiseButton, input, visible: false, buttonHeight);

            return input;
        }

        public static Transform EnsureRaiseColumn(Button raiseButton, Transform buttonRow)
        {
            Transform existingColumn = raiseButton.transform.parent;
            if (existingColumn != null && existingColumn.name == RaiseInputBuilder.RaiseColumnName)
                return existingColumn;

            int raiseIndex = raiseButton.transform.GetSiblingIndex();

            var columnGo = new GameObject(RaiseInputBuilder.RaiseColumnName, typeof(RectTransform));
            Transform column = columnGo.transform;
            column.SetParent(buttonRow, false);
            column.SetSiblingIndex(raiseIndex);

            raiseButton.transform.SetParent(column, false);

            var vlg = columnGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing                = RaiseInputBuilder.ColumnSpacing;
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            var colElement = columnGo.AddComponent<LayoutElement>();
            colElement.flexibleWidth = 0f;

            return column;
        }

        public static void SyncRaiseColumn(Button raiseButton, TMP_InputField input, bool visible, float buttonHeight)
        {
            if (raiseButton == null)
                return;

            Transform column = raiseButton.transform.parent;
            if (column == null || column.name != RaiseInputBuilder.RaiseColumnName)
                return;

            if (input != null)
            {
                input.gameObject.SetActive(visible);
                RaiseInputBuilder.ApplyButtonBackground(input, raiseButton);

                float buttonWidth = raiseButton.transform is RectTransform raiseRt
                    ? raiseRt.sizeDelta.x
                    : 0f;

                if (buttonWidth > 0f && input.transform is RectTransform inputRt)
                {
                    var inputElement = input.GetComponent<LayoutElement>();
                    if (inputElement != null)
                    {
                        inputElement.minWidth       = buttonWidth;
                        inputElement.preferredWidth = buttonWidth;
                    }

                    inputRt.sizeDelta = new Vector2(buttonWidth, RaiseInputBuilder.InputHeight);
                }
            }

            float columnHeight = buttonHeight + (visible
                ? RaiseInputBuilder.InputHeight + RaiseInputBuilder.ColumnSpacing
                : 0f);

            var colElement = column.GetComponent<LayoutElement>()
                          ?? column.gameObject.AddComponent<LayoutElement>();

            colElement.minHeight       = columnHeight;
            colElement.preferredHeight = columnHeight;

            if (raiseButton.transform is RectTransform raiseRect)
            {
                float buttonWidth = raiseRect.sizeDelta.x;
                if (buttonWidth > 0f)
                {
                    colElement.minWidth       = buttonWidth;
                    colElement.preferredWidth = buttonWidth;
                }
            }

            Transform buttonRow = column.parent;
            var rowElement = buttonRow?.GetComponent<LayoutElement>();
            if (rowElement != null)
                rowElement.preferredHeight = Mathf.Max(buttonHeight, columnHeight);

            if (buttonRow is RectTransform rowRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRt);
        }

        public static void RebuildPanel(RectTransform panel)
        {
            if (panel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        private static void EnsurePanelHeight(Transform panel)
        {
            if (panel is not RectTransform panelRect) return;
            Vector2 size = panelRect.sizeDelta;
            if (size.y < PanelHeight)
                panelRect.sizeDelta = new Vector2(size.x, PanelHeight);
        }

        private static Transform CreateButtonRow(Transform parent)
        {
            var rowGo = new GameObject(ButtonRowName, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 10f;
            hlg.childAlignment         = TextAnchor.LowerCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth      = false;
            hlg.childControlHeight     = false;

            return rowGo.transform;
        }

        private static void ConfigureRowElement(Transform row, float height)
        {
            var element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth   = 1f;
        }

        private static void Reparent(Component component, Transform parent)
        {
            if (component == null) return;
            component.transform.SetParent(parent, false);
        }
    }
}
