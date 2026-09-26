using System;
using System.Globalization;

namespace Greenline
{
    // The CSS that follows tokens.css and page.css in the HUD frame, from the config.
    public static class PageStyle
    {
        // The :root rule of the background opacities (0 to 1) of the plant list (its header and the grid)
        // and of the hover cards. It comes after tokens.css, so it replaces the game's values there.
        public static string OpacityRule(float panel, float card) =>
            ":root{--gl-panel-opacity:" + Percent(panel) + ";--gl-card-opacity:" + Percent(card) + "}";

        private static string Percent(float value) =>
            Math.Round(Math.Clamp((double)value, 0, 1) * 100, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }
}
