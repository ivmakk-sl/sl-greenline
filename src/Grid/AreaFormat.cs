using System;
using System.Globalization;

namespace Greenline
{
    // The texts of the area light and the area temperature on the lines of the pot grid, as the game HUD shows them
    // (btFmtLight and btTempText of CoreUI1.html). The game's culture can write a decimal comma, so the texts use the
    // invariant culture. No game or BepInEx type here, so the tests can link this file.
    public static class AreaFormat
    {
        // A whole number with no ".0", else the value rounded to one decimal ("0.5").
        public static string Light(float light)
        {
            if (light == Math.Floor(light)) return ((int)light).ToString(CultureInfo.InvariantCulture);
            return (Math.Floor(light * 10.0 + 0.5) / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        // A whole number with a "+" before a number above 0.
        public static string Temp(int temp) => (temp > 0 ? "+" : "") + temp.ToString(CultureInfo.InvariantCulture);
    }
}
