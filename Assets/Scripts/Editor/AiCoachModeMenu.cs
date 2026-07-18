using UnityEditor;
using UnityEngine;
using TexasHoldem.Dev;

namespace TexasHoldem
{
    public static class AiCoachModeMenu
    {
        private const string EnablePath = "Texas Hold'em/Dev/AI Coach Mode/Enable";
        private const string DisablePath = "Texas Hold'em/Dev/AI Coach Mode/Disable";

        [MenuItem(EnablePath, false, 10)]
        public static void EnableCoach()
        {
            AiCoachMode.IsEnabled = true;
            Debug.LogWarning("[AiCoach] Enabled. Shared TrainerAdvice overlay will show on human turns in Play Mode.");
        }

        [MenuItem(EnablePath, true)]
        public static bool EnableCoachValidate()
        {
            Menu.SetChecked(EnablePath, AiCoachMode.IsEnabled);
            return !AiCoachMode.IsEnabled;
        }

        [MenuItem(DisablePath, false, 11)]
        public static void DisableCoach()
        {
            AiCoachMode.IsEnabled = false;
            Debug.LogWarning("[AiCoach] Disabled. Gameplay and logging match baseline (no coach overlay).");
        }

        [MenuItem(DisablePath, true)]
        public static bool DisableCoachValidate()
        {
            Menu.SetChecked(DisablePath, !AiCoachMode.IsEnabled);
            return AiCoachMode.IsEnabled;
        }
    }
}
