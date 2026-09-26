using System;
using System.Collections.Generic;
using HarmonyLib;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Show;
using UnityEngine;

namespace Greenline
{
    // The game's trap hover runs while the game speed is paused, when ActionManager.Update does not, and
    // the player can still open a pot menu then. A second check in the same frame returns at once.
    [HarmonyPatch(typeof(TrapShowManager), "OnUpdate")]
    internal static class HoverOnTrapUpdate
    {
        private static void Postfix()
        {
            WorldHover.Check();
        }
    }

    // The hover card of the pot under the pointer in the game world. The pot is found as the game's
    // trap hover finds its trap, which also works while the game speed is paused (the camera's own hover
    // stops then): the pointer ray hits of MouseRaycastService (cached for each frame), and the nearest
    // hit that belongs to a furniture, by ColliderMapManager. The game passes pointer moves to the page only over
    // the HUD parts, so the hovered pot and the pointer place go to page.js by the rule of HoverLogic.Sender:
    // the world card follows the pointer, and the card of a grid cell goes when the pointer leaves the HUD. The
    // config entry WorldHoverCard turns it off; an error turns it off until the next game start, and the
    // rest of the mod keeps working.
    internal static class WorldHover
    {
        private static readonly HoverLogic.Sender sender = new HoverLogic.Sender();
        private static long lastHovered = -1;
        private static long hoveredPot;
        private static int lastFrame = -1;
        private static bool failed;

        internal static void Check()
        {
            if (failed || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            try
            {
                if (!Plugin.WorldHoverCard.Value)
                {
                    Hide();
                    return;
                }
                var world = BaseSingleton<BattleShowWorld>.Instance;
                if (world == null) return;
                long hovered = HoveredFurniture(world);
                // PotAt runs on each check: a pot placed under a still pointer joins the scan later.
                long pot = PotGrid.PotAt(hovered);
                if (hovered != lastHovered || pot != hoveredPot)
                {
                    lastHovered = hovered;
                    hoveredPot = pot;
                    if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline hover: id={hovered} pot={hoveredPot}");
                }
                var mouse = Input.mousePosition;
                if (sender.Send(hoveredPot, mouse.x, mouse.y, Time.realtimeSinceStartup))
                    PageScript.SetHover(hoveredPot, mouse.x / Screen.width, 1f - mouse.y / Screen.height);
            }
            catch (Exception e)
            {
                TurnOff(e.Message);
            }
        }

        // The InstanceId of the furniture under the pointer, or 0 when the pointer is over the web UI or
        // over no furniture.
        private static long HoveredFurniture(BattleShowWorld world)
        {
            if (MouseRaycastService.IsPointerOverUI()) return 0;
            var map = world._ColliderMapManager;
            if (map == null) return 0;
            int count = MouseRaycastService.GetMouseHits(out var hits);
            if (hits == null) return 0;
            var furniture = new List<(float Distance, long Id)>();
            for (int i = 0; i < count && i < hits.Length; i++)
            {
                var hit = hits[i];
                var collider = hit.collider;
                if (collider != null) furniture.Add((hit.distance, map.GetInstanceId(collider.GetInstanceID())));
            }
            return HoverLogic.Nearest(furniture);
        }

        // Also called with the error text of the page script.
        internal static void TurnOff(string reason)
        {
            if (failed) return;
            failed = true;
            Plugin.Log.LogWarning($"Greenline: the world hover card is off until the next game start: {reason}");
            try { PageScript.SetHover(0, 0f, 0f); } catch (Exception) { /* the card goes when the HUD reloads */ }
        }

        private static void Hide()
        {
            if (sender.Hide()) PageScript.SetHover(0, 0f, 0f);
            lastHovered = -1;
        }
    }
}
