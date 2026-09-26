using System;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;

namespace Greenline
{
    // Turns on the game's own Patrol. The released game has it switched off by the static flag
    // GameKey.PlantChoreBatchEnabled; PlantChoreBatchManager.IsUnlocked also needs planting level 3.
    // The header of the HUD plant list reads its own value, which the game sets once at load, before
    // the mod can set the flag, so the mod sets that value too.
    internal static class Patrol
    {
        private static bool turnedOn;

        internal static void TurnOn()
        {
            if (!Plugin.EnablePatrol.Value) return;
            try
            {
                if (GameKey.PlantChoreBatchEnabled) return;
                GameKey.PlantChoreBatchEnabled = true;
                if (!turnedOn && Plugin.Verbose.Value) Plugin.Log.LogDebug("Greenline: the game's Patrol is turned on.");
                turnedOn = true;
            }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: Patrol could not be turned on: {e.Message}"); }
        }

        internal static void UnlockHeader(State_Web_CoreUI1 state)
        {
            if (!Plugin.EnablePatrol.Value || state?.PlantPatrolUnlocked == null || state.PlantPatrolUnlocked.Value) return;
            try { state.PlantPatrolUnlocked.Value = Available(); }
            catch (Exception e) { Plugin.WarnOnce($"Greenline: the Patrol header value could not be set: {e.Message}"); }
        }

        // Whether the game's Patrol can run now: the flag and planting level 3.
        internal static bool Available()
        {
            var manager = BaseSingleton<BattleLogicWorld>.Instance?._PlantChoreBatchManager;
            return manager != null && manager.IsUnlocked();
        }

        // The count of chores that wait, the same count as the game's "Tend ×N" quick action.
        internal static int PendingChores()
        {
            var manager = BaseSingleton<BattleLogicWorld>.Instance?._PlantChoreBatchManager;
            return manager != null && manager.IsUnlocked() ? manager.CountPendingChores() : 0;
        }
    }
}
