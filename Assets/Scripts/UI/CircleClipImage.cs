using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// UI Image clipped to a circle using the element's own square rect as the mask.
    /// </summary>
    [ExecuteAlways]
    public class CircleClipImage : Image
    {
        private static readonly int MaskCenterId = Shader.PropertyToID("_MaskCenter");
        private static readonly int MaskRadiusId = Shader.PropertyToID("_MaskRadius");
        private static readonly int FeatherId    = Shader.PropertyToID("_Feather");

        private Material _clipMaterial;

        [SerializeField] private Shader _shader;
        [SerializeField, Range(0.5f, 4f)] private float _featherPx = 1f;

        public float FeatherPx
        {
            get => _featherPx;
            set { _featherPx = Mathf.Clamp(value, 0.5f, 4f); MarkRenderDirty(); }
        }

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
            EnsureClipMaterial();
            EnsureRendererSettings();
            base.OnEnable();
            MarkRenderDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CleanupMaterial();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            MarkRenderDirty();
        }
#endif

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

            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f)
                radius = 1f;

            mat.SetVector(MaskCenterId, Vector4.zero);
            mat.SetFloat(MaskRadiusId, radius);
            mat.SetFloat(FeatherId, _featherPx / Mathf.Max(radius, 1f));
        }
    }
}
