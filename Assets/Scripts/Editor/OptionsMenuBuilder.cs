using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TexasHoldem;

/// <summary>
/// Builds a compact centered debug options list (checkbox + yellow label rows).
/// Run via Texas Holdem > Apply Options Menu Layout or Tools > Texas Hold'em > Build Options Menu.
/// </summary>
public static class OptionsMenuBuilder
{
    private const float RowHeight     = 20f;
    private const float DefaultRowSpacing = 3f;
    private const float CheckboxSize  = 15f;
    private const float PanelPadding  = 10f;
    private const int   BotThinkRowIndex = 1;
    private const int   EquitySimsRowIndex = 6;
    private const float SliderTrackHeight = 10f;
    private const float SliderHandleSize  = 16f;

    private static readonly Color PanelBg     = new Color(0.020f, 0.173f, 0.020f, 1f);
    private static readonly Color LabelColor  = UiColors.PotGold;

    private static readonly string[] OptionLabels =
    {
        "Restart",
        "Bots think",
        "Dealer BJ",
        "Blackjack Test",
        "BJ All Test",
        "Double Down Test",
        "Equity sims",
    };

    private static float PanelHeight(float rowSpacing) =>
        PanelPadding * 2f
        + OptionLabels.Length * RowHeight
        + (OptionLabels.Length - 1) * rowSpacing;

    [MenuItem("Texas Holdem/Apply Options Menu Layout")]
    [MenuItem("Tools/Texas Hold'em/Build Options Menu")]
    public static void Build()
    {
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[OptionsMenuBuilder] No Canvas found in the scene.");
            return;
        }

        var rootTransform = canvas.transform.Find("OptionsMenu_Panel");
        GameObject panel;
        Vector2 panelPos = Vector2.zero;
        float rowSpacing = DefaultRowSpacing;
        if (rootTransform != null)
        {
            panel = rootTransform.gameObject;
            var existingMenu = panel.GetComponent<OptionsMenu>();
            if (existingMenu != null)
            {
                var existingSo = new SerializedObject(existingMenu);
                panelPos = new Vector2(
                    existingSo.FindProperty("_panelPosX").floatValue,
                    existingSo.FindProperty("_panelPosY").floatValue);
                rowSpacing = existingSo.FindProperty("_rowSpacing").floatValue;
            }
            else
                panelPos = ((RectTransform)panel.transform).anchoredPosition;

            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(panel.transform.GetChild(i).gameObject);
        }
        else
        {
            panel = CreateRect("OptionsMenu_Panel", canvas.transform);
        }

        Undo.RecordObject(panel, "Build Options Menu");
        panel.transform.SetAsLastSibling();

        var rt = (RectTransform)panel.transform;
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = panelPos;
        rt.sizeDelta        = new Vector2(OptionsMenu.PanelWidth, PanelHeight(rowSpacing));

        var bg = GetOrAdd<Image>(panel);
        bg.color         = PanelBg;
        bg.raycastTarget = true;
        OptionsMenuToggleStyle.EnsurePanelBackgroundSprite(bg);

        var canvasGroup = GetOrAdd<CanvasGroup>(panel);

        var vl                    = GetOrAdd<VerticalLayoutGroup>(panel);
        vl.padding                = new RectOffset(
            (int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
        vl.spacing                = rowSpacing;
        vl.childAlignment         = TextAnchor.UpperLeft;
        vl.childControlWidth      = true;
        vl.childControlHeight     = true;
        vl.childForceExpandWidth  = false;
        vl.childForceExpandHeight = false;

        var menuComp = panel.GetComponent<OptionsMenu>();
        if (menuComp == null)
            menuComp = Undo.AddComponent<OptionsMenu>(panel);

        var audioSource = GetOrAdd<AudioSource>(panel);
        audioSource.playOnAwake = false;

#if UNITY_EDITOR
        OptionsMenuToggleStyle.CacheSprites(
            Resources.Load<Sprite>("UI/OptionsCheckbox"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/OptionsCheckBold.png")
                ?? Resources.Load<Sprite>("UI/OptionsCheckBold"));
#endif

        var so = new SerializedObject(menuComp);
        so.Update();
        so.FindProperty("_panelPosX").floatValue = panelPos.x;
        so.FindProperty("_panelPosY").floatValue = panelPos.y;
        so.FindProperty("_rowSpacing").floatValue = rowSpacing;
        so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("_menuToggleClip").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Pop04.wav");
        so.FindProperty("_menuToggleVolume").floatValue = 0.35f;

        var togglesProp = so.FindProperty("_toggles");
        togglesProp.arraySize = OptionLabels.Length;

        Slider botThinkSlider = null;
        Slider equitySimsSlider = null;
        for (int i = 0; i < OptionLabels.Length; i++)
        {
            if (i == BotThinkRowIndex)
            {
                botThinkSlider = AddBotThinkSliderRow(panel.transform, OptionLabels[i]);
                togglesProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
            }
            else if (i == EquitySimsRowIndex)
            {
                equitySimsSlider = AddEquitySimsSliderRow(panel.transform, OptionLabels[i]);
                togglesProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
            }
            else
            {
                Toggle toggle = AddToggleRow(panel.transform, OptionLabels[i]);
                togglesProp.GetArrayElementAtIndex(i).objectReferenceValue = toggle;
            }
        }

        so.FindProperty("_botThinkSlider").objectReferenceValue = botThinkSlider;
        so.FindProperty("_equitySimsSlider").objectReferenceValue = equitySimsSlider;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(menuComp);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[OptionsMenuBuilder] Compact centered options menu built (7 rows).");
    }

    /// <summary>Batch-mode entry point for rebuilding TexasScene without opening the Editor UI.</summary>
    public static void BuildFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TexasScene.unity");
        Build();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static Slider AddBotThinkSliderRow(Transform parent, string label)
        => OptionsMenuRowFactory.CreateBotThinkSliderRow(parent, OptionsMenu.SecondsToSliderValue(OptionsMenu.BotThinkDefaultSeconds));

    private static Slider AddEquitySimsSliderRow(Transform parent, string label)
        => OptionsMenuRowFactory.CreateEquitySimsSliderRow(parent, OptionsMenu.EquitySimsToSliderValue(OptionsMenu.EquitySimsDefault));

    private static Toggle AddToggleRow(Transform parent, string label)
    {
        var row = CreateRect("Row_" + label, parent);
        AddRowLayout(row);

        var toggleGo = CreateRect("Toggle", row.transform);
        var toggleLe = GetOrAdd<LayoutElement>(toggleGo);
        toggleLe.minWidth = toggleLe.preferredWidth = CheckboxSize;
        toggleLe.minHeight = toggleLe.preferredHeight = CheckboxSize;

        var toggleRt = (RectTransform)toggleGo.transform;
        toggleRt.sizeDelta = new Vector2(CheckboxSize, CheckboxSize);

        var toggle = GetOrAdd<Toggle>(toggleGo);

        var bgGo = CreateRect("Background", toggleGo.transform);
        Stretch((RectTransform)bgGo.transform);
        GetOrAdd<Image>(bgGo);

        var checkGo = CreateRect("Checkmark", bgGo.transform);
        var checkRt = (RectTransform)checkGo.transform;
        checkRt.anchorMin        = Vector2.zero;
        checkRt.anchorMax        = Vector2.one;
        checkRt.sizeDelta        = Vector2.zero;
        checkRt.anchoredPosition = Vector2.zero;
        GetOrAdd<Image>(checkGo);

        OptionsMenuToggleStyle.Apply(toggle);

        toggle.isOn = false;

        var labelGo = CreateRect("Label", row.transform);
        var labelLe = GetOrAdd<LayoutElement>(labelGo);
        labelLe.flexibleWidth = 1f;
        labelLe.minHeight     = RowHeight;
        labelLe.preferredHeight = RowHeight;

        var tmp = GetOrAdd<TextMeshProUGUI>(labelGo);
        StyleRowLabel(tmp, label);

        return toggle;
    }

    private static void AddRowLayout(GameObject row)
    {
        var rowLe = GetOrAdd<LayoutElement>(row);
        rowLe.preferredHeight = RowHeight;
        rowLe.minHeight       = RowHeight;

        var hl                    = GetOrAdd<HorizontalLayoutGroup>(row);
        hl.childAlignment         = TextAnchor.MiddleLeft;
        hl.spacing                = 8f;
        hl.childControlWidth      = true;
        hl.childControlHeight     = true;
        hl.childForceExpandWidth  = false;
        hl.childForceExpandHeight = false;
    }

    private static void StyleRowLabel(TextMeshProUGUI tmp, string text)
    {
        tmp.text               = text;
        tmp.fontSize           = 14f;
        tmp.fontStyle          = FontStyles.Normal;
        tmp.color              = LabelColor;
        tmp.alignment          = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Overflow;
    }

    private static void SetCenteredBar(RectTransform rt, float height)
    {
        rt.anchorMin        = new Vector2(0f, 0.5f);
        rt.anchorMax        = new Vector2(1f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
    }

    private static GameObject CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Options Menu");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
