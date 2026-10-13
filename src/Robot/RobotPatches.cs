using System;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using Il2CppInterop.Runtime;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using DynamicNpc = GameCore.HotUpdate.Battle.Logic.DynamicNpc;

namespace Greenline
{
    // The "Same as last crop" sowing preset of the GR-3 robot. A pick of it keeps the placeholder seed id
    // (RobotLogic.Placeholder) in the preset of that pot size. While the robot plans or sows a pot, the
    // preset reads give the seed of that pot instead; the window and the save keep the placeholder. The
    // seed count, the Farm Log entry of too few seeds, and the fertilizer stay the game's.
    internal static class RobotSow
    {
        // The pot that the robot sows now, or 0.
        internal static long SowPot;

        private static bool? free;
        private static string lastLine;

        // False after a game update that made the placeholder an item: the feature then stays off.
        internal static bool On()
        {
            if (free == null)
            {
                free = !ConfigManager.Instance.Has_Config_Item(RobotLogic.Placeholder);
                if (free == false) Plugin.Log.LogWarning($"Greenline robot: the seed id {RobotLogic.Placeholder} is an item of the game now, Same as last crop is off");
            }
            return free.Value;
        }

        // The preset of a pot size, with the placeholder resolved for the pot.
        internal static int Resolve(int preset, int tier, long potId)
        {
            if (preset != RobotLogic.Placeholder || potId == 0 || !On()) return preset;
            var agent = BaseSingleton<BattleLogicWorld>.Instance?._AgentManager?.GetAgent(potId);
            var plant = agent?.GetAgentComponent<PlantComponent>();
            int matureSeed = plant != null && plant.State == PlantState.Mature
                ? ReplantLogic.SeedForCrop(Replant.SeedItems(), plant.PlantConfigId) : 0;
            int seed = RobotLogic.SeedFor(preset, matureSeed, SaveKeys.Get("crop", potId, 0), PotOptions.Replant(potId));
            if (Plugin.Verbose.Value)
            {
                string line = $"Greenline robot: pot {potId} tier {tier} placeholder -> seed {seed}";
                if (line != lastLine) Plugin.Log.LogDebug(lastLine = line);
            }
            return seed;
        }

        // The pot that the current action of a robot targets, or 0 when it targets no pot. A robot can hold
        // claims on more than one pot (a harvest and a sow), so its action names the pot of the sow.
        internal static long ActionPot(long robotId)
        {
            var world = BaseSingleton<BattleLogicWorld>.Instance;
            var sources = world?._ActionManager?.AgentActionSourceDict;
            if (robotId == 0 || sources == null || !sources.TryGetValue(robotId, out var source)) return 0;
            long target = source?.CurrentAction != null ? source.CurrentAction.TargetId : 0;
            if (target == 0) return 0;
            return world._AgentManager.GetAgent(target)?.GetAgentComponent<PlantComponent>() != null ? target : 0;
        }

        // The pot that a robot has claimed: PlantChoreManager keeps one claim for each pot (pot id to robot
        // id). With no claim, or more than one, it gives 0 and the preset stays the placeholder (the robot
        // skips the pot).
        internal static long ClaimedPot(long robotId)
        {
            var claims = BaseSingleton<BattleLogicWorld>.Instance?._PlantChoreManager?._claims;
            if (claims == null) return 0;
            long found = 0;
            var it = claims.GetEnumerator();
            while (it.MoveNext())
            {
                if (it.Current.Value != robotId) continue;
                if (found != 0) return 0;
                found = it.Current.Key;
            }
            return found;
        }
    }

    // A pick in the sowing presets of the robot window. The game accepts only a seed item, so for the
    // placeholder the mod writes the preset and notifies the window as the game does; any other pick
    // goes to the game.
    [HarmonyPatch(typeof(DynamicNpc), "OnSetRobotSowPreset")]
    internal static class RobotOnSetSowPreset
    {
        private static bool Prefix(DynamicNpc __instance, long robotId, int tier, int seedItemId)
        {
            if (seedItemId != RobotLogic.Placeholder || __instance == null || robotId != __instance.InstanceId) return true;
            try
            {
                if (!RobotSow.On() || tier < 0 || tier >= RobotLogic.SowTiers) return true;
                var robot = __instance.GetAgentComponent<RobotComponent>();
                var ids = robot?._sowPresetSeedIds;
                if (ids == null || ids.Length <= tier) return true;
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline robot: robot {robotId} tier {tier} set to Same as last crop");
                if (ids[tier] == RobotLogic.Placeholder) return false;
                ids[tier] = RobotLogic.Placeholder;
                robot.Notify();
                return false;
            }
            catch (Exception e)
            {
                Plugin.WarnOnce($"Greenline robot: the Same as last crop pick failed: {e.Message}");
                return true;
            }
        }
    }

    // The lists of the robot window: the entry "Same as last crop" first after "None" for each pot size,
    // and the preset row of a size that holds the placeholder.
    [HarmonyPatch(typeof(DynamicNpc), "BuildSowData")]
    internal static class RobotOnBuildSowData
    {
        // The game's icon of the Plant action.
        private const string Icon = "../../Res/icon/icon_line_nature_18.png";

        private static void Postfix(RobotPanelData data, RobotComponent comp)
        {
            if (data == null || comp == null) return;
            try
            {
                if (!RobotSow.On()) return;
                string name = ModTexts.Current()["sameAsLastCrop"];
                var candidates = data.SowCandidates;
                var presets = data.SowPresets;
                for (int tier = 0; tier < RobotLogic.SowTiers; tier++)
                {
                    if (candidates != null)
                    {
                        int at = candidates.Count;
                        for (int i = 0; i < candidates.Count; i++)
                            if (candidates[i] != null && candidates[i].Tier == tier) { at = i; break; }
                        candidates.Insert(at, Entry(tier, name));
                    }
                    if (presets == null || comp.GetSowPreset(tier) != RobotLogic.Placeholder) continue;
                    RobotPanelSeedData row = null;
                    for (int i = 0; i < presets.Count; i++)
                        if (presets[i] != null && presets[i].Tier == tier) row = presets[i];
                    if (row == null) presets.Add(Entry(tier, name));
                    else
                    {
                        row.SeedItemId = RobotLogic.Placeholder;
                        row.Name = name;
                        row.Icon = Icon;
                        row.PerPot = 0;
                        row.HomeStock = 0;
                    }
                }
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline robot: the sowing presets failed: {e.Message}"); }
        }

        private static RobotPanelSeedData Entry(int tier, string name) =>
            new RobotPanelSeedData { Tier = tier, SeedItemId = RobotLogic.Placeholder, Name = name, Icon = Icon, PerPot = 0, HomeStock = 0 };
    }

    // The sow itself: the pot is the one that the robot (the owner of the effect) has claimed.
    [HarmonyPatch(typeof(AE_RobotSowPlant), "Run")]
    internal static class RobotOnSowPlant
    {
        private static void Prefix(EffectBtArgs args)
        {
            try
            {
                long robotId = args != null ? args.OwnerId : 0;
                long pot = RobotSow.ActionPot(robotId);
                string from = "action";
                if (pot == 0)
                {
                    pot = robotId != 0 ? RobotSow.ClaimedPot(robotId) : 0;
                    from = "claim";
                }
                RobotSow.SowPot = pot;
                if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline robot: sow by robot {robotId} at pot {pot} (from {from})");
            }
            catch (Exception e)
            {
                RobotSow.SowPot = 0;
                Plugin.WarnOnce($"Greenline robot: the pot of a sow is unknown: {e.Message}");
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            RobotSow.SowPot = 0;
            return __exception;
        }
    }

    // The plan of a sow chore (PlantChoreManager.ResolveSowPlan) reads the seed of the pot size from the
    // robot context through RobotSowRules.GetPreset. A hook of GetPreset (Harmony or native) crashes the game
    // process when the robot plans a sow, and a Harmony patch of ResolveSowPlan fails in the interop
    // trampoline on its ref Candidate argument. So a native detour of ResolveSowPlan, which gets each
    // argument as a raw pointer, writes the seed of the pot into each pot size of the context that holds
    // the placeholder, runs the plan, and writes the placeholder back.
    internal static class RobotPlanDetour
    {
        // The native signature of an il2cpp instance method: this, the arguments, then its MethodInfo.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ResolveSowPlanFn(IntPtr self, IntPtr furniture, IntPtr ctx, IntPtr candidate, IntPtr methodInfo);

        // The fields of the seed of each pot size in RobotContext, small to large.
        private static readonly string[] SeedFields = { "SowSeedSmall", "SowSeedMedium", "SowSeedLarge" };

        // The places of the seed fields in the unboxed RobotContext, read from the il2cpp metadata at Apply, so a
        // game update that moves a field moves the place too (game 1.1.18385: 0x40, 0x44, 0x48).
        private static int[] SeedOffsets;

        // Static, so the garbage collector keeps the delegates while the detour runs.
        private static ResolveSowPlanFn hook;
        private static ResolveSowPlanFn original;
        private static INativeDetour detour;

        internal static void Apply()
        {
            var code = NativeMeta.Code(typeof(PlantChoreManager), "ResolveSowPlan");
            SeedOffsets = ReadSeedOffsets();
            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Greenline robot: RobotContext seed fields at 0x{SeedOffsets[0]:X}, 0x{SeedOffsets[1]:X}, 0x{SeedOffsets[2]:X}");
            hook = Hook;
            detour = INativeDetour.CreateAndApply(code, hook, out original);
        }

        // A field that is missing or no longer an int throws, so the patch group of the robot stays off.
        private static int[] ReadSeedOffsets()
        {
            var klass = NativeMeta.Struct(Il2CppClassPointerStore<RobotContext>.NativeClassPtr, "RobotContext");
            var offsets = new int[SeedFields.Length];
            for (int i = 0; i < SeedFields.Length; i++)
                offsets[i] = NativeMeta.FieldOffset(klass, "RobotContext", SeedFields[i], NativeMeta.TypeI4, out _);
            return offsets;
        }

        internal static void Undo()
        {
            detour?.Dispose();
            detour = null;
        }

        private static int Hook(IntPtr self, IntPtr furniture, IntPtr ctx, IntPtr candidate, IntPtr methodInfo)
        {
            int[] saved = null;
            try
            {
                if (ctx != IntPtr.Zero && furniture != IntPtr.Zero && RobotSow.On())
                {
                    long potId = 0;
                    for (int tier = 0; tier < RobotLogic.SowTiers; tier++)
                    {
                        int preset = Marshal.ReadInt32(ctx, SeedOffsets[tier]);
                        if (preset != RobotLogic.Placeholder) continue;
                        if (potId == 0) potId = new Furniture(furniture).InstanceId;
                        saved ??= new int[RobotLogic.SowTiers];
                        saved[tier] = preset;
                        Marshal.WriteInt32(ctx, SeedOffsets[tier], RobotSow.Resolve(preset, tier, potId));
                    }
                }
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline robot: the seed of a plan failed: {e.Message}"); }
            try { return original(self, furniture, ctx, candidate, methodInfo); }
            finally
            {
                for (int tier = 0; saved != null && tier < RobotLogic.SowTiers; tier++)
                    if (saved[tier] != 0) Marshal.WriteInt32(ctx, SeedOffsets[tier], saved[tier]);
            }
        }
    }

    // The preset that the sow and the robot window read.
    [HarmonyPatch(typeof(RobotComponent), "GetSowPreset")]
    internal static class RobotOnGetSowPreset
    {
        private static void Postfix(int tier, ref int __result) => __result = RobotSow.Resolve(__result, tier, RobotSow.SowPot);
    }
}
