using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Applies GGPoker-style SDF frames to bottom action-panel buttons.</summary>
    public static class ActionPanelButtonUtility
    {
        public const string SdfFrameName = "SdfFrame";

        public static void ApplyCallLook(Button button, TMP_Text label, TMP_FontAsset font, float fontSize)
        {
            if (button == null)
                return;

            RemoveMistakenRootGraphic(button);

            Image img = button.GetComponent<Image>();
            if (img != null)
            {
                img.enabled       = false;
                img.raycastTarget = false;
            }

            ActionBadgeSdfGraphic gfx = EnsureSdfFrameGraphic(button);
            if (gfx == null)
                return;

            float w = 120f;
            float h = 50f;
            if (button.transform is RectTransform rt)
            {
                Canvas.ForceUpdateCanvases();
                if (rt.rect.width  > 0f) w = rt.rect.width;
                if (rt.rect.height > 0f) h = rt.rect.height;
            }

            gfx.ApplyGgpokerCallPreset(w, h);
            gfx.raycastTarget = true;
            button.targetGraphic = gfx;
            button.transition    = Selectable.Transition.ColorTint;

            if (gfx.isActiveAndEnabled)
                gfx.ForceRefresh();

            ColorBlock colors = button.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration     = 0.08f;
            button.colors = colors;

            if (label != null && font != null)
            {
                label.font = font;
                ButtonLabelStyle.Apply(label, ActionPanelButtonStyle.CallLabelGold, fontSize);
            }

            if (button.GetComponent<ButtonHoverFix>() == null)
                button.gameObject.AddComponent<ButtonHoverFix>();
        }

        public static void RemoveCallSdfFrame(Button button)
        {
            if (button == null)
                return;

            RemoveMistakenRootGraphic(button);

            Transform frame = button.transform.Find(SdfFrameName);
            if (frame != null)
                DestroyObject(frame.gameObject);
        }

        private static ActionBadgeSdfGraphic EnsureSdfFrameGraphic(Button button)
        {
            Transform root = button.transform;
            Transform frameT = root.Find(SdfFrameName);
            if (frameT == null)
            {
                var go = new GameObject(SdfFrameName, typeof(RectTransform));
                frameT = go.transform;
                frameT.SetParent(root, false);
                frameT.SetAsFirstSibling();

                var rt = (RectTransform)frameT;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            ActionBadgeSdfGraphic gfx = frameT.GetComponent<ActionBadgeSdfGraphic>();
            if (gfx == null)
                gfx = frameT.gameObject.AddComponent<ActionBadgeSdfGraphic>();

            return gfx;
        }

        private static void RemoveMistakenRootGraphic(Button button)
        {
            ActionBadgeSdfGraphic rootGfx = button.GetComponent<ActionBadgeSdfGraphic>();
            if (rootGfx != null)
                DestroyObject(rootGfx);
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(obj);
            else
#endif
                Object.Destroy(obj);
        }
    }
}
