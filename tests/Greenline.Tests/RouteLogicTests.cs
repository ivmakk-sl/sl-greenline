using System;
using System.Collections.Generic;
using System.Linq;
using Greenline;
using Xunit;
using static Greenline.PatrolLogic;

public class RouteLogicTests
{
    private static PotChore Pot(long id, int floor, float x, float z, int action = Harvest) =>
        new PotChore { PotId = id, Floor = floor, X = x, Z = z, FurnitureActionId = 2, ActionId = action };

    // The floors in a row: 101 (Basement), 1 (Home), 102 (2nd Floor), 103; the cost is the count of stairs.
    private static readonly int[] Stack = { 101, 1, 102, 103 };
    private static int Stairs(int from, int to) => Math.Abs(Array.IndexOf(Stack, from) - Array.IndexOf(Stack, to));

    private static (float X, float Z)? NoStairsPlace(int floor) => null;

    private static string Order(RouteLogic.Start start, IList<PotChore> pots,
        Func<int, int, int> cost = null, Func<int, (float X, float Z)?> stairs = null) =>
        string.Join(",", RouteLogic.Order(start, pots, cost ?? Stairs, stairs ?? NoStairsPlace).Select(p => p.PotId));

    private static RouteLogic.Start At(int floor, float x = 0, float z = 0) => new RouteLogic.Start { Floor = floor, X = x, Z = z };

    // A walk length with a wall between the places (1, 0) and (2, 0): the walk around it is 100 long.
    private static double WallBetween1And2((float X, float Y, float Z) a, (float X, float Y, float Z) b)
    {
        bool wall = (a.X == 1 && b.X == 2) || (a.X == 2 && b.X == 1);
        return wall ? 100 : Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }

    [Fact]
    public void The_walk_length_goes_around_a_wall_between_two_near_pots()
    {
        // In a straight line 1 and 2 are next to each other; with the wall, the walk 1, 3, 2 is shorter.
        var pots = new[] { Pot(1, 1, 1, 0), Pot(2, 1, 2, 0), Pot(3, 1, 10, 0) };

        Assert.Equal("1,2,3", Order(At(1), pots));
        Assert.Equal("1,3,2", string.Join(",", RouteLogic.Order(At(1), pots, Stairs, NoStairsPlace, WallBetween1And2).Select(p => p.PotId)));
    }

    // The length of a walk from (0, 0) through the pots in order, with the distance of WallBetween1And2 or the
    // straight one.
    private static double Length(IEnumerable<PotChore> order, Func<(float X, float Y, float Z), (float X, float Y, float Z), double> walk)
    {
        double sum = 0;
        var here = (0f, 0f, 0f);
        foreach (var pot in order)
        {
            sum += walk(here, (pot.X, pot.Y, pot.Z));
            here = (pot.X, pot.Y, pot.Z);
        }
        return sum;
    }

    private static IEnumerable<List<PotChore>> Permutations(List<PotChore> pots)
    {
        if (pots.Count <= 1) { yield return new List<PotChore>(pots); yield break; }
        for (int i = 0; i < pots.Count; i++)
        {
            var rest = new List<PotChore>(pots);
            var first = rest[i];
            rest.RemoveAt(i);
            foreach (var tail in Permutations(rest)) { tail.Insert(0, first); yield return tail; }
        }
    }

    private static double Straight((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    [Fact]
    public void The_route_of_a_floor_is_the_shortest_walk_through_all_its_pots()
    {
        var random = new Random(68);
        for (int round = 0; round < 200; round++)
        {
            int count = 2 + random.Next(6);
            var pots = Enumerable.Range(1, count).Select(i => Pot(i, 1, random.Next(-20, 21), random.Next(-20, 21))).ToList();

            var route = RouteLogic.Order(At(1), pots, Stairs, NoStairsPlace, Straight);

            double best = Permutations(pots).Min(p => Length(p, Straight));
            Assert.True(Math.Abs(Length(route, Straight) - best) < 1e-3, $"round {round}: {Length(route, Straight)} against the shortest {best}");
        }
    }

    [Fact]
    public void A_floor_with_more_than_twelve_pots_walks_a_row_with_no_walk_back()
    {
        // 14 pots in a row, given out of order: the nearest neighbor and 2-opt walk the row from its near end.
        var pots = new[] { 7, 2, 13, 4, 11, 1, 9, 14, 3, 6, 12, 5, 10, 8 }.Select(i => Pot(i, 1, i, 0)).ToList();

        Assert.Equal(string.Join(",", Enumerable.Range(1, 14)), Order(At(1), pots));
    }

    [Fact]
    public void The_walk_length_has_the_heights_of_the_places()
    {
        var pots = new[] { new PotChore { PotId = 1, Floor = 1, X = 1, Y = 3, Z = 0 } };
        (float X, float Y, float Z)? seen = null;

        RouteLogic.Order(new RouteLogic.Start { Floor = 1, X = 0, Y = 3, Z = 0 }, pots, Stairs, NoStairsPlace, (a, b) => { seen = b; return 1; });

        Assert.Equal((1f, 3f, 0f), seen);
    }

    [Fact]
    public void The_floor_of_the_character_comes_first_then_the_other_floor()
    {
        var pots = new[] { Pot(1, 102, 0, 0), Pot(2, 1, 5, 0, Weed), Pot(3, 1, 2, 0) };

        Assert.Equal("3,2,1", Order(At(1), pots));
    }

    [Fact]
    public void One_large_floor_has_no_walk_back()
    {
        var pots = new[] { Pot(1, 1, 20, 0), Pot(2, 1, 1, 0), Pot(3, 1, 21, 0), Pot(4, 1, 2, 0) };

        Assert.Equal("2,4,1,3", Order(At(1), pots));
    }

    [Fact]
    public void The_kind_of_the_chore_does_not_change_the_route()
    {
        var pots = new[] { Pot(1, 1, 20, 0, Pest), Pot(2, 1, 1, 0, Weed) };

        Assert.Equal("2,1", Order(At(1), pots));
    }

    [Fact]
    public void With_no_chore_on_the_floor_of_the_character_the_nearest_floor_comes_first()
    {
        var pots = new[] { Pot(1, 103, 0, 0), Pot(2, 101, 0, 0) };

        Assert.Equal("2,1", Order(At(1), pots));
    }

    [Fact]
    public void Each_next_floor_is_the_one_of_the_lowest_cost_from_the_floor_before()
    {
        var pots = new[] { Pot(1, 101, 0, 0), Pot(2, 102, 0, 0), Pot(3, 103, 0, 0) };

        // From Home, the Basement and the 2nd Floor tie; then from the Basement the 2nd Floor is nearer.
        Assert.Equal("1,2,3", Order(At(1), pots));
    }

    [Fact]
    public void A_tie_of_floor_cost_goes_to_the_lower_floor_id()
    {
        var pots = new[] { Pot(1, 102, 0, 0), Pot(2, 101, 0, 0) };

        Assert.Equal("2,1", Order(At(1), pots, cost: (a, b) => a == b ? 0 : 1));
    }

    [Fact]
    public void One_pot_and_no_pot()
    {
        Assert.Equal("7", Order(At(1), new[] { Pot(7, 102, 3, 3) }));
        Assert.Equal("", Order(At(1), new PotChore[0]));
    }

    [Fact]
    public void Two_opt_shortens_a_nearest_neighbor_walk_that_goes_back()
    {
        var pots = new[] { Pot(1, 1, 7, 1), Pot(2, 1, 4, 2), Pot(3, 1, 1, 3), Pot(4, 1, 2, 1) };

        // Nearest neighbor alone: 4,2,1,3 (13.96, back from (7,1) to (1,3)); the best walk is 10.80.
        Assert.Equal("4,3,2,1", Order(At(1), pots));
    }

    [Fact]
    public void A_later_floor_starts_at_its_stairs_place_when_the_game_gives_it()
    {
        var pots = new[] { Pot(1, 1, 10, 0), Pot(2, 102, 0, 0), Pot(3, 102, 11, 0) };

        Assert.Equal("1,2,3", Order(At(1, 9, 0), pots, stairs: f => f == 102 ? (0f, 0f) : ((float, float)?)null));
    }

    [Fact]
    public void A_later_floor_starts_at_the_last_pot_of_the_floor_before_with_no_stairs_place()
    {
        var pots = new[] { Pot(1, 1, 10, 0), Pot(2, 102, 0, 0), Pot(3, 102, 11, 0) };

        Assert.Equal("1,3,2", Order(At(1, 9, 0), pots));
    }

    [Fact]
    public void Plan_on_the_route_keeps_the_chores_of_one_pot_together()
    {
        var pots = new[] { Pot(1, 1, 5, 0), Pot(2, 1, 1, 0) };

        var plan = Plan(RouteLogic.Order(At(1), pots, Stairs, NoStairsPlace), replantOn: true, budget: 10);

        Assert.Equal("2:1617 2:1618 2:1610 1:1617 1:1618 1:1610", string.Join(" ", plan.Sends.Select(s => s.PotId + ":" + s.ActionId)));
    }
}
