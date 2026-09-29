using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using GameCore.HotUpdate.Battle.Logic;

namespace Greenline
{
    [BepInPlugin(PluginGuid, "Greenline", "1.1.0")]
    [BepInProcess("SurvivalLog.exe")]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.ivmakk.survivallog.greenline";

        internal static new ManualLogSource Log;
        internal static Harmony Harmony;
        internal static ConfigEntry<bool> Verbose;
        internal static ConfigEntry<bool> EnablePatrol;
        internal static ConfigEntry<bool> WorldHoverCard;
        internal static ConfigEntry<float> PanelOpacity;
        internal static ConfigEntry<float> CardOpacity;

        public override void Load()
        {
            Log = base.Log;
            Verbose = Config.Bind(
                "General", "Verbose", false,
                "Log pot grid, replant, and page script detail at Debug level. Keep off in normal play.");
            EnablePatrol = Config.Bind(
                "General", "EnablePatrol", true,
                "Turn on the game's hidden Patrol (Tend All): one button queues the chores of all pots, within your Stamina. Needs planting level 3. Restart the game after a change.");
            WorldHoverCard = Config.Bind(
                "General", "WorldHoverCard", true,
                "Show the pot card next to the pointer when the pointer is on a pot in the game world. After an error it turns off by itself until the next game start; the rest of the mod keeps working.");
            PanelOpacity = Config.Bind(
                "General", "PanelOpacity", 0.9f,
                new ConfigDescription(
                    "The opacity of the background of the plant list: its header and the pot grid. 0 is fully see-through, 1 is solid. The game uses 0.6. The icons and the text stay fully visible. Restart the game after a change.",
                    new AcceptableValueRange<float>(0f, 1f)));
            CardOpacity = Config.Bind(
                "General", "CardOpacity", 0.96f,
                new ConfigDescription(
                    "The opacity of the background of the pot card and the other Greenline tooltips. 0 is fully see-through, 1 is solid. The game uses 0.96. The icons and the text stay fully visible. Restart the game after a change.",
                    new AcceptableValueRange<float>(0f, 1f)));
            Harmony = new Harmony(PluginGuid);
            // Each patch target is attached on its own, so a target missing after a game update turns
            // off only its own feature.
            foreach (var type in new[]
                     {
                         typeof(GridOnAddMaturePlant),
                         typeof(GridOnRemoveMaturePlant),
                         typeof(GridOnRebuildFloorButtons),
                         typeof(GridOnSwitchLanguage),
                         typeof(FramePatch),
                         typeof(HoverOnTrapUpdate),
                         typeof(ReplantOnBatchRequest),
                         typeof(ReplantOnDispatch),
                         typeof(ReplantOnStartPlant),
                         typeof(ReplantOnHarvest),
                         typeof(ReplantOnHarvestPoor),
                         typeof(ReplantOnPlantPanelOpen),
                         typeof(PotOptionsOnDetailsOpen),
                         typeof(PotOptionMessage),
                         typeof(SaveKeysOnStartBattle),
                         typeof(SaveKeysOnLoadAgent),
                     })
            {
                try { Harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception e) { Log.LogWarning($"patch {type.Name} failed, its target method is missing: {e.Message}"); }
            }

            Log.LogInfo("Greenline loaded.");
        }

        private static readonly HashSet<string> warned = new HashSet<string>();

        // Logs a warning once for each distinct text, so a failure on each frame does not flood the log.
        internal static void WarnOnce(string message)
        {
            if (warned.Add(message)) Log.LogWarning(message);
        }
    }

    // ActionManager.Update is the per-frame tick of the action system during play (see Trapline).
    [HarmonyPatch(typeof(ActionManager), "Update")]
    internal static class FramePatch
    {
        private static void Postfix()
        {
            Patrol.TurnOn();
            Replant.PlantWhenReady();
            Replant.CheckQueue();
            PotGrid.PushIfDirty();
            WorldHover.Check();
        }
    }
}
