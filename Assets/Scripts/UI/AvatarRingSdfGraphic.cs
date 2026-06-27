using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>SDF annulus ring — single quad, fragment-shader anti-aliasing. No mesh tessellation.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public class AvatarRingSdfGraphic : RawImage
    {
        public enum RingLook
        {
            Chrome,
            Gold
        }

        private static readonly int StrokeWidthPxId  = Shader.PropertyToID("_StrokeWidthPx");
        private static readonly int OuterRadiusPxId  = Shader.PropertyToID("_OuterRadiusPx");
        private static readonly int RectSizeId        = Shader.PropertyToID("_RectSize");
        private static readonly int FillAmountId      = Shader.PropertyToID("_FillAmount");
        private static readonly int RingLookId        = Shader.PropertyToID("_RingLook");
        private static readonly int ChromeColorTopId  = Shader.PropertyToID("_ChromeColorTop");
        private static readonly int ChromeColorBotId  = Shader.PropertyToID("_ChromeColorBot");
        private static readonly int GoldColorTopId  = Shader.PropertyToID("_GoldColorTop");
        private static readonly int GoldColorBotId  = Shader.PropertyToID("_GoldColorBot");

        private Material _ringMaterial;

        [SerializeField] private Shader _shader;
        [SerializeField] private RingLook _look = RingLook.Chrome;
        [SerializeField, Range(1f, 40f)]
        private float _strokeWidthPx = 6f;
        [SerializeField, Range(0f, 1f)]
        private float _fillAmount = 1f;
        [SerializeField] private Color _chromeColorTop = new Color(0.95f, 0.95f, 1.00f, 1f);
        [SerializeField] private Color _chromeColorBot = new Color(0.25f, 0.25f, 0.30f, 1f);
        [FormerlySerializedAs("_goldColor")]
        [SerializeField] private Color _goldColorTop = new Color(1.00f, 0.88f, 0.35f, 1f);
        [SerializeField] private Color _goldColorBot = new Color(0.45f, 0.28f, 0.05f, 1f);
        /// <summary>Outer radius in pixels. Set to -1 to derive automatically from the RectTransform size.</summary>
        [SerializeField] private float _outerRadiusPx = -1f;

        private Color _defaultGoldTop;
        private Color _defaultGoldBot;
        private bool  _goldDefaultsCached;

        public RingLook Look
        {
            get => _look;
            set { _look = value; MarkRenderDirty(); }
        }

        public float StrokeWidthPx
        {
            get => _strokeWidthPx;
            set { _strokeWidthPx = Mathf.Clamp(value, 1f, 40f); MarkRenderDirty(); }
        }

        /// <summary>Outer radius in pixels; -1 derives from the RectTransform size.</summary>
        public float OuterRadiusPx
        {
            get => _outerRadiusPx;
            set { _outerRadiusPx = value; MarkRenderDirty(); }
        }

        /// <summary>Copies annulus geometry and RectTransform layout so stacked rings stay concentric.</summary>
        public void CopyGeometryFrom(AvatarRingSdfGraphic source)
        {
            if (source == null) return;
            _strokeWidthPx = source._strokeWidthPx;
            _outerRadiusPx = source._outerRadiusPx;

            RectTransform srcRect = source.rectTransform;
            RectTransform dstRect = rectTransform;
            if (srcRect != null && dstRect != null)
            {
                dstRect.anchorMin        = srcRect.anchorMin;
                dstRect.anchorMax        = srcRect.anchorMax;
                dstRect.pivot            = srcRect.pivot;
                dstRect.anchoredPosition = srcRect.anchoredPosition;
                dstRect.sizeDelta        = srcRect.sizeDelta;
                dstRect.localScale       = srcRect.localScale;
                dstRect.localRotation    = srcRect.localRotation;
            }

            MarkRenderDirty();
        }

        /// <summary>1 = full ring; 0 = empty. Drains clockwise from the top.</summary>
        public float FillAmount
        {
            get => _fillAmount;
            set { _fillAmount = Mathf.Clamp01(value); MarkRenderDirty(); }
        }

        public Color DefaultGoldColorTop
        {
            get { CacheGoldDefaultsIfNeeded(); return _defaultGoldTop; }
        }

        public Color DefaultGoldColorBot
        {
            get { CacheGoldDefaultsIfNeeded(); return _defaultGoldBot; }
        }

        /// <summary>Restores metallic gold colors cached from the Inspector defaults.</summary>
        public void RestoreDefaultGoldColors()
        {
            CacheGoldDefaultsIfNeeded();
            _goldColorTop = _defaultGoldTop;
            _goldColorBot = _defaultGoldBot;
            MarkRenderDirty();
        }

        /// <summary>Sets gold metal gradient (e.g. gold → red urgency shift during countdown).</summary>
        public void SetGoldColors(Color top, Color bot)
        {
            _goldColorTop = top;
            _goldColorBot = bot;
            MarkRenderDirty();
        }

        private void CacheGoldDefaultsIfNeeded()
        {
            if (_goldDefaultsCached)
                return;
            _defaultGoldTop     = _goldColorTop;
            _defaultGoldBot     = _goldColorBot;
            _goldDefaultsCached = true;
        }

        /// <summary>
        /// Directly sets the ring material on the CanvasRenderer, bypassing the IMaterialModifier chain.
        /// Avoids calling base.materialForRendering to prevent stencil material caching issues in Edit Mode.
        /// </summary>
        protected override void UpdateMaterial()
        {
            if (!IsActive())
                return;

            if (!EnsureRingMaterial())
            {
                base.UpdateMaterial();
                return;
            }

            ApplyUniforms(_ringMaterial);

            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(_ringMaterial, 0);
            canvasRenderer.SetTexture(mainTexture);
        }

        /// <summary>Explicit quad so the ring renders regardless of base-class mesh path.</summary>
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
            CacheGoldDefaultsIfNeeded();
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
            UnityEngine.Canvas.ForceUpdateCanvases();
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

        private bool EnsureRingMaterial()
        {
            Shader shader = UiAvatarShaders.ResolveRingSdf(_shader);
            if (shader == null)
                return false;

            if (_shader == null)
                _shader = shader;

            if (_ringMaterial == null || _ringMaterial.shader != shader)
            {
                CleanupMaterial();
                _ringMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            return true;
        }

        private void CleanupMaterial()
        {
            if (_ringMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_ringMaterial);
            else
                DestroyImmediate(_ringMaterial);
            _ringMaterial = null;
        }

        private void ApplyUniforms(Material mat)
        {
            if (mat == null)
                return;

            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                rect = rectTransform.rect;

            float minSide = Mathf.Min(rect.width, rect.height);
            float outerR  = _outerRadiusPx > 0f
                ? _outerRadiusPx
                : minSide * 0.5f - 0.5f; // inset from quad edge — avoids square-mesh corner artifacts
            if (outerR <= 0f)
                outerR = 64f;

            mat.SetFloat(StrokeWidthPxId,  _strokeWidthPx);
            mat.SetFloat(OuterRadiusPxId,  outerR);
            // Pass actual pixel dimensions so the shader can convert UV → pixel space,
            // ensuring a geometrically round circle even on non-square rects.
            mat.SetVector(RectSizeId, new Vector2(rect.width, rect.height));
            mat.SetFloat(FillAmountId,     _look == RingLook.Chrome ? 1f : _fillAmount);
            mat.SetFloat(RingLookId,       _look == RingLook.Chrome ? 0f : 1f);
            mat.SetColor(ChromeColorTopId, _chromeColorTop);
            mat.SetColor(ChromeColorBotId, _chromeColorBot);
            mat.SetColor(GoldColorTopId,   _goldColorTop);
            mat.SetColor(GoldColorBotId,   _goldColorBot);
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
