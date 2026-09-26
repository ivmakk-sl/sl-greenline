using System;
using System.Collections.Generic;
using GameCore.HotUpdate.ReduxUI;

namespace Greenline
{
    // Runs page.js (an embedded resource) in the root page, which reaches the CoreUI1 iframe itself.
    internal static class PageScript
    {
        private static string script;
        private static readonly HashSet<string> loggedMissing = new HashSet<string>();
        private static readonly HashSet<string> loggedOther = new HashSet<string>();

        // The files of page/ are embedded as "<namespace>.Web.page.<file>".
        private static string Script() => script ?? (script = WithCss("__greenlineCss", "page.css")
            + PageIncludes.Join(Resource("page.js"), file => Resource("page." + file)));

        // The script text that sets window.<global> to tokens.css and the named CSS file, which the page
        // script writes into its frame's style node.
        internal static string WithCss(string global, string cssFile) =>
            "window." + global + "=" + PageJson.Str(Resource("tokens.css") + "\n" + Resource(cssFile)) + ";";

        internal static string Resource(string fileName)
        {
            var assembly = typeof(PageScript).Assembly;
            string name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("." + fileName, StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new System.IO.StreamReader(stream))
                return reader.ReadToEnd();
        }

        // Sends the pot data, then install(): setPots applies the grid when the CoreUI1 frame is
        // there, and install() retries for a short time when it is not there yet.
        public static void SetPots(string json)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null)
            {
                PotGrid.ForgetLastPush();
                return;
            }
            string js = Script() + ";window.__greenline.setPots(" + json + ");window.__greenline.install();";
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)(r =>
            {
                if (r == "no CoreUI1 frame") PotGrid.ForgetLastPush();
                LogPageCheck(r);
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline page script: {r}");
            }));
        }

        // The pot under the pointer in the game world, or 0. page.js is already in the root page once the
        // pot data went there; before that, the card has no pot data to show.
        // The pointer place is x and y as fractions of the screen from the top-left corner.
        public static void SetHover(long potId, float x, float y)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            string place = x.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) + ","
                           + y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
            string js = "window.__greenline?window.__greenline.setHover(" + potId + "," + place + "):'no script'";
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)(r =>
            {
                if (r != null && r.StartsWith("error:", StringComparison.Ordinal)) WorldHover.TurnOff("page " + r);
                else if (r != "ok" && Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline hover script: {r}");
            }));
        }

        // page.js returns "installed", "installed; missing: <parts>", "no CoreUI1 frame", or
        // "error: ...". Only a missing part or an error logs a warning, once for each distinct text.
        private static void LogPageCheck(string r)
        {
            // No CoreUI1 frame is normal while the main menu or a save load is on screen.
            if (r == null || r == "installed" || r == "no CoreUI1 frame") return;
            const string missingMarker = "; missing: ";
            int missingAt = r.IndexOf(missingMarker, StringComparison.Ordinal);
            if (missingAt >= 0)
            {
                string missing = r.Substring(missingAt + missingMarker.Length);
                if (loggedMissing.Add(missing)) Plugin.Log.LogWarning($"Greenline page check: missing {missing}");
                return;
            }
            if (loggedOther.Add(r)) Plugin.Log.LogWarning($"Greenline page check: {r}");
        }
    }

    // Runs plantpanel.js (an embedded resource) in the root page, which reaches the frames of the planting
    // window and of the growing crop window itself, and retries for a short time when the frame does not
    // exist yet.
    internal static class PlantPanelScript
    {
        private static string script;
        private static readonly HashSet<string> logged = new HashSet<string>();

        internal static void SetState(long potId, bool replant, bool fert, string reason)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            if (script == null) script = PageScript.WithCss("__greenlinePlantCss", "plantpanel.css") + PageScript.Resource("plantpanel.js");
            string json = PageJson.PlantPanelJson(potId, replant, fert, reason, GreenWords.Current());
            string js = script + ";window.__greenlinePlant.setState(" + json + ");window.__greenlinePlant.install();";
            webView.ExecuteJavaScript(js, (Il2CppSystem.Action<string>)(r =>
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline plant panel script: {r}");
                // "no frame" is normal: the script retries until the frame loads.
                if (r != null && r != "installed" && r != "no frame" && logged.Add(r))
                    Plugin.Log.LogWarning($"Greenline plant panel check: {r}");
            }));
        }
    }
}
