using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Fixed three-layer chip stack (Chip_0 back → Chip_2 front).
    /// Shows up to three chips from the bet breakdown using chip1 / chip5 / chip25 sprites.
    /// </summary>
    public class ChipStackView : MonoBehaviour
    {
        private const int MaxStackChips = 3;

        [SerializeField] private Image  _chip0;
        [SerializeField] private Image  _chip1;
        [SerializeField] private Image  _chip2;
        [SerializeField] private Sprite _sprite1;
        [SerializeField] private Sprite _sprite5;
        [SerializeField] private Sprite _sprite25;

        public RectTransform StackRoot => (RectTransform)transform;

        private void Awake() => EnsureRefs();

        public void SetAmount(int amount)
        {
            EnsureRefs();
            List<int> denoms = ChipBreakdown.BreakDown(
                amount, MaxStackChips, ChipBreakdown.StackDenominations);

            Image[] slots = { _chip0, _chip1, _chip2 };
            for (int i = 0; i < slots.Length; i++)
            {
                Image img = slots[i];
                if (img == null) continue;

                if (i < denoms.Count)
                {
                    img.sprite         = SpriteFor(denoms[i]);
                    img.color          = Color.white;
                    img.preserveAspect = true;
                    img.gameObject.SetActive(true);
                }
                else
                {
                    img.gameObject.SetActive(false);
                }
            }
        }

        public void Clear()
        {
            EnsureRefs();
            foreach (Image img in new[] { _chip0, _chip1, _chip2 })
            {
                if (img != null)
                    img.gameObject.SetActive(false);
            }
        }

        public Sprite SpriteForDenomination(int denomination) => SpriteFor(denomination);

        public Sprite SpriteForAmount(int amount)
            => SpriteFor(ChipBreakdown.LargestDenomination(amount, ChipBreakdown.StackDenominations));

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
