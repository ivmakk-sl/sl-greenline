using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace Greenline
{
    // Scans the pots and pushes the grid data. A trigger only sets the dirty flag, and FramePatch drains
    // it at most once each frame. A check each real second also covers the pot changes that send no HUD
    // message (a Till, a placed or removed pot, a new problem, the time to mature of a crop with a
    // problem). A push happens only when the JSON differs from the last one. The JSON has no time to
    // mature for a crop with no problem, so it does not change while such a crop grows.
    internal static class PotGrid
    {
        private const float CheckSeconds = 1f;

        private static bool dirty;
        private static float nextCheck;
        private static string lastJson;
        private static string lastFloorButtonsJson;
        private static List<GridLogic.Pot> lastPots = new List<GridLogic.Pot>();
        // Off after the first failure of a climate area read: each pot then counts as area 0, one zone for each floor.
        private static bool areasOn = true;
        // Off after the first failure of an area light or area temperature read: the lines then show no values.
        private static bool valuesOn = true;
        private static string lastAreasLog;

        // The requests of the page messages (PageMessages): a full send, or a new push of the pot data.
        internal static readonly SendLogic Sends = new SendLogic();

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
                if (Sends.TakeForget()) lastJson = null;
                bool full = Sends.TakeFull(now);
                // One pass over the furniture serves the grid and the count of the Tend All button.
                var watch = Stopwatch.StartNew();
                var home = HomePots();
                var pots = Scan(home);
                var areas = AreaValuesOf(pots);
                watch.Stop();
                lastPots = pots;
                var grid = GridLogic.Layout(pots, ParseFloorButtonIds(lastFloorButtonsJson));
                NameLines(ConfigManager.Instance, grid, areas);
                var texts = ModTexts.Current();
                string json = GridLogic.ToJson(texts.Language, texts.ToDictionary(), grid, pots, Patrol.Available(), Patrol.PendingChores(home),
                    PotOptions.GlobalReplant(), areas, Plugin.PinGrid.Value);
                if (!full && json == lastJson)
                {
                    PageScript.Check();
                    return;
                }
                lastJson = json;
                Push(json, full, grid, pots, watch.Elapsed.TotalMilliseconds);
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: pot grid push failed: {e.Message}"); }
        }

        // Called by PageScript when the game has no web view, so the next check sends the data again.
        internal static void ForgetLastPush() => lastJson = null;

        // The pot data alone, or with the page script after the page posted "no script".
        private static void Push(string json, bool full, GridLogic.Grid grid, List<GridLogic.Pot> pots, double scanMs)
        {
            if (full) PageScript.SendWithScript(json);
            else PageScript.SetPots(json);
            if (!Plugin.Verbose.Value) return;
            int floors = 0;
            var areas = new HashSet<(int, int)>();
            foreach (var l in grid.Lines) if (l.IsFloor) floors++;
            foreach (var p in pots) areas.Add((p.Floor, p.Area));
            Plugin.Log.LogDebug($"Greenline grid: floors {floors} areas {areas.Count} lines {grid.Lines.Count} scan {scanMs.ToString("0.000", CultureInfo.InvariantCulture)}ms");
            var sb = new StringBuilder();
            sb.Append($"Greenline pots: count={pots.Count} lines=");
            foreach (var l in grid.Lines) sb.Append($"[{l.Key} {l.Name} area {l.Area} row {l.Row}]");
            sb.Append(" cells=");
            foreach (var c in grid.Cells) sb.Append($"{c.PotId}@{c.Row}:{c.Column} ");
            foreach (var p in pots)
                sb.Append($"\n  pot={p.PotId} row={p.RowId} click={p.ClickId} capacity={p.PotSize} cropSize={p.CropSize} state={p.State} flags={p.Flags} grow={p.GrowRemainSeconds} '{p.PotName}' '{p.CropName}' fert='{p.FertName}' neverWithers={p.NeverWithers} fertDose='{p.FertDose}'");
            Plugin.Log.LogDebug(sb.ToString());
        }

        // The pots of the home, with the filters of the game's own Patrol scan
        // (PlantChoreBatchManager.Scan): a shown furniture on an unlocked floor and area of the home
        // map, whose furniture config names a pot config. The grid and Patrol read the same pots.
        internal static List<(Furniture Furniture, Config_Furniture Config, PlantComponent Plant)> HomePots()
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var agentManager = world._AgentManager;
            var tagManager = world._TagManager;
            var config = ConfigManager.Instance;

            var pots = new List<(Furniture, Config_Furniture, PlantComponent)>();
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
                pots.Add((furniture, furnitureConfig, plant));
            }
            return pots;
        }

        internal static GridLogic.PotState ToPotState(PlantState state)
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

        private static List<GridLogic.Pot> Scan(List<(Furniture Furniture, Config_Furniture Config, PlantComponent Plant)> home)
        {
            var config = ConfigManager.Instance;
            var pots = new List<GridLogic.Pot>();
            foreach (var (furniture, furnitureConfig, plant) in home)
            {
                int area = AreaOf(furniture);
                var potConfig = config.Get_Config_FurniturePlant(furnitureConfig.PlantFurnitureID);
                var pot = new GridLogic.Pot
                {
                    PotId = furniture.InstanceId,
                    ClickId = furniture.ShowInstanceId,
                    Floor = furniture.MapConfigId,
                    Area = area,
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
                    pot.NeverWithers = plant.NeverWithers();
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
                        pot.FertDose = GridLogic.FertDose(plant.FertItemConfigId, plant.PlantFertMissing, pot.PotSize);
                    }
                }
                pots.Add(pot);
            }
            return pots;
        }

        // The climate area of a pot (EnvAreaHelper.GetFurnitureAreaId), or 0 when the read failed once: the zone lines
        // and the area values then stay off for the game session.
        private static int AreaOf(Furniture furniture)
        {
            if (!areasOn) return 0;
            try { return EnvAreaHelper.GetFurnitureAreaId(furniture); }
            catch (Exception e)
            {
                areasOn = false;
                Plugin.WarnOnce($"Greenline: the climate area read failed, the grid shows no zone line and no area values: {e.Message}");
                return 0;
            }
        }

        // The values of each climate area with a pot, by the formula of the game HUD (RoomTemperatureHelper):
        // the area light is the weather light times the light factor of the area, and the area temperature is the
        // temperature of the area (with its heaters) less the cold of the weather. None after a failed read.
        private static Dictionary<int, GridLogic.AreaValues> AreaValuesOf(List<GridLogic.Pot> pots)
        {
            var areas = new Dictionary<int, GridLogic.AreaValues>();
            if (!areasOn || !valuesOn) return areas;
            try
            {
                // No weather yet (a save still loading): no values for this check only, with no warning.
                var weather = BaseSingleton<BattleLogicWorld>.Instance?._WeatherManager;
                if (weather == null) return areas;
                float weatherLight = weather.GetLightValue();
                int weatherCold = weather.GetColdValue();
                foreach (var pot in pots)
                {
                    if (pot.Area <= 0 || areas.ContainsKey(pot.Area)) continue;
                    areas[pot.Area] = new GridLogic.AreaValues
                    {
                        Name = AreaName(pot.Area),
                        Light = AreaFormat.Light(weatherLight * EnvAreaHelper.GetLightMul(pot.Area)),
                        Temp = AreaFormat.Temp(EnvAreaHelper.GetAreaTemperature(pot.Area) - weatherCold),
                    };
                }
            }
            catch (Exception e)
            {
                valuesOn = false;
                Plugin.WarnOnce($"Greenline: the area light or temperature read failed, the lines show no values: {e.Message}");
                areas.Clear();
            }
            if (Plugin.Verbose.Value && areas.Count > 0)
            {
                var sb = new StringBuilder("Greenline areas:");
                foreach (var kv in areas) sb.Append($" {kv.Key} light {kv.Value.Light} temp {kv.Value.Temp}");
                string text = sb.ToString();
                if (text != lastAreasLog)
                {
                    lastAreasLog = text;
                    Plugin.Log.LogDebug(text);
                }
            }
            return areas;
        }

        // The name of each line: the floor name of the floor switcher, and the game's name of the climate area
        // (EnvAreaHelper.GetAreaDisplayName, the name of the HUD) for a zone line, read once with the area values.
        private static void NameLines(ConfigManager config, GridLogic.Grid grid, Dictionary<int, GridLogic.AreaValues> areas)
        {
            foreach (var line in grid.Lines)
                line.Name = line.IsFloor ? FloorLabel(config, line.Floor)
                    : areas.TryGetValue(line.Area, out var values) ? values.Name : AreaName(line.Area);
        }

        private static string AreaName(int area)
        {
            try { return EnvAreaHelper.GetAreaDisplayName(area) ?? ""; }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Greenline: the climate area name read failed: {e.Message}");
                return "";
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
