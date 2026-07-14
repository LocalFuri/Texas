using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Shared repair + layout for seat action badge PNGs.</summary>
    public static class ActionBadgeUtility
    {
        public static void Repair(GameObject actionBadgeGo, ActionBadge badge)
        {
            if (actionBadgeGo == null)
                return;

            MigrateFromSdfGraphic(actionBadgeGo);
            CleanupDuplicateComponents(actionBadgeGo);
            RemoveNonImageGraphics(actionBadgeGo);

            Image img = EnsureBadgeFaceImage(actionBadgeGo);

            img.type           = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            img.maskable       = true;

            Transform label = actionBadgeGo.transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);

            if (badge != null && badge.UsesCustomLayout)
            {
                badge.ApplyCustomLayout(actionBadgeGo.transform as RectTransform,
                    ActionBadgeSprites.For(BettingAction.Check) ?? ActionBadgeSprites.Winner);
                ApplyGlobalOffset(actionBadgeGo.transform as RectTransform);
            }
            else
                ApplyAutoLayoutRect(actionBadgeGo.transform as RectTransform);

            if (badge != null)
                badge.EnsureBadgeStructure();

#if UNITY_EDITOR
            if (!Application.isPlaying && badge != null)
            {
                var so = new UnityEditor.SerializedObject(badge);
                so.Update();
                so.FindProperty("_badgeImage").objectReferenceValue = img;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
#endif

            if (badge != null)
                badge.WireBadgeImage(img);
        }

        /// <summary>Layout refresh only — safe during OnValidate (no destroy/migrate).</summary>
        public static void RepairLayoutOnly(GameObject actionBadgeGo, ActionBadge badge)
        {
            if (badge == null)
                return;

            badge.RefreshLayout();
        }

        /// <summary>Moves the badge PNG to a child so winner FX layers can sit underneath.</summary>
        public static Image EnsureBadgeFaceImage(GameObject actionBadgeGo)
        {
            if (actionBadgeGo == null)
                return null;

            Transform faceTransform = actionBadgeGo.transform.Find("BadgeFace");
            Image rootImage = actionBadgeGo.GetComponent<Image>();

            if (faceTransform == null)
            {
                var faceGo = new GameObject("BadgeFace", typeof(RectTransform), typeof(CanvasRenderer));
                faceGo.transform.SetParent(actionBadgeGo.transform, false);
                StretchFill(faceGo.transform as RectTransform);

                Image faceImage = faceGo.AddComponent<Image>();
                if (rootImage != null)
                {
                    faceImage.sprite         = rootImage.sprite;
                    faceImage.color          = rootImage.color;
                    faceImage.preserveAspect = rootImage.preserveAspect;
                    faceImage.type           = rootImage.type;
                    DestroyGraphicImmediately(rootImage);
                }

                faceTransform = faceGo.transform;
            }
            else if (rootImage != null && rootImage.transform != faceTransform)
                DestroyGraphicImmediately(rootImage);

            return faceTransform.GetComponent<Image>()
                   ?? faceTransform.gameObject.AddComponent<Image>();
        }

        private static void StretchFill(RectTransform rt)
        {
            if (rt == null)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
        }

        internal static void DestroyGraphic(Graphic graphic) => DestroyUnityObject(graphic);

        private static void MigrateFromSdfGraphic(GameObject actionBadgeGo)
        {
            ActionBadgeSdfGraphic[] sdfs = actionBadgeGo.GetComponents<ActionBadgeSdfGraphic>();
            for (int i = 0; i < sdfs.Length; i++)
                DestroyGraphicImmediately(sdfs[i]);
        }

        private static void RemoveNonImageGraphics(GameObject actionBadgeGo)
        {
            Graphic[] graphics = actionBadgeGo.GetComponents<Graphic>();
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is Image)
                    continue;

                DestroyGraphicImmediately(graphics[i]);
            }
        }

        public static void CleanupDuplicateComponents(GameObject actionBadgeGo)
        {
            ActionBadgeSdfGraphic[] graphics = actionBadgeGo.GetComponents<ActionBadgeSdfGraphic>();
            for (int i = 0; i < graphics.Length; i++)
                DestroyGraphicImmediately(graphics[i]);

            Image[] images = actionBadgeGo.GetComponents<Image>();
            for (int i = 1; i < images.Length; i++)
                DestroyComponent(images[i]);

            CanvasRenderer[] renderers = actionBadgeGo.GetComponents<CanvasRenderer>();
            for (int i = 1; i < renderers.Length; i++)
                DestroyComponent(renderers[i]);
        }

        public static void ApplyAutoLayoutRect(RectTransform rt, Sprite sprite = null)
        {
            if (rt == null)
                return;

            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ActionBadgeAnchoredPosition(rt.parent);

            ActionBadgeSprites.EnsureLoaded();
            sprite ??= ActionBadgeSprites.For(BettingAction.Check) ?? ActionBadgeSprites.Winner;
            rt.sizeDelta = ActionBadgeSprites.SizeForSprite(sprite);
        }

        public static Vector2 ActionBadgeAnchoredPosition(Transform seatRoot) =>
            new Vector2(
                PlayerHudLayout.ResolveActionBadgeX(seatRoot) + PlayerHudLayout.ActionBadgeOffset.x,
                PlayerHudLayout.ResolveActionBadgeY(seatRoot) + PlayerHudLayout.ActionBadgeOffset.y);

        public static void ApplyGlobalOffset(RectTransform rt)
        {
            if (rt == null || PlayerHudLayout.ActionBadgeOffset == Vector2.zero)
                return;

            rt.anchoredPosition += PlayerHudLayout.ActionBadgeOffset;
        }

        /// <summary>Legacy name — applies automatic card-centre layout.</summary>
        public static void ApplyLayoutRect(RectTransform rt) => ApplyAutoLayoutRect(rt);

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

        private static void DestroyGraphicImmediately(Graphic graphic) => DestroyUnityObject(graphic);

        private static void DestroyObject(Object obj) => DestroyUnityObject(obj);

        private static void DestroyComponent(Object component) => DestroyUnityObject(component);

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
                return;
            }

#if UNITY_EDITOR
            // Edit mode: Destroy is illegal; defer DestroyImmediate so we are not inside OnValidate.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            };
#else
            Object.DestroyImmediate(obj);
#endif
        }
    }
}
