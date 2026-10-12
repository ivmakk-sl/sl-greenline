using System;
using System.Collections.Generic;
using static Greenline.PatrolLogic;

namespace Greenline
{
    // The Patrol route: the order of the pots with a chore for a short walk of the character. The floor of
    // the character comes first, then each time the floor that the stairs reach first from the floor before
    // (a tie goes to the lower floor id). On a floor, the walk is the shortest order through all its pots up to
    // ExactLimit pots; with more, the nearest pot comes next, and 2-opt passes then reverse a part of the walk
    // while that shortens it. The distance is the walk length that `walk` gives
    // (in the game the path of the navigation mesh, which goes around walls), else the straight one on the
    // floor plane (x, z). The kind of a chore and the time left of a crop do not count. No game or BepInEx type
    // here.
    public static class RouteLogic
    {
        // The floor and the place of the character when Patrol starts.
        public struct Start
        {
            public int Floor;
            public float X, Y, Z;
        }

        // The pots in route order. `floorCost` gives the stairs between two floors; `stairs` gives the place
        // where the walk enters a floor, or null when the game gives none: the walk then starts from the place
        // of the last pot of the floor before. `walk` gives the walk length between two places on one floor, or
        // null for the straight distance.
        public static List<PotChore> Order(Start start, IList<PotChore> pots, Func<int, int, int> floorCost,
            Func<int, (float X, float Z)?> stairs,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk = null)
        {
            walk ??= Straight;
            var byFloor = new Dictionary<int, List<PotChore>>();
            foreach (var pot in pots)
            {
                if (!byFloor.TryGetValue(pot.Floor, out var list)) byFloor[pot.Floor] = list = new List<PotChore>();
                list.Add(pot);
            }

            var route = new List<PotChore>();
            int floor = start.Floor;
            var place = (start.X, start.Y, start.Z);
            bool first = true;
            while (byFloor.Count > 0)
            {
                int next = NextFloor(floor, byFloor.Keys, floorCost);
                if (!first || next != start.Floor)
                {
                    var entry = stairs(next);
                    if (entry.HasValue) place = (entry.Value.X, place.Item2, entry.Value.Z);
                }
                first = false;
                var path = Walk(place, byFloor[next], walk);
                byFloor.Remove(next);
                route.AddRange(path);
                floor = next;
                place = Place(path[path.Count - 1]);
            }
            return route;
        }

        private static int NextFloor(int from, IEnumerable<int> floors, Func<int, int, int> floorCost)
        {
            int best = 0, bestCost = int.MaxValue;
            bool any = false;
            foreach (int floor in floors)
            {
                int cost = floor == from ? -1 : floorCost(from, floor);
                if (!any || cost < bestCost || (cost == bestCost && floor < best)) (best, bestCost, any) = (floor, cost, true);
            }
            return best;
        }

        // The most pots of one floor for the exact shortest walk: its table has 2^n * n entries, about 50000
        // for 12 pots, done once for each click of Tend All.
        private const int ExactLimit = 12;

        // The walk on one floor from the entry place: the shortest order through all its pots (Held-Karp on the
        // open path) up to ExactLimit pots, else nearest neighbor, then 2-opt.
        private static List<PotChore> Walk((float X, float Y, float Z) entry, List<PotChore> pots,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk)
        {
            if (pots.Count <= ExactLimit) return Shortest(entry, pots, walk);
            return NearestThen2Opt(entry, pots, walk);
        }

        // best[set, last]: the shortest walk from the entry through the pots of `set` (a bit for each pot) that
        // ends at pot `last`.
        private static List<PotChore> Shortest((float X, float Y, float Z) entry, List<PotChore> pots,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk)
        {
            int n = pots.Count;
            if (n == 0) return new List<PotChore>();
            var d = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    d[i, j] = i == j ? 0 : walk(Place(pots[i]), Place(pots[j]));
            int full = (1 << n) - 1;
            var best = new double[1 << n, n];
            var from = new int[1 << n, n];
            for (int set = 1; set <= full; set++)
                for (int last = 0; last < n; last++)
                {
                    best[set, last] = double.MaxValue;
                    if ((set & (1 << last)) == 0) continue;
                    int rest = set & ~(1 << last);
                    if (rest == 0)
                    {
                        best[set, last] = walk(entry, Place(pots[last]));
                        from[set, last] = -1;
                        continue;
                    }
                    for (int prev = 0; prev < n; prev++)
                    {
                        if ((rest & (1 << prev)) == 0 || best[rest, prev] == double.MaxValue) continue;
                        double length = best[rest, prev] + d[prev, last];
                        if (length < best[set, last]) (best[set, last], from[set, last]) = (length, prev);
                    }
                }
            int end = 0;
            for (int last = 1; last < n; last++)
                if (best[full, last] < best[full, end]) end = last;
            var path = new List<PotChore>();
            for (int set = full, at = end; at >= 0;)
            {
                path.Add(pots[at]);
                int prev = from[set, at];
                set &= ~(1 << at);
                at = prev;
            }
            path.Reverse();
            return path;
        }

        private static List<PotChore> NearestThen2Opt((float X, float Y, float Z) entry, List<PotChore> pots,
            Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk)
        {
            var left = new List<PotChore>(pots);
            var path = new List<PotChore>();
            var here = entry;
            while (left.Count > 0)
            {
                int nearest = 0;
                double best = walk(here, Place(left[0]));
                for (int i = 1; i < left.Count; i++)
                {
                    double d = walk(here, Place(left[i]));
                    if (d < best) (nearest, best) = (i, d);
                }
                var pot = left[nearest];
                left.RemoveAt(nearest);
                path.Add(pot);
                here = Place(pot);
            }

            // A home has a few dozen pots at most, so the passes cost nothing.
            bool shorter = true;
            while (shorter)
            {
                shorter = false;
                for (int i = 0; i < path.Count; i++)
                    for (int k = i + 1; k < path.Count; k++)
                    {
                        var before = i == 0 ? entry : Place(path[i - 1]);
                        double delta = walk(before, Place(path[k])) - walk(before, Place(path[i]));
                        if (k + 1 < path.Count) delta += walk(Place(path[i]), Place(path[k + 1])) - walk(Place(path[k]), Place(path[k + 1]));
                        if (delta >= -1e-4) continue;
                        path.Reverse(i, k - i + 1);
                        shorter = true;
                    }
            }
            return path;
        }

        private static (float X, float Y, float Z) Place(PotChore pot) => (pot.X, pot.Y, pot.Z);

        private static double Straight((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
            Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
