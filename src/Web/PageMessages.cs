using System;
using System.Collections.Generic;
using HarmonyLib;
using Vuplex.WebView.Internal;

namespace Greenline
{
    // The messages of the page scripts (window.vuplex.postMessage('slmod|greenline|<text>')). No call of the mod to
    // the web UI asks for a result, so the page tells the mod here what it must act on. The Prefix takes only the
    // messages with the prefix of this mod, so the game's handler and the other mods never get them, and lets each
    // other message through. It only routes: it calls no ExecuteJavaScript (no new call into Vuplex while it hands
    // out a message), and the next PotGrid.PushIfDirty sends what a request asks for.
    //   "no script"               the root page has no page script: a full send (SendLogic).
    //   "no frame", "check ..."   the HUD plant list has no grid: a new push of the pot data.
    //   "missing ...", "error ...", "plant panel ..."   a missing part or a script error: a warning, once each text.
    //   "counts ..."              the Verbose counts of the page script: a debug line.
    //   "hover error: ..."        the world pot card turns off.
    [HarmonyPatch(typeof(BaseWebView), "HandleMessageEmitted")]
    internal static class PageMessages
    {
        private const string Prefix_ = "slmod|greenline|";
        private static readonly HashSet<string> warned = new HashSet<string>();

        private static bool Prefix(string serializedMessage)
        {
            if (serializedMessage == null || !serializedMessage.StartsWith(Prefix_, StringComparison.Ordinal)) return true;
            try
            {
                string text = serializedMessage.Substring(Prefix_.Length);
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Greenline page message: " + text);
                if (text == PageJson.NoScript)
                {
                    PotGrid.Sends.RequestFull();
                    PotGrid.MarkDirty();
                }
                else if (text == "no frame" || text.StartsWith("check ", StringComparison.Ordinal))
                {
                    PotGrid.Sends.RequestForget();
                    PotGrid.MarkDirty();
                }
                else if (text.StartsWith("missing ", StringComparison.Ordinal) || text.StartsWith("error ", StringComparison.Ordinal)
                         || text.StartsWith("plant panel ", StringComparison.Ordinal))
                {
                    if (warned.Add(text)) Plugin.Log.LogWarning("Greenline page check: " + text);
                }
                else if (text.StartsWith("hover error:", StringComparison.Ordinal)) WorldHover.TurnOff("page " + text);
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: a page message failed: {e.Message}"); }
            return false;
        }
    }
}
