using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Procedural annulus ring — no sprite texture. Supports radial countdown fill.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public class AvatarRingGraphic : MaskableGraphic
    {
        public enum RingLook
        {
            Chrome,
            Gold
        }

        [SerializeField] private RingLook _look = RingLook.Chrome;
        [SerializeField, Range(0.5f, 0.95f)]
        private float _innerRadiusRatio = 0.753f;
        [SerializeField, Range(0f, 1f)]
        private float _fillAmount = 1f;
        [SerializeField, Range(16, 128)]
        private int _segmentCount = 96;

        public RingLook Look
        {
            get => _look;
            set { _look = value; SetVerticesDirty(); }
        }

        public float InnerRadiusRatio
        {
            get => _innerRadiusRatio;
            set { _innerRadiusRatio = Mathf.Clamp(value, 0.5f, 0.95f); SetVerticesDirty(); }
        }

        /// <summary>1 = full ring; 0 = hidden. Fills clockwise from the top.</summary>
        public float FillAmount
        {
            get => _fillAmount;
            set { _fillAmount = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        public override Texture mainTexture => s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            EnsureRendererSettings();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureRendererSettings();
            SetVerticesDirty();
        }

        private void EnsureRendererSettings()
        {
            if (canvasRenderer != null)
                canvasRenderer.cullTransparentMesh = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_fillAmount <= 0f)
                return;

            float outer = GetOuterRadius();
            if (outer <= 0f)
                return;

            float inner = outer * _innerRadiusRatio;
            if (inner >= outer)
                return;

            Vector2 center = GetRingCenter();

            float fillSweep = _fillAmount * 360f;
            int segments = Mathf.Max(4, Mathf.CeilToInt(_segmentCount * _fillAmount));

            for (int i = 0; i < segments; i++)
            {
                float deg0 = (i / (float)segments) * fillSweep;
                float deg1 = ((i + 1) / (float)segments) * fillSweep;
                AddSectorSubdivided(vh, center, inner, outer, deg0, deg1);
            }
        }

        private float GetOuterRadius()
        {
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                rect = rectTransform.rect;
            return Mathf.Min(rect.width, rect.height) * 0.5f;
        }

        private Vector2 GetRingCenter()
        {
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                rect = rectTransform.rect;
            return rect.center;
        }

        /// <summary>Two radial slices per sector so edge vertices carry full alpha.</summary>
        private void AddSectorSubdivided(VertexHelper vh, Vector2 center, float innerR, float outerR, float deg0, float deg1)
        {
            float midR = Mathf.Lerp(innerR, outerR, 0.5f);
            AddQuad(vh, center, innerR, midR, deg0, deg1, 0f, 0.5f);
            AddQuad(vh, center, midR, outerR, deg0, deg1, 0.5f, 1f);
        }

        private void AddQuad(VertexHelper vh, Vector2 center, float innerR, float outerR, float deg0, float deg1, float t0, float t1)
        {
            Vector2 d0 = DirectionFromTop(deg0);
            Vector2 d1 = DirectionFromTop(deg1);

            Color cInner0 = SampleColor(deg0, t0);
            Color cInner1 = SampleColor(deg1, t0);
            Color cOuter0 = SampleColor(deg0, t1);
            Color cOuter1 = SampleColor(deg1, t1);

            int idx = vh.currentVertCount;
            vh.AddVert(center + d0 * innerR, cInner0, Vector2.zero);
            vh.AddVert(center + d0 * outerR, cOuter0, Vector2.zero);
            vh.AddVert(center + d1 * outerR, cOuter1, Vector2.zero);
            vh.AddVert(center + d1 * innerR, cInner1, Vector2.zero);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx);
        }

        private static Vector2 DirectionFromTop(float clockwiseDeg)
        {
            float rad = clockwiseDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        private Color SampleColor(float clockwiseDeg, float radialT)
        {
            Color c = _look == RingLook.Gold
                ? SampleGoldRgb(clockwiseDeg, radialT)
                : SampleChromeRgb(clockwiseDeg, radialT);
            c.a = 1f;
            return c;
        }

        private static Color SampleGoldRgb(float clockwiseDeg, float radialT)
        {
            float light = Mathf.Clamp01(0.52f + 0.48f * Mathf.Cos((clockwiseDeg - 50f) * Mathf.Deg2Rad));
            Color dark   = new Color(0.62f, 0.44f, 0.06f, 1f);
            Color bright = new Color(1.00f, 0.86f, 0.22f, 1f);
            float rim    = radialT > 0.82f ? 1.1f : 1f;
            return Color.Lerp(dark, bright, light * rim);
        }

        private static Color SampleChromeRgb(float clockwiseDeg, float radialT)
        {
            float angle = (Mathf.PI * 0.5f) - clockwiseDeg * Mathf.Deg2Rad;
            const float kLightAngle  =  2.094f;
            const float kBounceAngle = -1.047f;

            float da   = WrapAngle(angle - kLightAngle);
            float angD = Mathf.Cos(da) * 0.5f + 0.5f;
            float bv   = ChromeBevelProfile(radialT);
            float baseV = Mathf.Clamp01((0.10f + angD * 1.06f) * bv);
            float r = baseV, g = baseV, b = baseV;

            float arcUL  = Mathf.Exp(-(da * da) / (2f * 0.50f * 0.50f));
            float arcULr = Mathf.Clamp01((radialT - 0.60f) * 4.0f) * Mathf.Clamp01((0.95f - radialT) * 4.0f);
            float sheenUL = arcUL * arcULr * 0.55f;
            r = Mathf.Lerp(r, 0.96f, sheenUL);
            g = Mathf.Lerp(g, 0.96f, sheenUL);
            b = Mathf.Lerp(b, 0.96f, sheenUL);

            float angS1 = Mathf.Exp(-(da * da) / (2f * 0.09f * 0.09f));
            float dRO   = radialT - 0.88f;
            float spec1 = angS1 * Mathf.Exp(-(dRO * dRO) / (2f * 0.07f * 0.07f));
            r = Mathf.Lerp(r, 1.00f, spec1 * 0.98f);
            g = Mathf.Lerp(g, 1.00f, spec1 * 0.98f);
            b = Mathf.Lerp(b, 1.00f, spec1 * 0.98f);

            float da2    = WrapAngle(angle - kBounceAngle);
            float arcLR  = Mathf.Exp(-(da2 * da2) / (2f * 0.35f * 0.35f));
            float arcLRr = Mathf.Clamp01((radialT - 0.18f) * 4.0f) * Mathf.Clamp01((0.45f - radialT) * 4.0f);
            float sheenLR = arcLR * arcLRr * 0.28f;
            r = Mathf.Lerp(r, 0.78f, sheenLR);
            g = Mathf.Lerp(g, 0.78f, sheenLR);
            b = Mathf.Lerp(b, 0.78f, sheenLR);

            const float kSig2 = 2f * 0.045f * 0.045f;
            float ds1 = WrapAngle(angle - 0.30f);
            float ds2 = WrapAngle(angle - 0.90f);
            float ds3 = WrapAngle(angle - 1.45f);
            float ds4 = WrapAngle(angle - 1.95f);
            float sMax = Mathf.Max(
                Mathf.Max(Mathf.Exp(-ds1 * ds1 / kSig2), Mathf.Exp(-ds2 * ds2 / kSig2)),
                Mathf.Max(Mathf.Exp(-ds3 * ds3 / kSig2), Mathf.Exp(-ds4 * ds4 / kSig2)));
            float outerFace = Mathf.Clamp01((radialT - 0.64f) * 12.5f) * Mathf.Clamp01((0.86f - radialT) * 12.5f);
            float streakMix = sMax * outerFace * Mathf.Clamp01((angD - 0.30f) * 5f)
                            * (1f - Mathf.Clamp01(spec1 * 3f)) * 0.30f;
            r = Mathf.Lerp(r, 0.88f, streakMix);
            g = Mathf.Lerp(g, 0.88f, streakMix);
            b = Mathf.Lerp(b, 0.88f, streakMix);

            return new Color(r, g, Mathf.Min(1f, b * 1.02f), 1f);
        }

        private static float ChromeBevelProfile(float rPos)
        {
            float[] ts = { 0.00f, 0.18f, 0.38f, 0.50f, 0.62f, 0.75f, 0.88f, 1.00f };
            float[] vs = { 0.50f, 0.58f, 0.28f, 0.20f, 0.30f, 0.70f, 1.00f, 0.65f };

            if (rPos <= ts[0]) return vs[0];
            if (rPos >= ts[ts.Length - 1]) return vs[vs.Length - 1];
            for (int i = 0; i < ts.Length - 1; i++)
            {
                if (rPos < ts[i + 1])
                {
                    float seg = (rPos - ts[i]) / (ts[i + 1] - ts[i]);
                    return Mathf.Lerp(vs[i], vs[i + 1], seg);
                }
            }
            return 0.5f;
        }

        private static float WrapAngle(float da)
        {
            while (da >  Mathf.PI) da -= 2f * Mathf.PI;
            while (da < -Mathf.PI) da += 2f * Mathf.PI;
            return da;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureRendererSettings();
            SetVerticesDirty();
        }
#endif
    }
}
