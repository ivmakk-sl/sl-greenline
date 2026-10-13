using Greenline;
using Xunit;

public class ReplantTests
{
    // Seed item id and the Config_Plant id that the seed plants.
    private static readonly (int Item, int Plant)[] SeedItems =
    {
        (15033, 33), (15021, 21), (15099, 33), (15024, 24),
    };

    [Fact]
    public void SeedForCrop_gives_the_lowest_seed_item_of_the_crop()
    {
        Assert.Equal(15033, ReplantLogic.SeedForCrop(SeedItems, 33));
        Assert.Equal(15024, ReplantLogic.SeedForCrop(SeedItems, 24));
    }

    [Fact]
    public void SeedForCrop_gives_none_for_a_crop_with_no_seed_item()
    {
        Assert.Equal(0, ReplantLogic.SeedForCrop(SeedItems, 99));
    }

    // The seed item ids of the planting window's seed list, in the window order.
    private static readonly int[] SeedList = { 15024, 15021, 15033 };

    [Fact]
    public void FindSeed_gives_the_index_of_the_last_crop_seed()
    {
        Assert.Equal((2, ReplantLogic.Reason.Plant), ReplantLogic.FindSeed(15033, SeedList));
        Assert.Equal((0, ReplantLogic.Reason.Plant), ReplantLogic.FindSeed(15024, SeedList));
    }

    [Fact]
    public void FindSeed_gives_a_reason_when_the_crop_is_unknown_or_not_in_the_list()
    {
        Assert.Equal((-1, ReplantLogic.Reason.NoLastCrop), ReplantLogic.FindSeed(0, SeedList));
        Assert.Equal((-1, ReplantLogic.Reason.SeedNotInList), ReplantLogic.FindSeed(15099, SeedList));
    }

    [Fact]
    public void Classify_plants_when_every_check_passes()
    {
        Assert.Equal(ReplantLogic.Reason.Plant, ReplantLogic.Classify(true, true, true, 5, 2).Reason);
    }

    [Theory]
    [InlineData(false, true, true, ReplantLogic.Reason.TooFewSeeds)]
    [InlineData(false, false, false, ReplantLogic.Reason.TooFewSeeds)]
    [InlineData(false, false, true, ReplantLogic.Reason.TooFewSeeds)]
    [InlineData(true, false, false, ReplantLogic.Reason.NotEnoughLight)]
    [InlineData(true, true, false, ReplantLogic.Reason.TooCold)]
    public void Classify_gives_the_first_failed_check(bool enough, bool light, bool temp, ReplantLogic.Reason reason)
    {
        Assert.Equal(reason, ReplantLogic.Classify(enough, light, temp, 1, 2).Reason);
    }

    [Fact]
    public void Classify_keeps_the_seed_counts_for_too_few_seeds()
    {
        var decision = ReplantLogic.Classify(false, true, true, 1, 2);

        Assert.Equal((ReplantLogic.Reason.TooFewSeeds, 1, 2), (decision.Reason, decision.Have, decision.Need));
    }

    private const int Premium = 15503, Compound = 15502, Basic = 15501;

    // The fertilizers of the planting window with the SpeedRate of each one.
    private static (int Item, int Count, float Speed)[] Pool(params (int Item, int Count)[] items) =>
        items.Select(i => (i.Item, i.Count, i.Item == Premium ? 1.0f : i.Item == Compound ? 0.5f : 0.3f)).ToArray();

    [Fact]
    public void PickFertilizer_takes_a_full_dose_of_a_lower_fertilizer_over_a_smaller_part_dose()
    {
        Assert.Equal(Basic, ReplantLogic.PickFertilizer(Pool((Premium, 1), (Basic, 4)), 4));
    }

    [Fact]
    public void PickFertilizer_takes_a_part_dose_when_it_gives_the_largest_speed_bonus()
    {
        Assert.Equal(Premium, ReplantLogic.PickFertilizer(Pool((Premium, 3), (Basic, 4)), 4));
    }

    [Fact]
    public void PickFertilizer_gives_a_tie_to_the_lower_fertilizer()
    {
        Assert.Equal(Compound, ReplantLogic.PickFertilizer(Pool((Premium, 1), (Compound, 5)), 2));
        Assert.Equal(Compound, ReplantLogic.PickFertilizer(Pool((Compound, 5), (Premium, 1)), 2));
    }

    [Fact]
    public void PickFertilizer_takes_a_part_dose_when_no_fertilizer_is_enough()
    {
        Assert.Equal(Basic, ReplantLogic.PickFertilizer(Pool((Basic, 1)), 2));
    }

    [Fact]
    public void PickFertilizer_gives_none_when_the_pool_has_no_fertilizer()
    {
        Assert.Equal(0, ReplantLogic.PickFertilizer(Pool((Premium, 0), (Basic, 0)), 2));
        Assert.Equal(0, ReplantLogic.PickFertilizer(Pool(), 1));
    }

    // The words of GreenWords in each display language (the game's texts of WebUI_PlantPanel_21 and _23
    // for light and cold).
    private static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        ["lastCrop"] = "Last crop:",
        ["replantStopped"] = "Replant stopped: {reason}",
        ["reasonSeeds"] = "seeds {have}/{need}",
        ["reasonLight"] = "not enough light",
        ["reasonCold"] = "too cold",
    };

    private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>
    {
        ["lastCrop"] = "上次作物：",
        ["replantStopped"] = "自动补种中止：{reason}",
        ["reasonSeeds"] = "种子 {have}/{need}",
        ["reasonLight"] = "光照不足",
        ["reasonCold"] = "温度过低",
    };

    private static ReplantLogic.Decision Stopped(ReplantLogic.Reason reason, int have = 0, int need = 0) =>
        new ReplantLogic.Decision { Reason = reason, Have = have, Need = need };

    [Theory]
    [InlineData(ReplantLogic.Reason.TooFewSeeds, 1, 2, "Replant stopped: seeds 1/2")]
    [InlineData(ReplantLogic.Reason.SeedNotInList, 0, 3, "Replant stopped: seeds 0/3")]
    [InlineData(ReplantLogic.Reason.NotEnoughLight, 3, 1, "Replant stopped: not enough light")]
    [InlineData(ReplantLogic.Reason.TooCold, 3, 1, "Replant stopped: too cold")]
    public void LineText_gives_the_last_crop_and_the_red_line_of_a_stopped_replant(ReplantLogic.Reason reason, int have, int need, string red)
    {
        var line = ReplantLogic.LineText("Watermelon", Stopped(reason, have, need), English);

        Assert.NotNull(line);
        Assert.Equal(("Last crop:", "Watermelon", red), (line.Label, line.Crop, line.Red));
    }

    [Fact]
    public void LineText_counts_no_seed_when_the_seed_is_not_in_the_list()
    {
        var line = ReplantLogic.LineText("Watermelon", Stopped(ReplantLogic.Reason.SeedNotInList, have: 5, need: 1), English);

        Assert.Equal("Replant stopped: seeds 0/1", line.Red);
    }

    [Theory]
    [InlineData(ReplantLogic.Reason.SeedNotInList, 0, 1, "自动补种中止：种子 0/1")]
    [InlineData(ReplantLogic.Reason.TooFewSeeds, 1, 2, "自动补种中止：种子 1/2")]
    [InlineData(ReplantLogic.Reason.NotEnoughLight, 3, 1, "自动补种中止：光照不足")]
    [InlineData(ReplantLogic.Reason.TooCold, 3, 1, "自动补种中止：温度过低")]
    public void LineText_is_in_Chinese_with_the_Chinese_words(ReplantLogic.Reason reason, int have, int need, string red)
    {
        var line = ReplantLogic.LineText("西瓜", Stopped(reason, have, need), Chinese);

        Assert.Equal(("上次作物：", "西瓜", red), (line.Label, line.Crop, line.Red));
    }

    [Fact]
    public void LineText_has_no_red_line_for_a_planting_window_by_hand_or_a_planting()
    {
        Assert.Equal("", ReplantLogic.LineText("Watermelon", null, English).Red);
        Assert.Equal("", ReplantLogic.LineText("Watermelon", Stopped(ReplantLogic.Reason.Plant), English).Red);
    }

    [Fact]
    public void LineText_gives_no_line_when_the_last_crop_is_unknown()
    {
        Assert.Null(ReplantLogic.LineText("", null, English));
        Assert.Null(ReplantLogic.LineText("", Stopped(ReplantLogic.Reason.NoLastCrop), English));
        Assert.Null(ReplantLogic.LineText("Watermelon", Stopped(ReplantLogic.Reason.NoLastCrop), English));
    }
}
