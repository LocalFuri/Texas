using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Bet chip graphics — up to three chips from the breakdown.
    /// Identical denominations stack vertically with slight overlap;
    /// different denominations sit in horizontal columns.
    /// </summary>
    public class ChipStackView : MonoBehaviour
    {
        private const int MaxStackChips = 3;

        public const float ChipSize       = 38f;
        public const float ColumnGapX     = 28f;
        public const float StackOverlapY  = 8f;

        /// <summary>Worst-case width (three single-chip columns).</summary>
        public static float MaxLayoutWidth  => ChipSize * 3f + ColumnGapX * 2f;

        /// <summary>Worst-case height (three identical chips stacked).</summary>
        public static float MaxLayoutHeight => ChipSize + StackOverlapY * 2f;

        [SerializeField] private Image  _chip0;
        [SerializeField] private Image  _chip1;
        [SerializeField] private Image  _chip2;
        [SerializeField] private Sprite _sprite1;
        [SerializeField] private Sprite _sprite5;
        [SerializeField] private Sprite _sprite25;

        private int _lastAmount;

        public RectTransform StackRoot => (RectTransform)transform;

        private void Awake() => EnsureRefs();

        public void SetAmount(int amount)
        {
            EnsureRefs();
            _lastAmount = amount;

            List<int> denoms = ChipBreakdown.BreakDown(
                amount, MaxStackChips, ChipBreakdown.StackDenominations);

            Image[] slots = { _chip0, _chip1, _chip2 };
            int slotIndex = 0;

            foreach (DenomGroup group in GroupByDenomination(denoms))
            {
                for (int i = 0; i < group.Count && slotIndex < slots.Length; i++)
                {
                    Image img = slots[slotIndex++];
                    if (img == null) continue;

                    img.sprite         = SpriteFor(group.Denomination);
                    img.color          = Color.white;
                    img.preserveAspect = true;
                    img.gameObject.SetActive(true);
                }
            }

            for (; slotIndex < slots.Length; slotIndex++)
            {
                if (slots[slotIndex] != null)
                    slots[slotIndex].gameObject.SetActive(false);
            }

            LayoutChips(denoms);
        }

        public void Clear()
        {
            EnsureRefs();
            _lastAmount = 0;
            foreach (Image img in new[] { _chip0, _chip1, _chip2 })
            {
                if (img != null)
                    img.gameObject.SetActive(false);
            }
        }

        /// <summary>Re-applies layout after container resize (e.g. Apply Layout in editor).</summary>
        public void RefreshLayout()
        {
            if (_lastAmount > 0)
                SetAmount(_lastAmount);
        }

        public Sprite SpriteForDenomination(int denomination) => SpriteFor(denomination);

        public Sprite SpriteForAmount(int amount)
            => SpriteFor(ChipBreakdown.LargestDenomination(amount, ChipBreakdown.StackDenominations));

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

            int colCount = groups.Count;
            float layoutWidth  = colCount * ChipSize + (colCount - 1) * ColumnGapX;
            float layoutHeight = ChipSize + (maxStack - 1) * StackOverlapY;
            float baseY        = -((maxStack - 1) * StackOverlapY) * 0.5f;

            var stackRt = StackRoot;
            stackRt.sizeDelta = new Vector2(layoutWidth, layoutHeight);

            Image[] slots = { _chip0, _chip1, _chip2 };
            int slotIndex = 0;

            for (int col = 0; col < groups.Count; col++)
            {
                DenomGroup group = groups[col];
                float colCenterX = -layoutWidth * 0.5f + ChipSize * 0.5f + col * (ChipSize + ColumnGapX);

                for (int j = 0; j < group.Count && slotIndex < slots.Length; j++)
                {
                    Image img = slots[slotIndex];
                    if (img == null || !img.gameObject.activeSelf)
                    {
                        slotIndex++;
                        continue;
                    }

                    var chipRt = (RectTransform)img.transform;
                    chipRt.anchorMin        = new Vector2(0.5f, 0.5f);
                    chipRt.anchorMax        = new Vector2(0.5f, 0.5f);
                    chipRt.pivot            = new Vector2(0.5f, 0.5f);
                    chipRt.sizeDelta        = new Vector2(ChipSize, ChipSize);
                    chipRt.anchoredPosition = new Vector2(colCenterX, baseY + j * StackOverlapY);
                    chipRt.SetSiblingIndex(slotIndex);

                    slotIndex++;
                }
            }
        }

        private Sprite SpriteFor(int denomination)
        {
            return denomination switch
            {
                25 => _sprite25,
                5  => _sprite5,
                1  => _sprite1,
                _  => _sprite1
            };
        }

        private void EnsureRefs()
        {
            if (_chip0 == null) _chip0 = transform.Find("Chip_0")?.GetComponent<Image>();
            if (_chip1 == null) _chip1 = transform.Find("Chip_1")?.GetComponent<Image>();
            if (_chip2 == null) _chip2 = transform.Find("Chip_2")?.GetComponent<Image>();
        }
    }
}
