using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Vertical two-tone fill for winner badge backdrop.</summary>
    public class ActionBadgeGradientImage : Image
    {
        [SerializeField] private Color _topColor    = new Color(1f, 0.85f, 0.2f, 0.42f);
        [SerializeField] private Color _bottomColor = new Color(0.65f, 0.55f, 0.13f, 0.58f);

        public void SetColors(Color top, Color bottom)
        {
            _topColor    = top;
            _bottomColor = bottom;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh);
            if (vh.currentVertCount == 0)
                return;

            UIVertex vert = default;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                minY = Mathf.Min(minY, vert.position.y);
                maxY = Mathf.Max(maxY, vert.position.y);
            }

            float range = maxY - minY;
            if (range <= 0.001f)
                return;

            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                float t = Mathf.InverseLerp(minY, maxY, vert.position.y);
                vert.color = Color.Lerp(_bottomColor, _topColor, t);
                vh.SetUIVertex(vert, i);
            }
        }
    }
}
