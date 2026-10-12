using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppInterop.Runtime;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using UnityEngine;
using SlShared.I18n;

namespace Greenline
{
    // The replant pots of the current Patrol request and the last crop of each pot.
    internal static class Replant
    {
        internal const int PlantActionId = 1610;
        private const float CheckSeconds = 1f;

        private static readonly HashSet<long> pots = new HashSet<long>();
        private static List<(int Item, int Plant)> seedItems;
        private static float nextCheck;

        // The planting that waits for the store: the window's actions act on the store's current window
        // state, and during RA_Open the store still holds the state of the window before (seen in the
        // game: the actions planted the pot of the window before, or reached no window). So the mod
        // sends them on a later frame, once the store holds the window of the replant pot.
        private const float PendingSeconds = 2f;
        private static long pendingPot;
        private static int pendingIndex;
        private static int pendingFert = -1;
        private static float pendingUntil;

        internal static void PlantWhenReady()
        {
            if (pendingPot == 0) return;
            long potId = pendingPot;
            try
            {
                var state = ReduxUISystem.Instance?.GetState<State_Web_PlantPanel>(Il2CppType.Of<State_Web_PlantPanel>());
                if (state == null || state.FurnitureInstanceId != potId)
                {
                    if (Time.realtimeSinceStartup < pendingUntil) return;
                    pendingPot = 0;
                    if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline replant: pot {potId} window never reached the store, the window stays open");
                    return;
                }
                pendingPot = 0;
                Ac_PlantPanel_SelectSeed.SendAction($"{{\"index\":{pendingIndex}}}");
                if (pendingFert >= 0) Ac_PlantPanel_SelectFert.SendAction($"{{\"index\":{pendingFert}}}");
                Ac_PlantPanel_StartPlant.SendAction();
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline replant: pot {potId} planting started");
            }
            catch (Exception e)
            {
                pendingPot = 0;
                Plugin.WarnOnce($"Greenline: replant failed, the planting window stays open: {e}");
            }
        }

        internal static void Add(long potId)
        {
            pots.Add(potId);
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline replant: Patrol queued Plant for pot {potId}");
        }

        internal static bool Take(long potId) => pots.Remove(potId);

        internal static void SetLastCrop(long potId, int seedItemId)
        {
            if (seedItemId <= 0) return;
            SaveKeys.Set("crop", potId, seedItemId);
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline replant: last crop of pot {potId} = seed {seedItemId}");
        }

        // The seed items of the game (Config_Item.Category 10) with the crop that each one plants.
        internal static List<(int Item, int Plant)> SeedItems()
        {
            if (seedItems != null) return seedItems;
            var list = new List<(int Item, int Plant)>();
            var it = ConfigManager.Instance._Config_Item_Dict.GetEnumerator();
            while (it.MoveNext())
            {
                var item = it.Current.Value;
                if (item != null && item.Category == 10 && item.Plant > 0) list.Add((it.Current.Key, item.Plant));
            }
            seedItems = list;
            return list;
        }

        // Drops a pot whose Plant chore is no longer queued, for example after the player cancels the
        // Patrol chores, so that a later Plant chore by hand is not changed.
        internal static void CheckQueue()
        {
            if (pots.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            if (now < nextCheck) return;
            nextCheck = now + CheckSeconds;
            try
            {
                var world = BaseSingleton<BattleLogicWorld>.Instance;
                long agentId = world._AgentManager.GetLeadingRoleId();
                var gone = new List<long>();
                foreach (long potId in pots)
                    if (!world._ActionManager.HasActionInQueue(agentId, PlantActionId, potId)) gone.Add(potId);
                foreach (long potId in gone)
                {
                    pots.Remove(potId);
                    if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline replant: pot {potId} has no queued Plant chore, dropped");
                }
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: replant queue check failed: {e.Message}"); }
        }

        // Selects the last crop's seed in a window that the player opened, when the containers have that
        // seed. The fertilizer selection stays. The player still clicks Plant.
        internal static void SelectLastCrop(State_Web_PlantPanel state)
        {
            long potId = state.FurnitureInstanceId;
            int lastSeed = SaveKeys.Get("crop", potId, 0);
            if (lastSeed <= 0) return;
            var seedIds = new List<int>();
            for (int i = 0; i < state.Seeds.Count; i++) seedIds.Add(state.Seeds[i].ItemConfigId.Value);
            var (index, _) = ReplantLogic.FindSeed(lastSeed, seedIds);
            if (index < 0 || index == state.SelectedSeedIndex.Value) return;
            Reducer_Web_PlantPanel.RecalcSelection(state, index, state.SelectedFertIndex.Value);
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline: pot {potId} window selects the last crop, seed {lastSeed} index {index}");
        }

        // Decides for the pot of the open window, through the window's own checks for the last crop's
        // seed. On "plant", the window keeps the last crop's seed selected, and PlantWhenReady starts the
        // planting through the window's own actions, so the window closes as after a click on its Start
        // button. Else it gives the window back the selection that RA_Open made, and the window stays
        // open.
        internal static void Decide(State_Web_PlantPanel state)
        {
            long potId = state.FurnitureInstanceId;
            int gameFert = state.SelectedFertIndex.Value;
            var seedIds = new List<int>();
            for (int i = 0; i < state.Seeds.Count; i++) seedIds.Add(state.Seeds[i].ItemConfigId.Value);

            int lastSeed = SaveKeys.Get("crop", potId, 0);
            var (index, found) = ReplantLogic.FindSeed(lastSeed, seedIds);
            // The window lists the seeds of the containers when the Plant chore runs, so a seed from a
            // harvest earlier in the same Patrol run counts.
            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Greenline replant: pot {potId} last seed {lastSeed} have {(index >= 0 ? state.Seeds[index].OwnedCount.Value : 0)} at Plant");
            ReplantLogic.Decision decision;
            if (index < 0)
            {
                decision = new ReplantLogic.Decision { Reason = found, Need = SeedsNeeded(lastSeed, state.Capacity.Value) };
            }
            else
            {
                var seed = state.Seeds[index];
                Reducer_Web_PlantPanel.RecalcSelection(state, index, -1);
                decision = ReplantLogic.Classify(state.HasEnoughSeed.Value, state.IsLightOk.Value, state.IsTempOk.Value,
                    seed.OwnedCount.Value, state.RequiredSeedCount.Value);
            }

            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Greenline replant: pot {potId} last seed {lastSeed} index {index} decision {decision.Reason} " +
                                    $"have {decision.Have} need {decision.Need}");

            if (decision.Reason == ReplantLogic.Reason.Plant)
            {
                pendingPot = potId;
                pendingIndex = index;
                pendingFert = !PotOptions.Fert(potId) ? -1 : NoBaseFert(state, index, potId) ? -1 : FertIndex(state, potId);
                pendingUntil = Time.realtimeSinceStartup + PendingSeconds;
                if (pendingFert >= 0) Reducer_Web_PlantPanel.RecalcSelection(state, index, pendingFert);
                return;
            }

            // The window stays open with the last crop's seed selected when the list has it, and shows why
            // the replant stopped.
            if (index >= 0) Reducer_Web_PlantPanel.RecalcSelection(state, index, gameFert);
            ShowLine(potId, decision);
        }

        // Sends the pot values and the last-crop line to the planting window of the pot. `stopped` is the
        // decision of a Patrol replant that did not plant, null for a planting window by hand.
        internal static void ShowLine(long potId, ReplantLogic.Decision? stopped)
        {
            var (name, icon) = CropOfSeed(SaveKeys.Get("crop", potId, 0));
            var line = ReplantLogic.LineText(name, stopped, ModTexts.Current().ToDictionary());
            if (line != null && Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Greenline replant: pot {potId} last-crop line '{line.Label} {line.Crop}' '{line.Red}'");
            PlantPanelScript.SetState(potId, PotOptions.Replant(potId), PotOptions.Fert(potId), line, icon);
        }

        // The name and the icon of the crop that a seed item plants (the icon of its first harvest item, as
        // the pot grid shows the crop), or empty texts for an unknown seed.
        private static (string Name, string Icon) CropOfSeed(int seedItemId)
        {
            var config = ConfigManager.Instance;
            var item = seedItemId > 0 ? config.Get_Config_Item(seedItemId) : null;
            var crop = item != null ? config.Get_Config_Plant(item.Plant) : null;
            if (crop == null) return ("", "");
            string name = string.IsNullOrEmpty(crop.Name) ? "" : config.GetLocalTxt(crop.Name);
            if (string.IsNullOrEmpty(name)) name = crop.Name ?? "";
            string icon = crop.Gain != null && crop.Gain.Count > 0 ? config.Get_Config_Item(crop.Gain[0])?.WebIcon ?? "" : "";
            return (name, icon);
        }

        // A seed that takes no fertilizer at planting (a research crop): the game drops its fertilizer, so
        // the mod picks none and shows no pop text.
        private static bool NoBaseFert(State_Web_PlantPanel state, int index, long potId)
        {
            var seed = state.Seeds[index];
            if (!seed.NoBaseFert.Value) return false;
            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Greenline replant: pot {potId} seed {seed.ItemConfigId.Value} no base fertilizer");
            return true;
        }

        // The window's index of the fertilizer that gives the pot the largest speed bonus, a part dose
        // included, or -1 with a pop text when the containers have no fertilizer.
        private static int FertIndex(State_Web_PlantPanel state, long potId)
        {
            var counts = new Dictionary<int, int>();
            var speeds = new Dictionary<int, float>();
            for (int i = 0; i < state.Ferts.Count; i++)
            {
                int item = state.Ferts[i].ItemConfigId.Value;
                counts[item] = (counts.TryGetValue(item, out int n) ? n : 0) + state.Ferts[i].OwnedCount.Value;
                speeds[item] = state.Ferts[i].SpeedRate.Value;
            }
            var pool = new List<(int Item, int Count, float Speed)>();
            foreach (var kv in counts) pool.Add((kv.Key, kv.Value, speeds[kv.Key]));
            int size = state.Capacity.Value;
            int pick = ReplantLogic.PickFertilizer(pool, size);
            if (Plugin.Verbose.Value)
            {
                var parts = new List<string>();
                foreach (var f in pool) parts.Add($"{f.Item}x{f.Count}@{f.Speed.ToString(CultureInfo.InvariantCulture)}");
                int dose = pick == 0 ? 0 : Math.Min(counts[pick], size);
                Plugin.Log.LogDebug($"Greenline replant: pot {potId} fertilizer size {size} pool {string.Join(",", parts)} " +
                                    $"picked {pick} dose {dose}/{size}");
            }
            if (pick == 0)
            {
                string text = I18nTexts.Fill(ModTexts.Current()["popNoFert"], ("pot", state.FacilityName.Value));
                EventDispatcherTools.NotifyGlobal<string>(EnumNotify_Show.PopText, text);
                return -1;
            }
            for (int i = 0; i < state.Ferts.Count; i++)
                if (state.Ferts[i].ItemConfigId.Value == pick) return i;
            return -1;
        }

        // The seed count that a pot needs for a crop: the pot fills with the crop, so its Capacity over
        // the crop size (the window's "Required" count). 0 when the crop is unknown.
        private static int SeedsNeeded(int seedItemId, int capacity)
        {
            var config = ConfigManager.Instance;
            var item = seedItemId > 0 ? config.Get_Config_Item(seedItemId) : null;
            var crop = item != null ? config.Get_Config_Plant(item.Plant) : null;
            return crop != null && crop.Size > 0 ? Math.Max(1, capacity / crop.Size) : 0;
        }
    }
}
