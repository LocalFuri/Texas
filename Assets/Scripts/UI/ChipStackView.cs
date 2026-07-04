using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Chip graphics — identical denominations stack vertically with slight overlap;
    /// different denominations sit in horizontal columns.
    /// Bet stacks use up to three chips ({25,5,1}); pot stacks use the full breakdown.
    /// </summary>
    public class ChipStackView : MonoBehaviour
    {
        private const int MaxBetStackChips = 3;

        public const float DefaultChipSize      = 38f;
        public const float DefaultColumnGapX    = 28f;
        private const float ChipDisplayScale    = 1.25f;
        /// <summary>Rendered chip diameter — layout math uses <see cref="ResolveChipSize"/>.</summary>
        public static float ResolveChipDisplaySize() => ResolveChipSize() * ChipDisplayScale;
        public const float MaxStackOverlapY   = 4f;
        private const float DefaultStackOverlapY = 2f;

        private static TableLayoutManager _cachedLayout;

        /// <summary>Worst-case width (three single-chip columns).</summary>
        public static float MaxLayoutWidth  => ResolveChipSize() * 3f + ResolveColumnGapX() * 2f;

        /// <summary>Worst-case height (three identical chips stacked at max overlap).</summary>
        public static float MaxLayoutHeight => ResolveChipSize() + MaxStackOverlapY * 2f;

        /// <summary>Layout chip diameter — from TableLayoutManager Chip Size, else default.</summary>
        public static float ResolveChipSize()
        {
            if (_cachedLayout == null)
            {
#if UNITY_2023_1_OR_NEWER
                _cachedLayout = Object.FindFirstObjectByType<TableLayoutManager>(
                    FindObjectsInactive.Include);
#else
                _cachedLayout = Object.FindObjectOfType<TableLayoutManager>();
#endif
            }

            return _cachedLayout != null
                ? _cachedLayout.ChipSize
                : DefaultChipSize;
        }

        [SerializeField] private Image  _chip0;
        [SerializeField] private Image  _chip1;
        [SerializeField] private Image  _chip2;
        [SerializeField] private Sprite _sprite1;
        [SerializeField] private Sprite _sprite5;
        [SerializeField] private Sprite _sprite25;
        [SerializeField] private Sprite _sprite100;
        [SerializeField] private Sprite _sprite500;

        [Tooltip("When set, uses Custom Stack Overlap Y instead of TableLayoutManager Stack Overlap Y.")]
        [SerializeField] private bool  _useCustomStackOverlap;
        [SerializeField, Range(1f, 12f)] private float _customStackOverlapY = DefaultStackOverlapY;

        [Tooltip("When set, uses Custom Column Gap X instead of TableLayoutManager Chip Column Gap X.")]
        [SerializeField] private bool _useCustomColumnGap;
        [SerializeField, Range(0f, 48f)] private float _customColumnGapX = DefaultColumnGapX;

        private readonly List<Image> _slots = new List<Image>();
        private int    _lastAmount;
        private bool   _lastExactMode;
        private float  _bottomLocalY;

        public RectTransform StackRoot => (RectTransform)transform;

        /// <summary>Bottom edge Y in stack-local space (centre-anchored root).</summary>
        public float GetBottomLocalY() => _bottomLocalY;

        private void Awake() => EnsureRefs();

        /// <summary>Approximate bet stack (max three chips, {25,5,1}).</summary>
        public void SetAmount(int amount)
        {
            EnsureRefs();
            _lastAmount    = amount;
            _lastExactMode = false;

            List<int> denoms = ChipBreakdown.BreakDown(
                amount, MaxBetStackChips, ChipBreakdown.StackDenominations);

            ApplyBreakdown(denoms);
        }

        /// <summary>Exact minimum chip count for the amount using all denominations.</summary>
        public void SetExactAmount(int amount)
        {
            EnsureRefs();
            _lastAmount    = amount;
            _lastExactMode = true;

            List<int> denoms = ChipBreakdown.BreakDown(
                amount, int.MaxValue, ChipBreakdown.Denominations);

            ApplyBreakdown(denoms);
        }

        public void Clear()
        {
            EnsureRefs();
            _lastAmount     = 0;
            _bottomLocalY   = 0f;
            foreach (Image img in _slots)
            {
                if (img != null)
                    img.gameObject.SetActive(false);
            }
        }

        /// <summary>Re-applies layout after container resize (e.g. Apply Layout in editor).</summary>
        public void RefreshLayout()
        {
            if (_lastAmount <= 0)
                return;

            if (_lastExactMode)
                SetExactAmount(_lastAmount);
            else
                SetAmount(_lastAmount);
        }

        /// <summary>Overrides vertical chip step for this stack (e.g. pot stack from UIManager).</summary>
        public void SetStackOverlapY(float overlapY)
        {
            _useCustomStackOverlap = true;
            _customStackOverlapY   = Mathf.Clamp(overlapY, 1f, 12f);
            RefreshLayout();
        }

        /// <summary>Overrides horizontal gap between denomination columns for this stack only.</summary>
        public void SetColumnGapX(float gapX)
        {
            _useCustomColumnGap = true;
            _customColumnGapX   = Mathf.Clamp(gapX, 0f, 48f);
            RefreshLayout();
        }

        /// <summary>Uses TableLayoutManager Chip Column Gap X again (e.g. pot stack default).</summary>
        public void ClearColumnGapOverride()
        {
            if (!_useCustomColumnGap)
                return;

            _useCustomColumnGap = false;
            RefreshLayout();
        }

        public Sprite SpriteForDenomination(int denomination) => SpriteFor(denomination);

        public Sprite SpriteForAmount(int amount)
            => SpriteFor(ChipBreakdown.LargestDenomination(amount, ChipBreakdown.StackDenominations));

        private void ApplyBreakdown(List<int> denoms)
        {
            EnsureSlotCount(denoms.Count);

            int slotIndex = 0;
            foreach (DenomGroup group in GroupByDenomination(denoms))
            {
                for (int i = 0; i < group.Count && slotIndex < _slots.Count; i++)
                {
                    Image img = _slots[slotIndex++];
                    if (img == null) continue;

                    img.sprite         = SpriteFor(group.Denomination);
                    img.color          = Color.white;
                    img.preserveAspect = true;
                    img.raycastTarget  = false;
                    img.gameObject.SetActive(true);
                }
            }

            for (; slotIndex < _slots.Count; slotIndex++)
            {
                if (_slots[slotIndex] != null)
                    _slots[slotIndex].gameObject.SetActive(false);
            }

            LayoutChips(denoms);
        }

        private void EnsureSlotCount(int required)
        {
            EnsureRefs();
            while (_slots.Count < required)
            {
                int index = _slots.Count;
                string name = $"Chip_{index}";

                Transform existing = transform.Find(name);
                Image img;
                if (existing != null)
                {
                    img = existing.GetComponent<Image>();
                }
                else
                {
                    var chipGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    chipGo.transform.SetParent(transform, false);
                    img = chipGo.GetComponent<Image>();
                    img.type           = Image.Type.Simple;
                    img.preserveAspect = true;
                    img.raycastTarget  = false;
                }

                _slots.Add(img);
            }
        }

        private readonly struct DenomGroup
        {
            public readonly int Denomination;
            public readonly int Count;

            public DenomGroup(int denomination, int count)
            {
                Denomination = denomination;
                Count        = count;
            }
        }

        private static List<DenomGroup> GroupByDenomination(List<int> denoms)
        {
            var groups = new List<DenomGroup>();
            foreach (int d in denoms)
            {
                if (groups.Count > 0 && groups[^1].Denomination == d)
                {
                    DenomGroup last = groups[^1];
                    groups[^1] = new DenomGroup(last.Denomination, last.Count + 1);
                }
                else
                {
                    groups.Add(new DenomGroup(d, 1));
                }
            }

            return groups;
        }

        private void LayoutChips(List<int> denoms)
        {
            if (denoms.Count == 0)
                return;

            List<DenomGroup> groups   = GroupByDenomination(denoms);
            int              maxStack = 1;
            foreach (DenomGroup g in groups)
            {
                if (g.Count > maxStack)
                    maxStack = g.Count;
            }

            int   colCount    = groups.Count;
            float chipSize    = ResolveChipSize();
            float overlapY    = ResolveStackOverlapY();
            float columnGapX  = ResolveColumnGapXForStack();
            float layoutWidth = colCount * chipSize + (colCount - 1) * columnGapX;
            float displaySize = ResolveChipDisplaySize();

            var stackRt = StackRoot;
            stackRt.sizeDelta = new Vector2(layoutWidth, chipSize + (maxStack - 1) * overlapY);

            const float baselineY = 0f;
            int slotIndex = 0;

            for (int col = 0; col < groups.Count; col++)
            {
                DenomGroup group     = groups[col];
                float      colCenterX = -layoutWidth * 0.5f + chipSize * 0.5f + col * (chipSize + columnGapX);
                float      colCenterY = baselineY;

                for (int j = 0; j < group.Count && slotIndex < _slots.Count; j++)
                {
                    Image img = _slots[slotIndex];
                    if (img == null || !img.gameObject.activeSelf)
                    {
                        slotIndex++;
                        continue;
                    }

                    var chipRt = (RectTransform)img.transform;
                    chipRt.anchorMin = new Vector2(0.5f, 0.5f);
                    chipRt.anchorMax = new Vector2(0.5f, 0.5f);
                    chipRt.pivot     = new Vector2(0.5f, 0.5f);
                    chipRt.sizeDelta = new Vector2(displaySize, displaySize);

                    if (j == 0)
                        colCenterY = SolveCenterYForVisualBottom(chipRt, img, colCenterX, baselineY);
                    else
                        colCenterY += overlapY;

                    chipRt.anchoredPosition = new Vector2(colCenterX, colCenterY);
                    chipRt.SetSiblingIndex(slotIndex);

                    slotIndex++;
                }
            }

            float minBottom = float.MaxValue;
            float maxTop    = float.MinValue;
            foreach (Image img in _slots)
            {
                if (img == null || !img.gameObject.activeSelf)
                    continue;

                var chipRt = (RectTransform)img.transform;
                minBottom = Mathf.Min(minBottom, GetChipVisualBottomLocalY(chipRt, img));
                maxTop    = Mathf.Max(maxTop, GetChipVisualTopLocalY(chipRt, img));
            }

            if (minBottom <= maxTop)
            {
                float contentCenterY = (minBottom + maxTop) * 0.5f;
                float layoutHeight   = maxTop - minBottom;

                stackRt.sizeDelta = new Vector2(layoutWidth, layoutHeight);

                foreach (Image img in _slots)
                {
                    if (img == null || !img.gameObject.activeSelf)
                        continue;

                    var chipRt = (RectTransform)img.transform;
                    chipRt.anchoredPosition -= new Vector2(0f, contentCenterY);
                }
            }

            UpdateBottomLocalY();
        }

        /// <summary>Center Y so the sprite's painted bottom sits on <paramref name="targetBottomY"/>.</summary>
        private static float SolveCenterYForVisualBottom(
            RectTransform chipRt, Image img, float centerX, float targetBottomY)
        {
            chipRt.anchoredPosition = new Vector2(centerX, 0f);
            float bottom = GetChipVisualBottomLocalY(chipRt, img);
            return chipRt.anchoredPosition.y + (targetBottomY - bottom);
        }

        private void UpdateBottomLocalY()
        {
            float minBottom = float.MaxValue;
            bool  any       = false;

            foreach (Image img in _slots)
            {
                if (img == null || !img.gameObject.activeSelf)
                    continue;

                any = true;
                var chipRt = (RectTransform)img.transform;
                float bottom = GetChipVisualBottomLocalY(chipRt, img);
                if (bottom < minBottom)
                    minBottom = bottom;
            }

            _bottomLocalY = any ? minBottom : 0f;
        }

        private static float GetChipVisualTopLocalY(RectTransform chipRt, Image img)
        {
            float renderedH = GetRenderedHalfHeight(img) * 2f;
            Sprite sprite   = img.sprite;
            if (sprite == null || sprite.bounds.size.y <= 0f)
                return chipRt.anchoredPosition.y + renderedH * 0.5f;

            float scale = renderedH / sprite.bounds.size.y;
            return chipRt.anchoredPosition.y + sprite.bounds.max.y * scale;
        }

        private static float GetChipVisualBottomLocalY(RectTransform chipRt, Image img)
        {
            float renderedH = GetRenderedHalfHeight(img) * 2f;
            Sprite sprite     = img.sprite;
            if (sprite == null || sprite.bounds.size.y <= 0f)
                return chipRt.anchoredPosition.y - renderedH * 0.5f;

            float scale  = renderedH / sprite.bounds.size.y;
            return chipRt.anchoredPosition.y + sprite.bounds.min.y * scale;
        }

        private static float GetRenderedHalfHeight(Image img)
        {
            var rt = (RectTransform)img.transform;
            float rectW = rt.rect.width;
            float rectH = rt.rect.height;
            if (rectW <= 0f) rectW = rt.sizeDelta.x;
            if (rectH <= 0f) rectH = rt.sizeDelta.y;

            Sprite sprite = img.sprite;
            if (sprite == null)
                return rectH * 0.5f;

            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float rectAspect   = rectW / rectH;
            float renderedH    = spriteAspect >= rectAspect
                ? rectH
                : rectW / spriteAspect;

            return renderedH * 0.5f;
        }

        private float ResolveStackOverlapY()
        {
            if (_useCustomStackOverlap)
                return _customStackOverlapY;

            if (_cachedLayout == null)
            {
#if UNITY_2023_1_OR_NEWER
                _cachedLayout = Object.FindFirstObjectByType<TableLayoutManager>(
                    FindObjectsInactive.Include);
#else
                _cachedLayout = Object.FindObjectOfType<TableLayoutManager>();
#endif
            }

            return _cachedLayout != null
                ? _cachedLayout.StackOverlapY
                : DefaultStackOverlapY;
        }

        private float ResolveColumnGapXForStack()
        {
            if (_useCustomColumnGap)
                return _customColumnGapX;

            return ResolveColumnGapX();
        }

        /// <summary>Horizontal gap between denomination columns — from TableLayoutManager unless overridden per stack.</summary>
        public static float ResolveColumnGapX()
        {
#if UNITY_2023_1_OR_NEWER
            TableLayoutManager layout = Object.FindFirstObjectByType<TableLayoutManager>(
                FindObjectsInactive.Include);
#else
            TableLayoutManager layout = Object.FindObjectOfType<TableLayoutManager>();
#endif
            return layout != null ? layout.ChipColumnGapX : DefaultColumnGapX;
        }

        private Sprite SpriteFor(int denomination)
        {
            return denomination switch
            {
                500 => _sprite500 != null ? _sprite500 : _sprite1,
                100 => _sprite100 != null ? _sprite100 : _sprite1,
                25  => _sprite25,
                5   => _sprite5,
                1   => _sprite1,
                _   => _sprite1
            };
        }

        private void EnsureRefs()
        {
            if (_chip0 == null) _chip0 = transform.Find("Chip_0")?.GetComponent<Image>();
            if (_chip1 == null) _chip1 = transform.Find("Chip_1")?.GetComponent<Image>();
            if (_chip2 == null) _chip2 = transform.Find("Chip_2")?.GetComponent<Image>();

            if (_slots.Count == 0)
            {
                if (_chip0 != null) _slots.Add(_chip0);
                if (_chip1 != null) _slots.Add(_chip1);
                if (_chip2 != null) _slots.Add(_chip2);
            }
        }

        /// <summary>Copies chip sprites from another stack (e.g. when bootstrapping the pot stack).</summary>
        public void CopySpritesFrom(ChipStackView source)
        {
            if (source == null) return;
            _sprite1   = source._sprite1;
            _sprite5   = source._sprite5;
            _sprite25  = source._sprite25;
            _sprite100 = source._sprite100;
            _sprite500 = source._sprite500;
        }

        public void AssignHighDenominations(Sprite sprite100, Sprite sprite500)
        {
            if (sprite100 != null) _sprite100 = sprite100;
            if (sprite500 != null) _sprite500 = sprite500;
        }
    }
}
