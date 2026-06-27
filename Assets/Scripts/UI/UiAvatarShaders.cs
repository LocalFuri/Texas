using UnityEngine;

namespace TexasHoldem
{
    /// <summary>Resolves UI avatar shaders from serialized refs, editor asset paths, then Shader.Find.</summary>
    public static class UiAvatarShaders
    {
        private static readonly int StencilId          = Shader.PropertyToID("_Stencil");
        private static readonly int StencilCompId      = Shader.PropertyToID("_StencilComp");
        private static readonly int StencilOpId        = Shader.PropertyToID("_StencilOp");
        private static readonly int StencilWriteMaskId = Shader.PropertyToID("_StencilWriteMask");
        private static readonly int StencilReadMaskId  = Shader.PropertyToID("_StencilReadMask");
        private static readonly int ColorMaskId        = Shader.PropertyToID("_ColorMask");
        private static readonly int ClipRectId         = Shader.PropertyToID("_ClipRect");

        public const string RingSdfPath     = "Assets/Shaders/UI/AvatarRingSDF.shader";
        public const string CircleClipPath  = "Assets/Shaders/UI/AvatarCircleClip.shader";
        public const string HudPanelGlowPath   = "Assets/Shaders/UI/HudPanelGlow.shader";
        public const string ActionBadgeSdfPath  = "Assets/Shaders/UI/ActionBadgeSDF.shader";
        public const string DealerButtonSdfPath = "Assets/Shaders/UI/DealerButtonSDF.shader";
        public const string RingSdfName        = "UI/AvatarRingSDF";
        public const string CircleClipName     = "UI/AvatarCircleClip";
        public const string HudPanelGlowName   = "UI/HudPanelGlow";
        public const string ActionBadgeSdfName = "UI/ActionBadgeSDF";
        public const string DealerButtonSdfName = "UI/DealerButtonSDF";

        public static Shader ResolveRingSdf(Shader assigned) =>
            Resolve(assigned, RingSdfPath, RingSdfName);

        public static Shader ResolveCircleClip(Shader assigned) =>
            Resolve(assigned, CircleClipPath, CircleClipName);

        public static Shader ResolveHudPanelGlow(Shader assigned) =>
            Resolve(assigned, HudPanelGlowPath, HudPanelGlowName);

        public static Shader ResolveActionBadgeSdf(Shader assigned) =>
            Resolve(assigned, ActionBadgeSdfPath, ActionBadgeSdfName);

        public static Shader ResolveDealerButtonSdf(Shader assigned) =>
            Resolve(assigned, DealerButtonSdfPath, DealerButtonSdfName);

        private static Shader Resolve(Shader assigned, string assetPath, string shaderName)
        {
            if (assigned != null && assigned.name != "Hidden/InternalErrorShader")
                return assigned;

#if UNITY_EDITOR
            Shader loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (loaded != null)
                return loaded;
#endif
            return Shader.Find(shaderName);
        }

        /// <summary>
        /// Ring graphics sit outside RectMask2D — copy stencil only, never canvas clip rect.
        /// </summary>
        public static void ApplyRingCanvasMaterial(Material custom, Material canvasBase)
        {
            if (custom == null)
                return;

            custom.DisableKeyword("UNITY_UI_CLIP_RECT");
            custom.DisableKeyword("UNITY_UI_ALPHACLIP");
            CopyStencil(custom, canvasBase);
        }

        /// <summary>
        /// Masked avatar images live under RectMask2D — keep clip rect from the canvas base material.
        /// </summary>
        public static void ApplyMaskedCircleCanvasMaterial(Material custom, Material canvasBase)
        {
            if (custom == null)
                return;

            CopyStencil(custom, canvasBase);
            CopyClipRectKeyword(custom, canvasBase);
        }

        private static void CopyStencil(Material custom, Material canvasBase)
        {
            if (canvasBase == null)
                return;

            custom.SetFloat(StencilId,          canvasBase.GetFloat(StencilId));
            custom.SetFloat(StencilCompId,      canvasBase.GetFloat(StencilCompId));
            custom.SetFloat(StencilOpId,        canvasBase.GetFloat(StencilOpId));
            custom.SetFloat(StencilWriteMaskId, canvasBase.GetFloat(StencilWriteMaskId));
            custom.SetFloat(StencilReadMaskId,  canvasBase.GetFloat(StencilReadMaskId));
            custom.SetFloat(ColorMaskId,        canvasBase.GetFloat(ColorMaskId));
        }

        private static void CopyClipRectKeyword(Material custom, Material canvasBase)
        {
            if (canvasBase == null)
            {
                custom.DisableKeyword("UNITY_UI_CLIP_RECT");
                custom.DisableKeyword("UNITY_UI_ALPHACLIP");
                return;
            }

            if (canvasBase.IsKeywordEnabled("UNITY_UI_CLIP_RECT"))
            {
                custom.EnableKeyword("UNITY_UI_CLIP_RECT");
                custom.SetVector(ClipRectId, canvasBase.GetVector(ClipRectId));
            }
            else
            {
                custom.DisableKeyword("UNITY_UI_CLIP_RECT");
            }

            if (canvasBase.IsKeywordEnabled("UNITY_UI_ALPHACLIP"))
                custom.EnableKeyword("UNITY_UI_ALPHACLIP");
            else
                custom.DisableKeyword("UNITY_UI_ALPHACLIP");
        }
    }
}
