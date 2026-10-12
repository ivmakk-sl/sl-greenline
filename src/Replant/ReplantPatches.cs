using System;
using HarmonyLib;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;

namespace Greenline
{
    // Each planting, by hand or by replant, sets the last crop of the pot.
    [HarmonyPatch(typeof(LeadingRole), "OnPlantPanelStartPlant")]
    internal static class ReplantOnStartPlant
    {
        private static void Prefix(long furnitureInstanceId, int seedItemConfigId)
        {
            Replant.SetLastCrop(furnitureInstanceId, seedItemConfigId);
        }
    }

    // The game forgets the crop at the harvest, so the mod keeps its seed. This also covers a crop that
    // was planted before the mod was installed. The harvest action (AE_HarvestPlant.Run) never calls
    // PlantComponent.Harvest: it clears the pot through Reset when the pot skips the Till (a talent), else
    // through TransitionToPoor.
    [HarmonyPatch(typeof(PlantComponent), "Reset")]
    internal static class ReplantOnHarvest
    {
        internal static void Prefix(PlantComponent __instance)
        {
            try
            {
                if (__instance == null || __instance.PlantConfigId <= 0) return;
                int seed = ReplantLogic.SeedForCrop(Replant.SeedItems(), __instance.PlantConfigId);
                if (seed > 0) Replant.SetLastCrop(__instance.ownerInstanceId, seed);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"Greenline: harvest record failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(PlantComponent), "TransitionToPoor")]
    internal static class ReplantOnHarvestPoor
    {
        private static void Prefix(PlantComponent __instance) => ReplantOnHarvest.Prefix(__instance);
    }

    // The planting window opened. For a replant pot, the mod decides whether it can plant the last
    // crop. For a planting by hand, the window selects the last crop's seed.
    [HarmonyPatch(typeof(Reducer_Web_PlantPanel), "RA_Open")]
    internal static class ReplantOnPlantPanelOpen
    {
        private static void Postfix(State_Web_PlantPanel __result)
        {
            if (__result == null) return;
            try { Replant.ShowLine(__result.FurnitureInstanceId, null); }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: pot option checkboxes failed: {e.Message}"); }
            if (!Replant.Take(__result.FurnitureInstanceId))
            {
                try { Replant.SelectLastCrop(__result); }
                catch (Exception e) { Plugin.WarnOnce($"Greenline: last crop selection failed: {e.Message}"); }
                return;
            }
            try { Replant.Decide(__result); }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: replant failed, the planting window stays open: {e}"); }
        }
    }
}
