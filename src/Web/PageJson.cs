using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SlShared.Json;

namespace Greenline
{
    // The JSON text between the plugin and the page scripts, built and read by hand, with the strings
    // of the shared JsonText. No game or BepInEx type here.
    public static class PageJson
    {
        // The data that the plant panel script reads: the pot of the open window (as a string, so the page
        // keeps every digit), its Auto-replant and Auto-fertilize values, the last-crop line (null when the
        // mod knows no last crop), and the words.
        public static string PlantPanelJson(long potId, bool replant, bool fert, ReplantLogic.CropLine line, string cropIcon,
            IEnumerable<KeyValuePair<string, string>> words)
        {
            var sb = new StringBuilder();
            sb.Append("{\"pot\":").Append(JsonText.Str(potId.ToString()))
                .Append(",\"replant\":").Append(replant ? "true" : "false")
                .Append(",\"fert\":").Append(fert ? "true" : "false")
                .Append(",\"lastCrop\":");
            if (line == null) sb.Append("null");
            else
                sb.Append("{\"label\":").Append(JsonText.Str(line.Label))
                    .Append(",\"name\":").Append(JsonText.Str(line.Crop))
                    .Append(",\"icon\":").Append(JsonText.Str(cropIcon))
                    .Append(",\"red\":").Append(JsonText.Str(line.Red)).Append('}');
            sb.Append(",\"words\":{");
            bool first = true;
            foreach (var w in words)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(JsonText.Str(w.Key)).Append(':').Append(JsonText.Str(w.Value));
            }
            sb.Append("}}");
            return sb.ToString();
        }

        // The page message (slmod|greenline|no script) of a command that finds no page script in the root page: a new
        // root page, or one that the game built again after a browser crash. PageMessages then asks for a full send.
        public const string NoScript = "no script";

        // Each command is a statement, so it gives the web UI no value to send back: the mod asks for no result
        // (a result callback can crash the web UI browser, vuplex.md), and the page answers by a page message.
        private const string ElsePostNoScript = "else window.vuplex.postMessage('slmod|greenline|" + NoScript + "');";

        // The push of the pot data when the page script is already in the root page.
        public static string SetPotsCommand(string json) =>
            "if(window.__greenline)window.__greenline.setPots(" + json + ");" + ElsePostNoScript;

        // The check, when the pot data did not change, that the root page still has the page script and the CoreUI1
        // frame still has the grid. check() posts only a result that is not ok; with Verbose it also posts its counts
        // once each real minute.
        public static string CheckCommand(bool verbose) =>
            "if(window.__greenline)window.__greenline.check(" + (verbose ? "true" : "false") + ");" + ElsePostNoScript;

        // The page script, then the push of the pot data: sent only after a page message NoScript.
        public static string SetPotsWithScriptCommand(string script, string json) =>
            script + ";window.__greenline.setPots(" + json + ");void 0;";

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

        // The data of a GREENLINE_REPLANT message: the new value of the Auto-replant checkbox, or null for a
        // message with no value.
        public static bool? ParseReplant(string json)
        {
            var on = string.IsNullOrEmpty(json) ? null : OnPattern.Match(json);
            return on != null && on.Success ? on.Groups[1].Value == "true" : (bool?)null;
        }
    }
}
