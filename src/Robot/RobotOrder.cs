using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx.Unity.IL2CPP.Hook;
using HarmonyLib;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;
using GameCore.HotUpdate.Battle.Logic;

namespace Greenline
{
    // The il2cpp metadata that a native detour reads at load, so a game update that moves a method or a field
    // fails the patch group instead of writing to the wrong place.
    internal static class NativeMeta
    {
        // il2cpp_type_get_type of an int, a float, and a struct field (IL2CPP_TYPE_I4, _R4, _VALUETYPE).
        internal const int TypeI4 = 0x08, TypeR4 = 0x0C, TypeValueType = 0x11;

        // The address of the native code of a game method.
        internal static IntPtr Code(Type type, string name)
        {
            var method = AccessTools.Method(type, name) ?? throw new MissingMethodException(type.Name, name);
            var field = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method)
                ?? throw new MissingMethodException(type.Name, name + " (no method info)");
            // The first field of an il2cpp MethodInfo is the address of the native code.
            var code = Marshal.ReadIntPtr((IntPtr)field.GetValue(null));
            if (code == IntPtr.Zero) throw new MissingMethodException(type.Name, name + " (no code)");
            return code;
        }

        // The class of a struct, or a throw when it is missing or no longer a struct.
        internal static IntPtr Struct(IntPtr klass, string name)
        {
            if (klass == IntPtr.Zero) throw new MissingFieldException(name, "(no class)");
            if (!IL2CPP.il2cpp_class_is_valuetype(klass)) throw new MissingFieldException(name, "(not a struct)");
            return klass;
        }

        // The place of a field in the unboxed struct, and its class. The metadata offset of a struct field counts
        // the object header, which an unboxed struct does not have. A field that is missing or of another type throws.
        internal static int FieldOffset(IntPtr klass, string structName, string fieldName, int type, out IntPtr fieldClass)
        {
            var field = IL2CPP.il2cpp_class_get_field_from_name(klass, fieldName);
            if (field == IntPtr.Zero) throw new MissingFieldException(structName, fieldName);
            var fieldType = IL2CPP.il2cpp_field_get_type(field);
            if (IL2CPP.il2cpp_type_get_type(fieldType) != type)
                throw new MissingFieldException(structName, $"{fieldName} (not of type 0x{type:X})");
            int offset = (int)IL2CPP.il2cpp_field_get_offset(field) - 2 * IntPtr.Size;
            if (offset < 0) throw new MissingFieldException(structName, fieldName + " (bad offset)");
            fieldClass = IL2CPP.il2cpp_class_from_type(fieldType);
            return offset;
        }
    }

    // The order in which a robot picks its next chore. PlantChoreManager.CollectCandidates calls TryResolveChore
    // for each pot and sorts the candidates by PlantChore.Rank, then DecayRemain: Remove Pests 0, Harvest 1,
    // Remove Weeds and Water 3, Till and the robot's Plant 4. With EnableRobotReplant on, a native detour of
    // TryResolveChore gives a Till or a Plant candidate a place after Remove Pests and before each Harvest
    // (RobotLogic.FinishRank), so a robot finishes an emptied pot before its next harvest. The game's own checks
    // (seeds, environment, claims) still run later, in TryPick. A Harmony patch of TryResolveChore fails in the
    // interop trampoline on its ref Candidate argument, which holds a reference field.
    internal static class RobotOrderDetour
    {
        // The native signature of an il2cpp instance method: this, the arguments, then its MethodInfo. il2cpp gives a
        // C# bool in the low byte only, so the result is a byte.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate byte TryResolveChoreFn(IntPtr self, IntPtr furniture, IntPtr plantComp, IntPtr ctx, IntPtr candidate, IntPtr stats, IntPtr methodInfo);

        // The places of PlantChore.ActionId, Rank, and DecayRemain in the unboxed Candidate, read from the il2cpp
        // metadata at Apply (game 1.1.18385: Candidate.Chore at 0x0, then 0x18, 0x1C, 0x20).
        private static int actionAt, rankAt, decayAt;

        // For the Verbose line: the action of each pot whose candidate got the new rank, so the line comes once.
        private static readonly Dictionary<long, int> logged = new Dictionary<long, int>();

        // Static, so the garbage collector keeps the delegates while the detour runs.
        private static TryResolveChoreFn hook;
        private static TryResolveChoreFn original;
        private static INativeDetour detour;

        internal static void Apply()
        {
            var code = NativeMeta.Code(typeof(PlantChoreManager), "TryResolveChore");
            var candidate = NativeMeta.Struct(
                IL2CPP.GetIl2CppNestedType(Il2CppClassPointerStore<PlantChoreManager>.NativeClassPtr, "Candidate"),
                "PlantChoreManager.Candidate");
            int choreAt = NativeMeta.FieldOffset(candidate, "PlantChoreManager.Candidate", "Chore", NativeMeta.TypeValueType, out var chore);
            chore = NativeMeta.Struct(chore, "PlantChore");
            actionAt = choreAt + NativeMeta.FieldOffset(chore, "PlantChore", "ActionId", NativeMeta.TypeI4, out _);
            rankAt = choreAt + NativeMeta.FieldOffset(chore, "PlantChore", "Rank", NativeMeta.TypeI4, out _);
            decayAt = choreAt + NativeMeta.FieldOffset(chore, "PlantChore", "DecayRemain", NativeMeta.TypeR4, out _);
            if (Plugin.Verbose.Value)
                Plugin.Log.LogDebug($"Greenline robot order: Candidate.Chore at 0x{choreAt:X}, ActionId 0x{actionAt - choreAt:X}, Rank 0x{rankAt - choreAt:X}, DecayRemain 0x{decayAt - choreAt:X}");
            hook = Hook;
            detour = INativeDetour.CreateAndApply(code, hook, out original);
        }

        internal static void Undo()
        {
            detour?.Dispose();
            detour = null;
        }

        private static byte Hook(IntPtr self, IntPtr furniture, IntPtr plantComp, IntPtr ctx, IntPtr candidate, IntPtr stats, IntPtr methodInfo)
        {
            byte result = original(self, furniture, plantComp, ctx, candidate, stats, methodInfo);
            try
            {
                int action = 0, rank = 0;
                bool changed = false;
                if (result != 0 && candidate != IntPtr.Zero)
                {
                    action = Marshal.ReadInt32(candidate, actionAt);
                    rank = Marshal.ReadInt32(candidate, rankAt);
                    float decay = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(candidate, decayAt));
                    var (newRank, newDecay) = RobotLogic.FinishRank(action, rank, decay);
                    if (newRank != rank || newDecay != decay)
                    {
                        Marshal.WriteInt32(candidate, rankAt, newRank);
                        Marshal.WriteInt32(candidate, decayAt, BitConverter.SingleToInt32Bits(newDecay));
                        changed = true;
                    }
                }
                if (Plugin.Verbose.Value && furniture != IntPtr.Zero) Log(new Furniture(furniture).InstanceId, action, rank, changed);
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline robot order: the rank of a chore failed: {e.Message}"); }
            return result;
        }

        // The line comes once for each pot and action; a pot with no chore or another chore leaves the set.
        private static void Log(long pot, int action, int rank, bool changed)
        {
            if (!changed)
            {
                logged.Remove(pot);
                return;
            }
            if (logged.TryGetValue(pot, out int last) && last == action) return;
            logged[pot] = action;
            Plugin.Log.LogDebug($"Greenline robot order: pot {pot} action {action} rank {rank} -> {RobotLogic.RankHarvest}");
        }
    }
}
