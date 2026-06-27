using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Soft outer bloom around a rounded HUD panel — SDF falloff, no sprite.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public class HudPanelGlowGraphic : RawImage
    {
        private static readonly int RectSizeId       = Shader.PropertyToID("_RectSize");
        private static readonly int PanelSizeId      = Shader.PropertyToID("_PanelSize");
        private static readonly int CornerRadiusPxId = Shader.PropertyToID("_CornerRadiusPx");
        private static readonly int GlowSpreadPxId   = Shader.PropertyToID("_GlowSpreadPx");
        private static readonly int GlowIntensityId  = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlowColorId      = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowFalloffId    = Shader.PropertyToID("_GlowFalloff");
        private static readonly int SideBoostId      = Shader.PropertyToID("_SideBoost");
        private static readonly int TopAttenId       = Shader.PropertyToID("_TopAtten");

        private Material _glowMaterial;

        [SerializeField] private Shader _shader;
        [SerializeField] private float _panelWidthPx  = 220f;
        [SerializeField] private float _panelHeightPx = 70f;
        [SerializeField, Range(1f, 40f)]
        private float _cornerRadiusPx = 14f;
        [SerializeField, Range(8f, 80f)]
        private float _glowSpreadPx = 30f;
        [SerializeField, Range(0f, 1.5f)]
        private float _glowIntensity;
        [SerializeField] private Color _glowColor = Color.white;
        [SerializeField, Range(0.8f, 2.5f)]
        private float _glowFalloff = 1.4f;
        [SerializeField, Range(0f, 1f)]
        private float _sideBoost = 0.72f;
        [FormerlySerializedAs("_vertAtten")]
        [SerializeField, Range(0f, 1f)]
        private float _topAtten = 0.85f;

        public float PanelWidthPx
        {
            get => _panelWidthPx;
            set { _panelWidthPx = Mathf.Max(value, 1f); MarkRenderDirty(); }
        }

        public float PanelHeightPx
        {
            get => _panelHeightPx;
            set { _panelHeightPx = Mathf.Max(value, 1f); MarkRenderDirty(); }
        }

        public float GlowSpreadPx
        {
            get => _glowSpreadPx;
            set { _glowSpreadPx = Mathf.Clamp(value, 8f, 80f); MarkRenderDirty(); }
        }

        /// <summary>0 = hidden; 1 = full bloom. Animated by PlayerView during active turn.</summary>
        public float GlowIntensity
        {
            get => _glowIntensity;
            set { _glowIntensity = Mathf.Clamp(value, 0f, 1.5f); MarkRenderDirty(); }
        }

        public Color GlowColor
        {
            get => _glowColor;
            set { _glowColor = value; MarkRenderDirty(); }
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive())
                return;

            if (!EnsureGlowMaterial())
            {
                base.UpdateMaterial();
                return;
            }

            ApplyUniforms(_glowMaterial);

            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(_glowMaterial, 0);
            canvasRenderer.SetTexture(mainTexture);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f)
                r = rectTransform.rect;

            Color32 c = color;
            vh.Clear();
            vh.AddVert(new Vector3(r.x,           r.y),            c, new Vector2(0, 0));
            vh.AddVert(new Vector3(r.x,           r.y + r.height), c, new Vector2(0, 1));
            vh.AddVert(new Vector3(r.x + r.width, r.y + r.height), c, new Vector2(1, 1));
            vh.AddVert(new Vector3(r.x + r.width, r.y),            c, new Vector2(1, 0));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureRendererSettings();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureRendererSettings();
            MarkRenderDirty();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += ForceCanvasRebuildInEditor;
#endif
        }

#if UNITY_EDITOR
        private void ForceCanvasRebuildInEditor()
        {
            if (this == null || Application.isPlaying) return;
            Canvas.ForceUpdateCanvases();
        }
#endif

        protected override void OnDisable()
        {
            base.OnDisable();
            CleanupMaterial();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkRenderDirty();
        }

        private void MarkRenderDirty()
        {
            SetVerticesDirty();
            SetMaterialDirty();
        }

        private void EnsureRendererSettings()
        {
            if (canvasRenderer != null)
                canvasRenderer.cullTransparentMesh = false;
        }

        private bool EnsureGlowMaterial()
        {
            Shader shader = UiAvatarShaders.ResolveHudPanelGlow(_shader);
            if (shader == null)
                return false;

            if (_shader == null)
                _shader = shader;

            if (_glowMaterial == null || _glowMaterial.shader != shader)
            {
                CleanupMaterial();
                _glowMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            return true;
        }

        private void CleanupMaterial()
        {
            if (_glowMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_glowMaterial);
            else
                DestroyImmediate(_glowMaterial);
            _glowMaterial = null;
        }

        private void ApplyUniforms(Material mat)
        {
            if (mat == null)
                return;

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                rect = rectTransform.rect;

            mat.SetVector(RectSizeId,      new Vector2(rect.width, rect.height));
            mat.SetVector(PanelSizeId,     new Vector2(_panelWidthPx, _panelHeightPx));
            mat.SetFloat(CornerRadiusPxId, _cornerRadiusPx);
            mat.SetFloat(GlowSpreadPxId,   _glowSpreadPx);
            mat.SetFloat(GlowIntensityId,  _glowIntensity);
            mat.SetColor(GlowColorId,      _glowColor);
            mat.SetFloat(GlowFalloffId,    _glowFalloff);
            mat.SetFloat(SideBoostId,      _sideBoost);
            mat.SetFloat(TopAttenId,       _topAtten);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureRendererSettings();
            MarkRenderDirty();
        }
#endif
    }
}
