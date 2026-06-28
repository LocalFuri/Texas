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
        /// <summary>RoundedRect.png spriteBorder — panel rect extends this far past Card_1 so opaque fill reaches the card edge.</summary>
        public const float RoundedRectBorderPx = 14f;
        public const float HudGlowSpreadPx = 14f;
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

        /// <summary>Fixed left edge of the HUD content band (avatar overlap side).</summary>
        public static float ComputePanelLeftX(float hudCenterX) => hudCenterX - PillW * 0.5f;

        /// <summary>Rect right edge for a centre-anchored, centre-pivot RectTransform.</summary>
        public static float GetRectRightX(RectTransform rt)
        {
            if (rt == null) return 0f;
            float w = rt.rect.width;
            if (w <= 0f)
                w = rt.sizeDelta.x;
            return rt.anchoredPosition.x + w * 0.5f;
        }

        /// <summary>Symmetric hole-card centres — independent of HudPanel width.</summary>
        public static void ComputeHoleCardCenterX(
            float hudCenterX, float cardWidth, float cardGap,
            out float card0CenterX, out float card1CenterX)
        {
            float cx = (cardWidth + cardGap) * 0.5f;
            card0CenterX = hudCenterX - cx;
            card1CenterX = hudCenterX + cx;
        }

        /// <summary>
        /// Sizes HudPanel from Card_1. Rect extends RoundedRectBorderPx past the card so 9-slice
        /// opaque fill (not the transparent corner) covers Card_1 lower-right.
        /// </summary>
        public static void ApplyHudPanelFromCard1(Transform root, RectTransform card1, Vector2 hudLocalPx)
        {
            if (root == null) return;

            float panelLeft  = ComputePanelLeftX(hudLocalPx.x);
            float panelWidth = PillW;
            float panelCenterX = hudLocalPx.x;
            float panelHeight = PillH;
            float panelCenterY = hudLocalPx.y;

            if (card1 != null)
            {
                float card1Right     = GetRectRightX(card1);
                float border         = (root.name == "Seat_You") ? 0f : RoundedRectBorderPx;
                float panelRectRight = card1Right + border;
                panelWidth  = Mathf.Max(PillW, panelRectRight - panelLeft);
                panelCenterX = panelLeft + panelWidth * 0.5f;

                if (root.name == "Seat_You")
                {
                    panelHeight = 104f;
                    panelCenterY = hudLocalPx.y - 14f;
                }
            }

            var hudGlow = FindChild(root, "HudGlow")?.GetComponent<HudPanelGlowGraphic>();
            float glowSpread = ResolveGlowSpreadPx(hudGlow);

            SetRect(FindChild(root, "HudPanel"), panelCenterX, panelCenterY, panelWidth, panelHeight);
            SetRect(FindChild(root, "HudGlow"), panelCenterX, panelCenterY,
                panelWidth + glowSpread * 2f, panelHeight + glowSpread * 2f);

            if (hudGlow != null)
            {
                hudGlow.PanelWidthPx  = panelWidth;
                hudGlow.PanelHeightPx = panelHeight;
            }
        }

        /// <summary>Uses the HudGlow component spread when set; default constant for new seats.</summary>
        private static float ResolveGlowSpreadPx(HudPanelGlowGraphic hudGlow)
        {
            if (hudGlow != null && hudGlow.GlowSpreadPx >= 8f)
                return hudGlow.GlowSpreadPx;
            return HudGlowSpreadPx;
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
            return new Vector2(0f, 0f);
        }

        /// <summary>Positions avatar, name/chips, action UI, and card-driven HudPanel.</summary>
        public static void Apply(Transform root, bool mirrored)
        {
            if (root == null) return;

            float avatarX   = AvatarPosX(mirrored);
            float textX     = TextPosX(mirrored);
            float nameTextX = NameTextPosX(mirrored);
            var   align     = TextAlign(mirrored);
            Vector2 hudLocalPx = ResolveHudLocalPx(root);

            SetRect(FindChild(root, "AvatarFrame"), avatarX, PillY, AvatarD, AvatarD);
            ApplyText(FindChild(root, "NameText"),   nameTextX, NameY, NameTextW, NameTextH, align);
            ApplyText(FindChild(root, "ChipsText"),  textX, ChipsY, TextW, 26f, align);
            ApplyText(FindChild(root, "StatusText"), textX, ChipsY, TextW, 22f, align);

            SetRect(FindChild(root, "ActionBadge"),    textX, ActionBadgeY, ActionBadgeGlowW, ActionBadgeGlowH);
            SetRect(FindChild(root, "SeatActionMenu"), textX, SeatActionMenuY, SeatActionMenuW, SeatActionMenuH);

            var pv = root.GetComponent<PlayerView>();
            RectTransform card1 = pv != null ? pv.GetCardRect(1) : null;
            ApplyHudPanelFromCard1(root, card1, hudLocalPx);
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
