using Greenline;
using Xunit;

public class GridLogicTests
{
    private static GridLogic.Pot Pot(long id, int floor) => new GridLogic.Pot { PotId = id, Floor = floor };

    [Fact]
    public void Cells_of_a_floor_follow_the_pot_id_order()
    {
        var lines = GridLogic.LayoutFloors(new[] { Pot(30, 1), Pot(10, 1), Pot(20, 1) }, new int[0]);

        var line = Assert.Single(lines);
        Assert.Equal(1, line.Floor);
        Assert.Equal(new long[] { 10, 20, 30 }, line.Pots);
    }

    [Fact]
    public void Floors_follow_the_given_order_then_the_other_floors_by_id()
    {
        var pots = new[] { Pot(1, 5), Pot(2, 1), Pot(3, 102), Pot(4, 101), Pot(5, 3) };

        var lines = GridLogic.LayoutFloors(pots, new[] { 102, 1, 101 });

        Assert.Equal(new[] { 102, 1, 101, 3, 5 }, lines.Select(l => l.Floor));
    }

    [Fact]
    public void A_floor_with_no_pot_has_no_line()
    {
        var lines = GridLogic.LayoutFloors(new[] { Pot(1, 1) }, new[] { 102, 1, 101 });

        Assert.Equal(new[] { 1 }, lines.Select(l => l.Floor));
    }

    // The PlantAnomaly bits of the game.
    private const int Pest = 1, Weed = 2, Drought = 4, LightLow = 8, LightHigh = 16, Frost = 32, NeedFert = 256;

    [Fact]
    public void Badge_of_a_crop_with_Pest_is_pest()
    {
        Assert.Equal("pest", GridLogic.Badge(Pest, GridLogic.PotState.Growing));
    }

    [Theory]
    [InlineData(Frost, "frost")]
    [InlineData(Pest | Frost, "pest")]
    [InlineData(Drought | Pest, "pest")]
    [InlineData(Drought | Frost, "frost")]
    [InlineData(Drought, "drought")]
    [InlineData(Weed | LightLow, "weed")]
    [InlineData(Weed, "weed")]
    [InlineData(LightLow, "lightLow")]
    [InlineData(NeedFert, "needFert")]
    [InlineData(NeedFert | LightLow, "lightLow")]
    [InlineData(NeedFert | Weed, "weed")]
    [InlineData(NeedFert | Pest, "pest")]
    [InlineData(LightHigh, "")]
    [InlineData(0, "")]
    public void Badge_of_a_growing_crop_is_its_most_urgent_problem(int flags, string badge)
    {
        Assert.Equal(badge, GridLogic.Badge(flags, GridLogic.PotState.Growing));
    }

    [Fact]
    public void Badge_of_a_withered_crop_is_withered()
    {
        Assert.Equal("withered", GridLogic.Badge(Drought, GridLogic.PotState.Withered));
    }

    [Fact]
    public void An_empty_pot_has_no_badge()
    {
        Assert.Equal("", GridLogic.Badge(0, GridLogic.PotState.Empty));
    }

    [Fact]
    public void Time_to_mature_of_a_stalled_crop_adds_the_stalled_time()
    {
        Assert.Equal(400, GridLogic.TimeToMature(300, Drought, 900, 1000));
    }

    [Theory]
    [InlineData(0, 900)]
    [InlineData(Drought, 0)]
    public void Time_to_mature_is_the_remaining_time_with_no_problem_or_no_stall_start(int flags, int stallStart)
    {
        Assert.Equal(300, GridLogic.TimeToMature(300, flags, stallStart, 1000));
    }

    [Fact]
    public void Fert_dose_of_a_part_dose_is_the_count_over_the_pot_size()
    {
        Assert.Equal("1/4", GridLogic.FertDose(15503, 3, 4));
    }

    [Theory]
    [InlineData(0, 3, 4)]
    [InlineData(15503, 0, 4)]
    [InlineData(15503, 4, 4)]
    [InlineData(15503, 5, 4)]
    public void Fert_dose_is_empty_with_no_fertilizer_or_a_full_dose(int fertItem, int fertMissing, int potSize)
    {
        Assert.Equal("", GridLogic.FertDose(fertItem, fertMissing, potSize));
    }

    [Fact]
    public void Time_to_mature_adds_nothing_for_a_stall_start_after_now()
    {
        Assert.Equal(300, GridLogic.TimeToMature(300, Weed, 1200, 1000));
    }
}
