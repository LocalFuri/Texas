using TMPro;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>
    /// Shared HUD band geometry for player seats. Default layout: avatar left, text right.
    /// Mirrored layout (left table seats): avatar right, text left — reference Lady Luck HUD.
    /// </summary>
    public static class PlayerHudLayout
    {
        public const float PillW     = 220f;
        public const float PillH     = 76f;
        public const float PillY     = 0f;
        /// <summary>Extra half-width past the card outer edge — clears RoundedRect 9-slice corner (14px).</summary>
        public const float PillRoundedCornerInset = 18f;
        public const float HudGlowSpreadPx = 30f;
        public const float AvatarD   = 162f;
        public const float AvatarX   = -133f;
        public const float TextX     = 25f;
        public const float TextW     = 155f;
        /// <summary>Wider name row; inner edge lines up with the chips row beside the avatar.</summary>
        public const float NameTextW = 200f;
        public const float NameTextH = 36f;
        public const float TextAvatarPad = 10f; // horizontal gap between text and avatar ring
        public const float NameChipsGap = 8f; // vertical space between name and chips rows
        public const float NameY     = 16f;
        public const float ChipsY    = -12f - NameChipsGap * 0.5f;
        public const float ActionBadgeY = 32f;
        public const float ActionBadgeGlowW = 156f;
        public const float ActionBadgeGlowH = 64f;
        public const float SeatActionMenuY  = 32f;
        public const float SeatActionMenuW  = 155f;
        public const float SeatActionMenuH  = 118f;

        public static float AvatarPosX(bool mirrored) => mirrored ? -AvatarX : AvatarX;
        public static float TextPosX(bool mirrored)
            => mirrored ? -(TextX + TextAvatarPad) : (TextX + TextAvatarPad);

        /// <summary>
        /// Name row X — wide rect shares the chips row inner edge (toward the avatar) so both columns align.
        /// Long names grow away from the avatar into the extra width.
        /// </summary>
        public static float NameTextPosX(bool mirrored)
        {
            float textX    = TextPosX(mirrored);
            float halfChip = TextW * 0.5f;
            float halfName = NameTextW * 0.5f;
            float innerEdge = mirrored ? textX + halfChip : textX - halfChip;
            return mirrored ? innerEdge - halfName : innerEdge + halfName;
        }

        public static TextAlignmentOptions TextAlign(bool mirrored)
            => mirrored ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

        /// <summary>
        /// Pill width from Card_1 right (or Card_0 left when mirrored), plus inset for sliced corner transparency.
        /// </summary>
        public static float ComputePillWidthFromCards(
            PlayerView view, float hudCenterX, bool mirrored,
            float fallbackCardWidth = 120f, float fallbackCardGap = 16f)
        {
            if (view != null)
            {
                RectTransform rt0 = view.GetCardRect(0);
                RectTransform rt1 = view.GetCardRect(1);

                if (mirrored && rt0 != null)
                {
                    float cardLeft = rt0.anchoredPosition.x - rt0.sizeDelta.x * 0.5f;
                    return (hudCenterX - cardLeft + PillRoundedCornerInset) * 2f;
                }

                if (!mirrored && rt1 != null)
                {
                    float cardRight = rt1.anchoredPosition.x + rt1.sizeDelta.x * 0.5f;
                    return (cardRight - hudCenterX + PillRoundedCornerInset) * 2f;
                }
            }

            float cx       = fallbackCardWidth * 0.5f + fallbackCardGap * 0.5f;
            float halfCard = fallbackCardWidth * 0.5f;
            float outerX   = mirrored ? (-cx - halfCard) : (cx + halfCard);
            float halfW    = mirrored ? hudCenterX - outerX : outerX - hudCenterX;
            return (halfW + PillRoundedCornerInset) * 2f;
        }

        private static Vector2 ResolveHudLocalPx(Transform root)
        {
            var pv = root.GetComponent<PlayerView>();
            if (pv != null)
            {
#if UNITY_EDITOR
                TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                    FindObjectsInactive.Include);
#else
                TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>();
#endif
                if (layout != null)
                {
                    PlayerView[] views = layout.GetPlayerViews();
                    for (int i = 0; i < views.Length; i++)
                    {
                        if (views[i] == pv)
                            return layout.GetSeatConfig(i).hudLocalPx;
                    }
                }
            }
            // Default fallback
            bool mirrored = pv != null ? pv.HudMirrored : false;
            return new Vector2(mirrored ? -14f : 14f, 0f);
        }

        private static float ResolvePillWidth(Transform root, bool mirrored, Vector2 hudLocalPx)
        {
            var pv = root != null ? root.GetComponent<PlayerView>() : null;
#if UNITY_EDITOR
            TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                FindObjectsInactive.Include);
#else
            TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>();
#endif
            if (layout != null && pv != null)
                return layout.ComputePillWidth(hudLocalPx, mirrored, pv);

            return ComputePillWidthFromCards(pv, hudLocalPx.x, mirrored);
        }

        /// <summary>Positions avatar, name/chips, and action UI for the chosen mirror mode.</summary>
        public static void Apply(Transform root, bool mirrored)
        {
            if (root == null) return;

            float avatarX   = AvatarPosX(mirrored);
            float textX     = TextPosX(mirrored);
            float nameTextX = NameTextPosX(mirrored);
            var   align     = TextAlign(mirrored);
            Vector2 hudLocalPx = ResolveHudLocalPx(root);
            float pillW = ResolvePillWidth(root, mirrored, hudLocalPx);

            SetRect(FindChild(root, "AvatarFrame"), avatarX, PillY, AvatarD, AvatarD);
            ApplyText(FindChild(root, "NameText"),   nameTextX, NameY, NameTextW, NameTextH, align);
            ApplyText(FindChild(root, "ChipsText"),  textX, ChipsY, TextW, 26f, align);
            ApplyText(FindChild(root, "StatusText"), textX, ChipsY, TextW, 22f, align);

            SetRect(FindChild(root, "ActionBadge"),    textX, ActionBadgeY, ActionBadgeGlowW, ActionBadgeGlowH);
            SetRect(FindChild(root, "SeatActionMenu"), textX, SeatActionMenuY, SeatActionMenuW, SeatActionMenuH);
            SetRect(FindChild(root, "HudPanel"),       hudLocalPx.x, hudLocalPx.y, pillW, PillH);
            SetRect(FindChild(root, "HudGlow"),        hudLocalPx.x, hudLocalPx.y, pillW + HudGlowSpreadPx * 2f, PillH + HudGlowSpreadPx * 2f);

            var hudGlow = FindChild(root, "HudGlow")?.GetComponent<HudPanelGlowGraphic>();
            if (hudGlow != null)
            {
                hudGlow.PanelWidthPx  = pillW;
                hudGlow.PanelHeightPx = PillH;
            }
        }

        private static void ApplyText(Transform t, float x, float y, float w, float h, TextAlignmentOptions align)
        {
            if (t == null) return;
            SetRect(t, x, y, w, h);
            var txt = t.GetComponent<TMP_Text>();
            if (txt != null)
                txt.alignment = align;
        }

        private static void SetRect(Transform t, float x, float y, float w, float h)
        {
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        private static Transform FindChild(Transform root, string name)
        {
            Transform direct = root.Find(name);
            if (direct != null) return direct;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }
            return null;
        }
    }
}
