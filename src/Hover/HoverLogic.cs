using System;
using System.Collections.Generic;

namespace Greenline
{
    // The game-free logic of the world hover card. No game or BepInEx type here, so the tests can link
    // this file.
    public static class HoverLogic
    {
        // The furniture under the pointer: the id of the nearest pointer ray hit (distance and furniture
        // InstanceId, 0 for a hit that is no furniture), or 0 for none. A furniture in front of a pot hides
        // it, as in the game.
        public static long Nearest(IEnumerable<(float Distance, long Id)> hits)
        {
            long nearest = 0;
            float nearestDistance = float.MaxValue;
            foreach (var (distance, id) in hits)
            {
                if (id <= 0 || distance >= nearestDistance) continue;
                nearest = id;
                nearestDistance = distance;
            }
            return nearest;
        }

        // When the world pot card goes to the page: at a change of the pot under the pointer, at a move of
        // that pot on the screen by a pixel or more while its card shows (a camera move, so the card follows
        // the pot), and once when the pointer leaves the web UI (the card of a grid cell gets no mouseleave
        // then). Nothing for a pointer move on the same pot, and nothing while no pot is under the pointer.
        public sealed class Sender
        {
            private long lastPot;
            private int lastX, lastY;
            private bool wasOverUi;

            // Whether the pot under the pointer (0 for none) and its place on the screen in pixels go to the
            // page now. `overUi`: the pointer is over the web UI.
            public bool Send(long pot, float x, float y, bool overUi)
            {
                bool leftUi = wasOverUi && !overUi;
                wasOverUi = overUi;
                int px = (int)Math.Round(x), py = (int)Math.Round(y);
                if (pot == lastPot)
                {
                    if (pot == 0) return leftUi;
                    if (px == lastX && py == lastY) return false;
                }
                lastPot = pot;
                lastX = px;
                lastY = py;
                return true;
            }

            // Forgets the shown pot. True when a pot card shows, so the page must hide it.
            public bool Hide()
            {
                bool shown = lastPot > 0;
                lastPot = 0;
                return shown;
            }
        }
    }
}
