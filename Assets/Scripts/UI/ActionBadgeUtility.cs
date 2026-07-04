using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Shared repair + layout for seat action confirmation pills.</summary>
    public static class ActionBadgeUtility
    {
        public const float GlowRectWidth  = 100f;
        public const float GlowRectHeight = 52f;
        public const float LayoutX        = 25f;
        public const float LayoutY        = 32f;

        /// <summary>Neon capsule layout — pill body plus halo outset baked into the badge rect.</summary>
        public const float NeonGlowOutset    = 10f;
        public const float NeonPillHeight    = 32f;
        public const float NeonPillPadH      = 28f;
        public const float NeonMinPillWidth  = 68f;
        public static float NeonBadgeHeight => NeonPillHeight + NeonGlowOutset * 2f;

        public static void Repair(GameObject actionBadgeGo, ActionBadge badge)
        {
            if (actionBadgeGo == null)
                return;

            CleanupDuplicateComponents(actionBadgeGo);
            ApplyLayoutRect(actionBadgeGo.transform as RectTransform);

            ActionBadgeSdfGraphic pill = actionBadgeGo.GetComponent<ActionBadgeSdfGraphic>();
            if (pill != null)
            {
                pill.maskable = false;
                pill.raycastTarget = false;
            }

            if (badge != null && pill != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var so = new UnityEditor.SerializedObject(badge);
                    so.Update();
                    so.FindProperty("_pillGraphic").objectReferenceValue = pill;
                    Transform labelT = actionBadgeGo.transform.Find("Label");
                    if (labelT != null)
                        so.FindProperty("_label").objectReferenceValue = labelT.GetComponent<TMPro.TMP_Text>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
#endif
            }

            pill?.ForceRefresh();
        }

        public static void CleanupDuplicateComponents(GameObject actionBadgeGo)
        {
            ActionBadgeSdfGraphic[] graphics = actionBadgeGo.GetComponents<ActionBadgeSdfGraphic>();
            if (graphics.Length > 1)
            {
                ActionBadgeSdfGraphic keep = graphics[0];
                for (int i = 1; i < graphics.Length; i++)
                    DestroyComponent(graphics[i]);
            }

            CanvasRenderer[] renderers = actionBadgeGo.GetComponents<CanvasRenderer>();
            for (int i = 1; i < renderers.Length; i++)
                DestroyComponent(renderers[i]);
        }

        public static void ApplyLayoutRect(RectTransform rt)
        {
            if (rt == null)
                return;

            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(LayoutX, LayoutY);
            rt.sizeDelta        = new Vector2(GlowRectWidth, GlowRectHeight);
        }

        /// <summary>
        /// Seat <see cref="ActionBadge"/> pills only — removes mistaken SDF graphics from sprite buttons.
        /// </summary>
        public static void RestoreSpriteButton(Button button)
        {
            if (button == null)
                return;

            GameObject go = button.gameObject;

            ActionBadgeSdfGraphic sdf = go.GetComponent<ActionBadgeSdfGraphic>();
            if (sdf != null)
                DestroyGraphicImmediately(sdf);

            Transform sdfFrame = go.transform.Find("SdfFrame");
            if (sdfFrame != null)
                DestroyObject(sdfFrame.gameObject);

            Image img = go.GetComponent<Image>();
            if (img == null)
                return;

            img.enabled          = true;
            button.transition    = Selectable.Transition.SpriteSwap;
            button.targetGraphic = img;
        }

        private static void DestroyGraphicImmediately(Graphic graphic)
        {
            if (graphic == null)
                return;

            Object.DestroyImmediate(graphic);
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

        private static void DestroyComponent(Object component)
        {
            if (component == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(component);
            else
#endif
                Object.Destroy(component);
        }
    }
}
