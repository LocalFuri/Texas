using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TexasHoldem
{
    /// <summary>
    /// Prevents a <see cref="Selectable"/> from staying in the highlighted/selected
    /// visual state when the pointer is pressed on the button then dragged outside
    /// before being released.
    ///
    /// Unity's EventSystem selects the button on PointerDown. When the pointer exits
    /// while still held and is released outside the bounds, the button evaluates to
    /// <see cref="Selectable.SelectionState.Selected"/> instead of Normal, leaving it
    /// visually stuck in the hover/highlight state.
    /// Clearing the EventSystem selection on exit forces the subsequent PointerUp
    /// (which Unity always delivers to the original pressed object) to resolve to Normal.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonHoverFix : MonoBehaviour, IPointerExitHandler
    {
        /// <summary>
        /// Clears the EventSystem selection so that releasing outside the button
        /// returns its visual state to Normal.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
