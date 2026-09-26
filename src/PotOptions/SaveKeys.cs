using System;
using System.Collections.Generic;
using HarmonyLib;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace Greenline
{
    // The pot memory in the save: greenline.<kind>.<potId> keys in the game's GameCounterDict, so the
    // values go back with a day or round snapshot of the game. The pot ids change at each load, so the
    // keys are read out before the load (StartBattle Prefix), each loaded furniture maps its saved id to
    // its new id (LoadGame Postfix), and the keys go back under the new ids (StartBattle Postfix).
    internal static class SaveKeys
    {
        private static Dictionary<string, int> loaded;
        private static readonly Dictionary<long, long> oldToNew = new Dictionary<long, long>();

        private static Il2CppSystem.Collections.Generic.Dictionary<string, int> Dict() =>
            BaseSingleton<GameSaveManager>.Instance?.gameSave?.CurSave?.GameCounterDict;

        internal static int Get(string kind, long potId, int fallback)
        {
            var dict = Dict();
            return dict != null && dict.TryGetValue(SaveKeyLogic.Key(kind, potId), out int value) ? value : fallback;
        }

        internal static void Set(string kind, long potId, int value)
        {
            var dict = Dict();
            if (dict == null) return;
            dict[SaveKeyLogic.Key(kind, potId)] = value;
        }

        // Before a load: the mod keys of the save that loads, taken out of its dictionary.
        internal static void TakeOut(GameSaveData save)
        {
            loaded = new Dictionary<string, int>();
            oldToNew.Clear();
            var dict = save?.CurSave?.GameCounterDict;
            if (dict == null) return;
            var keys = new List<string>();
            var it = dict.GetEnumerator();
            while (it.MoveNext())
                if (SaveKeyLogic.IsModKey(it.Current.Key))
                {
                    keys.Add(it.Current.Key);
                    loaded[it.Current.Key] = it.Current.Value;
                }
            foreach (string key in keys) dict.Remove(key);
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline save: {loaded.Count} pot keys read");
        }

        internal static void MapId(long oldId, long newId)
        {
            if (loaded != null && oldId != 0) oldToNew[oldId] = newId;
        }

        // After a load: the keys back under the new pot ids; the keys of a pot that no longer exists drop.
        internal static void PutBack(GameSaveData save)
        {
            if (loaded == null) return;
            var dict = save?.CurSave?.GameCounterDict;
            var rekeyed = SaveKeyLogic.Rekey(loaded, oldToNew);
            if (dict != null)
                foreach (var entry in rekeyed) dict[entry.Key] = entry.Value;
            if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline save: {rekeyed.Count} of {loaded.Count} pot keys kept, {oldToNew.Count} ids mapped");
            loaded = null;
            oldToNew.Clear();
        }
    }

    [HarmonyPatch(typeof(AgentManager), "StartBattle")]
    internal static class SaveKeysOnStartBattle
    {
        private static void Prefix(GameSaveData gameSave)
        {
            try { SaveKeys.TakeOut(gameSave); }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: save key read failed: {e.Message}"); }
        }

        private static void Postfix(GameSaveData gameSave)
        {
            try { SaveKeys.PutBack(gameSave); }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: save key write failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(BaseAgent), "LoadGame")]
    internal static class SaveKeysOnLoadAgent
    {
        private static void Postfix(BaseAgent __instance, AgentSave agentSave)
        {
            if (__instance == null || agentSave == null) return;
            SaveKeys.MapId(agentSave.SaveInstanceId, __instance.InstanceId);
        }
    }
}
