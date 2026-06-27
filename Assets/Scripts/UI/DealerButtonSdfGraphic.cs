using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Procedural gold dealer disc — smooth SDF circle, no texture dependency.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public class DealerButtonSdfGraphic : RawImage
    {
        private static readonly int RectSizeId            = Shader.PropertyToID("_RectSize");
        private static readonly int RadiusPxId            = Shader.PropertyToID("_RadiusPx");
        private static readonly int RimWidthPxId           = Shader.PropertyToID("_RimWidthPx");
        private static readonly int InnerRingWidthPxId      = Shader.PropertyToID("_InnerRingWidthPx");
        private static readonly int InnerRingRadiusFracId   = Shader.PropertyToID("_InnerRingRadiusFrac");
        private static readonly int GoldColorTopId          = Shader.PropertyToID("_GoldColorTop");
        private static readonly int GoldColorBotId          = Shader.PropertyToID("_GoldColorBot");
        private static readonly int RimColorId              = Shader.PropertyToID("_RimColor");
        private static readonly int HighlightStrengthId     = Shader.PropertyToID("_HighlightStrength");

        private Material _discMaterial;

        [SerializeField] private Shader _shader;
        [SerializeField] private float _radiusPx = -1f;
        [SerializeField, Range(1f, 6f)] private float _rimWidthPx = 2.5f;
        [SerializeField, Range(0.5f, 4f)] private float _innerRingWidthPx = 1.5f;
        [SerializeField, Range(0.5f, 0.95f)] private float _innerRingRadiusFrac = 0.78f;
        [SerializeField] private Color _goldColorTop = new Color(1.00f, 0.88f, 0.35f, 1f);
        [SerializeField] private Color _goldColorBot = new Color(0.72f, 0.52f, 0.08f, 1f);
        [SerializeField] private Color _rimColor = new Color(0.12f, 0.08f, 0.02f, 1f);
        [SerializeField, Range(0f, 1f)] private float _highlightStrength = 0.35f;

        public float RadiusPx
        {
            get => _radiusPx;
            set { _radiusPx = value; MarkRenderDirty(); }
        }

        public void AssignShaderIfNeeded()
        {
            if (_shader != null && _shader.name != "Hidden/InternalErrorShader")
                return;

            _shader = UiAvatarShaders.ResolveDealerButtonSdf(null);
            MarkRenderDirty();
        }

        public void ForceRefresh()
        {
            AssignShaderIfNeeded();
            EnsureRendererSettings();
            MarkRenderDirty();
            if (!IsActive())
                return;

            UpdateGeometry();
            UpdateMaterial();
            Canvas.ForceUpdateCanvases();
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive() || canvasRenderer == null)
                return;

            AssignShaderIfNeeded();

            if (!EnsureDiscMaterial())
            {
                Debug.LogWarning("[DealerButtonSdfGraphic] UI/DealerButtonSDF shader missing — disc will not render.", this);
                return;
            }

            UiAvatarShaders.ApplyRingCanvasMaterial(_discMaterial, base.materialForRendering);
            ApplyUniforms(_discMaterial);

            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(_discMaterial, 0);
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
            texture = null;
            EnsureRendererSettings();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureRendererSettings();
            MarkRenderDirty();
            if (IsActive())
                ForceRefresh();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorApplication.delayCall += ForceCanvasRebuildInEditor;
#endif
        }

#if UNITY_EDITOR
        private void ForceCanvasRebuildInEditor()
        {
            if (this == null || Application.isPlaying) return;
            ForceRefresh();
        }
#endif

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!Application.isPlaying)
                CleanupMaterial();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkRenderDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            MarkRenderDirty();
        }
#endif

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

        private bool EnsureDiscMaterial()
        {
            Shader shader = UiAvatarShaders.ResolveDealerButtonSdf(_shader);
            if (shader == null)
                return false;

            if (_shader == null)
                _shader = shader;

            if (_discMaterial == null || _discMaterial.shader != shader)
            {
                CleanupMaterial();
                _discMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            return true;
        }

        private void CleanupMaterial()
        {
            if (_discMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_discMaterial);
            else
                DestroyImmediate(_discMaterial);
            _discMaterial = null;
        }

        private void ApplyUniforms(Material mat)
        {
            if (mat == null)
                return;

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                rect = rectTransform.rect;

            float minSide = Mathf.Min(rect.width, rect.height);
            float radius  = _radiusPx > 0f ? _radiusPx : minSide * 0.5f - 1f;
            if (radius <= 0f)
                radius = 23f;

            mat.SetVector(RectSizeId, new Vector2(rect.width, rect.height));
            mat.SetFloat(RadiusPxId, radius);
            mat.SetFloat(RimWidthPxId, _rimWidthPx);
            mat.SetFloat(InnerRingWidthPxId, _innerRingWidthPx);
            mat.SetFloat(InnerRingRadiusFracId, _innerRingRadiusFrac);
            mat.SetColor(GoldColorTopId, _goldColorTop);
            mat.SetColor(GoldColorBotId, _goldColorBot);
            mat.SetColor(RimColorId, _rimColor);
            mat.SetFloat(HighlightStrengthId, _highlightStrength);
        }
    }
}
