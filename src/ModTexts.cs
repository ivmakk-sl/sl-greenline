using SlShared.I18n;

namespace Greenline
{
    // The mod texts (src/i18n/) through the i18n library, which reads the display language. No other code of the mod
    // reads the language.
    internal static class ModTexts
    {
        private static readonly GameTexts texts =
            new GameTexts(typeof(ModTexts).Assembly, "Greenline", warning => Plugin.Log.LogWarning("Greenline: " + warning));
        private static LanguageChoice logged;

        // The mod texts in the display language. The language can change while the game runs, so the texts are read
        // again at each push of the pot data and each open of a plant window.
        internal static I18nTextSet Current()
        {
            var set = texts.Current();
            if (!texts.Choice.Equals(logged))
            {
                logged = texts.Choice;
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Greenline texts: " + logged);
            }
            return set;
        }
    }
}
