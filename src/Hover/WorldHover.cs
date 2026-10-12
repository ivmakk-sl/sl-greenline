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

    // The pot card of the pot under the pointer in the game world, pinned to the pot. The pot is found as
    // the game's trap hover finds its trap, which also works while the game speed is paused (the camera's
    // own hover stops then): the pointer ray hits of MouseRaycastService (cached for each frame), and the
    // nearest hit that belongs to a furniture, by ColliderMapManager. The card's anchor is the middle of the
    // collider of that hit on the screen, so it stays still while the pointer moves on the pot. The pot and
    // its place go to the page by the rule of HoverLogic.Sender, with no result: the page reports an error
    // by message (PageMessages). The config entry WorldHoverCard turns it off; an error turns it off until
    // the next game start, and the rest of the mod keeps working.
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
                bool overUi = MouseRaycastService.IsPointerOverUI();
                var center = Vector3.zero;
                long hovered = overUi ? 0 : HoveredFurniture(world, out center);
                // PotAt runs on each check: a pot placed under a still pointer joins the scan later.
                long pot = PotGrid.PotAt(hovered);
                if (hovered != lastHovered || pot != hoveredPot)
                {
                    lastHovered = hovered;
                    hoveredPot = pot;
                    if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline hover: id={hovered} pot={hoveredPot}");
                }
                var place = Vector3.zero;
                if (hoveredPot != 0)
                {
                    var camera = Camera.main;
                    if (camera != null) place = camera.WorldToScreenPoint(center);
                }
                if (sender.Send(hoveredPot, place.x, place.y, overUi))
                {
                    PageScript.SetHover(hoveredPot, place.x / Screen.width, 1f - place.y / Screen.height, overUi);
                    if (Plugin.Verbose.Value) Plugin.Log.LogDebug($"Greenline hover send: pot={hoveredPot} place={Mathf.Round(place.x)},{Mathf.Round(place.y)}");
                }
            }
            catch (Exception e)
            {
                TurnOff(e.Message);
            }
        }

        // The InstanceId of the furniture under the pointer, or 0 over no furniture, with the middle of the
        // collider of its nearest hit in the world.
        private static long HoveredFurniture(BattleShowWorld world, out Vector3 center)
        {
            center = Vector3.zero;
            var map = world._ColliderMapManager;
            if (map == null) return 0;
            int count = MouseRaycastService.GetMouseHits(out var hits);
            if (hits == null) return 0;
            var furniture = new List<(float Distance, long Id)>();
            var centers = new List<Vector3>();
            for (int i = 0; i < count && i < hits.Length; i++)
            {
                var hit = hits[i];
                var collider = hit.collider;
                if (collider == null) continue;
                furniture.Add((hit.distance, map.GetInstanceId(collider.GetInstanceID())));
                centers.Add(collider.bounds.center);
            }
            long nearest = HoverLogic.Nearest(furniture);
            float best = float.MaxValue;
            for (int i = 0; i < furniture.Count; i++)
                if (furniture[i].Id == nearest && furniture[i].Distance < best) (best, center) = (furniture[i].Distance, centers[i]);
            return nearest;
        }

        // Also called with the error text of the page script.
        internal static void TurnOff(string reason)
        {
            if (failed) return;
            failed = true;
            Plugin.Log.LogWarning($"Greenline: the world hover card is off until the next game start: {reason}");
            try { PageScript.SetHover(0, 0f, 0f, false); } catch (Exception) { /* the card goes when the HUD reloads */ }
        }

        private static void Hide()
        {
            if (sender.Hide()) PageScript.SetHover(0, 0f, 0f, false);
            lastHovered = -1;
        }
    }
}
