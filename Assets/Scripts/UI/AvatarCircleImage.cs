using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// UIImage clipped to a soft circle defined in the parent mask rect —
    /// works even when AspectRatioFitter enlarges the quad beyond the mask.
    /// </summary>
    [ExecuteAlways]
    public class AvatarCircleImage : Image
    {
        private static readonly int MaskCenterId = Shader.PropertyToID("_MaskCenter");
        private static readonly int MaskRadiusId = Shader.PropertyToID("_MaskRadius");
        private static readonly int FeatherId    = Shader.PropertyToID("_Feather");

        private Material      _clipMaterial;
        private RectTransform _maskRoot;

        [SerializeField] private Shader _shader;
        [SerializeField, Range(0.5f, 4f)]
        private float _featherPx = 1.5f;

        public float FeatherPx
        {
            get => _featherPx;
            set { _featherPx = Mathf.Clamp(value, 0.5f, 4f); MarkRenderDirty(); }
        }

        /// <summary>
        /// Directly sets the circle-clip material on the CanvasRenderer, bypassing the IMaterialModifier chain.
        /// base.materialForRendering gives us the canvas-level base material (with any Mask stencil applied)
        /// so we can copy those settings before assigning our custom shader.
        /// </summary>
        protected override void UpdateMaterial()
        {
            if (!IsActive())
                return;

            if (!EnsureClipMaterial())
            {
                base.UpdateMaterial();
                return;
            }

            UiAvatarShaders.ApplyMaskedCircleCanvasMaterial(_clipMaterial, base.materialForRendering);
            ApplyMaskUniforms(_clipMaterial);

            canvasRenderer.materialCount = 1;
            canvasRenderer.SetMaterial(_clipMaterial, 0);
            canvasRenderer.SetTexture(mainTexture);
        }

        protected override void OnEnable()
        {
            EnsureStretchFill();
            CacheMaskRoot();
            EnsureClipMaterial();
            EnsureRendererSettings();
            base.OnEnable();
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

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CacheMaskRoot();
            MarkRenderDirty();
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkRenderDirty();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            CacheMaskRoot();
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

        /// <summary>Repairs collapsed Avatar rects left by stale prefab/scene overrides.</summary>
        private void EnsureStretchFill()
        {
            if (gameObject.name != "Avatar")
                return;

            RectTransform rt = rectTransform;
            bool collapsed = rt.anchorMin == rt.anchorMax
                && rt.sizeDelta.sqrMagnitude < 1f;
            if (!collapsed)
                return;

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
        }

        private void CacheMaskRoot()
        {
            _maskRoot = transform.parent as RectTransform;
        }

        private bool EnsureClipMaterial()
        {
            Shader shader = UiAvatarShaders.ResolveCircleClip(_shader);
            if (shader == null)
                return false;

            if (_shader == null)
                _shader = shader;

            if (_clipMaterial == null || _clipMaterial.shader != shader)
            {
                CleanupMaterial();
                _clipMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            return true;
        }

        private void CleanupMaterial()
        {
            if (_clipMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_clipMaterial);
            else
                DestroyImmediate(_clipMaterial);
            _clipMaterial = null;
        }

        private void ApplyMaskUniforms(Material mat)
        {
            if (mat == null)
                return;

            RectTransform mask = _maskRoot != null ? _maskRoot : rectTransform;
            Rect maskRect = mask.rect;
            float radius = Mathf.Min(maskRect.width, maskRect.height) * 0.5f;
            if (radius <= 0f)
                radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.5f;
            if (radius <= 0f)
                radius = 61f;

            Vector3 maskCenterLocal = transform.InverseTransformPoint(mask.TransformPoint(Vector3.zero));
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.0001f);
            float radiusLocal = radius / scale;

            mat.SetVector(MaskCenterId, new Vector4(maskCenterLocal.x, maskCenterLocal.y, 0f, 0f));
            mat.SetFloat(MaskRadiusId, radiusLocal);
            mat.SetFloat(FeatherId, _featherPx / Mathf.Max(radiusLocal, 1f));
        }
    }
}
