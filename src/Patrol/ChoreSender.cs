using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using GameCore.HotUpdate;
using UnityEngine;

namespace Greenline
{
    // Queues one plant chore as a player's click in the menu of a pot does: the click sends the event
    // CrossFloorMainActionsRequest (0x32c9) with the furniture action (the walk to the pot) and the
    // plant action, and the game checks the action again when it starts. A pot on another floor works the
    // same way. After a failed send, Patrol turns off with one warning.
    internal static class ChoreSender
    {
        internal static bool Broken { get; private set; }

        // Sends one chore. False when the send failed.
        internal static bool Send(long potId, int furnitureActionId, int plantActionId)
        {
            if (Broken) return false;
            try
            {
                var ids = furnitureActionId > 0
                    ? new Il2CppStructArray<int>(new[] { furnitureActionId, plantActionId })
                    : new Il2CppStructArray<int>(new[] { plantActionId });
                EventDispatcherTools.NotifyGlobal<int, Il2CppStructArray<int>, long, Vector3, Quaternion>(
                    EnumNotify_MapJump.CrossFloorMainActionsRequest, 0, ids, potId, Vector3.zero, Quaternion.identity);
                if (Plugin.Verbose.Value)
                    Plugin.Log.LogDebug($"Greenline Patrol: sent pot {potId} actions {furnitureActionId},{plantActionId}");
                return true;
            }
            catch (Exception e)
            {
                Broken = true;
                Plugin.Log.LogWarning($"Greenline Patrol: a chore cannot be queued as a menu click, Tend All is off: {e.Message}");
                PotGrid.MarkDirty();
                return false;
            }
        }
    }
}
