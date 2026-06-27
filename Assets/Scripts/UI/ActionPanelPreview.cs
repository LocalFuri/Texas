using UnityEngine;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Keeps the ActionPanel and its betting buttons visible in Edit Mode so they
    /// can be inspected and laid out in the Scene view. Has no effect at runtime.
    /// Attach this component to the ActionPanel GameObject.
    /// </summary>
    [ExecuteAlways]
    public class ActionPanelPreview : MonoBehaviour
    {
        private void OnEnable()
        {
            if (!Application.isPlaying)
                RestoreVisibility();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                RestoreVisibility();
        }

        /// <summary>Forces the panel and all child buttons visible in Edit Mode.</summary>
        private void RestoreVisibility()
        {
            // Ensure the panel itself is active.
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // Force any CanvasGroup alpha to 1 (CanvasGroup may be added at runtime).
            if (TryGetComponent<CanvasGroup>(out CanvasGroup group))
            {
                group.alpha          = 1f;
                group.blocksRaycasts = true;
            }

            foreach (Button btn in GetComponentsInChildren<Button>(includeInactive: true))
            {
                btn.gameObject.SetActive(true);

                // A prior ColorTint disabled-state can leave the canvas renderer at 50 % alpha
                // via CrossFadeColor even after the transition is switched to SpriteSwap,
                // making the button completely invisible in the scene view.
                // Setting interactable = true resets the Button state machine to Normal and
                // forces the target graphic back to its opaque normal colour.
                btn.interactable = true;

                if (btn.targetGraphic != null)
                    btn.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
            }
        }
    }
}
