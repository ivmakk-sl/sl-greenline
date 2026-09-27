using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Greenline
{
    // The JSON text between the plugin and the page scripts, built and read by hand: the plugin has no
    // JSON library. No game or BepInEx type here.
    public static class PageJson
    {
        // The data that plantpanel.js reads: the pot of the open window (as a string, so the page keeps
        // every digit), its Auto-replant and Auto-fertilize values, the reason line (empty for none), and
        // the words.
        public static string PlantPanelJson(long potId, bool replant, bool fert, string reason, IEnumerable<KeyValuePair<string, string>> words)
        {
            var sb = new StringBuilder();
            sb.Append("{\"pot\":").Append(Str(potId.ToString()))
                .Append(",\"replant\":").Append(replant ? "true" : "false")
                .Append(",\"fert\":").Append(fert ? "true" : "false")
                .Append(",\"reason\":").Append(Str(reason))
                .Append(",\"words\":{");
            bool first = true;
            foreach (var w in words)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Str(w.Key)).Append(':').Append(Str(w.Value));
            }
            sb.Append("}}");
            return sb.ToString();
        }

        // The result of SetPotsCommand when the root page has no page script: a new root page, or one
        // that the game built again after a browser crash.
        public const string NoScript = "no script";

        // The push of the pot data when the page script is already in the root page.
        public static string SetPotsCommand(string json) =>
            "window.__greenline?window.__greenline.setPots(" + json + "):'" + NoScript + "'";

        // The page script, then the push of the pot data: sent only when SetPotsCommand gave NoScript.
        public static string SetPotsWithScriptCommand(string script, string json) =>
            script + ";window.__greenline.setPots(" + json + ");";

        private static readonly Regex PotPattern = new Regex("\"pot\"\\s*:\\s*\"(\\d+)\"");
        private static readonly Regex KeyPattern = new Regex("\"key\"\\s*:\\s*\"(replant|fert)\"");
        private static readonly Regex OnPattern = new Regex("\"on\"\\s*:\\s*(true|false)");

        // The data of a GREENLINE_POT_OPTION message: the pot, the option key ("replant" or "fert"), and
        // the new value; or null for a message with no pot, an unknown key, or no value.
        public static (long Pot, string Key, bool On)? ParsePotOption(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var pot = PotPattern.Match(json);
            var key = KeyPattern.Match(json);
            var on = OnPattern.Match(json);
            if (!pot.Success || !key.Success || !on.Success || !long.TryParse(pot.Groups[1].Value, out long potId)) return null;
            return (potId, key.Groups[1].Value, on.Groups[1].Value == "true");
        }

        // A JSON string literal, with its quotes. Also a JavaScript string literal.
        internal static string Str(string s)
        {
            var sb = new StringBuilder((s ?? "").Length + 2);
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
