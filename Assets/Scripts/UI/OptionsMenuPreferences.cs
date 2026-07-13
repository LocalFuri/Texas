using UnityEngine;

namespace TexasHoldem
{
    /// <summary>Persists debug options menu settings via PlayerPrefs (survives quit/relaunch).</summary>
    public static class OptionsMenuPreferences
    {
        private const string KeyBotThinkDelay = "TexasHoldem.Options.BotThinkDelay";
        private const string KeyEquitySims    = "TexasHoldem.Options.EquitySimulationCount";
        private const string KeyShowBotCards  = "TexasHoldem.Options.ShowBotCards";
        private const string KeyTestMode      = "TexasHoldem.Options.TestMode";
        private const string KeyGodMode       = "TexasHoldem.Options.GodMode";
        private const string KeyAutoAdvance   = "TexasHoldem.Options.AutoAdvance";

        public static void Save(OptionsMenu menu)
        {
            if (menu == null)
                return;

            float botDelay = menu.CurrentBotThinkDelaySeconds;
            PlayerPrefs.SetFloat(KeyBotThinkDelay, botDelay);
            PlayerPrefs.SetInt(KeyEquitySims, menu.CurrentEquitySimulationCount);
            PlayerPrefs.SetInt(KeyShowBotCards, menu.ShowBotCards ? 1 : 0);
            PlayerPrefs.SetInt(KeyTestMode,     menu.TestMode ? 1 : 0);
            PlayerPrefs.SetInt(KeyGodMode,      menu.GodMode ? 1 : 0);
            PlayerPrefs.SetInt(KeyAutoAdvance,  menu.AutoAdvance ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void Load(OptionsMenu menu)
        {
            if (menu == null)
                return;

            if (PlayerPrefs.HasKey(KeyBotThinkDelay))
            {
                float delay = PlayerPrefs.GetFloat(KeyBotThinkDelay, OptionsMenu.BotThinkDefaultSeconds);
                menu.ApplyLoadedBotThinkDelay(delay);
            }

            if (PlayerPrefs.HasKey(KeyEquitySims))
            {
                PlayerPrefs.DeleteKey(KeyEquitySims);
                PlayerPrefs.Save();
            }

            menu.ApplyLoadedEquitySimulationCount(OptionsMenu.EquitySimsDefault);

            if (HasAnyToggleKey())
                menu.ApplyLoadedToggleStates(
                    GetBool(KeyShowBotCards),
                    GetBool(KeyTestMode),
                    GetBool(KeyGodMode),
                    GetBool(KeyAutoAdvance));
        }

        private static bool HasAnyToggleKey()
        {
            return PlayerPrefs.HasKey(KeyShowBotCards)
                || PlayerPrefs.HasKey(KeyTestMode)
                || PlayerPrefs.HasKey(KeyGodMode)
                || PlayerPrefs.HasKey(KeyAutoAdvance);
        }

        private static bool GetBool(string key)
            => PlayerPrefs.GetInt(key, 0) != 0;
    }
}
