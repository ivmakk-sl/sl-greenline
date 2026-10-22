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
    [BepInPlugin(PluginGuid, "Greenline", "1.3.0")]
    [BepInProcess("SurvivalLog.exe")]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.ivmakk.survivallog.greenline";

        internal static new ManualLogSource Log;
        internal static Harmony Harmony;
        internal static ConfigEntry<bool> Verbose;
        internal static ConfigEntry<bool> EnablePatrol;
        internal static ConfigEntry<bool> WorldHoverCard;
        internal static ConfigEntry<bool> EnableRobotReplant;
        internal static ConfigEntry<bool> PinGrid;
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
                "Tend All: one button queues the chores of all pots, within your Stamina, in the order of the shortest walk. Needs planting level 3. Restart the game after a change.");
            WorldHoverCard = Config.Bind(
                "General", "WorldHoverCard", true,
                "Show the pot card next to the pot when the pointer is on a pot in the game world. After an error it turns off by itself until the next game start; the rest of the mod keeps working.");
            EnableRobotReplant = Config.Bind(
                "General", "EnableRobotReplant", true,
                "The Farming GR-3: Same as last crop in its Sowing Presets, and it tills and plants each empty pot before its next harvest, also after it stops to charge (only Remove Pests comes first). Set to false for the robot as in the game without Greenline: a robot set to Same as last crop then plants nothing in that pot size until you pick a seed. Restart the game after a change.");
            PinGrid = Config.Bind(
                "General", "PinGrid", false,
                "Keeps the HUD plant list open (the pin at the right end of the last line of the list sets this).");
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
                         typeof(GridOnApplyLanguageKey),
                         typeof(FramePatch),
                         typeof(HoverOnTrapUpdate),
                         typeof(ReplantOnStartPlant),
                         typeof(ReplantOnHarvest),
                         typeof(ReplantOnHarvestPoor),
                         typeof(ReplantOnPlantPanelOpen),
                         typeof(PotOptionsOnDetailsOpen),
                         typeof(GreenlineMessage),
                         typeof(PageMessages),
                         typeof(SaveKeysOnStartBattle),
                         typeof(SaveKeysOnLoadAgent),
                     })
            {
                try { Harmony.CreateClassProcessor(type).Patch(); }
                catch (Exception e) { Log.LogWarning($"patch {type.Name} failed, its target method is missing: {e.Message}"); }
            }
            // The two robot groups share one switch; each still turns off alone when its target is missing.
            if (EnableRobotReplant.Value)
            {
                PatchRobot();
                PatchRobotOrder();
            }

            Log.LogInfo("Greenline loaded.");
        }

        // The patches of the Same as last crop sowing preset work only together, so they attach as one
        // group, the window entry last: when one target is missing, the group comes off again with one
        // warning, and the robot window shows and works as without the mod.
        private static void PatchRobot()
        {
            var patched = new List<System.Reflection.MethodBase>();
            foreach (var type in new[]
                     {
                         typeof(RobotOnSetSowPreset),
                         typeof(RobotOnSowPlant),
                         typeof(RobotOnGetSowPreset),
                         null, // the native detour of PlantChoreManager.ResolveSowPlan
                         typeof(RobotOnBuildSowData),
                     })
            {
                try
                {
                    if (type == null) RobotPlanDetour.Apply();
                    else patched.AddRange(Harmony.CreateClassProcessor(type).Patch());
                }
                catch (Exception e)
                {
                    foreach (var original in patched) Harmony.Unpatch(original, HarmonyPatchType.All, Harmony.Id);
                    RobotPlanDetour.Undo();
                    Log.LogWarning($"patch {type?.Name ?? "PlantChoreManager.ResolveSowPlan"} failed, its target method is missing, Same as last crop is off: {e.Message}");
                    return;
                }
            }
        }

        // The order of the robot chores is one native detour, apart from Same as last crop, so each turns off alone.
        private static void PatchRobotOrder()
        {
            try { RobotOrderDetour.Apply(); }
            catch (Exception e)
            {
                RobotOrderDetour.Undo();
                Log.LogWarning($"patch PlantChoreManager.TryResolveChore failed, its target is missing, the robot keeps the game's chore order: {e.Message}");
            }
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
            Replant.PlantWhenReady();
            Replant.CheckQueue();
            PotGrid.PushIfDirty();
            WorldHover.Check();
        }
    }
}
