using System;
using HarmonyLib;
using Vuplex.WebView.Internal;

namespace Greenline
{
    // The messages of the page script (window.vuplex.postMessage('slmod|greenline|<text>')). The Prefix takes
    // only the messages with the prefix of this mod, so the game's handler and the other mods never get them,
    // and lets each other message through. "hover error: <text>" turns the world pot card off.
    [HarmonyPatch(typeof(BaseWebView), "HandleMessageEmitted")]
    internal static class PageMessages
    {
        private const string Prefix_ = "slmod|greenline|";

        private static bool Prefix(string serializedMessage)
        {
            if (serializedMessage == null || !serializedMessage.StartsWith(Prefix_, StringComparison.Ordinal)) return true;
            try
            {
                string text = serializedMessage.Substring(Prefix_.Length);
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug("Greenline page message: " + text);
                if (text.StartsWith("hover error:", StringComparison.Ordinal)) WorldHover.TurnOff("page " + text);
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: a page message failed: {e.Message}"); }
            return false;
        }
    }
}
