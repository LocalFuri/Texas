using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class HeroPreflopOpenStatsMenu
    {
        [MenuItem("Texas Hold'em/Statistics/Hero Preflop Opens/Print Totals")]
        public static void PrintTotals()
        {
            HeroPreflopOpenStats stats = FindOrCreate();
            if (stats == null)
            {
                Debug.LogWarning("[HeroOpen] No GameManager in the open scene.");
                return;
            }

            stats.PrintTotals();
        }

        [MenuItem("Texas Hold'em/Statistics/Hero Preflop Opens/Reset Totals")]
        public static void ResetTotals()
        {
            HeroPreflopOpenStats stats = FindOrCreate();
            if (stats == null)
            {
                Debug.LogWarning("[HeroOpen] No GameManager in the open scene.");
                return;
            }

            stats.ResetTotals();
        }

        private static HeroPreflopOpenStats FindOrCreate()
        {
            HeroPreflopOpenStats existing = Object.FindFirstObjectByType<HeroPreflopOpenStats>();
            if (existing != null)
                return existing;

            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
                return null;

            return gm.gameObject.AddComponent<HeroPreflopOpenStats>();
        }
    }
}
