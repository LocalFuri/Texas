using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    public static class ActionButtonLabelSetup
    {
        private const string Casino3DSdfPath           = "Assets/TextMesh Pro/Fonts/Casino3D SDF.asset";
        private const string Casino3DSdfResourcesPath  = "Fonts/Casino3D SDF";
        private const float  FallbackButtonFontSize    = 40f;

        [MenuItem("Texas Holdem/Apply Action Button Label Styling")]
        public static void ApplyMenu()
        {
            ApplyToScene();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Applied BlackJack-style labels to all action buttons.");
        }

        public static void ApplyToScene()
        {
            TMP_FontAsset font = LoadFontAsset();
            if (!HasBakedGlyphs(font))
            {
                Debug.LogWarning("Casino3D SDF is missing or empty. Run Texas Holdem → Regenerate Casino3D SDF Font.");
                return;
            }

            float buttonFontSize = GetButtonFontSize();

            StyleLabel(FindLabel("StartButton"),     "START", new Color(1f, 1f, 0f), font, buttonFontSize);
            StyleLabel(FindLabel("FoldButton"),      "FOLD",  new Color(1f, 0f, 0f), font, buttonFontSize);
            StyleLabel(FindLabel("CheckCallButton"), "CHECK", ActionColors.CheckCallGreen, font, buttonFontSize);
            StyleLabel(FindLabel("AllInButton"),     "ALL IN", new Color(1f, 0f, 1f), font, buttonFontSize);
            StyleLabel(FindLabel("RaiseButton"),     "RAISE", ButtonLabelStyle.RaiseText, font, buttonFontSize);

            EnsureButtonRowSizer();
        }

        private static TMP_Text FindLabel(string buttonName)
        {
            var button = GameObject.Find(buttonName)?.GetComponent<Button>();
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static void StyleLabel(TMP_Text label, string text, Color color, TMP_FontAsset font, float fontSize)
        {
            if (label == null) return;

            if (!string.IsNullOrEmpty(text))
                label.text = text;

            label.font = font;
            ClearMaterialInstance(label);
            ButtonLabelStyle.Apply(label, color, fontSize);
            EditorUtility.SetDirty(label);
        }

        private static void ClearMaterialInstance(TMP_Text label)
        {
            if (label.fontMaterial != null)
            {
                Object.DestroyImmediate(label.fontMaterial);
                label.fontMaterial = null;
            }

            label.fontSharedMaterial = label.font.material;
        }

        private static void EnsureButtonRowSizer()
        {
            GameObject.Find("ButtonRow")?.GetComponent<ButtonRowFontSize>()?.Apply();
        }

        private static float GetButtonFontSize()
        {
            var gm = Object.FindObjectOfType<GameManager>();
            return gm != null ? gm.ButtonFontSize : FallbackButtonFontSize;
        }

        private static TMP_FontAsset LoadFontAsset()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Casino3DSdfPath);
            if (HasBakedGlyphs(font))
                return font;

            return Resources.Load<TMP_FontAsset>(Casino3DSdfResourcesPath);
        }

        private static bool HasBakedGlyphs(TMP_FontAsset fontAsset)
        {
            return fontAsset != null
                && fontAsset.characterTable != null
                && fontAsset.characterTable.Count > 0;
        }
    }
}
