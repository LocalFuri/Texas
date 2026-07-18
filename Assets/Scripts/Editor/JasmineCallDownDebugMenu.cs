using UnityEditor;
using UnityEngine;

namespace TexasHoldem
{
    /// <summary>Temporary debug entry point for the Jasmine call-down reproduction hand.</summary>
    public static class JasmineCallDownDebugMenu
    {
        [MenuItem("Texas Hold'em/Debug/Start Jasmine Call-Down Hand")]
        public static void StartFromMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[JasmineCallDown] Enter Play Mode first, then run this menu again.");
                return;
            }

            GameManager gm = Object.FindObjectOfType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("[JasmineCallDown] No GameManager found in the open scene.");
                return;
            }

            gm.StartJasmineCallDownDebugHand();
            Debug.Log(
                "[JasmineCallDown] Requested HU Ace Maverick (Kc8c) vs Jasmine Vale (QhTh); " +
                "board Kh8s5h / As / Ac. Confirm Console for seat prep + TexasHoldem_AI_Debug.txt.");
        }
    }
}
