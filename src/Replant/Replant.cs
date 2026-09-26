using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using UnityEngine;

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

        internal static void Clear() => pots.Clear();

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
        // seed and the crop fits the pot. The fertilizer selection stays. The player still clicks Plant.
        internal static void SelectLastCrop(State_Web_PlantPanel state)
        {
            long potId = state.FurnitureInstanceId;
            int lastSeed = SaveKeys.Get("crop", potId, 0);
            if (lastSeed <= 0) return;
            var seedIds = new List<int>();
            for (int i = 0; i < state.Seeds.Count; i++) seedIds.Add(state.Seeds[i].ItemConfigId.Value);
            var (index, _) = ReplantLogic.FindSeed(lastSeed, seedIds);
            if (index < 0 || index == state.SelectedSeedIndex.Value) return;
            if (state.Seeds[index].PlantSize.Value > state.Capacity.Value) return;
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
            ReplantLogic.Decision decision;
            if (index < 0)
            {
                decision = new ReplantLogic.Decision { Reason = found, Need = SeedsNeeded(lastSeed, state.Capacity.Value) };
            }
            else
            {
                var seed = state.Seeds[index];
                Reducer_Web_PlantPanel.RecalcSelection(state, index, -1);
                bool fits = seed.PlantSize.Value <= state.Capacity.Value;
                decision = ReplantLogic.Classify(state.HasEnoughSeed.Value, state.IsLightOk.Value, state.IsTempOk.Value, fits,
                    seed.OwnedCount.Value, state.RequiredSeedCount.Value);
            }

            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Greenline replant: pot {potId} last seed {lastSeed} index {index} decision {decision.Reason} " +
                                    $"have {decision.Have} need {decision.Need}");

            if (decision.Reason == ReplantLogic.Reason.Plant)
            {
                pendingPot = potId;
                pendingIndex = index;
                pendingFert = PotOptions.Fert(potId) ? FertIndex(state, potId) : -1;
                pendingUntil = Time.realtimeSinceStartup + PendingSeconds;
                if (pendingFert >= 0) Reducer_Web_PlantPanel.RecalcSelection(state, index, pendingFert);
                return;
            }

            // The window stays open with the last crop's seed selected when the list has it, and shows why
            // the replant stopped.
            if (index >= 0) Reducer_Web_PlantPanel.RecalcSelection(state, index, gameFert);
            PlantPanelScript.SetState(potId, PotOptions.Replant(potId), PotOptions.Fert(potId),
                ReplantLogic.ReasonText(decision, ItemName(lastSeed), GreenWords.Dictionary()));
        }

        // The window's index of the best fertilizer of which the containers have enough for the pot (its
        // Capacity), or -1 with a pop text when none is enough.
        private static int FertIndex(State_Web_PlantPanel state, long potId)
        {
            var pool = new Dictionary<int, int>();
            for (int i = 0; i < state.Ferts.Count; i++)
            {
                int item = state.Ferts[i].ItemConfigId.Value;
                pool[item] = (pool.TryGetValue(item, out int n) ? n : 0) + state.Ferts[i].OwnedCount.Value;
            }
            int need = state.Capacity.Value;
            int pick = ReplantLogic.PickFertilizer(pool, need);
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline replant: pot {potId} fertilizer need {need}, picked {pick}");
            if (pick == 0)
            {
                var words = GreenWords.Dictionary();
                string text = string.Format(words["popNoFert"], state.FacilityName.Value);
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

        private static string ItemName(int itemId)
        {
            var config = ConfigManager.Instance;
            var item = itemId > 0 ? config.Get_Config_Item(itemId) : null;
            if (item == null || string.IsNullOrEmpty(item.ItemName)) return "";
            string text = config.GetLocalTxt(item.ItemName);
            return string.IsNullOrEmpty(text) ? item.ItemName : text;
        }
    }
}
