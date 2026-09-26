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

        // When the hover goes to page.js: a change of the hovered pot at once, a pointer move at most
        // MovesPerSecond times, and nothing while the pointer stays still.
        public sealed class Sender
        {
            public const float MovesPerSecond = 30f;

            private long lastPot = -1;
            private float lastX, lastY, nextMove;

            // Whether the hovered pot (0 for none) and the pointer place go to the page now. `now` is in
            // seconds.
            public bool Send(long pot, float x, float y, float now)
            {
                if (pot == lastPot)
                {
                    if (x == lastX && y == lastY) return false;
                    if (now < nextMove) return false;
                    nextMove = now + 1f / MovesPerSecond;
                }
                lastPot = pot;
                lastX = x;
                lastY = y;
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
