using System;
using HarmonyLib;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;

namespace Greenline
{
    // A crop row added to or updated in the HUD plant list. The row id is the ShowInstanceId of the
    // pot's plant component.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_AddMaturePlant")]
    internal static class GridOnAddMaturePlant
    {
        private static void Postfix(Ac_CoreUI1_AddMaturePlant ac, State_Web_CoreUI1 state)
        {
            Patrol.UnlockHeader(state);
            if (Plugin.Verbose.Value && ac != null)
                Plugin.Log.LogDebug($"Greenline RA_AddMaturePlant: ShowInstanceId={ac.ShowInstanceId} status={ac.Status}");
            PotGrid.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RA_RemoveMaturePlant")]
    internal static class GridOnRemoveMaturePlant
    {
        private static void Postfix()
        {
            PotGrid.MarkDirty();
        }
    }

    // Captures the floor-button list of the floor switcher, the only source of the floor order.
    [HarmonyPatch(typeof(Reducer_Web_CoreUI1), "RebuildFloorButtons")]
    internal static class GridOnRebuildFloorButtons
    {
        private static void Postfix(State_Web_CoreUI1 state)
        {
            try { PotGrid.CaptureFloorButtonsJson(state?.FloorButtonsJson?.Value); }
            catch (Exception e) { Plugin.Log.LogWarning($"Greenline: capture FloorButtonsJson failed: {e.Message}"); }
        }
    }

    // A language switch changes the floor labels and the names. SwitchLanguage returns a UniTask, so
    // the push waits for the next frame (FramePatch), which gives the switch a frame to complete.
    [HarmonyPatch(typeof(ConfigManager), "SwitchLanguage")]
    internal static class GridOnSwitchLanguage
    {
        private static void Postfix()
        {
            PotGrid.MarkDirty();
        }
    }
}
