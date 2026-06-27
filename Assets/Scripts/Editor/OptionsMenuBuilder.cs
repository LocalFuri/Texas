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
    private const float PanelWidth    = 248f;
    private const float RowHeight     = 20f;
    private const float RowSpacing    = 3f;
    private const float CheckboxSize  = 15f;
    private const float PanelPadding  = 10f;

    private static readonly Color PanelBg     = new Color(0.020f, 0.173f, 0.020f, 1f);
    private static readonly Color LabelColor  = new Color(0.902f, 0.839f, 0.306f, 1f);

    private static readonly string[] OptionLabels =
    {
        "Autoplay",
        "Autoplay at max speed",
        "Dealer BJ",
        "Blackjack Test",
        "BJ All Test",
        "Double Down Test",
    };

    private static float PanelHeight =>
        PanelPadding * 2f
        + OptionLabels.Length * RowHeight
        + (OptionLabels.Length - 1) * RowSpacing;

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
        if (rootTransform != null)
        {
            panel = rootTransform.gameObject;
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
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(PanelWidth, PanelHeight);

        var bg = GetOrAdd<Image>(panel);
        bg.color         = PanelBg;
        bg.raycastTarget = true;
        OptionsMenuToggleStyle.EnsurePanelBackgroundSprite(bg);

        var canvasGroup = GetOrAdd<CanvasGroup>(panel);

        var vl                    = GetOrAdd<VerticalLayoutGroup>(panel);
        vl.padding                = new RectOffset(
            (int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
        vl.spacing                = RowSpacing;
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

        var so = new SerializedObject(menuComp);
        so.Update();
        so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("_menuToggleClip").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Pop04.wav");
        so.FindProperty("_menuToggleVolume").floatValue = 0.35f;

        var togglesProp = so.FindProperty("_toggles");
        togglesProp.arraySize = OptionLabels.Length;

        for (int i = 0; i < OptionLabels.Length; i++)
        {
            var toggle = AddToggleRow(panel.transform, OptionLabels[i]);
            togglesProp.GetArrayElementAtIndex(i).objectReferenceValue = toggle;
        }

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(menuComp);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[OptionsMenuBuilder] Compact centered options menu built (6 rows).");
    }

    /// <summary>Batch-mode entry point for rebuilding TexasScene without opening the Editor UI.</summary>
    public static void BuildFromCommandLine()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TexasScene.unity");
        Build();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static Toggle AddToggleRow(Transform parent, string label)
    {
        var row = CreateRect("Row_" + label, parent);
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

#if UNITY_EDITOR
        OptionsMenuToggleStyle.CacheSprites(
            Resources.Load<Sprite>("UI/OptionsCheckbox"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/OptionsCheckBold.png")
                ?? Resources.Load<Sprite>("UI/OptionsCheckBold"));
#endif
        OptionsMenuToggleStyle.Apply(toggle);

        toggle.isOn = false;

        var labelGo = CreateRect("Label", row.transform);
        var labelLe = GetOrAdd<LayoutElement>(labelGo);
        labelLe.flexibleWidth = 1f;
        labelLe.minHeight     = RowHeight;
        labelLe.preferredHeight = RowHeight;

        var tmp              = GetOrAdd<TextMeshProUGUI>(labelGo);
        tmp.text               = label;
        tmp.fontSize           = 14f;
        tmp.fontStyle          = FontStyles.Normal;
        tmp.color              = LabelColor;
        tmp.alignment        = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode     = TextOverflowModes.Overflow;

        return toggle;
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
