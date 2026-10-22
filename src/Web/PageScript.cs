using System;
using GameCore.HotUpdate.ReduxUI;
using SlShared.Json;

namespace Greenline
{
    // Runs page.js (an embedded resource) in the root page, which reaches the CoreUI1 iframe itself.
    internal static class PageScript
    {
        private static string script;

        // The Vite bundle of page/ is embedded as "Greenline.page.js".
        private static string Script() => script ?? (script =
            WithCssParts("__greenlineCss", "page.css", PageStyle.OpacityRule(Plugin.PanelOpacity.Value, Plugin.CardOpacity.Value))
            + Resource("page.js"));

        // The script text that sets window.<global> to { tokens, page }: tokens.css apart from the named CSS file and
        // any extra CSS, so the page script puts the CSS of its libraries between them.
        internal static string WithCssParts(string global, string cssFile, string extraCss = "") =>
            "window." + global + "={\"tokens\":" + JsonText.Str(Resource("tokens.css"))
            + ",\"page\":" + JsonText.Str(Resource(cssFile) + "\n" + extraCss) + "};";

        // The script text that sets window.<global> to tokens.css, the named CSS file, and any extra CSS,
        // which the page script writes into its frame's style node.
        internal static string WithCss(string global, string cssFile, string extraCss = "") =>
            "window." + global + "=" + JsonText.Str(Resource("tokens.css") + "\n" + Resource(cssFile) + "\n" + extraCss) + ";";

        internal static string Resource(string fileName)
        {
            var assembly = typeof(PageScript).Assembly;
            string name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("." + fileName, StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new System.IO.StreamReader(stream))
                return reader.ReadToEnd();
        }

        // Sends the pot data alone. The command posts "no script" when the root page has no page script (a new root
        // page), and setPots posts "no frame" when the CoreUI1 frame is not there; PageMessages then asks the next
        // check of PotGrid for a full send or a new push. No result: a result callback can crash the web UI browser.
        public static void SetPots(string json)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null)
            {
                PotGrid.ForgetLastPush();
                return;
            }
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline send: data {json.Length}");
            webView.ExecuteJavaScript(PageJson.SetPotsCommand(json), (Il2CppSystem.Action<string>)null);
        }

        // A browser crash or a frame built again drops the grid while the pot data stays the same, so no push would
        // come; check() posts "check <result>" when the grid is not there, and the next check of PotGrid pushes again.
        public static void Check()
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            webView.ExecuteJavaScript(PageJson.CheckCommand(Plugin.Verbose.Value), (Il2CppSystem.Action<string>)null);
        }

        // The page script with the pot data, after the page posted "no script" (SendLogic allows one each 5 real
        // seconds).
        public static void SendWithScript(string json)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null)
            {
                PotGrid.ForgetLastPush();
                return;
            }
            string js = PageJson.SetPotsWithScriptCommand(Script(), json);
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline send: full {js.Length}");
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)null);
        }

        // The pot of the world pot card, or 0, with its place on the screen as fractions from the top-left
        // corner. The page script is already in the root page once the pot data went there; before that, the
        // card has no pot data to show. No result: an error comes back as a page message (PageMessages).
        // `keepCell`: the pointer is over the web UI, so a 0 leaves the card of a grid cell open.
        public static void SetHover(long potId, float x, float y, bool keepCell)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            string place = x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ","
                           + y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            webView.ExecuteJavaScript("window.__greenline&&window.__greenline.setHover(" + potId + "," + place + "," + (keepCell ? "true" : "false") + ")",
                (Il2CppSystem.Action<string>)null);
        }
    }

    // Runs the plantpanel bundle (the embedded resource "Greenline.plantpanel.js") in the root page, which reaches the frames of the planting
    // window and of the growing crop window itself, and retries for a short time when the frame does not
    // exist yet.
    internal static class PlantPanelScript
    {
        private static string script;

        // The pot values of a window, and the last-crop line of a planting window (null for none, and for
        // the window of a growing crop).
        internal static void SetState(long potId, bool replant, bool fert, ReplantLogic.CropLine line = null, string cropIcon = "")
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            if (script == null) script = PageScript.WithCss("__greenlinePlantCss", "plantpanel.css") + PageScript.Resource("plantpanel.js");
            string json = PageJson.PlantPanelJson(potId, replant, fert, line, cropIcon, ModTexts.Current().ToDictionary());
            string js = script + ";window.__greenlinePlant.setState(" + json + ");window.__greenlinePlant.install();";
            // No result: the script posts "plant panel <result>" only for an error (PageMessages).
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)null);
        }
    }
}
