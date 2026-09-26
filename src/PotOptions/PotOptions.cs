using System;
using HarmonyLib;
using GameCore.HotUpdate.ReduxUI;

namespace Greenline
{
    // The window of a growing crop opened: it shows the pot's two options under the timer.
    // FurnitureLogicId is the furniture instance id of the pot, the id of the pot grid.
    [HarmonyPatch(typeof(Reducer_Web_PlantingDetails), "RA_Open")]
    internal static class PotOptionsOnDetailsOpen
    {
        private static void Postfix(State_Web_PlantingDetails __result)
        {
            if (__result == null) return;
            try
            {
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline: growing crop window of pot {__result.FurnitureLogicId}");
                PotOptions.Open(__result.FurnitureLogicId);
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: pot option checkboxes failed: {e.Message}"); }
        }
    }

    // A click on a pot option checkbox (plantpanel.js). The message layout is the one that Trapline
    // reads: four fields joined by U+001E, [2] the type and [3] the JSON data.
    [HarmonyPatch(typeof(WebUILayer), "OnMessageFromJS")]
    internal static class PotOptionMessage
    {
        private const string MessageType = "GREENLINE_POT_OPTION";

        private static bool Prefix(Vuplex.WebView.EventArgs<string> eventArgs)
        {
            string text = eventArgs?.Value;
            if (string.IsNullOrEmpty(text)) return true;
            string[] parts = text.Split('\x1E');
            if (parts.Length < 3 || parts[2] != MessageType) return true;
            try
            {
                var option = PageJson.ParsePotOption(parts.Length > 3 ? parts[3] : "");
                if (option.HasValue) PotOptions.Set(option.Value.Pot, option.Value.Key, option.Value.On);
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: {MessageType} handling failed: {e.Message}"); }
            return false;
        }
    }

    // The two options of each pot: Auto-replant (on until the player turns it off) and
    // Auto-fertilize (off until the player turns it on). A checkbox click applies at once.
    internal static class PotOptions
    {
        internal static bool Replant(long potId) => SaveKeys.Get("replant", potId, 1) != 0;

        internal static bool Fert(long potId) => SaveKeys.Get("fert", potId, 0) != 0;

        // Sends the pot's values to the window that opens for it.
        internal static void Open(long potId) => PlantPanelScript.SetState(potId, Replant(potId), Fert(potId), "");

        internal static void Set(long potId, string key, bool on)
        {
            SaveKeys.Set(key, potId, on ? 1 : 0);
            PotGrid.MarkDirty();
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline pot option: pot {potId} {key} {(on ? "on" : "off")}");
        }
    }
}
