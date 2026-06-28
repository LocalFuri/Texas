using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>Single denomination chip graphic — root of each chip prefab.</summary>
    [RequireComponent(typeof(Image))]
    public class ChipVisual : MonoBehaviour
    {
        [SerializeField] private int   _denomination = 1;
        [SerializeField] private Image _image;

        public int   Denomination => _denomination;
        public Image Image        => _image != null ? _image : (_image = GetComponent<Image>());

        public void SetDenomination(int value) => _denomination = value;

#if UNITY_EDITOR
        private void Reset()
        {
            _image = GetComponent<Image>();
        }
#endif
    }
}
