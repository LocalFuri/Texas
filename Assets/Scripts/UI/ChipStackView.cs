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

        public const float ChipSize           = 38f;
        /// <summary>Rendered chip diameter — layout math stays on <see cref="ChipSize"/>.</summary>
        public const float ChipDisplaySize    = ChipSize * 1.25f;
        public const float ColumnGapX         = 28f;
        public const float MaxStackOverlapY   = 4f;
        private const float DefaultStackOverlapY = 2f;

        private static TableLayoutManager _cachedLayout;

        /// <summary>Worst-case width (three single-chip columns).</summary>
        public static float MaxLayoutWidth  => ChipSize * 3f + ColumnGapX * 2f;

        /// <summary>Worst-case height (three identical chips stacked at max overlap).</summary>
        public static float MaxLayoutHeight => ChipSize + MaxStackOverlapY * 2f;

        [SerializeField] private Image  _chip0;
        [SerializeField] private Image  _chip1;
        [SerializeField] private Image  _chip2;
        [SerializeField] private Sprite _sprite1;
        [SerializeField] private Sprite _sprite5;
        [SerializeField] private Sprite _sprite25;
        [SerializeField] private Sprite _sprite100;
        [SerializeField] private Sprite _sprite500;

        private readonly List<Image> _slots = new List<Image>();
        private int    _lastAmount;
        private bool   _lastExactMode;

        public RectTransform StackRoot => (RectTransform)transform;

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
            _lastAmount = 0;
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

            List<DenomGroup> groups  = GroupByDenomination(denoms);
            int              maxStack = 1;
            foreach (DenomGroup g in groups)
            {
                if (g.Count > maxStack)
                    maxStack = g.Count;
            }

            int   colCount     = groups.Count;
            float overlapY     = ResolveStackOverlapY();
            float layoutWidth  = colCount * ChipSize + (colCount - 1) * ColumnGapX;
            float layoutHeight = ChipSize + (maxStack - 1) * overlapY;
            float baseY        = -((maxStack - 1) * overlapY) * 0.5f;

            var stackRt = StackRoot;
            stackRt.sizeDelta = new Vector2(layoutWidth, layoutHeight);

            int slotIndex = 0;
            for (int col = 0; col < groups.Count; col++)
            {
                DenomGroup group = groups[col];
                float colCenterX = -layoutWidth * 0.5f + ChipSize * 0.5f + col * (ChipSize + ColumnGapX);

                for (int j = 0; j < group.Count && slotIndex < _slots.Count; j++)
                {
                    Image img = _slots[slotIndex];
                    if (img == null || !img.gameObject.activeSelf)
                    {
                        slotIndex++;
                        continue;
                    }

                    var chipRt = (RectTransform)img.transform;
                    chipRt.anchorMin        = new Vector2(0.5f, 0.5f);
                    chipRt.anchorMax        = new Vector2(0.5f, 0.5f);
                    chipRt.pivot            = new Vector2(0.5f, 0.5f);
                    chipRt.sizeDelta        = new Vector2(ChipDisplaySize, ChipDisplaySize);
                    chipRt.anchoredPosition = new Vector2(colCenterX, baseY + j * overlapY);
                    chipRt.SetSiblingIndex(slotIndex);

                    slotIndex++;
                }
            }
        }

        private static float ResolveStackOverlapY()
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
                ? _cachedLayout.StackOverlapY
                : DefaultStackOverlapY;
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
