using Greenline;
using Xunit;

public class GridLogicTests
{
    // The climate areas (EnvArea ids of the game).
    private const int Interior = 1, Sunroom = 2, Terrace = 3, ColdStorage = 4;

    private static GridLogic.Pot Pot(long id, int floor, int area = Interior) => new GridLogic.Pot { PotId = id, Floor = floor, Area = area };

    private static string Lines(GridLogic.Grid grid) =>
        string.Join(" ", grid.Lines.Select(l => (l.IsFloor ? "F" + l.Floor : "Z" + l.Area) + "@" + l.Row));

    private static string Place(GridLogic.Grid grid, long pot)
    {
        var cell = grid.Cells.Single(c => c.PotId == pot);
        return cell.Row + ":" + cell.Column;
    }

    [Fact]
    public void Pots_on_two_floors_all_in_Interior_give_two_floor_lines_and_no_zone_line()
    {
        var pots = new[] { Pot(1, 102), Pot(2, 102), Pot(3, 1), Pot(4, 1), Pot(5, 1) };

        var grid = GridLogic.Layout(pots, new[] { 102, 1 });

        Assert.Equal("F102@1 F1@3", Lines(grid));
        Assert.Equal("2:1", Place(grid, 1));
        Assert.Equal("2:2", Place(grid, 2));
        Assert.Equal("4:3", Place(grid, 5));
        Assert.Equal(4, grid.Rows);
        Assert.Equal(3, grid.Columns);
    }

    [Fact]
    public void Two_climate_areas_on_a_floor_put_the_main_area_under_the_floor_line_and_a_zone_line_over_the_other()
    {
        var pots = new[] { Pot(4, 102, Terrace), Pot(1, 102), Pot(5, 102, Terrace), Pot(2, 102), Pot(3, 102) };

        var grid = GridLogic.Layout(pots, new[] { 102 });

        Assert.Equal("F102@1 Z3@3", Lines(grid));
        Assert.Equal("2:1", Place(grid, 1));
        Assert.Equal("2:3", Place(grid, 3));
        Assert.Equal("4:1", Place(grid, 4));
        Assert.Equal("4:2", Place(grid, 5));
    }

    [Fact]
    public void A_floor_with_only_a_terrace_has_no_zone_line_and_the_terrace_values_on_its_floor_line()
    {
        var grid = GridLogic.Layout(new[] { Pot(1, 102, Terrace), Pot(2, 102, Terrace) }, new[] { 102 });

        Assert.Equal("F102@1", Lines(grid));
        Assert.Equal(Terrace, grid.Lines[0].Area);
    }

    [Fact]
    public void With_no_Interior_the_lowest_area_is_the_main_area()
    {
        var pots = new[] { Pot(1, 1, ColdStorage), Pot(2, 1, Terrace), Pot(3, 1, ColdStorage) };

        var grid = GridLogic.Layout(pots, new[] { 1 });

        Assert.Equal("F1@1 Z4@3", Lines(grid));
        Assert.Equal(Terrace, grid.Lines[0].Area);
        Assert.Equal(ColdStorage, grid.Lines[1].Area);
        Assert.Equal("2:1", Place(grid, 2));
        Assert.Equal("4:1", Place(grid, 1));
        Assert.Equal("4:2", Place(grid, 3));
    }

    [Fact]
    public void Eight_pots_of_one_area_continue_on_a_second_line_of_cells()
    {
        var pots = Enumerable.Range(1, 8).Select(i => Pot(i, 1)).ToArray();

        var grid = GridLogic.Layout(pots, new[] { 1 });

        Assert.Equal("F1@1", Lines(grid));
        Assert.Equal("2:6", Place(grid, 6));
        Assert.Equal("2:7", Place(grid, 7));
        Assert.Equal("3:1", Place(grid, 8));
        Assert.Equal(7, grid.Columns);
    }

    [Fact]
    public void Pots_with_no_area_give_one_zone_for_each_floor()
    {
        var grid = GridLogic.Layout(new[] { Pot(1, 102, 0), Pot(2, 102, 0), Pot(3, 1, 0) }, new[] { 102, 1 });

        Assert.Equal("F102@1 F1@3", Lines(grid));
        Assert.Equal(0, grid.Lines[0].Area);
    }

    [Fact]
    public void Cells_of_an_area_follow_the_pot_id_order_also_for_big_ids()
    {
        var pots = new[] { Pot(4295003822, 1), Pot(4294974098, 1), Pot(4294992680, 1) };

        var grid = GridLogic.Layout(pots, new int[0]);

        Assert.Equal("2:1", Place(grid, 4294974098));
        Assert.Equal("2:2", Place(grid, 4294992680));
        Assert.Equal("2:3", Place(grid, 4295003822));
    }

    [Fact]
    public void The_cells_come_in_row_and_column_order()
    {
        var pots = new[] { Pot(5, 1, Terrace), Pot(1, 1), Pot(3, 102), Pot(2, 1) };

        var grid = GridLogic.Layout(pots, new[] { 102, 1 });

        Assert.Equal(new long[] { 3, 1, 2, 5 }, grid.Cells.Select(c => c.PotId));
    }

    [Fact]
    public void Floors_follow_the_given_order_then_the_other_floors_by_id()
    {
        var pots = new[] { Pot(1, 5), Pot(2, 1), Pot(3, 102), Pot(4, 101), Pot(5, 3) };

        var grid = GridLogic.Layout(pots, new[] { 102, 1, 101 });

        Assert.Equal(new[] { 102, 1, 101, 3, 5 }, grid.Lines.Select(l => l.Floor));
    }

    [Fact]
    public void A_floor_with_no_pot_has_no_line()
    {
        var grid = GridLogic.Layout(new[] { Pot(1, 1) }, new[] { 102, 1, 101 });

        Assert.Equal(new[] { 1 }, grid.Lines.Select(l => l.Floor));
    }

    [Fact]
    public void Line_keys_name_the_floor_and_the_area()
    {
        var grid = GridLogic.Layout(new[] { Pot(1, 102), Pot(2, 102, Sunroom) }, new[] { 102 });

        Assert.Equal(new[] { "f102", "z102:2" }, grid.Lines.Select(l => l.Key));
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
