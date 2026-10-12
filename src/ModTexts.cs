using System;
using System.Collections.Generic;
using GameCore.HotUpdate;
using SlShared.I18n;

namespace Greenline
{
    // The mod texts (src/i18n/) through the i18n library: this adapter reads the display language and writes the
    // warnings of the library to the log. No other code of the mod reads the language.
    internal static class ModTexts
    {
        private const int English = 1;

        private static readonly I18nTexts texts = I18nTexts.Load(typeof(ModTexts).Assembly, "Greenline");
        private static readonly HashSet<string> loggedLanguages = new HashSet<string>();
        private static int warningsWritten;

        // The LanguageType number of the display language (Chinese = 0, English = 1). A missing config gives
        // English.
        private static int Language()
        {
            try
            {
                var cache = ConfigManager.Instance?.customCache;
                return cache != null ? (int)cache.LanguageType : English;
            }
            catch (Exception) { return English; }
        }

        // The mod texts in the display language. The language can change while the game runs, so the texts are read
        // again at each push of the pot data and each open of a plant window.
        internal static I18nTextSet Current()
        {
            int language = Language();
            var set = texts.For(language, key => ConstantTextTools.ToConstantTextOrEmpty(key), ((LanguageType)language).ToString());
            if (loggedLanguages.Add(set.Language) && Plugin.Verbose.Value) Plugin.Log.LogDebug("Greenline texts: language=" + set.Language);
            WriteWarnings();
            return set;
        }

        private static void WriteWarnings()
        {
            var warnings = texts.Warnings;
            while (warningsWritten < warnings.Count) Plugin.Log.LogWarning("Greenline: " + warnings[warningsWritten++]);
        }
    }
}
