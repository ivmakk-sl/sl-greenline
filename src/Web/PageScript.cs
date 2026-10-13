using System;
using System.Collections.Generic;
using GameCore.HotUpdate.ReduxUI;
using SlShared.Json;

namespace Greenline
{
    // Runs page.js (an embedded resource) in the root page, which reaches the CoreUI1 iframe itself.
    internal static class PageScript
    {
        private static string script;
        private static readonly HashSet<string> loggedMissing = new HashSet<string>();
        private static readonly HashSet<string> loggedOther = new HashSet<string>();

        // The Vite bundle of page/ is embedded as "Greenline.page.js".
        private static string Script() => script ?? (script =
            WithCss("__greenlineCss", "page.css", PageStyle.OpacityRule(Plugin.PanelOpacity.Value, Plugin.CardOpacity.Value))
            + Resource("page.js"));

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

        // A full send whose result has not come back yet blocks a second one, for at most this long: a
        // browser crash drops the result.
        private const float FullSendWaitSeconds = 5f;
        private static float fullSendAt = float.NegativeInfinity;
        // The number of the last full send: only its result opens the gate, not a late one of an older send.
        private static int fullSendId;

        // Sends the pot data alone when the root page has the page script, and the script with the pot data
        // when it does not (a new root page). setPots applies the grid once when the CoreUI1 frame is
        // there; when it is not, the next check of PotGrid sends again.
        public static void SetPots(string json)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null)
            {
                PotGrid.ForgetLastPush();
                return;
            }
            webView.ExecuteJavaScript(PageJson.SetPotsCommand(json), (Il2CppSystem.Action<string>)(r =>
            {
                if (r == PageJson.NoScript) SendWithScript(json);
                else OnSetPotsResult(r);
            }));
        }

        // A browser crash or a frame built again drops the grid while the pot data stays the same, so no
        // push would come; a result other than "ok" makes the next check of PotGrid push again.
        public static void Check()
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            webView.ExecuteJavaScript(PageJson.CheckCommand, (Il2CppSystem.Action<string>)(r =>
            {
                if (r == "ok") return;
                PotGrid.ForgetLastPush();
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline page check: {r}");
            }));
        }

        private static void SendWithScript(string json)
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (webView == null || now - fullSendAt < FullSendWaitSeconds)
            {
                PotGrid.ForgetLastPush();
                return;
            }
            fullSendAt = now;
            int id = ++fullSendId;
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Greenline page script: sent");
            webView.ExecuteJavaScript(PageJson.SetPotsWithScriptCommand(Script(), json), (Il2CppSystem.Action<string>)(r =>
            {
                if (id == fullSendId) fullSendAt = float.NegativeInfinity;
                OnSetPotsResult(r);
            }));
        }

        private static void OnSetPotsResult(string r)
        {
            if (r == "no CoreUI1 frame") PotGrid.ForgetLastPush();
            LogPageCheck(r);
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline page script: {r}");
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

    // Runs the plantpanel bundle (the embedded resource "Greenline.plantpanel.js") in the root page, which reaches the frames of the planting
    // window and of the growing crop window itself, and retries for a short time when the frame does not
    // exist yet.
    internal static class PlantPanelScript
    {
        private static string script;
        private static readonly HashSet<string> logged = new HashSet<string>();

        // The pot values of a window, and the last-crop line of a planting window (null for none, and for
        // the window of a growing crop).
        internal static void SetState(long potId, bool replant, bool fert, ReplantLogic.CropLine line = null, string cropIcon = "")
        {
            var webView = ReduxUISystem.Instance?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
            if (webView == null) return;
            if (script == null) script = PageScript.WithCss("__greenlinePlantCss", "plantpanel.css") + PageScript.Resource("plantpanel.js");
            string json = PageJson.PlantPanelJson(potId, replant, fert, line, cropIcon, ModTexts.Current().ToDictionary());
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
