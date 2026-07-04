using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>PNG action badge above the player name when a player acts or wins.</summary>
    public class ActionBadge : MonoBehaviour
    {
        public const float DisplayDurationSecs = 3f;

        [SerializeField] private Image _badgeImage;

        private void Awake()
        {
            ActionBadgeSprites.EnsureLoaded();
            ActionBadgeUtility.Repair(gameObject, this);
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            _badgeImage ??= GetComponent<Image>();
        }

        internal void WireBadgeImage(Image image) => _badgeImage = image;

        /// <summary>Shows the badge for the given betting action.</summary>
        public void Show(BettingAction action, int amount = 0)
        {
            PresentSprite(ActionBadgeSprites.For(action), DisplayDurationSecs);
        }

        /// <summary>Hides the badge immediately.</summary>
        public void Hide()
        {
            CancelInvoke(nameof(Hide));
            gameObject.SetActive(false);
        }

        /// <summary>Shows the winner badge for <paramref name="duration"/> seconds.</summary>
        public void ShowWin(int potAmount, float duration)
        {
            PresentSprite(ActionBadgeSprites.Winner, duration);
        }

        private void PresentSprite(Sprite sprite, float duration)
        {
            if (sprite == null)
            {
                Debug.LogWarning("[ActionBadge] Missing badge sprite — run Texas Holdem → Create Action Badge Sprite Set.", this);
                return;
            }

            enabled = true;
            ActionBadgeUtility.Repair(gameObject, this);
            ResolveReferences();

            if (_badgeImage == null)
            {
                Debug.LogWarning("[ActionBadge] Badge Image missing after repair — run Texas Holdem → Repair Action Badges In Scene.", this);
                return;
            }

            _badgeImage.sprite         = sprite;
            _badgeImage.color          = Color.white;
            _badgeImage.preserveAspect = true;
            _badgeImage.enabled        = true;

            HideLabelChild();
            FitToSprite(sprite);
            BringToFrontOfSeat();

            // Activate after setup — prefab starts inactive; never call Hide() from Awake (that races first Show).
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            CancelInvoke(nameof(Hide));
            Invoke(nameof(Hide), duration);
        }

        private void FitToSprite(Sprite sprite)
        {
            RectTransform rt = transform as RectTransform;
            if (rt == null)
                return;

            rt.sizeDelta = ActionBadgeSprites.SizeForSprite(sprite);
        }

        private void HideLabelChild()
        {
            Transform label = transform.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);
        }

        /// <summary>Draws above cards, name, and bet chip display on the seat.</summary>
        public void BringToFront()
        {
            Transform parent = transform.parent;
            if (parent == null)
                return;

            transform.SetAsLastSibling();
        }

        private void BringToFrontOfSeat() => BringToFront();
    }
}
