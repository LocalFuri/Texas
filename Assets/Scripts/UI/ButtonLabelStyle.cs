using System;
using TMPro;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>Matches BlackJack: Casino3D SDF with vertex color only — no outline/underlay.</summary>
    public static class ButtonLabelStyle
    {
        public const float DefaultFontSize = 24f;

        /// <summary>RAISE button label — RGB (0, 170, 255).</summary>
        public static readonly Color RaiseText = new Color32(0, 170, 255, 255);

        private static Func<float> _actionButtonFontSizeProvider;
        private const  float       _fallbackFontSize = 40f;

        /// <summary>Returns the current action button font size from the registered provider, or 40 as fallback.</summary>
        public static float ActionButtonFontSize => _actionButtonFontSizeProvider?.Invoke() ?? _fallbackFontSize;

        /// <summary>Registers a delegate that supplies the live action button font size from the Inspector.</summary>
        public static void RegisterFontSizeProvider(Func<float> provider) => _actionButtonFontSizeProvider = provider;

        public static void Apply(TMP_Text label, Color textColor, float fontSize = DefaultFontSize)
        {
            if (label == null || label.font == null) return;

            if (label.fontMaterial != null && label.fontMaterial != label.font.material)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEngine.Object.DestroyImmediate(label.fontMaterial);
                else
#endif
                    UnityEngine.Object.Destroy(label.fontMaterial);
            }

            label.fontSharedMaterial = label.font.material;
            label.raycastTarget      = false;
            label.color              = textColor;
            label.fontStyle          = FontStyles.Bold;
            label.fontSize           = fontSize;
            label.enableAutoSizing   = false;
            label.enableWordWrapping = false;
            label.overflowMode       = TextOverflowModes.Overflow;
            label.alignment          = TextAlignmentOptions.Center;
        }
    }
}
