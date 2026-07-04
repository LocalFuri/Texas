using UnityEngine;

namespace TexasHoldem
{
    /// <summary>PNG seat badges — loaded from Resources at runtime.</summary>
    [CreateAssetMenu(fileName = "ActionBadgeSpriteSet", menuName = "Texas Holdem/Action Badge Sprite Set")]
    public class ActionBadgeSpriteSet : ScriptableObject
    {
        [Tooltip("Badge height in canvas pixels for all seats (auto layout). Width follows sprite aspect ratio.")]
        [Min(8f)]
        public float BadgeHeight = 40f;

        public Sprite Check;
        public Sprite Fold;
        public Sprite Raise;
        public Sprite AllIn;
        public Sprite Winner;
    }
}
