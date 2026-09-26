using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace Greenline
{
    // Scans the pots and pushes the grid data. A trigger only sets the dirty flag, and FramePatch drains
    // it at most once each frame. A check each real second also covers the pot changes that send no HUD
    // message (a Till, a placed or removed pot, the time to mature). A push happens only when the JSON
    // differs from the last one.
    internal static class PotGrid
    {
        private const float CheckSeconds = 1f;

        private static bool dirty;
        private static float nextCheck;
        private static string lastJson;
        private static string lastFloorButtonsJson;
        private static List<GridLogic.Pot> lastPots = new List<GridLogic.Pot>();

        internal static void MarkDirty() => dirty = true;

        // The PotId of a hovered furniture InstanceId when it is a pot of the last scan, or 0.
        internal static long PotAt(long hovered)
        {
            if (hovered <= 0) return 0;
            foreach (var p in lastPots)
                if (p.PotId == hovered) return p.PotId;
            return 0;
        }

        internal static void CaptureFloorButtonsJson(string json)
        {
            if (!string.IsNullOrEmpty(json)) lastFloorButtonsJson = json;
            MarkDirty();
        }

        internal static void PushIfDirty()
        {
            float now = Time.realtimeSinceStartup;
            if (!dirty && now < nextCheck) return;
            dirty = false;
            nextCheck = now + CheckSeconds;
            try
            {
                var pots = Scan();
                lastPots = pots;
                var floors = GridLogic.LayoutFloors(pots, ParseFloorButtonIds(lastFloorButtonsJson));
                var config = ConfigManager.Instance;
                foreach (var floor in floors) floor.Label = FloorLabel(config, floor.Floor);
                string json = GridLogic.ToJson(Lang(), GreenWords.Current(), floors, pots, Patrol.Available(), Patrol.PendingChores());
                if (json == lastJson) return;
                lastJson = json;
                Push(json, floors, pots);
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: pot grid push failed: {e.Message}"); }
        }

        internal static string Lang() =>
            ConfigManager.Instance?.customCache?.LanguageType == LanguageType.Chinese ? "zh" : "en";

        // Called by PageScript when the page had no CoreUI1 frame, so the next check sends the data again.
        internal static void ForgetLastPush() => lastJson = null;

        private static void Push(string json, List<GridLogic.FloorLine> floors, List<GridLogic.Pot> pots)
        {
            PageScript.SetPots(json);
            if (!Plugin.Verbose.Value) return;
            var sb = new StringBuilder();
            sb.Append($"Greenline pots: count={pots.Count} floors=");
            foreach (var f in floors) sb.Append($"[{f.Floor} {f.Label}: {string.Join(",", f.Pots)}]");
            foreach (var p in pots)
                sb.Append($"\n  pot={p.PotId} row={p.RowId} click={p.ClickId} capacity={p.PotSize} cropSize={p.CropSize} state={p.State} flags={p.Flags} grow={p.GrowRemainSeconds} '{p.PotName}' '{p.CropName}' fert='{p.FertName}'");
            Plugin.Log.LogDebug(sb.ToString());
        }

        // The pots of the home, with the filters of the game's own Patrol scan
        // (PlantChoreBatchManager.Scan): a shown furniture on an unlocked floor and area of the home
        // map, whose furniture config names a pot config.
        private static List<GridLogic.Pot> Scan()
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var agentManager = world._AgentManager;
            var tagManager = world._TagManager;
            var config = ConfigManager.Instance;

            var pots = new List<GridLogic.Pot>();
            var furnitures = agentManager.GetAllFurnitures();
            if (furnitures == null) return pots;
            for (int i = 0; i < furnitures.Count; i++)
            {
                var furniture = furnitures[i];
                if (furniture == null || !furniture.IsShow) continue;
                if (!agentManager.IsHomeMap(furniture.MapConfigId)) continue;
                if (!FloorTagConstants.IsMapPointUnlocked(tagManager, furniture.MapConfigId)) continue;
                if (!AreaUnlockConstants.IsFurnitureAreaUnlocked(furniture)) continue;
                var furnitureConfig = config.Get_Config_Furniture(furniture.AgentConfigId);
                if (furnitureConfig == null || furnitureConfig.PlantFurnitureID <= 0) continue;
                var plant = furniture.GetAgentComponent<PlantComponent>();
                if (plant == null) continue;

                var potConfig = config.Get_Config_FurniturePlant(furnitureConfig.PlantFurnitureID);
                var pot = new GridLogic.Pot
                {
                    PotId = furniture.InstanceId,
                    ClickId = furniture.ShowInstanceId,
                    Floor = furniture.MapConfigId,
                    PotName = Text(config, furnitureConfig.Name),
                    PotIcon = furnitureConfig.ICON ?? "",
                    PotSize = potConfig != null ? potConfig.Capacity : 0,
                    State = ToPotState(plant.State),
                    Flags = (int)plant.AnomalyFlags,
                    AutoFert = PotOptions.Fert(furniture.InstanceId),
                    AutoReplant = PotOptions.Replant(furniture.InstanceId),
                };

                bool hasCrop = pot.State == GridLogic.PotState.Growing || pot.State == GridLogic.PotState.Mature
                               || pot.State == GridLogic.PotState.Withered;
                if (hasCrop)
                {
                    pot.RowId = plant.showInstanceId;
                    pot.GrowRemainSeconds = pot.State == GridLogic.PotState.Growing
                        ? GridLogic.TimeToMature(plant.GetRemainSeconds(), pot.Flags, plant.AnomalyStallStartSeconds, plant.GetCurrentTotalSeconds())
                        : 0;
                    pot.GrowTotalSeconds = pot.State == GridLogic.PotState.Growing ? plant.GetActualGrowthTotalSeconds() : 0;
                    var crop = config.Get_Config_Plant(plant.PlantConfigId);
                    if (crop != null)
                    {
                        pot.CropName = Text(config, crop.Name);
                        pot.CropSize = crop.Size;
                        if (crop.Gain != null && crop.Gain.Count > 0)
                            pot.CropIcon = config.Get_Config_Item(crop.Gain[0])?.WebIcon ?? "";
                    }
                    if (plant.FertItemConfigId > 0)
                    {
                        var fert = config.Get_Config_Item(plant.FertItemConfigId);
                        if (fert != null)
                        {
                            pot.FertName = Text(config, fert.ItemName);
                            pot.FertIcon = fert.WebIcon ?? "";
                        }
                    }
                }
                pots.Add(pot);
            }
            return pots;
        }

        private static GridLogic.PotState ToPotState(PlantState state)
        {
            switch (state)
            {
                case PlantState.Growing: return GridLogic.PotState.Growing;
                case PlantState.Mature: return GridLogic.PotState.Mature;
                case PlantState.Withered: return GridLogic.PotState.Withered;
                case PlantState.Poor: return GridLogic.PotState.Poor;
                default: return GridLogic.PotState.Empty;
            }
        }

        private static string Text(ConfigManager config, string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string text = config.GetLocalTxt(key);
            return string.IsNullOrEmpty(text) ? key : text;
        }

        // Only "id" is read out of the floor-button JSON: it and the array order are the one thing
        // nothing else gives. The name comes from FloorLabel, in the current display language.
        private static readonly Regex FloorIdPattern = new Regex("\"id\":(-?\\d+)", RegexOptions.Compiled);

        private static List<int> ParseFloorButtonIds(string json)
        {
            var ids = new List<int>();
            if (string.IsNullOrEmpty(json)) return ids;
            foreach (Match m in FloorIdPattern.Matches(json))
                if (int.TryParse(m.Groups[1].Value, out int id) && !ids.Contains(id))
                    ids.Add(id);
            return ids;
        }

        // The floor name from Config_MapPoint.Name, the same lookup that the floor switcher does, else
        // the floor id.
        private static string FloorLabel(ConfigManager config, int floorId)
        {
            try
            {
                var point = config.Get_Config_MapPoint(floorId);
                if (point != null && !string.IsNullOrEmpty(point.Name))
                {
                    string text = config.GetLocalTxt(point.Name);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Greenline: floor label lookup for {floorId} failed: {e.Message}");
            }
            return floorId.ToString();
        }
    }
}
