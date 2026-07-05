using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Button row layout for the action panel.</summary>
    public static class ActionPanelLayout
    {
        private const string ButtonRowName = "ButtonRow";

        public const string CheckCallColumnName = "CheckCallColumn";
        public const string AllInColumnName     = "AllInColumn";
        public const string FoldColumnName      = "FoldColumn";
        public const string BelowSpacerName     = "BelowSpacer";

        public static float BelowButtonSlotHeight =>
            ActionAmountBadge.BadgeHeight + RaiseInputBuilder.ColumnSpacing;

        private const float ButtonRowHeight = 50f;
        private const float PanelHeight     = 94f;

        public static bool IsButtonColumn(string columnName) =>
            columnName == CheckCallColumnName
            || columnName == AllInColumnName
            || columnName == FoldColumnName
            || columnName == RaiseInputBuilder.RaiseColumnName;

        public static void ConfigureRowAlignment(Transform buttonRow)
        {
            if (buttonRow == null)
                return;

            if (buttonRow.TryGetComponent(out HorizontalLayoutGroup hlg))
                hlg.childAlignment = TextAnchor.LowerCenter;
        }

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
                ConfigureRowAlignment(existingRow);
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
            if (buttonRow != null && IsButtonColumn(buttonRow.name))
                buttonRow = buttonRow.parent;

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
            Transform spacer   = EnsureBelowSpacer(column);
            input.transform.SetParent(spacer, false);
            input.transform.SetAsFirstSibling();
            raiseButton.transform.SetSiblingIndex(1);

            RaiseInputBuilder.ConfigureInputLayoutElement(input);
            RaiseInputBuilder.ApplyButtonBackground(input, raiseButton);

            float buttonHeight = raiseButton.transform is RectTransform raiseRt
                ? raiseRt.sizeDelta.y
                : ButtonRowHeight;
            SyncRaiseColumn(raiseButton, input, inputVisible: false, buttonHeight, belowSlotHeight: 0f);

            return input;
        }

        public static Transform EnsureRaiseColumn(Button raiseButton, Transform buttonRow)
            => EnsureButtonColumn(raiseButton, buttonRow, RaiseInputBuilder.RaiseColumnName);

        public static Transform EnsurePlainButtonColumn(Button button, Transform buttonRow, string columnName)
            => EnsureButtonColumn(button, buttonRow, columnName);

        public static Transform EnsureButtonColumn(Button button, Transform buttonRow, string columnName)
        {
            if (button == null || buttonRow == null)
                return null;

            Transform existingColumn = button.transform.parent;
            if (existingColumn != null && existingColumn.name == columnName)
            {
                ApplyBottomAlignedColumn(existingColumn);
                EnsureBelowSpacer(existingColumn);
                button.transform.SetSiblingIndex(1);
                return existingColumn;
            }

            int index = button.transform.GetSiblingIndex();
            if (buttonRow == button.transform.parent)
                index = button.transform.GetSiblingIndex();
            else if (existingColumn != null && existingColumn.parent == buttonRow)
                index = existingColumn.GetSiblingIndex();

            var columnGo = new GameObject(columnName, typeof(RectTransform));
            Transform column = columnGo.transform;
            column.SetParent(buttonRow, false);
            column.SetSiblingIndex(index);

            button.transform.SetParent(column, false);
            ApplyBottomAlignedColumn(column);
            EnsureBelowSpacer(column);
            button.transform.SetSiblingIndex(1);

            var colElement = columnGo.AddComponent<LayoutElement>();
            colElement.flexibleWidth = 0f;

            return column;
        }

        public static Transform EnsureBelowSpacer(Transform column)
        {
            if (column == null)
                return null;

            Transform slot = column.Find(BelowSpacerName);
            if (slot != null)
                return slot;

            var go = new GameObject(BelowSpacerName, typeof(RectTransform));
            slot = go.transform;
            slot.SetParent(column, false);
            slot.SetSiblingIndex(0);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight        = 0f;
            le.preferredHeight  = 0f;
            le.flexibleHeight   = 0f;
            le.flexibleWidth    = 0f;

            return slot;
        }

        public static void SyncPlainButtonColumn(Button button, float buttonHeight, float belowSlotHeight, float buttonWidth = 0f)
        {
            if (button == null)
                return;

            SyncColumnLayout(button.transform.parent, buttonHeight, belowSlotHeight, buttonWidth);
        }

        public static void SyncAmountBadgeColumn(
            Button button,
            ActionAmountBadge badge,
            bool badgeVisible,
            float buttonHeight,
            float belowSlotHeight,
            float buttonWidth = 0f)
        {
            if (button == null)
                return;

            Transform column = button.transform.parent;
            if (column == null)
                return;

            Transform spacer = EnsureBelowSpacer(column);
            ApplyBottomAlignedColumn(column);
            button.transform.SetSiblingIndex(1);

            if (badge != null)
            {
                badge.transform.SetParent(spacer, false);
                badge.transform.SetAsFirstSibling();

                if (badgeVisible)
                    badge.gameObject.SetActive(true);
                else
                    badge.Hide();
            }

            if (buttonWidth <= 0f && button.transform is RectTransform buttonRect)
                buttonWidth = buttonRect.sizeDelta.x;

            if (badge != null && badgeVisible && buttonWidth > 0f && badge.transform is RectTransform badgeRt)
                badgeRt.sizeDelta = new Vector2(buttonWidth, ActionAmountBadge.BadgeHeight);

            SyncColumnLayout(column, buttonHeight, belowSlotHeight, buttonWidth);
        }

        public static void SyncRaiseColumn(
            Button raiseButton,
            TMP_InputField input,
            bool inputVisible,
            float buttonHeight,
            float belowSlotHeight,
            float buttonWidth = 0f)
        {
            if (raiseButton == null)
                return;

            Transform column = raiseButton.transform.parent;
            if (column == null || column.name != RaiseInputBuilder.RaiseColumnName)
                return;

            ApplyBottomAlignedColumn(column);
            Transform spacer = EnsureBelowSpacer(column);
            raiseButton.transform.SetSiblingIndex(1);

            if (input != null)
            {
                input.transform.SetParent(spacer, false);
                input.transform.SetAsFirstSibling();
                input.gameObject.SetActive(inputVisible);
                RaiseInputBuilder.ApplyButtonBackground(input, raiseButton);

                if (buttonWidth <= 0f && raiseButton.transform is RectTransform raiseRt)
                    buttonWidth = raiseRt.sizeDelta.x;

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

            float slotHeight = belowSlotHeight;
            SyncColumnLayout(column, buttonHeight, slotHeight, buttonWidth);
        }

        public static void RebuildPanel(RectTransform panel)
        {
            if (panel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        private static void ApplyBottomAlignedColumn(Transform column)
        {
            if (column == null)
                return;

            var vlg = column.GetComponent<VerticalLayoutGroup>()
                   ?? column.gameObject.AddComponent<VerticalLayoutGroup>();

            vlg.spacing                = RaiseInputBuilder.ColumnSpacing;
            vlg.childAlignment         = TextAnchor.LowerCenter;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
        }

        private static void SyncColumnLayout(Transform column, float buttonHeight, float belowSlotHeight, float buttonWidth)
        {
            if (column == null)
                return;

            ApplyBottomAlignedColumn(column);
            SetLayoutElementHeight(EnsureBelowSpacer(column), belowSlotHeight);

            float columnHeight = buttonHeight + belowSlotHeight;
            var colElement = column.GetComponent<LayoutElement>()
                          ?? column.gameObject.AddComponent<LayoutElement>();

            colElement.minHeight       = columnHeight;
            colElement.preferredHeight = columnHeight;

            if (buttonWidth > 0f)
            {
                colElement.minWidth       = buttonWidth;
                colElement.preferredWidth = buttonWidth;
            }

            Transform buttonRow = column.parent;
            if (buttonRow != null)
            {
                var rowElement = buttonRow.GetComponent<LayoutElement>();
                if (rowElement != null)
                    rowElement.preferredHeight = Mathf.Max(buttonHeight, columnHeight);

                if (buttonRow is RectTransform rowRt)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rowRt);
            }
        }

        private static void SetLayoutElementHeight(Transform target, float height)
        {
            if (target == null)
                return;

            var le = target.GetComponent<LayoutElement>()
                  ?? target.gameObject.AddComponent<LayoutElement>();

            le.minHeight       = height;
            le.preferredHeight = height;
            le.flexibleHeight  = 0f;
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
