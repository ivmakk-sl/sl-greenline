using System;
using System.Collections.Generic;
using System.Linq;
using Greenline;
using SlShared.Route;
using Xunit;
using static Greenline.PatrolLogic;

public class PatrolLogicTests
{
    private const int PestBit = 1, WeedBit = 2, DroughtBit = 4;

    private static bool All(int action) => true;

    [Theory]
    [InlineData(GridLogic.PotState.Growing, PestBit, Pest)]
    [InlineData(GridLogic.PotState.Growing, PestBit | WeedBit | DroughtBit, Pest)]
    [InlineData(GridLogic.PotState.Growing, WeedBit | DroughtBit, Weed)]
    [InlineData(GridLogic.PotState.Growing, DroughtBit, Water)]
    [InlineData(GridLogic.PotState.Growing, 0, 0)]
    [InlineData(GridLogic.PotState.Growing, 8, 0)]
    [InlineData(GridLogic.PotState.Mature, 0, Harvest)]
    [InlineData(GridLogic.PotState.Mature, PestBit, Harvest)]
    [InlineData(GridLogic.PotState.Withered, 0, CleanUp)]
    [InlineData(GridLogic.PotState.Poor, 0, Till)]
    [InlineData(GridLogic.PotState.Empty, 0, 0)]
    public void ChoreFor_picks_the_chore_of_the_state_and_the_problems(GridLogic.PotState state, int flags, int chore)
    {
        Assert.Equal(chore, ChoreFor(state, flags, All));
    }

    [Fact]
    public void ChoreFor_gives_none_when_the_menu_does_not_offer_the_chore()
    {
        Assert.Equal(0, ChoreFor(GridLogic.PotState.Growing, PestBit | WeedBit, a => a != Pest));
        Assert.Equal(0, ChoreFor(GridLogic.PotState.Mature, 0, a => a != Harvest));
    }

    private static PotChore Pot(long id, int action, bool queued = false, bool autoReplant = true) =>
        new PotChore { PotId = id, FurnitureActionId = 2, ActionId = action, Queued = queued, AutoReplant = autoReplant };

    private static string Sends(PlanResult plan) => string.Join(" ", plan.Sends.Select(s => s.PotId + ":" + s.ActionId));

    [Fact]
    public void Plan_with_Auto_replant_on_puts_Till_and_Plant_right_after_the_Harvest()
    {
        var plan = Plan(new[] { Pot(1, Harvest), Pot(2, Weed) }, replantOn: true, budget: 10);

        Assert.Equal("1:1617 1:1618 1:1610 2:1614", Sends(plan));
        Assert.Equal((Outcome.All, 4), (plan.Outcome, plan.Total));
        Assert.All(plan.Sends, s => Assert.Equal(2, s.FurnitureActionId));
    }

    [Fact]
    public void Plan_with_the_pot_option_off_puts_only_Till_after_the_Harvest()
    {
        var plan = Plan(new[] { Pot(1, Harvest, autoReplant: false) }, replantOn: true, budget: 10);

        Assert.Equal("1:1617 1:1618", Sends(plan));
    }

    [Fact]
    public void Plan_with_Auto_replant_off_queues_the_Harvest_alone()
    {
        var plan = Plan(new[] { Pot(1, Harvest), Pot(2, Till) }, replantOn: false, budget: 10);

        Assert.Equal("1:1617 2:1618", Sends(plan));
        Assert.Equal(2, plan.Total);
    }

    [Fact]
    public void Plan_skips_a_pot_with_a_chore_in_the_queue()
    {
        var plan = Plan(new[] { Pot(1, Harvest, queued: true), Pot(2, Water) }, replantOn: true, budget: 10);

        Assert.Equal("2:1615", Sends(plan));
        Assert.Equal((Outcome.All, 1), (plan.Outcome, plan.Total));
    }

    [Fact]
    public void Plan_never_splits_a_Harvest_chain_and_queues_the_later_chores_that_fit()
    {
        // Budget 3: the Water fits, the Harvest chain of 3 does not fit in the 2 left, the Pest fits.
        var plan = Plan(new[] { Pot(1, Water), Pot(2, Harvest), Pot(3, Pest) }, replantOn: true, budget: 3);

        Assert.Equal("1:1615 3:1613", Sends(plan));
        Assert.Equal((Outcome.Part, 5), (plan.Outcome, plan.Total));
    }

    [Fact]
    public void Plan_with_no_chore_that_fits_is_not_enough_Stamina()
    {
        var plan = Plan(new[] { Pot(1, Harvest) }, replantOn: true, budget: 2);

        Assert.Empty(plan.Sends);
        Assert.Equal(Outcome.NoStamina, plan.Outcome);
    }

    [Fact]
    public void Plan_gives_the_outcome_of_each_text()
    {
        Assert.Equal(Outcome.NoChore, Plan(new PotChore[0], true, 10).Outcome);
        Assert.Equal(Outcome.AllQueued, Plan(new[] { Pot(1, Harvest, queued: true) }, true, 10).Outcome);
        var none = Plan(new[] { Pot(1, Harvest) }, true, 0);
        Assert.Equal(Outcome.NoStamina, none.Outcome);
        Assert.Empty(none.Sends);
    }

    [Fact]
    public void Pending_counts_each_chore_with_no_budget_cut()
    {
        var pots = new List<PotChore> { Pot(1, Harvest), Pot(2, Harvest, autoReplant: false), Pot(3, Pest), Pot(4, Weed, queued: true) };

        Assert.Equal(3 + 2 + 1, Pending(pots, replantOn: true));
        Assert.Equal(1 + 1 + 1, Pending(pots, replantOn: false));
    }

    // The Patrol route through the route library (shared/route): the pots go in and come back in route order.
    private static PotChore At(long id, int floor, float x, float z, int action = Harvest) =>
        new PotChore { PotId = id, Floor = floor, X = x, Z = z, FurnitureActionId = 2, ActionId = action };

    // The floors in a row: 101 (Basement), 1 (Home), 102 (2nd Floor), 103; the cost is the count of stairs.
    private static readonly int[] Stack = { 101, 1, 102, 103 };
    private static int Stairs(int from, int to) => Math.Abs(Array.IndexOf(Stack, from) - Array.IndexOf(Stack, to));

    private static RouteStart Home => new RouteStart { Floor = 1 };

    private static string Ids(IEnumerable<PotChore> pots) => string.Join(",", pots.Select(p => p.PotId));

    [Fact]
    public void The_route_goes_around_a_wall_between_two_near_pots()
    {
        var pots = new[] { At(1, 1, 1, 0), At(2, 1, 2, 0), At(3, 1, 10, 0) };
        double Wall((float X, float Y, float Z) a, (float X, float Y, float Z) b) =>
            (a.X == 1 && b.X == 2) || (a.X == 2 && b.X == 1) ? 100 : Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z);

        Assert.Equal("1,2,3", Ids(InRouteOrder(Home, pots, Stairs)));
        Assert.Equal("1,3,2", Ids(InRouteOrder(Home, pots, Stairs, walk: Wall)));
    }

    [Fact]
    public void The_route_does_the_floor_of_the_character_first()
    {
        var pots = new[] { At(1, 102, 0, 0), At(2, 1, 5, 0, Weed), At(3, 1, 2, 0) };

        Assert.Equal("3,2,1", Ids(InRouteOrder(Home, pots, Stairs)));
    }

    [Fact]
    public void The_kind_of_the_chore_does_not_change_the_route()
    {
        var pots = new[] { At(1, 1, 20, 0, Pest), At(2, 1, 1, 0, Weed) };

        Assert.Equal("2,1", Ids(InRouteOrder(Home, pots, Stairs)));
    }

    [Fact]
    public void Plan_on_the_route_keeps_the_chores_of_one_pot_together()
    {
        var pots = new[] { At(1, 1, 5, 0), At(2, 1, 1, 0) };

        var plan = Plan(InRouteOrder(Home, pots, Stairs), replantOn: true, budget: 10);

        Assert.Equal("2:1617 2:1618 2:1610 1:1617 1:1618 1:1610", string.Join(" ", plan.Sends.Select(s => s.PotId + ":" + s.ActionId)));
    }
}
